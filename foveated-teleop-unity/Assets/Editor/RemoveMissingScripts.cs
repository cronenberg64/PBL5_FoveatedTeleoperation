using UnityEngine;
using UnityEditor;

public class RemoveMissingScripts
{
    [MenuItem("PBL5/Tools/Remove Missing Scripts in Scene")]
    public static void Remove()
    {
        var objs = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        foreach (var obj in objs)
        {
            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
            count += removedCount;
        }
        Debug.Log($"Removed {count} missing scripts from the scene.");
    }
}
