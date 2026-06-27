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

        // Find WorldRoot in SimulatedDriving
        GameObject[] simRoots = simScene.GetRootGameObjects();
        GameObject worldRoot = null;
        foreach (var obj in simRoots)
        {
            if (obj.name == "WorldRoot")
            {
                worldRoot = obj;
                break;
            }
        }

        GameObject scenarioCorridor = null;
        GameObject scenarioDoorway = null;
        GameObject scenarioObstacle = null;

        if (worldRoot != null)
        {
            // Find children inside WorldRoot
            for (int i = 0; i < worldRoot.transform.childCount; i++)
            {
                Transform child = worldRoot.transform.GetChild(i);
                if (child.name == "Scenario_Corridor") scenarioCorridor = child.gameObject;
                if (child.name == "Scenario_Doorway") scenarioDoorway = child.gameObject;
                if (child.name == "Scenario_Obstacle") scenarioObstacle = child.gameObject;
            }
        }
        else
        {
            Debug.LogError("[OneShotMerger] Could not find WorldRoot in SimulatedDriving scene!");
        }

        // Now find the new objects in ViveFoveated to wire them up
        GameObject[] viveRoots = viveScene.GetRootGameObjects();
        GameObject newCorridor = null;
        GameObject newDoorway = null;
        GameObject newObstacle = null;
        ScenarioSelector selector = null;
        TrialMetricsLogger logger = null;
        RobotController robotController = null;

        // First pass: cleanup any old objects from previous merges
        foreach (var obj in viveRoots)
        {
            if (obj.name == "Scenario_Corridor" && scenarioCorridor != null) { DestroyImmediate(obj); continue; }
            if (obj.name == "Scenario_Doorway" && scenarioDoorway != null) { DestroyImmediate(obj); continue; }
            if (obj.name == "Scenario_Obstacle" && scenarioObstacle != null) { DestroyImmediate(obj); continue; }
            if (obj.name == "ScenarioSelector") { DestroyImmediate(obj); continue; }
        }

        // Re-fetch root objects after cleanup
        viveRoots = viveScene.GetRootGameObjects();

        // Copy them to ViveFoveated as root objects
        if (scenarioCorridor != null)
        {
            newCorridor = Instantiate(scenarioCorridor);
            newCorridor.name = "Scenario_Corridor";
            newCorridor.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(newCorridor, viveScene);
        }
        if (scenarioDoorway != null)
        {
            newDoorway = Instantiate(scenarioDoorway);
            newDoorway.name = "Scenario_Doorway";
            newDoorway.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(newDoorway, viveScene);
        }
        if (scenarioObstacle != null)
        {
            newObstacle = Instantiate(scenarioObstacle);
            newObstacle.name = "Scenario_Obstacle";
            newObstacle.transform.SetParent(null);
            SceneManager.MoveGameObjectToScene(newObstacle, viveScene);
        }
        
        // Create the ScenarioSelector GameObject
        GameObject selectorObj = new GameObject("ScenarioSelector");
        selector = selectorObj.AddComponent<ScenarioSelector>();
        SceneManager.MoveGameObjectToScene(selectorObj, viveScene);

        // Close SimulatedDriving
        EditorSceneManager.CloseScene(simScene, true);

        // Find existing components in ViveFoveated
        viveRoots = viveScene.GetRootGameObjects();
        foreach (var obj in viveRoots)
        {
            if (obj.name == "TrialMetricsLogger") logger = obj.GetComponent<TrialMetricsLogger>();
            if (obj.name == "InputManager") 
            {
                robotController = obj.GetComponent<RobotController>();
                // Fix InputManager so it doesn't fall through floor and can trigger trial ends
                obj.tag = "Player";
                var collider = obj.GetComponent<CapsuleCollider>();
                if (collider == null)
                {
                    collider = obj.AddComponent<CapsuleCollider>();
                    collider.height = 2f;
                    collider.radius = 0.5f;
                    collider.center = new Vector3(0, 1, 0);
                }
            }
        }

        // Wire up ScenarioSelector
        if (selector != null)
        {
            selector.scenarioCorridor = newCorridor;
            selector.scenarioDoorway = newDoorway;
            selector.scenarioObstacle = newObstacle;
            selector.robotController = robotController;
        }
        else
        {
            Debug.LogError("[OneShotMerger] ScenarioSelector component not found!");
        }

        // Wire up TrialMetricsLogger
        if (logger != null)
        {
            logger.scenarioSelector = selector;
            logger.robotController = robotController;
        }
        else
        {
            Debug.LogError("[OneShotMerger] TrialMetricsLogger component not found!");
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
        Debug.Log("Successfully merged SimulatedDriving scenarios into ViveFoveated and fixed colliders!");
    }
}
