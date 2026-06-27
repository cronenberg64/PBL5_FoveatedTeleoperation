using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Canvas))]
public class VRCanvasAdjuster : MonoBehaviour
{
    [Header("Default Parameters")]
    [SerializeField] private float defaultDistance = 1.2f; // m in front of camera
    [SerializeField] private Vector2 defaultSize = new Vector2(3.0f, 1.7f); // size in meters

    [Header("Sensitivity")]
    [SerializeField] private float distanceStep = 0.1f; // m per keypress
    [SerializeField] private float sizeStep = 0.1f; // m per keypress
    [SerializeField] private float heightOffset = -0.2f; // Offset canvas height slightly down
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float maxDistance = 5.0f;
    [SerializeField] private float minSize = 0.5f;
    [SerializeField] private float maxSize = 8.0f;

    private RectTransform rectTransform;
    private float currentDistance;
    private Vector2 currentSize;

    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        ResetToDefaults();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool changed = false;

        // 1. Distance adjustments (+/- keys)
        // Note: equalsKey represents '=' which is shared with '+' on US keyboards
        if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
        {
            currentDistance = Mathf.Clamp(currentDistance + distanceStep, minDistance, maxDistance);
            changed = true;
        }
        else if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
        {
            currentDistance = Mathf.Clamp(currentDistance - distanceStep, minDistance, maxDistance);
            changed = true;
        }

        // 2. Size adjustments ([ / ] keys)
        if (kb.rightBracketKey.wasPressedThisFrame)
        {
            float aspect = defaultSize.y / defaultSize.x;
            currentSize = currentSize + new Vector2(sizeStep, sizeStep * aspect);
            currentSize.x = Mathf.Clamp(currentSize.x, minSize, maxSize);
            currentSize.y = Mathf.Clamp(currentSize.y, minSize * aspect, maxSize * aspect);
            changed = true;
        }
        else if (kb.leftBracketKey.wasPressedThisFrame)
        {
            float aspect = defaultSize.y / defaultSize.x;
            currentSize = currentSize - new Vector2(sizeStep, sizeStep * aspect);
            currentSize.x = Mathf.Clamp(currentSize.x, minSize, maxSize);
            currentSize.y = Mathf.Clamp(currentSize.y, minSize * aspect, maxSize * aspect);
            changed = true;
        }

        // 3. Reset (Backspace key)
        if (kb.backspaceKey.wasPressedThisFrame)
        {
            ResetToDefaults();
            changed = true;
        }

        if (changed)
        {
            ApplySettings();
        }
    }

    public void ResetToDefaults()
    {
        currentDistance = defaultDistance;
        currentSize = defaultSize;
        ApplySettings();
        Debug.Log($"[VRCanvasAdjuster] VR Viewport Adjusted: Distance={currentDistance:F2}m, Size={currentSize.x:F2}x{currentSize.y:F2}m");
    }

    private void ApplySettings()
    {
        if (rectTransform == null) return;
        rectTransform.localPosition = new Vector3(0, heightOffset, currentDistance);
        rectTransform.sizeDelta = currentSize;
        rectTransform.localScale = Vector3.one;
    }
}
