using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CreateOptimizedGodRayPrefab
{
    const string kSavePath = "Assets/Rumble/FX/OptimizedGodRay.prefab";

    [MenuItem("Tools/FX/Create OptimizedGodRay Prefab From Selection")]
    static void CreateFromSelection()
    {
        var src = Selection.activeGameObject;
        if (!src)
        {
            EditorUtility.DisplayDialog("OptimizedGodRay",
                "Select a GodRay (Particle System) object in the Hierarchy.", "OK");
            return;
        }

        // Root + LODGroup
        var root = new GameObject("OptimizedGodRay");
        Undo.RegisterCreatedObjectUndo(root, "Create OptimizedGodRay");
        var lod = root.AddComponent<LODGroup>();
        lod.fadeMode = LODFadeMode.None;

        // Build children from the selected source
        var lod0 = InstantiateParticleChild(src, root.transform, "LOD0_Rich",
            maxParticles: 250, rateOverTime: 8f, noiseStrength: 0.35f, noiseFrequency: 0.35f);

        var lod1 = InstantiateParticleChild(src, root.transform, "LOD1_Lite",
            maxParticles: 120, rateOverTime: 3f, noiseStrength: 0.18f, noiseFrequency: 0.25f);

        // Tweak renderers/materials
        TuneRenderer(lod0);
        TuneRenderer(lod1);

        // LODs (LOD0 near, LOD1 mid, culled far)
        var r0 = lod0.GetComponent<Renderer>();
        var r1 = lod1.GetComponent<Renderer>();
        lod.SetLODs(new[]
        {
            new LOD(0.45f, new[] { r0 }),
            new LOD(0.15f, new[] { r1 }),
            new LOD(0.00f, System.Array.Empty<Renderer>()) // culled
        });
        lod.RecalculateBounds();

        // Optional: add culling script if it exists in your project
        var cullingType = System.Type.GetType("RayGroupCulling, Assembly-CSharp");
        if (cullingType != null) root.AddComponent(cullingType);

        // Save prefab
        EnsureFolder("Assets/Rumble/FX");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, kSavePath);
        Object.DestroyImmediate(root);

        if (prefab) EditorUtility.DisplayDialog("OptimizedGodRay", $"Created:\n{kSavePath}", "OK");
        else EditorUtility.DisplayDialog("OptimizedGodRay", "Failed to save prefab.", "OK");
    }

    static GameObject InstantiateParticleChild(GameObject src, Transform parent, string name,
        int maxParticles, float rateOverTime, float noiseStrength, float noiseFrequency)
    {
        // Find a ParticleSystem on the selected object or its children
        var srcPs = src.GetComponentInChildren<ParticleSystem>(true);
        if (!srcPs) throw new System.Exception("Selected object has no ParticleSystem.");

        // Child container
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        // ParticleSystem
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;

        // Godray look (cheap)
        main.startLifetime = 5f;
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = 6f;
        main.startSizeY = 18f;
        main.startSizeZ = 1f;
        main.maxParticles = maxParticles;

        // Emission
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = rateOverTime;

        // Shape
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, 6f, 0f);
        shape.scale = new Vector3(7f, 7f, 7f);

        // Subtle shimmer
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.octaveCount = 1;
        noise.scrollSpeed = 0.05f;

        // Renderer (Unity adds this automatically with ParticleSystem)
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (!r) r = go.AddComponent<ParticleSystemRenderer>(); // safety

        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        // Material: reuse source if compatible, else create safe URP/Particles/Unlit
        Material mat = null;
        var srcR = srcPs.GetComponent<ParticleSystemRenderer>();
        if (srcR && srcR.sharedMaterial && srcR.sharedMaterial.shader)
        {
            var names = srcR.sharedMaterial.shader.name;
            if (names.Contains("Universal Render Pipeline/Particles/Unlit"))
                mat = new Material(srcR.sharedMaterial);
        }
        if (!mat)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!shader) shader = Shader.Find("Particles/Unlit"); // fallback
            mat = new Material(shader) { name = "M_GodRay_Add" };
        }
        mat.enableInstancing = true;
        r.sharedMaterial = mat;

        return go;
    }

    static void TuneRenderer(GameObject go)
    {
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (!r || !r.sharedMaterial) return;
        r.receiveShadows = false;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        if (!r.sharedMaterial.enableInstancing) r.sharedMaterial.enableInstancing = true;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var build = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (!AssetDatabase.IsValidFolder($"{build}/{parts[i]}"))
                AssetDatabase.CreateFolder(build, parts[i]);
            build = $"{build}/{parts[i]}";
        }
    }
}
