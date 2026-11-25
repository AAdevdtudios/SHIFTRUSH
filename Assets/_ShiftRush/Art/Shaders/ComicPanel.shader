// Grip Pro comic panel look, drawn over the lit frame just before post (bloom and tonemapping still follow): the picture is flattened into a few cel
// bands that keep each colour's hue, black ink goes on silhouettes, creases and strong value breaks, the shadows get
// halftone dots on a 45 degree screen grid, and paper grain and a vignette sit on top.
// Used only by the Comic renderer (Assets/Settings/Comic_Renderer), so the game's own cameras never see it.
// Full Screen Pass renderer feature: Depth + Normal requirements, color buffer fetched.
Shader "ShiftRush/Comic Panel"
{
    Properties
    {
        [Header(Exposure)]
        _Lift ("Exposure lift", Range(0.5, 2.5)) = 1.35
        _Gamma ("Gamma (below 1 opens the shadows)", Range(0.4, 1.5)) = 0.8
        _Saturation ("Saturation", Range(0, 2)) = 1.25

        [Header(Cel bands)]
        _Cuts ("Band cuts (value)", Vector) = (0.1, 0.26, 0.47, 0.7)
        _Band0 ("Band values 0-3", Vector) = (0.12, 0.32, 0.56, 0.8)
        _Band4 ("Top band value", Range(0.5, 1)) = 0.97
        _BandSoft ("Band edge softness", Range(0.001, 0.05)) = 0.012
        _CelAmount ("Cel amount", Range(0, 1)) = 1

        [Header(Ink)]
        _InkColor ("Ink", Color) = (0.03, 0.02, 0.06, 1)
        _Thickness ("Line width (pixels at 1080p)", Range(0.5, 4)) = 1.6
        _DepthThreshold ("Silhouette edge", Range(0.001, 0.2)) = 0.03
        _NormalThreshold ("Crease edge", Range(0.05, 1.5)) = 0.6
        _LumaThreshold ("Value break edge", Range(0.02, 1)) = 0.3
        _LumaWeight ("Value break amount", Range(0, 1)) = 0.8
        _InkFar ("Lines gone by (m)", Float) = 140

        [Header(Halftone)]
        _DotSize ("Dot cell (pixels at 1080p)", Range(3, 24)) = 9
        _DotStart ("Dots start below value", Range(0, 1)) = 0.5
        _DotRange ("Dots full at", Range(0.05, 1)) = 0.45
        _ShadowTint ("Shadow ink tint", Color) = (0.04, 0.02, 0.12, 1)
        _DotStrength ("Dot strength", Range(0, 1)) = 1

        [Header(Paper)]
        _Grain ("Grain", Range(0, 0.1)) = 0.025
        _Vignette ("Vignette", Range(0, 1)) = 0.35
        _Boil ("Grain boil frames per second (0 = still)", Range(0, 24)) = 8
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "ComicPanel"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Lift, _Gamma, _Saturation;
            float4 _Cuts, _Band0;
            float _Band4, _BandSoft, _CelAmount;
            half4 _InkColor;
            float _Thickness, _DepthThreshold, _NormalThreshold, _LumaThreshold, _LumaWeight, _InkFar;
            float _DotSize, _DotStart, _DotRange, _DotStrength;
            half4 _ShadowTint;
            float _Grain, _Vignette, _Boil;

            float3 Lifted(float2 uv)
            {
                float3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                return pow(saturate(c * _Lift), _Gamma);
            }

            float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
            float Eye(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            // Value into bands, with a soft step so band edges don't crawl when the camera moves.
            float Band(float v)
            {
                float b = _Band0.x;
                b = lerp(b, _Band0.y, smoothstep(_Cuts.x - _BandSoft, _Cuts.x + _BandSoft, v));
                b = lerp(b, _Band0.z, smoothstep(_Cuts.y - _BandSoft, _Cuts.y + _BandSoft, v));
                b = lerp(b, _Band0.w, smoothstep(_Cuts.z - _BandSoft, _Cuts.z + _BandSoft, v));
                b = lerp(b, _Band4,   smoothstep(_Cuts.w - _BandSoft, _Cuts.w + _BandSoft, v));
                return b;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float scale = _ScreenParams.y / 1080.0;
                float2 px = 1.0 / _ScreenParams.xy;

                float3 col = Lifted(uv);
                // Value from a small blur, so texture noise doesn't flick between bands.
                float2 b1 = px * 1.5 * scale;
                float v = (Luma(col) * 2.0
                         + Luma(Lifted(uv + float2(b1.x, 0))) + Luma(Lifted(uv - float2(b1.x, 0)))
                         + Luma(Lifted(uv + float2(0, b1.y))) + Luma(Lifted(uv - float2(0, b1.y)))) / 6.0;

                // --- cel: keep the hue and saturation, replace the value with its band
                float vq = Band(v);
                float3 grey = Luma(col).xxx;
                float3 hue = lerp(grey, col, _Saturation);
                float3 cel = saturate(hue * (vq / max(Luma(col), 0.02)));
                float3 c = lerp(col, cel, _CelAmount);

                // --- halftone: dots on a 45 degree grid in screen pixels, bigger where it's darker
                float2 sp = uv * _ScreenParams.xy;
                float cell = _DotSize * scale;
                float2 r = float2(sp.x + sp.y, sp.x - sp.y) * 0.70710678;
                float2 cellPos = frac(r / cell) - 0.5;
                float dist = length(cellPos) * cell;
                float dark = saturate((_DotStart - vq) / _DotRange);
                float dotMask = 1.0 - smoothstep(dark * cell * 0.46 - 0.75, dark * cell * 0.46 + 0.75, dist);
                dotMask *= step(vq, _DotStart);
                c = lerp(c, c * 0.35 + _ShadowTint.rgb * 0.65, dotMask * _DotStrength);

                // --- ink
                float d = Eye(uv);
                float2 o = _Thickness * scale * px;
                float2 o1 = float2(o.x, o.y), o2 = float2(-o.x, o.y);
                float d1 = Eye(uv + o1), d2 = Eye(uv - o1), d3 = Eye(uv + o2), d4 = Eye(uv - o2);
                float id = 1.0 / max(d, 0.001);
                float depthEdge = (abs(1.0 / max(d1, 0.001) + 1.0 / max(d2, 0.001) - 2.0 * id)
                                 + abs(1.0 / max(d3, 0.001) + 1.0 / max(d4, 0.001) - 2.0 * id)) / id;
                depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 1.6, depthEdge);

                float3 n1 = SampleSceneNormals(uv + o1), n2 = SampleSceneNormals(uv - o1);
                float3 n3 = SampleSceneNormals(uv + o2), n4 = SampleSceneNormals(uv - o2);
                float normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 1.3, length(n1 - n2) + length(n3 - n4));
                normalEdge *= 1.0 - smoothstep(20.0, 40.0, d); // creases only on things close by

                // Sobel on the value: seams, logos and the edges of light pools get a line too.
                float l00 = Luma(Lifted(uv + float2(-o.x, -o.y))), l10 = Luma(Lifted(uv + float2(0, -o.y))), l20 = Luma(Lifted(uv + float2(o.x, -o.y)));
                float l01 = Luma(Lifted(uv + float2(-o.x, 0))),                                             l21 = Luma(Lifted(uv + float2(o.x, 0)));
                float l02 = Luma(Lifted(uv + float2(-o.x, o.y))), l12 = Luma(Lifted(uv + float2(0, o.y))),  l22 = Luma(Lifted(uv + float2(o.x, o.y)));
                float gx = (l20 + 2.0 * l21 + l22) - (l00 + 2.0 * l01 + l02);
                float gy = (l02 + 2.0 * l12 + l22) - (l00 + 2.0 * l10 + l20);
                float lumaEdge = smoothstep(_LumaThreshold, _LumaThreshold * 1.5, sqrt(gx * gx + gy * gy)) * _LumaWeight;

                float edge = saturate(max(depthEdge, max(normalEdge, lumaEdge)));
                edge *= 1.0 - smoothstep(_InkFar * 0.6, _InkFar, d);
                c = lerp(c, _InkColor.rgb, edge);

                // --- paper: grain (it can boil a few times a second like a printed page under a camera) and vignette
                float frame = _Boil > 0 ? floor(_Time.y * _Boil) : 0;
                float grain = (Hash(floor(sp) + frame * 17.31) - 0.5) * 2.0 * _Grain * 1.7;
                float2 q = uv * 2.0 - 1.0;
                float vig = clamp(1.1 - length(q) * _Vignette, 0.6, 1.0);
                c = saturate((c + grain) * vig);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
