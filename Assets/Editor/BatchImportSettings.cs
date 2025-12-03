#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class BatchImportSettings
{
    [MenuItem("Tools/Optimize/Textures → 2048 Crunch (skip UI)")]
public static void OptimizeLargeTextures()
{
    const int MaxSize = 2048;
    const int CrunchQuality = 50;

    // ‘t:Texture2D’ can still return guids whose importer isn’t TextureImporter (atlases, etc.)
    var guids = AssetDatabase.FindAssets("t:Texture2D");

    int changed = 0, skipped = 0, errors = 0;

    foreach (var guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);

        // Only proceed if the main asset is truly a Texture2D we can import
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) { skipped++; continue; }

        var importer = AssetImporter.GetAtPath(path);
        var ti = importer as TextureImporter;
        if (ti == null) { skipped++; continue; } // e.g., SpriteAtlas, RenderTexture, etc.

        try
        {
            // Skip typical UI sprite folders (adjust to your project)
            if (ti.textureType == TextureImporterType.Sprite && path.ToLower().Contains("/ui/"))
            { skipped++; continue; }

            bool dirty = false;

            // Cap size
            if (ti.maxTextureSize > MaxSize) { ti.maxTextureSize = MaxSize; dirty = true; }

            // Prefer crunch on desktop; Unity picks the right format per platform
            if (!ti.crunchedCompression) { ti.crunchedCompression = true; dirty = true; }
            if (ti.compressionQuality != CrunchQuality) { ti.compressionQuality = CrunchQuality; dirty = true; }

            // Sprites rarely need mipmaps
            if (ti.textureType == TextureImporterType.Sprite && ti.mipmapEnabled)
            { ti.mipmapEnabled = false; dirty = true; }

            // Platform override (Standalone)
            var ps = ti.GetPlatformTextureSettings("Standalone");
            if (!ps.overridden) ps.overridden = true;
            if (ps.maxTextureSize > MaxSize) { ps.maxTextureSize = MaxSize; dirty = true; }
            ti.SetPlatformTextureSettings(ps);

            if (dirty)
            {
                AssetDatabase.WriteImportSettingsIfDirty(path);
                changed++;
            }
            else skipped++;
        }
        catch (System.Exception ex)
        {
            errors++;
            Debug.LogWarning($"[Optimize] Skipped with error: {path}\n{ex.Message}");
        }
    }

    AssetDatabase.Refresh();
    Debug.Log($"[Optimize] Textures changed: {changed}, skipped: {skipped}, errors: {errors}");
}

    [MenuItem("Tools/Optimize/Audio → Vorbis (mono SFX)")]
    public static void OptimizeAudio()
    {
        var guids = AssetDatabase.FindAssets("t:AudioClip");
        int changed = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ai = (AudioImporter)AssetImporter.GetAtPath(path);
            if (ai == null) continue;

            var settings = ai.defaultSampleSettings;
            bool isMusic = path.ToLower().Contains("/music/") || path.ToLower().Contains("/bgm/");
            bool dirty = false;

            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.5f : 0.45f; // 0..1
            if (!isMusic && !ai.forceToMono) { ai.forceToMono = true; dirty = true; }

            ai.defaultSampleSettings = settings;
            dirty = true;

            if (dirty) { AssetDatabase.WriteImportSettingsIfDirty(path); changed++; }
        }
        AssetDatabase.Refresh();
        Debug.Log($"[Optimize] Audio updated: {changed}");
    }

    [MenuItem("Tools/Optimize/Clear Baked Lightmaps (Scene)")]
    public static void ClearLightmaps()
    {
        Lightmapping.Clear();
        Debug.Log("[Optimize] Cleared baked GI data for current scene.");
    }
}
#endif

