using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class ConditionController : MonoBehaviour
{
    public static ConditionController Instance { get; private set; }

    public enum Condition
    {
        UniformQ50,
        Foveated_15_85,
        PeripheralOnly_Q30
    }

    [Header("Configuration")]
    [SerializeField] private NetworkConfig config;

    [Header("State")]
    [SerializeField] private Condition activeCondition = Condition.UniformQ50;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread connectThread;
    private CancellationTokenSource cts;
    private readonly object lockObj = new object();
    private string pendingCommand = null;
    private bool isConnected = false;

    public Condition ActiveCondition => activeCondition;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        AwakeHelper();
    }

    private void Start()
    {
        if (config == null)
        {
            var rc = FindAnyObjectByType<RobotClient>();
            if (rc != null)
            {
                var field = rc.GetType().GetField("config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    config = (NetworkConfig)field.GetValue(rc);
                }
            }
            if (config == null)
            {
                Debug.LogError("[ConditionController] NetworkConfig is not assigned!");
                return;
            }
        }

        cts = new CancellationTokenSource();
        connectThread = new Thread(() => ConnectionLoop(cts.Token));
        connectThread.IsBackground = true;
        connectThread.Start();

        // Send initial condition
        SetCondition(activeCondition);
    }

    private void Update()
    {
        // Handle keyboard shortcuts if trial is NOT active
        if (TrialMetricsLogger.Instance == null || !TrialMetricsLogger.Instance.IsTrialActive)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    SetCondition(Condition.UniformQ50);
                }
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    SetCondition(Condition.Foveated_15_85);
                }
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                {
                    SetCondition(Condition.PeripheralOnly_Q30);
                }
            }
        }

        // Dequeue and write pending command if connected
        string cmd = null;
        lock (lockObj)
        {
            cmd = pendingCommand;
            pendingCommand = null;
        }

        if (cmd != null && isConnected && stream != null)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(cmd);
                stream.Write(data, 0, data.Length);
                Debug.Log($"[ConditionController] Sent command: {cmd.TrimEnd('\n')}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ConditionController] Send failed: {ex.Message}");
                MarkDisconnected();
            }
        }
    }

    public void SetCondition(Condition c)
    {
        activeCondition = c;
        string msg = "";
        switch (c)
        {
            case Condition.UniformQ50:
                msg = "$CFGuniform 050050\n";
                break;
            case Condition.Foveated_15_85:
                msg = "$CFGgaze    015085\n";
                break;
            case Condition.PeripheralOnly_Q30:
                msg = "$CFGperiph  030030\n";
                break;
        }

        RobotClient rc = FindAnyObjectByType<RobotClient>();
        if (rc != null && rc.isActiveAndEnabled)
        {
            rc.SendRawCommand(msg);
            Debug.Log($"[ConditionController] Routed condition message through RobotClient: {msg.TrimEnd('\n')}");
        }
        else
        {
            lock (lockObj)
            {
                pendingCommand = msg;
            }
        }
    }

    private void ConnectionLoop(CancellationToken token)
    {
        int attempt = 0;
        while (!token.IsCancellationRequested)
        {
            // If RobotClient is present in the scene, we don't need our own connection since we route through it.
            // To be safe, we can just allow the connection loop to run. If RobotClient is not there, we connect.
            // If RobotClient is there, we won't get any pending commands anyway because SetCondition routes it to rc.
            // But wait, if we connect, we might steal the port 1234 from RobotClient!
            // That is a critical point! If we connect, we steal port 1234 from RobotClient.
            // So we MUST NOT connect if RobotClient is present!
            // Let's declare a `private bool hasRobotClient = false;` updated on main thread in Start()/Update().
            
            if (hasRobotClient)
            {
                // We don't need our own connection; sleep and check again.
                Thread.Sleep(1000);
                continue;
            }

            if (!isConnected)
            {
                attempt++;
                try
                {
                    string ip = config != null ? config.robotIP : "127.0.0.1";
                    int port = config != null ? config.controlPort : 1234;
                    Debug.Log($"[ConditionController] Connecting to control port {ip}:{port} (attempt {attempt})...");
                    
                    tcpClient = new TcpClient();
                    tcpClient.Connect(ip, port);
                    stream = tcpClient.GetStream();
                    isConnected = true;
                    attempt = 0;
                    Debug.Log("[ConditionController] Connected to control port successfully!");

                    // Re-send current condition on connection/reconnection
                    SetCondition(activeCondition);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ConditionController] Connection failed (Is the python mock_pioneer server running?): {ex.Message}");
                    isConnected = false;
                    int waitSec = Mathf.Min((int)Mathf.Pow(2, attempt - 1), 16);
                    Thread.Sleep(waitSec * 1000);
                }
            }
            else
            {
                Thread.Sleep(500);
                try
                {
                    if (tcpClient != null && !tcpClient.Connected)
                    {
                        MarkDisconnected();
                    }
                }
                catch
                {
                    MarkDisconnected();
                }
            }
        }
    }

    private bool hasRobotClient = false;

    private void AwakeHelper()
    {
        // Check if RobotClient exists
        hasRobotClient = FindAnyObjectByType<RobotClient>() != null;
    }

    private void MarkDisconnected()
    {
        isConnected = false;
        Debug.LogWarning("[ConditionController] Disconnected from control port.");
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    private void Shutdown()
    {
        cts?.Cancel();
        try { stream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        isConnected = false;
        if (connectThread != null)
        {
            connectThread.Join(2000);
        }
    }
}
