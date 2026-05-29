using UnityEngine;

public class TrialEndTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Ensure the colliding object is tagged "Player"
        if (other.CompareTag("Player"))
        {
            if (TrialMetricsLogger.Instance != null)
            {
                // End trial as a success
                TrialMetricsLogger.Instance.EndTrial(true);
            }
            else
            {
                Debug.LogWarning("[TrialEndTrigger] No TrialMetricsLogger instance found to end the trial.");
            }
        }
    }
}
