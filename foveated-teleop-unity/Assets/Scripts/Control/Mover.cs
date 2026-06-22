using UnityEngine;
using System.Collections;

public class Mover : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Movement speed in m/s")]
    public float speed = 1f;

    [Tooltip("Distance to travel left/right from starting position")]
    public float range = 4f;

    [Tooltip("Initial delay before starting movement in seconds")]
    public float startDelay = 5f;

    private Vector3 startPosition;
    private bool canMove = false;
    private int direction = 1;

    private void Awake()
    {
        startPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        ResetMover();
    }

    public void ResetMover()
    {
        StopAllCoroutines();
        // Reset to initial position
        transform.localPosition = startPosition;
        canMove = false;
        direction = 1;

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(StartAfterDelay());
        }
    }

    private IEnumerator StartAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);
        canMove = true;
    }

    private void Update()
    {
        if (!canMove) return;

        // Move left-right relative to starting position
        Vector3 pos = transform.localPosition;
        pos.x += direction * speed * Time.deltaTime;

        // Ping-pong boundary check
        if (direction > 0 && pos.x >= startPosition.x + range)
        {
            pos.x = startPosition.x + range;
            direction = -1;
        }
        else if (direction < 0 && pos.x <= startPosition.x - range)
        {
            pos.x = startPosition.x - range;
            direction = 1;
        }

        transform.localPosition = pos;
    }
}
