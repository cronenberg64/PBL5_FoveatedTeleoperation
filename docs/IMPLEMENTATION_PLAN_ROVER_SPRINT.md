# PBL5 Master Implementation Plan — Real Rover Sprint (1-Week)

**Version:** Final pivot — rover-primary, simulation-fallback
**Sprint length:** 7 working days from start
**Hard decision point:** End of Day 7 — if rover not teleoperating, pivot to simulation-only

---

## What Changed Since Last Plan

| Item | Old | New |
|---|---|---|
| 360° camera | Primary for System B | **Dropped.** Single webcam on rover. |
| Lab workstation OS | CachyOS Linux | **Windows** (both workstation and mini PC) |
| Operator input | VR thumbsticks | **Serafim R1+ steering wheel + pedals** |
| Primary deliverable | Simulation user study | **Real rover teleoperation** |
| Fallback | Real rover demo | Simulation-only user study |
| Timeline | 2 weeks user study | **1-week sprint + decision** |

---

## Hardware Inventory (Current)

| Component | Location | Status |
|---|---|---|
| VINE lab workstation | Operator station | Windows, primary dev/run machine |
| Vive Focus Vision | Operator station | Windows tethered (PCVR) |
| Serafim R1+ wheel + pedals | Operator station | USB connected to workstation |
| TRIGKEY Mini PC | Mounted on rover | Windows, will run mock_pioneer + rover_bridge |
| USB webcam | Mounted on rover | Wired to mini PC USB |
| ESP32 (drive controller) | Mounted on rover | Connects out via WiFi to mini PC port 1234 |
| ESP32 (secondary, possibly) | Mounted on rover | Confirm role with Chandler |
| Drive motor + steering servo | Rover chassis | Controlled by primary ESP32 |
| Battery | Rover chassis | Anker power station visible in photo |

---

## Hardware Identification Notes

### Serafim R1+ Connection

- **Wheel base → workstation:** Single USB cable
- **Pedals → wheel base:** RJ9/RJ11 telephone connector (the clear plastic plug)
- **Windows recognition:** Should appear as a single USB game controller in Windows Game Controllers (`joy.cpl`). No vendor driver needed for basic HID axes.
- **Expected axes (typical wheel layout):**
  - Steering: X-axis, range -1 to +1 (center at 0)
  - Gas pedal: typically Z or Rz axis, 0 to 1
  - Brake pedal: typically Y or separate axis, 0 to 1
  - Discover actual mappings via Unity Input Debug window in Day 1

### ESP32 Setup

- **Two ESP32 boards visible in rover photo.** Main control ESP32 is the larger one on the right (cream PCB, with USB-C, near FRONT label). The smaller one on the left may be for sensors/auxiliary functions — **confirm with Chandler before relying on either.**
- **Firmware behavior** (from Phase 8 firmware review):
  - Connects OUT to TCP server at hardcoded host IP, port 1234
  - Protocol: `CMD{srv:3}{mtr:3}\n` (servo position + motor speed encoded)
  - WiFi credentials hardcoded for `VINE5` network
  - **Will need:** mini PC's IP to match the hardcoded target (or firmware update)

---

## System Architecture (Final)

