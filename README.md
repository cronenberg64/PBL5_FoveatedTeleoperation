# PBL5 Foveated Teleoperation for Meta Quest 3

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

- `Assets/Scripts/Network/`: TCP logic for drive commands and JPEG camera feed reception.
- `Assets/Scripts/Control/`: Modernized input handling and gear-shift logic.
- `Assets/Scripts/Gaze/`: Gaze coordinate providers and foveated shader controllers.
- `Assets/Shaders/`: HLSL foveated rendering shader.
- `Assets/InputActions/`: Unity Input System action asset.

## Setup Instructions

1. **Unity Version**: Use Unity 2022.3 LTS or newer.
2. **Dependencies**: 
   - Meta XR SDK (or Oculus Integration)
   - XR Interaction Toolkit
   - Input System Package
3. **Configuration**:
   - Create a `NetworkConfig` ScriptableObject via `Assets > Create > Teleoperation > Network Config`.
   - Set the `Robot IP` and `Ports` (Standard: 1234 for Control, 1235 for Camera).
4. **Input**: Ensure "Active Input Handling" is set to "Both" or "Input System Package" in Project Settings.
5. **Shaders**: Assign the `Teleoperation/FoveatedFeed` shader to the Camera Feed `RawImage` material.