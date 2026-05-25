"""
Foveation encoding strategies for the mock Pioneer camera streamer.

All functions return a raw bytes payload ready to send over the wire.
The caller is responsible for the outer 4-byte big-endian length prefix.
"""

import struct
import cv2
import numpy as np


def encode_uniform(frame: np.ndarray, quality: int) -> bytes:
    """
    Encode the full frame as a single JPEG at a uniform quality factor.

    Args:
        frame:   BGR image array (H, W, 3).
        quality: JPEG quality factor 1–100 (lower = smaller/worse).

    Returns:
        Raw JPEG bytes.
    """
    quality = int(np.clip(quality, 1, 100))
    ok, buf = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, quality])
    if not ok:
        raise RuntimeError("cv2.imencode failed for uniform encoding")
    return buf.tobytes()


def encode_dual_payload(
    frame: np.ndarray,
    gaze_uv: tuple[float, float],
    periph_quality: int = 15,
    fovea_quality: int = 85,
    fovea_size: int = 200,
) -> tuple[bytes, dict]:
    """
    Encode the frame using a dual-payload gaze-contingent strategy.

    The periphery is the full frame encoded at low quality.
    The fovea is a square crop centred on the gaze point encoded at high quality.
    The crop is clamped to frame bounds.

    Dual-payload binary layout (see docs/PROTOCOL.md for the full spec):
    ┌─────────────────────────────────────────────────────────────────┐
    │  Offset  Size  Type              Field                          │
    │  0       4     uint32 big-endian len_periph                     │
    │  4       4     uint32 big-endian len_fovea                      │
    │  8       2     uint16 big-endian crop_x  (left edge, pixels)    │
    │  10      2     uint16 big-endian crop_y  (top edge, pixels)     │
    │  12      2     uint16 big-endian crop_w                         │
    │  14      2     uint16 big-endian crop_h                         │
    │  16      len_periph bytes        periphery JPEG                 │
    │  16+len_periph  len_fovea bytes  foveal JPEG                    │
    └─────────────────────────────────────────────────────────────────┘

    The decoder should blit the foveal patch at (crop_x, crop_y) using the
    server's recorded crop coords — NOT the current live gaze — to keep the
    patch co-registered with what was actually encoded.

    Args:
        frame:          BGR image array (H, W, 3).
        gaze_uv:        (u, v) in [0.0, 1.0] UV space.
        periph_quality: JPEG quality for the full-frame periphery (default 15).
        fovea_quality:  JPEG quality for the foveal crop (default 85).
        fovea_size:     Side length of the square foveal crop in pixels (default 200).

    Returns:
        Tuple of (payload_bytes, stats_dict).
        stats_dict has keys: periph_bytes, fovea_bytes, crop_x, crop_y, crop_w, crop_h.
    """
    h, w = frame.shape[:2]
    u = float(np.clip(gaze_uv[0], 0.0, 1.0))
    v = float(np.clip(gaze_uv[1], 0.0, 1.0))

    # Gaze centre in pixel space.
    # UV: u=0 left, u=1 right, v=0 bottom, v=1 top (image row 0 = top).
    cx = int(u * w)
    cy = int((1.0 - v) * h)
    half = fovea_size // 2

    crop_x = int(np.clip(cx - half, 0, w))
    crop_y = int(np.clip(cy - half, 0, h))
    crop_x2 = int(np.clip(cx + half, 0, w))
    crop_y2 = int(np.clip(cy + half, 0, h))
    crop_w = crop_x2 - crop_x
    crop_h = crop_y2 - crop_y

    # ── Periphery: full frame at low quality ─────────────────────────────────
    pq = int(np.clip(periph_quality, 1, 100))
    ok_p, p_buf = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, pq])
    if not ok_p:
        raise RuntimeError("cv2.imencode failed for periphery")
    periph_bytes = p_buf.tobytes()

    # ── Fovea: cropped ROI at high quality ───────────────────────────────────
    foveal_patch = frame[crop_y:crop_y2, crop_x:crop_x2]
    fq = int(np.clip(fovea_quality, 1, 100))
    ok_f, f_buf = cv2.imencode(".jpg", foveal_patch, [cv2.IMWRITE_JPEG_QUALITY, fq])
    if not ok_f:
        raise RuntimeError("cv2.imencode failed for foveal patch")
    fovea_bytes = f_buf.tobytes()

    # ── Pack header + payloads ────────────────────────────────────────────────
    # >: big-endian  II: two uint32  HHHH: four uint16
    header = struct.pack(
        ">IIHHHH",
        len(periph_bytes),
        len(fovea_bytes),
        crop_x,
        crop_y,
        crop_w,
        crop_h,
    )
    payload = header + periph_bytes + fovea_bytes

    stats = {
        "periph_bytes": len(periph_bytes),
        "fovea_bytes": len(fovea_bytes),
        "crop_x": crop_x,
        "crop_y": crop_y,
        "crop_w": crop_w,
        "crop_h": crop_h,
    }
    return payload, stats


def encode_peripheral_only(frame: np.ndarray, quality: int) -> bytes:
    """
    Encode the frame for peripheral-only mode.
    Sends only the periphery JPEG (full frame) with fovea length set to 0.

    Args:
        frame:   BGR image array (H, W, 3).
        quality: JPEG quality factor 1–100.

    Returns:
        Dual-payload formatted bytes with 0 foveal length.
    """
    pq = int(np.clip(quality, 1, 100))
    ok, p_buf = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, pq])
    if not ok:
        raise RuntimeError("cv2.imencode failed for peripheral-only periphery")
    periph_bytes = p_buf.tobytes()

    # Pack 16-byte dual-payload header with 0 fovea size
    header = struct.pack(
        ">IIHHHH",
        len(periph_bytes),
        0,  # len_fovea = 0
        0,  # crop_x = 0
        0,  # crop_y = 0
        0,  # crop_w = 0
        0,  # crop_h = 0
    )
    return header + periph_bytes

