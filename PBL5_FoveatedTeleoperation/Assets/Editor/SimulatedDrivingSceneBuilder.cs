using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SimulatedDrivingSceneBuilder
{
    [MenuItem("PBL5/Build Simulated Driving Scene")]
    public static void BuildScene()
    {
        Debug.Log("[SceneBuilder] Starting Simulated Driving scene build...");

        // 1. Ensure directories exist
        EnsureFolderExists("Assets/Scenes");
        EnsureFolderExists("Assets/Materials");
        EnsureFolderExists("Assets/RenderTextures");

        // 2. Setup Tags
        RegisterTag("TrialStart");
        RegisterTag("TrialEnd");
        RegisterTag("Player");
        RegisterTag("Wall");

        // 3. Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 4. Create Materials
        Material floorMat = CreateMaterial("FloorMat", new Color(0.15f, 0.15f, 0.15f));
        Material wallMat = CreateMaterial("WallMat", new Color(0.7f, 0.75f, 0.8f));
        Material startMat = CreateMaterial("StartMat", new Color(0f, 0.5f, 1f, 0.35f), transparent: true);
        Material endMat = CreateMaterial("EndMat", new Color(0f, 1f, 0.3f, 0.35f), transparent: true);
        Material moverMat = CreateMaterial("MoverMat", new Color(1f, 0.3f, 0.1f));
        Material robotMat = CreateMaterial("RobotMat", new Color(0.2f, 0.6f, 1f));

        // 5. Setup Environment Lighting
        GameObject lightGO = new GameObject("Directional Light");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 6. Setup Ground Plane (50x50 units)
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5f, 1f, 5f); // 10 * 5 = 50 units
        ground.GetComponent<Renderer>().sharedMaterial = floorMat;

        // 7. Setup RenderTexture
        RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>("Assets/RenderTextures/RobotCam.renderTexture");
        if (rt == null)
        {
            rt = new RenderTexture(1280, 720, 24);
            AssetDatabase.CreateAsset(rt, "Assets/RenderTextures/RobotCam.renderTexture");
            Debug.Log("[SceneBuilder] Created RenderTexture at Assets/RenderTextures/RobotCam.renderTexture");
        }

        // 8. Setup Robot Capsule
        GameObject robot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        robot.name = "Robot";
        robot.tag = "Player";
        robot.transform.position = new Vector3(0f, 1f, -5f); // spawn at y=1 to sit on ground, z=-5
        robot.GetComponent<Renderer>().sharedMaterial = robotMat;

        // Add Rigidbody and freeze constraints
        Rigidbody rb = robot.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezePositionY | 
                         RigidbodyConstraints.FreezeRotationX | 
                         RigidbodyConstraints.FreezeRotationZ;

        // Add RobotController
        RobotController rc = robot.AddComponent<RobotController>();
        rc.moveSpeed = 3f;
        rc.turnSpeed = 90f;

        // Setup child camera at head height (local y=0.8, z=0.2)
        GameObject camGO = new GameObject("RobotCam");
        camGO.transform.SetParent(robot.transform);
        camGO.transform.localPosition = new Vector3(0f, 0.8f, 0.2f);
        camGO.transform.localRotation = Quaternion.identity;
        Camera cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.targetTexture = rt;

        // 9. Setup WorldRoot and Scenarios
        GameObject worldRoot = new GameObject("WorldRoot");
        ScenarioSelector selector = worldRoot.AddComponent<ScenarioSelector>();
        TrialMetricsLogger metricsLogger = worldRoot.AddComponent<TrialMetricsLogger>();
        metricsLogger.scenarioSelector = selector;
        metricsLogger.robotController = rc;

        // Scenario 1: Corridor
        GameObject scenarioCorridor = new GameObject("Scenario_Corridor");
        scenarioCorridor.transform.SetParent(worldRoot.transform);
        
        CreateWall(scenarioCorridor, "LeftWall", new Vector3(-1.05f, 1f, 10f), new Vector3(0.1f, 2f, 20f), wallMat);
        CreateWall(scenarioCorridor, "RightWall", new Vector3(1.05f, 1f, 10f), new Vector3(0.1f, 2f, 20f), wallMat);
        CreateTrigger(scenarioCorridor, "TrialStart", new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 0.5f), "TrialStart", startMat);
        CreateTrigger(scenarioCorridor, "TrialEnd", new Vector3(0f, 1f, 20f), new Vector3(2f, 2f, 0.5f), "TrialEnd", endMat);

        // Scenario 2: Doorway
        GameObject scenarioDoorway = new GameObject("Scenario_Doorway");
        scenarioDoorway.transform.SetParent(worldRoot.transform);

        CreateWall(scenarioDoorway, "PartitionLeft", new Vector3(-5.375f, 1f, 10f), new Vector3(9.25f, 2f, 0.1f), wallMat);
        CreateWall(scenarioDoorway, "PartitionRight", new Vector3(5.375f, 1f, 10f), new Vector3(9.25f, 2f, 0.1f), wallMat);
        CreateWall(scenarioDoorway, "CorridorWallLeft", new Vector3(-0.8f, 1f, 15f), new Vector3(0.1f, 2f, 10f), wallMat);
        CreateWall(scenarioDoorway, "CorridorWallRight", new Vector3(0.8f, 1f, 15f), new Vector3(0.1f, 2f, 10f), wallMat);
        CreateTrigger(scenarioDoorway, "TrialStart", new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 0.5f), "TrialStart", startMat);
        CreateTrigger(scenarioDoorway, "TrialEnd", new Vector3(0f, 1f, 20f), new Vector3(1.5f, 2f, 0.5f), "TrialEnd", endMat);

        // Scenario 3: Obstacle
        GameObject scenarioObstacle = new GameObject("Scenario_Obstacle");
        scenarioObstacle.transform.SetParent(worldRoot.transform);

        CreateTrigger(scenarioObstacle, "TrialStart", new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 0.5f), "TrialStart", startMat);
        CreateTrigger(scenarioObstacle, "TrialEnd", new Vector3(0f, 1f, 20f), new Vector3(4f, 2f, 0.5f), "TrialEnd", endMat);

        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "ObstacleCube";
        obstacle.tag = "Wall";
        obstacle.transform.SetParent(scenarioObstacle.transform);
        obstacle.transform.localPosition = new Vector3(0f, 0.5f, 10f); // 1m cube
        obstacle.transform.localScale = new Vector3(1f, 1f, 1f);
        obstacle.GetComponent<Renderer>().sharedMaterial = moverMat;
        Mover mover = obstacle.AddComponent<Mover>();
        mover.speed = 1f;
        mover.range = 4f;
        mover.startDelay = 5f;

        // Wire Selector GameObjects
        selector.scenarioCorridor = scenarioCorridor;
        selector.scenarioDoorway = scenarioDoorway;
        selector.scenarioObstacle = scenarioObstacle;
        selector.robotController = rc;

        // 10. Setup HUD Canvas
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvasComp = canvasGO.AddComponent<Canvas>();
        canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Setup HUD Camera (to prevent "No cameras rendering" warning on Display 1)
        GameObject hudCamGO = new GameObject("HUDCamera");
        Camera hudCam = hudCamGO.AddComponent<Camera>();
        hudCam.clearFlags = CameraClearFlags.SolidColor;
        hudCam.backgroundColor = Color.black;
        hudCam.cullingMask = 0; // Don't render anything in 3D
        hudCam.depth = -10; // Render behind UI overlay

        // Red Flash Overlay for Collisions
        GameObject flashGO = new GameObject("CollisionFlash");
        flashGO.transform.SetParent(canvasGO.transform, false);
        Image flashImage = flashGO.AddComponent<Image>();
        flashImage.color = new Color(1f, 0f, 0f, 0f); // transparent initially
        RectTransform flashRt = flashImage.GetComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.sizeDelta = Vector2.zero;

        // Attach CollisionReporter to Robot and link flash image
        CollisionReporter reporter = robot.AddComponent<CollisionReporter>();
        reporter.flashImage = flashImage;

        // Full screen RawImage
        GameObject rawImageGO = new GameObject("FeedImage");
        rawImageGO.transform.SetParent(canvasGO.transform, false);
        RawImage rawImage = rawImageGO.AddComponent<RawImage>();
        rawImage.texture = rt;
        
        RectTransform rawImgRt = rawImage.GetComponent<RectTransform>();
        rawImgRt.anchorMin = Vector2.zero;
        rawImgRt.anchorMax = Vector2.one;
        rawImgRt.sizeDelta = Vector2.zero;

        AspectRatioFitter fitter = rawImageGO.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = 16f / 9f;
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        // HUD Panel
        GameObject hudPanelGO = new GameObject("HUDPanel");
        hudPanelGO.transform.SetParent(canvasGO.transform, false);
        Image panelImage = hudPanelGO.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.75f); // 75% opacity dark panel

        RectTransform panelRt = hudPanelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(20f, -20f);
        panelRt.sizeDelta = new Vector2(320f, 200f);

        // Header and separator line
        CreateText(hudPanelGO, "Header", "SIMULATION STATS", -15f, 12, isBold: true);
        
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
        TextMeshProUGUI scenarioText = CreateText(hudPanelGO, "ScenarioText", "Scenario: Corridor", -45f, 16);
        TextMeshProUGUI conditionText = CreateText(hudPanelGO, "ConditionText", "Condition: Manual WASD", -75f, 16);
        TextMeshProUGUI trialText = CreateText(hudPanelGO, "TrialText", "Trial: #1", -105f, 16);
        TextMeshProUGUI timeText = CreateText(hudPanelGO, "TimeText", "Time: 0.00s (Ready)", -135f, 16);
        TextMeshProUGUI collisionText = CreateText(hudPanelGO, "CollisionText", "Collisions: 0", -165f, 16);

        // Bind HUD text references
        DrivingHUD hud = canvasGO.AddComponent<DrivingHUD>();
        SerializedObject hudSO = new SerializedObject(hud);
        hudSO.FindProperty("scenarioText").objectReferenceValue = scenarioText;
        hudSO.FindProperty("conditionText").objectReferenceValue = conditionText;
        hudSO.FindProperty("trialText").objectReferenceValue = trialText;
        hudSO.FindProperty("timeText").objectReferenceValue = timeText;
        hudSO.FindProperty("collisionText").objectReferenceValue = collisionText;
        hudSO.ApplyModifiedProperties();

        // 11. Save Scene
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SimulatedDriving.unity");
        Debug.Log("[SceneBuilder] Simulated Driving Scene setup complete! Saved to Assets/Scenes/SimulatedDriving.unity");
    }

    private static void EnsureFolderExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path);
            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void RegisterTag(string tag)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;
        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");
        bool found = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"[SceneBuilder] Registered Tag: {tag}");
        }
    }

    private static Material CreateMaterial(string name, Color color, bool transparent = false)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        
        mat.color = color;
        
        if (transparent)
        {
            if (mat.shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetFloat("_Surface", 1); // 1 is transparent
                mat.SetFloat("_Blend", 0); // 0 is alpha blend
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                mat.SetFloat("_Mode", 3); // 3 is transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
        }
        else
        {
            if (mat.shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetFloat("_Surface", 0); // 0 is opaque
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHATEMPLATE_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
            else
            {
                mat.SetFloat("_Mode", 0); // 0 is opaque
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = -1;
            }
        }
        
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static GameObject CreateWall(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.tag = "Wall";
        wall.transform.SetParent(parent.transform);
        wall.transform.localPosition = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = mat;
        return wall;
    }

    private static GameObject CreateTrigger(GameObject parent, string name, Vector3 pos, Vector3 scale, string tag, Material mat)
    {
        GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trigger.name = name;
        trigger.tag = tag;
        trigger.transform.SetParent(parent.transform);
        trigger.transform.localPosition = pos;
        trigger.transform.localScale = scale;
        
        trigger.GetComponent<Renderer>().sharedMaterial = mat;
        var col = trigger.GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (tag == "TrialStart" || name == "TrialStart")
        {
            trigger.AddComponent<TrialStartTrigger>();
        }
        else if (tag == "TrialEnd" || name == "TrialEnd")
        {
            trigger.AddComponent<TrialEndTrigger>();
        }

        return trigger;
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
