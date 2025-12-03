#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// ---------- Attribute to mark required references ----------
[AttributeUsage(AttributeTargets.Field)]
public class MandatoryAttribute : PropertyAttribute { } // you can use [Mandatory] on fields

// ---------- Scene Health Inspector (EditorWindow) ----------
public class SceneHealthInspector : EditorWindow
{
    [Serializable]
    public class Entry
    {
        public string hierarchyPath;     // "Root/Child/Leaf"
        [NonSerialized] public Transform cached; // resolved at runtime
    }

    [Serializable]
    class Profile
    {
        public string sceneName;
        public List<Entry> entries = new List<Entry>();
    }

    List<Entry> _watch = new List<Entry>();
    Vector2 _scroll;
    string _lastProfilePath;

    // Cached types for quick checks (optional)
    Type _tp_vTPC;    // Invector vThirdPersonController
    Type _tp_vInput;  // Invector vThirdPersonInput
    Type _tp_PNudge;  // PathFollowerNudge
    Type _tp_PMixer;  // PathNudgeInputMixer

    [MenuItem("Tools/QA/Scene Health Inspector")]
    public static void Open() => GetWindow<SceneHealthInspector>("Scene Health");

    void OnEnable()
    {
        _tp_vTPC   = Type.GetType("Invector.vCharacterController.vThirdPersonController, Invector.CharacterController");
        _tp_vInput = Type.GetType("Invector.vCharacterController.vThirdPersonInput, Invector.CharacterController");
        _tp_PNudge = Type.GetType("PathFollowerNudge");
        _tp_PMixer = Type.GetType("PathNudgeInputMixer");
    }

    void OnGUI()
    {
        EditorGUILayout.Space();

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Selected", GUILayout.Height(24)))
                AddSelected();

            if (GUILayout.Button("Remove Missing", GUILayout.Height(24)))
                _watch.RemoveAll(e => string.IsNullOrEmpty(e.hierarchyPath));
        }

