 

using UnityEditor;
using UnityEngine;

public static class UndoHistoryManager
{
    private const string MenuPath = "Tools/Undo/Advanced/Clear & Log Undo History";

    [MenuItem(MenuPath)]
    public static void ClearAndLogUndoHistory()
    {
        int undoCount = Undo.GetCurrentGroup();
        
        // Show confirmation dialog
        bool confirm = EditorUtility.DisplayDialog(
            "Clear Undo History?",
            $"This will remove {undoCount} undo steps.\nProceed?",
            "Yes, Clear It",
            "Cancel"
        );

        if (confirm)
        {
            Undo.ClearAll();
            //Debug.Log($"Cleared {undoCount} undo steps. History is now empty.");
            EditorUtility.DisplayDialog(
                "Success",
                "Undo history cleared.",
                "OK"
            );
        }
        else
        {
            //Debug.Log("Undo history preservation cancelled by user.");
        }
    }

    // Optional: Auto-clear on save with confirmation (uncomment to enable)
    /*
    [InitializeOnLoad]
    public static class AutoClearOnSave
    {
        static AutoClearOnSave()
        {
            EditorApplication.saveScene += () => 
            {
                if (EditorUtility.DisplayDialog(
                    "Clear Undo on Save?",
                    "Do you want to clear undo history after saving?",
                    "Yes",
                    "No"))
                {
                    int steps = Undo.GetCurrentGroup();
                    Undo.ClearAll();
                    //Debug.Log($"Scene saved. Cleared {steps} undo steps.");
                }
            };
        }
    }
    */
}
