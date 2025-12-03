#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PlayerHelperLayerCleaner
{
    [MenuItem("Tools/Emerald Setup/Ensure Layers (PlayerCharacter, AICreature, NonCombat)")]
    public static void EnsureLayers()
    {
        EnsureLayerExists("PlayerCharacter");
        EnsureLayerExists("AICreature");
        EnsureLayerExists("NonCombat");
        Debug.Log("Ensured layers: PlayerCharacter, AICreature, NonCombat");
    }

    [MenuItem("Tools/Emerald Setup/Clean Selected Player Helpers → NonCombat")]
    public static void CleanSelectedPlayerHelpers()
    {
        var root = Selection.activeGameObject;
        if (!root) { Debug.LogWarning("Select your Player root first."); return; }

        int nonCombat = GetOrCreateLayer("NonCombat");
        int playerChar = GetOrCreateLayer("PlayerCharacter");

        // keep root on PlayerCharacter
        root.layer = playerChar;

        int moved = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            var go = t.gameObject;

            // Skip the root; move helper bits only
            if (go == root) continue;

            // Match by name or by component type name
            if (IsHelperName(go.name) || HasType(go, "vFootStepTrigger") || HasType(go, "vHitBox"))
            {
                go.layer = nonCombat;
                moved++;
            }
        }

        EditorUtility.SetDirty(root);
        Debug.Log($"Moved {moved} helper objects under '{root.name}' to NonCombat. Root kept on PlayerCharacter.");
    }

    static bool IsHelperName(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("trigger") || n.Contains("hitbox") || n.Contains("foot") || n.Contains("toe");
    }

    static bool HasType(GameObject go, string typeName)
    {
        // Avoid hard reference to Invector assemblies
        return go.GetComponents<Component>().Any(c => c && c.GetType().Name == typeName);
    }

    static int GetOrCreateLayer(string layerName)
    {
        EnsureLayerExists(layerName);
        int i = LayerMask.NameToLayer(layerName);
        if (i == -1) throw new Exception($"Layer '{layerName}' not found.");
        return i;
    }

    static void EnsureLayerExists(string layerName)
    {
        var tm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
        var so = new SerializedObject(tm);
        var layers = so.FindProperty("layers");
        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName) return;

        for (int i = 8; i < 32; i++)
        {
            var sp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                so.ApplyModifiedProperties();
                return;
            }
        }
        Debug.LogError($"No free user layer slot to add '{layerName}'.");
    }
}
#endif
