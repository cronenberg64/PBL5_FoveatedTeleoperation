# Navigation Experiment Scenarios

These scenarios are designed to evaluate the "Drivability" of the robot under different streaming conditions (Uniform vs. Foveated).

## General Metrics for All Scenarios

*   **Completion Time (T)**: Seconds from start line to finish line.
*   **Collision Count (C)**: Number of times the robot hits a wall or obstacle.
*   **Alignment Error (E)**: For specific tasks, the degree offset from a perfect center-line.

---

## Scenario 1: Corridor Traversal
*   **Goal**: Navigate through a narrow 10m corridor with two 90-degree turns.
*   **Difficulty**: Requires high peripheral awareness to avoid clipping corners.
*   **Success Criteria**: Reach the end without exceeding 3 collisions.
*   **Recording**: Log (T) and (C).

## Scenario 2: Doorway Approach
*   **Goal**: Approach a narrow doorway (only 20cm wider than the robot) and pass through it.
*   **Difficulty**: Requires high-precision alignment. Evaluates whether foveation causes depth perception or alignment issues.
*   **Success Criteria**: Pass through the door without a "stuck" collision.
*   **Recording**: Log (C) and Alignment Error at the threshold.

## Scenario 3: Dynamic Obstacle Avoidance
*   **Goal**: Navigate across an open lab space with 5 scattered "cones" or objects.
*   **Difficulty**: Rapid gaze shifting is required to identify and avoid multiple dispersed targets.
*   **Success Criteria**: Reach the target zone without hitting more than 1 cone.
*   **Recording**: Success/Failure rate and (T).

---

## Experiment Conditions (Matched Bandwidth)

For the checkpoint, compare these three conditions at approximately **matched average bytes/frame**:

1.  **Uniform-Low**: `--mode uniform --quality 5` (Baseline)
2.  **Foveal-High**: `--mode gaze --periph-quality 5 --fovea-quality 85` (Experimental)
3.  **Peripheral-High**: `--mode uniform --quality 20` (Control - higher bandwidth than Condition 1 & 2 to show the "upper bound").

> **Note**: One data point per condition is sufficient for the checkpoint to demonstrate methodology. Full statistical N comes later.
