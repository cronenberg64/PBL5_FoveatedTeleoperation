using UnityEngine;
#if TOBII_SDK && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
using Tobii.GameIntegration.Net;
#endif

/// <summary>
/// Gaze source for the Tobii Eye Tracker 5.
/// 
/// Provides screen-normalized UV coordinates (0-1) to the GazeProvider.
/// 
/// Setup:
///   1. Import the Tobii Game Integration SDK for Unity.
///   2. Add "TOBII_SDK" to Scripting Define Symbols in Player Settings.
///   3. Add this component to the same GameObject as GazeProvider.
/// </summary>
[RequireComponent(typeof(GazeProvider))]
public class TobiiGazeSource : MonoBehaviour
{
    [Header("Tobii Settings")]
    [SerializeField] private bool useTobii = true;
    
    [Header("Status (Read-Only)")]
    [SerializeField] private bool isTobiiPresent;
    [SerializeField] private Vector2 currentGazeUV;

    private GazeProvider _gazeProvider;

    private void Awake()
    {
        _gazeProvider = GetComponent<GazeProvider>();
    }

    private void Start()
    {
#if TOBII_SDK && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        isTobiiPresent = true;
        Debug.Log("[TobiiGazeSource] Tobii SDK detected and active.");
#else
        isTobiiPresent = false;
        Debug.LogWarning("[TobiiGazeSource] TOBII_SDK define not found. Tobii tracking will be disabled.");
#endif
    }

    private void Update()
    {
        if (!useTobii) return;

#if TOBII_SDK && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        var gazePoint = TobiiGameIntegrationApi.GetLatestGazePoint();
        if (gazePoint.IsValid)
        {
            float screenW = 1920f;
            float screenH = 1080f;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            screenW = bounds.Width;
            screenH = bounds.Height;
#endif
            // Tobii screen coordinates are relative to the primary monitor, top-left is (0,0).
            // Unity UV is bottom-left (0,0).
            currentGazeUV = new Vector2(Mathf.Clamp01(gazePoint.X / screenW), Mathf.Clamp01(1f - (gazePoint.Y / screenH)));
            _gazeProvider.SetGazeUVOverride(currentGazeUV);
        }
#endif
    }

    private void OnDisable()
    {
        if (_gazeProvider != null)
            _gazeProvider.ClearGazeUVOverride();
    }
}
