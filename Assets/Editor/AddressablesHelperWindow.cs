// Assets/Editor/AddressablesHelperWindow.cs
// MIT – use freely. Tested on Unity 2021+ with Addressables 1.21+.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class AddressablesHelperWindow : EditorWindow
{
    [MenuItem("Tools/Rumble/Addressables Helper")]
    public static void Open()
    {
        GetWindow<AddressablesHelperWindow>("Addressables Helper");
    }

    enum Tab { AddressablesAudit, PrefabAudit, BatchLabeler }
    Tab _tab;

    // ---------- Shared ----------
    AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

    // ---------- Prefab search ----------
    string _nameFilter = "";
    string _componentTypeName = "";
    bool _includeInactiveInSearch = true;
    Vector2 _prefabScroll;
    List<string> _prefabGuids = new();
    List<GameObject> _prefabResults = new();

    // ---------- Batch labeler ----------
    string _targetGroupName = "Prefabs.PlayerVehicles";
    string _labelsCsv = "player";
    bool _markAsAddressable = true;
    Vector2 _batchScroll;
    List<UnityEngine.Object> _batchSelection = new();

    // ---------- Addressables audit ----------
    Vector2 _addrScroll;
    string _addrSearch = "";

    void OnGUI()
    {
        if (Settings == null)
        {
            EditorGUILayout.HelpBox("Addressables settings not found. Create Addressables via Window > Asset Management > Addressables.", MessageType.Warning);
            if (GUILayout.Button("Open Addressables Groups")) EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            return;
        }

        _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Addressables Audit", "Prefab Audit", "Batch Labeler" });
        GUILayout.Space(6);

        switch (_tab)
        {
            case Tab.AddressablesAudit: DrawAddressablesAudit(); break;
            case Tab.PrefabAudit:       DrawPrefabAudit();       break;
            case Tab.BatchLabeler:      DrawBatchLabeler();      break;
        }
    }

    // ================= Addressables Audit =================
    void DrawAddressablesAudit()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            _addrSearch = GUILayout.TextField(_addrSearch, GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.textField);
            if (GUILayout.Button("Groups Window", EditorStyles.toolbarButton, GUILayout.Width(110)))
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
        }

        _addrScroll = EditorGUILayout.BeginScrollView(_addrScroll);

        foreach (var group in Settings.groups.Where(g => g != null && g.ReadOnly == false))
        {
            if (group.entries.Count == 0) continue;

            EditorGUILayout.LabelField(group.Name, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var e in group.entries)
                {
                    var show = string.IsNullOrEmpty(_addrSearch)
                               || (e.address != null && e.address.IndexOf(_addrSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                               || (e.AssetPath != null && e.AssetPath.IndexOf(_addrSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                               || e.labels.Any(l => l.IndexOf(_addrSearch, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (!show) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(System.IO.Path.GetFileNameWithoutExtension(e.AssetPath)), GUILayout.Width(220));
                        EditorGUILayout.SelectableLabel(e.address, GUILayout.Height(16));
                        GUILayout.Label("Labels: " + string.Join(", ", e.labels), GUILayout.MinWidth(120));
                        if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(e.AssetPath);
                            EditorGUIUtility.PingObject(obj);
                            Selection.activeObject = obj;
                        }
                    }
                }
            }
            GUILayout.Space(8);
        }

        EditorGUILayout.EndScrollView();
    }

    // ================= Prefab Audit =================
    void DrawPrefabAudit()
    {
        EditorGUILayout.LabelField("Search Prefabs", EditorStyles.boldLabel);
        _nameFilter = EditorGUILayout.TextField(new GUIContent("Name Contains", "Optional partial name match"), _nameFilter);
        _componentTypeName = EditorGUILayout.TextField(new GUIContent("Component Type", "e.g., vThirdPersonController, vShooterMeleeInput, BoxCollider"), _componentTypeName);
        _includeInactiveInSearch = EditorGUILayout.ToggleLeft("Include Inactive Children (for GetComponentInChildren)", _includeInactiveInSearch);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan Project"))
            {
                ScanPrefabs();
            }
            if (GUILayout.Button("Select All"))
            {
                Selection.objects = _prefabResults.Where(p => p).ToArray();
            }
        }

        if (_prefabResults.Count == 0)
        {
            EditorGUILayout.HelpBox("No results. Click 'Scan Project' to search.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Results ({_prefabResults.Count})", EditorStyles.boldLabel);
        _prefabScroll = EditorGUILayout.BeginScrollView(_prefabScroll);

        foreach (var prefab in _prefabResults)
        {
            if (prefab == null) continue;
            var path = AssetDatabase.GetAssetPath(prefab);
            int missingCount = CountMissingScripts(prefab);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("►", GUILayout.Width(24)))
                {
                    EditorGUIUtility.PingObject(prefab);
                    Selection.activeObject = prefab;
                }

                EditorGUILayout.LabelField(prefab.name, GUILayout.Width(230));
                EditorGUILayout.SelectableLabel(path, GUILayout.Height(16));

                if (missingCount > 0)
                    GUILayout.Label($"Missing Scripts: {missingCount}", EditorStyles.boldLabel, GUILayout.Width(150));
                else
                    GUILayout.Label("OK", GUILayout.Width(60));
            }
        }

        EditorGUILayout.EndScrollView();
    }

    void ScanPrefabs()
    {
        _prefabResults.Clear();
        _prefabGuids = AssetDatabase.FindAssets("t:Prefab").ToList();

        Type compType = null;
        if (!string.IsNullOrWhiteSpace(_componentTypeName))
            compType = FindTypeByName(_componentTypeName.Trim());

        foreach (var guid in _prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(_nameFilter) &&
                System.IO.Path.GetFileNameWithoutExtension(path).IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!go) continue;

            if (compType != null)
            {
                var has = go.GetComponentInChildren(compType, _includeInactiveInSearch);
                if (!has) continue;
            }

            _prefabResults.Add(go);
        }
    }

    int CountMissingScripts(GameObject prefab)
    {
        // Instantiate temporarily to check full hierarchy safely (no editing scene)
        var temp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        int count = 0;
        try
        {
            count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(temp);
        }
        finally
        {
            if (temp) UnityEngine.Object.DestroyImmediate(temp);
        }
        return count;
    }

    static Type FindTypeByName(string typeName)
    {
        // Try FullName first, then simple Name.
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(typeName);
            if (t != null) return t;
        }
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
            if (t != null) return t;
        }
        return null;
    }

    // ================= Batch Labeler =================
    void DrawBatchLabeler()
    {
        EditorGUILayout.HelpBox("Select any number of Prefab assets in the Project window, then use the controls below to label / mark as Addressable in bulk.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Current Selection"))
            {
                _batchSelection = Selection.objects.Where(o => o != null).ToList();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear"))
            {
                _batchSelection.Clear();
            }
        }

        EditorGUILayout.LabelField($"Selected Objects: {_batchSelection.Count}");
        _batchScroll = EditorGUILayout.BeginScrollView(_batchScroll, GUILayout.MinHeight(80), GUILayout.MaxHeight(180));
        foreach (var o in _batchSelection)
        {
            EditorGUILayout.ObjectField(o, typeof(UnityEngine.Object), false);
        }
        EditorGUILayout.EndScrollView();

        _markAsAddressable = EditorGUILayout.ToggleLeft("Mark as Addressable", _markAsAddressable);
        _targetGroupName = EditorGUILayout.TextField(new GUIContent("Target Group"), _targetGroupName);
        _labelsCsv = EditorGUILayout.TextField(new GUIContent("Labels (CSV)"), _labelsCsv);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply to Selected"))
            {
                ApplyBatch();
            }
            if (GUILayout.Button("Open Addressables Groups"))
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            }
        }
    }

    void ApplyBatch()
    {
        if (_batchSelection.Count == 0)
        {
            ShowNotification(new GUIContent("No selection."));
            return;
        }

 
   // Get or create the target group and attach a BundledAssetGroupSchema
  // Get or create the target group and attach a BundledAssetGroupSchema
var group = CreateOrGetGroupCompat(Settings, _targetGroupName);
if (group == null)
{
    
 

    // Ensure the group has a BundledAssetGroupSchema
    var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
// Robust defaults that work across versions
if (bundled != null)
{
    if (bundled.BuildPath != null) bundled.BuildPath.SetVariableByName(Settings, "LocalBuildPath");
    if (bundled.LoadPath  != null) bundled.LoadPath.SetVariableByName(Settings, "LocalLoadPath");

    // If your project groups are for scenes, feel free to flip this to PackSeparately
    bundled.BundleMode  = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
    bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;

    EditorUtility.SetDirty(bundled);
}


    Debug.Log($"[AddressablesHelper] Created group: {_targetGroupName}");
}

//patch
     var labels = _labelsCsv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(l => l.Trim())
                       .Where(l => !string.IsNullOrEmpty(l))
                       .Distinct()
                       .ToList();

// Version-safe labels API
var existingLabels = new HashSet<string>(Settings.GetLabels() ?? Enumerable.Empty<string>());
foreach (var label in labels)
{
    if (!existingLabels.Contains(label))
    {
        Settings.AddLabel(label);
        existingLabels.Add(label);
    }
}






        int processed = 0;

        foreach (var o in _batchSelection)
        {
            var path = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(path)) continue;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) continue;

            AddressableAssetEntry entry = Settings.FindAssetEntry(guid);

            if (_markAsAddressable)
            {
                if (entry == null)
                    entry = Settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);

                // use filename (no extension) as default address
                var defaultAddress = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(entry.address))
                    entry.SetAddress(defaultAddress);

                foreach (var lab in labels)
                    entry.SetLabel(lab, true);

                processed++;
            }
            else
            {
                if (entry != null)
                {
                    Settings.RemoveAssetEntry(guid);
                    processed++;
                }
            }
        }

        Settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AddressablesHelper] Processed {processed} asset(s).");
        ShowNotification(new GUIContent($"Processed {processed} asset(s)."));
    }



