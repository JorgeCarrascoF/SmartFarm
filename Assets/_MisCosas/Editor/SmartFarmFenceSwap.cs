using UnityEditor;
using UnityEngine;

// Smart Farm > Swap fence models for prefabs
// Select the Terrace object (or any parent of fence pieces) and run the command.
// Every child that is a raw Env_WoodFence_xx model is replaced by the pack's prefab of the same name,
// which already has a collider. Position, rotation, scale, name and order are kept. Ctrl+Z undoes it.
public static class SmartFarmFenceSwap
{
    [MenuItem("Smart Farm/Swap fence models for prefabs")]
    static void Swap()
    {
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("SmartFarm: select the Terrace object first.");
            return;
        }

        int swapped = 0, skipped = 0;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        foreach (var root in Selection.gameObjects)
        {
            // Copy the list first: the hierarchy changes while we swap.
            var pieces = new System.Collections.Generic.List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root.transform && t.name.StartsWith("Env_WoodFence")) pieces.Add(t);

            foreach (var old in pieces)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject);
                string path = source != null ? AssetDatabase.GetAssetPath(source) : null;

                // Only touch raw models. Anything that is already a .prefab is left alone.
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }

                var prefab = FindPrefab(source.name);
                if (prefab == null) { Debug.LogWarning("SmartFarm: no prefab named " + source.name); skipped++; continue; }

                var parent = old.parent;
                var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                Undo.RegisterCreatedObjectUndo(made, "Swap fence");
                made.transform.localPosition = old.localPosition;
                made.transform.localRotation = old.localRotation;
                made.transform.localScale = old.localScale;
                made.transform.SetSiblingIndex(old.GetSiblingIndex());
                made.name = old.name;
                made.isStatic = old.gameObject.isStatic;

                Undo.DestroyObjectImmediate(old.gameObject);
                swapped++;
            }
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"SmartFarm: {swapped} fence pieces swapped for prefabs, {skipped} skipped. Save the scene (Ctrl+S).");
    }

    static GameObject FindPrefab(string modelName)
    {
        foreach (var guid in AssetDatabase.FindAssets(modelName + " t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == modelName)
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }
}
