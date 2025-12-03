#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public static class ShopUI_FromDatabase
{
    private const string RootDir  = "Assets/UI/Shop";
    private const string UxmlPath = RootDir + "/UpgradeShopPanel.auto.uxml";
    private const string UssPath  = RootDir + "/UpgradeShopPanel.auto.uss";

    [MenuItem("Tools/UI/Generate Shop UXML from Database")]
    public static void GenerateFromDB()
    {
        var db = PickDatabase();
        if (!db) return;

        EnsureFolder("Assets/UI");
        EnsureFolder(RootDir);

        if (!File.Exists(UssPath))
            File.WriteAllText(UssPath, DefaultUSS);

        // Get all UpgradeDef entries
        var defs = ExtractDefs(db);
        if (defs == null || defs.Count == 0)
        {
            EditorUtility.DisplayDialog("Shop UI", "UpgradeDatabase has no UpgradeDef entries I can read.", "OK");
            return;
        }

        // Build UXML
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<UXML xmlns=""UnityEngine.UIElements"">");
        sb.AppendLine($@"  <Style src=""{Path.GetFileName(UssPath)}"" />");
        sb.AppendLine(@"  <VisualElement name=""root"" class=""shop-root"">");
        sb.AppendLine(@"    <VisualElement name=""shop-panel"" class=""shop-panel"">");
        sb.AppendLine(@"      <ScrollView name=""shop-scroll"" class=""shop-scroll"" horizontal-scrolling=""false"" vertical-scrolling=""true"">");

        foreach (var def in defs)
        {
            if (!def) continue;
            string display = def.name;
            string safe = display.Trim().Replace(" ", "").ToLowerInvariant();

            sb.AppendLine($@"        <Button name=""row-{safe}"" class=""shop-row"">");
            sb.AppendLine($@"          <Label class=""row-label"" text=""{Escape(display)}"" />");
            sb.AppendLine($@"          <Label class=""row-cost""  text=""-"" />");
            sb.AppendLine($@"          <Label class=""row-state"" text=""-"" />");
            sb.AppendLine(@"        </Button>");
        }

        sb.AppendLine(@"      </ScrollView>");
        sb.AppendLine(@"    </VisualElement>");
        sb.AppendLine(@"  </VisualElement>");
        sb.AppendLine(@"</UXML>");

        File.WriteAllText(UxmlPath, sb.ToString());

        AssetDatabase.ImportAsset(UssPath);
        AssetDatabase.ImportAsset(UxmlPath);

        // Find or create PanelSettings
        var panel = FindOrCreatePanelSettings();

        // Spawn scene object
        var go = new GameObject("ShopUI (UITK)");
        Undo.RegisterCreatedObjectUndo(go, "Create ShopUI (UITK)");

        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = panel;
        doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

        var ctrl = go.AddComponent<NewShopUIToolkitController>();
        ctrl.toggleKey = KeyCode.Tab;
        ctrl.pauseOnOpen = true;

        var fDb = typeof(NewShopUIToolkitController).GetField("database",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (fDb != null) fDb.SetValue(ctrl, db);

        EditorApplication.delayCall += () =>
        {
            var method = typeof(NewShopUIToolkitController).GetMethod("SetOpen",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(ctrl, new object[] { true });
            Selection.activeObject = go;
        };

        EditorUtility.DisplayDialog("Shop UI",
            $"Generated:\n• {UxmlPath}\n• {UssPath}\nCreated 'ShopUI (UITK)' in scene.\nPress Play and hit Tab to toggle.", "Nice!");
    }

    // ---- helpers ----

    private static List<UpgradeDef> ExtractDefs(UpgradeDatabase db)
    {
        var list = new List<UpgradeDef>();
        var t = db.GetType();
        var fields = t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            var ft = f.FieldType;
            if (ft.IsArray && ft.GetElementType() == typeof(UpgradeDef))
            {
                var arr = f.GetValue(db) as UpgradeDef[];
                if (arr != null) list.AddRange(arr);
            }
            else if (typeof(IList).IsAssignableFrom(ft))
            {
                var iList = f.GetValue(db) as IList;
                if (iList != null && iList.Count > 0 && iList[0] is UpgradeDef)
                {
                    foreach (var item in iList) list.Add(item as UpgradeDef);
                }
            }
        }
        return list;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string accum = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = accum + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(accum, parts[i]);
            accum = next;
        }
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"","&quot;");
    }

    private static PanelSettings FindOrCreatePanelSettings()
    {
        var guids = AssetDatabase.FindAssets("t:PanelSettings");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
        }
        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.name = "MainPanelSettings";
        AssetDatabase.CreateAsset(ps, RootDir + "/MainPanelSettings.asset");
        return ps;
    }

    private static UpgradeDatabase PickDatabase()
    {
        // Try selected object first
        var sel = Selection.activeObject as UpgradeDatabase;
        if (sel) return sel;

        // Try to find in project
        var guids = AssetDatabase.FindAssets("t:UpgradeDatabase");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(path);
        }

        // Ask user
        var picked = EditorUtility.OpenFilePanel("Select UpgradeDatabase asset", Application.dataPath, "asset");
        if (string.IsNullOrEmpty(picked)) return null;
        if (!picked.StartsWith(Application.dataPath))
        {
            EditorUtility.DisplayDialog("Shop UI", "Pick a file inside the project's Assets folder.", "OK");
            return null;
        }
        string rel = "Assets" + picked.Substring(Application.dataPath.Length);
        return AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(rel);
    }

    private static readonly string DefaultUSS =
@"/* Fill screen, center the panel */
.shop-root {
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
}

/* Frame panel */
.shop-panel {
  width: 900px;
  height: 600px;
  padding: 48px;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
}

.shop-scroll { flex-grow: 1; flex-shrink: 1; }

.shop-row {
  height: 56px;
  margin-bottom: 12px;
  padding: 8px 12px;
  box-sizing: border-box;
  display: flex;
  flex-direction: row;
  justify-content: flex-start;
  align-items: flex-start;
  background-color: rgba(30, 40, 60, 0.92);
  border-radius: 8px;
}

.row-label { flex-grow: 1; unity-text-align: upper-left; font-size: 20px; color: white; }
.row-cost, .row-state { width: 90px; unity-text-align: upper-left; font-size: 20px; color: #CFE3FF; }

.shop-row:hover  { background-color: rgba(50, 70, 110, 0.96); }
.shop-row:active { background-color: rgba(20, 30, 50, 1.00); }
";
}
#endif
