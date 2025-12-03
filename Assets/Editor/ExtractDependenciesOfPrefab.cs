using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
//private const string kScenePath  = "Assets/Rumble/RpgQuest/_launcher/FinalCanvas/ScenesLevels/Level_01_Enchanted.unity";
 
 
public static class ExtractDependenciesOfScene
{
    // EDIT THESE THREE PATHS:
    // private const string kScenePath = "Assets/Rumble/RpgQuest/_launcher/FinalCanvas/ScenesLevels/Level_01_Enchanted.unity";
       // private const string kPackRoot   = "Assets/_downloadedassets/Aquarius Fantasy - Fae Pack";
       //private const string kTargetRoot = "Assets/Rumble/Keep/Enchanted/UsedFromAquarius";
    private const string kScenePath = "Assets/Rumble/RpgQuest/_launcher/FinalCanvas/ScenesLevels/level_01_azurenature.unity";

   

 private const string kPackRoot   = "Assets/_downloadedassets/AZURE Nature";
   // 

   
    private const string kTargetRoot = "Assets/Rumble/Keep/Azure/UsedFromAzureNature";

    // Include Terrain Layers + common deps
    private static readonly string[] kAllowedExts = {
        ".prefab", ".mat", ".png", ".jpg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr",
        ".fbx", ".obj", ".blend",
        ".anim", ".controller", ".overrideController", ".mask",
        ".shader", ".shadergraph", ".cginc", ".hlsl", ".glslinc",
        ".asset", ".terrainlayer", ".renderTexture", ".vfx", ".compute", ".cubemap", ".sbsar"
    };

    // Only skip obvious non-runtime stuff; do NOT skip by folder (demo assets can be real deps)
    private static readonly string[] kSkipExts = { ".unity", ".pdf", ".txt", ".md" };

    [MenuItem("Tools/Cleanup/Extract Enchanted Scene Dependencies (incl. TerrainLayers)")]
    public static void Extract()
    {
        if (!File.Exists(kScenePath)) { EditorUtility.DisplayDialog("Extract", $"Scene not found:\n{kScenePath}", "OK"); return; }
        if (!AssetDatabase.IsValidFolder(kPackRoot)) { EditorUtility.DisplayDialog("Extract", $"Pack root not found:\n{kPackRoot}", "OK"); return; }
        EnsureFolder(kTargetRoot);

        var deps = AssetDatabase.GetDependencies(new[] { kScenePath }, true)
            .Where(p => p.StartsWith(kPackRoot))
            .Where(p => !kSkipExts.Contains(Path.GetExtension(p).ToLower()))
            .Where(p => kAllowedExts.Contains(Path.GetExtension(p).ToLower()))
            .Distinct()
            .ToList();

        if (deps.Count == 0) { EditorUtility.DisplayDialog("Extract", "No pack dependencies found. Safe to delete the pack.", "OK"); return; }

        int moved = 0, failed = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var src in deps)
            {
                var rel  = src.Substring(kPackRoot.Length).TrimStart('/', '\\');
                var dest = Path.Combine(kTargetRoot, rel).Replace("\\", "/");
                var dir  = Path.GetDirectoryName(dest)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(dir)) EnsureFolder(dir);

                var err = AssetDatabase.MoveAsset(src, dest);
                if (string.IsNullOrEmpty(err)) moved++; else { Debug.LogWarning($"Move failed: {src} -> {dest} :: {err}"); failed++; }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Extract",
            $"Dependencies in pack: {deps.Count}\nMoved: {moved}\nFailed: {failed}\n\nNow you can safely delete:\n{kPackRoot}",
            "OK");
    }

    private static void EnsureFolder(string path)
    {
        path = path.Replace("\\", "/");
        var parts = path.Split('/');
        var build = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            var next = $"{build}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(build, parts[i]);
            build = next;
        }
    }
}
