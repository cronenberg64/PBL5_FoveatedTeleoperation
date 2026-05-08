# PBL5 Foveated Teleoperation

VR teleoperation of a Pioneer robot with **gaze-contingent foveated streaming**.  
Built on Michael Kirkpatrick's 2024 Ritsumeikan thesis (supervisors: Chandler & Caluya).

## Phase Status

| Phase | Status | Description |
|-------|--------|-------------|
| 0 | Done | Kirkpatrick baseline reproduced (control + camera TCP, JPEG stream) |
| 1 | Done | Unity 6 migration, URP shader, Input System modernisation |
| 2 | **Done** | Mock Pioneer server (webcam, command sink, gaze listener) |
| 3 | **Done** | Upstream gaze channel (Unity → server, 30 Hz) |
| 4 | **Done** | Metrics CSV logging, Experimenter HUD + keyboard shortcuts |
| 5 | Not started | Unity decoder for gaze-contingent JPEG container (real bandwidth savings) |
| 6 | Not started | User study automation |
| 7 | Not started | Statistical analysis (Shapiro-Wilk + Friedman/ANOVA, replicating Kirkpatrick) |

---

## Project Layout

```
PBL5_FoveatedTeleoperation/     ← repo root
├── mock_pioneer/               ← Python mock server (Task 1)
│   ├── server.py               ← main entry point
│   ├── foveation.py            ← encoding strategies
│   ├── metrics_server.py       ← session CSV logger
│   ├── requirements.txt
│   ├── logs/                   ← session CSVs written here
│   └── README.md
├── docs/
│   └── PROTOCOL.md             ← all three TCP protocol specs
└── PBL5_FoveatedTeleoperation/ ← Unity 6 project
    └── Assets/Scripts/
        ├── Network/            RobotClient, CameraFeedReceiver, GazeUploader, NetworkConfig
        ├── Gaze/               GazeProvider, FoveatedFeedController, GazeDebugDot
        ├── Control/            TeleoperationController
        ├── Metrics/            MetricsLogger
        ├── UI/                 ExperimenterHUD, ExperimenterInput
        └── Editor/             SceneSetup (Foveated → Setup Scene menu)
```

---

## Running the Mock Server

```bash
cd mock_pioneer
python3 -m venv .venv                     # create a virtual environment
source .venv/bin/activate                 # activate it
pip install -r requirements.txt           # once: installs opencv-python, numpy

# Kirkpatrick-comparable uniform JPEG at quality 20
python server.py --mode uniform --quality 20

# Other quality levels (50 / 20 / 10 / 5)
python server.py --mode uniform --quality 5

# Gaze-contingent mode (container encoded but not yet consumed by Unity — Phase 5)
python server.py --mode gaze
```

Press **Ctrl+C** to stop.  Session log written to `mock_pioneer/logs/server_session_<timestamp>.csv`.

---

## Smoke Test Procedure

1. `cd mock_pioneer && source .venv/bin/activate && python server.py --mode uniform --quality 20`  
   — Server output should say *"Listening on port 1234/1235/1236"* and *"Webcam opened"*.

2. Open the Unity project (`PBL5_FoveatedTeleoperation/`) in Unity 6.

3. In the Unity menu: **Foveated → Setup Scene**  
   — Console should log *"X objects created, Y references wired"*.

4. Press **Play**.  
   — Console should log `[RobotClient] Connected`, `[CameraFeed] Connected`, `[GazeUploader] Connected`.

5. The webcam image should appear on the `FeedCanvas` plane in front of the VR camera.

6. Move your head (Quest Link) or the scene camera — the foveated-blur circle should follow.

7. Press **M** — mistake counter on the HUD should increment by 1.

8. Check `mock_pioneer/logs/` for a non-empty CSV.  
   Check `Application.persistentDataPath` on your Mac for `foveated_drivability_<timestamp>.csv`.

---

## Experimenter Keyboard Shortcuts (Play mode)

| Key | Action |
|-----|--------|
| `T` | Toggle task timer start / end |
| `1` / `2` / `3` | Set task number |
| `M` | Mark a mistake |
| `R` | Reset mistake counter |
| `Q` | Cycle quality-mode label (local only; restart server for actual mode change) |

---

## Task Definitions (Kirkpatrick 2024)

| Task | Name | Description |
|------|------|-------------|
| 1 | Boundary-precision | Navigate robot through a narrow corridor without touching walls |
| 2 | Obstacle-avoidance | Steer around objects placed in an open area |
| 3 | Pattern-recognition | Identify and report symbols placed along the robot's path |

Metrics: **mistake tally** (operator-marked, key M) + **task completion time** (timer T).

---

## Notes

- Phase 5 (gaze-contingent server→client) is **not yet implemented** in Unity. The `FoveatedFeed.shader` provides a *client-side perceptual* approximation that does not reduce network bandwidth.
- Quest Pro eye tracking requires `META_XR_SDK` scripting define and hardware. On Quest 3 / Editor, head-gaze is used automatically.
- The `NetworkConfig.asset` ships with `robotIP = 127.0.0.1` for mock server use. Swap to the robot's IP when hardware is available.

## References

Kirkpatrick, M. (2024). *Evaluating Drivability of Remotely Controlled Robot from Transmitted Video Compression Quality*. Ritsumeikan University. Supervisors: Chandler & Caluya.
