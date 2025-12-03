// DiagMenu.cs (Editor-only)
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DiagMenu
{
    [MenuItem("Tools/Diagnostics/Create Diag Settings Asset")]
    public static void CreateAsset()
    {
        var path = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var asset = ScriptableObject.CreateInstance<DiagSettings>();
        AssetDatabase.CreateAsset(asset, $"{path}/DiagSettings.asset");
        Selection.activeObject = asset;
        Debug.Log("Created Assets/Resources/DiagSettings.asset");
    }

    [MenuItem("Tools/Diagnostics/Add Bootstrap To Scene")]
    public static void AddBootstrap()
    {
        var go = new GameObject("DiagBootstrap");
        go.AddComponent<DiagBootstrap>();
        Undo.RegisterCreatedObjectUndo(go, "Add DiagBootstrap");
        Debug.Log("Added DiagBootstrap to scene.");
    }
}
#endif
