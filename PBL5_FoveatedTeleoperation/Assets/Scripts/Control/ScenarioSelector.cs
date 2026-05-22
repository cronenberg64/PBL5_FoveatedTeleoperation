using UnityEngine;

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
        // Hotkeys for runtime switching
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ActivateScenario(DrivingScenario.Corridor);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ActivateScenario(DrivingScenario.Doorway);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
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
