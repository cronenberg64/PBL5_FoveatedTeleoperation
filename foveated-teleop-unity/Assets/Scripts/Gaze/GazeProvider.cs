using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Provides gaze coordinates (UV 0–1) for the foveated shader.
///
/// On Quest Pro: uses OVREyeGaze (eye-tracking hardware).
/// On Quest 3 / Editor: falls back to head-gaze (center-eye forward ray).
///
/// installed, define META_XR_SDK in Project Settings → Player → Scripting Define Symbols.
/// Without it, only head-gaze will be available.
/// </summary>
public class GazeProvider : MonoBehaviour
{
    public enum GazeMode
    {
        OVR,
        Tobii,
        Mouse,
        Wave,
        OpenXR
    }

    [Header("Gaze Mode Configuration")]
    [SerializeField] private GazeMode gazeMode = GazeMode.Mouse;

    public GazeMode ActiveGazeMode => gazeMode;

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

    /// <summary>Returns true if Tobii is providing valid gaze samples right now.</summary>
    public bool IsTobiiAvailable
    {
        get
        {
#if TOBII_SDK && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            var gazePoint = Tobii.GameIntegration.Net.TobiiGameIntegrationApi.GetLatestGazePoint();
            return gazePoint.IsValid;
#else
            return false;
#endif
        }
    }

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
        if (gazeMode == GazeMode.Tobii && Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer)
        {
            Debug.LogWarning("Tobii is only supported on Windows. Falling back to Mouse gaze.");
            gazeMode = GazeMode.Mouse;
        }

        if (gazeMode == GazeMode.Mouse)
        {
            if (_hasOverride)
            {
                gazeUV = _overrideUV;
            }
            else
            {
                Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                gazeUV = new Vector2(Mathf.Clamp01(mousePos.x / Screen.width), Mathf.Clamp01(mousePos.y / Screen.height));
            }
        }
        else if (gazeMode == GazeMode.Tobii)
        {
#if TOBII_SDK && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            var gazePoint = Tobii.GameIntegration.Net.TobiiGameIntegrationApi.GetLatestGazePoint();
            if (gazePoint.IsValid)
            {
                float screenW = GetSystemMetrics(SM_CXSCREEN);
                float screenH = GetSystemMetrics(SM_CYSCREEN);
                if (screenW <= 0f) screenW = 1920f;
                if (screenH <= 0f) screenH = 1080f;
                gazeUV = new Vector2(Mathf.Clamp01(gazePoint.X / screenW), Mathf.Clamp01(1f - (gazePoint.Y / screenH)));
            }
#else
            if (_hasOverride) gazeUV = _overrideUV;
            else gazeUV = new Vector2(0.5f, 0.5f);
#endif
        }
        else if (gazeMode == GazeMode.OpenXR)
        {
            if (!isEyeTrackingActive)
            {
                // First try HTC Vive Helper
                Vector3 dummyPos;
                Quaternion dummyRot;
                try
                {
                    if (ViveGazeHelper.TryGetGaze(out dummyPos, out dummyRot))
                    {
                        isEyeTrackingActive = true;
                        Debug.Log("[GazeProvider] OpenXR Eye tracking dynamically detected via ViveGazeHelper!");
                    }
                }
                catch { }

                // Fallback to standard Unity XR check
                if (!isEyeTrackingActive)
                {
                    var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
                    UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.EyeTracking, devices);
                    if (devices.Count > 0)
                    {
                        isEyeTrackingActive = true;
                        Debug.Log("[GazeProvider] OpenXR Eye tracking dynamically detected via Unity InputDevices!");
                    }
                }
            }