        EditorGUILayout.Space();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _watch.Count; i++)
        {
            var e = _watch[i];
            using (new GUILayout.VerticalScope("box"))
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{i+1}. {e.hierarchyPath}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Ping", GUILayout.Width(60)))
                    {
                        var tr = Resolve(e);
                        if (tr) { EditorGUIUtility.PingObject(tr.gameObject); Selection.activeObject = tr.gameObject; }
                    }
                    if (GUILayout.Button("X", GUILayout.Width(24))) { _watch.RemoveAt(i); i--; continue; }
                }

                EditorGUI.BeginChangeCheck();
                var trNew = (Transform)EditorGUILayout.ObjectField("Object", Resolve(e), typeof(Transform), true);
                if (EditorGUI.EndChangeCheck())
                {
                    if (trNew)
                    {
                        e.hierarchyPath = GetHierarchyPath(trNew);
                        e.cached = trNew;
                    }
                }

                EditorGUILayout.LabelField("Path", e.hierarchyPath);
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Run Checks", GUILayout.Height(28)))
                RunChecks();

            if (GUILayout.Button("Enable All Disabled Scripts (visible targets only)", GUILayout.Height(28)))
                EnableAllDisabled();
        }

        EditorGUILayout.Space();

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Profile...", GUILayout.Height(22))) SaveProfilePrompt();
            if (GUILayout.Button("Load Profile...", GUILayout.Height(22))) LoadProfilePrompt();
        }

        if (!string.IsNullOrEmpty(_lastProfilePath))
            EditorGUILayout.LabelField("Profile:", _lastProfilePath);
    }

    // ---------- Actions ----------
    void AddSelected()
    {
        foreach (var obj in Selection.objects)
        {
            var go = obj as GameObject;
            if (!go) continue;
            var path = GetHierarchyPath(go.transform);
            if (_watch.Any(w => w.hierarchyPath == path)) continue;
            _watch.Add(new Entry { hierarchyPath = path, cached = go.transform });
        }
    }

    void EnableAllDisabled()
    {
        int count = 0;
        foreach (var e in _watch)
        {
            var tr = Resolve(e);
            if (!tr) continue;

            var comps = tr.GetComponentsInChildren<Component>(true);
            foreach (var c in comps)
            {
                if (c is Behaviour b && !b.enabled)
                {
                    Undo.RecordObject(b, "Enable Behaviour");
                    b.enabled = true;
                    EditorUtility.SetDirty(b);
                    count++;
                }
            }
        }
        Debug.Log($"[SceneHealth] Enabled {count} disabled scripts across watched objects.");
    }

    void RunChecks()
    {
        var sb = new StringBuilder();
        sb.AppendLine("==== Scene Health Report ====");
        sb.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");
        sb.AppendLine($"Watch entries: {_watch.Count}");

        int errors = 0, warns = 0, infos = 0;

        foreach (var e in _watch)
        {
            var tr = Resolve(e);
            sb.AppendLine($"\n> {e.hierarchyPath} {(tr ? "" : "(NOT FOUND)")}");
            if (!tr) { errors++; continue; }

            var comps = tr.GetComponentsInChildren<Component>(true);

            // --- A) Missing scripts
            foreach (var c in comps)
            {
                if (c == null)
                {
                    errors++;
                    sb.AppendLine("  [ERR] Missing Script on child of " + tr.name);
                }
            }

            // --- B) Disabled scripts
            foreach (var c in comps)
            {
                if (c is Behaviour b && !b.enabled)
                {
                    warns++;
                    sb.AppendLine($"  [WARN] Disabled: {b.GetType().Name} on {b.gameObject.name}");
                }
            }

            // --- C) Mandatory refs (and common serialized refs)
            foreach (var c in comps)
            {
                if (!c) continue;
                var t = c.GetType();
                // Check fields: public OR [SerializeField], UnityEngine.Object refs
                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in fields)
                {
                    if (f.FieldType.IsSubclassOf(typeof(UnityEngine.Object)) || f.FieldType == typeof(UnityEngine.Object))
                    {
                        bool isPublic = f.IsPublic;
                        bool isSerialized = isPublic || f.GetCustomAttributes(typeof(SerializeField), true).Length > 0;

                        bool isMandatory =
                            f.GetCustomAttributes(true).Any(a =>
                                a.GetType().Name.Contains("Mandatory", StringComparison.OrdinalIgnoreCase) ||
                                a.GetType().Name.Contains("Required", StringComparison.OrdinalIgnoreCase));

                        if (!isSerialized && !isMandatory) continue;

                        var val = f.GetValue(c) as UnityEngine.Object;
                        if (val == null)
                        {
                            if (isMandatory)
                            {
                                errors++;
                                sb.AppendLine($"  [ERR] {t.Name}.{f.Name} is MANDATORY but not assigned on {((Component)c).gameObject.name}");
                            }
                            else
                            {
                                warns++;
                                sb.AppendLine($"  [WARN] {t.Name}.{f.Name} is null (serialized) on {((Component)c).gameObject.name}");
                            }
                        }
                    }
                }
            }

            // --- D) Quick “player wiring” checks
            var all = comps.Where(c => c != null).ToArray();
            bool hasTPC   = _tp_vTPC   != null && all.Any(c => _tp_vTPC.IsInstanceOfType(c));
            bool hasInput = _tp_vInput != null && all.Any(c => _tp_vInput.IsInstanceOfType(c));
            var nudge     = (_tp_PNudge != null) ? all.FirstOrDefault(c => _tp_PNudge.IsInstanceOfType(c)) as Component : null;
            var mixer     = (_tp_PMixer != null) ? all.FirstOrDefault(c => _tp_PMixer.IsInstanceOfType(c)) as Component : null;

            if (hasTPC)
            {
                if (!mixer)
                {
                    errors++;
                    sb.AppendLine("  [ERR] vThirdPersonController present but PathNudgeInputMixer is MISSING on this player.");
                }
                else if (mixer is Behaviour mb && !mb.enabled)
                {
                    errors++;
                    sb.AppendLine("  [ERR] PathNudgeInputMixer exists but is DISABLED.");
                }
            }

            if (nudge)
            {
                // check key fields via reflection safely
                var prField = _tp_PNudge.GetField("playerRoot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var rfField = _tp_PNudge.GetField("referenceFrame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var pathFld = _tp_PNudge.GetField("path", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                var pr = prField?.GetValue(nudge) as Transform;
                var rf = rfField?.GetValue(nudge) as Transform;
                var pv = pathFld?.GetValue(nudge) as UnityEngine.Object; // WaypointPathVisualizer

                if (!pr) { errors++; sb.AppendLine("  [ERR] PathFollowerNudge.playerRoot is NOT assigned."); }
                if (!rf) { warns++;  sb.AppendLine("  [WARN] PathFollowerNudge.referenceFrame is null (will try Camera.main)."); }
                if (!pv) { warns++;  sb.AppendLine("  [WARN] PathFollowerNudge.path is null (QRC should SetRoute before AP)."); }
            }
        }

        sb.AppendLine($"\nSummary: ERR={errors}  WARN={warns}  INFO={infos}");
        Debug.Log(sb.ToString());
    }

    // ---------- Save / Load ----------
    void SaveProfilePrompt()
    {
        var scene = SceneManager.GetActiveScene().name;
        var path = EditorUtility.SaveFilePanelInProject("Save Scene Health Profile", $"{scene}-HealthProfile", "json", "Save profile as JSON");
        if (string.IsNullOrEmpty(path)) return;
        var prof = new Profile { sceneName = scene, entries = _watch };
        var json = JsonUtility.ToJson(prof, true);
        File.WriteAllText(path, json, Encoding.UTF8);
        AssetDatabase.ImportAsset(path);
        _lastProfilePath = path;
        Debug.Log($"[SceneHealth] Saved profile: {path}");
    }

    void LoadProfilePrompt()
    {
        var path = EditorUtility.OpenFilePanel("Load Scene Health Profile", Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;
        var projPath = ToProjectPath(path);
        var json = File.ReadAllText(path, Encoding.UTF8);
        var prof = JsonUtility.FromJson<Profile>(json);
        if (prof == null) { Debug.LogError("[SceneHealth] Bad profile JSON."); return; }
        _watch = prof.entries ?? new List<Entry>();
        _lastProfilePath = projPath;
        // Resolve now
        foreach (var e in _watch) e.cached = FindByHierarchyPath(e.hierarchyPath);
        Debug.Log($"[SceneHealth] Loaded profile: {projPath}  ({_watch.Count} entries)");
    }

    // ---------- Helpers ----------
    static string GetHierarchyPath(Transform t)
    {
        var stack = new Stack<string>();
        while (t != null) { stack.Push(t.name); t = t.parent; }
        return string.Join("/", stack);
    }

    Transform Resolve(Entry e)
    {
        if (e.cached) return e.cached;
        e.cached = FindByHierarchyPath(e.hierarchyPath);
        return e.cached;
    }

    static Transform FindByHierarchyPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var parts = path.Split('/');
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        Transform cur = null;

        // find root
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == parts[0]) { cur = roots[i].transform; break; }
        }
        if (!cur) return null;

        for (int i = 1; i < parts.Length; i++)
        {
            var name = parts[i];
            Transform next = null;
            for (int c = 0; c < cur.childCount; c++)
            {
                var ch = cur.GetChild(c);
                if (ch.name == name) { next = ch; break; }
            }
            if (!next) return null;
            cur = next;
        }
        return cur;
    }

    static string ToProjectPath(string abs)
    {
        abs = abs.Replace('\\', '/');
        var root = Application.dataPath.Replace('\\', '/');
        if (abs.StartsWith(root))
            return "Assets" + abs.Substring(root.Length);
        return abs;
    }
}
#endif

