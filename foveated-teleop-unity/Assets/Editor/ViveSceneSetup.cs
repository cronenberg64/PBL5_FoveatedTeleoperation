using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
public class ViveSceneSetup
{
    [MenuItem("PBL5/Tools/Generate ViveFoveated Scene")]
    public static void GenerateScene()
    {
        // 1. Create New Scene
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        
        // 2. Try to instantiate XR Origin from menu
        bool xrCreated = EditorApplication.ExecuteMenuItem("GameObject/XR/XR Origin (VR)");
        if (!xrCreated)
        {
            Debug.LogError("Could not execute 'GameObject/XR/XR Origin (VR)'. Please ensure XR Interaction Toolkit is installed.");
            // Fallback basic setup
            GameObject originObj = new GameObject("XR Origin");
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObj.transform);
            GameObject mainCamera = new GameObject("Main Camera");
            mainCamera.transform.SetParent(cameraOffset.transform);
            mainCamera.AddComponent<Camera>();
            mainCamera.AddComponent<AudioListener>();
            mainCamera.tag = "MainCamera";
            
            GameObject leftHand = new GameObject("LeftHand Controller");
            leftHand.transform.SetParent(cameraOffset.transform);
            GameObject rightHand = new GameObject("RightHand Controller");
            rightHand.transform.SetParent(cameraOffset.transform);
        }

