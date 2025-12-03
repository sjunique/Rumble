// Assets/Editor/SWS_CircleBuilder.cs
// Editor-only: Generate a circular SWS path (PathManager + child "Waypoint i") and collectibles.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class SWS_CircleBuilder : EditorWindow
{
    Transform centerTransform;
    Vector3 manualCenter;
    bool useSelectedAsCenter = true;

    float radius = 50f;
    int pointCount = 24;
    float heightOffset = 0.2f;
    bool closeLoop = true;                // SWS reads children in order; loop is visual/logic in SWS settings

    bool useTerrainHeight = true;         // Works with Gaia too
    LayerMask groundMask = ~0;
    float raycastMaxDistance = 1500f;
    bool alignToSurfaceNormal = false;

    string waypointPrefix = "Waypoint ";
    GameObject collectiblePrefab;
    int spawnEveryN = 5;
    Vector3 collectibleOffset = new Vector3(0, 0.3f, 0);

    [MenuItem("Tools/Waypoints/Build Circle On Selected SWS Path")]
    public static void ShowWindow()
    {
        var w = GetWindow<SWS_CircleBuilder>("SWS Circle Builder");
        w.minSize = new Vector2(360, 350);
    }

    void OnEnable()
    {
        if (Selection.activeTransform) centerTransform = Selection.activeTransform;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Select your SWS Path GameObject (has PathManager), then build.", MessageType.Info);

        var sel = Selection.activeGameObject;
        bool hasPathManager = sel && (FindTypeByName("PathManager") != null) && sel.GetComponent(FindTypeByName("PathManager")) != null;

        EditorGUI.BeginDisabledGroup(!hasPathManager);
        useSelectedAsCenter = EditorGUILayout.Toggle("Use Selected As Center", useSelectedAsCenter);
        using (new EditorGUI.DisabledScope(!useSelectedAsCenter))
            centerTransform = (Transform)EditorGUILayout.ObjectField("Center Transform", centerTransform, typeof(Transform), true);
        using (new EditorGUI.DisabledScope(useSelectedAsCenter))
            manualCenter = EditorGUILayout.Vector3Field("Manual Center", manualCenter);

        EditorGUILayout.Space(6);
        GUILayout.Label("Circle", EditorStyles.boldLabel);
        radius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius", radius));
        pointCount = Mathf.Max(3, EditorGUILayout.IntField("Point Count", pointCount));
        closeLoop = EditorGUILayout.Toggle("Close Loop (visual)", closeLoop);
        heightOffset = EditorGUILayout.FloatField("Height Offset", heightOffset);

        EditorGUILayout.Space(6);
        GUILayout.Label("Grounding", EditorStyles.boldLabel);
        useTerrainHeight = EditorGUILayout.Toggle("Use Terrain Height", useTerrainHeight);
        groundMask = LayerMaskField("Raycast Ground Mask", groundMask);
        raycastMaxDistance = EditorGUILayout.FloatField("Raycast Max Distance", raycastMaxDistance);
        alignToSurfaceNormal = EditorGUILayout.Toggle("Align To Surface Normal", alignToSurfaceNormal);

        EditorGUILayout.Space(6);
        GUILayout.Label("Naming & Collectibles", EditorStyles.boldLabel);
        waypointPrefix = EditorGUILayout.TextField("Waypoint Prefix", string.IsNullOrEmpty(waypointPrefix) ? "Waypoint " : waypointPrefix);
        collectiblePrefab = (GameObject)EditorGUILayout.ObjectField("Collectible Prefab", collectiblePrefab, typeof(GameObject), false);
        spawnEveryN = Mathf.Max(2, EditorGUILayout.IntField("Spawn Every Nth", Mathf.Max(2, spawnEveryN)));
        collectibleOffset = EditorGUILayout.Vector3Field("Collectible Offset", collectibleOffset);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Build On Selected Path", GUILayout.Height(32)))
        {
            if (!hasPathManager)
            {
                EditorUtility.DisplayDialog("No PathManager", "Select a GameObject with SWS 'PathManager' component.", "OK");
            }
            else
            {
                BuildOnSelected(sel);
            }
        }
        EditorGUI.EndDisabledGroup();
    }

    void BuildOnSelected(GameObject pathGO)
    {
        Undo.RegisterFullObjectHierarchyUndo(pathGO, "Build SWS Circle");

        // center
        Vector3 center = useSelectedAsCenter && centerTransform
            ? centerTransform.position
            : manualCenter;

        // clear existing waypoints (children named "Waypoint *")
        var toDelete = new List<GameObject>();
        foreach (Transform c in pathGO.transform)
            toDelete.Add(c.gameObject);
        foreach (var go in toDelete)
            Undo.DestroyObjectImmediate(go);

        // collectibles parent
        Transform collectiblesParent = null;
        if (collectiblePrefab)
        {
            var parent = new GameObject(pathGO.name + "_Collectibles");
            Undo.RegisterCreatedObjectUndo(parent, "Create Collectibles Parent");
            parent.transform.position = center;
            collectiblesParent = parent.transform;
        }

        float step = Mathf.PI * 2f / pointCount;
        Vector3 first = Vector3.zero, prev = Vector3.zero;

        for (int i = 0; i < pointCount; i++)
        {
            float angle = step * i;
            Vector3 ring = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 normal;
            Vector3 world = ProjectToGround(center + ring, out normal) + Vector3.up * heightOffset;

            // create waypoint i
            var wp = new GameObject($"{waypointPrefix}{i}");
            Undo.RegisterCreatedObjectUndo(wp, "Create Waypoint");
            wp.transform.position = world;
            if (alignToSurfaceNormal) wp.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            wp.transform.SetParent(pathGO.transform, true); // keep world position

            // collectible (optional)
            if (collectiblePrefab && (i % spawnEveryN == 0))
            {
                var col = (GameObject)PrefabUtility.InstantiatePrefab(collectiblePrefab);
                Undo.RegisterCreatedObjectUndo(col, "Create Collectible");
                (col.transform).position = world + collectibleOffset;
                if (alignToSurfaceNormal) col.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                col.transform.SetParent(collectiblesParent, true);
            }

            if (i == 0) first = world; else prev = world;
        }

        // populate PathManager.waypoints (if the field exists)
        var pathManagerType = FindTypeByName("PathManager");
        TryPopulateSWSWaypointList(pathGO, pathManagerType);

        // select the path
        Selection.activeObject = pathGO;
        EditorGUIUtility.PingObject(pathGO);
    }

    // ---- helpers ----

    static Type FindTypeByName(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try { t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName); }
            catch { /* dynamic */ }
            if (t != null) return t;
        }
        return null;
    }

    static void TryPopulateSWSWaypointList(GameObject pathGO, Type pathManagerType)
    {
        if (pathManagerType == null) return;
        var comp = pathGO.GetComponent(pathManagerType);
        if (!comp) return;

        // Typical SWS has public List<Transform> waypoints;
        var waypointsField = pathManagerType.GetField("waypoints");
        if (waypointsField == null) return;

        var list = new List<Transform>();
        foreach (Transform c in pathGO.transform) list.Add(c);
        waypointsField.SetValue(comp, list);

        EditorUtility.SetDirty(comp as UnityEngine.Object);
    }

    Vector3 ProjectToGround(Vector3 world, out Vector3 normal)
    {
        normal = Vector3.up;

        if (useTerrainHeight)
        {
            Terrain t = GetNearestTerrain(world);
            if (t != null)
            {
                var baseY = t.transform.position.y;
                float y = t.SampleHeight(world) + baseY;
                // For exact normal use interpolated normal (optional)
                // var tn = t.terrainData.GetInterpolatedNormal( ... );
                return new Vector3(world.x, y, world.z);
            }
        }

        // raycast (meshes)
        var ray = new Ray(world + Vector3.up * (raycastMaxDistance * 0.5f), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal;
            return hit.point;
        }

        return world;
    }

    static Terrain GetNearestTerrain(Vector3 pos)
    {
        Terrain nearest = null; float best = float.MaxValue;
        foreach (var t in Terrain.activeTerrains)
        {
            float d = (t.transform.position - pos).sqrMagnitude;
            if (d < best) { best = d; nearest = t; }
        }
        return nearest;
    }

    // Nice layer-mask picker
    static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = Enumerable.Range(0, 32).Select(LayerMask.LayerToName).ToArray();
        var nums = Enumerable.Range(0, 32).ToArray();

        var names = new List<string>();
        var idxs = new List<int>();
        for (int i = 0; i < layers.Length; i++)
        {
            if (!string.IsNullOrEmpty(layers[i])) { names.Add(layers[i]); idxs.Add(nums[i]); }
        }

        int maskNoEmpty = 0;
        for (int i = 0; i < idxs.Count; i++)
            if (((1 << idxs[i]) & selected.value) != 0) maskNoEmpty |= (1 << i);

        maskNoEmpty = EditorGUILayout.MaskField(label, maskNoEmpty, names.ToArray());
        int mask = 0;
        for (int i = 0; i < idxs.Count; i++)
            if ((maskNoEmpty & (1 << i)) != 0) mask |= (1 << idxs[i]);

        selected.value = mask;
        return selected;
    }
}
