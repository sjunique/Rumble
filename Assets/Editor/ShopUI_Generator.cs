#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;

public static class ShopUI_Generator
{
    private const string RootDir = "Assets/UI/Shop";
    private const string UxmlPath = RootDir + "/UpgradeShopPanel.uxml";
    private const string UssPath  = RootDir + "/UpgradeShopPanel.uss";

    [MenuItem("Tools/UI/Create Default Shop (UXML + USS + GameObject)")]
    public static void CreateDefaultShop()
    {
        // 1) Ensure folder
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/UI/Shop"))
            AssetDatabase.CreateFolder("Assets/UI", "Shop");

        // 2) Write USS (only if missing)
        if (!File.Exists(UssPath))
        {
            File.WriteAllText(UssPath, DefaultUSS);
            Debug.Log($"[ShopUI] Wrote USS → {UssPath}");
        }

        // 3) Write UXML (references USS via <Style src="UpgradeShopPanel.uss" />)
        if (!File.Exists(UxmlPath))
        {
            File.WriteAllText(UxmlPath, DefaultUXML);
            Debug.Log($"[ShopUI] Wrote UXML → {UxmlPath}");
        }

        AssetDatabase.ImportAsset(UssPath);
        AssetDatabase.ImportAsset(UxmlPath);

        // 4) Find/create PanelSettings
        string[] panelGuids = AssetDatabase.FindAssets("t:PanelSettings");
        PanelSettings panelSettings = null;
        if (panelGuids.Length > 0)
        {
            panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(AssetDatabase.GUIDToAssetPath(panelGuids[0]));
        }
        else
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "MainPanelSettings";
            AssetDatabase.CreateAsset(panelSettings, "Assets/UI/Shop/MainPanelSettings.asset");
            Debug.Log("[ShopUI] Created Panel Settings at Assets/UI/Shop/MainPanelSettings.asset");
        }

        // 5) Load assets
        var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

        if (!vta || !panelSettings)
        {
            EditorUtility.DisplayDialog("ShopUI", "Failed to load UXML or PanelSettings.", "OK");
            return;
        }

        // 6) Create scene object
        var root = new GameObject("ShopUI (UITK)");
        Undo.RegisterCreatedObjectUndo(root, "Create ShopUI (UITK)");

        var doc = root.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        doc.visualTreeAsset = vta;

        // Attach controller (optional; you already have this script in your project)
        var ctrl = root.AddComponent<ShopUIToolkitController>();
        ctrl.pauseOnOpen = true;
        ctrl.toggleKey = KeyCode.Tab;

        // Try to open immediately so you can see it
        EditorApplication.delayCall += () =>
        {
            var method = typeof(ShopUIToolkitController).GetMethod("SetOpen",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(ctrl, new object[] { true });
            Selection.activeObject = root;
        };

        Debug.Log("[ShopUI] Shop panel created. Press Play and hit Tab to toggle.");
    }

    // ---------- File contents ----------

    private static readonly string DefaultUXML =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<UXML xmlns=""UnityEngine.UIElements"">
  <Style src=""UpgradeShopPanel.uss"" />
  <VisualElement name=""root"" class=""shop-root"">
    <VisualElement name=""shop-panel"" class=""shop-panel"">
      <ScrollView name=""shop-scroll"" class=""shop-scroll"" horizontal-scrolling=""false"" vertical-scrolling=""true"">
        <Button name=""row-shield"" class=""shop-row"">
          <Label class=""row-label"" text=""Shield"" />
          <Label class=""row-cost""  text=""50"" />
          <Label class=""row-state"" text=""Lv 0/3"" />
        </Button>
        <Button name=""row-scuba"" class=""shop-row"">
          <Label class=""row-label"" text=""Scuba"" />
          <Label class=""row-cost""  text=""50"" />
          <Label class=""row-state"" text=""Lv 0/3"" />
        </Button>
      </ScrollView>
    </VisualElement>
  </VisualElement>
</UXML>";

    private static readonly string DefaultUSS =
@"/* Fill screen, center the panel */
.shop-root {
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
}

/* The ornate frame panel */
.shop-panel {
  width: 900px;
  height: 600px;

  /* IMPORTANT: assign the frame sprite in code OR use USS resource()
     If you name your sprite 'framelarge' and put it in a Resources folder, you can do:
     background-image: resource(""framelarge"");
     -unity-background-scale-mode: nine-sliced;
  */

  padding: 48px;                /* inset so rows don't overlap the border */
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
}

/* ScrollView fills the padded area */
.shop-scroll {
  flex-grow: 1;
  flex-shrink: 1;
}

/* Button rows */
.shop-row {
  height: 56px;
  margin-bottom: 12px;
  padding: 8px 12px;
  box-sizing: border-box;

  display: flex;
  flex-direction: row;
  justify-content: flex-start;  /* left align */
  align-items: flex-start;      /* top align */

  background-color: rgba(30, 40, 60, 0.92);
  border-radius: 8px;
}

.row-label {
  flex-grow: 1;
  unity-text-align: upper-left;
  font-size: 20px;
  color: white;
}

.row-cost, .row-state {
  width: 90px;
  unity-text-align: upper-left;
  font-size: 20px;
  color: #CFE3FF;
}

.shop-row:hover  { background-color: rgba(50, 70, 110, 0.96); }
.shop-row:active { background-color: rgba(20, 30, 50, 1.00); }
";
}
#endif

