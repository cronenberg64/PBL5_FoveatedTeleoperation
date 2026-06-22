using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CollisionReporter : MonoBehaviour
{
    [Header("HUD Flash Settings")]
    [Tooltip("Reference to the Canvas UI Image that acts as a fullscreen red flash overlay")]
    public Image flashImage;
    
    [Tooltip("Duration of the red flash animation in seconds")]
    public float flashDuration = 0.2f;
    
    [Tooltip("Peak color and alpha of the red flash")]
    public Color flashColor = new Color(1f, 0f, 0f, 0.4f);

    private Coroutine flashCoroutine;

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object is tagged "Wall"
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Register the collision with the singleton metrics logger
            if (TrialMetricsLogger.Instance != null)
            {
                TrialMetricsLogger.Instance.RegisterCollision();
            }
            else
            {
                Debug.LogWarning("[CollisionReporter] No TrialMetricsLogger instance found to register collision.");
            }

            // Trigger the HUD flash
            if (flashImage != null)
            {
                if (flashCoroutine != null)
                {
                    StopCoroutine(flashCoroutine);
                }
                flashCoroutine = StartCoroutine(DoFlash());
            }
        }
    }

    private IEnumerator DoFlash()
    {
        float elapsed = 0f;
        
        // Start from peak color/alpha
        flashImage.color = flashColor;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedProgress = elapsed / flashDuration;
            
            // Linearly fade the alpha to 0
            float targetAlpha = Mathf.Lerp(flashColor.a, 0f, normalizedProgress);
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, targetAlpha);
            
            yield return null;
        }

        // Ensure it is completely transparent at the end
        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
    }
}
