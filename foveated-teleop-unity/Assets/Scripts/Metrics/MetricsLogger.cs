using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Singleton CSV logger for experiment metrics.
///
/// Writes to Application.persistentDataPath/foveated_drivability_&lt;timestamp&gt;.csv.
/// Columns: t_unix_ms, event_type, value_a, value_b, notes
///
/// Usage: MetricsLogger.Instance?.Log("frame_received", bytes, latencyMs, "");
/// </summary>
public class MetricsLogger : MonoBehaviour
{
    // ─── Singleton ──────────────────────────────────────────────
    public static MetricsLogger Instance { get; private set; }

    // ─── Internals ───────────────────────────────────────────────
    private StreamWriter writer;
    private readonly object fileLock = new object();
    private float lastFlushTime;
    private const float FlushIntervalSec = 1f;

    private const string Header = "t_unix_ms,event_type,value_a,value_b,notes";

    // ─── Lifecycle ───────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        OpenLog();
    }

    private void Update()
    {
        if (Time.time - lastFlushTime >= FlushIntervalSec)
        {
            lastFlushTime = Time.time;
            Flush();
        }
    }

    private void OnDestroy()
    {
        Flush();
        lock (fileLock)
        {
            writer?.Close();
            writer = null;
        }
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    // ─── Public API ──────────────────────────────────────────────

    /// <summary>
    /// Append one row to the session CSV.
    /// Thread-safe — may be called from background threads.
    /// </summary>
    /// <param name="eventType">Short identifier, e.g. "frame_received".</param>
    /// <param name="a">Primary numeric payload (bytes, Hz, cmd code, etc.).</param>
    /// <param name="b">Secondary numeric payload.</param>
    /// <param name="notes">Free-form string (no commas).</param>
    public void Log(string eventType, float a, float b, string notes)
    {
        long tMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string row = $"{tMs},{EscapeCsv(eventType)},{a:F3},{b:F3},{EscapeCsv(notes)}";
        lock (fileLock)
        {
            try
            {
                writer?.WriteLine(row);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MetricsLogger] Write failed: {ex.Message}");
            }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private void OpenLog()
    {
        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path = Path.Combine(Application.persistentDataPath, $"foveated_drivability_{ts}.csv");

        try
        {
            writer = new StreamWriter(path, append: false, Encoding.UTF8, bufferSize: 4096);
            writer.WriteLine(Header);
            Debug.Log($"[MetricsLogger] Logging to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MetricsLogger] Cannot open log file at {path}: {ex.Message}");
        }
    }

    private void Flush()
    {
        lock (fileLock)
        {
            try { writer?.Flush(); } catch { }
        }
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Contains(',') ? $"\"{s}\"" : s;
    }
}
