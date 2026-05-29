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

        WriteCsvHeader();
    }

    private void Update()
    {
        bool startPressed = false;
        bool successPressed = false;
        bool failPressed = false;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.f5Key.wasPressedThisFrame) startPressed = true;
            if (keyboard.f6Key.wasPressedThisFrame) successPressed = true;
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
        trialStartTime = Time.time;
        trialStartTimeStr = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        collisionCount = 0;

        Debug.Log($"[TrialMetricsLogger] Start Trial {trialId} | Scenario: {scenario} | Condition: {condition}");
    }

    public void EndTrial(bool success)
    {
        if (!isTrialActive)
        {
            Debug.LogWarning("[TrialMetricsLogger] EndTrial called but no trial is active.");
            return;
        }

        float endTime = Time.time;
        trialEndTimeStr = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        lastCompletionTime = endTime - trialStartTime;
        isTrialActive = false;

        AppendTrialToCsv(success);

        Debug.Log($"[TrialMetricsLogger] End Trial {trialId} | Success: {success} | Time: {lastCompletionTime:F2}s | Collisions: {collisionCount}");

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
                string header = "trial_id,scenario,condition,trial_start_time,trial_end_time,completion_time_seconds,collision_count,success\n";
                File.WriteAllText(csvFilePath, header, Encoding.UTF8);
                Debug.Log($"[TrialMetricsLogger] Initialized CSV log file at {csvFilePath}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TrialMetricsLogger] Failed to write CSV header: {ex.Message}");
        }
    }

    private void AppendTrialToCsv(bool success)
    {
        try
        {
            string row = $"{trialId},{activeScenario},{activeCondition},{trialStartTimeStr},{trialEndTimeStr},{lastCompletionTime:F3},{collisionCount},{success}\n";
            File.AppendAllText(csvFilePath, row, Encoding.UTF8);
            Debug.Log($"[TrialMetricsLogger] Wrote metrics row to CSV for trial {trialId}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TrialMetricsLogger] Failed to write trial metrics row: {ex.Message}");
        }
    }
}
