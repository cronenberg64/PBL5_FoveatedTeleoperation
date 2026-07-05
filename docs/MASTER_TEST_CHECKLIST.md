# Master Test & Verification Checklist

**PBL5 Foveated Teleoperation — All tests in one document.**

---

## 1-Week Sprint Tracking (Real Rover Pivot)

> **Note**: This is the current active sprint. The simulation phases below (Phase A.X) are now the fallback plan.

| Day | Task Focus | Acceptance Met | Carry-over / Notes |
|---|---|---|---|
| 1 | Workstation Input + Foveation Fix | [x] | Pedals bypassed, mapped to keyboard W/S |
| 2 | Mini PC Setup + Network Plumbing | [x] | Network static IPs verified, TCP connection stable |
| 3 | rover_bridge.py + ESP32 Connection | [x] | Hardware fixed by professor, wires spliced! |
| 4 | End-to-End Bench Test | [/] | Setup on stand, ready to test! |
| 5 | Mobile Rover Test | ☐ | |
| 6 | Buffer + Stability Pass | ☐ | |
| 7 | Decision Point + Demo Prep | ☐ | |

### Day 1 — Workstation Input + Foveation Fix
- [x] Foveation visibly rendering in headset (foveal patch follows gaze) *(Fixed previously, verify on new run)*
- [x] Serafim R1+ recognized by Windows
- [x] Unity Input System reads wheel + pedals
- [x] Capsule in ViveFoveated responds to wheel turn + pedal presses
- [x] Keyboard fallback still works for debugging

### Day 2 — Mini PC Setup + Network Plumbing
- [x] Mini PC running mock_pioneer, accessible from workstation over LAN
- [x] Webcam feed visible in Unity on workstation
- [x] Foveation works end-to-end across the network
- [x] Latency feels tolerable for driving (subjective)

### Day 3 — rover_bridge.py + ESP32 Connection
- [x] Active ESP32 identified and confirmed
- [x] rover_bridge.py running on mini PC
- [x] ESP32 successfully connects to bridge on boot
- [x] Manual drive command causes wheels to spin *(Hardware spliced!)*
- [x] Manual steering command causes servo to move *(Power connected!)*

### Day 4 — End-to-End Bench Test
- [x] Webcam feed visible in headset with foveation
- [x] Wheel + pedals control rover motors and servo
- [x] Total round-trip latency feels driveable
- [x] Foveal patch tracks gaze accurately

### Day 5 — Mobile Rover Test
- [x] Rover drives untethered on the ground
- [x] WiFi range covers expected operating area
- [x] Battery lasts at least one full session
- [x] Rover can navigate a simple obstacle course (success not required, just attempt)
### Day 6 — Buffer + Stability Pass
- [ ] System is stable for at least one continuous 15-minute drive session
- [ ] Documented startup procedure (what to power on first, command order, etc.)
- [ ] At least one good demo video captured

### Day 7 — Decision Point
- [ ] **DECISION MADE:** Commit to Rover OR Pivot to Simulation

---

## Phase A.1.5 — Foveation Rendering Verification (Simulation Fallback)

Run these ONCE before any study subjects.

### Server-Side Verification

- [x] Start mock_pioneer in venv:
  ```powershell
  cd mock_pioneer
  ./venv/scripts/activate.ps1
  python server.py --camera-source unity --mode gaze --periph-quality 15 --fovea-quality 85 --show
  ```
- [x] `--show` window opens and displays a frame (not blank)
- [x] Server startup log prints: `Encoding peripheral at quality 15, foveal at quality 85`
- [x] Open `logs/server_session_*.csv` — confirm `bytes_fovea` column is non-zero on most rows

### Unity Editor Verification (DesktopFoveated first)

- [x] Open `DesktopFoveated.unity`, hit Play
- [x] Console shows `[CameraFeedReceiver] Connected successfully!`
- [x] Canvas shows the camera feed (not blank white)
- [x] Move mouse to corner of screen → sharp region follows mouse, opposite corner is pixelated
- [x] Press 1 → Uniform: entire canvas same quality (no foveal patch)
- [x] Press 2 → Foveated: sharp patch at mouse, periphery pixelated
- [x] Press 3 → PeripheralOnly: everything uniformly blurry, no sharp patch

### VR Headset Verification (ViveFoveated)

