using UnityEngine;
using TMPro;

public class DesktopHUD : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI gazeModeText;
    [SerializeField] private TextMeshProUGUI bandwidthText;
    [SerializeField] private TextMeshProUGUI latencyText;
    [SerializeField] private TextMeshProUGUI tobiiStatusText;

    [Header("Dependencies")]
    [SerializeField] private CameraFeedReceiver feedReceiver;
    [SerializeField] private GazeProvider gazeProvider;
    [SerializeField] private NetworkConfig config;

    private void Start()
    {
        if (feedReceiver == null)
        {
            feedReceiver = FindFirstObjectByType<CameraFeedReceiver>();
        }
        if (gazeProvider == null)
        {
            gazeProvider = FindFirstObjectByType<GazeProvider>();
        }
    }

    private void Update()
    {
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (feedReceiver != null)
        {
            if (statusText != null)
            {
                if (feedReceiver.IsConnected)
                {
                    string ip = config != null ? config.robotIP : "Unknown IP";
                    int port = config != null ? config.cameraPort : 1235;
                    statusText.text = $"Status: Connected to mock_pioneer at IP {ip}:{port}";
                }
                else
                {
                    statusText.text = "Status: Disconnected";
                }
            }

            if (bandwidthText != null)
            {
                float bps = feedReceiver.BandwidthBytesPerSec;
                if (bps >= 1024f * 1024f)
                {
                    bandwidthText.text = $"Bandwidth: {bps / (1024f * 1024f):F2} MB/s";
                }
                else if (bps >= 1024f)
                {
                    bandwidthText.text = $"Bandwidth: {bps / 1024f:F1} KB/s";
                }
                else
                {
                    bandwidthText.text = $"Bandwidth: {bps:F0} B/s";
                }
            }

            if (latencyText != null)
            {
                latencyText.text = $"Latency: {feedReceiver.LatencyMs:F1} ms";
            }
        }
        else
        {
            if (statusText != null) statusText.text = "Status: No Receiver Found";
            if (bandwidthText != null) bandwidthText.text = "Bandwidth: --";
            if (latencyText != null) latencyText.text = "Latency: --";
        }

        if (gazeProvider != null)
        {
            if (gazeModeText != null)
            {
                gazeModeText.text = $"Gaze Mode: {gazeProvider.ActiveGazeMode.ToString()}";
            }
        }
        else
        {
            if (gazeModeText != null) gazeModeText.text = "Gaze Mode: --";
        }

        if (tobiiStatusText != null)
        {
            if (gazeProvider != null)
            {
                bool available = gazeProvider.IsTobiiAvailable;
                tobiiStatusText.text = $"Tobii Available: {(available ? "<color=green>Yes</color>" : "<color=red>No</color>")}";
            }
            else
            {
                tobiiStatusText.text = "Tobii Available: <color=red>No</color>";
            }
        }
    }
}
