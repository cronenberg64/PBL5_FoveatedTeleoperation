using UnityEngine;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

public class SceneFrameStreamer : MonoBehaviour
{
    [Header("Configuration")]
    public NetworkConfig config;
    public RenderTexture targetRenderTexture;
    
    [Tooltip("If true, stream frames. If false, streaming is disabled.")]
    public bool enableStreaming = false;

    private Texture2D texture2D;
    private float timeSinceLastFrame = 0f;
    private float targetInterval = 1f / 30f;

    // TCP connection variables
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private Thread senderThread;
    private bool isRunning = false;
    
    // Concurrent queue to send frames to the sender thread
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();
    private AutoResetEvent queueEvent = new AutoResetEvent(false);

    private void Start()
    {
        if (targetRenderTexture == null)
        {
            Debug.LogError("[SceneFrameStreamer] Target RenderTexture is not set!");
            return;
        }

        // Initialize reusable Texture2D
        texture2D = new Texture2D(targetRenderTexture.width, targetRenderTexture.height, TextureFormat.RGB24, false);

        if (enableStreaming)
        {
            StartSenderThread();
        }
    }

    private void OnEnable()
    {
        if (enableStreaming && !isRunning)
        {
            StartSenderThread();
        }
    }

    private void OnDisable()
    {
        StopSenderThread();
    }

    private void OnDestroy()
    {
        StopSenderThread();
        if (texture2D != null)
        {
            Destroy(texture2D);
        }
    }

    private void FixedUpdate()
    {
        if (!enableStreaming) return;

        timeSinceLastFrame += Time.fixedDeltaTime;
        if (timeSinceLastFrame >= targetInterval)
        {
            timeSinceLastFrame -= targetInterval;
            CaptureAndQueueFrame();
        }
    }

    private void CaptureAndQueueFrame()
    {
        if (targetRenderTexture == null || texture2D == null || !isRunning) return;

        try
        {
            // Capture frame from RenderTexture
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = targetRenderTexture;

            texture2D.ReadPixels(new Rect(0, 0, targetRenderTexture.width, targetRenderTexture.height), 0, 0);
            texture2D.Apply();

            RenderTexture.active = previousActive;

            // Encode to JPEG at Quality 95
            byte[] jpegBytes = texture2D.EncodeToJPG(95);

            // Keep queue size clamped to avoid latency accumulation if network slows down
            if (frameQueue.Count > 2)
            {
                frameQueue.TryDequeue(out _);
            }

            frameQueue.Enqueue(jpegBytes);
            queueEvent.Set();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneFrameStreamer] Failed to capture and encode frame: {ex.Message}");
        }
    }

    private void StartSenderThread()
    {
        StopSenderThread();
        isRunning = true;
        senderThread = new Thread(SenderThreadLoop);
        senderThread.IsBackground = true;
        senderThread.Start();
        Debug.Log("[SceneFrameStreamer] Sender thread started.");
    }

    private void StopSenderThread()
    {
        isRunning = false;
        queueEvent.Set();

        if (senderThread != null)
        {
            if (!senderThread.Join(1000))
            {
#if !UNITY_WEBGL
                senderThread.Abort();
#endif
            }
            senderThread = null;
        }

        CloseConnection();
        
        // Clear queue
        while (frameQueue.TryDequeue(out _)) { }
    }

    private void CloseConnection()
    {
        if (networkStream != null)
        {
            networkStream.Close();
            networkStream = null;
        }
        if (tcpClient != null)
        {
            tcpClient.Close();
            tcpClient = null;
        }
    }

    private void SenderThreadLoop()
    {
        while (isRunning)
        {
            if (tcpClient == null || !tcpClient.Connected)
            {
                // Attempt connection
                CloseConnection();
                string ip = config != null ? config.robotIP : "127.0.0.1";
                int port = config != null ? config.sceneFramePort : 1237;

                try
                {
                    Debug.Log($"[SceneFrameStreamer] Connecting to mock_pioneer at {ip}:{port}...");
                    tcpClient = new TcpClient();
                    
                    // Connect with timeout
                    var result = tcpClient.BeginConnect(ip, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    if (!success)
                    {
                        throw new TimeoutException("[SceneFrameStreamer] Connection timeout.");
                    }
                    tcpClient.EndConnect(result);
                    networkStream = tcpClient.GetStream();
                    Debug.Log("[SceneFrameStreamer] Connected successfully.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SceneFrameStreamer] Connection failed: {ex.Message}. Retrying in 2 seconds...");
                    CloseConnection();
                    // Wait before retry
                    for (int i = 0; i < 20 && isRunning; i++)
                    {
                        Thread.Sleep(100);
                    }
                    continue;
                }
            }

            // Wait for new frame to send
            if (frameQueue.IsEmpty)
            {
                queueEvent.WaitOne(500);
            }

            if (!isRunning) break;

            if (frameQueue.TryDequeue(out byte[] jpegBytes))
            {
                try
                {
                    // Prepare length prefix (Big-Endian uint32)
                    byte[] lengthPrefix = BitConverter.GetBytes(jpegBytes.Length);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(lengthPrefix);
                    }

                    // Write to network stream
                    networkStream.Write(lengthPrefix, 0, lengthPrefix.Length);
                    networkStream.Write(jpegBytes, 0, jpegBytes.Length);
                    networkStream.Flush();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SceneFrameStreamer] Send error: {ex.Message}. Reconnecting...");
                    CloseConnection();
                }
            }
        }
    }
}