```
┌────────────────────────────────────────────────────────────────┐
│ OPERATOR STATION (VINE lab workstation, Windows)                  │
│                                                                  │
│  ┌─────────────────────┐   ┌──────────────────────────────────┐│
│  │ Vive Focus Vision   │   │ Serafim R1+ wheel + pedals       ││
│  │ (PCVR tethered)     │   │ (USB game controller, HID)        ││
│  └────────┬────────────┘   └──────────┬───────────────────────┘│
│           │ eye gaze data              │ steering + gas + brake  │
│           ▼                            ▼                          │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ Unity: ViveFoveated.unity                                  │ │
│  │  - OpenXR XR rig + world-space canvas                      │ │
│  │  - GazeProvider → screen UV → upload to port 1236          │ │
│  │  - WheelInput → drive commands → send to port 1234         │ │
│  │  - CameraFeedReceiver → display foveated stream on canvas │ │
│  └────────────────────┬───────────────────────────────────────┘ │
└───────────────────────┼──────────────────────────────────────────┘
                        │ LAN (lab WiFi or Ethernet)
                        │ TCP: 1234 (cmds), 1235 (video), 1236 (gaze)
                        ▼
┌────────────────────────────────────────────────────────────────┐
│ ROVER (TRIGKEY mini PC, Windows)                                │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ mock_pioneer/server.py                                      │ │
│  │   --camera-source rover --camera-index 0                   │ │
│  │   --mode gaze --periph-quality 15 --fovea-quality 85       │ │
│  │   - Reads webcam → encodes dual-payload JPEG → port 1235   │ │
│  │   - Receives gaze on 1236, drive on 1234                   │ │
│  └─────────────────────────┬──────────────────────────────────┘ │
│                            │ drive commands                       │
│                            ▼                                       │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ rover_bridge.py                                             │ │
│  │   - Listens on port 1238 for ESP32 connection              │ │
│  │   - Translates Unity protocol → CMD{srv:3}{mtr:3}\n        │ │
│  │   - Sends to ESP32 over the same WiFi network              │ │
│  └─────────────────────────┬──────────────────────────────────┘ │
│                            │                                       │
│                  USB Webcam ───► OpenCV ingest                    │
└────────────────────────────┼──────────────────────────────────────┘
                             │ Rover-local WiFi
                             ▼
                  ┌───────────────────────────┐
                  │ ESP32 (drive controller)   │
                  │  - WiFi client → mini PC   │
                  │  - "CMD{srv}{mtr}" parsing │
                  │  - PWM ramp to motor       │
                  │  - Servo position update   │
                  └───────────┬───────────────┘
                              │
                  ┌───────────▼─────────────────┐
                  │ Drive motor + steering servo │
                  └──────────────────────────────┘
```

---

## 1-Week Sprint Plan

Each day is bounded by acceptance criteria. **Do not move to the next day until criteria are green.** Slip days roll forward into the buffer; if you eat the buffer, hit Day 7 decision point earlier.

### Day 1 — Workstation Input + Foveation Fix (HIGH RISK)

This day has two parallel tasks. The foveation fix is a blocker carried from the previous plan; the wheel integration is the first new piece of work. Both need to land today.

**Task 1.1: Fix foveation rendering in headset (~30-60 min)**

From the previous plan's Phase A.1.5:
1. Open ViveFoveated.unity in Unity Editor
2. Click the world-space RawImage in the Hierarchy
3. Inspector → Material slot — if "UI/Default", drag FoveatedFeed material onto it
4. Start mock_pioneer with `--mode gaze --periph-quality 15 --fovea-quality 85 --show`
5. Press Play, put on headset
6. Foveal patch should follow your eye, periphery visibly blurry

If step 3 doesn't fix it, send the diagnostic agent prompt (in Appendix A below).

**Task 1.2: Serafim R1+ identification and basic input wiring (~2-3 hours)**

1. **Hardware verification:**
   - Plug Serafim R1+ wheel base into workstation via USB
   - Plug pedals into wheel base via RJ9/RJ11 cable
   - Open Windows: Win+R → `joy.cpl` → Enter
   - Should see the wheel in the list. Click Properties.
   - Turn the wheel — X axis should respond. Press gas/brake — Z or Rz axis should respond.
   - **Document which axis is which.** Take a screenshot.

2. **Unity Input System discovery:**
   - In Unity, Window → Analysis → Input Debugger
   - Plug in the wheel, refresh
   - Should see a new device (likely "HID Joystick" or "Generic Gamepad")
   - Click into it, watch the axis values as you turn the wheel / press pedals
   - Note the exact action path (e.g., `<Joystick>/stick/x`, `<Joystick>/rz`)

3. **Agent prompt to integrate wheel input:**

