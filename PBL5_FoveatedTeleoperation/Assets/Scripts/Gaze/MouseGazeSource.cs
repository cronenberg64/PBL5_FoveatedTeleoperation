using UnityEngine;

/// <summary>
/// Mouse-cursor gaze proxy for desktop testing without a Tobii or VR headset.
///
/// Converts the screen-space mouse position to UV coordinates (0–1) and writes
/// them into the GazeProvider so the rest of the pipeline (GazeUploader,
/// FoveatedFeedController, GazeDebugDot) works without any hardware.
///
/// Setup:
///   1. Add this component to the same GameObject as GazeProvider.
///   2. Enable "Use Mouse Proxy" in the Inspector (or set via script).
///   3. GazeProvider will read the UV from this source each frame.
///
/// UV convention:
///   u = mouse X / Screen.width   (0 = left,   1 = right)
///   v = mouse Y / Screen.height  (0 = bottom, 1 = top)
///
/// This matches Unity's Input.mousePosition convention and the GazeUploader
/// protocol (u,v ∈ [0,1] → sent as u*1000, v*1000 to port 1236).
/// </summary>
[RequireComponent(typeof(GazeProvider))]
public class MouseGazeSource : MonoBehaviour
{
    [Header("Mouse Proxy Settings")]
    [Tooltip("When true, overrides GazeProvider with mouse-cursor UV coordinates")]
    [SerializeField] private bool useMouseProxy = true;

    [Tooltip("Smooth the mouse UV with a lerp factor (0 = no smoothing, 1 = frozen)")]
    [Range(0f, 0.95f)]
    [SerializeField] private float smoothing = 0f;

    [Header("Status (Read-Only)")]
    [SerializeField] private Vector2 mouseUV;

    private GazeProvider _gazeProvider;
    private Vector2 _smoothedUV = new Vector2(0.5f, 0.5f);

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _gazeProvider = GetComponent<GazeProvider>();
    }

    private void Update()
    {
        if (!useMouseProxy) return;

        // Compute raw mouse UV
        float rawU = Mathf.Clamp01(Input.mousePosition.x / Screen.width);
        float rawV = Mathf.Clamp01(Input.mousePosition.y / Screen.height);
        Vector2 rawUV = new Vector2(rawU, rawV);

        // Optional smoothing
        _smoothedUV = smoothing > 0f
            ? Vector2.Lerp(rawUV, _smoothedUV, smoothing)
            : rawUV;

        mouseUV = _smoothedUV;

        // Push into GazeProvider — uses the public setter
        _gazeProvider.SetGazeUVOverride(_smoothedUV);
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Enable or disable the mouse proxy at runtime.</summary>
    public void SetEnabled(bool enabled)
    {
        useMouseProxy = enabled;
        if (!enabled)
            _gazeProvider.ClearGazeUVOverride();
    }
}
