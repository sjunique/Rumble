// Assets/Editor/FindInstancingIssues.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class FindInstancingIssues : EditorWindow
{
    // Offenders = materials that have Enable GPU Instancing ON
    // but whose shader likely does NOT support instancing (or unknown → treated as issue).
    private static readonly List<(Material mat, string context, string reason)> s_offenders = new();

    private static bool s_safeModeTreatUnknownAsOffender = true;

    // --------------------------- MENU ENTRIES ---------------------------

    [MenuItem("Tools/Instancing/Scan Project for Instancing Issues")]
    public static void ScanProject()
    {
        s_offenders.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Material");
        int inspected = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) continue;

            if (mat.enableInstancing && mat.shader != null)
            {
                bool? supports = TryShaderSupportsInstancing(mat.shader, out string why);
                if (supports == false || (supports == null && s_safeModeTreatUnknownAsOffender))
                {
                    s_offenders.Add((mat, path, why ?? (supports == null ? "unknown (safe mode)" : "reported false")));
                }
            }

            inspected++;
        }

        if (s_offenders.Count == 0)
            Debug.Log($"[Instancing Scan] No issues found. Inspected {inspected} materials.");
        else
            Debug.LogWarning($"[Instancing Scan] Found {s_offenders.Count} offenders. Open: Tools/Instancing/Show Last Scan Results");
    }

    [MenuItem("Tools/Instancing/Scan OPEN Scene Renderers")]
    public static void ScanOpenScene()
    {
        s_offenders.Clear();
        var renderers = GameObject.FindObjectsOfType<Renderer>(true);

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            if (mats == null) continue;

            foreach (var m in mats)
            {
                if (!m || !m.shader) continue;

                if (m.enableInstancing)
                {
                    bool? supports = TryShaderSupportsInstancing(m.shader, out string why);
                    if (supports == false || (supports == null && s_safeModeTreatUnknownAsOffender))
                    {
                        string ctx = $"{GetPath(r.transform)} [{AssetDatabase.GetAssetPath(m)}]";
                        s_offenders.Add((m, ctx, why ?? (supports == null ? "unknown (safe mode)" : "reported false")));
                    }
                }
            }
        }

        if (s_offenders.Count == 0)
            Debug.Log("[Instancing Scan] Open scene: no offenders.");
        else
            Debug.LogWarning("[Instancing Scan] Open scene offenders found. Open: Tools/Instancing/Show Last Scan Results");
    }

    [MenuItem("Tools/Instancing/Show Last Scan Results")]
    public static void ShowResults()
    {
        var w = GetWindow<FindInstancingIssues>("Instancing Issues");
        w.minSize = new Vector2(760, 420);
        w.Show();
    }

    [MenuItem("Tools/Instancing/Disable Instancing Under Folder...")]
    public static void DisableInstancingUnderFolder()
    {
        string absPath = EditorUtility.OpenFolderPanel("Pick an asset folder", Application.dataPath, "");
        if (string.IsNullOrEmpty(absPath)) return;

        // Convert absolute path -> "Assets/..."
        string projPath = absPath.Replace(Application.dataPath, "Assets");
        if (!projPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[Instancing] Selected folder is outside this project.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { projPath });

        int changed = 0;
        var toUndo = new List<UnityEngine.Object>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat || !mat.enableInstancing) continue;

            toUndo.Add(mat);
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            changed++;
        }

        if (toUndo.Count > 0) Undo.RecordObjects(toUndo.ToArray(), "Disable instancing under folder");
        AssetDatabase.SaveAssets();
        Debug.Log($"[Instancing] Disabled instancing on {changed} materials in {projPath}");
    }

    [MenuItem("Tools/Instancing/Disable Instancing For Selected")]
    public static void DisableInstancingForSelected()
    {
        var selectionGuids = Selection.assetGUIDs;
        if (selectionGuids == null || selectionGuids.Length == 0)
        {
            Debug.Log("[Instancing] Nothing selected in Project view.");
            return;
        }

        var roots = new List<string>();
        foreach (var guid in selectionGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path)) roots.Add(path);
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", roots.ToArray());
        int changed = 0;
        var toUndo = new List<UnityEngine.Object>();

        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat || !mat.enableInstancing) continue;

            toUndo.Add(mat);
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            changed++;
        }

        if (toUndo.Count > 0) Undo.RecordObjects(toUndo.ToArray(), "Disable instancing for selected");
        AssetDatabase.SaveAssets();
        Debug.Log($"[Instancing] Disabled instancing on {changed} materials under selection.");
    }

    [MenuItem("Tools/Instancing/Scan OPEN Scene Particle Systems")]
    public static void ScanOpenSceneParticleSystems()
    {
        var psrs = GameObject.FindObjectsOfType<ParticleSystemRenderer>(true);
        var offenders = new List<(ParticleSystemRenderer psr, string goPath, string matName, string shaderName)>();

        foreach (var psr in psrs)
        {
            if (!psr.enableGPUInstancing) continue;

            var mats = psr.sharedMaterials;
            if (mats == null) continue;

            foreach (var m in mats)
            {
                if (!m || !m.shader) continue;

                bool? supports = TryShaderSupportsInstancing(m.shader, out _);
                if (supports == false || supports == null)
                    offenders.Add((psr, GetPath(psr.transform), m.name, m.shader.name));
            }
        }

        if (offenders.Count == 0)
        {
            Debug.Log("[Instancing Scan] Particle systems: no offenders.");
            return;
        }

        Debug.LogWarning("[Instancing Scan] ParticleSystemRenderer with GPU Instancing ON + non-instanced shaders:\n" +
                         string.Join("\n", offenders.Select(o => $"{o.goPath} -> {o.matName} (shader: {o.shaderName})")));

        if (EditorUtility.DisplayDialog("Disable GPU Instancing on these Particle Systems?",
            $"Found {offenders.Count} entries. Disable 'Enable GPU Instancing' on their ParticleSystemRenderer components?",
            "Disable", "Cancel"))
        {
            Undo.RecordObjects(offenders.Select(o => (UnityEngine.Object)o.psr).Distinct().ToArray(), "Disable PSR instancing");
            foreach (var o in offenders)
            {
                o.psr.enableGPUInstancing = false;
                EditorUtility.SetDirty(o.psr);
            }
            Debug.Log($"[Instancing Scan] Disabled GPU Instancing on {offenders.Select(o => o.psr).Distinct().Count()} ParticleSystemRenderer(s).");
        }
    }

    [MenuItem("Tools/Instancing/Scan & Fix Terrain Detail Instancing (OPEN Scene)")]
    public static void ScanAndFixTerrainDetailInstancing()
    {
        var terrains = GameObject.FindObjectsOfType<Terrain>(true);
        if (terrains.Length == 0)
        {
            Debug.Log("[Instancing Scan] No Terrains in the open scene.");
            return;
        }

        int terrainsDrawInstanced = terrains.Count(t => t != null && t.drawInstanced);

        if (terrainsDrawInstanced == 0)
        {
            Debug.Log("[Instancing Scan] Terrains: no drawInstanced enabled.");
        }
        else
        {
            Debug.LogWarning($"[Instancing Scan] Terrains with drawInstanced=true: {terrainsDrawInstanced}");
        }

        if (terrainsDrawInstanced > 0 &&
            EditorUtility.DisplayDialog("Disable Terrain instanced details?",
                $"Turn OFF Terrain.drawInstanced on {terrainsDrawInstanced} terrain(s)?", "Disable", "Cancel"))
        {
            int changed = 0;
            foreach (var t in terrains)
            {
                if (t != null && t.drawInstanced)
                {
                    Undo.RecordObject(t, "Disable Terrain DrawInstanced");
                    t.drawInstanced = false;
                    EditorUtility.SetDirty(t);
                    changed++;
                }
            }
            Debug.Log($"[Instancing Scan] Disabled Terrain.drawInstanced on {changed} terrain(s).");
        }

        // NOTE: If Gaia set DetailPrototypes to instanced mesh, you'd have to modify TerrainData.detailPrototypes
        // (API varies by Unity version). The above usually suffices to stop GL instancing spam.
    }

    // --------------------------- WINDOW UI ---------------------------

    private Vector2 _scroll;

    private void OnGUI()
    {
        GUILayout.Label("GPU Instancing: Find materials/components that cause GL instancing spam", EditorStyles.boldLabel);
        s_safeModeTreatUnknownAsOffender = EditorGUILayout.ToggleLeft(
            "Safe mode: treat unknown shader instancing support as offenders", s_safeModeTreatUnknownAsOffender);

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan Project"))
            ScanProject();
        if (GUILayout.Button("Scan OPEN Scene"))
            ScanOpenScene();
        if (GUILayout.Button("Scan OPEN Scene Particle Systems"))
            ScanOpenSceneParticleSystems();
        if (GUILayout.Button("Scan & Fix Terrain Instancing"))
            ScanAndFixTerrainDetailInstancing();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (s_offenders.Count == 0)
        {
            EditorGUILayout.HelpBox("No offenders recorded yet. Run a scan.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"Found {s_offenders.Count} potential offenders:");
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
        foreach (var (mat, ctx, reason) in s_offenders)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select", GUILayout.Width(70)))
                Selection.activeObject = mat;

            EditorGUILayout.ObjectField(mat, typeof(Material), false);
            EditorGUILayout.LabelField(ctx, GUILayout.MaxHeight(18));
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(reason))
                EditorGUILayout.LabelField("  • " + reason, EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Disable Instancing on All Listed Materials"))
        {
            Undo.RecordObjects(s_offenders.Select(o => (UnityEngine.Object)o.mat).Distinct().ToArray(), "Disable instancing on listed materials");
            foreach (var (mat, _, _) in s_offenders)
            {
                mat.enableInstancing = false;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Instancing Scan] Disabled instancing on {s_offenders.Select(o => o.mat).Distinct().Count()} material(s).");
        }
    }

    // --------------------------- HELPERS ---------------------------

    private static string GetPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack.ToArray());
    }

    // Robust, exception-safe: checks pass tags without compile-time dependency on ShaderTagId/ShaderUtil.
    // Returns true  -> at least one pass declares Instancing true/on/enabled
    //         false -> saw tags but none were true
    //         null  -> no tag info available / API mismatch (unknown)
    private static bool? TryShaderSupportsInstancing(Shader shader, out string why)
    {
        why = null;
        int passCount = 0;

        try { passCount = shader.passCount; }
        catch (Exception e) { why = "shader.passCount threw: " + e.GetType().Name; return null; }

        bool sawAnyTag = false;

        for (int i = 0; i < passCount; i++)
        {
            string tagValue = FindPassTagValueCompat(shader, i, "Instancing");
            if (!string.IsNullOrEmpty(tagValue))
            {
                sawAnyTag = true;
                string t = tagValue.ToLowerInvariant();
                if (t == "true" || t == "on" || t == "enabled")
                    return true;
            }
        }

        if (sawAnyTag) return false;
        return null;
    }
