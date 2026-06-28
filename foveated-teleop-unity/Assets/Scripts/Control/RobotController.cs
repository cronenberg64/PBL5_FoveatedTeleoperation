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
    private RobotClient robotClient;
    private Vector3 spawnPosition = new Vector3(0f, 1f, -2f);
    private Quaternion spawnRotation = Quaternion.identity;

    [Header("OpenXR Input Bindings")]
    private InputAction driveAction;
    private InputAction startTrialAction;
    private InputAction endSuccessAction;
    private InputAction endFailAction;

    [Header("Wheel Input (Workaround)")]
    public bool useWheel = true;
    private InputAction wheelSteerAction;
    private InputAction gamepadSteerAction;
    private InputAction gamepadGasAction;
    private InputAction gamepadBrakeAction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        robotClient = GetComponent<RobotClient>();
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

        // Setup Wheel Steering Input
        wheelSteerAction = new InputAction(type: InputActionType.Value, binding: "<Joystick>/stick/x");
        gamepadSteerAction = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftStick/x");
        
        // Setup Pedals (XInput triggers)
        gamepadGasAction = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/rightTrigger");
        gamepadBrakeAction = new InputAction(type: InputActionType.Value, binding: "<Gamepad>/leftTrigger");
    }

    private void OnEnable()
    {
        driveAction?.Enable();
        startTrialAction?.Enable();
        endSuccessAction?.Enable();
        endFailAction?.Enable();
        wheelSteerAction?.Enable();
        gamepadSteerAction?.Enable();
        gamepadGasAction?.Enable();
        gamepadBrakeAction?.Enable();
    }

    private void OnDisable()
    {
        driveAction?.Disable();
        startTrialAction?.Disable();
        endSuccessAction?.Disable();
        endFailAction?.Disable();
        wheelSteerAction?.Disable();
        gamepadSteerAction?.Disable();
        gamepadGasAction?.Disable();
        gamepadBrakeAction?.Disable();
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

        // Wheel Steering and Pedal Input
        if (useWheel)
        {
            // Steering
            float steerVal = 0f;
            if (wheelSteerAction != null && wheelSteerAction.ReadValue<float>() != 0f)
            {
                steerVal = wheelSteerAction.ReadValue<float>();
            }
            else if (gamepadSteerAction != null && gamepadSteerAction.ReadValue<float>() != 0f)
            {
                steerVal = gamepadSteerAction.ReadValue<float>();
            }

            // Apply deadzone for centering jitter from wheel's hall sensors
            if (Mathf.Abs(steerVal) > 0.05f)
            {
                turnInput += steerVal;
            }

            // Pedals
            float gasVal = gamepadGasAction != null ? gamepadGasAction.ReadValue<float>() : 0f;
            float brakeVal = gamepadBrakeAction != null ? gamepadBrakeAction.ReadValue<float>() : 0f;
            
            if (gasVal > 0.05f) moveInput += gasVal;
            if (brakeVal > 0.05f) moveInput -= brakeVal;
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

        // ---- Send to physical robot ----
        if (robotClient != null && robotClient.IsConnected)
        {
            int neutralTurn = 90;
            int neutralSpeed = 256;

            int turn = Mathf.RoundToInt(neutralTurn + turnInput * neutralTurn);
            turn = Mathf.Clamp(turn, 0, 180);

            int speed = Mathf.RoundToInt(neutralSpeed + Mathf.Abs(moveInput) * neutralSpeed);
            speed = Mathf.Clamp(speed, 0, 512);

            int cmd = 0;
            if (Mathf.Approximately(moveInput, 0f) && Mathf.Approximately(turnInput, 0f))
            {
                cmd = 0;
            }
            else if (moveInput >= 0f)
            {
                cmd = 1;
            }
            else
            {
                cmd = 2;
            }

            robotClient.SendDriveCommand(cmd, turn, speed);
        }

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
