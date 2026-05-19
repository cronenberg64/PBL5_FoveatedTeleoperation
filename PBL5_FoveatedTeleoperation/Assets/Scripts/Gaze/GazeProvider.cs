using UnityEngine;

/// <summary>
/// Provides gaze coordinates (UV 0–1) for the foveated shader.
///
/// On Quest Pro: uses OVREyeGaze (eye-tracking hardware).
/// On Quest 3 / Editor: falls back to head-gaze (center-eye forward ray).
///
/// NOTE: This script uses conditional compilation. If you have the Meta XR SDK
/// installed, define META_XR_SDK in Project Settings → Player → Scripting Define Symbols.
/// Without it, only head-gaze will be available.
/// </summary>
public class GazeProvider : MonoBehaviour
{
    [Header("Gaze Output (Read-Only)")]
    [Tooltip("Current gaze position in UV space (0–1) on the feed plane")]
    [SerializeField] private Vector2 gazeUV = new Vector2(0.5f, 0.5f);

    [Tooltip("True if hardware eye tracking is active")]
    [SerializeField] private bool isEyeTrackingActive = false;

    // Metrics: count updates per second
    private int _updateCount;
    private float _metricsWindowStart;

    [Header("Configuration")]
    [Tooltip("The camera feed plane(s) to raycast against for UV coordinates")]
    [SerializeField] private RectTransform[] feedPlanes;

    [Tooltip("The center-eye camera (usually the main VR camera)")]
    [SerializeField] private Camera vrCamera;

    [Tooltip("Maximum raycast distance")]
    [SerializeField] private float maxRayDistance = 10f;

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>Current gaze UV (0–1 range). If no valid hit, returns (0.5, 0.5).</summary>
    public Vector2 GazeUV => gazeUV;

    /// <summary>Whether hardware eye tracking is being used.</summary>
    public bool IsEyeTrackingActive => isEyeTrackingActive;

    // ── External override (MouseGazeSource, Tobii, etc.) ────────────────
    // When set, skips the raycast entirely and feeds this value directly.
    private bool _hasOverride = false;
    private Vector2 _overrideUV = new Vector2(0.5f, 0.5f);

    /// <summary>
    /// Override the gaze UV with a value from an external source (e.g. mouse proxy,
    /// Tobii). Clears automatically if <see cref="ClearGazeUVOverride"/> is called.
    /// </summary>
    public void SetGazeUVOverride(Vector2 uv)
    {
        _overrideUV = uv;
        _hasOverride = true;
    }

    /// <summary>Remove any active override; resume normal gaze ray computation.</summary>
    public void ClearGazeUVOverride()
    {
        _hasOverride = false;
    }

    // ─── Lifecycle ──────────────────────────────────────────────

    private void Start()
    {
        if (vrCamera == null)
        {
            vrCamera = Camera.main;
        }

        CheckEyeTrackingAvailability();
    }

    private void Update()
    {
        if (_hasOverride)
        {
            gazeUV = _overrideUV;
        }
        else
        {
            Ray gazeRay = GetGazeRay();
            UpdateGazeUV(gazeRay);
        }

        _updateCount++;
        float now = Time.time;
        if (now - _metricsWindowStart >= 1f)
        {
            float hz = _updateCount / (now - _metricsWindowStart);
            MetricsLogger.Instance?.Log("gaze_update", hz, 0f,
                _hasOverride ? "override" : (isEyeTrackingActive ? "eye" : "head"));
            _updateCount = 0;
            _metricsWindowStart = now;
        }
    }

    // ─── Eye Tracking Detection ─────────────────────────────────

    private void CheckEyeTrackingAvailability()
    {
#if META_XR_SDK && false
        try
        {
            // Check if eye tracking is supported and enabled
            if (OVRPlugin.eyeTrackingEnabled)
            {
                isEyeTrackingActive = true;
                Debug.Log("[GazeProvider] Eye tracking is available (Quest Pro detected).");
                return;
            }
        }
        catch (System.Exception)
        {
            // OVRPlugin not available or eye tracking not enabled
        }
#endif
        isEyeTrackingActive = false;
        Debug.Log("[GazeProvider] Eye tracking unavailable. Using head-gaze fallback.");
    }

