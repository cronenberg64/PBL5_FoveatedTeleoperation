using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the FoveatedFeed shader material by feeding it the current gaze UV
/// from GazeProvider every frame. Attach to the same GameObject as the
/// camera feed RawImage.
///
/// Setup:
///   1. Create a Material using the "Teleoperation/FoveatedFeed" shader
///   2. Assign it to the RawImage component
///   3. Assign GazeProvider and this controller
/// </summary>
[RequireComponent(typeof(RawImage))]
public class FoveatedFeedController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GazeProvider gazeProvider;

    [Header("Shader Parameters")]
    [Tooltip("Radius of the clear foveal circle (UV space, 0–0.5)")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float foveaRadius = 0.15f;

    [Tooltip("Width of the smooth transition band")]
    [Range(0.01f, 0.3f)]
    [SerializeField] private float transitionWidth = 0.05f;

    [Tooltip("Blur radius for the periphery (higher = more blurry)")]
    [Range(1f, 64f)]
    [SerializeField] private float peripheryPixelSize = 15f;

    [Header("Toggle")]
    [SerializeField] private bool foveationEnabled = true;

    private Material feedMaterial;
    private RawImage rawImage;

    // Shader property IDs (cached for performance)
    private static readonly int GazePointId = Shader.PropertyToID("_GazePoint");
    private static readonly int FoveaRadiusId = Shader.PropertyToID("_FoveaRadius");
    private static readonly int TransitionWidthId = Shader.PropertyToID("_TransitionWidth");
    private static readonly int PeripheryPixelSizeId = Shader.PropertyToID("_PeripheryPixelSize");
    private static readonly int FoveaTexId = Shader.PropertyToID("_FoveaTex");
    private static readonly int CropRectId = Shader.PropertyToID("_CropRect");

    private CameraFeedReceiver feedReceiver;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();

        if (rawImage.material != null)
        {
            // Create an instance so we don't modify the shared material asset
            feedMaterial = new Material(rawImage.material);
            rawImage.material = feedMaterial;
        }
        else
        {
            Debug.LogError("[FoveatedFeedController] RawImage has no material assigned! " +
                           "Please assign a material using the Teleoperation/FoveatedFeed shader.");
        }

        feedReceiver = FindAnyObjectByType<CameraFeedReceiver>();
    }

    private void Update()
    {
        if (feedMaterial == null) return;

        // Update gaze point
        Vector2 gaze = gazeProvider != null
            ? gazeProvider.GazeUV
            : new Vector2(0.5f, 0.5f);

        feedMaterial.SetVector(GazePointId, new Vector4(gaze.x, gaze.y, 0, 0));
        feedMaterial.SetFloat(TransitionWidthId, transitionWidth);

        // Fetch active condition from ConditionController (fallback to Foveated if not found)
        ConditionController.Condition activeCondition = ConditionController.Condition.Foveated_15_85;
        if (ConditionController.Instance != null)
        {
            activeCondition = ConditionController.Instance.ActiveCondition;
        }

        if (activeCondition == ConditionController.Condition.UniformQ50)
        {
            // 1. Uniform Mode: Entire screen is sharp (foveal), and unpixelated
            feedMaterial.SetFloat(FoveaRadiusId, 10f);
            feedMaterial.SetFloat(PeripheryPixelSizeId, 1f); 
        }
        else if (activeCondition == ConditionController.Condition.InverseFoveated_TBD)
        {
            // 3. Inverse-Foveated Mode: The server provides inverted qualities, but we still apply the foveal patch
            feedMaterial.SetFloat(FoveaRadiusId, foveaRadius);
            feedMaterial.SetFloat(PeripheryPixelSizeId, peripheryPixelSize);
        }
        else // ConditionController.Condition.Foveated_15_85
        {
            // 2. Foveated Mode: Foveal circle is sharp, everything outside is pixelated
            feedMaterial.SetFloat(FoveaRadiusId, foveaRadius);
            feedMaterial.SetFloat(PeripheryPixelSizeId, peripheryPixelSize);
        }

        if (feedReceiver != null)
        {
            if (feedReceiver.FoveaTex != null)
            {
                feedMaterial.SetTexture(FoveaTexId, feedReceiver.FoveaTex);
            }
            RectInt rect = feedReceiver.CropRect;
            feedMaterial.SetVector(CropRectId, new Vector4(rect.x, rect.y, rect.width, rect.height));
        }
    }

    private void OnDestroy()
    {
        if (feedMaterial != null)
        {
            Destroy(feedMaterial);
        }
    }

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>Enable or disable the foveation effect at runtime.</summary>
    public void SetFoveationEnabled(bool enabled)
    {
        foveationEnabled = enabled;
    }

    /// <summary>Adjust the fovea radius at runtime.</summary>
    public void SetFoveaRadius(float radius)
    {
        foveaRadius = Mathf.Clamp(radius, 0.01f, 0.5f);
    }

    /// <summary>Adjust the periphery blur radius at runtime.</summary>
    public void SetPeripheryPixelSize(float size)
    {
        peripheryPixelSize = Mathf.Clamp(size, 1f, 64f);
    }
}
