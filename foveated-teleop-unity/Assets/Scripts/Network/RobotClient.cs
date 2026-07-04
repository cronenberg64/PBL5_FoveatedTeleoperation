using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP Server that waits for the ESP32 to connect directly, then sends drive commands.
/// Protocol: CMD[srv:3][mtr:3]\n
/// </summary>
public class RobotClient : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private NetworkConfig config;

    [Header("Status (Read-Only)")]
    [SerializeField] private bool isConnected = false;
    [SerializeField] private string lastCommand = "";

    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream stream;
    private Thread listenerThread;
    private CancellationTokenSource cts;
    private readonly object lockObj = new object();
    private string pendingCommand = null;

    public bool IsConnected => isConnected;

    public void SendDriveCommand(int cmd, int turn, int speed)
    {
        int servoAngle = Mathf.RoundToInt(30 + (turn / 180f) * 120);
        servoAngle = Mathf.Clamp(servoAngle, 30, 150);

        float throttle = (speed - 256) * (200f / 256f);
        int motorSpeed = 255;
        if (cmd == 1) motorSpeed = Mathf.RoundToInt(255 + throttle);
        else if (cmd == 2) motorSpeed = Mathf.RoundToInt(255 - throttle);
        
        motorSpeed = Mathf.Clamp(motorSpeed, 55, 455);

        string cmdString = $"CMD{servoAngle:D3}{motorSpeed:D3}\n";
        
        lock (lockObj)
        {
            if (isConnected) pendingCommand = cmdString;
        }
    }

    public void SendRawCommand(string command)
    {
        lock (lockObj)
        {
            if (isConnected) pendingCommand = command;
        }
    }

    private void Start()
    {
        if (config == null) return;
        cts = new CancellationTokenSource();
        listenerThread = new Thread(() => ListenForESP32(cts.Token));
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    private string lastSentCommand = "";
    private float lastSendTime = 0f;

    private void Update()
    {
        string cmd = null;
        lock (lockObj)
        {
            cmd = pendingCommand;
        }

        if (cmd != null && isConnected && stream != null)
        {
            if (cmd != lastSentCommand)
            {
                try
                {
                    byte[] data = Encoding.ASCII.GetBytes(cmd);
                    stream.Write(data, 0, data.Length);
                    lastCommand = cmd.TrimEnd('\n');
                    lastSentCommand = cmd;
                    MetricsLogger.Instance?.Log("cmd_sent", data.Length, 0f, lastCommand);
                }
                catch
                {
                    isConnected = false;
                }
            }
        }
    }

    private void ListenForESP32(CancellationToken token)
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, config.esp32Port);
            tcpListener.Start();
            Debug.Log($"[RobotClient] TCP Server listening on port {config.esp32Port} for ESP32...");
            
            while (!token.IsCancellationRequested)
            {
                if (!tcpListener.Pending())
                {
                    Thread.Sleep(100);
                    continue;
                }

                connectedClient = tcpListener.AcceptTcpClient();
                connectedClient.NoDelay = true;
                stream = connectedClient.GetStream();
                isConnected = true;
                Debug.Log($"[RobotClient] ESP32 CONNECTED from {((IPEndPoint)connectedClient.Client.RemoteEndPoint).Address}!");

                while (isConnected && !token.IsCancellationRequested)
                {
                    if (connectedClient.Client.Poll(0, SelectMode.SelectRead) && connectedClient.Client.Available == 0)
                    {
                        isConnected = false;
                        Debug.LogWarning("[RobotClient] ESP32 Disconnected.");
                        break;
                    }
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[RobotClient] Listener error: " + e.Message);
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        isConnected = false;
        stream?.Close();
        connectedClient?.Close();
        tcpListener?.Stop();
        if (listenerThread != null) listenerThread.Join(1000);
    }

    private void OnApplicationQuit()
    {
        OnDestroy();
    }
}
