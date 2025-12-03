using UnityEngine;

// Assets/Editor/CircleWaypointPlacer.cs
// Editor-only: Place waypoint transforms in a circle on terrain/ground, plus optional collectibles.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class CircleWaypointPlacer : EditorWindow
{
    // ---- Center ----
    Transform centerTransform;
    Vector3 manualCenter = Vector3.zero;
    bool useSelectedAsCenter = true;

    // ---- Ring ----
    [Min(0.1f)] float radius = 50f;
    [Min(3)] int pointCount = 24;
    float randomJitter = 0f;            // optional wobble
    float heightOffset = 0.2f;          // lift from ground
    bool alignToSurfaceNormal = false;

    // ---- Grounding ----
    bool useTerrainHeight = true;       // Gaia terrains work via Terrain.activeTerrains
    LayerMask groundMask = ~0;          // for raycast mode
    float raycastMaxDistance = 1500f;

    // ---- Output / Prefabs ----
    string groupName = "WaypointRing";
    string waypointPrefix = "WP_";
    GameObject waypointPrefab;          // optional; if null, creates empty GameObjects
    GameObject collectiblePrefab;       // optional
    int spawnEveryN = 5;
    Vector3 collectibleOffset = new Vector3(0, 0.3f, 0);

    [MenuItem("Tools/Waypoints/Circle Waypoint Placer")]
    public static void ShowWindow()
    {
        var w = GetWindow<CircleWaypointPlacer>("Circle Waypoints");
        w.minSize = new Vector2(360, 420);
    }

    void OnEnable()
    {
        if (Selection.activeTransform) centerTransform = Selection.activeTransform;
    }

    void OnGUI()
    {
        GUILayout.Label("Center", EditorStyles.boldLabel);
        useSelectedAsCenter = EditorGUILayout.Toggle("Use Selected Transform", useSelectedAsCenter);
        using (new EditorGUI.DisabledScope(!useSelectedAsCenter))
            centerTransform = (Transform)EditorGUILayout.ObjectField("Center Transform", centerTransform, typeof(Transform), true);
        using (new EditorGUI.DisabledScope(useSelectedAsCenter))
            manualCenter = EditorGUILayout.Vector3Field("Manual Center", manualCenter);

        EditorGUILayout.Space(6);
        GUILayout.Label("Circle", EditorStyles.boldLabel);
        radius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius", radius));
        pointCount = Mathf.Max(3, EditorGUILayout.IntField("Points", pointCount));
        randomJitter = EditorGUILayout.Slider("Random Jitter", randomJitter, 0f, Mathf.Max(0f, radius * 0.2f));
        heightOffset = EditorGUILayout.FloatField("Height Offset", heightOffset);
        alignToSurfaceNormal = EditorGUILayout.Toggle("Align To Surface Normal", alignToSurfaceNormal);

        EditorGUILayout.Space(6);
        GUILayout.Label("Grounding", EditorStyles.boldLabel);
        useTerrainHeight = EditorGUILayout.Toggle("Use Terrain Height (Terrain.activeTerrains)", useTerrainHeight);
        groundMask = LayerMaskField("Raycast Ground Mask", groundMask);
        raycastMaxDistance = EditorGUILayout.FloatField("Raycast Max Distance", raycastMaxDistance);

        EditorGUILayout.Space(6);
        GUILayout.Label("Output", EditorStyles.boldLabel);
        groupName = EditorGUILayout.TextField("Parent Name", string.IsNullOrEmpty(groupName) ? "WaypointRing" : groupName);
        waypointPrefix = EditorGUILayout.TextField("Waypoint Prefix", string.IsNullOrEmpty(waypointPrefix) ? "WP_" : waypointPrefix);
        waypointPrefab = (GameObject)EditorGUILayout.ObjectField("Waypoint Prefab (optional)", waypointPrefab, typeof(GameObject), false);

        EditorGUILayout.Space(6);
        GUILayout.Label("Collectibles (optional)", EditorStyles.boldLabel);
        collectiblePrefab = (GameObject)EditorGUILayout.ObjectField("Collectible Prefab", collectiblePrefab, typeof(GameObject), false);
        spawnEveryN = Mathf.Max(2, EditorGUILayout.IntField("Spawn Every Nth", Mathf.Max(2, spawnEveryN)));
        collectibleOffset = EditorGUILayout.Vector3Field("Collectible Offset", collectibleOffset);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Generate Waypoint Ring", GUILayout.Height(34)))
            Generate();
    }

    void Generate()
    {
        Vector3 center = useSelectedAsCenter && centerTransform ? centerTransform.position : manualCenter;

        // Create parent(s)
        GameObject ring = new GameObject(groupName);
        Undo.RegisterCreatedObjectUndo(ring, "Create Waypoint Ring");
        ring.transform.position = center;

        Transform collectiblesParent = null;
        if (collectiblePrefab)
        {
            var c = new GameObject(groupName + "_Collectibles");
            Undo.RegisterCreatedObjectUndo(c, "Create Collectibles Parent");
            collectiblesParent = c.transform;
            collectiblesParent.position = center;
        }

        float step = Mathf.PI * 2f / pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float a = step * i;
            Vector3 local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;

            if (randomJitter > 0f)
            {
                local += new Vector3(
                    Random.Range(-randomJitter, randomJitter),
                    0f,
                    Random.Range(-randomJitter, randomJitter));
            }

            Vector3 normal;
            Vector3 world = ProjectToGround(center + local, out normal) + Vector3.up * heightOffset;

            // Waypoint
            GameObject wp;
            if (waypointPrefab)
                wp = (GameObject)PrefabUtility.InstantiatePrefab(waypointPrefab);
            else
                wp = new GameObject();

            Undo.RegisterCreatedObjectUndo(wp, "Create Waypoint");
            wp.name = $"{waypointPrefix}{i:00}";
            wp.transform.position = world;
            if (alignToSurfaceNormal) wp.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            wp.transform.SetParent(ring.transform, true); // keep world position

            // Collectible
            if (collectiblePrefab && (i % spawnEveryN == 0))
            {
                var col = (GameObject)PrefabUtility.InstantiatePrefab(collectiblePrefab);
                Undo.RegisterCreatedObjectUndo(col, "Create Collectible");
                col.transform.position = world + collectibleOffset;
                if (alignToSurfaceNormal) col.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
                col.transform.SetParent(collectiblesParent, true);
            }
        }

        Selection.activeObject = ring;
        EditorGUIUtility.PingObject(ring);
    }

    // ---------- helpers ----------
    Vector3 ProjectToGround(Vector3 world, out Vector3 surfaceNormal)
    {
        surfaceNormal = Vector3.up;

        if (useTerrainHeight)
        {
            Terrain t = GetNearestTerrain(world);
            if (t != null)
            {
                float y = t.SampleHeight(world) + t.transform.position.y;
                return new Vector3(world.x, y, world.z);
            }
        }

        var ray = new Ray(world + Vector3.up * (raycastMaxDistance * 0.5f), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            surfaceNormal = hit.normal;
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

    // LayerMask picker that shows only named layers
    static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = Enumerable.Range(0, 32).Select(LayerMask.LayerToName).ToArray();
        var ids = Enumerable.Range(0, 32).ToArray();
        var names = new List<string>(); var idx = new List<int>();

        for (int i = 0; i < layers.Length; i++)
            if (!string.IsNullOrEmpty(layers[i])) { names.Add(layers[i]); idx.Add(ids[i]); }

        int maskNoEmpty = 0;
        for (int i = 0; i < idx.Count; i++)
            if (((1 << idx[i]) & selected.value) != 0) maskNoEmpty |= (1 << i);

        maskNoEmpty = EditorGUILayout.MaskField(label, maskNoEmpty, names.ToArray());
        int mask = 0;
        for (int i = 0; i < idx.Count; i++)
            if ((maskNoEmpty & (1 << i)) != 0) mask |= (1 << idx[i]);

        selected.value = mask;
        return selected;
    }
}
