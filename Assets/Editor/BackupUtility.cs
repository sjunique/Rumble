#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.IO.Compression;
// Ensure System.IO.Compression.FileSystem is referenced for ZipFile support
using System.Collections.Generic;

public static class BackupUtility
{
    // Defaults (you can change this; user can override in menu)
    private static string DefaultBackupRootPath = @"/media/anons/ctx500gb/unityProjects";

    private const string PREF_BACKUP_ROOT   = "BackupUtility.Root";
    private const string PREF_LAST_SETTINGS = "BackupUtility.LastSettingsJson";

    [Serializable]
    public class BackupSettings
    {
        public bool includeAssets = true;
        public bool includeProjectSettings = true;
        public bool includePackages = true;

        // Exclusions are relative to the PROJECT ROOT, e.g.:
        // "Assets/Art/BigTextures", "Assets/Addressables/LocalCache"
        public List<string> excludePaths = new List<string>();

        // Simple substring match (case-insensitive) on the relative path
        // e.g. "Library", "Temp", ".git", ".psd"
        public List<string> excludePatterns = new List<string>();
    }

    // ---------------- Menu Items ----------------

    [MenuItem("Tools/Backup/Set Backup Root...")]
    public static void SetBackupRoot()
    {
        string current = GetBackupRootPath();
        string chosen = EditorUtility.OpenFolderPanel("Choose Backup Root", string.IsNullOrEmpty(current) ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) : current, "");
        if (!string.IsNullOrEmpty(chosen))
        {
            EditorPrefs.SetString(PREF_BACKUP_ROOT, chosen);
            Debug.Log($"[BackupUtility] Backup root set to: {chosen}");
        }
    }

    [MenuItem("Tools/Backup/Open Backup Root")]
    public static void OpenBackupRoot()
    {
        var root = GetBackupRootPath();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            EditorUtility.DisplayDialog("Backup Root", "Backup root not set or does not exist.\nUse Tools → Backup → Set Backup Root…", "OK");
            return;
        }
        EditorUtility.RevealInFinder(root);
    }

    [MenuItem("Tools/Backup/Backup Project (choose exclusions)...")]
    public static void BackupWithUI()
    {
        var settings = LoadLastSettings() ?? CreateDefaultSettings();
        BackupSettingsWindow.Show(settings);
    }

    [MenuItem("Tools/Backup/Quick Backup (use last settings)")]
    public static void QuickBackup()
    {
        var settings = LoadLastSettings() ?? CreateDefaultSettings();
        DoBackup(settings);
    }

    // ---------------- Core Backup ----------------

    public static void DoBackup(BackupSettings settings)
    {
        string backupRootPath = GetBackupRootPath();
        if (string.IsNullOrEmpty(backupRootPath) || !Directory.Exists(backupRootPath))
        {
            Debug.LogError("Backup root path does not exist. Set it via Tools → Backup → Set Backup Root…");
            return;
        }

        SaveLastSettings(settings);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string projectName = Application.productName;
        string timestamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string tempFolderName = $"{projectName}_Backup_{timestamp}";
        string tempBackupPath = Path.Combine(backupRootPath, tempFolderName);
        Directory.CreateDirectory(tempBackupPath);

        try
        {
            var includeList = new List<string>();
            if (settings.includeAssets)          includeList.Add("Assets");
            if (settings.includeProjectSettings) includeList.Add("ProjectSettings");
            if (settings.includePackages)        includeList.Add("Packages");

            // Normalize excludes
            var excludeAbs = BuildExcludeAbsoluteSet(projectRoot, settings.excludePaths);
            var excludePatterns = BuildPatternList(settings.excludePatterns);

            int totalFolders = includeList.Count;
            for (int i = 0; i < totalFolders; i++)
            {
                string folder = includeList[i];
                string src = Path.Combine(projectRoot, folder);
                string dst = Path.Combine(tempBackupPath, folder);
                if (!Directory.Exists(src))
                {
                    Debug.LogWarning($"[Backup] Skipping missing folder: {folder}");
                    continue;
                }

                float prog = (float)i / Mathf.Max(1, totalFolders);
                EditorUtility.DisplayProgressBar("Backing up project", $"Copying {folder}...", prog);
                CopyFolderFiltered(src, dst, projectRoot, excludeAbs, excludePatterns);
                Debug.Log($"[Backup] Copied: {folder}");
            }

            EditorUtility.DisplayProgressBar("Zipping backup", "Creating archive...", 0.95f);
            string zipPath = Path.Combine(backupRootPath, $"{projectName}_Backup_{timestamp}.zip");
            ZipFile.CreateFromDirectory(tempBackupPath, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);

            Debug.Log($"[Backup] Project successfully zipped to: {zipPath}");
            EditorUtility.DisplayDialog("Backup Completed", $"Project backed up and zipped to:\n{zipPath}", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Backup] Failed: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            // Remove temp unzipped folder
            try { if (Directory.Exists(tempBackupPath)) Directory.Delete(tempBackupPath, true); } catch { /* ignore */ }
        }
    }

    // ---------------- Helpers ----------------

    private static string GetBackupRootPath()
    {
        var p = EditorPrefs.GetString(PREF_BACKUP_ROOT, "");
        if (string.IsNullOrEmpty(p)) p = DefaultBackupRootPath;
        return p;
    }

    private static BackupSettings CreateDefaultSettings()
    {
        return new BackupSettings
        {
            includeAssets = true,
            includeProjectSettings = true,
            includePackages = true,
            excludePaths = new List<string>
            {
                // Examples (relative to project root) — add/remove in UI:
                // "Assets/Art/Raw",
                // "Assets/Addressables/LocalCache",
                // "Assets/StreamingAssets/Cache"
            },
            excludePatterns = new List<string>
            {
                // Examples — add/remove in UI:
                ".git", ".svn", ".DS_Store",
                "Library", "Temp", "Logs", "obj",
                "~", ".tmp", ".cache"
            }
        };
    }

    private static void CopyFolderFiltered(string src, string dst, string projectRoot, HashSet<string> excludeAbs, List<string> excludePatterns)
    {
        Directory.CreateDirectory(dst);

        // Files
        foreach (var file in Directory.GetFiles(src))
        {
            if (ShouldSkip(file, projectRoot, excludeAbs, excludePatterns)) continue;

            string destFile = Path.Combine(dst, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        // Dirs
        foreach (var dir in Directory.GetDirectories(src))
        {
            if (ShouldSkip(dir, projectRoot, excludeAbs, excludePatterns)) continue;

            string destDir = Path.Combine(dst, Path.GetFileName(dir));
            CopyFolderFiltered(dir, destDir, projectRoot, excludeAbs, excludePatterns);
        }
    }

    private static bool ShouldSkip(string path, string projectRoot, HashSet<string> excludeAbs, List<string> patterns)
    {
        string full = Path.GetFullPath(path).Replace('\\', '/');

        // Directory or file under an excluded absolute path
        foreach (var ex in excludeAbs)
        {
            if (full.StartsWith(ex, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Pattern match on RELATIVE path
        string rel = MakeRelative(full, projectRoot);
        string relLower = rel.ToLowerInvariant();
        foreach (var pat in patterns)
        {
            if (string.IsNullOrWhiteSpace(pat)) continue;
            if (relLower.Contains(pat.ToLowerInvariant()))
                return true;
        }
        return false;
    }

    private static HashSet<string> BuildExcludeAbsoluteSet(string projectRoot, List<string> rels)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in rels)
        {
            if (string.IsNullOrWhiteSpace(rel)) continue;
            string abs = Path.GetFullPath(Path.Combine(projectRoot, rel)).Replace('\\', '/');
            // Ensure path ends with a slash to avoid partial matches
            if (!abs.EndsWith("/")) abs += "/";
            set.Add(abs);
        }
        return set;
    }

    private static List<string> BuildPatternList(List<string> patterns)
    {
        var list = new List<string>();
        foreach (var p in patterns) if (!string.IsNullOrWhiteSpace(p)) list.Add(p.Trim());
        return list;
    }

    private static string MakeRelative(string full, string root)
    {
        full = full.Replace('\\', '/');
        root = root.Replace('\\', '/');
        if (!root.EndsWith("/")) root += "/";
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return full.Substring(root.Length);
        return full;
    }

    private static void SaveLastSettings(BackupSettings s)
    {
        string json = JsonUtility.ToJson(s);
        EditorPrefs.SetString(PREF_LAST_SETTINGS, json);
    }

    private static BackupSettings LoadLastSettings()
    {
        string json = EditorPrefs.GetString(PREF_LAST_SETTINGS, "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<BackupSettings>(json); }
        catch { return null; }
    }

    // ---------------- UI Window ----------------
    public class BackupSettingsWindow : EditorWindow
    {
        BackupSettings settings;
        Vector2 scroll;

        public static void Show(BackupSettings initial)
        {
            var win = GetWindow<BackupSettingsWindow>("Backup Project");
            win.settings = initial ?? CreateDefaultSettings();
            win.minSize = new Vector2(520, 420);
            win.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Include Top-Level Folders", EditorStyles.boldLabel);
            settings.includeAssets          = EditorGUILayout.ToggleLeft("Assets",          settings.includeAssets);
            settings.includeProjectSettings = EditorGUILayout.ToggleLeft("ProjectSettings", settings.includeProjectSettings);
            settings.includePackages        = EditorGUILayout.ToggleLeft("Packages",        settings.includePackages);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Exclude Specific Directories (relative to project root)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120));
                for (int i = 0; i < settings.excludePaths.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    settings.excludePaths[i] = EditorGUILayout.TextField(settings.excludePaths[i]);
                    if (GUILayout.Button("X", GUILayout.Width(26))) { settings.excludePaths.RemoveAt(i); i--; }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Add"))
                {
                    string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    string start = Directory.Exists(Path.Combine(root, "Assets")) ? Path.Combine(root, "Assets") : root;
                    string folder = EditorUtility.OpenFolderPanel("Choose folder to exclude", start, "");
                    if (!string.IsNullOrEmpty(folder))
                    {
                        string rel = MakeRelative(Path.GetFullPath(folder).Replace('\\', '/'), root.Replace('\\', '/'));
                        if (!string.IsNullOrEmpty(rel) && !settings.excludePaths.Contains(rel))
                            settings.excludePaths.Add(rel);
                    }
                }
                if (GUILayout.Button("Add Selected"))
                {
                    string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
                    foreach (var go in Selection.gameObjects)
                    {
                        string path = AssetDatabase.GetAssetPath(go);
                        if (string.IsNullOrEmpty(path)) continue;
                        // path is relative to project root already (e.g. "Assets/Folder")
                        string rel = path.Replace('\\', '/');
                        if (AssetDatabase.IsValidFolder(path))
                        {
                            if (!settings.excludePaths.Contains(rel))
                                settings.excludePaths.Add(rel);
                        }
                        else
                        {
                            // for a file, exclude its directory
                            string dir = Path.GetDirectoryName(rel).Replace('\\', '/');
                            if (!settings.excludePaths.Contains(dir))
                                settings.excludePaths.Add(dir);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Exclude Patterns (substring in relative path)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                for (int i = 0; i < settings.excludePatterns.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    settings.excludePatterns[i] = EditorGUILayout.TextField(settings.excludePatterns[i]);
                    if (GUILayout.Button("X", GUILayout.Width(26))) { settings.excludePatterns.RemoveAt(i); i--; }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Add Pattern")) settings.excludePatterns.Add("");
                if (GUILayout.Button("Common Defaults"))
                {
                    settings.excludePatterns.AddRange(new[] { ".git", ".svn", ".DS_Store", "Library", "Temp", "Logs", "obj", "~", ".tmp", ".cache" });
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Backup Root..."))
                {
                    SetBackupRoot();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(100))) this.Close();
                if (GUILayout.Button("Backup Now", GUILayout.Width(120)))
                {
                    this.Close();
                    DoBackup(settings);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Exclusion paths are relative to the project root (e.g. Assets/BigTextures).\n" +
                                    "Patterns are simple substring matches on the relative path.", MessageType.Info);
        }
    }
}
#endif

