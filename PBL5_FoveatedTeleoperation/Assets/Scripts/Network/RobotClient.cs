using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP client that sends drive commands to the Pioneer robot.
/// Protocol: $CMD + turn(3 digits) + speed(3 digits) + \n
/// CMD: "1" = forward, "2" = backward, "0" = stop
/// Neutral turn = 090, neutral speed = 256.
/// </summary>
public class RobotClient : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private NetworkConfig config;

    [Header("Status (Read-Only)")]
    [SerializeField] private bool isConnected;
    [SerializeField] private string lastCommand = "";

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread connectThread;
    private CancellationTokenSource cts;

    private readonly object lockObj = new object();
    private string pendingCommand = null;
    private bool shouldReconnect = false;

    // ─── Public API ─────────────────────────────────────────────

    /// <summary>Whether the client is currently connected to the robot.</summary>
    public bool IsConnected => isConnected;

    /// <summary>
    /// Queue a drive command to be sent on the next network tick.
    /// </summary>
    /// <param name="cmd">Command code: 0=stop, 1=forward, 2=backward</param>
    /// <param name="turn">Turn value 0–180 (90 = straight)</param>
    /// <param name="speed">Speed value 0–512 (256 = neutral/stopped)</param>
    public void SendDriveCommand(int cmd, int turn, int speed)
    {
        // Enforce safety clamps
        cmd = Mathf.Clamp(cmd, 0, 2);
        turn = Mathf.Clamp(turn, 0, config != null ? config.maxTurn : 180);
        speed = Mathf.Clamp(speed, 0, config != null ? config.maxSpeed : 512);

        string formatted = $"${cmd}{turn:D3}{speed:D3}\n";
        lock (lockObj)
        {
            pendingCommand = formatted;
        }
    }

    /// <summary>
    /// Send a raw command string (e.g. config message) directly over the control socket.
    /// </summary>
    public void SendRawCommand(string command)
    {
        lock (lockObj)
        {
            pendingCommand = command;
        }
    }

    // ─── Lifecycle ──────────────────────────────────────────────

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("[RobotClient] NetworkConfig is not assigned!");
            return;
        }

        cts = new CancellationTokenSource();
        connectThread = new Thread(() => ConnectionLoop(cts.Token));
        connectThread.IsBackground = true;
        connectThread.Start();
    }

    private void Update()
    {
        // Grab pending command and send it
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
                lastCommand = cmd.TrimEnd('\n');
                // packed: cmd digit is [1], turn is [2..4], speed is [5..7]
                MetricsLogger.Instance?.Log("cmd_sent", data.Length, 0f, lastCommand);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RobotClient] Send failed: {ex.Message}");
                MarkDisconnected();
            }
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    // ─── Connection Logic (Background Thread) ───────────────────

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
                    Debug.Log($"[RobotClient] Connecting to {config.robotIP}:{config.controlPort} (attempt {attempt})...");
                    tcpClient = new TcpClient();
                    tcpClient.Connect(config.robotIP, config.controlPort);
                    stream = tcpClient.GetStream();
                    isConnected = true;
                    attempt = 0;
                    Debug.Log("[RobotClient] Connected successfully!");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RobotClient] Connection failed: {ex.Message}");
                    isConnected = false;

                    // Exponential back-off: 1s, 2s, 4s, 8s, max 16s
                    int waitSec = Mathf.Min((int)Mathf.Pow(2, attempt - 1), 16);
                    Thread.Sleep(waitSec * 1000);
                }
            }
            else
            {
                // Poll connection health
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

    private void MarkDisconnected()
    {
        isConnected = false;
        Debug.LogWarning("[RobotClient] Disconnected from robot.");
    }

    private void Disconnect()
    {
        cts?.Cancel();
        try { stream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        isConnected = false;
        connectThread?.Join(2000);
    }
}
