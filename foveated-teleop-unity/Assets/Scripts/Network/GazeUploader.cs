using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Sends the operator's gaze UV upstream to the mock Pioneer server at ~30 Hz.
///
/// Protocol: $GAZE[u:D3][v:D3]\n  (u,v = UV * 1000, clamped 0–999)
/// Example:  $GAZE500500\n  →  gaze at image centre.
/// </summary>
public class GazeUploader : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private NetworkConfig config;
    [SerializeField] private GazeProvider gazeProvider;

    [Header("Status (Read-Only)")]
    [SerializeField] private bool isConnected;
    [SerializeField] private string lastUploaded = "";
    [SerializeField] private float uploadHz;

    private TcpClient tcpClient;
    private NetworkStream stream;
    private Thread uploadThread;
    private CancellationTokenSource cts;

    private const int SendIntervalMs = 33;  // ~30 Hz

    // Thread-safe gaze snapshot — written by main thread, read by background thread
    private volatile float _gazeU = 0.5f;
    private volatile float _gazeV = 0.5f;

    // ─── Lifecycle ──────────────────────────────────────────────

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
                UnityEngine.Debug.LogError("[GazeUploader] NetworkConfig is not assigned!");
                return;
            }
        }
        if (gazeProvider == null)
        {
            gazeProvider = FindAnyObjectByType<GazeProvider>();
            if (gazeProvider == null)
            {
                UnityEngine.Debug.LogWarning("[GazeUploader] GazeProvider is not assigned. Will try to find one in Update.");
            }
        }

        cts = new CancellationTokenSource();
        uploadThread = new Thread(() => UploadLoop(cts.Token));
        uploadThread.IsBackground = true;
        uploadThread.Start();
    }

    private void Update()
    {
        // Gaze is now pushed externally by FoveatedFeedController.
        // We only maintain the loop here.
    }

    public void ForceGaze(float u, float v)
    {
        _gazeU = u;
        _gazeV = v;
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    // ─── Upload Loop (Background Thread) ────────────────────────

    private void UploadLoop(CancellationToken token)
    {
        int attempt = 0;
        int sentThisSecond = 0;
        long windowStartTicks = Stopwatch.GetTimestamp();

        while (!token.IsCancellationRequested)
        {
            if (!isConnected)
            {
                attempt++;
                try
                {
                    UnityEngine.Debug.Log($"[GazeUploader] Connecting to {config.robotIP}:{config.gazePort} (attempt {attempt})…");
                    tcpClient = new TcpClient();
                    tcpClient.Connect(config.robotIP, config.gazePort);
                    stream = tcpClient.GetStream();
                    isConnected = true;
                    attempt = 0;
                    UnityEngine.Debug.Log("[GazeUploader] Connected to gaze server.");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[GazeUploader] Connection failed: {ex.Message}");
                    isConnected = false;
                    int waitSec = Mathf.Min((int)Mathf.Pow(2, attempt - 1), 16);
                    Thread.Sleep(waitSec * 1000);
                    continue;
                }
            }

            // Send gaze sample
            try
            {
                // Read the volatile snapshot (written by main thread in Update)
                float gu = _gazeU;
                float gv = _gazeV;
                int u = Mathf.Clamp(Mathf.RoundToInt(gu * 1000f), 0, 999);
                int v = Mathf.Clamp(Mathf.RoundToInt(gv * 1000f), 0, 999);

                string msg = $"$GAZE{u:D3}{v:D3}\n";
                byte[] data = Encoding.ASCII.GetBytes(msg);
                stream.Write(data, 0, data.Length);

                lastUploaded = msg.TrimEnd('\n');
                sentThisSecond++;

                // Rolling 1-second Hz estimate (thread-safe via Stopwatch)
                long nowTicks = Stopwatch.GetTimestamp();
                double elapsedSec = (nowTicks - windowStartTicks) / (double)Stopwatch.Frequency;
                if (elapsedSec >= 1.0)
                {
                    uploadHz = (float)(sentThisSecond / elapsedSec);
                    sentThisSecond = 0;
                    windowStartTicks = nowTicks;

                    MetricsLogger.Instance?.Log("gaze_uploaded", uploadHz, 0f, "");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[GazeUploader] Send failed: {ex.Message}");
                MarkDisconnected();
            }

            Thread.Sleep(SendIntervalMs);
        }
    }

    private void MarkDisconnected()
    {
        isConnected = false;
        try { stream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        UnityEngine.Debug.LogWarning("[GazeUploader] Disconnected from gaze server.");
    }

    private void Shutdown()
    {
        cts?.Cancel();
        MarkDisconnected();
        uploadThread?.Join(2000);
    }
}
