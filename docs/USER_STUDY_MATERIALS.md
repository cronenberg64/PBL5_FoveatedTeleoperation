# User Study Materials

> **Note**: These materials are for the **Simulation Fallback** plan. If the Real Rover Sprint (1-Week) succeeds, these will be adapted for real-rover trials instead of simulation.

**PBL5 Foveated Teleoperation — Complete Study Package**

Principal Investigator: Jonathan Setiawan, ISSE Undergraduate, Ritsumeikan University
Supervisors: Prof. Nicko, Prof. Chandler, Vine Lab

---

## 1. Consent Form

> **Research Study Consent Form**
>
> **Title:** Matched-Bandwidth Evaluation of Gaze-Contingent Foveated Teleoperation
>
> **Principal Investigator:** Jonathan Setiawan, ISSE Undergraduate, Ritsumeikan University
> **Supervisors:** Prof. Nicko, Prof. Chandler, Vine Lab
>
> **Purpose:** This study investigates how different video encoding methods affect performance during virtual reality–based teleoperation of a simulated robot. The results will help us understand bandwidth-efficient video transmission for remote robot operation.
>
> **What you will do:**
> - Wear a VR headset (HTC Vive Focus Vision) for approximately 30–40 minutes
> - Use VR controllers to drive a simulated robot through three navigation scenarios
> - Each scenario is performed under three different visual conditions (order randomized)
> - After each condition, rate your experience on a short survey
> - Total session time: approximately 45–60 minutes
>
> **Risks:**
> - Some users experience mild dizziness, eye strain, or motion sickness when using VR headsets. If you feel uncomfortable at any time, you may stop the session immediately with no penalty.
> - Eye tracking requires that you remain within the headset's tracking range. You may briefly experience visual artifacts if the tracking loses calibration.
>
> **Benefits:** No direct benefit to you. Your participation contributes to research in foveated video compression for robotics.
>
> **Confidentiality:** Your name will not appear in any data files. You will be assigned a Subject ID (e.g., S07). The ID-to-name mapping will be stored separately and accessible only to the principal investigator. Data may be published in academic venues but will only reference Subject IDs.
>
> **Voluntary Participation:** Your participation is entirely voluntary. You may withdraw at any time, for any reason, without consequence. If you withdraw, your partial data will be deleted unless you explicitly agree it can be kept.
>
> **Contact:**
> - Principal Investigator: jonathanrustam2@gmail.com
> - Supervisor: [Prof. Nicko email] / [Prof. Chandler email]
>
> ---
>
> **Consent Declaration:**
>
> I have read and understood the above information. I voluntarily agree to participate in this research study.
>
> | | |
> |---|---|
> | Subject Name (print): | ________________________________ |
> | Subject Signature: | ________________________________ |
> | Date: | ________________________________ |
> | Researcher Signature: | ________________________________ |
> | Date: | ________________________________ |

---

## 2. Pre-Screening Questionnaire

Subject ID: S____

| # | Question | Response |
|---|----------|----------|
| 1 | Age | _______ |
| 2 | Have you used a VR headset before? | ☐ Never  ☐ Once or twice  ☐ Occasionally  ☐ Regularly |
| 3 | Do you wear corrective lenses? | ☐ Glasses  ☐ Contacts  ☐ No correction |
| 3a | If glasses, willing to remove during session? | ☐ Yes  ☐ No |
| 4 | History of motion sickness in VR or video games? | ☐ Yes  ☐ No  ☐ Not sure |
| 5 | Currently feeling well? (no illness, migraine, well-rested) | ☐ Yes  ☐ No |
| 6 | Familiar with using video game controllers? | ☐ Yes  ☐ Somewhat  ☐ Not really |

**Exclusion criteria (do not proceed if):**
- Severe motion sickness history → exclude
- Currently unwell → reschedule
- Unable to comfortably wear headset (glasses conflict) → exclude

