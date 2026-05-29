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

    private void Update()
    {
        UpdateHUD();
    }

    public void UpdateHUD()
    {
        if (TrialMetricsLogger.Instance != null)
        {
            var logger = TrialMetricsLogger.Instance;

            if (scenarioText != null)
            {
                scenarioText.text = $"Scenario: {logger.CurrentScenario}";
            }

            if (conditionText != null)
            {
                conditionText.text = $"Condition: {logger.CurrentCondition}";
            }

            if (trialText != null)
            {
                trialText.text = $"Trial: {logger.TrialId}";
            }

            if (timeText != null)
            {
                timeText.text = $"Time: {logger.ElapsedTime:F1}s";
            }

            if (collisionText != null)
            {
                collisionText.text = $"Collisions: {logger.CollisionCount}";
            }
        }
    }
}
