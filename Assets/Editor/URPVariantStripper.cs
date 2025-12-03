#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
// IMPORTANT: we implement the fully-qualified interface name to dodge namespace hiccups
public class URPVariantStripper :IPreprocessShaders
{
    public int callbackOrder => 1000;

    // Toggle what you want to strip
    const bool STRIP_LIGHTMAPS = true;                  // you're not baking yet
    const bool STRIP_FOG = true;                        // you said no fog for now
    const bool STRIP_ADDITIONAL_LIGHTS = true;
    const bool STRIP_ADDITIONAL_LIGHT_SHADOWS = true;
    const bool STRIP_SOFT_SHADOWS = true;
    const bool STRIP_RECEIVE_SHADOWS_OFF = true;
    const bool STRIP_CLEARCOAT_PARALLAX_DETAIL = true;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> variants)
    {
        if (shader == null) return;

        // Only touch URP stock shaders. Remove this guard if you also want to strip your SGs.
        if (!shader.name.StartsWith("Universal Render Pipeline/"))
            return;

        for (int i = variants.Count - 1; i >= 0; --i)
        {
            var v = variants[i];
            var set = v.shaderKeywordSet;

            bool strip = false;

            if (STRIP_LIGHTMAPS && (set.IsEnabled(new ShaderKeyword("LIGHTMAP_ON")) ||
                                    set.IsEnabled(new ShaderKeyword("DIRLIGHTMAP_COMBINED")) ||
                                    set.IsEnabled(new ShaderKeyword("LIGHTMAP_SHADOW_MIXING")) ||
                                    set.IsEnabled(new ShaderKeyword("SHADOWS_SHADOWMASK"))))
                strip = true;

            if (STRIP_FOG && (set.IsEnabled(new ShaderKeyword("FOG_LINEAR")) ||
                              set.IsEnabled(new ShaderKeyword("FOG_EXP")) ||
                              set.IsEnabled(new ShaderKeyword("FOG_EXP2"))))
                strip = true;

            if (STRIP_ADDITIONAL_LIGHTS && set.IsEnabled(new ShaderKeyword("ADDITIONAL_LIGHTS")))
                strip = true;

            if (STRIP_ADDITIONAL_LIGHT_SHADOWS && set.IsEnabled(new ShaderKeyword("ADDITIONAL_LIGHT_SHADOWS")))
                strip = true;

            if (STRIP_SOFT_SHADOWS && set.IsEnabled(new ShaderKeyword("SHADOWS_SOFT")))
                strip = true;

            if (STRIP_RECEIVE_SHADOWS_OFF && set.IsEnabled(new ShaderKeyword("RECEIVE_SHADOWS_OFF")))
                strip = true;

            if (STRIP_CLEARCOAT_PARALLAX_DETAIL &&
               (set.IsEnabled(new ShaderKeyword("_CLEARCOAT")) ||
                set.IsEnabled(new ShaderKeyword("_PARALLAXMAP")) ||
                set.IsEnabled(new ShaderKeyword("_DETAIL_MULX2"))))
                strip = true;

            if (strip) variants.RemoveAt(i);
        }
    }
}
#endif
