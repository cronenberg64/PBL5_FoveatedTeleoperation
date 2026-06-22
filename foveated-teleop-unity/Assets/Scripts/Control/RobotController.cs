using UnityEngine;
using UnityEngine.InputSystem;

public class RobotController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3f; // m/s
    public float turnSpeed = 90f; // deg/s

    [Header("State (Read-Only)")]
    public bool isBraking = false;

#if WAVE_XR
    [Header("Wave Settings")]
    [SerializeField] private WaveVR_Controller.EDeviceType waveDeviceType = WaveVR_Controller.EDeviceType.Dominant;
#endif

    private Rigidbody rb;
    private Vector3 spawnPosition = new Vector3(0f, 1f, -2f);
    private Quaternion spawnRotation = Quaternion.identity;

    [Header("OpenXR Input Bindings")]
    private InputAction driveAction;
    private InputAction startTrialAction;
    private InputAction endSuccessAction;
    private InputAction endFailAction;

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

        // Setup OpenXR bindings using Unity Input System
        driveAction = new InputAction(type: InputActionType.Value, binding: "<XRController>{RightHand}/thumbstick");
        startTrialAction = new InputAction(type: InputActionType.Button, binding: "<XRController>{RightHand}/primaryButton");
        endSuccessAction = new InputAction(type: InputActionType.Button, binding: "<XRController>{RightHand}/secondaryButton");
        endFailAction = new InputAction(type: InputActionType.Button, binding: "<XRController>{LeftHand}/secondaryButton");
    }

    private void OnEnable()
    {
        driveAction?.Enable();
        startTrialAction?.Enable();
        endSuccessAction?.Enable();
        endFailAction?.Enable();
    }

    private void OnDisable()
    {
        driveAction?.Disable();
        startTrialAction?.Disable();
        endSuccessAction?.Disable();
        endFailAction?.Disable();
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

        // Handle input for brake
        isBraking = keyboard != null && keyboard.spaceKey.isPressed;

#if META_XR_SDK
        isBraking |= OVRInput.Get(OVRInput.Button.PrimaryHandTrigger);
#endif

        // Reset hotkey for convenience (resets active trial in TrialMetricsLogger if present)
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
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
        // Handle Trial shortcuts from VR Controllers
        if (TrialMetricsLogger.Instance != null)
        {
            if (startTrialAction.WasPressedThisFrame() && !TrialMetricsLogger.Instance.IsTrialActive)
            {
                TrialMetricsLogger.Instance.StartTrial("VR_Trial", ConditionController.Instance != null ? ConditionController.Instance.ActiveCondition.ToString() : "Unknown");
            }
            if (TrialMetricsLogger.Instance.IsTrialActive)
            {
                if (endSuccessAction.WasPressedThisFrame())
                {
                    TrialMetricsLogger.Instance.EndTrial(true);
                }
                else if (endFailAction.WasPressedThisFrame())
                {
                    TrialMetricsLogger.Instance.EndTrial(false);
                }
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

        // WASD inputs
        float moveInput = 0f;
        float turnInput = 0f;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput -= 1f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turnInput += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turnInput -= 1f;
        }

        // VR Controller OpenXR input
        if (driveAction != null)
        {
            Vector2 vrInput = driveAction.ReadValue<Vector2>();
            moveInput += vrInput.y;
            turnInput += vrInput.x;
        }

#if META_XR_SDK
        moveInput += OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y;
        turnInput += OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).x;
#endif

#if WAVE_XR
        var waveDevice = WaveVR_Controller.Input(waveDeviceType);
        if (waveDevice != null && waveDevice.connected)
        {
            Vector2 touchpad = waveDevice.GetAxis(wvr.WVR_InputId.WVR_InputId_Alias1_Touchpad);
            moveInput += touchpad.y;
            turnInput += touchpad.x;
        }
#endif

        // Clamp inputs to ensure they don't exceed standard limits (additive movement)
        moveInput = Mathf.Clamp(moveInput, -1f, 1f);
        turnInput = Mathf.Clamp(turnInput, -1f, 1f);

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