AddressableAssetGroup CreateOrGetGroupCompat(AddressableAssetSettings settings, string groupName)
{
    var existing = settings.groups.FirstOrDefault(g => g != null && g.Name == groupName);
    if (existing != null) return existing;

    // Find CreateGroup overloads across Addressables versions.
    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
    var methods = typeof(AddressableAssetSettings).GetMethods(flags)
        .Where(m => m.Name == "CreateGroup")
        .ToArray();

    // Try 6-arg: (string name, bool setAsDefault, bool readOnly, bool postEvent, IEnumerable<AddressableAssetGroupSchema> schemas, Type[] types)
    var sixArg = methods.FirstOrDefault(m => m.GetParameters().Length == 6);
    if (sixArg != null)
    {
        var groupObj = sixArg.Invoke(settings, new object[] { groupName, false, false, false, null, null });
        return (AddressableAssetGroup)groupObj;
    }

    // Try 5-arg: (string name, bool setAsDefault, bool readOnly, bool postEvent, IEnumerable<AddressableAssetGroupSchema> schemas)
    var fiveArg = methods.FirstOrDefault(m => m.GetParameters().Length == 5);
    if (fiveArg != null)
    {
        var groupObj = fiveArg.Invoke(settings, new object[] { groupName, false, false, false, null });
        return (AddressableAssetGroup)groupObj;
    }

    // Fallback: if no compatible overloads found, throw a clear error.
    throw new InvalidOperationException("[AddressablesHelper] Could not find a compatible Addressables CreateGroup overload.");
}










}
#endif
