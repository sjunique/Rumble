// Assets/Editor/WaypointCircleForSWS.cs
// Editor-only generator for SWS paths arranged in a circle on terrain/ground.

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class WaypointCircleForSWS : EditorWindow
{
    // ---------- UI Fields ----------
    Transform centerTransform;
    Vector3 centerPosition = Vector3.zero;
    bool useSelectedObjectAsCenter = true;

    float radius = 50f;
    int pointCount = 24;
    float randomJitter = 0f;

    bool useTerrainHeight = true;       // Gaia small terrain works via Terrain.activeTerrains
    LayerMask groundMask = ~0;
    float raycastMaxDistance = 1500f;
    float heightOffset = 0.2f;
    bool alignToSurfaceNormal = false;

    string pathName = "Path_Circle";
    string waypointPrefix = "WP_";

    GameObject collectiblePrefab;
    int spawnEveryN = 5;
    Vector3 collectibleOffset = Vector3.up * 0.3f;

    [MenuItem("Tools/Waypoints/Circle for SWS")]
    public static void ShowWindow()
    {
        var w = GetWindow<WaypointCircleForSWS>("Circle for SWS");
        w.minSize = new Vector2(360, 420);
    }

    void OnEnable()
    {
        if (Selection.activeTransform != null)
            centerTransform = Selection.activeTransform;
    }

    void OnGUI()
    {
        GUILayout.Label("Center", EditorStyles.boldLabel);
        useSelectedObjectAsCenter = EditorGUILayout.Toggle("Use Selected Transform", useSelectedObjectAsCenter);
        using (new EditorGUI.DisabledScope(!useSelectedObjectAsCenter))
        {
            centerTransform = (Transform)EditorGUILayout.ObjectField("Center Transform", centerTransform, typeof(Transform), true);
        }
        using (new EditorGUI.DisabledScope(useSelectedObjectAsCenter))
        {
            centerPosition = EditorGUILayout.Vector3Field("Manual Center", centerPosition);
        }

        EditorGUILayout.Space(6);
        GUILayout.Label("Circle", EditorStyles.boldLabel);
        radius = EditorGUILayout.FloatField("Radius", Mathf.Max(0.1f, radius));
        pointCount = EditorGUILayout.IntField("Points", Mathf.Max(3, pointCount));
        randomJitter = EditorGUILayout.Slider("Random Jitter", randomJitter, 0f, Mathf.Max(0f, radius * 0.2f));

        EditorGUILayout.Space(6);
        GUILayout.Label("Placement", EditorStyles.boldLabel);
        useTerrainHeight = EditorGUILayout.Toggle("Use Terrain Height", useTerrainHeight);
        groundMask = LayerMaskField("Ground Mask (Raycast)", groundMask);
        raycastMaxDistance = EditorGUILayout.FloatField("Raycast Max Distance", raycastMaxDistance);
        heightOffset = EditorGUILayout.FloatField("Height Offset", heightOffset);
        alignToSurfaceNormal = EditorGUILayout.Toggle("Align To Surface Normal", alignToSurfaceNormal);

        EditorGUILayout.Space(6);
        GUILayout.Label("Naming", EditorStyles.boldLabel);
        pathName = EditorGUILayout.TextField("Path Name", string.IsNullOrEmpty(pathName) ? "Path_Circle" : pathName);
        waypointPrefix = EditorGUILayout.TextField("Waypoint Prefix", string.IsNullOrEmpty(waypointPrefix) ? "WP_" : waypointPrefix);

        EditorGUILayout.Space(6);
        GUILayout.Label("Collectibles (optional)", EditorStyles.boldLabel);
        collectiblePrefab = (GameObject)EditorGUILayout.ObjectField("Collectible Prefab", collectiblePrefab, typeof(GameObject), false);
        spawnEveryN = Mathf.Max(2, EditorGUILayout.IntField("Spawn Every N Points", Mathf.Max(2, spawnEveryN)));
        collectibleOffset = EditorGUILayout.Vector3Field("Collectible Offset", collectibleOffset);

        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(!HasCenter()))
        {
            if (GUILayout.Button("Generate SWS Circle Path", GUILayout.Height(36)))
                Generate();
        }

        EditorGUILayout.HelpBox(
            "• Places a Path parent with SWS 'PathManager' (if found) and child waypoints.\n" +
            "• Works with Gaia terrains (Use Terrain Height) or any collider (Raycast).\n" +
            "• After generation you can move/rotate points as usual.\n" +
            "• If SWS doesn’t auto-refresh, click its Update/Refresh in the PathManager inspector.",
            MessageType.Info);
    }

    bool HasCenter() => useSelectedObjectAsCenter ? centerTransform != null : true;

    void Generate()
    {
        Vector3 center = useSelectedObjectAsCenter && centerTransform ? centerTransform.position : centerPosition;

        // Create parent
        GameObject pathGO = new GameObject(pathName);
        Undo.RegisterCreatedObjectUndo(pathGO, "Create SWS Path");
        pathGO.transform.position = center;

        // Try to add SWS PathManager if present
        var pathManagerType = FindTypeByName("PathManager");
        if (pathManagerType != null)
        {
            Undo.AddComponent(pathGO, pathManagerType);
        }
        else
        {
            Debug.LogWarning("[Circle for SWS] SWS 'PathManager' type not found. Path will still be created with children; add PathManager yourself later.");
        }

        // Parents
        Transform waypointsParent = pathGO.transform;
        GameObject collectiblesParentGO = null;
        Transform collectiblesParent = null;
        if (collectiblePrefab)
        {
            collectiblesParentGO = new GameObject($"{pathName}_Collectibles");
            Undo.RegisterCreatedObjectUndo(collectiblesParentGO, "Create Collectibles Parent");
            collectiblesParent = collectiblesParentGO.transform;
            collectiblesParent.position = center;
        }

        float step = Mathf.PI * 2f / pointCount;
        for (int i = 0; i < pointCount; i++)
        {
            float angle = step * i;
            Vector3 local = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            if (randomJitter > 0f)
            {
                local += new Vector3(
                    UnityEngine.Random.Range(-randomJitter, randomJitter),
                    0f,
                    UnityEngine.Random.Range(-randomJitter, randomJitter));
            }

            Vector3 normal;
            Vector3 world = ProjectToGround(center + local, out normal) + Vector3.up * heightOffset;

            // Waypoint child
            GameObject wp = new GameObject($"{waypointPrefix}{i:00}");
            Undo.RegisterCreatedObjectUndo(wp, "Create Waypoint");
         //   wp.transform.SetParent(waypointsParent, worldSpace: true);
wp.transform.SetParent(waypointsParent, true);

            wp.transform.position = world;
            if (alignToSurfaceNormal)
                wp.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

            // collectible every Nth
            if (collectiblePrefab && (i % spawnEveryN == 0))
            {
                var col = (GameObject)PrefabUtility.InstantiatePrefab(collectiblePrefab) as GameObject;
                Undo.RegisterCreatedObjectUndo(col, "Create Collectible");
              //  col.transform.SetParent(collectiblesParent, worldSpace: true);
              col.transform.SetParent(collectiblesParent, true);
                col.transform.position = world + collectibleOffset;
                if (alignToSurfaceNormal)
                    col.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            }
        }

        // Try to populate PathManager.waypoints (optional, if field exists)
        TryPopulateSWSWaypointList(pathGO, pathManagerType);

        // Select created path
        Selection.activeObject = pathGO;
        EditorGUIUtility.PingObject(pathGO);
    }

    // --- Helpers ---

    static Type FindTypeByName(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (t != null) return t;
            }
            catch { /* dynamic assembly */ }
        }
        return null;
    }

    static void TryPopulateSWSWaypointList(GameObject pathGO, Type pathManagerType)
    {
        if (pathManagerType == null) return;

        var comp = pathGO.GetComponent(pathManagerType);
        if (comp == null) return;

        var waypointsField = pathManagerType.GetField("waypoints");
        if (waypointsField == null) return; // older/newer SWS versions might manage children automatically

        var children = new List<Transform>();
        foreach (Transform c in pathGO.transform) children.Add(c);

        // Assign list via reflection
        waypointsField.SetValue(comp, children);
        EditorUtility.SetDirty(comp as UnityEngine.Object);
    }

    Vector3 ProjectToGround(Vector3 world, out Vector3 surfaceNormal)
    {
        surfaceNormal = Vector3.up;

        if (useTerrainHeight)
        {
            Terrain t = GetNearestTerrain(world);
            if (t != null)
            {
                var tp = t.transform.position;
                float y = t.SampleHeight(world) + tp.y;
                // optional: t.terrainData.GetInterpolatedNormal(...) for better normal
                return new Vector3(world.x, y, world.z);
            }
        }

        // Raycast (meshes, Gaia details, etc.)
        Ray ray = new Ray(world + Vector3.up * (raycastMaxDistance * 0.5f), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            surfaceNormal = hit.normal;
            return hit.point;
        }

        return world; // fallback
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

    // Nice layer mask field (EditorGUILayout.LayerField doesn’t support mask)
    static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = Enumerable.Range(0, 32).Select(LayerMask.LayerToName).ToArray();
        var layerNumbers = Enumerable.Range(0, 32).ToArray();

        List<string> layerNames = new List<string>();
        List<int> layerIndices = new List<int>();
        for (int i = 0; i < layers.Length; i++)
        {
            if (!string.IsNullOrEmpty(layers[i]))
            {
                layerNames.Add(layers[i]);
                layerIndices.Add(layerNumbers[i]);
            }
        }

        int maskWithoutEmpty = 0;
        for (int i = 0; i < layerIndices.Count; i++)
        {
            if (((1 << layerIndices[i]) & selected.value) > 0)
                maskWithoutEmpty |= (1 << i);
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layerNames.ToArray());
        int mask = 0;
        for (int i = 0; i < layerIndices.Count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) > 0)
                mask |= (1 << layerIndices[i]);
        }
        selected.value = mask;
        return selected;
    }
}