- [x] Vive Streaming Hub on PC shows Focus Vision connected
- [x] Open `ViveFoveated.unity`, hit Play
- [x] Console shows `[SceneFrameStreamer] Connected successfully.`
- [x] Console shows `[CameraFeedReceiver] Connected successfully!`
- [x] Console shows `[GazeProvider] OpenXR Eye tracking dynamically detected!`
- [x] World-space canvas visible at ~2m distance, readable
- [x] Look at CORNER of canvas → corner is sharp, opposite corner is blurry
- [x] Look at CENTER → center is sharp, edges are blurry
- [x] Effect is driven by EYE gaze, not HEAD direction (turn head but look at same spot — patch stays)
- [x] Move robot: Use Right Thumbstick (VR) OR W/A/S/D/Arrow Keys (Keyboard) to drive forward/back and steer left/right
- [x] Brake robot: Squeeze Right Controller Grip Trigger (VR) OR Spacebar (Keyboard)
- [x] Reset robot position: Press 'R' on keyboard
- [x] Switch maps (Scenarios): Press 7, 8, or 9 (OR C, D, O) on keyboard to switch to Corridor, Doorway, or Obstacle scenarios. NOTE: Do NOT use 1, 2, or 3 for maps!
- [x] A button (right controller) OR F5 (Keyboard) → Console shows "Start Trial"
- [x] B button (right controller) OR F6 (Keyboard) → Console shows "End Trial | Success: True"
- [x] Y button (left controller) OR F7 (Keyboard) → Console shows "End Trial | Success: False"
- [x] Open `logs/trial_metrics_*.csv` → confirm rows written with correct fields

### Condition Switching (while in VR, assistant presses keyboard)

- [x] Press 1 → Uniform: canvas uniformly crisp, no foveal effect
- [ ] Press 2 → Foveated: sharp patch follows eye gaze, periphery pixelated *(Fix deployed, pending verification)*
- [x] Press 3 → PeripheralOnly: everything uniformly blurry

---

## Phase A.2 — End-to-End Smoke Test (Self-Test, 9 Trials)

Complete ALL 9 trial combinations yourself before any subject sees the system.

### Pre-Run Setup
- [ ] mock_pioneer running with: `--camera-source unity --mode gaze --periph-quality 15 --fovea-quality 85`
- [ ] ViveFoveated scene loaded and playing
- [ ] Eye tracking calibrated (via Vive Hub on headset)
- [ ] Timer ready (phone or watch)

### Trial Matrix

| Trial | Scenario | Condition | Start (A) | End | Time | Collisions | Notes |
|-------|----------|-----------|-----------|-----|------|------------|-------|
| 1 | Corridor | Uniform (press 1) | [ ] | [ ] | ___s | ___ | |
| 2 | Corridor | Foveated (press 2) | [ ] | [ ] | ___s | ___ | |
| 3 | Corridor | PeripheralOnly (press 3) | [ ] | [ ] | ___s | ___ | |
| 4 | Doorway | Uniform (press 1) | [ ] | [ ] | ___s | ___ | |
| 5 | Doorway | Foveated (press 2) | [ ] | [ ] | ___s | ___ | |
| 6 | Doorway | PeripheralOnly (press 3) | [ ] | [ ] | ___s | ___ | |
| 7 | Obstacle | Uniform (press 1) | [ ] | [ ] | ___s | ___ | |
| 8 | Obstacle | Foveated (press 2) | [ ] | [ ] | ___s | ___ | |
| 9 | Obstacle | PeripheralOnly (press 3) | [ ] | [ ] | ___s | ___ | |

