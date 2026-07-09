# PBL5 Foveated Teleoperation for VIVE Focus Vision

A modern Unity-based teleoperation system for Pioneer robots, featuring **Gaze-Contingent Foveated Streaming** to optimize perceptual quality and bandwidth.

## Core Features

- **Gaze-Contingent Foveated Rendering**: A custom HLSL shader renders a high-quality "Foveal Circle" (Quality 50) at the user's focus point while pixelating the periphery (Quality 5) to simulate bandwidth savings.
- **Quest 3 & Pro Integration**: Supports hardware eye-tracking via OVRPlugin (Quest Pro) or head-gaze fallback (Quest 3).
- **Modern Input System**: Replaces legacy `Input.GetAxis` with Unity's Action-based Input System.
  - **Accelerator**: Right Trigger
  - **Brake**: Left Trigger
  - **Gear Shift**: A Button (toggles Forward/Reverse)
  - **Steer**: Right Thumbstick X
- **TCP Protocol Baseline**: Reconstructed `$CMD + turn(3) + speed(3) + \n` protocol for communication with the physical Pioneer robot.

## Hardware Architecture

The system is designed to operate on a physical Pioneer-style rover using a decentralized networking architecture to minimize latency:

1. **Workstation (Unity Client)**: Runs the VR interface and eye-tracking. It acts as a **TCP Server** to receive direct connections from the ESP32 for zero-latency motor control.
2. **Mini PC (On Rover)**: Runs `server.py` over WiFi. It handles camera capture, applies the OpenCV foveation encoding based on gaze coordinates, and streams the dual-payload video feed back to Unity.
3. **ESP32 (On Rover)**: Handles physical wheel/servo control. It connects directly to the Workstation's IP over WiFi to receive drive commands, completely bypassing the Mini PC.

## Setup & Testing Instructions

1. **Unity Version**: Use Unity 6 (`6000.4.3f1`) or newer.
2. **Dependencies**: Ensure the **Input System** and **XR Interaction Toolkit** packages are installed.
3. **Network Configuration**: Create or edit `NetworkConfig` (Assets → Create → Teleoperation → Network Config). Set the `robotIP` to your Mini PC's IP address. 

---

### Option A: Physical Rover Teleoperation (Live)
In this mode, the Workstation receives live video from the Mini PC and sends physical drive commands directly to the ESP32.

1. **Power up the Rover**:
   * Hook up the main battery to power the Mini PC.
   * Power on the ESP32 and ensure it connects to the lab WiFi.
2. **Start the Python Camera Server** (on the Mini PC):
   ```bash
   cd mock_pioneer
   python server.py
   ```
3. **Open the Scene in Unity**:
   * Open `Assets/Scenes/ViveFoveated.unity` (if using VR) or `DesktopFoveated.unity` (if running a desktop test with Tobii).
4. **Press Play** in Unity to begin. The ESP32 should automatically connect to your Workstation, and the video feed will start streaming.

---

### Option B: Virtual Simulated Driving (Loopback)
In this mode, Unity captures the virtual 3D corridor, sends it to Python over TCP, Python applies foveation processing, and streams it back to display on the viewport. Useful for testing without hardware.

1. **Start the Python Server in Unity mode** (on your Workstation):
   ```powershell
   cd mock_pioneer
   python server.py --camera-source unity
   ```
2. **Open the Scene in Unity**:
   * Open `Assets/Scenes/SimulatedDriving.unity`.
   * *If the scene is empty*, click **PBL5** > **Build Simulated Driving Scene** to generate the track.
3. **Press Play** in Unity to begin.

---

### Controlling & Switching Foveation Conditions
While the Unity scene is running, you can dynamically switch between rendering modes using these keyboard hotkeys:

*   **Press 1**: **Uniform Mode** (`UniformQ50`) — Uniform quality across the entire screen (no foveation).
*   **Press 2**: **Foveated Mode** (`Foveated_15_85`) — Gaze-contingent foveation enabled! Periphery is rendered at Quality 15, while a high-resolution circle follows your cursor at Quality 85.
*   **Press 3**: **Inverse-Foveated** (`InverseFoveated_TBD`) — Reversed gaze-contingency! Periphery is sharp while the gaze focal point is pixelated and degraded.

**Driving Controls**: Press **W/A/S/D** to steer/drive, and **Space** to apply brakes. Gear shifting is mapped to the **A Button** on VR controllers.

---

### Logging & User Study Controls

During a user study, you must tell Unity which task the user is performing *before* starting the trial timer. 

1. **Select Task Scenario**: Press one of the following keys to label the upcoming trial in the CSV:
   * **Press 7 (or C)**: Corridor (Navigation Task)
   * **Press 8 (or D)**: Doorway (Identification Task)
   * **Press 9 (or O)**: Obstacle (Visual Search Task)

2. **Select Rendering Condition**:
   * **Press 1, 2, or 3** to set the foveation mode (Uniform, Foveated, Inverse).
   * *Note: You cannot change the condition while a trial is actively recording!*

3. **Start/Stop Trial (CSV Logging)**:
   * Press **Enter** on the keyboard (or the **A** button on the VR controller) to **Start** the trial timer.
   * Press **Enter** again (or the **B** button on the VR controller) to **End** the trial and save the CSV row.
   * *Warning: Do not double-press or mash the Start/Stop buttons, or you may cause a file sharing violation on the CSV.*