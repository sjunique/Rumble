
using System.IO;
using UnityEngine;  
 

#if UNITY_EDITOR
using UnityEditor;
public static class SaveDebugMenu {
  [MenuItem("Tools/Save/Show Save Folder")]
  public static void ShowSaveFolder() {
    var path = Application.persistentDataPath;
    Debug.Log("[Save] " + path);
    EditorUtility.RevealInFinder(path);
  }
}
#endif

