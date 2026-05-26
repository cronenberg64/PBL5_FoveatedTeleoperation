using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PBL5MenuItems
{
    [MenuItem("PBL5/Open XR Scene", false, 1)]
    public static void OpenXRScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }
    }

    [MenuItem("PBL5/Open Desktop Scene", false, 2)]
    public static void OpenDesktopScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene("Assets/Scenes/DesktopFoveated.unity", OpenSceneMode.Single);
        }
    }

    [MenuItem("PBL5/Open Simulated Driving", false, 3)]
    public static void OpenSimulatedDrivingScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SimulatedDriving.unity", OpenSceneMode.Single);
        }
    }
}
