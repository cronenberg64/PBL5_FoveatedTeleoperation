using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads Unity Input System actions and converts them into robot drive commands.
///
/// Input mapping:
///   Accelerator  → Right Trigger (or UpArrow)
///   Brake        → Left Trigger  (or DownArrow)
///   Steer        → Right Thumbstick X (or Left/Right Arrow)
///   GearShift    → A Button (or Space)
///
/// Protocol output: $CMD + turn(3 digits, 000–180) + speed(3 digits, 000–512) + \n
///   CMD: 1=forward, 2=backward, 0=stop
///   Neutral turn  = 090 (straight)
///   Neutral speed = 256 (stopped)
/// </summary>
public class TeleoperationController : MonoBehaviour
{
    public enum Gear { Forward, Reverse }

    [Header("References")]
    [SerializeField] private RobotClient robotClient;
    [SerializeField] private NetworkConfig config;

    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Tuning")]
    [Tooltip("Dead-zone below which steering is ignored")]
    [Range(0f, 0.3f)]
    [SerializeField] private float steerDeadZone = 0.1f;

    [Tooltip("Dead-zone below which trigger input is ignored")]
    [Range(0f, 0.2f)]
    [SerializeField] private float triggerDeadZone = 0.05f;

    [Header("State (Read-Only)")]
    [SerializeField] private Gear currentGear = Gear.Forward;
    [SerializeField] private float currentSteer;
    [SerializeField] private float currentAccel;
    [SerializeField] private float currentBrake;

    private InputAction acceleratorAction;
    private InputAction brakeAction;
    private InputAction gearShiftAction;
    private InputAction steerAction;

    private bool gearShiftHeld = false;

    /// <summary>Current gear state, readable by UI.</summary>
    public Gear CurrentGear => currentGear;

    // ─── Lifecycle ──────────────────────────────────────────────

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogError("[TeleoperationController] Input Action Asset not assigned!");
            return;
        }

        var map = inputActions.FindActionMap("Teleoperation", throwIfNotFound: true);
        acceleratorAction = map.FindAction("Accelerator", throwIfNotFound: true);
        brakeAction       = map.FindAction("Brake",       throwIfNotFound: true);
        gearShiftAction   = map.FindAction("GearShift",   throwIfNotFound: true);
        steerAction       = map.FindAction("Steer",       throwIfNotFound: true);

        map.Enable();
    }

    private void OnDisable()
    {
        inputActions?.FindActionMap("Teleoperation")?.Disable();
    }

    private void FixedUpdate()
    {
        if (robotClient == null || config == null) return;

        ReadInputs();
        HandleGearShift();
        SendCommand();
    }

    // ─── Input Processing ───────────────────────────────────────

    private void ReadInputs()
    {
        currentAccel = acceleratorAction.ReadValue<float>();
        currentBrake = brakeAction.ReadValue<float>();
        currentSteer = steerAction.ReadValue<float>();

        // Fallback for Generic Gamepad / Serafim R1+ (Pedals = Triggers, Wheel = Left Stick X)
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float gpAccel = gamepad.rightTrigger.ReadValue();
            if (gpAccel > currentAccel) currentAccel = gpAccel;

            float gpBrake = gamepad.leftTrigger.ReadValue();
            if (gpBrake > currentBrake) currentBrake = gpBrake;

            float gpSteer = gamepad.leftStick.x.ReadValue();
            if (Mathf.Abs(gpSteer) > Mathf.Abs(currentSteer)) currentSteer = gpSteer;
        }

        // Apply dead-zones
        if (Mathf.Abs(currentSteer) < steerDeadZone) currentSteer = 0f;
        if (currentAccel < triggerDeadZone) currentAccel = 0f;
        if (currentBrake < triggerDeadZone) currentBrake = 0f;
    }

    private void HandleGearShift()
    {
        bool pressed = gearShiftAction.IsPressed();
        
        var gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonSouth.isPressed)
        {
            pressed = true;
        }

        // Toggle on rising edge only (de-bounce)
        if (pressed && !gearShiftHeld)
        {
            currentGear = (currentGear == Gear.Forward) ? Gear.Reverse : Gear.Forward;
            Debug.Log($"[TeleoperationController] Gear shifted to {currentGear}");
        }

        gearShiftHeld = pressed;
    }

    private void SendCommand()
    {
        int neutralTurn  = config.neutralTurn;  // 90
        int neutralSpeed = config.neutralSpeed; // 256

        // ── Steering ────────────────────────────────
        // steerInput: -1 (full left) to +1 (full right)
        // turn: 0 (full left) to 180 (full right), 90 = straight
        int turn = Mathf.RoundToInt(neutralTurn + currentSteer * neutralTurn);
        turn = Mathf.Clamp(turn, 0, config.maxTurn);

        // ── Speed ───────────────────────────────────
        // Net throttle: accelerator minus brake
        float throttle = Mathf.Clamp01(currentAccel) - Mathf.Clamp01(currentBrake);
        // speed: neutralSpeed (256) when idle, up to maxSpeed (512) at full throttle,
        //        or down to 0 at full brake
        int speed = Mathf.RoundToInt(neutralSpeed + throttle * neutralSpeed);
        speed = Mathf.Clamp(speed, 0, config.maxSpeed);

        // ── Command Code ────────────────────────────
        // Determine if the robot should be driving
        bool isIdle = Mathf.Approximately(throttle, 0f) && Mathf.Approximately(currentSteer, 0f);
        int cmd;
        if (isIdle)
        {
            cmd = 0; // Stop
        }
        else
        {
            cmd = (currentGear == Gear.Forward) ? 1 : 2;
        }

        robotClient.SendDriveCommand(cmd, turn, speed);
    }
}
