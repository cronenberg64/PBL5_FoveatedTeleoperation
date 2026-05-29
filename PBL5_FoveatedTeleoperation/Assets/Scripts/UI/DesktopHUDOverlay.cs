using UnityEngine;
using UnityEngine.UI;

public class DesktopHUDOverlay : MonoBehaviour
{
    public Text hudText;
    public CameraFeedReceiver receiver;
    public NetworkConfig config;
    public GazeProvider gazeProvider;
    
    private int _lastFrames;
    private float _timer;
    private int _framesThisSecond;
    private string _latencyMock = "< 10ms";

    void Update()
    {
        if (hudText == null || config == null || gazeProvider == null) return;
        
        string status = receiver != null && receiver.IsConnected ? "Connected" : "Disconnected";
        
        _timer += Time.unscaledDeltaTime;
        if (receiver != null)
        {
            if (receiver.FramesReceived > _lastFrames)
            {
                _framesThisSecond += (receiver.FramesReceived - _lastFrames);
                _lastFrames = receiver.FramesReceived;
            }
        }

        string bandwidth = "0";
        if (_timer >= 1f)
        {
            bandwidth = (_framesThisSecond * 45).ToString(); // Approx 45KB per frame
            _framesThisSecond = 0;
            _timer = 0f;
        }

        hudText.text = $"Status: {status} to mock_pioneer at IP {config.robotIP}\n" +
                       $"Gaze Mode: {gazeProvider.currentGazeMode}\n" +
                       $"Bandwidth: ~{bandwidth} KB/s\n" +
                       $"Latency: {_latencyMock}";
    }
}
