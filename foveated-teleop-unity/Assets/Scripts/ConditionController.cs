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
        Foveated_90_5,
        InverseFoveated_5_90
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
    private string cachedIp = "127.0.0.1";
    private int cachedPort = 1234;

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
            var rc = FindAnyObjectByType<CameraFeedReceiver>();
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
        
        cachedIp = config.robotIP;
        cachedPort = config.controlPort;

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
                    SetCondition(Condition.Foveated_90_5);
                }
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                {
                    SetCondition(Condition.InverseFoveated_5_90);
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

        if (cmd != null)
        {
            if (isConnected && stream != null)
            {
                try
                {
                    byte[] data = Encoding.ASCII.GetBytes(cmd);
                    stream.Write(data, 0, data.Length);
                    Debug.Log($"[ConditionController] ✅ SENT command: {cmd.TrimEnd('\n')}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ConditionController] ❌ Send FAILED: {ex.Message}");
                    MarkDisconnected();
                }
            }
            else
            {
                Debug.LogError($"[ConditionController] ❌ DROPPED command (not connected): {cmd.TrimEnd('\n')}  isConnected={isConnected}, stream={stream != null}");
            }
        }
    }

    public void SetCondition(Condition c, bool updateShader = true)
    {
        activeCondition = c;
        string msg = "";
        switch (c)
        {
            case Condition.UniformQ50:
                msg = "$CFGuniform 050050\n";
                break;
            case Condition.Foveated_90_5:
                msg = "$CFGgaze    005090\n"; // calibrated to 5 and 90
                break;
            case Condition.InverseFoveated_5_90:
                msg = "$CFGgaze    090005\n"; // calibrated inverse to 90 and 5
                break;
        }

        Debug.Log($"[ConditionController] 🔄 SetCondition({c}) → queuing: {msg.TrimEnd('\n')}  (isConnected={isConnected})");

        lock (lockObj)
        {
            pendingCommand = msg;
        }

        if (updateShader)
        {
            // Also update the local shader so the circle only appears in Foveated mode
            FoveatedFeedController ffc = FindAnyObjectByType<FoveatedFeedController>();
            if (ffc != null)
            {
                ffc.SetFoveationEnabled(c == Condition.Foveated_90_5 || c == Condition.InverseFoveated_5_90);
            }
        }
    }

    private void ConnectionLoop(CancellationToken token)
    {
        int attempt = 0;
        while (!token.IsCancellationRequested)
        {

            if (!isConnected)
            {
                attempt++;
                try
                {
                    string ip = cachedIp;
                    int port = cachedPort;
                    Debug.Log($"[ConditionController] Connecting to control port {ip}:{port} (attempt {attempt})...");
                    
                    tcpClient = new TcpClient();
                    tcpClient.Connect(ip, port);
                    stream = tcpClient.GetStream();
                    isConnected = true;
                    attempt = 0;
                    Debug.Log("[ConditionController] Connected to control port successfully!");

                    // Re-send current condition on connection/reconnection
                    SetCondition(activeCondition, false);
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

    private void AwakeHelper()
    {
        // No longer relying on RobotClient
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
