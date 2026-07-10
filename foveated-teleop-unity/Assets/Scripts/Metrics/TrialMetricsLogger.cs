using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using System.Text;

public class TrialMetricsLogger : MonoBehaviour
{
    public static TrialMetricsLogger Instance { get; private set; }

    [Header("References")]
    public ScenarioSelector scenarioSelector;
    public RobotController robotController;

    [Header("Current Settings")]
    [SerializeField] private string currentCondition = "Manual WASD";

    // Trial state fields
    private int trialId = 1;
    private string activeScenario = "";
    private string activeCondition = "";
    private string trialStartTimeStr = "";
    private string trialEndTimeStr = "";
    private float trialStartTime = 0f;
    private int collisionCount = 0;
    private bool isTrialActive = false;
    private float lastCompletionTime = 0f;

    private string csvFilePath;
    private string sessionTimestamp;
    private GazeTelemetryLogger gazeTelemetry;
    private TextMesh statusText;

    // Public properties for external components and HUD
    public int TrialId => trialId;
    public string CurrentScenario => isTrialActive ? activeScenario : (scenarioSelector != null ? scenarioSelector.ActiveScenario.ToString() : "None");
    public string CurrentCondition
    {
        get
        {
            if (isTrialActive)
            {
                return activeCondition;
            }
            else if (ConditionController.Instance != null)
            {
                return ConditionController.Instance.ActiveCondition.ToString();
            }
            return currentCondition;
        }
    }
    public float ElapsedTime => isTrialActive ? (Time.time - trialStartTime) : lastCompletionTime;
    public int CollisionCount => collisionCount;
    public bool IsTrialActive => isTrialActive;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize session timestamp and CSV file path
        sessionTimestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string logsDir = Path.Combine(Application.dataPath, "../logs");
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }
        csvFilePath = Path.Combine(logsDir, $"trial_metrics_{sessionTimestamp}.csv");

        gazeTelemetry = GetComponent<GazeTelemetryLogger>();
        if (gazeTelemetry == null)
        {
            gazeTelemetry = gameObject.AddComponent<GazeTelemetryLogger>();
        }

        WriteCsvHeader();
    }

    private void SetupVRHUD()
    {
        if (Camera.main == null) return;

        GameObject hudObj = new GameObject("VRStatusHUD");
        hudObj.transform.SetParent(Camera.main.transform);
        // Position it top left relative to the camera
        hudObj.transform.localPosition = new Vector3(-0.6f, 0.4f, 1.0f);
        hudObj.transform.localRotation = Quaternion.identity;

        statusText = hudObj.AddComponent<TextMesh>();
        statusText.characterSize = 0.004f;
        statusText.fontSize = 64;
        statusText.anchor = TextAnchor.UpperLeft;
        statusText.alignment = TextAlignment.Left;
        statusText.richText = true;
    }

    private void Update()
    {
        bool startPressed = false;
        bool successPressed = false;
        bool failPressed = false;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.enterKey.wasPressedThisFrame) 
            {
                if (isTrialActive) successPressed = true;
                else startPressed = true;
            }
            if (keyboard.f7Key.wasPressedThisFrame) failPressed = true;
            if (keyboard.f8Key.wasPressedThisFrame) RegisterCollision();
        }

#if META_XR_SDK
        if (OVRInput.GetDown(OVRInput.RawButton.A, OVRInput.Controller.RTouch)) startPressed = true;
        if (OVRInput.GetDown(OVRInput.RawButton.B, OVRInput.Controller.RTouch)) successPressed = true;
        if (OVRInput.GetDown(OVRInput.RawButton.Y, OVRInput.Controller.LTouch)) failPressed = true;
#endif

#if WAVE_XR
        var domDevice = WaveVR_Controller.Input(WaveVR_Controller.EDeviceType.Dominant);
        var nonDomDevice = WaveVR_Controller.Input(WaveVR_Controller.EDeviceType.NonDominant);
        if (domDevice != null && domDevice.connected)
        {
            if (domDevice.GetPressDown(wvr.WVR_InputId.WVR_InputId_Alias1_A)) startPressed = true;
            if (domDevice.GetPressDown(wvr.WVR_InputId.WVR_InputId_Alias1_B)) successPressed = true;
        }
        if (nonDomDevice != null && nonDomDevice.connected)
        {
            if (nonDomDevice.GetPressDown(wvr.WVR_InputId.WVR_InputId_Alias1_Y)) failPressed = true;
        }