### Post-Run Verification
- [ ] Open `logs/trial_metrics_*.csv` — exactly 9 rows
- [ ] All fields populated (no empty cells)
- [ ] No NullReferenceException or crashes in Unity Console during full session
- [ ] Total session time: _____ minutes (target: 30–40 min)
- [ ] Motion sickness level: None / Mild / Moderate / Severe
- [ ] Conditions are visually distinguishable to you
- [ ] Eye tracking stayed calibrated throughout (foveal patch didn't drift)

### If Issues Found
- Frame rate too low → check Unity Stats overlay, aim for 90fps
- Latency too high → check `logs/server_session_*.csv` latency column, target <100ms
- Capsule speed too high → reduce `moveSpeed` in RobotController Inspector
- HUD jitter → confirm HUD is parented to Camera, not world root

---

## Phase A.4 — Pilot Trials (2 Colleagues)

### Pilot 1: _____________________ Date: __________

- [ ] Pre-screening form completed — no exclusion criteria
- [ ] Consent form signed
- [ ] Briefing script read aloud verbatim
- [ ] Eye tracking calibrated
- [ ] 1 practice trial completed
- [ ] 9 main trials completed (condition order per Latin Square)
- [ ] Post-condition surveys completed (3x)
- [ ] Post-session survey completed
- [ ] CSV saved and backed up
- [ ] Notes on confusion points: _________________________________
- [ ] Session duration: _____ min

### Pilot 2: _____________________ Date: __________

- [ ] Pre-screening form completed — no exclusion criteria
- [ ] Consent form signed
- [ ] Briefing script read aloud verbatim
- [ ] Eye tracking calibrated
- [ ] 1 practice trial completed
- [ ] 9 main trials completed (condition order per Latin Square)
- [ ] Post-condition surveys completed (3x)
- [ ] Post-session survey completed
- [ ] CSV saved and backed up
- [ ] Notes on confusion points: _________________________________
- [ ] Session duration: _____ min

### Post-Pilot Review
- [ ] Briefing script revised based on confusion points
- [ ] Per-subject timing estimate confirmed: _____ min
- [ ] No fatal protocol issues identified
- [ ] Pilot data saved to `logs/pilots/` subfolder (NOT included in main analysis)

---

## Phase A.5 — Main Study Session Checklist (Per Subject)

**Print one copy of this page per subject.**

Subject ID: S____ | Date: __________ | Time: __________
Latin Square Ordering ID: ____ | Condition Order: __ → __ → __

### Pre-Session (Minutes 0–5)
- [ ] Pre-screening questionnaire completed
- [ ] No exclusion criteria triggered
- [ ] Consent form signed (both copies)
- [ ] Subject ID assigned (DO NOT write name on data files)

### Briefing (Minutes 5–10)
- [ ] Briefing script read aloud VERBATIM
- [ ] Subject asked "any questions?" — answered without revealing conditions
- [ ] Headset fit adjusted
- [ ] Eye tracking calibrated via Vive Hub

### Practice (Minutes 10–15)
- [ ] 1 practice trial with Uniform condition, any scenario
- [ ] Subject understands controls (thumbstick + buttons)

### Main Trials (Minutes 15–50)

Condition order for this subject (from Latin Square sheet):
1st condition: _____________ | 2nd condition: _____________ | 3rd condition: _____________

| Trial | Scenario | Condition | Assistant Action | Started? | Ended? |
|-------|----------|-----------|-----------------|----------|--------|
| 1 | Corridor | _________ | Press __ on keyboard | [ ] | [ ] |
| 2 | Doorway | _________ | (same condition) | [ ] | [ ] |
| 3 | Obstacle | _________ | (same condition) | [ ] | [ ] |
| | | | **Post-condition survey #1** | | [ ] |
| 4 | Corridor | _________ | Press __ on keyboard | [ ] | [ ] |
| 5 | Doorway | _________ | (same condition) | [ ] | [ ] |
| 6 | Obstacle | _________ | (same condition) | [ ] | [ ] |
| | | | **Post-condition survey #2** | | [ ] |
| 7 | Corridor | _________ | Press __ on keyboard | [ ] | [ ] |
| 8 | Doorway | _________ | (same condition) | [ ] | [ ] |
| 9 | Obstacle | _________ | (same condition) | [ ] | [ ] |
| | | | **Post-condition survey #3** | | [ ] |

### Post-Session (Minutes 50–60)
- [ ] Post-session survey completed
- [ ] Debrief: reveal what the 3 conditions were (only after all data collected)
- [ ] Thank subject

### Data Management
- [ ] CSV file saved: `trial_metrics_S____.csv`
- [ ] CSV backed up to cloud / external drive
- [ ] Post-condition survey data entered into spreadsheet
- [ ] Post-session survey data entered into spreadsheet
- [ ] Subject tracking spreadsheet updated

### Researcher Notes
- Any deviations from protocol: _________________________________
- Technical issues: _________________________________
- Subject observations: _________________________________

---

## Quick Reference: Key Commands

| Action | Command |
|--------|---------|
| Start mock_pioneer on Mini PC | `python server.py --camera-source rover --camera-index 0 --mode gaze --rover-out` |
| Switch to Uniform | Press `1` on keyboard |
| Switch to Foveated | Press `2` on keyboard |
| Switch to PeripheralOnly | Press `3` on keyboard |
| Switch Scenarios (If applicable) | Press `7`, `8`, or `9` on keyboard |
| Keyboard Drive/Steer | Press `W`/`S` (Forward/Backward), `A`/`D` (Steer Left/Right) or Arrow keys |
| Serafim R1+ Steering | Turn Wheel |
| Serafim R1+ Gas/Brake | Press Pedals |
| Start trial (keyboard) | `F5` |
| End trial success (keyboard) | `F6` |
| End trial failure (keyboard) | `F7` |