            if (isEyeTrackingActive)
            {
                Ray gazeRay = GetOpenXREyeTrackingRay();
                UpdateGazeUV(gazeRay);
            }
            else
            {
                Ray gazeRay = GetHeadGazeRay();
                UpdateGazeUV(gazeRay);
            }
        }
        else if (gazeMode == GazeMode.Wave)
        {
#if WAVE_XR
            if (!isEyeTrackingActive)
            {
                if (Wave.Essence.Eye.EyeManager.Instance != null && Wave.Essence.Eye.EyeManager.Instance.IsEyeTrackingAvailable())
                {
                    isEyeTrackingActive = true;
                    Debug.Log("[GazeProvider] Wave Eye tracking dynamically detected!");
                }
            }

            if (isEyeTrackingActive)
            {
                Ray gazeRay = GetWaveEyeTrackingRay();
                UpdateGazeUV(gazeRay);
            }
            else
            {
                Ray gazeRay = GetHeadGazeRay();
                UpdateGazeUV(gazeRay);
            }
#else
            if (_hasOverride)
            {
                gazeUV = _overrideUV;
            }
            else
            {
                Ray gazeRay = GetHeadGazeRay();
                UpdateGazeUV(gazeRay);
            }
#endif
        }
        else // GazeMode.OVR
        {
#if META_XR_SDK && false
            if (isEyeTrackingActive)
            {
                Ray gazeRay = GetEyeTrackingRay();
                UpdateGazeUV(gazeRay);
            }
            else
            {
                Ray gazeRay = GetHeadGazeRay();
                UpdateGazeUV(gazeRay);
            }
#else
            if (_hasOverride)
            {
                gazeUV = _overrideUV;
            }
            else
            {
                Ray gazeRay = GetHeadGazeRay();
                UpdateGazeUV(gazeRay);
            }
#endif
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
#if META_XR_SDK
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
#if WAVE_XR
        if (gazeMode == GazeMode.Wave)
        {
            if (Wave.Essence.Eye.EyeManager.Instance != null && Wave.Essence.Eye.EyeManager.Instance.IsEyeTrackingAvailable())
            {
                isEyeTrackingActive = true;
                Debug.Log("[GazeProvider] Wave Eye tracking is available.");
                return;
            }
        }
#endif
        if (gazeMode == GazeMode.OpenXR)
        {
            // First try HTC Vive Helper
            Vector3 dummyPos;
            Quaternion dummyRot;
            try
            {
                if (ViveGazeHelper.TryGetGaze(out dummyPos, out dummyRot))
                {
                    isEyeTrackingActive = true;
                    Debug.Log("[GazeProvider] OpenXR Eye tracking is available via ViveGazeHelper.");
                    return;
                }
            }
            catch { }

            // Fallback to standard Unity XR check
            var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.EyeTracking, devices);
            if (devices.Count > 0)
            {
                isEyeTrackingActive = true;
                Debug.Log("[GazeProvider] OpenXR Eye tracking is available via Unity InputDevices.");
                return;
            }
        }

        isEyeTrackingActive = false;
        Debug.Log("[GazeProvider] Eye tracking unavailable. Using head-gaze fallback.");
    }

    // ─── Gaze Ray Construction ──────────────────────────────────

    private Ray GetGazeRay()
    {
#if META_XR_SDK
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

#if META_XR_SDK
    /// <summary>Eye-tracking ray using OVRPlugin (Quest Pro).</summary>
    private Ray GetEyeTrackingRay()
    {
        OVRPlugin.EyeGazesState eyeGazesState = default;
        if (OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeGazesState))
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

    private Ray GetOpenXREyeTrackingRay()
    {
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;

        // 1. First, try HTC VIVE's specific API via the helper (if compiled)
        // This is necessary because VIVE's OpenXR extension sometimes doesn't populate eyesData correctly.
        try
        {
            if (ViveGazeHelper.TryGetGaze(out position, out rotation))
            {
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[GazeDebug] ViveGazeHelper returned Pos: {position}, Rot: {rotation.eulerAngles}. Local Dir: {rotation * Vector3.forward}");
                }
                
                // Let's assume the data is in Tracking Space (relative to XR Origin) like standard OpenXR
                return TransformRayToWorld(position, rotation * Vector3.forward);
            }
        }
        catch { /* Helper might not be available */ }

        var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.EyeTracking, devices);
        
        if (devices.Count > 0)
        {
            foreach (var eyeDevice in devices)
            {
                // 2. Try standard eyesData
                if (eyeDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.eyesData, out UnityEngine.XR.Eyes eyes))
                {
                    Vector3 leftPos = Vector3.zero;
                    Vector3 rightPos = Vector3.zero;
                    Quaternion leftRot = Quaternion.identity;
                    Quaternion rightRot = Quaternion.identity;

                    bool hasLeft = eyes.TryGetLeftEyePosition(out leftPos) && eyes.TryGetLeftEyeRotation(out leftRot);
                    bool hasRight = eyes.TryGetRightEyePosition(out rightPos) && eyes.TryGetRightEyeRotation(out rightRot);

                    if (hasLeft && hasRight)
                    {
                        position = (leftPos + rightPos) * 0.5f;
                        Vector3 direction = ((leftRot * Vector3.forward) + (rightRot * Vector3.forward)).normalized;
                        return TransformRayToWorld(position, direction);
                    }
                    else if (hasLeft)
                    {
                        return TransformRayToWorld(leftPos, leftRot * Vector3.forward);
                    }
                    else if (hasRight)
                    {
                        return TransformRayToWorld(rightPos, rightRot * Vector3.forward);
                    }
                }

                // 3. Fallback to devicePosition and deviceRotation
                // HTC Vive sometimes incorrectly returns head rotation if eye tracking is disabled in Vive Console.
                // But we must allow it if it's the only option.
                if (eyeDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out position) &&
                    eyeDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rotation))
                {
                    // To prevent it from perfectly locking to the center of the screen if it returns pure head rotation,
                    // we'll still return it, but log a warning if it perfectly matches the head.
                    return TransformRayToWorld(position, rotation * Vector3.forward);
                }
            }
        }

        return GetHeadGazeRay();
    }

#if WAVE_XR
    private Ray GetWaveEyeTrackingRay()
    {
        if (Wave.Essence.Eye.EyeManager.Instance != null)
        {
            Vector3 origin;
            Vector3 direction;
            if (Wave.Essence.Eye.EyeManager.Instance.GetEyeOrigin(Wave.Essence.Eye.EyeType.Combined, out origin) &&
                Wave.Essence.Eye.EyeManager.Instance.GetEyeDirectionNormalized(Wave.Essence.Eye.EyeType.Combined, out direction))
            {
                return TransformRayToWorld(origin, direction);
            }
        }
        return GetHeadGazeRay();
    }
#endif

    private Ray TransformRayToWorld(Vector3 localPosition, Vector3 localDirection)
    {
        if (vrCamera != null && vrCamera.transform.parent != null)
        {
            Vector3 worldPos = vrCamera.transform.parent.TransformPoint(localPosition);
            Vector3 worldDir = vrCamera.transform.parent.TransformDirection(localDirection);
            return new Ray(worldPos, worldDir);
        }
        else if (vrCamera != null)
        {
            Vector3 worldPos = vrCamera.transform.TransformPoint(localPosition);
            Vector3 worldDir = vrCamera.transform.TransformDirection(localDirection);
            return new Ray(worldPos, worldDir);
        }
        return new Ray(localPosition, localDirection);
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
#endif
}
