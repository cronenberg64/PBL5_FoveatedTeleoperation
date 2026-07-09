# Encoding Condition Quick Reference (for researcher during sessions)

| Key | Condition | What it looks like |
|---|---|---|
| 1 | Uniform | Same quality everywhere, moderate sharpness |
| 2 | Foveated | Sharp where subject looks, blurry elsewhere |
| 3 | Inverse-Foveated | Blurry where subject looks, sharp elsewhere |

Scenario/task switch keys: F2 (Corridor) / F3 (Doorway) / F4 (Obstacle)

Trial control: F5 start / F6 end-success / F7 end-fail

Reminder: do not tell the subject which condition is active during 
or before their session.

## Calibrated Bandwidth-Matched Presets
*These values were derived from the `bench_bandwidth` sweep to ensure equal network load across conditions (-0.9% delta).*

| Condition | Periph Quality (PQ) | Fovea Quality (FQ) |
|---|---|---|
| **Uniform** | 50 | 50 |
| **Foveated** | 5 | 90 |
| **Inverse-Foveated** | 90 | 5 |