**Screened by:** _________________ **Date:** _________

---

## 3. Standardized Briefing Script

> *Read this verbatim. Same words to every subject. Do not paraphrase.*

"Thank you for participating in this study. I'd like to give you a brief overview of what you'll be doing today.

You will wear a VR headset that displays a simulated robot's camera view. Your task is to drive the robot through three different navigation scenarios using the VR controllers. The three scenarios are: traversing a corridor, approaching and entering a doorway, and avoiding scattered obstacles.

Each scenario will be performed three times, under three different visual conditions. The visual conditions differ in how the video feed is processed for transmission. I will not tell you the specifics of the conditions until after the session — please just focus on completing each scenario as quickly and safely as possible.

Before each trial, I will set up the system. When you are ready, I will press a button to begin the trial. The trial ends when you reach the end zone, or I will end it manually if needed.

The right thumbstick controls forward, reverse, and turning. The left thumbstick is not used.

If at any time you feel dizzy, uncomfortable, or want to take a break, please tell me. You can stop the session entirely at any time.

After each set of three trials under one condition, I will pause and ask you two short questions about how it felt. After all 9 trials are done, you'll fill out a final survey.

The total session takes about 45 to 60 minutes including setup. Do you have any questions before we begin?"

---

## 4. Post-Condition Survey

*Administer after each condition (3 times total per subject).*

Subject ID: S____ | Condition #: ____ (1st / 2nd / 3rd experienced)

**Q1. Mental demand:** "How mentally demanding was this condition?"

| Very Low | | | | | | Very High |
|---|---|---|---|---|---|---|
| 1 | 2 | 3 | 4 | 5 | 6 | 7 |

Circle one: **1  2  3  4  5  6  7**

**Q2. Visual quality:** "How would you rate the visual quality of the video feed?"

| Very Poor | | | | | | Excellent |
|---|---|---|---|---|---|---|
| 1 | 2 | 3 | 4 | 5 | 6 | 7 |

Circle one: **1  2  3  4  5  6  7**

---

## 5. Post-Session Survey

Subject ID: S____

**Q1. Condition preference ranking:**

"Please rank the three conditions you experienced from most preferred (1) to least preferred (3) for driving the robot."

| Condition experienced | Rank (1 = most preferred) |
|---|---|
| 1st condition you tried | ____ |
| 2nd condition you tried | ____ |
| 3rd condition you tried | ____ |

**Q2. Open feedback:**

"Any other comments about the system, the task, or your experience? (Optional)"

________________________________________________________________________

________________________________________________________________________

________________________________________________________________________

**Q3. Issues:**

"Did anything feel off or wrong during the session?"

________________________________________________________________________

________________________________________________________________________

---

## 6. Latin Square Randomization Sheet

For 3 conditions: U = Uniform, F = Foveated, P = PeripheralOnly

| Ordering ID | Condition Order |
|---|---|
| 1 | U → F → P |
| 2 | U → P → F |
| 3 | F → U → P |
| 4 | F → P → U |
| 5 | P → U → F |
| 6 | P → F → U |

### Assignment for N=12

| Subject | Ordering ID | Order | Keyboard presses |
|---|---|---|---|
| S01 | 1 | U → F → P | 1, then 2, then 3 |
| S02 | 2 | U → P → F | 1, then 3, then 2 |
| S03 | 3 | F → U → P | 2, then 1, then 3 |
| S04 | 4 | F → P → U | 2, then 3, then 1 |
| S05 | 5 | P → U → F | 3, then 1, then 2 |
| S06 | 6 | P → F → U | 3, then 2, then 1 |
| S07 | 1 | U → F → P | 1, then 2, then 3 |
| S08 | 2 | U → P → F | 1, then 3, then 2 |
| S09 | 3 | F → U → P | 2, then 1, then 3 |
| S10 | 4 | F → P → U | 2, then 3, then 1 |
| S11 | 5 | P → U → F | 3, then 1, then 2 |
| S12 | 6 | P → F → U | 3, then 2, then 1 |

