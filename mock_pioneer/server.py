#!/usr/bin/env python3
"""
Mock Pioneer server — replaces the physical Pioneer robot during development.

Three concurrent TCP services:
  1234  Control listener  — receives $[cmd][turn:3][speed:3]\\n drive commands
  1235  Camera streamer   — sends length-prefixed JPEG frames from the webcam
  1236  Gaze listener     — receives $GAZE[u:3][v:3]\\n gaze coordinates

Usage:
    python server.py --mode uniform --quality 20
    python server.py --mode gaze

See README.md for full option reference.
"""

import argparse
import socket
import struct
import threading
import time
from typing import Optional

import cv2
import numpy as np

from foveation import encode_uniform, encode_gaze_contingent
from metrics_server import MetricsServer

# ── Shared state ─────────────────────────────────────────────────────────────

_gaze_lock = threading.Lock()
_latest_gaze: tuple[float, float] = (0.5, 0.5)
_gaze_timestamp: float = 0.0          # epoch seconds of last valid gaze update
_GAZE_STALE_TIMEOUT_S: float = 0.5   # fall back to centre after 500 ms

_metrics: Optional[MetricsServer] = None


def _get_gaze() -> tuple[float, float]:
    """Return the latest non-stale gaze UV, or (0.5, 0.5) if stale / never set."""
    with _gaze_lock:
        age = time.time() - _gaze_timestamp
        if _gaze_timestamp == 0.0 or age > _GAZE_STALE_TIMEOUT_S:
            return (0.5, 0.5)
        return _latest_gaze


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

_current_mode: str = "uniform"
_current_quality: int = 20


def _send_frame(conn: socket.socket, payload: bytes) -> None:
    """Send a length-prefixed frame matching CameraFeedReceiver.ReadExact protocol."""
    header = struct.pack(">I", len(payload))
    conn.sendall(header + payload)


def camera_thread(stop_event: threading.Event, mode: str, quality: int) -> None:
    global _current_mode, _current_quality
    _current_mode = mode
    _current_quality = quality

    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("[Camera] ERROR: Cannot open webcam (cv2.VideoCapture(0)). "
              "Check webcam permissions and connection.")
        return

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
    print(f"[Camera] Webcam opened — mode={mode}, quality={quality}")

    server_sock = _make_server_socket(1235)
    server_sock.settimeout(1.0)

    try:
        while not stop_event.is_set():
            # Wait for a Unity client to connect
            try:
                conn, addr = server_sock.accept()
            except socket.timeout:
                continue

            print(f"[Camera] Client connected: {addr}")
            frame_interval = 1.0 / 20.0   # ~20 FPS target

            try:
                while not stop_event.is_set():
                    t0 = time.time()

                    ok, frame = cap.read()
                    if not ok:
                        print("[Camera] Webcam read failed — skipping frame.")
                        time.sleep(frame_interval)
                        continue

                    gaze = _get_gaze()

                    if mode == "gaze":
                        payload = encode_gaze_contingent(frame, gaze)
                    else:
                        payload = encode_uniform(frame, quality)

                    try:
                        _send_frame(conn, payload)
                    except (BrokenPipeError, ConnectionResetError, OSError):
                        print("[Camera] Client disconnected mid-stream.")
                        break

                    if _metrics:
                        _metrics.log(
                            "frame_sent",
                            bytes_out=len(payload),
                            gaze_uv=f"{gaze[0]:.3f},{gaze[1]:.3f}",
                            quality_mode=mode,
                        )

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
        help="Camera encoding mode (default: uniform). "
             "'gaze' builds the two-region container but Unity won't consume it until Phase 5.",
    )
    parser.add_argument(
        "--quality",
        type=int,
        choices=[5, 10, 20, 50],
        default=20,
        help="JPEG quality for uniform mode (default: 20). Ignored in gaze mode.",
    )
    parser.add_argument(
        "--log-dir",
        default="logs",
        help="Directory for session CSV logs (default: logs/)",
    )
    args = parser.parse_args()

    global _metrics
    _metrics = MetricsServer(log_dir=args.log_dir)
    _metrics.log("server_start", quality_mode=f"{args.mode}-{args.quality}")

    stop_event = threading.Event()

    threads = [
        threading.Thread(target=control_thread, args=(stop_event,), name="ControlThread", daemon=True),
        threading.Thread(target=gaze_thread,    args=(stop_event,), name="GazeThread",    daemon=True),
        threading.Thread(
            target=camera_thread,
            args=(stop_event, args.mode, args.quality),
            name="CameraThread",
            daemon=True,
        ),
    ]

    for t in threads:
        t.start()

    print(f"\n[Server] Running — mode={args.mode}, quality={args.quality}")
    print("[Server] Press Ctrl+C to stop.\n")

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n[Server] Shutting down…")
        stop_event.set()

    for t in threads:
        t.join(timeout=3.0)

    if _metrics:
        _metrics.log("server_stop")
        _metrics.close()

    print("[Server] Done.")


if __name__ == "__main__":
    main()