```
Add Serafim R1+ steering wheel input to RobotController.cs and the 
Input System action map. The wheel is plugged into the operator 
workstation as a USB HID game controller. It exposes:

- Steering: X axis, range -1 to +1 (center 0)
- Gas pedal: separate axis (typically Z or Rz), range 0 to 1
- Brake pedal: separate axis (typically Y or another Z variant), 
  range 0 to 1

Before wiring, USE THE UNITY INPUT DEBUGGER to confirm the exact 
binding paths. Don't assume — check the actual device's exposed 
controls. The binding paths I'm guessing are <Joystick>/stick/x, 
<Joystick>/rz, <Joystick>/z but they could differ.

Implementation:

1. In Assets/Settings/TeleoperationInput.inputactions, add a new 
   Action Map "Wheel" with actions:
   - Steer (Pass Through, axis float)
   - Gas (Pass Through, axis float, 0-1)
   - Brake (Pass Through, axis float, 0-1)
   
2. Bind each action to the discovered wheel/pedal axis paths

3. In RobotController.cs, add a new input mode "Wheel" alongside 
   existing WASD and VR controller modes. Drive logic:
   - forward_speed = Gas_value * max_forward_speed
   - reverse_speed = Brake_value * max_reverse_speed  
     (if both pressed simultaneously, prioritize brake — same as 
     real cars when brake-throttle override is engaged)
   - turn_rate = Steer_value * max_turn_rate
   - Apply same scaled bounds as the WASD path

4. Add a public bool "useWheel" in the inspector. When true, read 
   wheel inputs in addition to (or instead of?) WASD. For now: 
   ADDITIVE — wheel + WASD both contribute. This way you can debug 
   without the wheel plugged in.

5. Add a dead zone (e.g., 0.05) on the steering axis to ignore 
   centering jitter from the wheel's hall sensors.

6. Log to console at startup: detected wheel device name, 
   confirmed binding paths, "Wheel input active: YES/NO".

Acceptance: 
- Plug in Serafim R1+, run scene, turn wheel → capsule turns
- Press gas → capsule accelerates forward; press brake → decelerates/reverses
- WASD still works simultaneously for keyboard fallback
```

**Acceptance criteria for Day 1:**
- [ ] Foveation visibly rendering in headset (foveal patch follows gaze)
- [ ] Serafim R1+ recognized by Windows
- [ ] Unity Input System reads wheel + pedals
- [ ] Capsule in ViveFoveated responds to wheel turn + pedal presses
- [ ] Keyboard fallback still works for debugging

If Day 1 doesn't end with both green: prioritize foveation first, push wheel to Day 2.

---

### Day 2 — Mini PC Setup + Network Plumbing

**Task 2.1: Mini PC Python + dependencies (~45 min)**

```powershell
# On mini PC (Windows)
python --version  # confirm 3.10+

# If not installed, download from python.org

# Install dependencies
pip install opencv-python numpy pillow

# Clone or copy mock_pioneer code
# Use USB drive, git clone, or rsync from workstation
```

**Task 2.2: Webcam test on mini PC (~30 min)**

```powershell
# Identify webcam index
python -c "import cv2; cap = cv2.VideoCapture(0); print('opened:', cap.isOpened(), 'res:', int(cap.get(3)), 'x', int(cap.get(4)))"

# Test mock_pioneer with the webcam
python server.py --mode uniform --quality 50 --show
```

Window should open showing the webcam feed. If not, try indices 1, 2, etc.

**Task 2.3: Network configuration (~1 hour)**

