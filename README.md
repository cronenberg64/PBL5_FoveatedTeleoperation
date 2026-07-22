# PBL5 Foveated Teleoperation for VIVE Focus Vision

A Unity-based teleoperation system for Pioneer robots, featuring **Gaze-Contingent Foveated Streaming** to optimize perceptual quality and bandwidth.

## Core Features

- **Gaze-Contingent Foveated Rendering**: A custom HLSL shader renders a high-quality "Foveal Circle" (Quality 95) at the user's focus point while pixelating the periphery (Quality 5) to simulate bandwidth savings.
- **VIVE Focus Vision Integration**: Supports hardware eye-tracking via OVRPlugin for gaze-contingent streaming.
- **Hardware Teleoperation Controls**: Integrated with the **Serafim R1+ Steering Wheel and Pedal Dock** via Unity's Action-based Input System.
  - **Accelerator**: Gas Pedal
  - **Brake**: Brake Pedal
  - **Steering**: Steering Wheel
- **TCP Protocol Baseline**: Reconstructed `$CMD + turn(3) + speed(3) + \n` protocol for communication with the physical Pioneer robot.

## Hardware Architecture

The system operates on a physical Pioneer-style rover using a decentralized networking architecture to minimize latency:

1. **Workstation (Unity Client)**: Runs the VR interface and eye-tracking. Acts as a **TCP Server** to receive direct connections from the ESP32 for zero-latency motor control.
2. **Mini PC (On Rover)**: Runs `server.py` over WiFi. Handles camera capture, applies the OpenCV foveation encoding based on gaze coordinates, and streams the dual-payload video feed back to Unity.
3. **ESP32 (On Rover)**: Handles physical wheel/servo control. Connects directly to the Workstation's IP over WiFi to receive drive commands, completely bypassing the Mini PC.

## Setup & Testing Instructions

1. **Unity Version**: Use Unity 6 (`6000.4.3f1`) or newer.
2. **Dependencies**: Ensure the **Input System** and **XR Interaction Toolkit** packages are installed.
3. **Network Configuration**: Create or edit `NetworkConfig` (Assets → Create → Teleoperation → Network Config). Set the `robotIP` to your Mini PC's IP address. 

### Running a Live Teleoperation Session

1. **Power up the Rover**:
   * Connect the 2 batteries on the rover (one for the motor and the other for the miniPC)
   * Plug in the monitor, keyboard, and mouse to the Mini PC
   * Power on the miniPC and ensure it connects to the lab WiFi
2. **Start the Python Camera Server** (on the Mini PC):
   ```bash
   cd mock_pioneer
   python server.py
   ```
   * Once the server is running and streaming starts, unplug the monitor, keyboard, and mouse from the Mini PC so the rover can navigate without trailing cables
3. **Open the Scene in Unity**:
   * Open `Assets/Scenes/ViveFoveated.unity` or `DesktopFoveated.unity`
4. **Press Play** in Unity to begin. The ESP32 should automatically connect to your Workstation, and the video feed will start streaming.

> [!Note]
> **Troubleshooting Eye Tracking / Foveated Rendering**: If gaze-contingent foveated rendering or eye tracking is not functioning in VR, SteamVR may have failed to detect the headset's eye-tracking runtime at launch. Restart the headset and Unity (or restart SteamVR or delete VIVE business streaming from runtime), then press Play again.

---

### Controlling & Switching Foveation Conditions

While the Unity scene is running, you can dynamically switch between rendering modes using these keyboard hotkeys:

*   **Press 1**: **Uniform Mode** (`UniformQ50`) — Uniform quality across the entire screen (no foveation).
*   **Press 2**: **Foveated Mode** (`Foveated_90_5`) — Gaze-contingent foveation enabled! Periphery is rendered at Quality 5, while a high-resolution circle follows your gaze at Quality 90.
*   **Press 3**: **Inverse-Foveated** (`InverseFoveated_5_90`) — Reversed gaze-contingency! Periphery is sharp while the gaze focal point is pixelated and degraded.

**Driving Controls**: Teleoperation is controlled using a **Serafim R1+ Steering Wheel and Pedal Set** connected to the Workstation PC (Gas Pedal = Accelerator, Brake Pedal = Brake, Wheel = Steering). Keyboard fallbacks (**W/A/S/D** to steer/drive, **Space** to brake) are also supported.

---

### Logging & User Study Controls

During a user study, you must tell Unity which task the participant is performing *before* starting the trial timer. 

1. **Select Task Scenario**: Press one of the following keys to label the upcoming trial in the CSV:
   * **Press 7 (or C)**: Navigation Task
   * **Press 8 (or D)**: Identification Task
   * **Press 9 (or O)**: Visual Search Task

2. **Select Rendering Condition**:
   * **Press 1, 2, or 3** to set the foveation mode (Uniform, Foveated, Inverse)
   * *Note: You cannot change the condition while a trial is actively recording*

3. **Start/Stop Trial (CSV Logging)**:
   * Press **Enter** on the keyboard to start the trial timer
   * Press **Enter** again to end the trial and save the CSV row

---

## Results

Within-subjects experiment comparing three bandwidth-matched streaming conditions across three teleoperation tasks. Non-parametric Friedman tests used due to small sample sizes (N = 4–6 per task); significance threshold α = 0.05.

### Task Durations

![Duration by task and condition](docs/analysis/figures/duration_by_scenario_combined.png)

### Navigation (N = 6)

Participants drove the rover through a corridor. While objective task performance (duration, collisions) was not significantly affected by condition, participants reported significantly higher **mental demand** (p = .019) and **overall workload** (p = .042) under Inverse-Foveated — degrading the foveal region was the most cognitively taxing configuration.

