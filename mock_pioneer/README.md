# Mock Pioneer Server

A Python substitute for the physical Pioneer robot. Exposes three TCP services so the Unity client can be developed and tested entirely on a laptop.

## Port table

| Port | Direction         | Protocol                                |
|------|-------------------|-----------------------------------------|
| 1234 | Unity → Server    | Control commands (ASCII)                |
| 1235 | Server → Unity    | Length-prefixed JPEG camera frames      |
| 1236 | Unity → Server    | Gaze coordinates (ASCII)                |

## Prerequisites

- Python 3.10+
- A working webcam accessible as device index 0
- On macOS: grant Terminal camera permission in System Settings → Privacy

```bash
cd mock_pioneer
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

## Running

```bash
# Uniform JPEG at quality 20 (Kirkpatrick-comparable baseline)
python server.py --mode uniform --quality 20

# Other quality levels (mirrors Kirkpatrick's 50/20/10/5 factors)
python server.py --mode uniform --quality 50
python server.py --mode uniform --quality 10
python server.py --mode uniform --quality 5

# Gaze-contingent mode (container built but not yet consumed by Unity — Phase 5)
python server.py --mode gaze
```

Optional flags:
- `--log-dir PATH`  Directory for session CSV logs (default: `logs/`)

Press **Ctrl+C** to stop all threads and flush the log.

## Protocol summary

### Control (port 1234) — server receives

```
$[cmd][turn:3][speed:3]\n
```

| Field   | Width | Range   | Notes                       |
|---------|-------|---------|-----------------------------|
| `$`     | 1     | literal |                             |
| cmd     | 1     | 0–2     | 0=stop, 1=forward, 2=reverse|
| turn    | 3     | 000–180 | 090 = straight ahead        |
| speed   | 3     | 000–512 | 256 = neutral / stopped     |
| `\n`    | 1     | literal |                             |

Example: `$1090256\n` → forward, straight, neutral speed

### Camera (port 1235) — server sends

```
[4 bytes big-endian uint32: payload length N]
[N bytes: JPEG payload]
```

In **uniform** mode the payload is a standard JPEG. In **gaze** mode the payload is the two-region container described in `docs/PROTOCOL.md`.

### Gaze (port 1236) — server receives

```
$GAZE[u:3][v:3]\n
```

| Field   | Width | Range   | Notes                              |
|---------|---------|---------|------------------------------------|
| `$GAZE` | 5     | literal |                                    |
| u       | 3     | 000–999 | horizontal UV × 1000               |
| v       | 3     | 000–999 | vertical UV × 1000                 |
| `\n`    | 1     | literal |                                    |

Example: `$GAZE500500\n` → centre of frame

## Session logs

Each run produces `logs/server_session_<YYYYMMDD_HHMMSS>.csv` with columns:

| Column         | Description                                |
|----------------|--------------------------------------------|
| `t`            | Unix timestamp (seconds, 4 decimal places) |
| `event`        | `server_start`, `frame_sent`, `cmd_received`, `gaze_received`, `server_stop` |
| `bytes_out`    | Bytes sent for frame events                |
| `cmd_received` | `cmd,turn,speed` string for control events |
| `gaze_uv`      | `u,v` string for gaze events               |
| `quality_mode` | Active mode at log time                    |

## Troubleshooting

**"Cannot open webcam"**
- macOS: System Settings → Privacy & Security → Camera → allow Terminal (or your shell app)
- Linux: ensure you are in the `video` group (`sudo usermod -aG video $USER`, then re-login)
- Try `cv2.VideoCapture(1)` if device 0 is your iPhone Continuity Camera

**"Connection refused" in Unity**
- Firewall may be blocking loopback ports. On macOS: System Settings → Network → Firewall → allow incoming connections for Python.
- Ensure the server is running *before* pressing Play in Unity.

**Port already in use**
- A previous run may not have exited cleanly. Find and kill it:
  ```bash
  lsof -ti :1234 -ti :1235 -ti :1236 | xargs kill -9
  ```

**Low frame rate**
- The server targets 20 FPS. If the webcam is slow to initialise, the first few seconds may drop frames.
- `--quality 5` produces the smallest payloads and is fastest to encode.
