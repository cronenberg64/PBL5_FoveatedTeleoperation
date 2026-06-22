using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OneShotMerger : EditorWindow
{
    [MenuItem("PBL5/Tools/Merge Scenarios to ViveFoveated")]
    public static void Merge()
    {
        string simPath = "Assets/Scenes/SimulatedDriving.unity";
        string vivePath = "Assets/Scenes/ViveFoveated.unity";

        // Open ViveFoveated as main scene
        Scene viveScene = EditorSceneManager.OpenScene(vivePath, OpenSceneMode.Single);
        
        // Open SimulatedDriving additively
        Scene simScene = EditorSceneManager.OpenScene(simPath, OpenSceneMode.Additive);

        // Find objects in SimulatedDriving
        GameObject[] simRoots = simScene.GetRootGameObjects();
        GameObject scenarioCorridor = null;
        GameObject scenarioDoorway = null;
        GameObject scenarioObstacle = null;
        GameObject scenarioSelectorObj = null;

        foreach (var obj in simRoots)
        {
            if (obj.name == "Scenario_Corridor") scenarioCorridor = obj;
            if (obj.name == "Scenario_Doorway") scenarioDoorway = obj;
            if (obj.name == "Scenario_Obstacle") scenarioObstacle = obj;
            if (obj.name == "ScenarioSelector") scenarioSelectorObj = obj;
        }

        // Copy them to ViveFoveated
        if (scenarioCorridor != null) SceneManager.MoveGameObjectToScene(Instantiate(scenarioCorridor), viveScene);
        if (scenarioDoorway != null) SceneManager.MoveGameObjectToScene(Instantiate(scenarioDoorway), viveScene);
        if (scenarioObstacle != null) SceneManager.MoveGameObjectToScene(Instantiate(scenarioObstacle), viveScene);
        
        GameObject newSelectorObj = null;
        if (scenarioSelectorObj != null)
        {
            newSelectorObj = Instantiate(scenarioSelectorObj);
            SceneManager.MoveGameObjectToScene(newSelectorObj, viveScene);
        }

        // Close SimulatedDriving
        EditorSceneManager.CloseScene(simScene, true);

        // Now find the new objects in ViveFoveated to wire them up
        GameObject[] viveRoots = viveScene.GetRootGameObjects();
        GameObject newCorridor = null;
        GameObject newDoorway = null;
        GameObject newObstacle = null;
        ScenarioSelector selector = null;
        TrialMetricsLogger logger = null;
        RobotController robotController = null;

        foreach (var obj in viveRoots)
        {
            if (obj.name.Contains("Scenario_Corridor")) { newCorridor = obj; newCorridor.name = "Scenario_Corridor"; }
            if (obj.name.Contains("Scenario_Doorway")) { newDoorway = obj; newDoorway.name = "Scenario_Doorway"; }
            if (obj.name.Contains("Scenario_Obstacle")) { newObstacle = obj; newObstacle.name = "Scenario_Obstacle"; }
            if (obj.name.Contains("ScenarioSelector")) { selector = obj.GetComponent<ScenarioSelector>(); obj.name = "ScenarioSelector"; }
            if (obj.name.Contains("TrialMetricsLogger")) { logger = obj.GetComponent<TrialMetricsLogger>(); }
            if (obj.name.Contains("InputManager")) { robotController = obj.GetComponent<RobotController>(); }
        }

        // Wire up ScenarioSelector
        if (selector != null)
        {
            selector.scenarioCorridor = newCorridor;
            selector.scenarioDoorway = newDoorway;
            selector.scenarioObstacle = newObstacle;
            selector.robotController = robotController;
        }

        // Wire up TrialMetricsLogger
        if (logger != null)
        {
            logger.scenarioSelector = selector;
            logger.robotController = robotController;
        }

        // Remove old ground plane since scenarios have ground
        foreach (var obj in viveRoots)
        {
            if (obj.name == "Ground")
            {
                DestroyImmediate(obj);
            }
        }

        EditorSceneManager.SaveScene(viveScene);
        Debug.Log("Successfully merged SimulatedDriving scenarios into ViveFoveated!");
    }
}
