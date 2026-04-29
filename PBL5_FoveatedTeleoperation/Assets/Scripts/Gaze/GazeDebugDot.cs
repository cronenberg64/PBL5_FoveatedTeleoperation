using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a small debug dot on the camera feed plane at the current gaze point.
/// Useful for verifying gaze tracking accuracy during Phase 3 testing.
/// </summary>
public class GazeDebugDot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private RectTransform feedPlane;

    [Header("Appearance")]
    [SerializeField] private Color dotColor = Color.red;
    [SerializeField] private float dotSize = 12f;

    [Header("Toggle")]
    [SerializeField] private bool showDebugDot = true;

    private GameObject dotObject;
    private RectTransform dotRect;
    private Image dotImage;

    private void Start()
    {
        CreateDot();
    }

    private void Update()
    {
        if (dotObject == null) return;

        dotObject.SetActive(showDebugDot);
        if (!showDebugDot) return;

        if (gazeProvider == null || feedPlane == null) return;

        // Position the dot at the gaze UV on the feed plane
        Vector2 uv = gazeProvider.GazeUV;
        Rect rect = feedPlane.rect;

        float x = rect.x + uv.x * rect.width;
        float y = rect.y + uv.y * rect.height;

        dotRect.anchoredPosition = new Vector2(x, y);
    }

    private void CreateDot()
    {
        dotObject = new GameObject("GazeDebugDot");
        dotObject.transform.SetParent(feedPlane, false);

        dotRect = dotObject.AddComponent<RectTransform>();
        dotRect.sizeDelta = new Vector2(dotSize, dotSize);
        dotRect.anchorMin = Vector2.zero;
        dotRect.anchorMax = Vector2.zero;
        dotRect.pivot = new Vector2(0.5f, 0.5f);

        dotImage = dotObject.AddComponent<Image>();
        dotImage.color = dotColor;
        dotImage.raycastTarget = false;

        // Make it circular by adding a mask-friendly sprite (or just use a round sprite)
        // For now, a colored square is sufficient for debugging
    }

    /// <summary>Toggle the debug dot on/off at runtime.</summary>
    public void SetVisible(bool visible)
    {
        showDebugDot = visible;
    }
}
