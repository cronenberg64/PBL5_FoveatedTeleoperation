using UnityEngine;
using TMPro;

public class DrivingHUD : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI scenarioText;
    [SerializeField] private TextMeshProUGUI conditionText;
    [SerializeField] private TextMeshProUGUI trialText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI collisionText;

    [Header("Data Sources")]
    [SerializeField] private RobotController robotController;
    [SerializeField] private ScenarioSelector scenarioSelector;

    [Header("Settings")]
    [SerializeField] private string conditionName = "Manual WASD";

    private void Update()
    {
        UpdateHUD();
    }

    public void UpdateHUD()
    {
        if (scenarioText != null && scenarioSelector != null)
        {
            scenarioText.text = $"Scenario: {scenarioSelector.ActiveScenario}";
        }

        if (conditionText != null)
        {
            conditionText.text = $"Condition: {conditionName}";
        }

        if (robotController != null)
        {
            if (trialText != null)
            {
                trialText.text = $"Trial: #{robotController.trialNumber}";
            }

            if (timeText != null)
            {
                if (robotController.isTrialActive)
                {
                    timeText.text = $"Time: {robotController.elapsedTime:F2}s";
                }
                else
                {
                    timeText.text = $"Time: {robotController.elapsedTime:F2}s (Ready)";
                }
            }

            if (collisionText != null)
            {
                collisionText.text = $"Collisions: {robotController.collisionCount}";
            }
        }
    }
}
