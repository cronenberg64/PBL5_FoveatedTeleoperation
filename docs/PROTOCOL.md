# TCP Protocol Reference

Foveated Teleoperation — all three TCP channels.

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

Implemented in `RobotClient.SendDriveCommand`.

---

## Port 1235 — Camera Feed (Server → Unity)

Direction: server **sends**, Unity client **receives**.

### Outer framing (both modes)

```
[4 bytes, big-endian uint32: payload length N]
[N bytes: payload — format depends on mode]
```

---

### Uniform mode payload

A standard JPEG encoded at the quality factor chosen via `--quality`.

```
[N bytes: JPEG data]
```

Quality levels used in experiments: **50, 20, 10, 5**.

---

### Gaze-contingent (dual-payload) mode payload

Activated via `--mode gaze`. The payload encodes the full frame at low quality
(periphery) **and** a high-quality crop centred on the server's recorded gaze
point (fovea). Unity composites the two at the crop coordinates embedded in the
header — **not** at the current live gaze — so the patch is always
co-registered with what the server actually encoded.

> **Temporal note**: The foveal patch arrives with a small lag relative to
> current gaze (server crops at gaze T-n; frame arrives at T). Compositing at
> the *server's crop coords* is correct. Chasing live gaze with stale data
> causes visible misregistration. This is a known trade-off; it is also why
> total round-trip latency matters more than eye-tracker sample rate for
> foveation systems.

#### Dual-payload binary layout

```
Offset  Size  Type              Field
──────────────────────────────────────────────────────────
0       4     uint32 big-endian len_periph  — byte count of periphery JPEG
4       4     uint32 big-endian len_fovea   — byte count of foveal JPEG
8       2     uint16 big-endian crop_x      — foveal crop left edge (pixels)
10      2     uint16 big-endian crop_y      — foveal crop top edge (pixels)
12      2     uint16 big-endian crop_w      — foveal crop width (pixels)
14      2     uint16 big-endian crop_h      — foveal crop height (pixels)
16      len_periph  bytes       periph_jpeg — full-frame JPEG at low quality
16+len_periph  len_fovea bytes  fovea_jpeg  — cropped ROI JPEG at high quality
```

Total header: **16 bytes**.

#### Decode procedure (Unity `CameraFeedReceiver`)

1. Read 16-byte header; unpack `len_periph`, `len_fovea`, `crop_x/y/w/h`.
2. Read `len_periph` bytes → decode as background texture (full frame, low-res).
3. Read `len_fovea` bytes → decode as foveal patch texture.
4. Blit foveal patch onto background at `(crop_x, crop_y)` with size `(crop_w, crop_h)`.
5. Hand composite texture to `FoveatedFeedController` / shader as normal.

#### Encoding parameters (server defaults)

| Parameter          | Value | Notes                              |
|--------------------|-------|------------------------------------|
| Peripheral quality | 15    | `--periph-quality` flag            |
| Foveal quality     | 85    | `--fovea-quality` flag             |
| Foveal crop size   | 200×200 px | `--fovea-size` flag (clamped to frame) |

---

## Port 1236 — Gaze Coordinates (Unity → Server)

Direction: Unity client **sends**, server **receives**.

### Frame format

```
$GAZE[u:3][v:3]\n
```

All ASCII. Total 12 bytes including newline.

| Field   | Width | Values  | Semantics                                     |
|---------|-------|---------|-----------------------------------------------|
| `$GAZE` | 5     | literal | Start-of-frame marker                         |
| u       | 3     | 000–999 | Horizontal UV × 1000; 500 = horizontal centre |
| v       | 3     | 000–999 | Vertical UV × 1000; 500 = vertical centre     |
| `\n`    | 1     | `0x0A`  | End-of-frame                                  |

Example: `$GAZE500500\n` → gaze at the centre of the image.

Sent at ~30 Hz by `GazeUploader.cs` (or `MouseGazeSource.cs` for desktop proxy).
The server holds the latest non-stale gaze; values older than 500 ms fall back to (0.5, 0.5).

---

## Gaze staleness and fallback

The server's `_get_gaze()` returns `(0.5, 0.5)` if:
- No gaze message has ever been received, **or**
- The most recent gaze message is older than 500 ms.

---

## Connection lifecycle

All three services use `SO_REUSEADDR` and accept exactly one client per port.
On client disconnect the server loops back to `accept()` without crashing.
Unity clients implement exponential back-off reconnection (1 s → 2 s → 4 s → … → 16 s max).
