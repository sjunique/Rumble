#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Unity.AI.Navigation; // for NavMeshSurface

public class NavMeshTileGridBaker : EditorWindow
{
    Bounds worldBounds;
    int cols = 4, rows = 4;
    float margin = 1.0f;          // small overlap to avoid seams
    LayerMask includedLayers = ~0; // keep Terrain/Statics only for speed

    [MenuItem("Tools/NavMesh/Create Tile Grid...")]
    static void Open() => GetWindow<NavMeshTileGridBaker>("NavMesh Tile Grid");

    void OnGUI()
    {
        EditorGUILayout.LabelField("World Bounds", EditorStyles.boldLabel);
        worldBounds.center = EditorGUILayout.Vector3Field("Center", worldBounds.center);
        worldBounds.size   = EditorGUILayout.Vector3Field("Size", worldBounds.size);

        if (GUILayout.Button("Use Selected Renderer/Terrain Bounds"))
        {
            if (Selection.activeGameObject)
            {
                var t = Selection.activeGameObject.GetComponent<Terrain>();
                if (t)
                {
                    var size = t.terrainData.size;
                    worldBounds = new Bounds(t.transform.position + size * 0.5f, size);
                }
                else
                {
                    var r = Selection.activeGameObject.GetComponent<Renderer>();
                    if (r) worldBounds = r.bounds;
                }
            }
        }

        cols = Mathf.Max(1, EditorGUILayout.IntField("Columns", cols));
        rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", rows));
        margin = EditorGUILayout.Slider("Tile Overlap (m)", margin, 0f, 2f);
        includedLayers = LayerMaskField("Included Layers", includedLayers);

        if (GUILayout.Button("Create Tiles Under Selected Root"))
        {
            var root = Selection.activeTransform;
            if (!root)
            {
                EditorUtility.DisplayDialog("Tile Grid", "Select a root GameObject in the hierarchy.", "OK");
                return;
            }
            CreateTiles(root);
        }
    }

    static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        var layers = InternalEditorUtility.layers;
        int mask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layerNum = LayerMask.NameToLayer(layers[i]);
            if (((selected.value >> layerNum) & 1) == 1) mask |= (1 << i);
        }
        mask = EditorGUILayout.MaskField(label, mask, layers);
        int newMask = 0;
        for (int i = 0; i < layers.Length; i++)
            if ((mask & (1 << i)) != 0) newMask |= (1 << LayerMask.NameToLayer(layers[i]));
        selected.value = newMask;
        return selected;
    }

    void CreateTiles(Transform root)
    {
        float dx = worldBounds.size.x / cols;
        float dz = worldBounds.size.z / rows;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var go = new GameObject($"NavTile_{r}_{c}");
            go.transform.SetParent(root, false);

            // center/size in world space
            Vector3 cWorld = new Vector3(
                worldBounds.min.x + dx * (c + 0.5f),
                worldBounds.center.y,
                worldBounds.min.z + dz * (r + 0.5f)
            );
            Vector3 sWorld = new Vector3(dx + margin*2f, worldBounds.size.y, dz + margin*2f);

            // place tile object at volume center
            go.transform.position = cWorld;

            // volume = BoxCollider (local center/size)
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size   = sWorld;

            // NavMeshSurface set to Volume collection
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.layerMask = includedLayers;

            // Set 'useGeometry = PhysicsColliders' via serialized property (no enum reference needed)
            TrySetUseGeometryPhysicsColliders(surface);

            // link helper so the surface tracks the box size/center
            go.AddComponent<NavSurfaceFromBoxCollider>();
        }

        EditorUtility.DisplayDialog(
            "Tile Grid",
            "Tiles created. Select a tile → in NavSurfaceFromBoxCollider click 'Sync & Bake This Volume'.\n" +
            "Or multi-select tiles and bake them together.",
            "OK"
        );
    }

    static void TrySetUseGeometryPhysicsColliders(NavMeshSurface surface)
    {
        // NavMeshSurface has a serialized int 'm_UseGeometry' (0=RenderMeshes, 1=PhysicsColliders)
        var so = new SerializedObject(surface);
        var p  = so.FindProperty("m_UseGeometry");
        if (p != null) { p.intValue = 1; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
#endif
// NavMeshTileGridBaker.cs  
// This script creates a grid of NavMeshSurface tiles based on a specified world bounds.
// It allows you to set the number of columns and rows, and includes a margin for overlap
// to avoid seams. The created tiles are set up to use the PhysicsColliders for geometry
// collection, and can be baked individually or in bulk.        