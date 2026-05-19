using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Receives camera frames over TCP from the mock Pioneer server and renders
/// them onto a RawImage in the scene.
///
/// Supports two payload modes (auto-detected from NetworkConfig):
///
/// UNIFORM MODE  — payload is a plain JPEG.
///   [4 bytes big-endian uint32: payload length N]
///   [N bytes: JPEG data]
///
/// GAZE (DUAL-PAYLOAD) MODE — payload contains a low-quality full-frame
///   periphery and a high-quality foveal crop.
///   [4 bytes big-endian uint32: outer payload length N]
///   [N bytes: dual-payload body]
///     Body layout (see docs/PROTOCOL.md):
///       Offset  Size  Field
///        0       4    len_periph  (uint32 big-endian)
///        4       4    len_fovea   (uint32 big-endian)
///        8       2    crop_x      (uint16 big-endian)
///       10       2    crop_y      (uint16 big-endian)
///       12       2    crop_w      (uint16 big-endian)
///       14       2    crop_h      (uint16 big-endian)
///       16       len_periph bytes  periphery JPEG
///       16+len_periph  len_fovea bytes  foveal JPEG
///
/// The foveal patch is blitted at the server's recorded crop coords, NOT at
/// current live gaze. This keeps the patch co-registered with what was encoded.
/// </summary>
public class CameraFeedReceiver : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private NetworkConfig config;

    [Header("Display")]
    [Tooltip("RawImage component where the camera feed is displayed")]
    [SerializeField] private RawImage feedDisplay;

    [Header("Mode")]
    [Tooltip("When enabled, parse the dual-payload gaze-contingent format instead of plain JPEG")]
    [SerializeField] private bool dualPayloadMode = false;

    [Header("Status (Read-Only)")]
    [SerializeField] private bool isConnected;
    [SerializeField] private int framesReceived;

    private TcpClient tcpClient;
    private Thread receiveThread;
    private CancellationTokenSource cts;

    // ── Frame types ────────────────────────────────────────────────────────

    private readonly struct UniformFrame
    {
        public readonly byte[] JpegData;
        public readonly long   ReceivedMs;
        public UniformFrame(byte[] data, long ms) { JpegData = data; ReceivedMs = ms; }
    }

    private readonly struct DualFrame
    {
        public readonly byte[] PeriphJpeg;
        public readonly byte[] FoveaJpeg;
        public readonly int    CropX, CropY, CropW, CropH;
        public readonly long   ReceivedMs;
        public readonly int    TotalBytes;
        public DualFrame(byte[] periph, byte[] fovea, int x, int y, int w, int h, long ms)
        {
            PeriphJpeg = periph; FoveaJpeg = fovea;
            CropX = x; CropY = y; CropW = w; CropH = h;
            ReceivedMs = ms; TotalBytes = periph.Length + fovea.Length + 16;
        }
    }

    // Thread-safe queues — only one will be used depending on mode
    private readonly ConcurrentQueue<UniformFrame> _uniformQueue = new();
    private readonly ConcurrentQueue<DualFrame>    _dualQueue    = new();

    // Textures reused each frame to avoid GC pressure
    private Texture2D _periphTex;   // full-frame periphery (or uniform)
    private Texture2D _foveaTex;    // foveal patch (dual mode only)

    // ── Lifecycle ──────────────────────────────────────────────────────────

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

        _periphTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (dualPayloadMode)
            _foveaTex = new Texture2D(2, 2, TextureFormat.RGB24, false);

        feedDisplay.texture = _periphTex;

        cts = new CancellationTokenSource();
        receiveThread = new Thread(() => ReceiveLoop(cts.Token));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void Update()
    {
        if (dualPayloadMode)
            ApplyDualFrames();
        else
            ApplyUniformFrames();
    }

    private void OnDestroy()  => Shutdown();
    private void OnApplicationQuit() => Shutdown();

    // ── Main-thread frame application ──────────────────────────────────────

    private void ApplyUniformFrames()
    {
        UniformFrame latest = default;
        bool hasFrame = false;
        while (_uniformQueue.TryDequeue(out var f)) { latest = f; hasFrame = true; }
        if (!hasFrame) return;

        long decodeStart = Stopwatch.GetTimestamp();
        if (_periphTex.LoadImage(latest.JpegData))
        {
            feedDisplay.texture = _periphTex;
            framesReceived++;
            double decodeMs = (Stopwatch.GetTimestamp() - decodeStart) * 1000.0 / Stopwatch.Frequency;
            MetricsLogger.Instance?.Log("frame_received", latest.JpegData.Length, (float)decodeMs, "uniform");
        }
    }

    private void ApplyDualFrames()
    {
        DualFrame latest = default;
        bool hasFrame = false;
        while (_dualQueue.TryDequeue(out var f)) { latest = f; hasFrame = true; }
        if (!hasFrame) return;

        long decodeStart = Stopwatch.GetTimestamp();

        // 1. Decode periphery as the base texture
        bool ok = _periphTex.LoadImage(latest.PeriphJpeg);
        if (!ok) return;

        // 2. Decode the foveal patch
        if (!_foveaTex.LoadImage(latest.FoveaJpeg)) return;

        // 3. Blit foveal patch onto periphery at server's recorded crop coords.
        //    Periphery is the full original frame size (server sends full-res, low-Q).
        //    We blit pixel by pixel via GetPixels32 / SetPixels32 for correctness.
        BlitFoveaOntoBase(latest.CropX, latest.CropY, latest.CropW, latest.CropH);

        feedDisplay.texture = _periphTex;
        framesReceived++;

        double decodeMs = (Stopwatch.GetTimestamp() - decodeStart) * 1000.0 / Stopwatch.Frequency;
        MetricsLogger.Instance?.Log("frame_received", latest.TotalBytes, (float)decodeMs, "gaze");
    }

    /// <summary>
    /// Blit _foveaTex pixels into _periphTex at the given crop rectangle.
    /// Both textures must already be decoded (LoadImage called).
    /// </summary>
    private void BlitFoveaOntoBase(int cropX, int cropY, int cropW, int cropH)
    {
        // Resize fovea tex to match crop dimensions if necessary
        Color32[] foveaPixels = _foveaTex.GetPixels32();
        int fw = _foveaTex.width;
        int fh = _foveaTex.height;

        int bw = _periphTex.width;
        int bh = _periphTex.height;

        Color32[] basePixels = _periphTex.GetPixels32();

        // Map foveal pixels into the base pixel array
        for (int fy = 0; fy < fh && fy < cropH; fy++)
        {
            for (int fx = 0; fx < fw && fx < cropW; fx++)
            {
                // Unity Texture2D: pixel (0,0) = bottom-left.
                // crop_y/crop_x from server: top-left origin.
                // Convert: base_row = (bh - 1) - (cropY + fy)
                int baseRow = (bh - 1) - (cropY + fy);
                int baseCol = cropX + fx;
                if (baseRow < 0 || baseRow >= bh || baseCol < 0 || baseCol >= bw) continue;

                int baseIdx  = baseRow  * bw + baseCol;
                int foveaIdx = (fh - 1 - fy) * fw + fx; // fovea also bottom-left after LoadImage
                basePixels[baseIdx] = foveaPixels[foveaIdx];
            }
        }

        _periphTex.SetPixels32(basePixels);
        _periphTex.Apply();
    }

    // ── Receive Loop (Background Thread) ──────────────────────────────────

    private void ReceiveLoop(CancellationToken token)
    {
        int attempt = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                attempt++;
                Debug.Log($"[CameraFeed] Connecting to {config.robotIP}:{config.cameraPort} (attempt {attempt})...");
                tcpClient = new TcpClient();
                tcpClient.Connect(config.robotIP, config.cameraPort);
                isConnected = true;
                attempt = 0;
                Debug.Log($"[CameraFeed] Connected — mode={(dualPayloadMode ? "dual-payload" : "uniform")}");

                using (NetworkStream stream = tcpClient.GetStream())
                {
                    byte[] outerHdr = new byte[4];
                    while (!token.IsCancellationRequested)
                    {
                        // Read outer 4-byte frame length
                        ReadExact(stream, outerHdr, 0, 4);
                        int frameLen = (outerHdr[0] << 24) | (outerHdr[1] << 16)
                                     | (outerHdr[2] << 8)  |  outerHdr[3];

                        if (frameLen <= 0 || frameLen > 20_000_000)
                        {
                            Debug.LogWarning($"[CameraFeed] Invalid frame length: {frameLen}. Reconnecting...");
                            break;
                        }

                        byte[] payload = new byte[frameLen];
                        ReadExact(stream, payload, 0, frameLen);
                        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        if (dualPayloadMode)
                        {
                            if (TryParseDualPayload(payload, nowMs, out DualFrame df))
                            {
                                while (_dualQueue.Count > 2) _dualQueue.TryDequeue(out _);
                                _dualQueue.Enqueue(df);
                            }
                        }
                        else
                        {
                            while (_uniformQueue.Count > 2) _uniformQueue.TryDequeue(out _);
                            _uniformQueue.Enqueue(new UniformFrame(payload, nowMs));
                        }
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

    // ── Dual-payload parser ────────────────────────────────────────────────

    /// <summary>
    /// Parse a dual-payload body (everything after the outer 4-byte length prefix).
    ///
    /// Layout:
    ///   [0..3]  uint32 BE  len_periph
    ///   [4..7]  uint32 BE  len_fovea
    ///   [8..9]  uint16 BE  crop_x
    ///   [10..11] uint16 BE crop_y
    ///   [12..13] uint16 BE crop_w
    ///   [14..15] uint16 BE crop_h
    ///   [16 .. 16+len_periph)        periphery JPEG
    ///   [16+len_periph .. end)        foveal JPEG
    /// </summary>
    private static bool TryParseDualPayload(byte[] data, long nowMs, out DualFrame result)
    {
        result = default;
        const int HeaderSize = 16;
        if (data.Length < HeaderSize) return false;

        int lenPeriph = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
        int lenFovea  = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
        int cropX     = (data[8]  << 8) | data[9];
        int cropY     = (data[10] << 8) | data[11];
        int cropW     = (data[12] << 8) | data[13];
        int cropH     = (data[14] << 8) | data[15];

        if (lenPeriph <= 0 || lenFovea <= 0) return false;
        if (data.Length < HeaderSize + lenPeriph + lenFovea) return false;

        byte[] periph = new byte[lenPeriph];
        byte[] fovea  = new byte[lenFovea];
        Array.Copy(data, HeaderSize,             periph, 0, lenPeriph);
        Array.Copy(data, HeaderSize + lenPeriph, fovea,  0, lenFovea);

        result = new DualFrame(periph, fovea, cropX, cropY, cropW, cropH, nowMs);
        return true;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Reads exactly <paramref name="count"/> bytes or throws.</summary>
    private static void ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0) throw new IOException("Connection closed by remote host.");
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
