using UnityEngine;
using UnityEditor;

public class FixNetworkConfig
{
    [MenuItem("PBL5/Tools/Fix Network Configs in Scene")]
    public static void Fix()
    {
        string[] configGuids = AssetDatabase.FindAssets("t:NetworkConfig");
        if (configGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(configGuids[0]);
            NetworkConfig config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(path);
            
            var conditionController = Object.FindAnyObjectByType<ConditionController>();
            if (conditionController != null)
            {
                var so = new SerializedObject(conditionController);
                so.FindProperty("config").objectReferenceValue = config;
                so.ApplyModifiedProperties();
                Debug.Log("Fixed ConditionController NetworkConfig.");
            }
            
            var robotClient = Object.FindAnyObjectByType<RobotClient>();
            if (robotClient != null)
            {
                var so = new SerializedObject(robotClient);
                so.FindProperty("config").objectReferenceValue = config;
                so.ApplyModifiedProperties();
            }
            
            var receiver = Object.FindAnyObjectByType<CameraFeedReceiver>();
            if (receiver != null)
            {
                var so = new SerializedObject(receiver);
                so.FindProperty("config").objectReferenceValue = config;
                so.ApplyModifiedProperties();
            }
            
            var desktopHUD = Object.FindAnyObjectByType<DesktopHUD>();
            if (desktopHUD != null)
            {
                var so = new SerializedObject(desktopHUD);
                so.FindProperty("config").objectReferenceValue = config;
                so.ApplyModifiedProperties();
            }
        }
        
        var gazeProvider = Object.FindAnyObjectByType<GazeProvider>();
        var rawImages = Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None);
        UnityEngine.UI.RawImage feedDisplay = null;
        foreach (var ri in rawImages)
        {
            if (ri.gameObject.name == "FeedDisplay" || ri.gameObject.name == "FoveatedFeed")
            {
                feedDisplay = ri;
                break;
            }
        }

        if (gazeProvider != null)
        {
            var so = new SerializedObject(gazeProvider);
            so.FindProperty("gazeMode").enumValueIndex = 4; // OpenXR
            
            Camera mainCam = Object.FindAnyObjectByType<Camera>();
            if (mainCam != null)
            {
                so.FindProperty("vrCamera").objectReferenceValue = mainCam;
            }
            
            if (feedDisplay != null)
            {
                SerializedProperty feedPlanesProp = so.FindProperty("feedPlanes");
                feedPlanesProp.arraySize = 1;
                feedPlanesProp.GetArrayElementAtIndex(0).objectReferenceValue = feedDisplay.rectTransform;
            }

            so.ApplyModifiedProperties();
            Debug.Log("Fixed GazeProvider to use OpenXR and wired camera/planes.");

            var gazeUploader = gazeProvider.GetComponent<GazeUploader>();
            if (gazeUploader == null)
            {
                gazeUploader = gazeProvider.gameObject.AddComponent<GazeUploader>();
                var soUploader = new SerializedObject(gazeUploader);
                soUploader.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
                soUploader.ApplyModifiedProperties();
                Debug.Log("Added missing GazeUploader to transmit eye tracking data to the server.");
            }
        }


        if (feedDisplay != null)
        {
            string[] matGuids = AssetDatabase.FindAssets("FoveatedMaterial t:Material");
            if (matGuids.Length > 0)
            {
                string matPath = AssetDatabase.GUIDToAssetPath(matGuids[0]);
                Material fovMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                feedDisplay.material = fovMat;
            }

            var foveatedController = feedDisplay.GetComponent<FoveatedFeedController>();
            if (foveatedController == null)
            {
                foveatedController = feedDisplay.gameObject.AddComponent<FoveatedFeedController>();
            }

            if (gazeProvider != null)
            {
                var soFov = new SerializedObject(foveatedController);
                soFov.FindProperty("gazeProvider").objectReferenceValue = gazeProvider;
                soFov.ApplyModifiedProperties();
            }
            Debug.Log("Fixed FoveatedFeed shader and controller on FeedDisplay.");
        }
    }
}
