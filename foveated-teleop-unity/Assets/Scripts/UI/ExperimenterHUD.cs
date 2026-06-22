using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World-space HUD overlay that follows the VR camera.
/// Displays task number, quality mode, elapsed time, and mistake count.
///
/// Attach to a child GameObject of the VR camera.
/// The SceneSetup editor script positions it at z=2 m, top-left of view.
/// </summary>
public class ExperimenterHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI taskLabel;
    [SerializeField] private TextMeshProUGUI qualityLabel;
    [SerializeField] private TextMeshProUGUI timerLabel;
    [SerializeField] private TextMeshProUGUI mistakeLabel;

    // ─── State ───────────────────────────────────────────────────
    private int currentTask = 0;
    private string qualityMode = "uniform-Q20";
    private int mistakes = 0;
    private bool taskRunning = false;
    private float taskStartTime = 0f;

    // ─── Lifecycle ───────────────────────────────────────────────

    private void Update()
    {
        if (taskLabel == null) return;

        taskLabel.text    = currentTask > 0 ? $"Task {currentTask}" : "No task";
        qualityLabel.text = $"Mode: {qualityMode}";
        mistakeLabel.text = $"Mistakes: {mistakes}";

        if (taskRunning)
        {
            float elapsed = Time.time - taskStartTime;
            int minutes = (int)(elapsed / 60f);
            int seconds = (int)(elapsed % 60f);
            timerLabel.text = $"{minutes:D2}:{seconds:D2}";
        }
        else
        {
            timerLabel.text = "--:--";
        }
    }

    // ─── Public API ──────────────────────────────────────────────

    /// <summary>Begin timing a task.</summary>
    public void StartTask(int taskNumber)
    {
        currentTask = taskNumber;
        taskRunning = true;
        taskStartTime = Time.time;
        mistakes = 0;
        Debug.Log($"[ExperimenterHUD] Task {taskNumber} started.");
    }

    /// <summary>Stop timing; leaves mistake count and task number visible.</summary>
    public void EndTask()
    {
        taskRunning = false;
        float elapsed = Time.time - taskStartTime;
        Debug.Log($"[ExperimenterHUD] Task {currentTask} ended — elapsed={elapsed:F1}s, mistakes={mistakes}");
    }

    /// <summary>Add one to the mistake counter.</summary>
    public void IncrementMistake()
    {
        mistakes++;
        Debug.Log($"[ExperimenterHUD] Mistake #{mistakes} recorded for task {currentTask}.");
    }

    /// <summary>Reset mistake counter without ending the task.</summary>
    public void ResetMistakes()
    {
        mistakes = 0;
    }

    /// <summary>Update the quality mode label.</summary>
    public void SetQualityMode(string mode)
    {
        qualityMode = mode;
    }

    /// <summary>Set the task number without starting/stopping the timer.</summary>
    public void SetTask(int taskNumber)
    {
        currentTask = taskNumber;
    }

    // ─── Read-only accessors for ExperimenterInput ────────────────

    public int CurrentTask => currentTask;
    public bool IsTaskRunning => taskRunning;
    public int MistakeCount => mistakes;
    public string QualityMode => qualityMode;
    public float ElapsedSeconds => taskRunning ? Time.time - taskStartTime : 0f;
}
