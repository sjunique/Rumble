#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EmeraldSceneFixer
{
    [MenuItem("Tools/Emerald/Normalize Scene AIs")]
    public static void Normalize()
    {
        int aiLayer = LayerMask.NameToLayer("AI");
        if (aiLayer < 0)
        {
            Debug.LogWarning("No 'AI' layer found. Create one (Edit > Project Settings > Tags and Layers). Using Default for now.");
            aiLayer = 0; // Default
        }

        var emeraldType = System.Type.GetType("EmeraldAI.EmeraldAISystem, Emerald AI");
        if (emeraldType == null)
        {
            Debug.LogError("EmeraldAISystem type not found. Is Emerald AI imported?");
            return;
        }

        int fixedCount = 0;
        foreach (var comp in Object.FindObjectsOfType(emeraldType, true))
        {
            var go = (comp as Component).gameObject;

            // Tag/Layer
            if (go.tag == "Water") go.tag = "Enemy";
            if (go.layer == LayerMask.NameToLayer("Respawn")) go.layer = aiLayer;

            // Detection: include Default (player), exclude AI
            TrySet(comp, "PlayerTag", "Player");
            TrySet(comp, "DetectionAngle", 360);
            TrySet(comp, "DetectionRadius", 35f);

            // Most Emerald versions have either LayerMask or int for detection layers
            var defaultMask = LayerMask.GetMask("Default");
            TrySet(comp, "DetectionLayers", (LayerMask)defaultMask);
            TrySet(comp, "DetectionLayerMask", defaultMask);

            fixedCount++;
        }

        Debug.Log($"[EmeraldSceneFixer] Normalized {fixedCount} Emerald AI object(s). Review factions manually.");
    }

    static void TrySet(object obj, string name, object value)
    {
        var t = obj.GetType();
        var f = t.GetField(name, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (f != null)
        {
            if (value is LayerMask lm && f.FieldType == typeof(int))
                f.SetValue(obj, lm.value);
            else
                f.SetValue(obj, value);
            return;
        }
        var p = t.GetProperty(name, System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if (p != null && p.CanWrite)
        {
            if (value is LayerMask lm && p.PropertyType == typeof(int))
                p.SetValue(obj, lm.value);
            else
                p.SetValue(obj, value);
        }
    }
}
#endif
