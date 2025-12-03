#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class PlaceTerrainMarkerTool : EditorWindow
{
    public GameObject markerPrefab; // Optional: assign a prefab (or it makes empty + TerrainMarker)
    public Color markerColor = Color.cyan;
    public float markerRadius = 1f;
    public string markerLabel = "SpawnPoint";

    private bool isPlacing = false;

    [MenuItem("Tools/Place Terrain Marker")]
    public static void ShowWindow()
    {
        var window = GetWindow<PlaceTerrainMarkerTool>("Place Marker Tool");
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Click 'Start Placing' and then click anywhere in the Scene View (on terrain or mesh) to drop a marker!", MessageType.Info);

        markerPrefab = (GameObject)EditorGUILayout.ObjectField("Marker Prefab (optional)", markerPrefab, typeof(GameObject), false);
        markerColor = EditorGUILayout.ColorField("Gizmo Color", markerColor);
        markerRadius = EditorGUILayout.FloatField("Gizmo Radius", markerRadius);
        markerLabel = EditorGUILayout.TextField("Marker Label", markerLabel);

        if (!isPlacing)
        {
            if (GUILayout.Button("Start Placing"))
                StartPlacing();
        }
        else
        {
            if (GUILayout.Button("Stop Placing"))
                StopPlacing();
            EditorGUILayout.HelpBox("Placing mode: Click in Scene View to add markers.", MessageType.Warning);
        }
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }
    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        isPlacing = false;
    }

    void StartPlacing() => isPlacing = true;
    void StopPlacing() => isPlacing = false;

    void OnSceneGUI(SceneView sceneView)
    {
        if (!isPlacing) return;
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !Event.current.alt)
        {
            // Raycast from mouse into scene
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                PlaceMarkerAt(hit.point, hit.normal);
                Event.current.Use();
            }
        }
    }

    void PlaceMarkerAt(Vector3 pos, Vector3 normal)
    {
        GameObject marker = null;
        if (markerPrefab != null)
        {
            marker = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab);
            marker.transform.position = pos;
            marker.transform.up = normal; // Align to surface
        }
        else
        {
            marker = new GameObject("TerrainMarker");
            marker.transform.position = pos;
            marker.transform.up = normal;
            var tm = marker.AddComponent<TerrainMarker>();
            tm.gizmoColor = markerColor;
            tm.gizmoRadius = markerRadius;
            tm.markerLabel = markerLabel;
        }
        Undo.RegisterCreatedObjectUndo(marker, "Place Terrain Marker");
        Selection.activeGameObject = marker;
    }
}
#endif
