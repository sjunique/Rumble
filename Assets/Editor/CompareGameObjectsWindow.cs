// Assets/Editor/ComparePlayers.cs
// Compare two GameObjects (optionally including children) and print differences.
// Works for prefab instances too. Open via: Tools > Compare GameObjects

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CompareGameObjectsWindow : EditorWindow
{
    GameObject a, b;
    bool includeChildren = true;
    bool showOnlyDiffs = true;

    [MenuItem("Tools/Compare GameObjects")]
    public static void Open() => GetWindow<CompareGameObjectsWindow>("Compare GameObjects");

    void OnGUI()
    {
        GUILayout.Label("Drag two player objects (scene objects or prefab instances):", EditorStyles.boldLabel);
        a = (GameObject)EditorGUILayout.ObjectField("Object A", a, typeof(GameObject), true);
        b = (GameObject)EditorGUILayout.ObjectField("Object B", b, typeof(GameObject), true);
        includeChildren = EditorGUILayout.ToggleLeft("Include children (by hierarchy path)", includeChildren);
        showOnlyDiffs   = EditorGUILayout.ToggleLeft("Show only differences", showOnlyDiffs);

        using (new EditorGUI.DisabledScope(a == null || b == null))
        {
            if (GUILayout.Button("Compare")) Compare(a, b, includeChildren, showOnlyDiffs);
        }
    }

    // --- Core ---
    static void Compare(GameObject goA, GameObject goB, bool recurse, bool onlyDiffs)
    {
        var mapA = Snapshot(goA, recurse);
        var mapB = Snapshot(goB, recurse);

        var allPaths = new SortedSet<string>(mapA.Keys, StringComparer.Ordinal);
        foreach (var p in mapB.Keys) allPaths.Add(p);

        var lines = new List<string>();
        lines.Add($"=== Compare '{goA.name}' vs '{goB.name}' (children:{recurse}) ===");

        foreach (var path in allPaths)
        {
            var hasA = mapA.TryGetValue(path, out var compsA);
            var hasB = mapB.TryGetValue(path, out var compsB);

            if (!hasA || !hasB)
            {
                lines.Add($"[PATH] {path}: {(hasA ? "only in A" : "")}{(!hasA && !hasB ? "" : hasB ? "only in B" : "")}");
                continue;
            }

            // Compare component sets & order
            var typesA = compsA.Select(c => c.typeName).ToList();
            var typesB = compsB.Select(c => c.typeName).ToList();
            bool sameOrder = typesA.SequenceEqual(typesB);

            if (!sameOrder || !typesA.OrderBy(x=>x).SequenceEqual(typesB.OrderBy(x=>x)))
            {
                lines.Add($"[PATH] {path}");
                lines.Add($"  Components A: {string.Join(", ", typesA)}");
                lines.Add($"  Components B: {string.Join(", ", typesB)}");
            }

            // Compare per-component (by index to respect order)
            int count = Math.Max(compsA.Count, compsB.Count);
            for (int i = 0; i < count; i++)
            {
                var ca = i < compsA.Count ? compsA[i] : null;
                var cb = i < compsB.Count ? compsB[i] : null;

                if (ca == null || cb == null)
                {
                    lines.Add($"  [{i}] {(ca==null?"<none>":ca.typeName)} vs {(cb==null?"<none>":cb.typeName)}");
                    continue;
                }

                if (ca.typeName != cb.typeName)
                {
                    lines.Add($"  [{i}] TYPE differs: {ca.typeName} vs {cb.typeName}");
                    continue;
                }

                // Behaviour.enabled difference
                if (ca.enabledStr != cb.enabledStr)
                    lines.Add($"  [{i}] {ca.typeName}.enabled A={ca.enabledStr} B={cb.enabledStr}");

                // Serialized fields diff
                var diffs = DiffSerialized(ca.serializedProps, cb.serializedProps);
                if (diffs.Count > 0 || !onlyDiffs)
                {
                    lines.Add($"  [{i}] {ca.typeName} fields{(diffs.Count==0?" (no diffs)":"")}:");
                    if (diffs.Count == 0)
                        continue;

                    foreach (var d in diffs)
                        lines.Add($"       - {d.name}: A={Truncate(d.a)} | B={Truncate(d.b)}");
                }
            }
        }

        var report = string.Join("\n", lines);
        Debug.Log(report);
        EditorGUIUtility.systemCopyBuffer = report;
        EditorUtility.DisplayDialog("Compare GameObjects", "Report printed to Console and copied to clipboard.", "OK");
    }

    static string Truncate(string s) => s.Length <= 160 ? s : s.Substring(0,157) + "...";

    class CompInfo
    {
        public string typeName;
        public string enabledStr; // "true"/"false"/"-" if not Behaviour
        public List<(string name, string value)> serializedProps;
    }

    static Dictionary<string, List<CompInfo>> Snapshot(GameObject root, bool recurse)
    {
        var dict = new Dictionary<string, List<CompInfo>>(StringComparer.Ordinal);
        var transforms = recurse ? root.GetComponentsInChildren<Transform>(true) : new[] { root.transform };

        foreach (var t in transforms)
        {
            string path = GetPath(root.transform, t);
            var list = new List<CompInfo>();
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) { list.Add(new CompInfo { typeName = "<Missing Script>", enabledStr = "-", serializedProps = new List<(string,string)>() }); continue; }

                var so = new SerializedObject(c);
                var props = new List<(string, string)>();

                var iter = so.GetIterator();
                bool enterChildren = true;
                while (iter.NextVisible(enterChildren))
                {
                    // Skip very noisy or transient props
                    if (iter.name == "m_Script") { enterChildren = false; continue; }
                    props.Add((iter.propertyPath, PropertyValue(iter)));
                    enterChildren = false;
                }

                var info = new CompInfo
                {
                    typeName = c.GetType().Name,
                    enabledStr = (c is Behaviour b) ? b.enabled.ToString().ToLower() : "-",
                    serializedProps = props
                };
                list.Add(info);
            }
            dict[path] = list;
        }
        return dict;
    }

    static string GetPath(Transform root, Transform t)
    {
        if (t == root) return t.name;
        var stack = new Stack<string>();
        var cur = t;
        while (cur && cur != root) { stack.Push(cur.name); cur = cur.parent; }
        stack.Push(root.name);
        return string.Join("/", stack.ToArray());
    }

  static string PropertyValue(SerializedProperty p)
{
    try
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Boolean: return p.boolValue ? "true" : "false";
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Float:   return p.floatValue.ToString("0.###");
            case SerializedPropertyType.String:  return p.stringValue ?? "";
            case SerializedPropertyType.Enum:
            {
                // enumValueIndex can be -1 or > names.Length in some cases – guard it
                var idx = p.enumValueIndex;
                var names = p.enumDisplayNames ?? System.Array.Empty<string>();
                if (idx >= 0 && idx < names.Length) return names[idx];
                // fallbacks
                if (names.Length > 0) return $"<{idx}>";
                return p.intValue.ToString(); // raw value
            }
            case SerializedPropertyType.ObjectReference:
                return p.objectReferenceValue ? p.objectReferenceValue.name : "None";
            case SerializedPropertyType.Color:     return p.colorValue.ToString();
            case SerializedPropertyType.Vector2:   return p.vector2Value.ToString();
            case SerializedPropertyType.Vector3:   return p.vector3Value.ToString();
            case SerializedPropertyType.Vector4:   return p.vector4Value.ToString();
            case SerializedPropertyType.Rect:      return p.rectValue.ToString();
            case SerializedPropertyType.Bounds:    return p.boundsValue.ToString();
#if UNITY_2020_1_OR_NEWER
            case SerializedPropertyType.Quaternion:return p.quaternionValue.eulerAngles.ToString();
            case SerializedPropertyType.ExposedReference:
                return p.exposedReferenceValue ? p.exposedReferenceValue.name : "None";
            case SerializedPropertyType.ManagedReference:
                return p.managedReferenceFullTypename ?? "None";
#endif
            case SerializedPropertyType.ArraySize: return p.arraySize.ToString();
            default:
                // Don’t explode on unsupported/opaque types
                return $"<{p.propertyType}>";
        }
    }
    catch
    {
        // Absolute last-resort guard so the diff tool never crashes
        return "<unavailable>";
    }
}

    class FieldDiff { public string name; public string a; public string b; }

    static List<FieldDiff> DiffSerialized(List<(string name, string value)> A, List<(string name, string value)> B)
    {
        var da = A.ToDictionary(x => x.name, x => x.value);
        var db = B.ToDictionary(x => x.name, x => x.value);
        var keys = new SortedSet<string>(da.Keys);
        foreach (var k in db.Keys) keys.Add(k);

        var diffs = new List<FieldDiff>();
        foreach (var k in keys)
        {
            da.TryGetValue(k, out var va);
            db.TryGetValue(k, out var vb);
            if (va != vb)
                diffs.Add(new FieldDiff { name = k, a = va ?? "<missing>", b = vb ?? "<missing>" });
        }
        return diffs;
    }
}
#endif

