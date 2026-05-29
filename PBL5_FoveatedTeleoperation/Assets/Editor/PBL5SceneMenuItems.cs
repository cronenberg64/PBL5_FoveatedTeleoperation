using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PBL5SceneMenuItems
{
    [MenuItem("PBL5/Open XR Scene")]
    public static void OpenXRScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
    }

    [MenuItem("PBL5/Open Desktop Scene")]
    public static void OpenDesktopScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DesktopFoveated.unity");
    }

    [MenuItem("PBL5/Open Simulated Driving")]
    public static void OpenSimulatedDriving()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SimulatedDriving.unity");
    }
}
