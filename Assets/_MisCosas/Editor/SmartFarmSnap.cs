using UnityEditor;
using UnityEngine;

// Smart Farm > Snap selected to terrain  (shortcut Ctrl+Shift+G)
// Sets the Y of every selected object to the terrain height under its X/Z. X, Z and rotation are not changed.
// Only the selected objects move, not their children's heights, so use it on objects over flat ground.
public static class SmartFarmSnap
{
    [MenuItem("Smart Farm/Snap selected to terrain %#g")]
    static void Snap()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("SmartFarm: no active Terrain in the scene.");
            return;
        }

        int n = 0;
        foreach (var t in Selection.transforms)
        {
            Vector3 p = t.position;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            Undo.RecordObject(t, "Snap to terrain");
            t.position = p;
            n++;
        }
        Debug.Log($"SmartFarm: {n} object(s) snapped to the terrain.");
    }
}
