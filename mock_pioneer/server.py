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
import sys
import os
import glob

# Suppress verbose OpenCV warnings
os.environ["OPENCV_LOG_LEVEL"] = "FATAL"

import cv2
import numpy as np

from foveation import encode_uniform, encode_dual_payload
from metrics_server import MetricsServer

# ── Shared state ─────────────────────────────────────────────────────────────

_gaze_lock = threading.Lock()
_latest_gaze: tuple[float, float] = (0.5, 0.5)
_gaze_timestamp: float = 0.0          # epoch seconds of last valid gaze update
_last_stale_log: float = 0.0          # epoch seconds of last stale log message
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

# Set by camera_thread/control_thread; read by control/gaze/camera threads for CSV logging/encoding
_config_lock = threading.Lock()
_current_mode: str = "uniform"
_current_quality: int = 20
_current_periph_quality: int = 15
_current_fovea_quality: int = 85
_current_fovea_size: int = 300

# Thread-safe queue for passing frames to the main thread for display (macOS requirement)
_preview_queue = queue.Queue(maxsize=1)


def _get_gaze() -> tuple[float, float, float]:
    """Return the latest non-stale gaze UV and its timestamp, or (0.5, 0.5, 0.0) if stale / never set."""
    global _last_stale_log
    with _gaze_lock:
        age = time.time() - _gaze_timestamp
        if _gaze_timestamp == 0.0 or age > _GAZE_STALE_TIMEOUT_S:
            now = time.time()
            try:
                if now - _last_stale_log > 5.0:
                    print(f"[Gaze] STALE — using center fallback (age: {age:.2f}s)")
                    _last_stale_log = now
            except NameError:
                _last_stale_log = now
                print(f"[Gaze] STALE — using center fallback (age: {age:.2f}s)")
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


def probe_linux_video_devices() -> dict:
    devices = {}
    if sys.platform.startswith('linux'):
        # Glob for /dev/video*
        for path in glob.glob('/dev/video*'):
            basename = os.path.basename(path)
            try:
                idx = int(basename[5:])
                # Try to read name from sysfs
                name_path = f"/sys/class/video4linux/video{idx}/name"
                name = f"video{idx}"
                if os.path.exists(name_path):
                    with open(name_path, "r") as f:
                        name = f.read().strip()
                devices[idx] = name
            except ValueError:
                continue
    return devices


def probe_cameras() -> str:
    available = []
    linux_names = probe_linux_video_devices()
    for idx in range(6):
        cap = cv2.VideoCapture(idx)
        if cap.isOpened():
            w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
            h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
            ret, frame = cap.read()
            if (ret and frame is not None) or (w > 0 and h > 0):
                if ret and frame is not None:
                    h, w = frame.shape[:2]
                name = linux_names.get(idx, "USB Camera")
                available.append(f"{idx}: {name} {w}x{h}")
            cap.release()
    return "[" + ", ".join(available) + "]"


def _read_frame_with_retry(cap, idx, camera_flip, camera_source) -> tuple:
    """
    Read a frame from cap. If read fails, attempts up to 5 reconnects.
    Returns (updated_cap, success, frame).
    """
    if cap is None:
        return None, False, None
        
    ok, frame = cap.read()
    if ok and frame is not None:
        if camera_flip:
            frame = cv2.flip(frame, 1)
        return cap, True, frame
        
    print(f"[Camera] Camera read failed on index {idx} — initiating reconnect retry...")
    for attempt in range(1, 6):
        print(f"[Camera] Reconnect attempt {attempt}/5 in 1s...")
        time.sleep(1.0)
        try:
            cap.release()
        except Exception:
            pass
        cap = cv2.VideoCapture(idx)
        if cap.isOpened():
            cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
            cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
            ok_check, check_frame = cap.read()
            if ok_check and check_frame is not None:
                print("[Camera] Camera reconnected successfully!")
                if camera_flip:
                    check_frame = cv2.flip(check_frame, 1)
                return cap, True, check_frame
                
    print("[Camera] ERROR: Reconnection failed after 5 attempts. Falling back to synthetic generator.")
    return None, False, None


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


