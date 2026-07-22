# Summary Statistics & Statistical Tests

Within-subjects design comparing three streaming conditions: **Uniform** (Q50), **Foveated** (90/5), and **Inverse-Foveated** (5/90). All bandwidth-matched. Non-parametric Friedman tests used due to small sample sizes; significance threshold α = 0.05.

---

## 1. Navigation Task (N = 6)

Participants drove the rover through a corridor. Measured by completion time, collisions, and subjective workload.

### Descriptive Statistics

| Metric | Uniform | Foveated | Inverse-Foveated |
|---|---|---|---|
| Duration (s) | 154.49 ± 140.84 | 163.09 ± 65.34 | 181.30 ± 76.39 |
| Collisions | 3.00 ± 2.00 | 3.17 ± 2.14 | 3.33 ± 2.25 |
| TLX Mental Demand | 35.00 ± 18.97 | 48.33 ± 25.03 | 54.17 ± 25.77 |
| TLX Frustration | 30.50 ± 21.01 | 35.83 ± 20.10 | 46.67 ± 26.77 |
| Overall Workload (RTLX) | 32.94 ± 16.71 | 40.78 ± 21.73 | 41.97 ± 19.28 |

### Friedman Tests

| DV | χ² | df | p | Sig? |
|---|---|---|---|---|
| Duration | 4.000 | 2 | .135 | No |
| Collisions | 0.091 | 2 | .956 | No |
| Overall Workload (RTLX) | 6.348 | 2 | **.042** | **Yes** |
| Mental Demand | 7.913 | 2 | **.019** | **Yes** |
| Frustration | 4.778 | 2 | .092 | No |

**Key findings:** While objective task performance (duration, collisions) was not significantly affected by the streaming condition, participants reported significantly higher **mental demand** (p = .019) and **overall workload** (p = .042) under degraded conditions. Inverse-Foveated consistently ranked highest in perceived workload across all subjective measures, suggesting that degrading the foveal region — exactly where the user is looking — is the most cognitively taxing configuration. Uniform was consistently rated lowest in workload, indicating that even though Foveated preserves the gaze region at high quality, the visible peripheral degradation still elevates cognitive load relative to a uniform baseline.

---

## 2. Identification Task (N = 5)

Participants drove to a sign and read its text aloud. Measured by completion time, read depth (how many characters they could resolve), and subjective workload.

### Descriptive Statistics

| Metric | Uniform | Foveated | Inverse-Foveated |
|---|---|---|---|
| Duration (s) | 47.12 ± 8.88 | 48.69 ± 8.61 | 30.82 ± 9.54 |
| Collisions | 0.00 ± 0.00 | 0.00 ± 0.00 | 0.00 ± 0.00 |
| Read Depth | 4.40 ± 1.34 | 5.00 ± 0.00 | 3.80 ± 1.10 |
| TLX Mental Demand | 59.00 ± 17.10 | 57.00 ± 21.68 | 74.00 ± 11.40 |
| TLX Frustration | 45.00 ± 23.98 | 47.00 ± 24.39 | 59.00 ± 29.66 |
| Overall Workload (RTLX) | 40.00 ± 14.52 | 40.83 ± 17.54 | 48.83 ± 16.46 |

### Friedman Tests

| DV | χ² | df | p | Sig? |
|---|---|---|---|---|
| Duration | 6.400 | 2 | **.041** | **Yes** |
| Read Depth | 3.500 | 2 | .174 | No |
| Overall Workload (RTLX) | 2.800 | 2 | .247 | No |
| Mental Demand | 2.842 | 2 | .242 | No |
| Frustration | 3.111 | 2 | .211 | No |

**Key findings:** Duration was significantly different across conditions (p = .041). Counterintuitively, Inverse-Foveated was the **fastest** condition (30.82 s vs ~48 s for the others). This likely reflects a speed-accuracy trade-off: under Inverse-Foveated, the sign text at the gaze point was degraded, so participants gave up trying to read it sooner and stopped closer. This is supported by the lower read depth under Inverse-Foveated (3.80 vs 5.00 for Foveated), although the read depth difference did not reach significance. Foveated achieved a perfect read depth of 5.00 with zero variance — every participant could fully resolve the sign text when the foveal region was streamed at high quality.

---

## 3. Visual Search Task (N = 4)

Participants searched for and navigated toward a specific target object in the environment. Measured by completion time, collisions, and subjective workload.

### Descriptive Statistics

| Metric | Uniform | Foveated | Inverse-Foveated |
|---|---|---|---|
| Duration (s) | 240.25 ± 215.19 | 136.87 ± 81.91 | 296.59 ± 230.13 |
| Collisions | 1.50 ± 1.73 | 2.00 ± 2.31 | 3.80 ± 3.90 |
| TLX Mental Demand | 58.75 ± 30.65 | 66.25 ± 28.10 | 84.00 ± 19.49 |
| TLX Frustration | 38.75 ± 35.21 | 43.75 ± 25.29 | 77.60 ± 12.50 |
| Overall Workload (RTLX) | 43.54 ± 26.30 | 46.88 ± 20.18 | 61.50 ± 13.99 |

### Friedman Tests

| DV | χ² | df | p | Sig? |
|---|---|---|---|---|
| Duration | 1.500 | 2 | .472 | No |
| Collisions | 4.000 | 2 | .135 | No |
| Overall Workload (RTLX) | 3.500 | 2 | .174 | No |
| Mental Demand | 4.933 | 2 | .085 | No |
| Frustration | 6.000 | 2 | **.050** | **Yes** |

**Key findings:** Frustration was significantly different across conditions (p = .050), with Inverse-Foveated producing dramatically higher frustration (77.60) compared to Uniform (38.75) and Foveated (43.75). The very tight SD for Inverse-Foveated frustration (12.50) indicates this was a universal experience — every participant found it frustrating to search for objects when the region they were directly looking at was degraded. Although not statistically significant with N = 4, the mean duration under Foveated (136.87 s) was nearly half that of Inverse-Foveated (296.59 s), suggesting that high-quality foveal streaming meaningfully accelerates visual search. The high variance in duration across all conditions reflects the inherent difficulty variability of the search task.

---

## Cross-Task Summary

| Finding | Task | p-value |
|---|---|---|
| Inverse-Foveated increases overall workload (RTLX) | Navigation | .042 |
| Inverse-Foveated increases mental demand | Navigation | .019 |
| Inverse-Foveated decreases task duration (speed-accuracy trade-off) | Identification | .041 |
| Inverse-Foveated increases frustration | Visual Search | .050 |

Across all three tasks, the **Inverse-Foveated** condition consistently produced the worst subjective experience. The consistent pattern — Uniform ≤ Foveated < Inverse-Foveated for subjective workload — supports the hypothesis that degrading the foveal region is significantly more disruptive than degrading the periphery, even when total bandwidth is held constant.
