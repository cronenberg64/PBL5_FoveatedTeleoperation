# TCP Protocol Reference

Foveated Teleoperation — all three TCP channels.

Baseline (Kirkpatrick 2024): control (1234) + camera (1235).
Novel addition: gaze upstream (1236).

---

## Port 1234 — Control Commands (Unity → Server)

Direction: Unity client **sends**, Pioneer / mock server **receives**.

### Frame format

```
$[cmd][turn:3][speed:3]\n
```

All ASCII. Total 9 bytes including newline.

| Field   | Width | Values  | Semantics                            |
|---------|-------|---------|--------------------------------------|
| `$`     | 1     | literal | Start-of-frame marker                |
| cmd     | 1     | 0–2     | 0 = stop, 1 = forward, 2 = reverse   |
| turn    | 3     | 000–180 | Steer angle; 090 = straight ahead    |
| speed   | 3     | 000–512 | Drive speed; 256 = neutral/stopped   |
| `\n`    | 1     | `0x0A`  | End-of-frame                         |

Example: `$1090256\n` → forward, straight, neutral speed.

Source: Kirkpatrick (2024) §3.2; implemented in `RobotClient.SendDriveCommand`.

---

## Port 1235 — Camera Feed (Server → Unity)

Direction: server **sends**, Unity client **receives**.

### Frame format

```
[4 bytes, big-endian uint32: payload length N]
[N bytes: JPEG payload]
```

In **uniform mode** the payload is a standard JPEG.  
In **gaze-contingent mode** (Phase 5) the payload is the two-region container below.

### Uniform JPEG payload

A standard JPEG encoded by `cv2.imencode(".jpg", frame, [IMWRITE_JPEG_QUALITY, Q])`.  
Quality factors Q used in the experiment: **50, 20, 10, 5** (matching Kirkpatrick).

### Gaze-contingent container payload (Phase 5 — not yet consumed by Unity)

```
[4 bytes, int32 big-endian: ROI centre X in pixels]
[4 bytes, int32 big-endian: ROI centre Y in pixels]
[4 bytes, int32 big-endian: ROI side length in pixels (square)]
[4 bytes, int32 big-endian: ROI JPEG byte count R]
[R bytes: ROI JPEG (high quality, foveal_quality=50)]
[remaining bytes: periphery JPEG (low quality, peripheral_quality=5, half-resolution)]
```

The Unity decoder (to be implemented in Phase 5, `CameraFeedReceiver`) must:
1. Read the 16-byte header.
2. Read exactly R bytes for the ROI JPEG.
3. Read the remainder of the payload for the periphery JPEG.
4. Composite: decode periphery as base texture, overlay the decoded ROI patch at (cx, cy).

Implemented server-side in `mock_pioneer/foveation.py::encode_gaze_contingent`.

---

## Port 1236 — Gaze Coordinates (Unity → Server)

Direction: Unity client **sends**, server **receives**.  
Novel addition (not in Kirkpatrick baseline).

### Frame format

```
$GAZE[u:3][v:3]\n
```

All ASCII. Total 12 bytes including newline.

| Field   | Width | Values  | Semantics                                    |
|---------|-------|---------|----------------------------------------------|
| `$GAZE` | 5     | literal | Start-of-frame marker                        |
| u       | 3     | 000–999 | Horizontal UV × 1000; 500 = horizontal centre |
| v       | 3     | 000–999 | Vertical UV × 1000; 500 = vertical centre    |
| `\n`    | 1     | `0x0A`  | End-of-frame                                 |

Example: `$GAZE500500\n` → gaze at the centre of the image.

Sent at ~30 Hz by `GazeUploader.cs`.  
The server maintains the latest non-stale gaze; values older than 500 ms fall back to (0.5, 0.5).

---

## Gaze staleness and fallback

The server's `_get_gaze()` function returns the centre `(0.5, 0.5)` if:
- No gaze message has ever been received, **or**
- The most recent gaze message is older than 500 ms.

This prevents artefacts when the Unity client disconnects the gaze channel mid-session.

---

## Connection lifecycle

All three services use `SO_REUSEADDR` and accept exactly one client per port.  
On client disconnect the server loops back to `accept()` without crashing.  
Unity clients implement exponential back-off reconnection (1 s → 2 s → 4 s → … → 16 s max).
