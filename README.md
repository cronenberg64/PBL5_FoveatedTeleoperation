# PBL5 Foveated Teleoperation for Meta Quest Pro

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

## Project Structure

The Unity project is located in the `foveated-teleop-unity/` subfolder.

- `Assets/Scripts/Network/`: TCP logic for drive commands and JPEG camera feed reception.
- `Assets/Scripts/Control/`: Modernized input handling and gear-shift logic.
- `Assets/Scripts/Gaze/`: Gaze coordinate providers and foveated shader controllers.
- `Assets/Shaders/`: HLSL foveated rendering shader.
- `Assets/InputActions/`: Unity Input System action asset.

## Setup & Testing Instructions

1. **Unity Version**: Use Unity 6 (`6000.4.3f1`) or newer.
2. **Dependencies**: Ensure the **Input System** and **XR Interaction Toolkit** packages are installed.

---

### Option A: Virtual Simulated Driving (Track Loopback)
In this mode, Unity captures the virtual 3D corridor, sends it to Python over TCP, Python applies foveation processing, and streams it back to display on the viewport.

1. **Start the Python Server in Unity mode**:
   ```powershell
   cd mock_pioneer
   python server.py --camera-source unity
   ```
2. **Open the Scene in Unity**:
   * Click **PBL5** > **Open Simulated Driving** (or open `Assets/Scenes/SimulatedDriving.unity`).
   * *If the scene is empty*, click **PBL5** > **Build Simulated Driving Scene** to generate the track.
3. **Press Play** in Unity to begin.

---

### Option B: Live Camera Teleoperation (Webcam Mode)
In this mode, the Python server streams your local webcam feed (simulating a physical robot camera) to the Unity screen.

1. **Start the Python Server in Webcam mode**:
   ```powershell
   cd mock_pioneer
   python server.py
   ```
2. **Open the Scene in Unity**:
   * Click **PBL5** > **Open Desktop Scene** (or open `Assets/Scenes/DesktopFoveated.unity`).
3. **Press Play** in Unity to begin.

---

### Controlling & Switching Foveation Conditions
While the Unity scene is running, you can dynamically switch between rendering modes using these keyboard hotkeys:

*   **Press 1**: **Uniform Mode** (`UniformQ50`) — Uniform quality across the entire screen (no foveation).
*   **Press 2**: **Foveated Mode** (`Foveated_15_85`) — Gaze-contingent foveation enabled! Periphery is rendered at Quality 15, while a high-resolution circle follows your cursor at Quality 85.
*   **Press 3**: **Peripheral Only** (`PeripheralOnly_Q30`) — Peripheral rendering at Quality 30.

**Driving Controls**: Press **W/A/S/D** to steer/drive, and **Space** to apply brakes.