using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GaiaInstancingFix : MonoBehaviour
{
    // Run this method from the menu item
    #if UNITY_EDITOR
    [MenuItem("Tools/Gaia/Disable GPU Instancing on All Gaia Materials")]
    #endif
    public static void DisableGaiaInstancing()
    {
        #if UNITY_EDITOR
        // Search for all materials in the project
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");
        int materialsFixed = 0;

        foreach (string guid in materialGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Focus on materials in Gaia folders to be safe
            if (path.Contains("Gaia") || path.Contains("GAIA"))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && material.enableInstancing)
                {
                    material.enableInstancing = false;
                    Debug.Log("Disabled GPU Instancing on: " + path, material);
                    materialsFixed++;
                    // Mark the material as dirty to save changes
                    EditorUtility.SetDirty(material);
                }
            }
        }

        // Save all changes to the assets
        AssetDatabase.SaveAssets();
        Debug.Log($"GPU Instancing fix completed! Fixed {materialsFixed} materials.");
        #endif
    }

    // Optional: Run automatically when entering Play Mode
    void Start()
    {
        DisableGaiaInstancing();
    }
}