    // ─── Gaze Ray Construction ──────────────────────────────────

    private Ray GetGazeRay()
    {
#if META_XR_SDK && false
        if (isEyeTrackingActive)
        {
            return GetEyeTrackingRay();
        }
#endif
        return GetHeadGazeRay();
    }

    /// <summary>Head-gaze fallback: ray from center of VR camera view.</summary>
    private Ray GetHeadGazeRay()
    {
        if (vrCamera == null)
            return new Ray(Vector3.zero, Vector3.forward);

        return new Ray(vrCamera.transform.position, vrCamera.transform.forward);
    }

#if META_XR_SDK && false
    /// <summary>Eye-tracking ray using OVRPlugin (Quest Pro).</summary>
    private Ray GetEyeTrackingRay()
    {
        OVRPlugin.EyeGazesState eyeGazesState;
        if (OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, out eyeGazesState))
        {
            // Use the combined gaze (average of both eyes)
            var combinedGaze = eyeGazesState.EyeGazes[2]; // Index 2 = combined
            if (combinedGaze.IsValid)
            {
                Vector3 position = new Vector3(
                    combinedGaze.Pose.Position.x,
                    combinedGaze.Pose.Position.y,
                    -combinedGaze.Pose.Position.z // Convert from OVR to Unity coords
                );

                Quaternion rotation = new Quaternion(
                    combinedGaze.Pose.Orientation.x,
                    combinedGaze.Pose.Orientation.y,
                    -combinedGaze.Pose.Orientation.z,
                    -combinedGaze.Pose.Orientation.w
                );

                // Transform from tracking space to world space
                if (vrCamera != null)
                {
                    position = vrCamera.transform.parent.TransformPoint(position);
                    Vector3 forward = vrCamera.transform.parent.TransformDirection(rotation * Vector3.forward);
                    return new Ray(position, forward);
                }

                return new Ray(position, rotation * Vector3.forward);
            }
        }

        // Fall back to head gaze if eye data is invalid this frame
        return GetHeadGazeRay();
    }
#endif

    // ─── UV Computation ─────────────────────────────────────────

    private void UpdateGazeUV(Ray gazeRay)
    {
        if (feedPlanes == null || feedPlanes.Length == 0 || vrCamera == null)
        {
            gazeUV = new Vector2(0.5f, 0.5f);
            return;
        }

        // Raycast against each feed plane and use the closest hit
        float closestDist = float.MaxValue;
        Vector2 bestUV = new Vector2(0.5f, 0.5f);
        bool hitAny = false;

        foreach (var plane in feedPlanes)
        {
            if (plane == null) continue;

            // Create a virtual plane from the RectTransform
            Vector3 planeNormal = plane.forward;
            Vector3 planePoint = plane.position;
            Plane p = new Plane(planeNormal, planePoint);

            float enter;
            if (p.Raycast(gazeRay, out enter) && enter < maxRayDistance && enter < closestDist)
            {
                Vector3 hitWorld = gazeRay.GetPoint(enter);

                // Convert world hit to local space of the RectTransform
                Vector3 localHit = plane.InverseTransformPoint(hitWorld);
                Rect rect = plane.rect;

                // Normalize to 0–1 UV
                float u = (localHit.x - rect.x) / rect.width;
                float v = (localHit.y - rect.y) / rect.height;

                // Only accept if within bounds
                if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                {
                    bestUV = new Vector2(u, v);
                    closestDist = enter;
                    hitAny = true;
                }
            }
        }

        if (hitAny)
        {
            gazeUV = bestUV;
        }
        else
        {
            // Default to center when not looking at any feed plane
            gazeUV = new Vector2(0.5f, 0.5f);
        }
    }
}
