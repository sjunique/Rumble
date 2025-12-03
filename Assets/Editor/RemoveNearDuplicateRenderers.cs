#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class RemoveNearDuplicateRenderers : EditorWindow
{
    [System.Serializable]
    public class NameRule
    {
        public string contains;
        public int scoreDelta;
    }

    [Header("Match Thresholds")]
    float posEpsilon = 0.03f;
    float rotEpsilonDeg = 1.0f;
    float scaleEpsilon = 0.02f;

    [Header("Filters (optional)")]
    string[] nameFilters = new[] { "Tree", "Grass", "Bush", "Mystical", "Leaves" };
    bool requireFilterMatch = false;

    [Header("Heuristics (higher = keep)")]
    int lodGroupBonus = 100;
    int childCountWeight = 2;
    int hasMeshRendererBonus = 5;

    // tweak name scoring to prefer/avoid certain variants
    List<NameRule> nameRules = new List<NameRule>
    {
        new NameRule { contains = "Leaves",  scoreDelta = -30 },
        new NameRule { contains = "_small",  scoreDelta = -10 },
        new NameRule { contains = "_LODx",   scoreDelta = -5 },  // generic
        new NameRule { contains = "Billboard", scoreDelta = -15 },
        new NameRule { contains = "_Sp_",    scoreDelta =  5 },  // Gaia spawned
        new NameRule { contains = "Mystical_Tree", scoreDelta =  10 },
    };

    bool dryRun = true;
    bool onlyDisable = false;
    bool groupByRoundedGrid = true;
    Transform trashParent;

    [MenuItem("Tools/Scene Cleanup/Remove Near-duplicate Renderers")]
    public static void Open() => GetWindow<RemoveNearDuplicateRenderers>("Remove Duplicates");

    void OnGUI()
    {
        GUILayout.Label("Near-duplicate Cleanup", EditorStyles.boldLabel);

        posEpsilon = EditorGUILayout.Slider(new GUIContent("Position Epsilon (m)"), posEpsilon, 0.001f, 0.2f);
        rotEpsilonDeg = EditorGUILayout.Slider(new GUIContent("Rotation Epsilon (deg)"), rotEpsilonDeg, 0.1f, 5f);
        scaleEpsilon = EditorGUILayout.Slider(new GUIContent("Scale Epsilon (m)"), scaleEpsilon, 0.001f, 0.2f);

        EditorGUILayout.Space();
        requireFilterMatch = EditorGUILayout.ToggleLeft(new GUIContent("Require name to match filters"), requireFilterMatch);
        if (requireFilterMatch)
        {
            EditorGUILayout.HelpBox("Only objects whose names contain ANY of these filters will be considered.", MessageType.Info);
        }
        EditorGUILayout.LabelField("Name Filters (comma-separated):");
        var nf = EditorGUILayout.TextField(string.Join(",", nameFilters));
        nameFilters = nf.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

        EditorGUILayout.Space();
        GUILayout.Label("Keep/Remove Scoring", EditorStyles.boldLabel);
        lodGroupBonus = EditorGUILayout.IntField("LODGroup Bonus", lodGroupBonus);
        childCountWeight = EditorGUILayout.IntField("ChildCount Weight", childCountWeight);
        hasMeshRendererBonus = EditorGUILayout.IntField("Has MeshRenderer Bonus", hasMeshRendererBonus);

        EditorGUILayout.Space();
        GUILayout.Label("Name Rules", EditorStyles.boldLabel);
        for (int i = 0; i < nameRules.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            nameRules[i].contains = EditorGUILayout.TextField(nameRules[i].contains);
            nameRules[i].scoreDelta = EditorGUILayout.IntField(nameRules[i].scoreDelta, GUILayout.Width(80));
            if (GUILayout.Button("X", GUILayout.Width(22))) { nameRules.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Add Rule")) nameRules.Add(new NameRule { contains = "", scoreDelta = 0 });

        EditorGUILayout.Space();
        groupByRoundedGrid = EditorGUILayout.ToggleLeft("Group by Rounded Grid (fast binning)", groupByRoundedGrid);
        dryRun = EditorGUILayout.ToggleLeft("Dry Run (preview only)", dryRun);
        onlyDisable = EditorGUILayout.ToggleLeft("Move to Trash Parent instead of Destroy", onlyDisable);
        trashParent = (Transform)EditorGUILayout.ObjectField("Trash Parent (optional)", trashParent, typeof(Transform), true);

        EditorGUILayout.Space();
        if (GUILayout.Button("SCAN & (Dry) RUN"))
        {
            CleanScene();
        }
    }

    void CleanScene()
    {
        var renderers = GameObject.FindObjectsOfType<Renderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy) // visible ones
            .ToArray();

        // Optional filter by names
        if (requireFilterMatch && nameFilters.Length > 0)
        {
            renderers = renderers.Where(r => nameFilters.Any(f => r.name.Contains(f))).ToArray();
        }

        // Bin by rounded position to reduce pair checks
        var buckets = new Dictionary<Vector3Int, List<Transform>>();
        foreach (var r in renderers)
        {
            var t = r.transform;
            if (t == null) continue;
            var p = t.position;
            var key = groupByRoundedGrid
                ? new Vector3Int(
                    Mathf.RoundToInt(p.x / posEpsilon),
                    Mathf.RoundToInt(p.y / posEpsilon),
                    Mathf.RoundToInt(p.z / posEpsilon))
                : new Vector3Int(0, 0, 0);

            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Transform>();
            list.Add(t);
        }

        int groups = 0, pairs = 0, removed = 0;

        Undo.SetCurrentGroupName("Remove Near-duplicate Renderers");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var kv in buckets)
        {
            var list = kv.Value;
            if (list.Count < 2) continue;
            groups++;

            // Work on a local copy because we may remove entries
            var candidates = new List<Transform>(list);

            for (int i = 0; i < candidates.Count; i++)
            {
                var a = candidates[i]; if (!a) continue;

                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var b = candidates[j]; if (!b) continue;

                    if (IsNearDuplicate(a, b))
                    {
                        pairs++;
                        var keep = Score(a) >= Score(b) ? a : b;
                        var drop = keep == a ? b : a;

                        Debug.LogWarning($"[Duplicate] KEEP: {keep.name}  DROP: {drop.name}  at {keep.position}");

                        if (!dryRun)
                        {
                            if (onlyDisable)
                            {
                                if (trashParent)
                                {
                                    Undo.SetTransformParent(drop, trashParent, "Move duplicate to trash");
                                }
                                drop.gameObject.SetActive(false);
                                Undo.RegisterCompleteObjectUndo(drop.gameObject, "Disable duplicate");
                            }
                            else
                            {
                                Undo.DestroyObjectImmediate(drop.gameObject);
                            }
                            removed++;

                            // prevent re-matching the same 'drop' with others
                            candidates[j] = null;
                        }
                    }
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        var msg = $"Buckets: {buckets.Count}, Groups: {groups}, Pairs found: {pairs}, {(dryRun ? "Would remove" : "Removed")} {removed}.";
        Debug.Log($"[RemoveNearDuplicateRenderers] {msg}");
        EditorUtility.DisplayDialog("Near-duplicate Cleanup", msg, "OK");
    }

    bool IsNearDuplicate(Transform a, Transform b)
    {
        if (!a || !b) return false;

        // Position
        if ((a.position - b.position).sqrMagnitude > (posEpsilon * posEpsilon)) return false;

        // Rotation
        if (Quaternion.Angle(a.rotation, b.rotation) > rotEpsilonDeg) return false;

        // Scale (approx by lossyScale diff length)
        if ((a.lossyScale - b.lossyScale).magnitude > scaleEpsilon) return false;

        return true;
    }

    int Score(Transform t)
    {
        if (!t) return int.MinValue;

        int s = 0;

        // Prefer full LODGroup roots (whole tree)
        if (t.GetComponent<LODGroup>()) s += lodGroupBonus;

        // Prefer transforms that look like whole prefabs (more children)
        s += t.childCount * childCountWeight;

        // MeshRenderer presence
        if (t.GetComponent<MeshRenderer>()) s += hasMeshRendererBonus;

        // Name-based tweaks
        string nm = t.name;
        foreach (var rule in nameRules)
        {
            if (string.IsNullOrEmpty(rule.contains)) continue;
            if (nm.Contains(rule.contains)) s += rule.scoreDelta;
        }

        return s;
    }
}
#endif
