#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Collections.Generic;

public class MissingReferenceFinder : EditorWindow
{
    [MenuItem("Tools/Find Missing Inspector References")]
    static void ShowWindow()
    {
        GetWindow<MissingReferenceFinder>("Find Missing References");
    }

    private Vector2 scroll;
    private List<string> missingList = new List<string>();

    void OnGUI()
    {
        if (GUILayout.Button("Scan Scene for Missing References"))
        {
            missingList.Clear();
            ScanScene();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var entry in missingList)
        {
            EditorGUILayout.HelpBox(entry, MessageType.Error);
        }
        EditorGUILayout.EndScrollView();

        if (missingList.Count == 0 && GUILayout.Button("No missing references found. (Scan Again)"))
        {
            ScanScene();
        }
    }

    void ScanScene()
    {
        var allMonoBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>();
        foreach (var mb in allMonoBehaviours)
        {
            if (mb == null) continue;
            var so = new SerializedObject(mb);
            var sp = so.GetIterator();
            while (sp.NextVisible(true))
            {
                if (sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    // Only care about fields visible in Inspector and NOT the script ref
                    if (sp.name == "m_Script") continue;
                    if (sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                    {
                        string path = GameObjectPath(mb.gameObject);
                        string msg = $"{mb.GetType().Name} on {path} is missing reference: '{sp.displayName}'";
                        missingList.Add(msg);
                        Debug.LogWarning(msg, mb.gameObject);
                    }
                }
            }
        }

        if (missingList.Count == 0)
            missingList.Add("🎉 No missing references found!");
    }

    // Helper to get hierarchy path for easier locating in large scenes
    string GameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }
}
#endif
