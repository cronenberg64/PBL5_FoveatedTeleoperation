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

    private void Start()
    {
        if (UnityEngine.XR.XRSettings.isDeviceActive)
        {
            Transform panel = transform.Find("HUDPanel");
            if (panel != null)
            {
                panel.localScale = new Vector3(2.5f, 2.5f, 1f);
            }
            else
            {
                ScaleText(scenarioText);
                ScaleText(conditionText);
                ScaleText(trialText);
                ScaleText(timeText);
                ScaleText(collisionText);
            }
        }
    }

    private void ScaleText(TextMeshProUGUI text)
    {
        if (text != null)
        {
            text.fontSize *= 2.5f;
            RectTransform rt = text.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.y * 2.5f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y * 2.5f);
        }
    }

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