[UnityEditor.MenuItem("Tools/Instancing/Enable Terrain DrawInstanced (OPEN Scene)")]
static void EnableTerrainDrawInstanced()
{
    var terrains = UnityEngine.GameObject.FindObjectsOfType<UnityEngine.Terrain>(true);
    int changed = 0;
    foreach (var t in terrains)
    {
        if (t != null && !t.drawInstanced)
        {
            UnityEditor.Undo.RecordObject(t, "Enable Terrain DrawInstanced");
            t.drawInstanced = true;
            UnityEditor.EditorUtility.SetDirty(t);
            changed++;
        }
    }
//UnityEditor.Debug.Log($"[Instancing] Enabled Terrain.drawInstanced on {changed} terrain(s).");
}
    // Never throws; returns "" if API signature not found or reflection fails.
    private static string FindPassTagValueCompat(Shader shader, int passIndex, string tagName)
    {
        try
        {
            var shaderType = typeof(Shader);

            // Try older string overload: FindPassTagValue(int, string)
            var miString = shaderType.GetMethod(
                "FindPassTagValue",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new Type[] { typeof(int), typeof(string) },
                null);

            if (miString != null)
            {
                var res = miString.Invoke(shader, new object[] { passIndex, tagName });
                return res as string ?? string.Empty;
            }

            // Try newer ShaderTagId overload via reflection
            var tagIdType = Type.GetType("UnityEngine.Rendering.ShaderTagId, UnityEngine.CoreModule", false);
            if (tagIdType != null)
            {
                var ctor = tagIdType.GetConstructor(new[] { typeof(string) });
                if (ctor != null)
                {
                    object tagId = ctor.Invoke(new object[] { tagName });
                    var miTag = shaderType.GetMethod(
                        "FindPassTagValue",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new Type[] { typeof(int), tagIdType },
                        null);
                    if (miTag != null)
                    {
                        var res = miTag.Invoke(shader, new object[] { passIndex, tagId });
                        return res as string ?? string.Empty;
                    }
                }
            }
        }
        catch
        {
            // swallow
        }

        return string.Empty;
    }
}
#endif
