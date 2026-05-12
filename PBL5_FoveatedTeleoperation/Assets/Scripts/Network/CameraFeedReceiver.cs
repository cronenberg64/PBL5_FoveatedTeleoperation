using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Receives JPEG frames over TCP from the robot's camera server
/// and renders them onto a RawImage in the VR scene.
///
/// Frame protocol (simple length-prefixed):
///   [4 bytes big-endian uint32: frame length N]
///   [N bytes: JPEG data]
///
/// If your robot server sends raw JPEG streams without framing,
/// switch to a delimiter-based or fixed-size approach.
/// </summary>
public class CameraFeedReceiver : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private NetworkConfig config;

    [Header("Display")]
    [Tooltip("RawImage component where the camera feed is displayed")]
    [SerializeField] private RawImage feedDisplay;

    [Header("Status (Read-Only)")]
    [SerializeField] private bool isConnected;
    [SerializeField] private int framesReceived;

    private TcpClient tcpClient;
    private Thread receiveThread;
    private CancellationTokenSource cts;

    // Thread-safe queue for passing (JPEG bytes, receive-timestamp) to main thread
    private readonly ConcurrentQueue<(byte[] data, long receivedMs)> frameQueue =
        new ConcurrentQueue<(byte[], long)>();

    private Texture2D feedTexture;

    // ─── Lifecycle ──────────────────────────────────────────────

    private void Start()
    {
        if (config == null)
        {
            Debug.LogError("[CameraFeed] NetworkConfig is not assigned!");
            return;
        }
        if (feedDisplay == null)
        {
            Debug.LogError("[CameraFeed] Feed display RawImage is not assigned!");
            return;
        }

        feedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        feedDisplay.texture = feedTexture;

        cts = new CancellationTokenSource();
        receiveThread = new Thread(() => ReceiveLoop(cts.Token));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void Update()
    {
        // Dequeue and display the latest frame (skip intermediate frames)
        (byte[] data, long receivedMs) latestEntry = default;
        bool hasFrame = false;
        while (frameQueue.TryDequeue(out var entry))
        {
            latestEntry = entry;
            hasFrame = true;
        }

        if (hasFrame)
        {
            long decodeStart = Stopwatch.GetTimestamp();
            if (feedTexture.LoadImage(latestEntry.data))
            {
                feedDisplay.texture = feedTexture;
                framesReceived++;

                double decodeMs = (Stopwatch.GetTimestamp() - decodeStart)
                    * 1000.0 / Stopwatch.Frequency;
                MetricsLogger.Instance?.Log(
                    "frame_received",
                    latestEntry.data.Length,
                    (float)decodeMs,
                    "");
            }
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    // ─── Receive Loop (Background Thread) ───────────────────────

    private void ReceiveLoop(CancellationToken token)
    {
        int attempt = 0;
        while (!token.IsCancellationRequested)
        {
            // Try to connect
            try
            {
                attempt++;
                Debug.Log($"[CameraFeed] Connecting to {config.robotIP}:{config.cameraPort} (attempt {attempt})...");
                tcpClient = new TcpClient();
                tcpClient.Connect(config.robotIP, config.cameraPort);
                isConnected = true;
                attempt = 0;
                Debug.Log("[CameraFeed] Connected to camera server!");

                using (NetworkStream stream = tcpClient.GetStream())
                {
                    byte[] headerBuf = new byte[4];

                    while (!token.IsCancellationRequested)
                    {
                        // Read 4-byte frame length header
                        ReadExact(stream, headerBuf, 0, 4);
                        int frameLen = (headerBuf[0] << 24)
                                     | (headerBuf[1] << 16)
                                     | (headerBuf[2] << 8)
                                     | headerBuf[3];

                        if (frameLen <= 0 || frameLen > 10_000_000)
                        {
                            Debug.LogWarning($"[CameraFeed] Invalid frame length: {frameLen}. Reconnecting...");
                            break;
                        }

                        // Read the JPEG frame
                        byte[] jpegData = new byte[frameLen];
                        ReadExact(stream, jpegData, 0, frameLen);

                        // Enqueue for the main thread (keep queue small)
                        while (frameQueue.Count > 2)
                            frameQueue.TryDequeue(out _);
                        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        frameQueue.Enqueue((jpegData, nowMs));
                    }
                }
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Debug.LogWarning($"[CameraFeed] Connection error: {ex.Message}");
                isConnected = false;
                int waitSec = Mathf.Min((int)Mathf.Pow(2, attempt - 1), 16);
                Thread.Sleep(waitSec * 1000);
            }
            finally
            {
                try { tcpClient?.Close(); } catch { }
                isConnected = false;
            }
        }
    }

    /// <summary>Reads exactly <paramref name="count"/> bytes or throws.</summary>
    private static void ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
                throw new IOException("Connection closed by remote host.");
            totalRead += read;
        }
    }

    private void Shutdown()
    {
        cts?.Cancel();
        try { tcpClient?.Close(); } catch { }
        isConnected = false;
        receiveThread?.Join(2000);
    }
}