def _handle_cfg(line: str) -> None:
    # Format: $CFG[mode:8][pq:3][fq:3]
    # Example: $CFGuniform 050050
    if len(line) < 18:
        raise ValueError(f"CFG command too short: {line!r}")
    
    mode_str = line[4:12].strip()
    pq_str = line[12:15]
    fq_str = line[15:18]
    
    try:
        pq = int(pq_str)
        fq = int(fq_str)
    except ValueError:
        raise ValueError(f"Non-integer quality in CFG: {line!r}")
        
    if mode_str not in ("uniform", "gaze"):
        raise ValueError(f"Unknown mode in CFG: {mode_str!r}")
        
    global _current_mode, _current_quality, _current_periph_quality, _current_fovea_quality
    with _config_lock:
        _current_mode = mode_str
        if mode_str == "uniform":
            _current_quality = pq
        _current_periph_quality = pq
        _current_fovea_quality = fq
        
    print(f"[Control] Runtime config updated: mode={mode_str}, pq={pq}, fq={fq}")

_rover_out_sock = None

def _connect_rover_out():
    global _rover_out_sock
    while True:
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            s.connect(("127.0.0.1", 1238))
            _rover_out_sock = s
            print("[Control] Connected to rover_bridge on port 1238")
            break
        except Exception:
            time.sleep(2)

