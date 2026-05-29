using UnityEngine;
using UnityEngine.InputSystem;

public class RobotController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f; // m/s
    public float turnSpeed = 90f; // deg/s

    [Header("State (Read-Only)")]
    public bool isBraking = false;

    private Rigidbody rb;
    private Vector3 spawnPosition = new Vector3(0f, 1f, -2f);
    private Quaternion spawnRotation = Quaternion.identity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Freeze Y position and X/Z rotation to satisfy the requirements
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionY | 
                             RigidbodyConstraints.FreezeRotationX | 
                             RigidbodyConstraints.FreezeRotationZ;
        }
    }

    private void Start()
    {
        // Store initial position as spawn point
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Handle input for brake
        isBraking = keyboard.spaceKey.isPressed;

        // Reset hotkey for convenience (resets active trial in TrialMetricsLogger if present)
        if (keyboard.rKey.wasPressedThisFrame)
        {
            if (TrialMetricsLogger.Instance != null && TrialMetricsLogger.Instance.IsTrialActive)
            {
                TrialMetricsLogger.Instance.EndTrial(false);
            }
            else
            {
                ResetRobot();
            }
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (isBraking)
        {
            // Apply strong braking
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            rb.angularVelocity = Vector3.zero;
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // WASD inputs
        float moveInput = 0f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput -= 1f;

        float turnInput = 0f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turnInput += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turnInput -= 1f;

        // Move forward/backward along local Z axis
        Vector3 targetVelocity = transform.forward * moveInput * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y; // Keep vertical physics if any
        rb.linearVelocity = targetVelocity;

        // Turn left/right along local Y axis
        float turnAmount = turnInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turnAmount, 0f));
    }

    public void ResetRobot()
    {
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Debug.Log("[RobotController] Robot position and velocities reset.");
    }

    public void SetSpawnPoint(Vector3 pos, Quaternion rot)
    {
        spawnPosition = pos;
        spawnRotation = rot;
    }
}
