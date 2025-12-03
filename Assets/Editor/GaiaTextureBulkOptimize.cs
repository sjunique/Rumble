#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class GaiaTextureBulkOptimize
{
    // Edit these if you want different limits
    const int MaxSize = 2048;        // 1024 if you want smaller
    const int CrunchQuality = 50;    // 0..100

    // Folder filter – change if your GAIA path differs
    const string FolderHint = "Assets/Procedural Worlds"; // covers "Assets/Procedural Worlds/..."

    [MenuItem("Tools/Optimize/GAIA Textures → 2048 + Crunch")]
    public static void OptimizeGaiaTextures_Menu()
    {
        OptimizeGaiaTextures();
    }

    // Exposed for batchmode
    public static void OptimizeGaiaTextures()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FolderHint });
        int changed = 0, skipped = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!ti) { skipped++; continue; }

            // Skip UI sprites to avoid artifacts
            if (ti.textureType == TextureImporterType.Sprite && path.ToLower().Contains("/ui/"))
            { skipped++; continue; }

            bool dirty = false;

            // Keep normal maps as normal type (don’t force Sprite/Default changes)
            // Crunch works for normals too on desktop; leave them as-is otherwise.
            if (ti.maxTextureSize > MaxSize) { ti.maxTextureSize = MaxSize; dirty = true; }

            // Global compression settings
            if (!ti.crunchedCompression) { ti.crunchedCompression = true; dirty = true; }
            if (ti.compressionQuality != CrunchQuality) { ti.compressionQuality = CrunchQuality; dirty = true; }

            // Sprites generally don’t need mipmaps
            if (ti.textureType == TextureImporterType.Sprite && ti.mipmapEnabled)
            { ti.mipmapEnabled = false; dirty = true; }

            // Apply per-platform default (Standalone)
            var ps = ti.GetPlatformTextureSettings("Standalone");
            if (!ps.overridden) { ps.overridden = true; }
            if (ps.maxTextureSize > MaxSize) { ps.maxTextureSize = MaxSize; dirty = true; }
            // Let Unity pick the right compressed format; Crunch flag above will be honored.

            ti.SetPlatformTextureSettings(ps);

            if (dirty)
            {
                AssetDatabase.WriteImportSettingsIfDirty(path);
                changed++;
            }
            else skipped++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[GAIA Optimize] Changed: {changed}, Skipped: {skipped}. Folder: {FolderHint}, Max={MaxSize}, CrunchQ={CrunchQuality}");
    }
}
#endif

