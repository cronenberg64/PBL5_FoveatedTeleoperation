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

### Configuration command format

```
$CFG[mode:8][pq:3][fq:3]\n
```

All ASCII. Total 18 bytes including newline.

| Field   | Width | Values  | Semantics                            |
|---------|-------|---------|--------------------------------------|
| `$CFG`  | 4     | literal | Config command prefix                |
| mode    | 8     | string  | Mode name, left-aligned, space-padded: `"uniform "`, `"gaze    "`, `"periph  "` |
| pq      | 3     | 000–100 | Peripheral quality (zero-padded)     |
| fq      | 3     | 000–100 | Fovea quality (zero-padded)          |
| `\n`    | 1     | `0x0A`  | End-of-frame                         |

Examples:
- `$CFGuniform 050050\n` → switches to Uniform Q50 mode.
- `$CFGgaze    015085\n` → switches to Gaze foveation mode (periph Q15, fovea Q85).
- `$CFGperiph  030030\n` → switches to Peripheral-only Q30 mode.

Implemented in `ConditionController.SetCondition`.

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

### Peripheral-only mode payload

Activated via `$CFGperiph  [pq:3][fq:3]\n` or `--mode periph`. 

The payload uses the exact same 16-byte dual-payload binary layout as gaze-contingent mode, but with the following parameters:
- `len_fovea = 0` (the fovea_jpeg payload is omitted / 0 bytes)
- `crop_x = 0`, `crop_y = 0`, `crop_w = 0`, `crop_h = 0`

Unity's `CameraFeedReceiver` detects `len_fovea = 0` and renders only the peripheral full-frame JPEG at the peripheral quality.

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

## Port 1237 — Unity Scene Frame Input (Unity → Server)

Direction: Unity client **sends**, mock server **receives**.
Active when the server is started with `--camera-source unity`.

### Frame format

```
[4 bytes, big-endian uint32: payload length N]
[N bytes: raw JPEG frame data encoded from Unity camera at Q95]
```

This protocol allows Unity to stream its high-resolution camera rendering frames directly into the mock server. The server decodes this high-Q stream, then applies uniform or foveated encoding on it before streaming the resulting compressed payload back over Port 1235.

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

---

## Phase 3 — Bandwidth calibration (LOCKED)

Results from the matched-bandwidth sweep (locked on May 22, 2026):

- **Baseline:** Uniform Q50 — avg_bytes = 13154.24 bytes/frame
- **Matched foveated condition:** periph_q=15, fovea_q=85 — avg_bytes = 13302.55 bytes/frame (+1.13% from baseline)
- **Note:** One anomalous data point at (periph_q=25, fovea_q=85) was observed and flagged for a targeted re-run (investigation required before final publication).

These values were produced by the `bench_bandwidth.py` sweep and recorded in `mock_pioneer/bench_results.csv`.

No further Phase 3 sweeps should be run until Phase 4 design is approved.
