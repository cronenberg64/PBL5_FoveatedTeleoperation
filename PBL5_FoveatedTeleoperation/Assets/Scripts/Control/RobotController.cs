using UnityEngine;

public class RobotController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f; // m/s
    public float turnSpeed = 90f; // deg/s

    [Header("State (Read-Only)")]
    public int trialNumber = 1;
    public float elapsedTime = 0f;
    public int collisionCount = 0;
    public bool isTrialActive = false;
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
        // Handle input for brake
        isBraking = Input.GetKey(KeyCode.Space);

        // Handle trial timer
        if (isTrialActive)
        {
            elapsedTime += Time.deltaTime;
        }

        // Reset hotkey for convenience
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetRobot();
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

        // WASD inputs
        float moveInput = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveInput += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveInput -= 1f;

        float turnInput = 0f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) turnInput += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) turnInput -= 1f;

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
        isTrialActive = false;
        elapsedTime = 0f;
        collisionCount = 0;
        Debug.Log("[RobotController] Robot state and position reset.");
    }

    public void SetSpawnPoint(Vector3 pos, Quaternion rot)
    {
        spawnPosition = pos;
        spawnRotation = rot;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only count collisions with obstacles, walls, etc., ignoring the ground
        if (collision.gameObject.name != "Ground")
        {
            collisionCount++;
            Debug.Log($"[RobotController] Collision detected with: {collision.gameObject.name}. Total collisions: {collisionCount}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TrialStart") || other.name == "TrialStart")
        {
            if (!isTrialActive)
            {
                isTrialActive = true;
                elapsedTime = 0f;
                collisionCount = 0;
                Debug.Log($"[RobotController] Trial {trialNumber} started!");
            }
        }
        else if (other.CompareTag("TrialEnd") || other.name == "TrialEnd")
        {
            if (isTrialActive)
            {
                isTrialActive = false;
                Debug.Log($"[RobotController] Trial {trialNumber} finished! Time: {elapsedTime:F2}s, Collisions: {collisionCount}");
                trialNumber++;
            }
        }
    }
}
