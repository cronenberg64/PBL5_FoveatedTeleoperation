using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class DesktopSceneSetup
{
    [MenuItem("Teleoperation/Setup Desktop Scene")]
    public static void CreateScene()
    {
        // 1. Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        // Camera
        GameObject camGO = new GameObject("Main Camera");
        Camera cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";
        
        // 2. Canvas
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // RawImage
        GameObject rawImageGO = new GameObject("FeedImage");
        rawImageGO.transform.SetParent(canvasGO.transform, false);
        RawImage rawImage = rawImageGO.AddComponent<RawImage>();
        RectTransform rt = rawImage.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        FoveatedFeedController ffc = rawImageGO.AddComponent<FoveatedFeedController>();
        
        // Assign Material FoveatedMaterial
        string[] matGuids = AssetDatabase.FindAssets("FoveatedMaterial t:Material");
        if (matGuids.Length > 0)
        {
            Material foveatedMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(matGuids[0]));
            rawImage.material = foveatedMat;
        }
        else
        {
            Debug.LogWarning("FoveatedMaterial not found. You need to assign the material to the FeedImage manually.");
        }
        
        // Find Config
        string[] configGuids = AssetDatabase.FindAssets("t:NetworkConfig");
        NetworkConfig config = null;
        if (configGuids.Length > 0)
        {
            config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(AssetDatabase.GUIDToAssetPath(configGuids[0]));
        }
        else
        {
            Debug.LogError("NetworkConfig not found! Run setup after creating NetworkConfig.");
        }

        // 3. Receiver
        GameObject receiverGO = new GameObject("FeedReceiver");
        CameraFeedReceiver receiver = receiverGO.AddComponent<CameraFeedReceiver>();
        SerializedObject receiverSO = new SerializedObject(receiver);
        receiverSO.FindProperty("feedDisplay").objectReferenceValue = rawImage;
        receiverSO.FindProperty("config").objectReferenceValue = config;
        receiverSO.FindProperty("dualPayloadMode").boolValue = true;
        receiverSO.ApplyModifiedProperties();

        // 4. Gaze
        GameObject gazeGO = new GameObject("GazeManager");
        GazeProvider gazeProvider = gazeGO.AddComponent<GazeProvider>();
        MouseGazeSource mouseSource = gazeGO.AddComponent<MouseGazeSource>();
        SerializedObject gazeProviderSO = new SerializedObject(gazeProvider);
        gazeProviderSO.FindProperty("vrCamera").objectReferenceValue = cam;
        
        SerializedProperty feedPlanesProp = gazeProviderSO.FindProperty("feedPlanes");
        feedPlanesProp.arraySize = 1;
        feedPlanesProp.GetArrayElementAtIndex(0).objectReferenceValue = rt;
        gazeProviderSO.ApplyModifiedProperties();
        
        SerializedObject mouseSourceSO = new SerializedObject(mouseSource);
        mouseSourceSO.FindProperty("useMouseProxy").boolValue = true;
        mouseSourceSO.ApplyModifiedProperties();

        SerializedObject ffcSO = new SerializedObject(ffc);
        ffcSO.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
        ffcSO.ApplyModifiedProperties();

        // 5. Input
        GameObject inputGO = new GameObject("InputManager");
        RobotClient robotClient = inputGO.AddComponent<RobotClient>();
        SerializedObject rcSO = new SerializedObject(robotClient);
        rcSO.FindProperty("config").objectReferenceValue = config;
        rcSO.ApplyModifiedProperties();

        TeleoperationController teleController = inputGO.AddComponent<TeleoperationController>();
        string[] iaGuids = AssetDatabase.FindAssets("t:InputActionAsset");
        InputActionAsset actions = null;
        foreach (var guid in iaGuids)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name.Contains("Teleoperation"))
            {
                actions = asset;
                break;
            }
        }
        
        SerializedObject tcSO = new SerializedObject(teleController);
        tcSO.FindProperty("robotClient").objectReferenceValue = robotClient;
        tcSO.FindProperty("config").objectReferenceValue = config;
        tcSO.FindProperty("inputActions").objectReferenceValue = actions;
        tcSO.ApplyModifiedProperties();

        // AspectRatioFitter (similar to SimulatedDriving for correct aspect ratio handling)
        AspectRatioFitter fitter = rawImageGO.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = 16f / 9f;
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        // 6. HUD Panel Overlay
        GameObject hudPanelGO = new GameObject("HUDPanel");
        hudPanelGO.transform.SetParent(canvasGO.transform, false);
        Image panelImage = hudPanelGO.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.75f); // 75% opacity dark panel

        RectTransform panelRt = hudPanelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(20f, -20f);
        panelRt.sizeDelta = new Vector2(400f, 160f);

        // Header and separator line
        CreateText(hudPanelGO, "Header", "TELEOPERATION STATS", -15f, 12, isBold: true);
        
        GameObject lineGO = new GameObject("SeparatorLine");
        lineGO.transform.SetParent(hudPanelGO.transform, false);
        Image lineImg = lineGO.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.2f);
        RectTransform lineRt = lineImg.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 1f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.pivot = new Vector2(0f, 1f);
        lineRt.anchoredPosition = new Vector2(15f, -35f);
        lineRt.sizeDelta = new Vector2(-30f, 1f);

        // Text elements
        TextMeshProUGUI statusText = CreateText(hudPanelGO, "StatusText", "Status: Disconnected", -45f, 14);
        TextMeshProUGUI gazeModeText = CreateText(hudPanelGO, "GazeModeText", "Gaze Mode: Mouse", -70f, 14);
        TextMeshProUGUI bandwidthText = CreateText(hudPanelGO, "BandwidthText", "Bandwidth: 0 B/s", -95f, 14);
        TextMeshProUGUI latencyText = CreateText(hudPanelGO, "LatencyText", "Latency: 0.0 ms", -120f, 14);

        // Bind HUD
        DesktopHUD hud = canvasGO.AddComponent<DesktopHUD>();
        SerializedObject hudSO = new SerializedObject(hud);
        hudSO.FindProperty("statusText").objectReferenceValue = statusText;
        hudSO.FindProperty("gazeModeText").objectReferenceValue = gazeModeText;
        hudSO.FindProperty("bandwidthText").objectReferenceValue = bandwidthText;
        hudSO.FindProperty("latencyText").objectReferenceValue = latencyText;
        hudSO.FindProperty("feedReceiver").objectReferenceValue = receiver;
        hudSO.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
        hudSO.FindProperty("config").objectReferenceValue = config;
        hudSO.ApplyModifiedProperties();

        // 7. Save Scene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DesktopFoveated.unity");
        Debug.Log("Desktop Scene setup complete! Saved to Assets/Scenes/DesktopFoveated.unity");
    }

    private static TextMeshProUGUI CreateText(GameObject parent, string name, string defaultText, float yOffset, int fontSize, bool isBold = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        if (isBold)
        {
            tmp.fontStyle = FontStyles.Bold;
        }
        
        RectTransform rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(15f, yOffset);
        rt.sizeDelta = new Vector2(-30f, 30f);
        
        return tmp;
    }
}