1. **Static IPs for both machines:**
   - Workstation: e.g., `192.168.0.100`
   - Mini PC: e.g., `192.168.0.208` (to match the ESP32 firmware's hardcoded target)
   - Set via Windows Network Settings → Properties → IPv4 Manual

2. **Firewall:**
   - On mini PC: allow incoming TCP on ports 1234, 1235, 1236, 1238
   - Use `New-NetFirewallRule` PowerShell command or Windows Defender Firewall GUI

3. **Connectivity test:**
   - From workstation: `ping 192.168.0.208`
   - From workstation: `Test-NetConnection -ComputerName 192.168.0.208 -Port 1234`
   - Both should succeed

4. **Unity NetworkConfig update:**
   - In Unity, find the NetworkConfig ScriptableObject
   - Change the host IP from `127.0.0.1` to `192.168.0.208`
   - Save

**Task 2.4: First cross-machine test (~30 min)**

- Run mock_pioneer on mini PC with `--camera-source webcam --mode gaze --periph-quality 15 --fovea-quality 85`
- Run Unity ViveFoveated on workstation
- Confirm: workstation displays webcam feed from mini PC, foveation works with mouse gaze (use mouse proxy first, then test with headset)

**Acceptance criteria for Day 2:**
- [ ] Mini PC running mock_pioneer, accessible from workstation over LAN
- [ ] Webcam feed visible in Unity on workstation
- [ ] Foveation works end-to-end across the network
- [ ] Latency feels tolerable for driving (subjective — should be obviously responsive)

---

### Day 3 — rover_bridge.py + ESP32 Connection (HIGH RISK)

This day is high risk because the ESP32 firmware is the unknown — Chandler hasn't touched it since 2024, and we're depending on the protocol behavior being as documented.

**Task 3.1: Confirm which ESP32 is active (~30 min)**

- Visit the rover, look at it physically
- Use a USB cable to connect each ESP32 to a laptop running Arduino IDE
- Open Serial Monitor (115200 baud)
- Power on the rover
- Watch the serial output — the active ESP32 should print connection attempts: "Connecting to host..." or similar
- Identify which one is running Chandler's firmware
- **Mark the inactive one with tape so you don't get confused**

**Task 3.2: rover_bridge.py implementation**

Agent prompt:

```
Create mock_pioneer/rover_bridge.py: Python middleware that translates 
between Unity's drive command protocol and the ESP32's CMD protocol.

Architecture:
- mock_pioneer/server.py listens on port 1234 for Unity drive commands 
  in format $[cmd][turn:3][speed:3]\n (cmd is F/R/S, turn is 0-999, 
  speed is 0-255)
- The ESP32 firmware connects OUT to TCP port 1234 and receives 
  commands in format CMD[srv:3][mtr:3]\n (srv is servo angle 30-150, 
  mtr is motor speed encoded as 255+signed_speed clamped to [55, 455])

The bridge sits between them: it acts as the TCP server the ESP32 
dials into, and it receives translated drive commands from server.py.

IMPLEMENTATION (use Option A from previous design — module imported 
by server.py):

1. Create RoverBridge class in mock_pioneer/rover_bridge.py:
   - start(listen_port=1238): starts the TCP server in a thread
   - send_drive_command(turn, speed, direction): translates and 
     sends to connected ESP32
   - stop(): sends END to ESP32, closes connection

2. Modify mock_pioneer/server.py:
   - Add CLI flag --rover-out (action=store_true)
   - When set, start RoverBridge on listen port 1238
   - In the existing control_thread that parses Unity drive commands, 
     after parsing each command, call:
       bridge.send_drive_command(turn=parsed_turn, 
                                 speed=parsed_speed,
                                 direction=parsed_cmd)

3. Translation logic:
   - Unity turn [0, 999] → ESP32 servo:
       servo = int(30 + (turn / 999.0) * 120)
       # Maps Unity center (500) → servo 90 (center)
   - Unity (direction, speed) → ESP32 motor:
       if direction == 'F': mtr = 255 + min(speed * 200 // 255, 200)
       elif direction == 'R': mtr = 255 - min(speed * 200 // 255, 200)
       elif direction == 'S': mtr = 255
       # Scales Unity 0-255 to ESP32's safe range of 0-200

4. Logging:
   - Log every translated command to logs/bridge_session_<ts>.csv:
     timestamp, unity_dir, unity_turn, unity_speed, esp32_srv, 
     esp32_mtr, esp32_connected (bool), send_success (bool)

5. Connection management:
   - If the ESP32 disconnects, accept reconnection automatically
   - On Ctrl+C / shutdown, send "END\n" to ESP32 to stop motors safely
   - Print to console when ESP32 connects/disconnects with timestamp

6. Add docs entry in mock_pioneer/docs/PROTOCOL.md describing the 
   translation logic.

Acceptance:
- Run server.py --rover-out on mini PC
- Power on rover (ESP32 should auto-connect since its hardcoded host 
  matches the mini PC's static IP)
- Console shows "ESP32 connected from <IP>"
- Send a test Unity command via a small Python test client to 
  127.0.0.1:1234 with $F500100\n (forward, center steering, 
  medium speed)
- Bridge log shows the translation, motors on the rover should spin 
  forward
- Send $S500000\n (stop) - motors should stop
```

**Task 3.3: First motor test (~30 min)**

- Lift the rover so its wheels are OFF the ground (don't let it drive away during testing)
- Start everything: mock_pioneer with --rover-out, ESP32 powered on
- Use the test client (or Unity) to send a few drive commands
- **Motors should physically spin.** This is the moment of truth for hardware integration.

**Acceptance criteria for Day 3:**
- [ ] Active ESP32 identified and confirmed
- [ ] rover_bridge.py running on mini PC
- [ ] ESP32 successfully connects to bridge on boot
- [ ] Manual drive command causes wheels to spin
- [ ] Manual steering command causes servo to move

---

### Day 4 — End-to-End Bench Test

Goal: full pipeline with rover stationary on the bench (wheels off ground), driven from the headset + wheel.

**Task 4.1: Full pipeline assembly (~1 hour)**

1. **On mini PC:**
   ```powershell
   python server.py --camera-source rover --camera-index 0 ^
     --mode gaze --periph-quality 15 --fovea-quality 85 --rover-out
   ```

2. **On workstation:**
   - Power on Vive Focus Vision, calibrate eye tracking
   - Plug in Serafim R1+
   - Open Unity, load ViveFoveated.unity
   - Verify NetworkConfig IP points at mini PC
   - Press Play → put on headset

3. **What should happen:**
   - Headset displays webcam feed (rover's view) with foveation
   - Foveal patch follows your eye
   - Turning the wheel sends steering commands → rover's servo moves
   - Pressing gas sends forward command → rover wheels spin forward
   - Pressing brake sends reverse/stop → wheels stop or spin backward

**Task 4.2: Latency assessment (~30 min)**

1. Use stopwatch or stopwatch app: measure visual delay
   - Wave your hand in front of the rover's webcam
   - Note how long until you see it in the headset
   - Estimate roughly (10-50ms ideal, 100-200ms tolerable, >300ms broken)

2. Measure command-to-action delay:
   - Press gas
   - Note how long until wheels start spinning
   - Same scale

3. **If latency is bad:** see Appendix B (latency optimization checklist)

**Task 4.3: Foveal patch alignment verification (~15 min)**

- Look at one extreme corner of the headset's view of the webcam feed
- The foveal patch (sharp area) should be in that corner
- If it's not, gaze coords from headset to server are misaligned
- Diagnostic: log gaze UVs in mock_pioneer console, see if they match where you're looking

**Acceptance criteria for Day 4:**
- [ ] Webcam feed visible in headset with foveation
- [ ] Wheel + pedals control rover motors and servo
- [ ] Total round-trip latency feels driveable (subjective: can you make a turn intentionally?)
- [ ] Foveal patch tracks gaze accurately

---

### Day 5 — Mobile Rover Test

Goal: rover on the ground, battery-powered, driving around the lab.

**Task 5.1: Battery and tethering check (~30 min)**

- Confirm the rover's battery (Anker power station in the photo) can power the mini PC + ESP32 + motors for at least 30 minutes
- Check WiFi range from rover to access point — drive the rover to corners of the lab and watch for packet loss in the bridge logs
- If using lab WiFi (VINE5), confirm both mini PC and rover-side ESP32 stay connected as the rover moves

**Task 5.2: First mobile drive (~1 hour)**

- Set up an open space in the lab (clear floor area, ~5x5 meters minimum)
- Place rover at one end
- Operator wears headset at the other end (or on a different floor — test WiFi reach)
- Drive around, do figure-8s, corner turns, forward/back maneuvers
- **Set a max speed initially** — don't ram it into walls. Tune the ESP32-side speed cap if needed.

**Task 5.3: Course practice (~1 hour)**

- Set up a simple corridor with chairs/boxes
- Try to navigate the rover through it
- This is the closest you'll get to a user-study scenario in this sprint

**Acceptance criteria for Day 5:**
- [ ] Rover drives untethered on the ground
- [ ] WiFi range covers expected operating area
- [ ] Battery lasts at least one full session
- [ ] Rover can navigate a simple obstacle course (success not required, just attempt)

---

### Day 6 — Buffer + Stability Pass

If you used buffer in previous days, this day is yours. If not, use it for:

**Task 6.1: Latency optimization (if needed)**

Run through Appendix B's checklist, apply changes that have most impact.

**Task 6.2: Failure mode handling (~2 hours)**

- What happens when ESP32 loses WiFi? Test by disconnecting briefly.
- What happens when webcam drops frames? Test by unplugging webcam.
- What happens when mini PC reboots? Test full restart procedure.
- Document the boot-up checklist for tomorrow's demo.

**Task 6.3: Polish for demo (~2 hours)**

- Clean up any obvious bugs from the previous days
- Update HUD elements if needed (e.g., add "rover connected" indicator)
- Take a few sample drive videos for the presentation

**Acceptance criteria for Day 6:**
- [ ] System is stable for at least one continuous 15-minute drive session
- [ ] Documented startup procedure (what to power on first, command order, etc.)
- [ ] At least one good demo video captured

---

### Day 7 — Decision Point + Demo Prep (or Pivot)

**End-of-day-7 decision:**

```
IF rover successfully teleoperating end-to-end:
    -> Commit to rover-based deliverable
    -> Continue with brief user testing or just demo presentation
    -> See "Post-Sprint: Rover Path" below

IF rover NOT working (key blocker remains):
    -> Pivot to simulation-only deliverable
    -> See "Post-Sprint: Simulation Fallback" below
    -> The rover work is not wasted — it's documented as 
       "real-world hardware integration attempted" in the writeup
```

The decision is binary. Don't try to half-pivot — pick one path and execute. Spend Day 7 morning evaluating; commit to a path by lunch; spend afternoon executing on that path.

---

## Post-Sprint Paths

### Post-Sprint: Rover Path (if rover works)

1. **Brief user pilots (3-5 lab colleagues, 1 day):**
   - Same protocol shape as original Phase A.4
   - But: only the foveation conditions you care about
   - Real-rover trials, not simulation
   - Single corridor scenario is enough for the demo deliverable

2. **Data collection (2-3 days):**
   - 12+ subjects if approval allows
   - Drive the rover through 1-3 physical scenarios per subject
   - Same conditions: Uniform / Foveated / Peripheral-only
   - CSV logs from rover_bridge + trial metrics

3. **Analysis + writeup (2 days):**
   - Use analyze_study_results.py from original Phase A.6
   - Emphasis: "live teleoperation of a physical rover" vs simulation framings

### Post-Sprint: Simulation Fallback (if rover doesn't work by Day 7)

1. **Restore SimulatedDriving as primary deliverable.** It's already built.

2. **Run Phase A.4-A.6 from the original plan** (pilot trials, main study, analysis). Materials in the previous plan's Appendix.

3. **Frame the rover work honestly in the writeup:**
   - "Physical rover integration was attempted. Specific blockers encountered: [list]. Architecture and bridge code remains available for future continuation. Final user study conducted on simulation-only path due to integration constraints within sprint window."
   - Don't hide it. Don't apologize. Just state what happened.

4. **The rover_bridge.py and mini PC config are NOT wasted work** — they exist for the next student or the next sprint.

---

## Latency Optimization Checklist (Appendix B)

Apply in order, easiest first:

1. **Reduce JPEG quality to find the floor of acceptable quality:**
   - Try `--periph-quality 10 --fovea-quality 70` instead of 15/85
   - Recompute matched-bandwidth baseline if you change these

2. **Reduce frame resolution:**
   - In mock_pioneer, set webcam to 640x480 instead of 1280x720
   - In OpenCV: `cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)` / `..._HEIGHT, 480`
   - Halves the number of pixels = roughly halves encode time

3. **Reduce target framerate:**
   - 20 fps instead of 30 — gives each frame more processing time
   - In mock_pioneer's camera thread, increase the sleep between frames

4. **Use Ethernet instead of WiFi between mini PC and workstation:**
   - WiFi has highly variable jitter (5-50ms)
   - Ethernet is consistent (<2ms)
   - This only works for static-rover testing — defeats the purpose for mobile use

5. **Buffer pre-allocation in Unity:**
   - If GC spikes are visible (Profiler shows occasional 50ms+ frames), pre-allocate textures and byte buffers in CameraFeedReceiver
   - Less likely to be a bottleneck but worth checking

6. **UDP for video (advanced, high effort):**
   - TCP retransmits dropped packets, adding 100ms+ latency under packet loss
   - UDP drops lost packets immediately → lower worst-case latency, higher quality variance
   - Requires significant rewrite of the streaming code
   - **Don't do this unless other options exhausted**

---

## Appendix A: Foveation Diagnostic Agent Prompt

```
Foveation visual is not rendering in ViveFoveated.unity. Diagnose 
and fix in this order:

1. Show the current Material assigned to the world-space RawImage. 
   If it's not FoveatedFeed, assign FoveatedFeed material. Confirm 
   by playing the scene and looking at the canvas.

2. Verify CameraFeedReceiver in ViveFoveated has dualPayloadMode 
   enabled and is wired to the correct world-space RawImage.

3. Verify GazeProvider's update path writes _GazeU and _GazeV 
   uniforms to the displayed RawImage's material every frame. The 
   gaze upload to port 1236 may work but the local shader may not 
   be receiving UV updates.

4. After each fix, smoke test inside the headset. Foveal sharp 
   region must follow user's eye gaze (NOT head, NOT mouse). 
   Periphery must be visibly pixelated.

5. If fixes touched scene structure, update the editor scene 
   builder script so fixes aren't lost on rebuild.

Do NOT declare "complete" until YOU have verified the visual effect 
by playing the scene with the headset connected and observing 
foveation follow your eyes.
```

---

## Appendix C: Startup Procedure (Build during Day 6)

Recommended power-on order:

1. Boot workstation, plug in Serafim R1+, plug in Vive Focus Vision
2. Boot mini PC (mounted on rover)
3. Wait for both machines to acquire static IPs
4. Power on rover battery → ESP32 boots and connects to mini PC over WiFi
5. Start mock_pioneer on mini PC: `server.py --camera-source rover --mode gaze --rover-out`
6. Verify in console: "ESP32 connected", "Camera opened: 640x480 @ 30 fps"
7. Open Unity on workstation, load ViveFoveated, press Play
8. Don the headset, calibrate eye tracking if needed
9. Smoke test: small steering wheel turn, small gas press → confirm rover responds
10. Begin session

Shutdown:
1. Press Esc / End trial in Unity → motors stop via END command
2. Close Unity
3. Ctrl+C on mock_pioneer (sends END to ESP32)
4. Power off rover battery
5. Disconnect Vive

---

## Daily Acceptance Tracking

Update this table each evening:

| Day | Acceptance Met | Carry-over to next day | Notes |
|---|---|---|---|
| 1 | ☐ | | |
| 2 | ☐ | | |
| 3 | ☐ | | |
| 4 | ☐ | | |
| 5 | ☐ | | |
| 6 | ☐ | | |
| 7 | ☐ | | |