def control_thread(stop_event: threading.Event, use_rover_out: bool) -> None:
    if use_rover_out:
        threading.Thread(target=_connect_rover_out, daemon=True).start()

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
                    if line.startswith("$CFG"):
                        _handle_cfg(line)
                    else:
                        cmd, turn, speed = _parse_control(line)
                        ts = time.time()
                        print(f"[Control] t={ts:.3f}  cmd={cmd}  turn={turn}  speed={speed}")
                        
                        global _rover_out_sock
                        if _rover_out_sock:
                            try:
                                _rover_out_sock.sendall(f"${cmd}{turn:03d}{speed:03d}\n".encode('ascii'))
                            except Exception as e:
                                print(f"[Control] Failed to send to rover_bridge: {e}")
                                _rover_out_sock = None
                                threading.Thread(target=_connect_rover_out, daemon=True).start()
                                
                        with _config_lock:
                            mode_lbl = _current_mode
                        if _metrics:
                            _metrics.log(
                                "cmd_received",
                                cmd_received=f"{cmd},{turn},{speed}",
                                quality_mode=mode_lbl,
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


_unity_frame_queue = queue.Queue(maxsize=2)


def _recv_exact(conn: socket.socket, num_bytes: int) -> Optional[bytes]:
    """Read exactly num_bytes from the socket, or return None if closed."""
    buf = b""
    while len(buf) < num_bytes:
        chunk = conn.recv(num_bytes - len(buf))
        if not chunk:
            return None
        buf += chunk
    return buf


def unity_frame_thread(stop_event: threading.Event) -> None:
    """Listen on port 1237 for incoming frames from Unity, decode them, and put them in the queue."""
    server_sock = _make_server_socket(1237)
    server_sock.settimeout(1.0)
    
    while not stop_event.is_set():
        try:
            conn, addr = server_sock.accept()
        except socket.timeout:
            continue
        print(f"[UnityFrame] Client connected: {addr}")
        
        try:
            conn.settimeout(2.0)
            while not stop_event.is_set():
                # 1. Read 4-byte length prefix
                len_buf = _recv_exact(conn, 4)
                if not len_buf:
                    print("[UnityFrame] Client disconnected.")
                    break
                
                length = struct.unpack(">I", len_buf)[0]
                
                # 2. Read raw JPEG bytes
                jpeg_bytes = _recv_exact(conn, length)
                if not jpeg_bytes:
                    print("[UnityFrame] Connection closed prematurely.")
                    break
                
                # 3. Decode JPEG to numpy BGR image
                nparr = np.frombuffer(jpeg_bytes, np.uint8)
                frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
                if frame is None:
                    print("[UnityFrame] Failed to decode JPEG frame.")
                    continue
                
                # 4. Push to shared queue, discarding oldest to avoid latency buildup
                while not _unity_frame_queue.empty():
                    try:
                        _unity_frame_queue.get_nowait()
                    except queue.Empty:
                        break
                
                _unity_frame_queue.put(frame)
        except Exception as exc:
            print(f"[UnityFrame] Error: {exc}")
        finally:
            try:
                conn.close()
            except Exception:
                pass
                
    server_sock.close()
    print("[UnityFrame] Server socket closed.")


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
    camera_source: str = "webcam",
    camera_index: int = 0,
    camera_flip: bool = False,
) -> None:
    global _current_mode, _current_quality, _current_periph_quality, _current_fovea_quality, _current_fovea_size
    with _config_lock:
        _current_mode = mode
        _current_quality = quality
        _current_periph_quality = periph_quality
        _current_fovea_quality = fovea_quality
        _current_fovea_size = fovea_size

    cap = None
    idx = camera_index if camera_source == "rover" else 0
    if camera_source in ("webcam", "rover"):
        cap = cv2.VideoCapture(idx)
        if not cap.isOpened():
            if camera_source == "rover":
                print(f"[Camera] WARNING: Cannot open rover camera index {idx}. "
                      "Falling back to synthetic frame generator.")
            else:
                print("[Camera] WARNING: Cannot open webcam (cv2.VideoCapture(0)). "
                      "Falling back to synthetic frame generator.")
            cap = None
        else:
            cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
            cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
            source_name = "Rover camera" if camera_source == "rover" else "Webcam"
            print(f"[Camera] {source_name} opened - index={idx}, mode={mode}"
                  + (f", quality={quality}" if mode == "uniform"
                     else f", periph_quality={periph_quality}, fovea_quality={fovea_quality}"
                          f", fovea_size={fovea_size}"))
    else:
        print(f"[Camera] Unity frame source active (listening on port 1237) - mode={mode}"
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
                conn.settimeout(None) # Prevent inheriting the 1.0s timeout from server_sock!
            except socket.timeout:
                # If no client, but show is on, we still want to see the camera
                if show:
                    if camera_source == "unity":
                        try:
                            frame = _unity_frame_queue.get(timeout=0.1)
                            ok = True
                        except queue.Empty:
                            ok = False
                    elif cap is not None:
                        cap, ok, frame = _read_frame_with_retry(cap, idx, camera_flip, camera_source)
                        if not ok:
                            frame = _generate_synthetic_frame()
                            ok = True
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
            
            frame_interval = 1.0 / 20.0   # ~20 FPS target for webcam/synthetic

            try:
                while not stop_event.is_set():
                    t0 = time.time()

                    if camera_source == "unity":
                        try:
                            # Block until frame arrives from Unity
                            frame = _unity_frame_queue.get(timeout=0.5)
                            ok = True
                        except queue.Empty:
                            continue
                    elif cap is not None:
                        cap, ok, frame = _read_frame_with_retry(cap, idx, camera_flip, camera_source)
                        if not ok:
                            frame = _generate_synthetic_frame()
                            ok = True
                    else:
                        ok = True
                        frame = _generate_synthetic_frame()

                    # Read current config under lock
                    with _config_lock:
                        curr_mode = _current_mode
                        curr_quality = _current_quality
                        curr_pq = _current_periph_quality
                        curr_fq = _current_fovea_quality
                        curr_fs = _current_fovea_size

                    gaze_u, gaze_v, gaze_ts = _get_gaze()

                    if curr_mode == "gaze":
                        payload, stats = encode_dual_payload(
                            frame,
                            (gaze_u, gaze_v),
                            periph_quality=curr_pq,
                            fovea_quality=curr_fq,
                            fovea_size=curr_fs,
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
                            # Compute condition_label
                            cond_lbl = "Foveated" # Default fallback
                            if curr_fq > curr_pq:
                                cond_lbl = "Foveated"
                            elif curr_pq > curr_fq:
                                cond_lbl = "InverseFoveated"


                            delta_ms = (t0 - gaze_ts) * 1000 if gaze_ts > 0 else 0.0
                            _metrics.log(
                                "frame_sent",
                                bytes_total=len(payload),
                                bytes_periph=stats["periph_bytes"],
                                bytes_fovea=stats["fovea_bytes"],
                                gaze_uv=f"{gaze_u:.3f},{gaze_v:.3f}",
                                quality_mode=curr_mode,
                                condition_label=cond_lbl,
                                crop_x=str(stats["crop_x"]),
                                crop_y=str(stats["crop_y"]),
                                crop_w=str(stats["crop_w"]),
                                crop_h=str(stats["crop_h"]),
                                delta_ms=f"{delta_ms:.1f}",
                            )
                    else:
                        payload = encode_uniform(frame, curr_quality)
                        if show:
                            try:
                                _preview_queue.put_nowait((f"Uniform (Q={curr_quality})", frame))
                            except queue.Full:
                                pass
                        
                        if _metrics:
                            _metrics.log(
                                "frame_sent",
                                bytes_total=len(payload),
                                gaze_uv=f"{gaze_u:.3f},{gaze_v:.3f}",
                                quality_mode=f"{curr_mode}-{curr_quality}",
                            )

                    try:
                        if loss > 0 and random.random() < (loss / 100.0):
                            pass # Simulate packet loss
                        else:
                            _send_frame(conn, payload)
                    except (BrokenPipeError, ConnectionResetError, OSError):
                        print("[Camera] Client disconnected mid-stream.")
                        break

                    # Only regulate frame rate for webcam/synthetic (Unity source is self-regulated at 30Hz)
                    if camera_source != "unity":
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
        if cap is not None:
            cap.release()
        server_sock.close()
        print("[Camera] Server socket and resources released.")


# ── Entry point ───────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Mock Pioneer server — webcam + command sink + gaze listener"
    )
    parser.add_argument(
        "--mode",
        choices=["uniform", "gaze"],
        default="gaze",
        help="Camera encoding mode. 'uniform' = single JPEG. 'gaze' = dual-payload ROI. (default: gaze).",
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
        default=5,
        metavar="Q",
        help="Gaze mode: periphery JPEG quality 1–100 (default: 5).",
    )
    parser.add_argument(
        "--fovea-quality",
        type=int,
        default=90,
        metavar="Q",
        help="Gaze mode: foveal crop JPEG quality 1–100 (default: 90).",
    )
    parser.add_argument(
        "--fovea-size",
        type=int,
        default=300,
        metavar="PX",
        help="Gaze mode: foveal square crop size in pixels (default: 300).",
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
    parser.add_argument(
        "--camera-source",
        choices=["webcam", "unity", "rover"],
        default="rover",
        help="Source of camera frames (default: rover).",
    )
    parser.add_argument(
        "--camera-index",
        type=int,
        default=0,
        help="Camera index for cv2.VideoCapture (default: 0).",
    )
    parser.add_argument(
        "--camera-flip",
        action="store_true",
        help="If true, flip the camera frames horizontally (cv2.flip(frame, 1)) to handle inverted mounts.",
    )
    parser.add_argument("--metrics-off", action="store_true", help="Disable logging completely")
    parser.add_argument("--rover-out", action="store_true", help="Forward drive commands to local port 1238 (rover_bridge.py)")

    args = parser.parse_args()

    # Startup validation check for rover camera
    if args.camera_source == "rover":
        cap = cv2.VideoCapture(args.camera_index)
        if not cap.isOpened():
            available = probe_cameras()
            sys.exit(f"[Server] ERROR: cv2.VideoCapture({args.camera_index}) failed. Available indices: {available}")
        
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        fps = cap.get(cv2.CAP_PROP_FPS)
        if fps <= 0 or math.isnan(fps):
            fps = 30.0
            
        ret, frame = cap.read()
        if not ret or frame is None or w == 0 or h == 0:
            cap.release()
            available = probe_cameras()
            sys.exit(f"[Server] ERROR: cv2.VideoCapture({args.camera_index}) failed (0x0 or failed read). Available indices: {available}")
            
        print(f"[Server] Rover camera index={args.camera_index}: {w}x{h} @ {fps:.1f} fps")
        cap.release()

    global _current_mode, _current_quality, _current_periph_quality, _current_fovea_quality, _current_fovea_size
    with _config_lock:
        _current_mode = args.mode
        _current_quality = args.quality
        _current_periph_quality = args.periph_quality
        _current_fovea_quality = args.fovea_quality
        _current_fovea_size = args.fovea_size

    global _metrics
    _metrics = MetricsServer(log_dir=args.log_dir)
    mode_label = (
        f"uniform-{args.quality}"
        if args.mode == "uniform"
        else f"gaze-p{args.periph_quality}-f{args.fovea_quality}"
    )
    if args.loss > 0:
        mode_label += f"-loss{args.loss}%"
    if args.camera_source == "unity":
        mode_label += f"-unity"
    elif args.camera_source == "rover":
        mode_label += f"-rover"
    
    _metrics.log("server_start", quality_mode=mode_label)

    # Debug: print explicit encoding qualities at startup for visibility
    print(f"[Server] Encoding peripheral at quality {args.periph_quality}, foveal at quality {args.fovea_quality}")

    stop_event = threading.Event()

    threads = [
        threading.Thread(target=control_thread, args=(stop_event, args.rover_out), name="ControlThread", daemon=True),
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
                args.camera_source,
                args.camera_index,
                args.camera_flip,
            ),
            name="CameraThread",
            daemon=True,
        ),
    ]

    if args.camera_source == "unity":
        threads.append(
            threading.Thread(target=unity_frame_thread, args=(stop_event,), name="UnityFrameThread", daemon=True)
        )

    for t in threads:
        t.start()

    print(f"\n[Server] Running - mode={args.mode}"
          + (f", quality={args.quality}" if args.mode == "uniform"
             else f", periph_quality={args.periph_quality}"
                  f", fovea_quality={args.fovea_quality}"
                  f", fovea_size={args.fovea_size}"))
    print(f"[Server] Camera Source: {args.camera_source}")
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
