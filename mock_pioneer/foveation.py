"""
Foveation encoding strategies for the mock Pioneer camera streamer.

All functions return a raw bytes payload ready to send over the wire.
The caller is responsible for framing (4-byte big-endian length prefix).
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


def encode_gaze_contingent(
    frame: np.ndarray,
    gaze_uv: tuple[float, float],
    foveal_quality: int = 50,
    peripheral_quality: int = 5,
    foveal_radius_px: int = 120,
) -> bytes:
    """
    Encode the frame using a gaze-contingent two-region strategy.

    A high-quality square ROI is cropped around the gaze point; the rest of
    the frame is downsampled and encoded at low quality to represent the
    bandwidth-cheap periphery.

    Custom container layout (NOTE: Unity Phase-5 decoder must match this):
    ┌──────────────────────────────────────────────────────┐
    │  4 bytes  int32 big-endian  ROI centre X (pixels)   │
    │  4 bytes  int32 big-endian  ROI centre Y (pixels)   │
    │  4 bytes  int32 big-endian  ROI side length (pixels) │
    │  4 bytes  int32 big-endian  ROI JPEG byte count (R) │
    │  R bytes                    ROI JPEG data            │
    │  remaining bytes            periphery JPEG data      │
    └──────────────────────────────────────────────────────┘

    The decoder reads the 16-byte header, reads R bytes of ROI JPEG, then
    reads to end-of-frame-payload for the periphery JPEG.

    Args:
        frame:             BGR image array (H, W, 3).
        gaze_uv:           (u, v) in [0.0, 1.0] UV space.
        foveal_quality:    JPEG quality for the ROI patch.
        peripheral_quality: JPEG quality for the downsampled full frame.
        foveal_radius_px:  Half-side of the square ROI in pixels.

    Returns:
        Raw container bytes (header + ROI JPEG + periphery JPEG).
    """
    h, w = frame.shape[:2]
    u, v = float(np.clip(gaze_uv[0], 0.0, 1.0)), float(np.clip(gaze_uv[1], 0.0, 1.0))

    cx = int(u * w)
    cy = int((1.0 - v) * h)  # UV v=0 is bottom; image row 0 is top
    r = int(foveal_radius_px)

    x0 = max(0, cx - r)
    y0 = max(0, cy - r)
    x1 = min(w, cx + r)
    y1 = min(h, cy + r)

    roi_patch = frame[y0:y1, x0:x1]
    roi_side = max(x1 - x0, y1 - y0)

    ok_roi, roi_buf = cv2.imencode(
        ".jpg", roi_patch, [cv2.IMWRITE_JPEG_QUALITY, int(np.clip(foveal_quality, 1, 100))]
    )
    if not ok_roi:
        raise RuntimeError("cv2.imencode failed for ROI patch")

    # Periphery: quarter-resolution to save bandwidth
    small = cv2.resize(frame, (w // 2, h // 2), interpolation=cv2.INTER_AREA)
    ok_peri, peri_buf = cv2.imencode(
        ".jpg", small, [cv2.IMWRITE_JPEG_QUALITY, int(np.clip(peripheral_quality, 1, 100))]
    )
    if not ok_peri:
        raise RuntimeError("cv2.imencode failed for periphery")

    roi_bytes = roi_buf.tobytes()
    peri_bytes = peri_buf.tobytes()

    header = struct.pack(">iiii", cx, cy, roi_side, len(roi_bytes))
    return header + roi_bytes + peri_bytes
