using UnityEngine;
#if TOBII_SDK
using Tobii.Gaming;
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
#if TOBII_SDK
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

#if TOBII_SDK
        GazePoint gazePoint = TobiiAPI.GetGazePoint();
        if (gazePoint.IsValid)
        {
            // Tobii screen coordinates are 0-1 from bottom-left to top-right
            currentGazeUV = gazePoint.Viewport;
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
