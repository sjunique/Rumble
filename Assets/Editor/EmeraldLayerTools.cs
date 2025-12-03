// Assets/Editor/EmeraldLayerTools.cs
// Utility for Option A: dedicated layers + quick relayering for Emerald AI & Player.
// Works in the Editor. Unity 2020+.

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EmeraldLayerTools
{
    // === Public Menu ===

    [MenuItem("Tools/Emerald Setup/1) Ensure Required Layers")]
    public static void EnsureRequiredLayersMenu()
    {
        EnsureLayerExists("PlayerCharacter");
        EnsureLayerExists("AICreature");
        EnsureLayerExists("NonCombat"); // optional sandbox, safe to keep
        Debug.Log("Emerald Setup: Ensured layers [PlayerCharacter, AICreature, NonCombat].");
    }

    [MenuItem("Tools/Emerald Setup/2) Selected → PlayerCharacter (recursive)")]
    public static void SelectedToPlayerCharacter() =>
        SetSelectionLayerRecursive("PlayerCharacter");

    [MenuItem("Tools/Emerald Setup/3) Selected → AICreature (recursive)")]
    public static void SelectedToAICreature() =>
        SetSelectionLayerRecursive("AICreature");

    [MenuItem("Tools/Emerald Setup/4) Selected → NonCombat (recursive)")]
    public static void SelectedToNonCombat() =>
        SetSelectionLayerRecursive("NonCombat");

    [MenuItem("Tools/Emerald Setup/5) Scene: All Emerald AIs → AICreature")]
    public static void AllEmeraldToAICreature()
    {
        var emeraldType = FindTypeByFullName("EmeraldAI.EmeraldSystem");
        if (emeraldType == null)
        {
            Debug.LogWarning("Emerald Setup: Could not find type EmeraldAI.EmeraldSystem. Is Emerald imported?");
            return;
        }

        int targetLayer = GetOrCreateLayer("AICreature");
        int count = 0;

        foreach (var comp in UnityEngine.Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (comp && emeraldType.IsAssignableFrom(comp.GetType()))
            {
                SetLayerRecursive(comp.gameObject, targetLayer);
                count++;
            }
        }

        Debug.Log($"Emerald Setup: Relayered {count} Emerald AI root(s) to AICreature.");
    }

    [MenuItem("Tools/Emerald Setup/6) Selected is Player → Tag=Player + PlayerCharacter + Add FactionExtension (if available)")]
    public static void SelectedAsPlayerAndFaction()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Select your Player root first.");
            return;
        }

        var go = Selection.activeGameObject;

        // Tag -> Player (creates if missing)
        EnsureTagExists("Player");
        go.tag = "Player";

        // Layer -> PlayerCharacter (recursive)
        int playerLayer = GetOrCreateLayer("PlayerCharacter");
        SetLayerRecursive(go, playerLayer);

        // Try to add Emerald Faction Extension, if the type exists
        TryAddComponentIfAvailable(go, "EmeraldAI.FactionExtension");
        TryAddComponentIfAvailable(go, "FactionExtension"); // fallback if no namespace

        Debug.Log($"Emerald Setup: '{go.name}' set Tag=Player, Layer=PlayerCharacter, and tried to add FactionExtension.");
    }

    // === Helpers ===

    static void SetSelectionLayerRecursive(string layerName)
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Nothing selected. Select a root object first.");
            return;
        }
        int layer = GetOrCreateLayer(layerName);
        foreach (var obj in Selection.gameObjects)
            SetLayerRecursive(obj, layer);

        Debug.Log($"Emerald Setup: Set {Selection.gameObjects.Length} selected root(s) to layer '{layerName}' recursively.");
    }

    static void SetLayerRecursive(GameObject root, int layer)
    {
        if (root == null) return;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
        EditorUtility.SetDirty(root);
    }

    static int GetOrCreateLayer(string layerName)
    {
        EnsureLayerExists(layerName);
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
            throw new Exception($"Failed to resolve layer '{layerName}'.");
        return layer;
    }

    static void EnsureLayerExists(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName)) return;

        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
        if (tagManager == null)
        {
            Debug.LogError("Could not load TagManager.asset");
            return;
        }

        var so = new SerializedObject(tagManager);
        var layersProp = so.FindProperty("layers");
        if (layersProp == null || !layersProp.isArray)
        {
            Debug.LogError("TagManager layers property not found.");
            return;
        }

        // Already exists?
        for (int i = 0; i < layersProp.arraySize; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (sp != null && sp.stringValue == layerName)
                return;
        }

        // Find empty slot 8..31 (Unity reserves 0..7)
        for (int i = 8; i <= 31; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (sp != null && string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                so.ApplyModifiedProperties();
                return;
            }
        }

        Debug.LogError($"No empty user layer slots left to add '{layerName}'.");
    }

    static void EnsureTagExists(string tag)
    {
        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
        if (tagManager == null)
        {
            Debug.LogError("Could not load TagManager.asset");
            return;
        }

        var so = new SerializedObject(tagManager);
        var tagsProp = so.FindProperty("tags");
        if (tagsProp == null || !tagsProp.isArray)
        {
            Debug.LogError("TagManager tags property not found.");
            return;
        }

        // Already exists?
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            var sp = tagsProp.GetArrayElementAtIndex(i);
            if (sp != null && sp.stringValue == tag) return;
        }

        // Add new tag
        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        var newElem = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
        newElem.stringValue = tag;
        so.ApplyModifiedProperties();
    }

    static Type FindTypeByFullName(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                Type[] types = Array.Empty<Type>();
                try { types = a.GetTypes(); } catch { }
                return types;
            })
            .FirstOrDefault(t => t.FullName == fullName);
    }

    static void TryAddComponentIfAvailable(GameObject go, string fullTypeName)
    {
        var t = FindTypeByFullName(fullTypeName);
        if (t == null) return;
        if (go.GetComponent(t) == null)
        {
            go.AddComponent(t);
            EditorUtility.SetDirty(go);
        }
    }
}
#endif
