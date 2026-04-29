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

The Unity project is located in the `PBL5_FoveatedTeleoperation/` subfolder.

- `Assets/Scripts/Network/`: TCP logic for drive commands and JPEG camera feed reception.
- `Assets/Scripts/Control/`: Modernized input handling and gear-shift logic.
- `Assets/Scripts/Gaze/`: Gaze coordinate providers and foveated shader controllers.
- `Assets/Shaders/`: HLSL foveated rendering shader.
- `Assets/InputActions/`: Unity Input System action asset.

## Setup Instructions

1. **Unity Version**: Use Unity 6 (6000.4.3f1) or newer.
2. **Dependencies**: 
   - Meta XR SDK (All-in-One)
   - XR Interaction Toolkit
   - Input System Package
3. **Configuration**:
   - Create a `NetworkConfig` ScriptableObject via `Assets > Create > Teleoperation > Network Config`.
   - Assign the `NetworkConfig` to the `RobotClient` and `TeleoperationController` scripts.
4. **Scene Setup**:
   - Add an **XR Origin (VR)**.
   - Create a **RobotManager** GameObject with `RobotClient` and `TeleoperationController`.
   - Create a **RawImage** for the viewport and attach `CameraFeedReceiver`, `GazeProvider`, and `FoveatedFeedController`.
5. **Shaders**: Assign the `Teleoperation/FoveatedFeed` shader to a new Material and apply it to the viewport **RawImage**.