**Per each condition block (3 trials):** Scenario order is fixed: Corridor → Doorway → Obstacle.

**Example for S03 (Ordering 3: F → U → P):**

| Trial | Scenario | Condition | Keyboard |
|---|---|---|---|
| 1 | Corridor | Foveated | Press 2 |
| 2 | Doorway | Foveated | (same) |
| 3 | Obstacle | Foveated | (same) |
| — | *Post-condition survey #1* | | |
| 4 | Corridor | Uniform | Press 1 |
| 5 | Doorway | Uniform | (same) |
| 6 | Obstacle | Uniform | (same) |
| — | *Post-condition survey #2* | | |
| 7 | Corridor | PeripheralOnly | Press 3 |
| 8 | Doorway | PeripheralOnly | (same) |
| 9 | Obstacle | PeripheralOnly | (same) |
| — | *Post-condition survey #3* | | |

---

## 7. Subject Tracking Spreadsheet

| Subject ID | Date | Time | Pre-screen ✓ | Consent ✓ | Ordering ID | Completed ✓ | CSV File | Notes |
|---|---|---|---|---|---|---|---|---|
| S01 | | | ☐ | ☐ | 1 | ☐ | | |
| S02 | | | ☐ | ☐ | 2 | ☐ | | |
| S03 | | | ☐ | ☐ | 3 | ☐ | | |
| S04 | | | ☐ | ☐ | 4 | ☐ | | |
| S05 | | | ☐ | ☐ | 5 | ☐ | | |
| S06 | | | ☐ | ☐ | 6 | ☐ | | |
| S07 | | | ☐ | ☐ | 1 | ☐ | | |
| S08 | | | ☐ | ☐ | 2 | ☐ | | |
| S09 | | | ☐ | ☐ | 3 | ☐ | | |
| S10 | | | ☐ | ☐ | 4 | ☐ | | |
| S11 | | | ☐ | ☐ | 5 | ☐ | | |
| S12 | | | ☐ | ☐ | 6 | ☐ | | |

---

## 8. Per-Session Protocol (60-Minute Block)

| Time | Activity | Researcher Action |
|---|---|---|
| 0:00–0:05 | Pre-screening + Consent | Hand forms, collect signatures |
| 0:05–0:10 | Briefing | Read script VERBATIM |
| 0:10–0:15 | Headset fit + Eye calibration | Assist with headset, run Vive Hub calibration |
| 0:15–0:20 | Practice trial | 1 trial, Uniform, any scenario |
| 0:20–0:30 | Condition 1 (3 trials) | Press condition key, then F5/F6 per trial |
| 0:30–0:31 | Post-condition survey #1 | Hand paper form |
| 0:31–0:41 | Condition 2 (3 trials) | Press condition key, then F5/F6 per trial |
| 0:41–0:42 | Post-condition survey #2 | Hand paper form |
| 0:42–0:52 | Condition 3 (3 trials) | Press condition key, then F5/F6 per trial |
| 0:52–0:53 | Post-condition survey #3 | Hand paper form |
| 0:53–0:57 | Post-session survey | Hand paper form |
| 0:57–1:00 | Debrief + Thank you | Reveal conditions, thank subject |

---

## 9. Researcher Reminders (READ BEFORE EVERY SESSION)

> [!CAUTION]
> - Read the briefing script **verbatim** every time — no improvising
> - Do NOT reveal condition names (Uniform / Foveated / PeripheralOnly) until AFTER the session
> - Do NOT react to performance ("good job", "oh that's tough") — this introduces bias
> - Save CSV after EACH session → backup to cloud / USB immediately
> - If a technical issue occurs mid-session, document it and exclude that subject from analysis — recruit a replacement
> - If subject feels dizzy → stop immediately, no questions asked, exclude from analysis
