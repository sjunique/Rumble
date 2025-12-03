#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;

public static class NavTileBatch
{
    [MenuItem("Tools/NavMesh/Bake All Child Tiles (Selected Root)")]
    public static void BakeAllUnderSelected()
    {
        var root = Selection.activeTransform;
        if (!root) { Debug.LogWarning("Select the grid root first."); return; }
        int count = 0;
        foreach (var s in root.GetComponentsInChildren<NavMeshSurface>(true))
        {
            var box = s.GetComponent<BoxCollider>();
            if (box) { s.center = box.center; s.size = box.size; }
            s.BuildNavMesh();
            count++;
        }
        Debug.Log($"Baked {count} NavMeshSurface tiles under '{root.name}'.");
    }
}
#endif

