using UnityEngine;
using UnityEngine.InputSystem;

public class ScenarioSelector : MonoBehaviour
{
    public enum DrivingScenario
    {
        Corridor,
        Doorway,
        Obstacle
    }

    [Header("Scenario GameObjects")]
    public GameObject scenarioCorridor;
    public GameObject scenarioDoorway;
    public GameObject scenarioObstacle;

    [Header("References")]
    public RobotController robotController;

    [Header("Selection Settings")]
    [SerializeField] private DrivingScenario initialScenario = DrivingScenario.Corridor;

    private DrivingScenario activeScenario;

    public DrivingScenario ActiveScenario => activeScenario;

    private void Start()
    {
        // Setup initial scenario
        ActivateScenario(initialScenario, resetRobot: false);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Hotkeys for runtime switching (using C, D, O or 7, 8, 9 to avoid conflict with Condition keys 1, 2, 3)
        if (keyboard.cKey.wasPressedThisFrame || keyboard.digit7Key.wasPressedThisFrame)
        {
            ActivateScenario(DrivingScenario.Corridor);
        }
        else if (keyboard.dKey.wasPressedThisFrame || keyboard.digit8Key.wasPressedThisFrame)
        {
            ActivateScenario(DrivingScenario.Doorway);
        }
        else if (keyboard.oKey.wasPressedThisFrame || keyboard.digit9Key.wasPressedThisFrame)
        {
            ActivateScenario(DrivingScenario.Obstacle);
        }
    }

    public void ActivateScenario(DrivingScenario scenario, bool resetRobot = true)
    {
        activeScenario = scenario;

        // Toggles active states
        if (scenarioCorridor != null)
            scenarioCorridor.SetActive(scenario == DrivingScenario.Corridor);
        if (scenarioDoorway != null)
            scenarioDoorway.SetActive(scenario == DrivingScenario.Doorway);
        if (scenarioObstacle != null)
            scenarioObstacle.SetActive(scenario == DrivingScenario.Obstacle);

        Debug.Log($"[ScenarioSelector] Active Scenario switched to: {scenario}");

        // Optionally reset robot position/state
        if (resetRobot && robotController != null)
        {
            robotController.ResetRobot();
        }
    }
}
