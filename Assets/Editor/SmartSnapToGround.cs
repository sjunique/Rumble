using UnityEngine;

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

public class SmartSnapToGround : EditorWindow
{
    [Header("Raycast")]
    [SerializeField] LayerMask groundLayers = ~0;
    [SerializeField] string requiredTag = "";          // optional
    [SerializeField] float rayStartHeight = 100f;
    [SerializeField] QueryTriggerInteraction triggerMode = QueryTriggerInteraction.Ignore;

    [Header("Marker / Offset")]
    [SerializeField] string markerName = "Marker";     // child name or tag "Marker"
    [SerializeField] float skin = 0.02f;               // keep a hair above ground

    [Header("How to place")]
    [SerializeField] bool useColliderBottom = true;    // else renderer bounds
    [SerializeField] bool yOnly = true;                // only change Y position
    [SerializeField] bool alignUpToNormal = false;     // rotate up to hit.normal

    [MenuItem("Tools/Snap/Smart Snap Selected To Ground")]
    public static void Open() => GetWindow<SmartSnapToGround>("Smart Snap To Ground");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Raycast", EditorStyles.boldLabel);
        groundLayers    = DrawLayerMask("Ground Layers", groundLayers);
        triggerMode     = (QueryTriggerInteraction)EditorGUILayout.EnumPopup("Triggers", triggerMode);
        rayStartHeight  = EditorGUILayout.FloatField("Ray Start Height", rayStartHeight);
        requiredTag     = EditorGUILayout.TextField("Only Snap To Tag (optional)", requiredTag);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Marker / Offset", EditorStyles.boldLabel);
        markerName      = EditorGUILayout.TextField("Marker Name", markerName);
        skin            = EditorGUILayout.FloatField("Skin Offset", Mathf.Max(0f, skin));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        useColliderBottom = EditorGUILayout.Toggle("Use Collider Bottom", useColliderBottom);
        yOnly             = EditorGUILayout.Toggle("Only Move Y", yOnly);
        alignUpToNormal   = EditorGUILayout.Toggle("Align Up To Surface", alignUpToNormal);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button($"Snap {Selection.gameObjects.Length} Selected Object(s)"))
            {
                SnapSelection();
            }
        }
    }

    void SnapSelection()
    {
        int snapped = 0;
        foreach (var obj in Selection.gameObjects)
        {
            if (TrySnapObject(obj)) snapped++;
        }
        Debug.Log($"[SmartSnapToGround] Snapped {snapped} object(s).");
    }

    bool TrySnapObject(GameObject obj)
    {
        if (!obj) return false;

        // 1) Determine ray origin (prefer child "marker" or tagged Marker)
        Vector3 origin = GetMarkerOrDefaultOrigin(obj.transform);

        // 2) Raycast down
        if (!Physics.Raycast(origin, Vector3.down, out var hit, rayStartHeight * 2f, groundLayers, triggerMode))
            return false;

        if (!string.IsNullOrEmpty(requiredTag) && !hit.collider.CompareTag(requiredTag))
            return false;

        // 3) Compute vertical offset so bottom sits on ground
        float offsetY = GetBottomOffsetY(obj);

        // 4) Compute target position
        Vector3 targetPos = obj.transform.position;

        if (yOnly)
        {
            targetPos.y = hit.point.y + offsetY + skin;
        }
        else
        {
            // Keep original horizontal but allow minor correction using marker XZ if you like.
            targetPos = new Vector3(targetPos.x, hit.point.y + offsetY + skin, targetPos.z);
        }

        // 5) Apply rotation (optional)
        Quaternion targetRot = obj.transform.rotation;
        if (alignUpToNormal)
        {
            // rotate so object's up aligns with the surface normal
            var from = obj.transform.up;
            var to   = hit.normal;
            var delta = Quaternion.FromToRotation(from, to);
            targetRot = delta * obj.transform.rotation;
        }

        // 6) Record undo and assign
        Undo.RecordObject(obj.transform, "Smart Snap To Ground");
        if (alignUpToNormal) obj.transform.rotation = targetRot;
        obj.transform.position = targetPos;

        return true;
    }

    Vector3 GetMarkerOrDefaultOrigin(Transform root)
    {
        // Prefer a child named markerName (case-insensitive) or tagged "Marker"
        var marker = FindMarker(root);
        Vector3 basePos = marker ? marker.position : root.position;
        return basePos + Vector3.up * rayStartHeight;
    }

    Transform FindMarker(Transform root)
    {
        // 1) by tag
        var tagged = root.GetComponentsInChildren<Transform>(true)
                         .FirstOrDefault(t => t.CompareTag("Marker"));
        if (tagged) return tagged;

        // 2) by name contains markerName
        if (!string.IsNullOrEmpty(markerName))
        {
            var nameLower = markerName.ToLowerInvariant();
            var named = root.GetComponentsInChildren<Transform>(true)
                            .FirstOrDefault(t => t.name.ToLowerInvariant().Contains(nameLower));
            if (named) return named;
        }

        // 3) fallback: any child named "Cylinder" (since you mentioned a cylinder marker)
        var cyl = root.GetComponentsInChildren<Transform>(true)
                      .FirstOrDefault(t => t.name.ToLowerInvariant().Contains("cylinder"));
        return cyl;
    }

    float GetBottomOffsetY(GameObject obj)
    {
        // World-space distance from object.position to the bottom of its bounds
        Bounds b;
        if (useColliderBottom)
        {
            var col = obj.GetComponentInChildren<Collider>();
            if (col) { b = col.bounds; return obj.transform.position.y - b.min.y; }
        }

        // fallback: renderer bounds
        var rend = obj.GetComponentInChildren<Renderer>();
        if (rend) { b = rend.bounds; return obj.transform.position.y - b.min.y; }

        // last resort: zero (use pivot)
        return 0f;
    }

    // Layer mask drawer with names
    static LayerMask DrawLayerMask(string label, LayerMask selected)
    {
        var layers = new string[32];
        var layerNumbers = new int[32];
        int count = 0;

        for (int i = 0; i < 32; i++)
        {
            var name = LayerMask.LayerToName(i);
            if (!string.IsNullOrEmpty(name))
            {
                layers[count] = name;
                layerNumbers[count] = i;
                count++;
            }
        }

        int maskWithoutEmpty = 0;
        for (int i = 0; i < count; i++)
        {
            if (((selected.value >> layerNumbers[i]) & 1) == 1)
                maskWithoutEmpty |= (1 << i);
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers, EditorStyles.layerMaskField);

        int mask = 0;
        for (int i = 0; i < count; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
                mask |= (1 << layerNumbers[i]);
        }

        selected.value = mask;
        return selected;
    }
}
#endif
