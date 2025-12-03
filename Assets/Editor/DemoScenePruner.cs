// Assets/Editor/DemoScenePruner.cs
// Safe “keep only what the demo scene needs” tool.
//
// Features:
// - Computes full dependency closure for selected scenes (Assets + Packages).
// - Optional: copy needed deps from Packages/ into Assets/_DemoKeep (preserves structure).
// - Moves every other asset under the chosen Environment Root Folder into Assets/_DemoTrash (reversible).
// - Writes a summary report.
//
// Notes:
// - Only touches assets under the chosen Environment Root Folder (never your whole project).
// - Uses AssetDatabase APIs; works with meta files automatically.
// - Always back up or have VCS before big moves.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class DemoScenePruner : EditorWindow
{
    [Serializable]
    public class ObjList : ScriptableObject { public List<UnityEngine.Object> items = new(); }

    // UI backing fields
    ObjList _scenes;
    ObjList _extraKeep;
    UnityEngine.Object _envRootFolder; // a folder asset under Assets/ (or the embedded package root you want to prune)
    bool _copyPackagesToAssets = true;
    string _keepFolder = "Assets/_DemoKeep";
    string _trashFolder = "Assets/_DemoTrash";
    Vector2 _scroll;

    // Results
    HashSet<string> _depsAssets = new();
    HashSet<string> _depsPackages = new();
    List<string> _envAllAssets = new();
    List<string> _envKeepAssets = new();
    List<string> _envTrashAssets = new();

    const string MENU = "Tools/Demo Scene Pruner";

    [MenuItem(MENU)]
    static void Open()
    {
        var w = GetWindow<DemoScenePruner>("Demo Scene Pruner");
        w.minSize = new Vector2(520, 460);
    }

    void OnEnable()
    {
        if (!_scenes) { _scenes = CreateInstance<ObjList>(); }
        if (!_extraKeep) { _extraKeep = CreateInstance<ObjList>(); }
    }

    void OnGUI()
    {
        using var sv = new EditorGUILayout.ScrollViewScope(_scroll);
        _scroll = sv.scrollPosition;

        EditorGUILayout.HelpBox("Goal: keep only what your demo scene needs.\n" +
            "1) Pick demo scene(s) and the environment root folder you imported.\n" +
            "2) Analyze to see what’s needed under that folder.\n" +
            "3) Move Unused → moves to Assets/_DemoTrash (reversible).\n" +
            "Optional: copy required Package deps into Assets/_DemoKeep.", MessageType.Info);

        DrawObjList("Demo Scenes to Keep", _scenes, ".unity");
        DrawObjList("Extra Keep (assets/folders)", _extraKeep, null);

        EditorGUILayout.Space(6);
        _envRootFolder = EditorGUILayout.ObjectField(new GUIContent("Environment Root Folder", "Folder you want to prune under Assets/ (e.g., Assets/Environments/AncientRuins)."), _envRootFolder, typeof(UnityEngine.Object), false);

        EditorGUILayout.Space(6);
        _copyPackagesToAssets = EditorGUILayout.ToggleLeft(new GUIContent("Copy package deps into Assets/_DemoKeep", "If the scene references Prefabs/Materials from Packages/, copy them into Assets so the project becomes standalone."), _copyPackagesToAssets);

        using (new EditorGUI.DisabledScope(!_copyPackagesToAssets))
            _keepFolder = EditorGUILayout.TextField("Keep Folder", _keepFolder);

        _trashFolder = EditorGUILayout.TextField("Trash Folder (reversible)", _trashFolder);

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledScope(!CanAnalyze()))
        {
            if (GUILayout.Button("Analyze (dry run)")) Analyze();
        }

        using (new EditorGUI.DisabledScope(_envTrashAssets.Count == 0))
        {
            if (GUILayout.Button($"Move Unused to '{_trashFolder}'"))
            {
                if (EditorUtility.DisplayDialog("Confirm move",
                    $"Move {_envTrashAssets.Count} asset(s) from '{GetAssetPath(_envRootFolder)}' into '{_trashFolder}'?\n\nThis is reversible—files are only moved.",
                    "Move", "Cancel"))
                {
                    MoveUnused();
                }
            }
        }

        // Summary
        if (_envAllAssets.Count > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Analysis Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Env assets (under root): {_envAllAssets.Count}");
            EditorGUILayout.LabelField($"Needed under root:       {_envKeepAssets.Count}");
            EditorGUILayout.LabelField($"Unused under root:       {_envTrashAssets.Count}");
            if (_depsPackages.Count > 0)
                EditorGUILayout.LabelField($"Package deps (will copy): {_depsPackages.Count}");

            if (GUILayout.Button("Write Report to Console"))
                WriteReportToConsole();
        }
    }

    void DrawObjList(string label, ObjList list, string extFilter)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        int remove = -1;
        for (int i = 0; i < list.items.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            list.items[i] = EditorGUILayout.ObjectField(list.items[i], typeof(UnityEngine.Object), false);
            if (!IsValid(list.items[i], extFilter)) GUI.color = Color.yellow;
            if (GUILayout.Button("X", GUILayout.Width(22))) remove = i;
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (remove >= 0) list.items.RemoveAt(remove);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add"))
        {
            list.items.Add(null);
        }
        if (GUILayout.Button("Use Selection"))
        {
            foreach (var o in Selection.objects)
                if (IsValid(o, extFilter)) list.items.Add(o);
        }
        EditorGUILayout.EndHorizontal();
    }

    static bool IsValid(UnityEngine.Object o, string extFilter)
    {
        if (!o) return false;
        var p = AssetDatabase.GetAssetPath(o);
        if (string.IsNullOrEmpty(p)) return false;
        return extFilter == null || p.EndsWith(extFilter, StringComparison.OrdinalIgnoreCase);
    }

    bool CanAnalyze()
    {
        return _scenes.items.Any(IsValidScene) && _envRootFolder && AssetDatabase.IsValidFolder(GetAssetPath(_envRootFolder));
    }

    static bool IsValidScene(UnityEngine.Object o)
    {
        if (!o) return false;
        var p = AssetDatabase.GetAssetPath(o);
        return p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }

    static string GetAssetPath(UnityEngine.Object o) => o ? AssetDatabase.GetAssetPath(o) : null;

    void Analyze()
    {
        _depsAssets.Clear();
        _depsPackages.Clear();
        _envAllAssets.Clear();
        _envKeepAssets.Clear();
        _envTrashAssets.Clear();

        var scenePaths = _scenes.items.Where(IsValidScene).Select(AssetDatabase.GetAssetPath).ToArray();
        var extraPaths = _extraKeep.items.Where(o => o).Select(AssetDatabase.GetAssetPath).ToArray();
        var root = GetAssetPath(_envRootFolder);

        // 1) Collect dependency closure of scenes + extras
        var seed = scenePaths.Concat(extraPaths).Distinct().ToArray();
        var allDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in seed)
        {
            foreach (var d in AssetDatabase.GetDependencies(s, true))
                allDeps.Add(d);
        }

        // Split into Assets/ and Packages/
        foreach (var d in allDeps)
        {
            if (d.StartsWith("Assets/")) _depsAssets.Add(d);
            else if (d.StartsWith("Packages/")) _depsPackages.Add(d);
        }

        // 2) Enumerate everything under the environment root folder
        _envAllAssets = AssetDatabase.FindAssets("", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        // 3) Keep vs Trash inside that root
        foreach (var p in _envAllAssets)
        {
            if (_depsAssets.Contains(p))
                _envKeepAssets.Add(p);
            else
                _envTrashAssets.Add(p);
        }

        // 4) Optionally copy package deps (we’ll actually copy on Move stage)
        Debug.Log($"[Pruner] Analyzed {scenePaths.Length} scene(s).");
        Debug.Log($"[Pruner] Under root '{root}': total={_envAllAssets.Count}, keep={_envKeepAssets.Count}, trash={_envTrashAssets.Count}");
        if (_copyPackagesToAssets)
            Debug.Log($"[Pruner] Package deps to copy: {_depsPackages.Count}");
        else if (_depsPackages.Count > 0)
            Debug.LogWarning($"[Pruner] Found {_depsPackages.Count} package deps, but copy option is OFF. Your scene will still rely on those packages.");
    }

    void MoveUnused()
    {
        if (_envTrashAssets.Count == 0)
        {
            Debug.Log("[Pruner] Nothing to move.");
            return;
        }

        EnsureFolder(_trashFolder);
        if (_copyPackagesToAssets) EnsureFolder(_keepFolder);

        // Copy package deps first (if any)
        if (_copyPackagesToAssets && _depsPackages.Count > 0)
        {
            int copied = 0;
            foreach (var pkg in _depsPackages)
            {
                // Recreate folder structure inside _keepFolder using file name to avoid collisions
                string dest = Path.Combine(_keepFolder, SanitizePackagePath(pkg)).Replace("\\", "/");
                var destDir = Path.GetDirectoryName(dest).Replace("\\", "/");
                EnsureFolder(destDir);
                if (AssetDatabase.CopyAsset(pkg, dest))
                    copied++;
                else
                    Debug.LogWarning($"[Pruner] Failed to copy: {pkg} -> {dest}");
            }
            Debug.Log($"[Pruner] Copied {copied}/{_depsPackages.Count} package assets into '{_keepFolder}'.");
        }

        // Move trash assets into _trashFolder, preserving sub-folders relative to root
        var root = GetAssetPath(_envRootFolder);
        int moved = 0;

        foreach (var src in _envTrashAssets)
        {
            string rel = src.Substring(root.Length).TrimStart('/');
            string dest = Path.Combine(_trashFolder, rel).Replace("\\", "/");
            EnsureFolder(Path.GetDirectoryName(dest).Replace("\\", "/"));

            var err = AssetDatabase.MoveAsset(src, dest);
            if (string.IsNullOrEmpty(err)) moved++;
            else Debug.LogWarning($"[Pruner] Move failed: {src} -> {dest}\n{err}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Pruner] Moved {moved}/{_envTrashAssets.Count} unused assets to '{_trashFolder}'. Review and delete when ready.");
    }

    void WriteReportToConsole()
    {
        Debug.Log("==== Demo Scene Pruner Report ====");
        Debug.Log($"Keep (under root) {_envKeepAssets.Count}:\n" + string.Join("\n", _envKeepAssets.Take(100)));
        if (_envKeepAssets.Count > 100) Debug.Log($"... +{_envKeepAssets.Count - 100} more");
        Debug.Log($"Trash (under root) {_envTrashAssets.Count}:\n" + string.Join("\n", _envTrashAssets.Take(100)));
        if (_envTrashAssets.Count > 100) Debug.Log($"... +{_envTrashAssets.Count - 100} more");
        if (_depsPackages.Count > 0)
            Debug.Log($"Package deps {_depsPackages.Count}:\n" + string.Join("\n", _depsPackages.Take(100)));
    }

    // Helpers
    static void EnsureFolder(string assetFolderPath)
    {
        if (string.IsNullOrEmpty(assetFolderPath)) return;
        if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

        var parts = assetFolderPath.Split('/');
        string cur = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    static string SanitizePackagePath(string p)
    {
        // Example: Packages/com.publisher.asset/Prefabs/Tree.prefab -> com.publisher.asset/Prefabs/Tree.prefab
        if (p.StartsWith("Packages/"))
            p = p.Substring("Packages/".Length);
        return p;
    }
}

