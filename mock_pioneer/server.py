#!/usr/bin/env python3
"""
Mock Pioneer server — replaces the physical Pioneer robot during development.

Three concurrent TCP services:
  1234  Control listener  — receives $[cmd][turn:3][speed:3]\\n drive commands
  1235  Camera streamer   — sends length-prefixed JPEG frames from the webcam
  1236  Gaze listener     — receives $GAZE[u:3][v:3]\\n gaze coordinates

Usage:
    python server.py --mode uniform --quality 20
    python server.py --mode gaze --periph-quality 15 --fovea-quality 85

See docs/PROTOCOL.md for the full payload format reference.
"""

import argparse
import queue
import random
import socket
import struct
import threading
import time
from typing import Optional
import math

import cv2
import numpy as np

from foveation import encode_uniform, encode_dual_payload
from metrics_server import MetricsServer

# ── Shared state ─────────────────────────────────────────────────────────────

_gaze_lock = threading.Lock()
_latest_gaze: tuple[float, float] = (0.5, 0.5)
_gaze_timestamp: float = 0.0          # epoch seconds of last valid gaze update
_GAZE_STALE_TIMEOUT_S: float = 0.5   # fall back to centre after 500 ms

_metrics: Optional[MetricsServer] = None

def _generate_synthetic_frame() -> np.ndarray:
    """Generate a 640x480 synthetic frame with a moving circle and grid pattern."""
    frame = np.zeros((480, 640, 3), dtype=np.uint8)
    # Draw a grid
    for x in range(0, 640, 80):
        cv2.line(frame, (x, 0), (x, 480), (40, 40, 40), 1)
    for y in range(0, 480, 80):
        cv2.line(frame, (0, y), (640, y), (40, 40, 40), 1)
    # Bouncing ball based on current time
    t = time.time()
    cx = int(320 + 200 * math.sin(t * 2))
    cy = int(240 + 150 * math.cos(t * 1.5))
    # Green ball
    cv2.circle(frame, (cx, cy), 40, (0, 255, 0), -1)
    
    cv2.putText(frame, "SYNTHETIC FEED (Webcam Offline)", (10, 450),
                cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 1)
    return frame

# Set by camera_thread at startup; read by control/gaze threads for CSV logging
_current_mode: str = "uniform"
_current_quality: int = 20

# Thread-safe queue for passing frames to the main thread for display (macOS requirement)
_preview_queue = queue.Queue(maxsize=1)


def _get_gaze() -> tuple[float, float, float]:
    """Return the latest non-stale gaze UV and its timestamp, or (0.5, 0.5, 0.0) if stale / never set."""
    with _gaze_lock:
        age = time.time() - _gaze_timestamp
        if _gaze_timestamp == 0.0 or age > _GAZE_STALE_TIMEOUT_S:
            return (0.5, 0.5, 0.0)
        return (_latest_gaze[0], _latest_gaze[1], _gaze_timestamp)


def _set_gaze(u: float, v: float) -> None:
    global _latest_gaze, _gaze_timestamp
    with _gaze_lock:
        _latest_gaze = (u, v)
        _gaze_timestamp = time.time()


# ── Helpers ───────────────────────────────────────────────────────────────────

