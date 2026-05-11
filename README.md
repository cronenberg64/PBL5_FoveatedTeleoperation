# Foveated Teleoperation

A Unity-based VR teleoperation system for robot control featuring gaze-contingent foveated streaming. This repository includes both the Unity client and a Python-based mock server for local development and testing.

## Project Layout

```text
PBL5_FoveatedTeleoperation/     ← repo root
├── mock_pioneer/               ← Python mock server
│   ├── server.py               ← Main entry point
│   ├── foveation.py            ← Encoding strategies
│   ├── metrics_server.py       ← Session CSV logger
│   └── logs/                   ← Server-side session logs
├── docs/
│   └── PROTOCOL.md             ← TCP protocol specifications
└── PBL5_FoveatedTeleoperation/ ← Unity 6 project (URP)
    └── Assets/Scripts/
        ├── Network/            ← RobotClient, CameraFeedReceiver, GazeUploader
        ├── Gaze/               ← GazeProvider, FoveatedFeedController
        ├── Metrics/            ← MetricsLogger
        ├── UI/                 ← ExperimenterHUD, ExperimenterInput
        └── Editor/             ← SceneSetup automation
```

## Setup and Execution

### 1. Mock Server (Python)
The mock server simulates the robot hardware by providing a control sink, a camera stream (webcam), and a gaze listener.

```bash
cd mock_pioneer
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt

# Run with uniform JPEG encoding at quality 20
python server.py --mode uniform --quality 20
```

### 2. Unity Client
1. Open the `PBL5_FoveatedTeleoperation` folder in Unity 6 (6000.4.3f1).
2. Go to **Foveated → Setup Scene** in the top menu to automate component wiring.
3. Press **Play** to connect to the mock server.

## Smoke Test Procedure

1. Start the mock server (`python server.py`).
2. Press **Play** in Unity.
3. Verify the webcam feed appears on the VR plane.
4. Verify the foveation circle follows your head/eye gaze.
5. Press **M** to verify the mistake counter increments on the HUD.
6. Check `Application.persistentDataPath` for the client-side CSV log.

## Experimenter Controls

| Key | Action |
|-----|--------|
| `T` | Toggle task timer (Start/End) |
| `1` / `2` / `3` | Set current task number |
| `M` | Increment mistake counter |
| `R` | Reset mistake counter |
| `Q` | Cycle quality mode label (Local HUD only) |

## Task Definitions

| Task | Name | Description |
|------|------|-------------|
| 1 | Boundary-precision | Navigate through a narrow corridor without contact |
| 2 | Obstacle-avoidance | Steer around objects in an open area |
| 3 | Pattern-recognition | Identify and report symbols along the path |

The system records **mistake counts** and **completion time** for each task.

## Protocol Summary

*   **Control (Port 1234)**: ASCII commands `$CMD[turn:3][speed:3]\n`
*   **Camera (Port 1235)**: Length-prefixed JPEG frames
*   **Gaze (Port 1236)**: ASCII coordinates `$GAZE[u:3][v:3]\n`
