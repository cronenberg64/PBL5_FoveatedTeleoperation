using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Editor helper: Foveated → Setup Scene
///
/// Populates the active scene with all GameObjects and wired references needed
/// for a smoke-testable foveated-teleoperation session.  Safe to run multiple
/// times — existing objects are found rather than duplicated.
/// </summary>
public static class SceneSetup
{
    private const string FoveatedMaterialGuid = "460baac647be2c04eb27ed0139180922";

    [MenuItem("Foveated/Setup Scene")]
    public static void SetupScene()
    {
        int created = 0;
        int wired = 0;

        // ── 1. Find the XR Origin camera ──────────────────────────────────────

        // Unity XR Interaction Toolkit names it "XR Origin (VR)" or "XR Origin"
        GameObject xrOrigin = GameObject.Find("XR Origin (VR)")
                           ?? GameObject.Find("XR Origin");

        Camera vrCamera = null;
        Transform cameraOffset = null;

        if (xrOrigin != null)
        {
            // Camera Offset → Main Camera is the standard XRIT hierarchy
            cameraOffset = xrOrigin.transform.Find("Camera Offset");
            if (cameraOffset == null)
                cameraOffset = xrOrigin.transform;

            var camGO = cameraOffset.Find("Main Camera");
            if (camGO != null)
                vrCamera = camGO.GetComponent<Camera>();
            if (vrCamera == null)
                vrCamera = xrOrigin.GetComponentInChildren<Camera>();
        }

        if (vrCamera == null)
        {
            // Fallback: use the scene's main camera
            vrCamera = Camera.main;
            Debug.LogWarning("[SceneSetup] Could not locate XR Origin camera — using Camera.main.");
        }

        // ── 2. Load the FoveatedMaterial ──────────────────────────────────────

        string matPath = AssetDatabase.GUIDToAssetPath(FoveatedMaterialGuid);
        Material foveatedMaterial = null;
        if (!string.IsNullOrEmpty(matPath))
        {
            foveatedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        if (foveatedMaterial == null)
        {
            Debug.LogWarning(
                $"[SceneSetup] FoveatedMaterial not found at GUID {FoveatedMaterialGuid}. " +
                "RawImage material will not be assigned — assign manually.");
        }

        // ── 3. Load the NetworkConfig ScriptableObject ────────────────────────

        NetworkConfig networkConfig = null;
        string[] configGuids = AssetDatabase.FindAssets("t:NetworkConfig");
        if (configGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(configGuids[0]);
            networkConfig = AssetDatabase.LoadAssetAtPath<NetworkConfig>(path);
        }

        if (networkConfig == null)
        {
            Debug.LogWarning(
                "[SceneSetup] No NetworkConfig asset found. " +
                "Create one via Assets → Create → Teleoperation → Network Config and re-run Setup Scene.");
        }

        // ── 4. Load TeleoperationInput action asset ────────────────────────────

        UnityEngine.InputSystem.InputActionAsset inputActions = null;
        string[] inputGuids = AssetDatabase.FindAssets("t:InputActionAsset TeleoperationInput");
        if (inputGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(inputGuids[0]);
            inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
        }

        // ── 5. FeedCanvas (world-space, child of VR camera) ───────────────────

        Transform feedCanvasParent = vrCamera != null ? vrCamera.transform : null;
        GameObject feedCanvasGO = feedCanvasParent != null
            ? feedCanvasParent.Find("FeedCanvas")?.gameObject
            : GameObject.Find("FeedCanvas");

        if (feedCanvasGO == null)
        {
            feedCanvasGO = new GameObject("FeedCanvas");
            if (feedCanvasParent != null)
                feedCanvasGO.transform.SetParent(feedCanvasParent, worldPositionStays: false);

            // Position 2 m in front of the camera, 1.6 m wide
            feedCanvasGO.transform.localPosition = new Vector3(0f, 0f, 2f);
            feedCanvasGO.transform.localRotation = Quaternion.identity;

            Canvas canvas = feedCanvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rt = feedCanvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1.6f, 0.9f);
            rt.localScale = Vector3.one;

            feedCanvasGO.AddComponent<CanvasScaler>();
            feedCanvasGO.AddComponent<GraphicRaycaster>();

            created++;
            Debug.Log("[SceneSetup] Created FeedCanvas.");
        }

        RectTransform feedCanvasRect = feedCanvasGO.GetComponent<RectTransform>();

        // ── 6. FeedImage (RawImage child of FeedCanvas) ────────────────────────

        Transform feedImageTransform = feedCanvasGO.transform.Find("FeedImage");
        GameObject feedImageGO;

        if (feedImageTransform == null)
        {
            feedImageGO = new GameObject("FeedImage");
            feedImageGO.transform.SetParent(feedCanvasGO.transform, worldPositionStays: false);
            created++;
            Debug.Log("[SceneSetup] Created FeedImage.");
        }
        else
        {
            feedImageGO = feedImageTransform.gameObject;
        }

        RectTransform feedRect = feedImageGO.GetComponent<RectTransform>();
        if (feedRect == null)
            feedRect = feedImageGO.AddComponent<RectTransform>();

        feedRect.anchorMin = Vector2.zero;
        feedRect.anchorMax = Vector2.one;
        feedRect.offsetMin = Vector2.zero;
        feedRect.offsetMax = Vector2.zero;

        RawImage rawImage = feedImageGO.GetComponent<RawImage>();
        if (rawImage == null)
            rawImage = feedImageGO.AddComponent<RawImage>();

        if (foveatedMaterial != null && rawImage.material != foveatedMaterial)
        {
            rawImage.material = foveatedMaterial;
            wired++;
        }

        // ── 7. Gaze-related components on FeedImage ────────────────────────────

        FoveatedFeedController ffc = feedImageGO.GetComponent<FoveatedFeedController>();
        if (ffc == null)
        {
            ffc = feedImageGO.AddComponent<FoveatedFeedController>();
            created++;
        }

        GazeDebugDot debugDot = feedImageGO.GetComponent<GazeDebugDot>();
        if (debugDot == null)
        {
            debugDot = feedImageGO.AddComponent<GazeDebugDot>();
            created++;
        }

        CameraFeedReceiver cfr = feedImageGO.GetComponent<CameraFeedReceiver>();
        if (cfr == null)
        {
            cfr = feedImageGO.AddComponent<CameraFeedReceiver>();
            created++;
        }

        // ── 8. GazeProvider on the VR camera ──────────────────────────────────

        GameObject gazePoviderGO = vrCamera != null ? vrCamera.gameObject : feedCanvasGO;
        GazeProvider gazeProvider = gazePoviderGO.GetComponent<GazeProvider>();
        if (gazeProvider == null)
        {
            gazeProvider = gazePoviderGO.AddComponent<GazeProvider>();
            created++;
            Debug.Log("[SceneSetup] Added GazeProvider to camera.");
        }

        // ── 9. Wire GazeProvider references ────────────────────────────────────

        SerializedObject soGaze = new SerializedObject(gazeProvider);
        var vrCamProp = soGaze.FindProperty("vrCamera");
        if (vrCamProp != null && vrCamera != null)
        {
            vrCamProp.objectReferenceValue = vrCamera;
            wired++;
        }

        // Add FeedCanvas rect to feedPlanes array
        var planesProp = soGaze.FindProperty("feedPlanes");
        if (planesProp != null)
        {
            bool alreadyContains = false;
            for (int i = 0; i < planesProp.arraySize; i++)
            {
                if (planesProp.GetArrayElementAtIndex(i).objectReferenceValue == feedRect)
                {
                    alreadyContains = true;
                    break;
                }
            }
            if (!alreadyContains)
            {
                planesProp.arraySize++;
                planesProp.GetArrayElementAtIndex(planesProp.arraySize - 1).objectReferenceValue = feedRect;
                wired++;
            }
        }
        soGaze.ApplyModifiedProperties();

        // ── 10. Wire FoveatedFeedController ──────────────────────────────────

        SerializedObject soFFC = new SerializedObject(ffc);
        var gazeProviderProp = soFFC.FindProperty("gazeProvider");
        if (gazeProviderProp != null)
        {
            gazeProviderProp.objectReferenceValue = gazeProvider;
            wired++;
        }
        soFFC.ApplyModifiedProperties();

        // ── 11. Wire GazeDebugDot ─────────────────────────────────────────────

        SerializedObject soDot = new SerializedObject(debugDot);
        var dotGazeProp = soDot.FindProperty("gazeProvider");
        if (dotGazeProp != null)
        {
            dotGazeProp.objectReferenceValue = gazeProvider;
            wired++;
        }
        var dotPlaneProp = soDot.FindProperty("feedPlane");
        if (dotPlaneProp != null)
        {
            dotPlaneProp.objectReferenceValue = feedRect;
            wired++;
        }
        soDot.ApplyModifiedProperties();

        // ── 12. Wire CameraFeedReceiver ───────────────────────────────────────

        SerializedObject soCFR = new SerializedObject(cfr);
        var cfrConfigProp = soCFR.FindProperty("config");
        if (cfrConfigProp != null && networkConfig != null)
        {
            cfrConfigProp.objectReferenceValue = networkConfig;
            wired++;
        }
        var cfrDisplayProp = soCFR.FindProperty("feedDisplay");
        if (cfrDisplayProp != null)
        {
            cfrDisplayProp.objectReferenceValue = rawImage;
            wired++;
        }
        soCFR.ApplyModifiedProperties();

        // ── 13. RobotManager root ─────────────────────────────────────────────

        GameObject robotManager = GameObject.Find("RobotManager");
        if (robotManager == null)
        {
            robotManager = new GameObject("RobotManager");
            created++;
            Debug.Log("[SceneSetup] Created RobotManager.");
        }

        // ── 14. Add components to RobotManager ────────────────────────────────

        RobotClient robotClient = robotManager.GetComponent<RobotClient>();
        if (robotClient == null)
        {
            robotClient = robotManager.AddComponent<RobotClient>();
            created++;
        }

        TeleoperationController teleop = robotManager.GetComponent<TeleoperationController>();
        if (teleop == null)
        {
            teleop = robotManager.AddComponent<TeleoperationController>();
            created++;
        }

        GazeUploader gazeUploader = robotManager.GetComponent<GazeUploader>();
        if (gazeUploader == null)
        {
            gazeUploader = robotManager.AddComponent<GazeUploader>();
            created++;
        }

        MetricsLogger metricsLogger = robotManager.GetComponent<MetricsLogger>();
        if (metricsLogger == null)
        {
            metricsLogger = robotManager.AddComponent<MetricsLogger>();
            created++;
        }

        // ── 15. HUD canvas ─────────────────────────────────────────────────────

        GameObject hudGO = vrCamera != null
            ? vrCamera.transform.Find("HUDCanvas")?.gameObject
            : GameObject.Find("HUDCanvas");

        if (hudGO == null)
        {
            hudGO = new GameObject("HUDCanvas");
            if (vrCamera != null)
                hudGO.transform.SetParent(vrCamera.transform, worldPositionStays: false);

            // Top-left corner: -0.7 m left, +0.35 m up, 2 m out
            hudGO.transform.localPosition  = new Vector3(-0.7f, 0.35f, 2f);
            hudGO.transform.localRotation  = Quaternion.identity;

            Canvas hudCanvas = hudGO.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.WorldSpace;

            RectTransform hudRt = hudGO.GetComponent<RectTransform>();
            hudRt.sizeDelta   = new Vector2(0.6f, 0.25f);
            hudRt.localScale  = Vector3.one;

            hudGO.AddComponent<CanvasScaler>();
            hudGO.AddComponent<GraphicRaycaster>();

            // Four TMP labels stacked vertically
            string[] labelNames = { "TaskLabel", "QualityLabel", "TimerLabel", "MistakeLabel" };
            for (int i = 0; i < labelNames.Length; i++)
            {
                GameObject labelGO = new GameObject(labelNames[i]);
                labelGO.transform.SetParent(hudGO.transform, worldPositionStays: false);

                RectTransform lrt = labelGO.AddComponent<RectTransform>();
                lrt.anchorMin  = new Vector2(0f, 1f - (i + 1) * 0.25f);
                lrt.anchorMax  = new Vector2(1f, 1f - i * 0.25f);
                lrt.offsetMin  = Vector2.zero;
                lrt.offsetMax  = Vector2.zero;

                TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
                tmp.fontSize  = 0.03f;
                tmp.color     = Color.white;
                tmp.text      = labelNames[i];
            }

            created++;
            Debug.Log("[SceneSetup] Created HUDCanvas.");
        }

        ExperimenterHUD hudComp = hudGO.GetComponent<ExperimenterHUD>();
        if (hudComp == null)
        {
            hudComp = hudGO.AddComponent<ExperimenterHUD>();
            created++;
        }

        ExperimenterInput hudInput = hudGO.GetComponent<ExperimenterInput>();
        if (hudInput == null)
        {
            hudInput = hudGO.AddComponent<ExperimenterInput>();
            created++;
        }

        // ── 16. Wire HUD label references ─────────────────────────────────────

        SerializedObject soHUD = new SerializedObject(hudComp);
        var textProps = new[] { "taskLabel", "qualityLabel", "timerLabel", "mistakeLabel" };
        var labelNames2 = new[] { "TaskLabel", "QualityLabel", "TimerLabel", "MistakeLabel" };
        for (int i = 0; i < textProps.Length; i++)
        {
            var prop = soHUD.FindProperty(textProps[i]);
            if (prop != null)
            {
                Transform lt = hudGO.transform.Find(labelNames2[i]);
                if (lt != null)
                {
                    prop.objectReferenceValue = lt.GetComponent<TextMeshProUGUI>();
                    wired++;
                }
            }
        }
        soHUD.ApplyModifiedProperties();

        // ── 17. Wire ExperimenterInput → HUD ──────────────────────────────────

        SerializedObject soInput = new SerializedObject(hudInput);
        var hudProp = soInput.FindProperty("hud");
        if (hudProp != null)
        {
            hudProp.objectReferenceValue = hudComp;
            wired++;
        }
        soInput.ApplyModifiedProperties();

        // ── 18. Wire RobotClient ──────────────────────────────────────────────

        SerializedObject soRC = new SerializedObject(robotClient);
        var rcConfigProp = soRC.FindProperty("config");
        if (rcConfigProp != null && networkConfig != null)
        {
            rcConfigProp.objectReferenceValue = networkConfig;
            wired++;
        }
        soRC.ApplyModifiedProperties();

        // ── 19. Wire TeleoperationController ─────────────────────────────────

        SerializedObject soTeleop = new SerializedObject(teleop);
        var teleopClientProp   = soTeleop.FindProperty("robotClient");
        var teleopConfigProp   = soTeleop.FindProperty("config");
        var teleopInputProp    = soTeleop.FindProperty("inputActions");
        if (teleopClientProp != null)  { teleopClientProp.objectReferenceValue = robotClient; wired++; }
        if (teleopConfigProp != null && networkConfig != null)
                                       { teleopConfigProp.objectReferenceValue = networkConfig; wired++; }
        if (teleopInputProp != null && inputActions != null)
                                       { teleopInputProp.objectReferenceValue = inputActions; wired++; }
        soTeleop.ApplyModifiedProperties();

        // ── 20. Wire GazeUploader ─────────────────────────────────────────────

        SerializedObject soGU = new SerializedObject(gazeUploader);
        var guConfigProp = soGU.FindProperty("config");
        var guGazeProp   = soGU.FindProperty("gazeProvider");
        if (guConfigProp != null && networkConfig != null)
                                       { guConfigProp.objectReferenceValue = networkConfig; wired++; }
        if (guGazeProp != null)        { guGazeProp.objectReferenceValue = gazeProvider; wired++; }
        soGU.ApplyModifiedProperties();

        // ── Summary ───────────────────────────────────────────────────────────

        EditorUtility.SetDirty(feedCanvasGO);
        EditorUtility.SetDirty(robotManager);
        EditorUtility.SetDirty(hudGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[SceneSetup] Done — {created} objects created, {wired} references wired.");
        Debug.Log(
            "[SceneSetup] Next steps:\n" +
            "  1. Assign a NetworkConfig asset to all network scripts if not auto-detected.\n" +
            "  2. Start 'python server.py --mode uniform --quality 20' in mock_pioneer/.\n" +
            "  3. Press Play — webcam feed should appear on the FeedCanvas plane.");
    }
}
