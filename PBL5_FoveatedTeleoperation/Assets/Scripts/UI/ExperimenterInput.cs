using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Keyboard shortcuts for the experimenter's HUD.
/// Reads Unity Input System Keyboard so it works in Editor + builds.
///
/// Bindings:
///   M      — mark a mistake
///   T      — toggle task start / end
///   1/2/3  — set task number (switches task, does NOT restart timer)
///   Q      — cycle quality mode label (local only; relaunch server for real mode change)
///   R      — reset mistake counter for current task
/// </summary>
public class ExperimenterInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExperimenterHUD hud;

    private static readonly string[] QualityModes =
    {
        "uniform-Q50",
        "uniform-Q20",
        "uniform-Q10",
        "uniform-Q5",
        "gaze-contingent",
    };

    private int qualityIndex = 1;  // default: uniform-Q20

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.mKey.wasPressedThisFrame)
            HandleMistake();

        if (kb.tKey.wasPressedThisFrame)
            HandleTaskToggle();

        if (kb.digit1Key.wasPressedThisFrame)
            HandleSetTask(1);
        if (kb.digit2Key.wasPressedThisFrame)
            HandleSetTask(2);
        if (kb.digit3Key.wasPressedThisFrame)
            HandleSetTask(3);

        if (kb.qKey.wasPressedThisFrame)
            HandleCycleQuality();

        if (kb.rKey.wasPressedThisFrame)
            HandleResetMistakes();
    }

    // ─── Handlers ────────────────────────────────────────────────

    private void HandleMistake()
    {
        if (hud == null) return;
        hud.IncrementMistake();
        MetricsLogger.Instance?.Log("mistake_marked", hud.MistakeCount, hud.CurrentTask,
            $"task={hud.CurrentTask},elapsed={hud.ElapsedSeconds:F1}");
    }

    private void HandleTaskToggle()
    {
        if (hud == null) return;
        if (hud.IsTaskRunning)
        {
            float elapsed = hud.ElapsedSeconds;
            hud.EndTask();
            MetricsLogger.Instance?.Log("task_end", hud.CurrentTask, elapsed,
                $"task={hud.CurrentTask},mistakes={hud.MistakeCount}");
        }
        else
        {
            hud.StartTask(hud.CurrentTask > 0 ? hud.CurrentTask : 1);
            MetricsLogger.Instance?.Log("task_start", hud.CurrentTask, 0f,
                $"task={hud.CurrentTask},mode={hud.QualityMode}");
        }
    }

    private void HandleSetTask(int number)
    {
        if (hud == null) return;
        hud.SetTask(number);
        Debug.Log($"[ExperimenterInput] Task number set to {number}.");
    }

    private void HandleCycleQuality()
    {
        if (hud == null) return;
        qualityIndex = (qualityIndex + 1) % QualityModes.Length;
        string mode = QualityModes[qualityIndex];
        hud.SetQualityMode(mode);
        MetricsLogger.Instance?.Log("quality_mode_intent", qualityIndex, 0f, mode);
        Debug.Log($"[ExperimenterInput] Quality mode intent: {mode} (restart server to apply).");
    }

    private void HandleResetMistakes()
    {
        if (hud == null) return;
        hud.ResetMistakes();
        MetricsLogger.Instance?.Log("mistakes_reset", hud.CurrentTask, 0f, "");
        Debug.Log("[ExperimenterInput] Mistake counter reset.");
    }
}
