using UnityEngine;

/// <summary>
/// ScriptableObject storing robot network configuration.
/// Create via Assets → Create → Teleoperation → Network Config.
/// Keep sensitive IPs out of version control by .gitignore-ing the .asset file.
/// </summary>
[CreateAssetMenu(fileName = "NetworkConfig", menuName = "Teleoperation/Network Config")]
public class NetworkConfig : ScriptableObject
{
    [Header("Robot Control Connection (ESP32 UDP)")]
    [Tooltip("IP address of the ESP32 (assigned by router)")]
    public string esp32IP = "192.168.0.XXX";

    [Tooltip("UDP port for sending direct drive commands to ESP32")]
    public int esp32Port = 1234;

    [Header("Mini PC Video & Config Connection")]
    [Tooltip("IP address of the Mini PC for video and gaze")]
    public string robotIP = "192.168.0.208";

    [Tooltip("TCP port on Mini PC for receiving configuration commands ($CFG)")]
    public int controlPort = 1234;

    [Header("Camera Feed Connection")]
    [Tooltip("TCP port for receiving JPEG camera frames")]
    public int cameraPort = 1235;

    [Header("Gaze Upload Connection")]
    [Tooltip("TCP port for sending gaze UV coordinates to the server")]
    public int gazePort = 1236;

    [Header("Scene Frame Streaming Connection")]
    [Tooltip("TCP port for sending high-quality camera frames from Unity to the server")]
    public int sceneFramePort = 1237;

    [Header("Protocol Constants")]
    [Tooltip("Neutral turn value (straight ahead)")]
    public int neutralTurn = 90;

    [Tooltip("Neutral speed value (stopped)")]
    public int neutralSpeed = 256;

    [Tooltip("Maximum turn range (0 to this value)")]
    public int maxTurn = 180;

    [Tooltip("Maximum speed range (0 to this value)")]
    public int maxSpeed = 512;
}