        // Wait a frame for XR Origin to be created if via menu? 
        // ExecuteMenuItem is immediate in some cases, delayed in others. 
        // We'll search for Main Camera
        Camera cam = Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
        }

        // 3. Create World Space Canvas
        GameObject canvasObj = new GameObject("Canvas");
        if (cam != null) canvasObj.transform.SetParent(cam.transform);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.layer = LayerMask.NameToLayer("UI");
        
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.localPosition = new Vector3(0, 0, 2.0f); // 2m in front
        canvasRect.localRotation = Quaternion.identity;
        // 2.3m x 1.3m
        canvasRect.sizeDelta = new Vector2(2.3f, 1.3f);
        canvasRect.localScale = Vector3.one;

        // 4. Raw Image
        GameObject rawImageObj = new GameObject("FoveatedFeed");
        rawImageObj.transform.SetParent(canvasObj.transform, false);
        rawImageObj.layer = LayerMask.NameToLayer("UI");
        RawImage rawImage = rawImageObj.AddComponent<RawImage>();
        RectTransform rawImageRect = rawImageObj.GetComponent<RectTransform>();
        rawImageRect.anchorMin = Vector2.zero;
        rawImage.rectTransform.anchorMax = Vector2.one;
        rawImage.rectTransform.sizeDelta = Vector2.zero;

        // Load FoveatedMaterial and assign it
        string[] matGuids = AssetDatabase.FindAssets("FoveatedMaterial t:Material");
        if (matGuids.Length > 0)
        {
            string matPath = AssetDatabase.GUIDToAssetPath(matGuids[0]);
            Material fovMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            rawImage.material = fovMat;
        }

        // Add FoveatedFeedController
        var foveatedController = rawImageObj.AddComponent<FoveatedFeedController>();
        rawImageRect.offsetMax = Vector2.zero;

        // 5. HUD child canvas
        GameObject hudObj = new GameObject("HUD");
        hudObj.transform.SetParent(canvasObj.transform, false);
        hudObj.layer = LayerMask.NameToLayer("UI");
        RectTransform hudRect = hudObj.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(1, 1);
        hudRect.anchorMax = new Vector2(1, 1);
        hudRect.pivot = new Vector2(1, 1);
        hudRect.anchoredPosition = new Vector2(-0.05f, -0.05f); // 5cm padding
        hudRect.sizeDelta = new Vector2(0.5f, 0.4f);

        // Add dummy text to HUD
        GameObject textObj = new GameObject("StatusText");
        textObj.transform.SetParent(hudObj.transform, false);
        textObj.layer = LayerMask.NameToLayer("UI");
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "Status: --\nGaze Mode: OpenXR\nBandwidth: --\nLatency: --";
        tmp.fontSize = 0.05f; // 0.05 world units
        tmp.alignment = TextAlignmentOptions.TopRight;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // 6. Network and Gaze setup
        GameObject receiverObj = new GameObject("Receiver");
        var receiver = receiverObj.AddComponent<CameraFeedReceiver>();

        GameObject gazeObj = new GameObject("GazeManager");
        var gazeProvider = gazeObj.AddComponent<GazeProvider>();
        // Wire OpenXR gaze mode, camera, and planes
        var soGaze = new SerializedObject(gazeProvider);
        soGaze.FindProperty("gazeMode").enumValueIndex = 4; // OpenXR
        
        Camera mainCam = Object.FindAnyObjectByType<Camera>();
        if (mainCam != null)
        {
            soGaze.FindProperty("vrCamera").objectReferenceValue = mainCam;
        }

        SerializedProperty feedPlanesProp = soGaze.FindProperty("feedPlanes");
        feedPlanesProp.arraySize = 1;
        feedPlanesProp.GetArrayElementAtIndex(0).objectReferenceValue = rawImage.rectTransform;

        soGaze.ApplyModifiedProperties();
        // Add MouseGazeSource
        gazeObj.AddComponent<MouseGazeSource>();

        // Add GazeUploader to send data to server
        var gazeUploader = gazeObj.AddComponent<GazeUploader>();
        var soUploader = new SerializedObject(gazeUploader);
        soUploader.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
        soUploader.ApplyModifiedProperties();

        // Wire GazeProvider to FoveatedFeedController
        var soFov = new SerializedObject(foveatedController);
        soFov.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
        soFov.ApplyModifiedProperties();

        GameObject inputObj = new GameObject("InputManager");
        // Position capsule slightly in front of origin at floor level
        inputObj.transform.position = new Vector3(0f, 0.5f, -3f);
        // Add Rigidbody so RobotController.FixedUpdate can apply velocity
        var rb = inputObj.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezePositionY
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationZ;
        // Add RobotController
        inputObj.AddComponent<RobotController>();
        // Add ConditionController
        inputObj.AddComponent<ConditionController>();
        // Add RobotClient
        inputObj.AddComponent<RobotClient>();
        // Add NetworkConfig (find existing asset)
        string[] configGuids = AssetDatabase.FindAssets("t:NetworkConfig");
        if (configGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(configGuids[0]);
            NetworkConfig config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(path);
            
            // Wire it
            var so = new SerializedObject(receiver);
            so.FindProperty("feedDisplay").objectReferenceValue = rawImage;
            so.FindProperty("dualPayloadMode").boolValue = true;
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedProperties();

            var so2 = new SerializedObject(inputObj.GetComponent<RobotClient>());
            if (so2.targetObject != null)
            {
                so2.FindProperty("config").objectReferenceValue = config;
                so2.ApplyModifiedProperties();
            }

            var so3 = new SerializedObject(inputObj.GetComponent<ConditionController>());
            if (so3.targetObject != null)
            {
                so3.FindProperty("config").objectReferenceValue = config;
                so3.ApplyModifiedProperties();
            }
        }

        // Add DesktopHUD to GazeManager to display status
        var hudComponent = gazeObj.AddComponent<DesktopHUD>();
        var hudSO = new SerializedObject(hudComponent);
        hudSO.FindProperty("statusText").objectReferenceValue = tmp;
        hudSO.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
        hudSO.FindProperty("feedReceiver").objectReferenceValue = receiver;
        hudSO.ApplyModifiedProperties();

        // Add TrialMetricsLogger singleton
        GameObject trialLoggerObj = new GameObject("TrialMetricsLogger");
        trialLoggerObj.AddComponent<TrialMetricsLogger>();

        // 7. Save Scene
        string scenePath = "Assets/Scenes/ViveFoveated.unity";
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(newScene, scenePath);
        Debug.Log("Generated ViveFoveated scene at " + scenePath);
    }
}
#endif
