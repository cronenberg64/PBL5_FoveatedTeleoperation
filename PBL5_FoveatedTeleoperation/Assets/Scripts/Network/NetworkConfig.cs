using UnityEngine;

/// <summary>
/// ScriptableObject storing robot network configuration.
/// Create via Assets → Create → Teleoperation → Network Config.
/// Keep sensitive IPs out of version control by .gitignore-ing the .asset file.
/// </summary>
[CreateAssetMenu(fileName = "NetworkConfig", menuName = "Teleoperation/Network Config")]
public class NetworkConfig : ScriptableObject
{
    [Header("Robot Control Connection")]
    [Tooltip("IP address of the Pioneer robot's control server")]
    public string robotIP = "192.168.0.244";

    [Tooltip("TCP port for sending drive commands")]
    public int controlPort = 1234;

    [Header("Camera Feed Connection")]
    [Tooltip("TCP port for receiving JPEG camera frames")]
    public int cameraPort = 1235;

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
