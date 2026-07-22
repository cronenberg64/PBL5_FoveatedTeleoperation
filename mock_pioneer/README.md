# Camera Server (mock_pioneer)

Python server for the Pioneer rover teleoperation system. Captures video from the rover's on-board camera, applies gaze-contingent foveated encoding, and streams the processed feed to the Unity VR client.

## Port Table

| Port | Direction      | Protocol                                |
|------|----------------|-----------------------------------------|
| 1234 | Unity → Server | Control commands (ASCII)                |
| 1235 | Server → Unity | Length-prefixed JPEG camera frames      |
| 1236 | Unity → Server | Gaze coordinates (ASCII)                |

## Prerequisites

- Python 3.10+
- A USB camera connected to the Mini PC on the rover

```bash
cd mock_pioneer
python -m venv venv
venv\Scripts\activate   # Windows
pip install -r requirements.txt
```

## Running

```bash
# Rover camera with gaze-contingent foveation (study configuration)
python server.py --camera-source rover --camera-index 0 --mode gaze

# Uniform JPEG at quality 50
python server.py --camera-source rover --mode uniform --quality 50

# With preview window and flipped camera
python server.py --camera-source rover --camera-index 2 --mode gaze --show --camera-flip
```

Optional flags:
- `--camera-index N` — Select the hardware camera index (default: 0)
- `--camera-flip` — Horizontally flip the camera feed (for inverted-mounted cameras)
- `--periph-quality N` — Peripheral JPEG quality for gaze mode (default: 5)
- `--fovea-quality N` — Foveal JPEG quality for gaze mode (default: 90)
- `--show` — Display a preview window on the server
- `--log-dir PATH` — Directory for session CSV logs (default: `logs/`)

Press **Ctrl+C** to stop all threads and flush the log.

## Protocol Summary

See [docs/PROTOCOL.md](../docs/PROTOCOL.md) for the full TCP protocol reference covering all ports.

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
|---------|---------|---------|----------------------------------|
| `$GAZE` | 5     | literal |                                    |
| u       | 3     | 000–999 | horizontal UV × 1000               |
| v       | 3     | 000–999 | vertical UV × 1000                 |
| `\n`    | 1     | literal |                                    |

## Session Logs

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
- Ensure the camera is plugged into the Mini PC and recognised by the OS.
- Try `cv2.VideoCapture(1)` or `--camera-index 1` if device 0 is not the rover camera.

**"Connection refused" in Unity**
- Check the firewall is not blocking the server ports.
- Ensure the server is running *before* pressing Play in Unity.

**Port already in use**
- A previous run may not have exited cleanly. Find and kill it:
  ```powershell
  Get-Process -Name python | Stop-Process -Force
  ```
