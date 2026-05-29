using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
        
        // Assign Material FoveatedFeed
        string[] matGuids = AssetDatabase.FindAssets("FoveatedFeed t:Material");
        if (matGuids.Length > 0)
        {
            Material foveatedMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(matGuids[0]));
            rawImage.material = foveatedMat;
        }
        else
        {
            Debug.LogWarning("FoveatedFeed material not found. You need to assign the material to the FeedImage manually.");
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

        // 6. HUD Overlay
        GameObject hudGO = new GameObject("HUDText");
        hudGO.transform.SetParent(canvasGO.transform, false);
        Text hudText = hudGO.AddComponent<Text>();
        hudText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        hudText.fontSize = 24;
        hudText.color = Color.green;
        hudText.alignment = TextAnchor.UpperLeft;
        
        RectTransform hudRt = hudGO.GetComponent<RectTransform>();
        hudRt.anchorMin = new Vector2(0, 1);
        hudRt.anchorMax = new Vector2(0, 1);
        hudRt.pivot = new Vector2(0, 1);
        hudRt.anchoredPosition = new Vector2(10, -10);
        hudRt.sizeDelta = new Vector2(600, 200);

        DesktopHUDOverlay hudOverlay = hudGO.AddComponent<DesktopHUDOverlay>();
        SerializedObject hudSO = new SerializedObject(hudOverlay);
        hudSO.FindProperty("hudText").objectReferenceValue = hudText;
        hudSO.FindProperty("receiver").objectReferenceValue = receiver;
        hudSO.FindProperty("config").objectReferenceValue = config;
        hudSO.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
        hudSO.ApplyModifiedProperties();

        // 7. Save Scene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/DesktopFoveated.unity");
        Debug.Log("Desktop Scene setup complete! Saved to Assets/Scenes/DesktopFoveated.unity");
    }
}