| Metric | Uniform | Foveated | Inverse-Foveated |
|---|---|---|---|
| Duration (s) | 154.49 ± 140.84 | 163.09 ± 65.34 | 181.30 ± 76.39 |
| Collisions | 3.00 ± 2.00 | 3.17 ± 2.14 | 3.33 ± 2.25 |
| Overall Workload (RTLX) | 32.94 ± 16.71 | 40.78 ± 21.73 | **41.97 ± 19.28** |
| Mental Demand | 35.00 ± 18.97 | 48.33 ± 25.03 | **54.17 ± 25.77** |

| DV | χ²(2) | p | Significant? |
|---|---|---|---|
| Duration | 4.000 | .135 | No |
| Collisions | 0.091 | .956 | No |
| Overall Workload (RTLX) | 6.348 | **.042** | **Yes** |
| Mental Demand | 7.913 | **.019** | **Yes** |
| Frustration | 4.778 | .092 | No |

---

### Identification (N = 5)

Participants drove to a sign and read its text aloud. Duration was significantly different across conditions (p = .041). Counterintuitively, Inverse-Foveated was the **fastest** — reflecting a speed-accuracy trade-off where participants gave up trying to read degraded text sooner. Foveated achieved a perfect read depth of 5.00 with zero variance.

| Metric | Uniform | Foveated | Inverse-Foveated |
|---|---|---|---|
| Duration (s) | 47.12 ± 8.88 | 48.69 ± 8.61 | **30.82 ± 9.54** |
| Read Depth | 4.40 ± 1.34 | **5.00 ± 0.00** | 3.80 ± 1.10 |
| Overall Workload (RTLX) | 40.00 ± 14.52 | 40.83 ± 17.54 | 48.83 ± 16.46 |

![Identification read depth](docs/analysis/figures/identification_read_depth.png)

| DV | χ²(2) | p | Significant? |
|---|---|---|---|
| Duration | 6.400 | **.041** | **Yes** |
| Read Depth | 3.500 | .174 | No |
| Overall Workload (RTLX) | 2.800 | .247 | No |
| Mental Demand | 2.842 | .242 | No |
| Frustration | 3.111 | .211 | No |

---

### Visual Search (N = 4)

Participants searched for and navigated toward a target object. Frustration was significantly different across conditions (p = .050), with Inverse-Foveated producing dramatically higher frustration (77.60 vs ~40). The tight SD (12.50) indicates this was universally experienced.

| Metric | Uniform | Foveated | Inverse-Foveated |
|---|---|---|---|
| Duration (s) | 240.25 ± 215.19 | 136.87 ± 81.91 | 296.59 ± 230.13 |
| Collisions | 1.50 ± 1.73 | 2.00 ± 2.31 | 3.80 ± 3.90 |
| Overall Workload (RTLX) | 43.54 ± 26.30 | 46.88 ± 20.18 | 61.50 ± 13.99 |
| Frustration | 38.75 ± 35.21 | 43.75 ± 25.29 | **77.60 ± 12.50** |

| DV | χ²(2) | p | Significant? |
|---|---|---|---|
| Duration | 1.500 | .472 | No |
| Collisions | 4.000 | .135 | No |
| Overall Workload (RTLX) | 3.500 | .174 | No |
| Mental Demand | 4.933 | .085 | No |
| Frustration | 6.000 | **.050** | **Yes** |

---

### Subjective Workload (NASA-TLX)

![Overall Workload (RTLX)](docs/analysis/figures/nasa_tlx_Overall_Workload_(RTLX)_combined.png)

![Mental Demand](docs/analysis/figures/nasa_tlx_Mental_Demand_combined.png)

![Frustration](docs/analysis/figures/nasa_tlx_Frustration_combined.png)

### Individual Differences

![Individual differences across conditions](docs/analysis/figures/individual_differences_combined.png)

---

### Summary of Significant Effects

| Finding | Task | p |
|---|---|---|
| Inverse-Foveated increases overall workload (RTLX) | Navigation | .042 |
| Inverse-Foveated increases mental demand | Navigation | .019 |
| Inverse-Foveated decreases duration (speed-accuracy trade-off) | Identification | .041 |
| Inverse-Foveated increases frustration | Visual Search | .050 |

Across all three tasks, the **Inverse-Foveated** condition consistently produced the worst subjective experience. The pattern — Uniform ≤ Foveated < Inverse-Foveated for subjective workload — supports the hypothesis that degrading the foveal region is significantly more disruptive than degrading the periphery, even when total bandwidth is held constant.

---

## Repository Structure

```
PBL5_FoveatedTeleoperation/
├── foveated-teleop-unity/     # Unity project (VR client)
├── mock_pioneer/              # Python camera server & foveation pipeline
│   ├── server.py              # Main server
│   ├── foveation.py           # Gaze-contingent encoding
│   ├── rover_bridge.py        # ESP32 command translation
│   └── requirements.txt
├── docs/
│   ├── PROTOCOL.md            # TCP protocol reference
│   ├── bench_results.csv      # Bandwidth calibration data
│   └── analysis/              # Statistical analysis
│       ├── analyze_results.py
│       ├── fixation_classifier.py
│       ├── summary_statistics.md
│       ├── PBL5_Experiment_Spreadsheet-3.xlsx
│       └── figures/           # Generated plots (9 PNGs)
└── study_logs/                # Raw experimental data
    ├── task1/                 # Navigation
    ├── task2/                 # Identification
    └── task3/                 # Visual Search
```