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
    [SerializeField] private float transitionWidth = 0.1f;

    [Tooltip("Pixelation block size for the periphery (higher = more blocky)")]
    [Range(2f, 64f)]
    [SerializeField] private float peripheryPixelSize = 32f;

    [Header("Toggle")]
    [SerializeField] private bool foveationEnabled = true;

    private Material feedMaterial;
    private RawImage rawImage;

    // Shader property IDs (cached for performance)
    private static readonly int GazePointId = Shader.PropertyToID("_GazePoint");
    private static readonly int FoveaRadiusId = Shader.PropertyToID("_FoveaRadius");
    private static readonly int TransitionWidthId = Shader.PropertyToID("_TransitionWidth");
    private static readonly int PeripheryPixelSizeId = Shader.PropertyToID("_PeripheryPixelSize");

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
    }

    private void Update()
    {
        if (feedMaterial == null) return;

        if (!foveationEnabled)
        {
            // When disabled, set fovea radius to a huge value so everything is "foveal"
            feedMaterial.SetFloat(FoveaRadiusId, 10f);
            return;
        }

        // Update gaze point
        Vector2 gaze = gazeProvider != null
            ? gazeProvider.GazeUV
            : new Vector2(0.5f, 0.5f);

        feedMaterial.SetVector(GazePointId, new Vector4(gaze.x, gaze.y, 0, 0));
        feedMaterial.SetFloat(FoveaRadiusId, foveaRadius);
        feedMaterial.SetFloat(TransitionWidthId, transitionWidth);
        feedMaterial.SetFloat(PeripheryPixelSizeId, peripheryPixelSize);
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

    /// <summary>Adjust the periphery pixel size at runtime.</summary>
    public void SetPeripheryPixelSize(float size)
    {
        peripheryPixelSize = Mathf.Clamp(size, 2f, 64f);
    }
}