#endif

        if (startPressed)
        {
            string scenario = scenarioSelector != null ? scenarioSelector.ActiveScenario.ToString() : "Unknown";
            StartTrial(scenario, CurrentCondition);
        }
        if (successPressed)
        {
            EndTrial(true);
        }
        if (failPressed)
        {
            EndTrial(false);
        }

        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (statusText == null && Camera.main != null)
        {
            SetupVRHUD();
        }

        if (statusText != null)
        {
            string state = isTrialActive ? "<color=#00FF00>RECORDING TRIAL</color>" : "<color=#FF0000>NOT RECORDING</color>";
            statusText.text = $"{state}\nScenario: {CurrentScenario}\nCondition: {CurrentCondition}\nTrial: {trialId} | Time: {ElapsedTime:F1}s";
        }
    }


    public void StartTrial(string scenario, string condition)
    {
        // SetCondition before starting the timer, locking it
        if (ConditionController.Instance != null)
        {
            if (System.Enum.TryParse(condition, out ConditionController.Condition parsedCond))
            {
                ConditionController.Instance.SetCondition(parsedCond);
            }
            else
            {
                ConditionController.Instance.SetCondition(ConditionController.Instance.ActiveCondition);
            }
            condition = ConditionController.Instance.ActiveCondition.ToString();
        }

        if (isTrialActive)
        {
            Debug.LogWarning("[TrialMetricsLogger] StartTrial called but a trial is already active. Restarting timer.");
        }

        isTrialActive = true;
        activeScenario = scenario;
        activeCondition = condition;
        trialStartTimeStr = System.DateTime.Now.ToString("HH:mm:ss.fff");
        trialStartTime = Time.time;
        collisionCount = 0;
        lastCompletionTime = 0f;

        if (gazeTelemetry != null)
        {
            string logsDir = Path.Combine(Application.dataPath, "../logs");
            string telemetryPath = Path.Combine(logsDir, $"gaze_telemetry_SubjX_Trial{trialId}_{sessionTimestamp}.csv");
            gazeTelemetry.StartLogging(telemetryPath);
        }

        Debug.Log($"[TrialMetricsLogger] Trial {trialId} STARTED: {scenario} ({condition})");
    }

    public void EndTrial(bool success)
    {
        if (!isTrialActive)
        {
            Debug.LogWarning("[TrialMetricsLogger] EndTrial called but no trial is active.");
            return;
        }

        isTrialActive = false;
        trialEndTimeStr = System.DateTime.Now.ToString("HH:mm:ss.fff");
        lastCompletionTime = Time.time - trialStartTime;

        float blinkRate = 0f;
        if (gazeTelemetry != null)
        {
            gazeTelemetry.StopLogging(out blinkRate);
        }

        WriteCsvRow(success, blinkRate);

        Debug.Log($"[TrialMetricsLogger] Trial {trialId} ENDED. Success: {success}, Time: {lastCompletionTime:F2}s, Collisions: {collisionCount}, BlinkRate: {blinkRate:F2}");
        trialId++;

        // Reset the robot on trial end
        if (robotController != null)
        {
            robotController.ResetRobot();
        }
    }

    public void RegisterCollision()
    {
        if (!isTrialActive)
        {
            Debug.LogWarning("[TrialMetricsLogger] Collision registered outside of an active trial.");
        }
        collisionCount++;
        Debug.Log($"[TrialMetricsLogger] Collision registered. Total collisions in trial {trialId}: {collisionCount}");
    }

    private void WriteCsvHeader()
    {
        try
        {
            if (!File.Exists(csvFilePath))
            {
                File.WriteAllText(csvFilePath, "TrialID,Scenario,Condition,StartTime,EndTime,DurationSeconds,Success,Collisions,BlinkRatePerMin\n", Encoding.UTF8);
                Debug.Log($"[TrialMetricsLogger] Initialized CSV log file at {csvFilePath}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TrialMetricsLogger] Failed to write CSV header: {ex.Message}");
        }
    }

    private void WriteCsvRow(bool success, float blinkRate)
    {
        try
        {
            string row = $"{trialId},{activeScenario},{activeCondition},{trialStartTimeStr},{trialEndTimeStr},{lastCompletionTime:F2},{(success ? 1 : 0)},{collisionCount},{blinkRate:F2}\n";
            File.AppendAllText(csvFilePath, row, Encoding.UTF8);
            Debug.Log($"[TrialMetricsLogger] Wrote metrics row to CSV for trial {trialId}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TrialMetricsLogger] Failed to write trial metrics row: {ex.Message}");
        }
    }
}
