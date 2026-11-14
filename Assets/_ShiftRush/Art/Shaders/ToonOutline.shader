// SHIFT//RUSH ink outlines over the whole scene, drawn once from depth and normals (no material changes needed).
// Lines appear where depth jumps (silhouettes) or the surface turns sharply (panel creases), thin out with
// distance so far trees don't turn to noise, and soften at speed (_Strength is driven by the game).
// Used by a URP Full Screen Pass renderer feature (Depth + Normal requirements, color buffer fetched).
Shader "ShiftRush/Toon Outline"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.06, 0.04, 0.12, 1)
        _Thickness ("Thickness (pixels at 1080p)", Range(0.5, 4)) = 1.4
        _DepthThreshold ("Depth edge", Range(0.001, 0.2)) = 0.035
        _NormalThreshold ("Crease edge", Range(0.05, 1.5)) = 0.75
        _NormalWeight ("Crease amount", Range(0, 1)) = 0.6
        _CreaseFadeEnd ("Crease lines gone by (m)", Float) = 40
        _FarInk ("Ink kept far away", Range(0, 1)) = 0.45
        _FadeStart ("Fade from (m)", Float) = 60
        _FadeEnd ("Gone by (m)", Float) = 260
        _Strength ("Strength", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ToonOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _InkColor;
            float _CreaseFadeEnd, _FarInk;
            float _Thickness, _DepthThreshold, _NormalThreshold, _NormalWeight, _FadeStart, _FadeEnd, _Strength;
            float _ToonSpeedSoften; // set by the game: 0 standing, 1 flat out

            float Eye(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float d = Eye(uv);
                if (d > _FadeEnd) return scene; // sky and far hills: no lines

                float2 px = _Thickness * (_ScreenParams.y / 1080.0) / _ScreenParams.xy;
                // Roberts cross on depth and on normals.
                float2 o1 = float2(px.x, px.y), o2 = float2(-px.x, px.y);
                float d1 = Eye(uv + o1), d2 = Eye(uv - o1), d3 = Eye(uv + o2), d4 = Eye(uv - o2);
                // Second difference: a flat surface, however steeply it recedes (the road ahead), changes depth
                // at a steady rate and scores ~0; only a real step (a silhouette) stands out.
                // Taken on 1/depth, which is exactly linear across a plane on screen.
                float id = 1.0 / max(d, 0.001);
                float depthEdge = (abs(1.0 / max(d1, 0.001) + 1.0 / max(d2, 0.001) - 2.0 * id)
                                 + abs(1.0 / max(d3, 0.001) + 1.0 / max(d4, 0.001) - 2.0 * id)) / id;
                depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 1.6, depthEdge);

                float3 n1 = SampleSceneNormals(uv + o1), n2 = SampleSceneNormals(uv - o1);
                float3 n3 = SampleSceneNormals(uv + o2), n4 = SampleSceneNormals(uv - o2);
                float normalEdge = length(n1 - n2) + length(n3 - n4);
                normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 1.3, normalEdge) * _NormalWeight;
                // Creases (panel lines) are for things close by: on a car 40 m away they'd fill it with ink.
                normalEdge *= 1.0 - smoothstep(_CreaseFadeEnd * 0.5, _CreaseFadeEnd, d);

                float edge = saturate(max(depthEdge, normalEdge));
                // Lighter ink with distance (small things would go dark), then none at all.
                float fade = lerp(1.0, _FarInk, smoothstep(15.0, _FadeStart, d)) * (1.0 - smoothstep(_FadeStart, _FadeEnd, d));
                edge *= fade * _Strength * (1.0 - 0.45 * _ToonSpeedSoften);
                return half4(lerp(scene.rgb, _InkColor.rgb, edge), scene.a);
            }
            ENDHLSL
        }
    }
}
