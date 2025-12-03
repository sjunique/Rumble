#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class ShopUIPanelCreator
{
    // Remember last used assets across calls
    private const string PREFAB_PREF = "ShopUI_LastPrefab";
    private const string UXML_PREF   = "ShopUI_LastUXML";
    private const string PANEL_PREF  = "ShopUI_LastPanelSettings";
    private const string SPRITE_PREF = "ShopUI_LastFrameSprite";

    // ---------- MENU: create from prefab ----------
    [MenuItem("Tools/UI/Create Shop Panel (from Prefab)")]
    public static void CreateFromPrefab()
    {
        var lastPath = EditorPrefs.GetString(PREFAB_PREF, "");
        var prefab = LoadOrPick<GameObject>("Select ShopUI Prefab", "prefab", lastPath);
        if (!prefab) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(go, "Create Shop Panel (Prefab)");
        Selection.activeObject = go;

        var path = AssetDatabase.GetAssetPath(prefab);
        EditorPrefs.SetString(PREFAB_PREF, path);
    }

    // ---------- MENU: build UITK object in-scene ----------
    [MenuItem("Tools/UI/Build Shop Panel (UI Toolkit)")]
    public static void BuildUITK()
    {
        // Pick required assets
        var lastUxml  = EditorPrefs.GetString(UXML_PREF, "");
        var lastPanel = EditorPrefs.GetString(PANEL_PREF, "");
        var lastSprite= EditorPrefs.GetString(SPRITE_PREF, "");

        var uxml = LoadOrPick<VisualTreeAsset>("Select UXML (UpgradeShopPanel)", "uxml", lastUxml);
        if (!uxml) return;

        var panelSettings = LoadOrPick<PanelSettings>("Select Panel Settings", "asset", lastPanel);
        if (!panelSettings) return;

        var frameSprite = LoadOrPick<Sprite>("(Optional) Select frame sprite (framelarge)", "png", lastSprite, optional:true);

        // Create root GO
        var root = new GameObject("ShopUI (UITK)");
        Undo.RegisterCreatedObjectUndo(root, "Build Shop Panel (UITK)");

        // UIDocument
        var uiDoc = root.AddComponent<UIDocument>();
        uiDoc.panelSettings = panelSettings;
        uiDoc.visualTreeAsset = uxml;

        // Controller (create if missing in project)
        var ctrl = root.AddComponent<ShopUIToolkitController>();
        ctrl.toggleKey = KeyCode.Tab;
        ctrl.pauseOnOpen = true;
        ctrl.GetType().GetField("frameSprite", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(ctrl, frameSprite);

        // Try to open immediately so you can see it
        EditorApplication.delayCall += () =>
        {
            var method = typeof(ShopUIToolkitController).GetMethod("SetOpen",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(ctrl, new object[] { true });
            Selection.activeObject = root;
        };

        // Remember selections
        EditorPrefs.SetString(UXML_PREF,  AssetDatabase.GetAssetPath(uxml));
        EditorPrefs.SetString(PANEL_PREF, AssetDatabase.GetAssetPath(panelSettings));
        if (frameSprite) EditorPrefs.SetString(SPRITE_PREF, AssetDatabase.GetAssetPath(frameSprite));
    }

    // ---------- helpers ----------
    private static T LoadOrPick<T>(string title, string ext, string lastPath, bool optional = false) where T : Object
    {
        T asset = null;
        if (!string.IsNullOrEmpty(lastPath))
            asset = AssetDatabase.LoadAssetAtPath<T>(lastPath);

        if (!asset)
        {
            var filter = typeof(T) == typeof(GameObject) ? "Prefab" :
                         typeof(T) == typeof(VisualTreeAsset) ? "VisualTreeAsset" :
                         typeof(T) == typeof(PanelSettings) ? "PanelSettings" :
                         typeof(T) == typeof(Sprite) ? "Sprite" : typeof(T).Name;

            string path = EditorUtility.OpenFilePanel(title, Application.dataPath, "*");
            if (string.IsNullOrEmpty(path))
            {
                if (!optional) EditorUtility.DisplayDialog("Cancelled", $"{title} not selected.", "OK");
                return null;
            }

            if (path.StartsWith(Application.dataPath))
            {
                string rel = "Assets" + path.Substring(Application.dataPath.Length);
                asset = AssetDatabase.LoadAssetAtPath<T>(rel);
                if (!asset)
                {
                    EditorUtility.DisplayDialog("Invalid", $"Could not load {filter} at:\n{rel}", "OK");
                    return null;
                }
                // store last
                EditorPrefs.SetString(
                    typeof(T) == typeof(GameObject) ? PREFAB_PREF :
                    typeof(T) == typeof(VisualTreeAsset) ? UXML_PREF :
                    typeof(T) == typeof(PanelSettings) ? PANEL_PREF :
                    typeof(T) == typeof(Sprite) ? SPRITE_PREF : "ShopUI_Last",
                    rel);
            }
            else
            {
                EditorUtility.DisplayDialog("Outside Assets", "Please pick a file inside your project's Assets folder.", "OK");
                return null;
            }
        }
        return asset;
    }
}
#endif