def _make_server_socket(port: int) -> socket.socket:
    """Create a listening TCP socket with SO_REUSEADDR on the given port."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind(("", port))
    sock.listen(1)
    print(f"[Server] Listening on port {port}")
    return sock


def _recv_line(conn: socket.socket, max_bytes: int = 64) -> Optional[str]:
    """
    Read bytes from conn until '\\n', up to max_bytes.
    Returns the decoded string (stripped) or None on connection close.
    Raises ValueError on malformed data.
    """
    buf = b""
    while len(buf) < max_bytes:
        chunk = conn.recv(1)
        if not chunk:
            return None
        buf += chunk
        if buf.endswith(b"\n"):
            return buf.decode("ascii").strip()
    raise ValueError(f"Line exceeded {max_bytes} bytes without newline: {buf!r}")


# ── Control listener (port 1234) ─────────────────────────────────────────────

def _parse_control(line: str) -> tuple[int, int, int]:
    """
    Parse '$[cmd][turn:3][speed:3]' → (cmd, turn, speed).
    Clamps values to valid ranges.
    """
    if not line.startswith("$") or len(line) < 8:
        raise ValueError(f"Malformed control command: {line!r}")
    cmd   = int(line[1])
    turn  = int(line[2:5])
    speed = int(line[5:8])
    cmd   = max(0, min(2, cmd))
    turn  = max(0, min(180, turn))
    speed = max(0, min(512, speed))
    return cmd, turn, speed


def control_thread(stop_event: threading.Event) -> None:
    server_sock = _make_server_socket(1234)
    server_sock.settimeout(1.0)
    while not stop_event.is_set():
        try:
            conn, addr = server_sock.accept()
        except socket.timeout:
            continue
        print(f"[Control] Client connected: {addr}")
        try:
            conn.settimeout(5.0)
            while not stop_event.is_set():
                try:
                    line = _recv_line(conn)
                except socket.timeout:
                    continue
                if line is None:
                    print("[Control] Client disconnected.")
                    break
                try:
                    cmd, turn, speed = _parse_control(line)
                    ts = time.time()
                    print(f"[Control] t={ts:.3f}  cmd={cmd}  turn={turn}  speed={speed}")
                    if _metrics:
                        _metrics.log(
                            "cmd_received",
                            cmd_received=f"{cmd},{turn},{speed}",
                            quality_mode=_current_mode,
                        )
                except ValueError as exc:
                    print(f"[Control] Parse error: {exc}")
        except Exception as exc:
            print(f"[Control] Error: {exc}")
        finally:
            try:
                conn.close()
            except Exception:
                pass
    server_sock.close()


# ── Gaze listener (port 1236) ─────────────────────────────────────────────────

def _parse_gaze(line: str) -> tuple[float, float]:
    """
    Parse '$GAZE[u:3][v:3]' → (u_norm, v_norm) in [0.0, 1.0].
    """
    if not line.startswith("$GAZE") or len(line) < 11:
        raise ValueError(f"Malformed gaze message: {line!r}")
    u_int = int(line[5:8])
    v_int = int(line[8:11])
    u_int = max(0, min(999, u_int))
    v_int = max(0, min(999, v_int))
    return u_int / 999.0, v_int / 999.0


def gaze_thread(stop_event: threading.Event) -> None:
    server_sock = _make_server_socket(1236)
    server_sock.settimeout(1.0)
    while not stop_event.is_set():
        try:
            conn, addr = server_sock.accept()
        except socket.timeout:
            continue
        print(f"[Gaze] Client connected: {addr}")
        try:
            conn.settimeout(2.0)
            while not stop_event.is_set():
                try:
                    line = _recv_line(conn, max_bytes=16)
                except socket.timeout:
                    continue
                if line is None:
                    print("[Gaze] Client disconnected.")
                    break
                try:
                    u, v = _parse_gaze(line)
                    _set_gaze(u, v)
                    if _metrics:
                        _metrics.log(
                            "gaze_received",
                            gaze_uv=f"{u:.3f},{v:.3f}",
                            quality_mode=_current_mode,
                        )
                except ValueError as exc:
                    print(f"[Gaze] Parse error: {exc}")
        except Exception as exc:
            print(f"[Gaze] Error: {exc}")
        finally:
            try:
                conn.close()
            except Exception:
                pass
    server_sock.close()


# ── Camera streamer (port 1235) ───────────────────────────────────────────────

def _send_frame(conn: socket.socket, payload: bytes) -> None:
    """Send a length-prefixed frame matching CameraFeedReceiver.ReadExact protocol."""
    header = struct.pack(">I", len(payload))
    conn.sendall(header + payload)


def camera_thread(
    stop_event: threading.Event,
    mode: str,
    quality: int,
    periph_quality: int,
    fovea_quality: int,
    fovea_size: int,
    show: bool = False,
    loss: float = 0.0,
) -> None:
    global _current_mode, _current_quality
    _current_mode = mode
    _current_quality = quality

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("[Camera] WARNING: Cannot open webcam (cv2.VideoCapture(0)). "
              "Falling back to synthetic frame generator.")
        cap = None
    else:
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        print(f"[Camera] Webcam opened — mode={mode}"
              + (f", quality={quality}" if mode == "uniform"
                 else f", periph_quality={periph_quality}, fovea_quality={fovea_quality}"
                      f", fovea_size={fovea_size}"))

    server_sock = _make_server_socket(1235)
    server_sock.settimeout(1.0)

    try:
        while not stop_event.is_set():
            # Wait for a Unity client to connect
            try:
                conn, addr = server_sock.accept()
            except socket.timeout:
                # If no client, but show is on, we still want to see the camera
                if show:
                    if cap is not None:
                        ok, frame = cap.read()
                    else:
                        ok = True
                        frame = _generate_synthetic_frame()
                    if ok:
                        # Push to queue for main thread to show
                        try:
                            _preview_queue.put_nowait(("Waiting for Client", frame))
                        except queue.Full:
                            pass
                continue

            print(f"[Camera] Client connected: {addr}")
            
            frame_interval = 1.0 / 20.0   # ~20 FPS target

            try:
                while not stop_event.is_set():
                    t0 = time.time()

                    if cap is not None:
                        ok, frame = cap.read()
                        if not ok:
                            print("[Camera] Webcam read failed — skipping frame.")
                            time.sleep(frame_interval)
                            continue
                    else:
                        ok = True
                        frame = _generate_synthetic_frame()

                    gaze_u, gaze_v, gaze_ts = _get_gaze()

                    if mode == "gaze":
                        payload, stats = encode_dual_payload(
                            frame,
                            (gaze_u, gaze_v),
                            periph_quality=periph_quality,
                            fovea_quality=fovea_quality,
                            fovea_size=fovea_size,
                        )
                        
                        if show:
                            # Draw the ROI rectangle on a copy for the preview window
                            preview = frame.copy()
                            x, y = stats["crop_x"], stats["crop_y"]
                            w, h = stats["crop_w"], stats["crop_h"]
                            cv2.rectangle(preview, (x, y), (x + w, y + h), (0, 0, 255), 2)
                            cv2.putText(preview, f"FOVEA {w}x{h} @ {x},{y}", (10, 30),
                                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 0, 255), 2)
                            try:
                                _preview_queue.put_nowait(("Gaze Mode", preview))
                            except queue.Full:
                                pass
                        
                        if _metrics:
                            delta_ms = (t0 - gaze_ts) * 1000 if gaze_ts > 0 else 0.0
                            _metrics.log(
                                "frame_sent",
                                bytes_total=len(payload),
                                bytes_periph=stats["periph_bytes"],
                                bytes_fovea=stats["fovea_bytes"],
                                gaze_uv=f"{gaze_u:.3f},{gaze_v:.3f}",
                                quality_mode=mode,
                                crop_x=str(stats["crop_x"]),
                                crop_y=str(stats["crop_y"]),
                                crop_w=str(stats["crop_w"]),
                                crop_h=str(stats["crop_h"]),
                                delta_ms=f"{delta_ms:.1f}",
                            )
                    else:
                        payload = encode_uniform(frame, quality)
                        if show:
                            try:
                                _preview_queue.put_nowait((f"Uniform (Q={quality})", frame))
                            except queue.Full:
                                pass
                        
                        if _metrics:
                            _metrics.log(
                                "frame_sent",
                                bytes_total=len(payload),
                                gaze_uv=f"{gaze_u:.3f},{gaze_v:.3f}",
                                quality_mode=f"{mode}-{quality}",
                            )

                    try:
                        if loss > 0 and random.random() < (loss / 100.0):
                            pass # Simulate packet loss
                        else:
                            _send_frame(conn, payload)
                    except (BrokenPipeError, ConnectionResetError, OSError):
                        print("[Camera] Client disconnected mid-stream.")
                        break

                    elapsed = time.time() - t0
                    sleep_t = frame_interval - elapsed
                    if sleep_t > 0:
                        time.sleep(sleep_t)

            except Exception as exc:
                print(f"[Camera] Stream error: {exc}")
            finally:
                try:
                    conn.close()
                except Exception:
                    pass
    finally:
        cap.release()
        server_sock.close()
        print("[Camera] Webcam released.")


# ── Entry point ───────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Mock Pioneer server — webcam + command sink + gaze listener"
    )
    parser.add_argument(
        "--mode",
        choices=["uniform", "gaze"],
        default="uniform",
        help="Camera encoding mode. 'uniform' = single JPEG. 'gaze' = dual-payload ROI (default: uniform).",
    )
    parser.add_argument(
        "--quality",
        type=int,
        choices=[5, 10, 20, 50],
        default=20,
        help="JPEG quality for uniform mode (default: 20). Ignored in gaze mode.",
    )
    parser.add_argument(
        "--periph-quality",
        type=int,
        default=15,
        metavar="Q",
        help="Gaze mode: periphery JPEG quality 1–100 (default: 15).",
    )
    parser.add_argument(
        "--fovea-quality",
        type=int,
        default=85,
        metavar="Q",
        help="Gaze mode: foveal crop JPEG quality 1–100 (default: 85).",
    )
    parser.add_argument(
        "--fovea-size",
        type=int,
        default=200,
        metavar="PX",
        help="Gaze mode: side length of the square foveal crop in pixels (default: 200).",
    )
    parser.add_argument(
        "--log-dir",
        default="logs",
        help="Directory for session CSV logs (default: logs/)",
    )
    parser.add_argument(
        "--show",
        action="store_true",
        help="Show local camera preview window with fovea ROI visualization.",
    )
    parser.add_argument(
        "--loss",
        type=float,
        default=0.0,
        metavar="N",
        help="Simulate packet loss: drop N percent of payloads (0.0 to 100.0).",
    )
    args = parser.parse_args()

    global _metrics
    _metrics = MetricsServer(log_dir=args.log_dir)
    mode_label = (
        f"uniform-{args.quality}"
        if args.mode == "uniform"
        else f"gaze-p{args.periph_quality}-f{args.fovea_quality}"
    )
    if args.loss > 0:
        mode_label += f"-loss{args.loss}%"
    
    _metrics.log("server_start", quality_mode=mode_label)

    # Debug: print explicit encoding qualities at startup for visibility
    print(f"[Server] Encoding peripheral at quality {args.periph_quality}, foveal at quality {args.fovea_quality}")

    stop_event = threading.Event()

    threads = [
        threading.Thread(target=control_thread, args=(stop_event,), name="ControlThread", daemon=True),
        threading.Thread(target=gaze_thread,    args=(stop_event,), name="GazeThread",    daemon=True),
        threading.Thread(
            target=camera_thread,
            args=(
                stop_event,
                args.mode,
                args.quality,
                args.periph_quality,
                args.fovea_quality,
                args.fovea_size,
                args.show,
                args.loss,
            ),
            name="CameraThread",
            daemon=True,
        ),
    ]

    for t in threads:
        t.start()

    print(f"\n[Server] Running — mode={args.mode}"
          + (f", quality={args.quality}" if args.mode == "uniform"
             else f", periph_quality={args.periph_quality}"
                  f", fovea_quality={args.fovea_quality}"
                  f", fovea_size={args.fovea_size}"))
    print("[Server] Press Ctrl+C to stop.\n")

    try:
        while not stop_event.is_set():
            if args.show:
                try:
                    # Polling the queue for frames to show (on main thread)
                    label, frame = _preview_queue.get(timeout=0.1)
                    cv2.imshow(f"Mock Pioneer - {label}", frame)
                    if cv2.waitKey(1) & 0xFF == ord('q'):
                        print("[Server] 'q' pressed in preview window. Shutting down...")
                        stop_event.set()
                        break
                except queue.Empty:
                    pass
            else:
                time.sleep(0.1)
    except KeyboardInterrupt:
        print("\n[Server] Shutting down…")
        stop_event.set()

    if args.show:
        cv2.destroyAllWindows()

    for t in threads:
        t.join(timeout=3.0)

    if _metrics:
        _metrics.log("server_stop")
        _metrics.close()

    print("[Server] Done.")


if __name__ == "__main__":
    main()
