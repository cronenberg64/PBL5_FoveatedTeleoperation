using UnityEngine;

public class TrialStartTrigger : MonoBehaviour
{
    [Tooltip("The experimental condition name logged for this trial")]
    [SerializeField] private string conditionName = "Manual WASD";

    private void OnTriggerEnter(Collider other)
    {
        // Ensure the colliding object is tagged "Player"
        if (other.CompareTag("Player"))
        {
            if (TrialMetricsLogger.Instance != null)
            {
                // Query current scenario name from selector
                string scenario = "Unknown";
                if (TrialMetricsLogger.Instance.scenarioSelector != null)
                {
                    scenario = TrialMetricsLogger.Instance.scenarioSelector.ActiveScenario.ToString();
                }
                
                TrialMetricsLogger.Instance.StartTrial(scenario, conditionName);
            }
            else
            {
                Debug.LogWarning("[TrialStartTrigger] No TrialMetricsLogger instance found to start the trial.");
            }
        }
    }
}
