// Assets/Editor/SWS_ConvertSelected.cs
// Converts a parent with waypoint children to SWS PathManager,
// with an optional "snap to ground" pass (terrain or raycast).

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SWS_ConvertSelected
{
    // --- Snapping defaults (tweak if you like) ---
    static bool useTerrainHeight = true;          // Use Terrain.activeTerrains first
    static LayerMask groundMask = ~0;             // For raycast fallback
    static float raycastMaxDistance = 2000f;
    static float heightOffset = 0.20f;            // small lift to avoid z-fighting
    static bool alignToSurfaceNormal = false;     // rotate Y-up to ground normal

    [MenuItem("Tools/Waypoints/Convert Selected To SWS Path", true)]
    static bool ValidateConvert() => Selection.activeGameObject != null;

    [MenuItem("Tools/Waypoints/Convert Selected To SWS Path")]
    static void ConvertSelectedNoSnap()
    {
        ConvertSelectedToSwsPathInternal(snapToGround: false);
    }

    [MenuItem("Tools/Waypoints/Convert + Snap To Ground")]
    static void ConvertSelectedWithSnap()
    {
        ConvertSelectedToSwsPathInternal(snapToGround: true);
    }

    static void ConvertSelectedToSwsPathInternal(bool snapToGround)
    {
        var go = Selection.activeGameObject;
        if (!go)
        {
            EditorUtility.DisplayDialog("No Selection", "Select the parent object that holds your waypoint transforms.", "OK");
            return;
        }

        // Find SWS PathManager type
        var pathManagerType = FindTypeByName("PathManager");
        if (pathManagerType == null)
        {
            EditorUtility.DisplayDialog(
                "SWS Not Found",
                "Couldn’t find type 'PathManager'. Make sure Simple Waypoint System (SWS) is imported.",
                "OK");
            return;
        }

        // Collect child waypoints in current hierarchy order
        var waypoints = new List<Transform>();
        foreach (Transform c in go.transform) waypoints.Add(c);

        if (waypoints.Count < 2)
        {
            EditorUtility.DisplayDialog("Need More Points", "Add at least 2 waypoint children under the selected object.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(go, "Convert To SWS Path");

        // Optional: snap each child to ground
        if (snapToGround)
        {
            foreach (var wp in waypoints)
            {
                Vector3 n;
                var snapped = ProjectToGround(wp.position, out n);
                wp.position = snapped + Vector3.up * heightOffset;
                if (alignToSurfaceNormal)
                    wp.rotation = Quaternion.FromToRotation(Vector3.up, n);
            }
        }

        // Add PathManager if missing
        var pm = go.GetComponent(pathManagerType);
        if (pm == null) pm = Undo.AddComponent(go, pathManagerType);

        // Assign to waypoints field/property (handles List<Transform> or Transform[])
        bool assigned = TrySetWaypointsList(pm, pathManagerType, waypoints);
        if (!assigned)
        {
            EditorUtility.DisplayDialog(
                "Couldn’t Assign Waypoints",
                "This SWS version uses a different field/property than 'waypoints' or 'pathObjects'. " +
                "Tell me the exact member name & type shown on PathManager and I’ll adapt it.",
                "OK");
            return;
        }

        EditorUtility.SetDirty(go);
        EditorGUIUtility.PingObject(go);
        Selection.activeObject = go;

        EditorUtility.DisplayDialog("Done",
            $"Converted '{go.name}' into an SWS Path with {waypoints.Count} points.\n" +
            (snapToGround ? "Waypoints were snapped to ground." : "No snapping was applied."),
            "OK");
    }

    // ---------- Helpers ----------

    static Type FindTypeByName(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (t != null) return t;
            }
            catch { /* dynamic assemblies may throw */ }
        }
        return null;
    }

    // Assigns either List<Transform> or Transform[] to field/property by common names
    static bool TrySetWaypointsList(Component pm, Type pmType, List<Transform> points)
    {
        bool AssignField(string name)
        {
            var f = pmType.GetField(name);
            if (f == null) return false;

            var ft = f.FieldType;
            if (ft == typeof(Transform[])) { f.SetValue(pm, points.ToArray()); return true; }
            if (typeof(System.Collections.IList).IsAssignableFrom(ft)) { f.SetValue(pm, points); return true; }
            return false;
        }

        bool AssignProperty(string name)
        {
            var p = pmType.GetProperty(name);
            if (p == null || !p.CanWrite) return false;

            var pt = p.PropertyType;
            if (pt == typeof(Transform[])) { p.SetValue(pm, points.ToArray(), null); return true; }
            if (typeof(System.Collections.IList).IsAssignableFrom(pt)) { p.SetValue(pm, points, null); return true; }
            return false;
        }

        if (AssignField("waypoints") || AssignProperty("waypoints")) return true;
        if (AssignField("pathObjects") || AssignProperty("pathObjects")) return true;
        if (AssignField("nodes") || AssignProperty("nodes")) return true; // older variants

        return false;
    }

    static Vector3 ProjectToGround(Vector3 world, out Vector3 surfaceNormal)
    {
        surfaceNormal = Vector3.up;

        // Terrain first (works with Gaia terrains)
        if (useTerrainHeight)
        {
            Terrain t = GetNearestTerrain(world);
            if (t != null)
            {
                float y = t.SampleHeight(world) + t.transform.position.y;
                // For exact normal you could use GetInterpolatedNormal with normalized local coords
                return new Vector3(world.x, y, world.z);
            }
        }

        // Raycast fallback (for mesh ground)
        var ray = new Ray(world + Vector3.up * (raycastMaxDistance * 0.5f), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            surfaceNormal = hit.normal;
            return hit.point;
        }

        return world; // no change if nothing hit
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
}
