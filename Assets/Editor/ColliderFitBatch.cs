#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ColliderFitBatch
{
    [MenuItem("Tools/Collectibles/Fit BoxColliders On Selection")]
    public static void FitSelected()
    {
        int n = 0;
        foreach (var go in Selection.gameObjects)
        {
            foreach (var box in go.GetComponentsInChildren<BoxCollider>(true))
            {
                var fitter = box.GetComponent<AutoFitBoxCollider>() ?? box.gameObject.AddComponent<AutoFitBoxCollider>();
                fitter.FitNow();
                n++;
            }
        }
        Debug.Log($"[FitSelected] Fitted {n} BoxCollider(s).");
    }
}
#endif

