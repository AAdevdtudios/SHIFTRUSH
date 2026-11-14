// SHIFT//RUSH toon look for car bodies: the sun in hard bands (lit / mid / shade in a cool violet), a crisp
// highlight, and a coloured rim on the edges facing away from the camera. Keeps the pack's palette texture.
// Point and spot lights (underglow, nitro) light it in bands too. Shadows, depth and normals come from URP/Lit.
Shader "ShiftRush/Toon Car"
{
    Properties
    {
        [MainTexture] _BaseMap ("Palette", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade", Color) = (0.52, 0.53, 0.62, 1)
        _MidColor ("Mid tone", Color) = (0.82, 0.82, 0.88, 1)
        _Bands ("Band edge (shade | mid | lit)", Vector) = (0.38, 0.62, 0, 0)
        _BandSoftness ("Band softness", Range(0.001, 0.1)) = 0.02
        _SpecColor ("Highlight", Color) = (1, 1, 1, 1)
        _SpecSize ("Highlight size", Range(0.8, 0.999)) = 0.965
        _RimColor ("Rim", Color) = (1, 1, 1, 1)
        _RimPower ("Rim width", Range(1, 8)) = 3
        _RimStrength ("Rim strength", Range(0, 2)) = 0.3
        _Ambient ("Sky light", Range(0, 1)) = 0.35
        _EmissionMap ("Glow map", 2D) = "white" {}
        [HDR] _EmissionColor ("Glow", Color) = (0, 0, 0, 1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Cut out (decals)", Float) = 0
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Surface ("Surface", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #pragma multi_compile_local_fragment _ _ALPHATEST_ON // always built: liveries switch it on at runtime

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _EmissionColor;
                half _AlphaClip;
                half4 _BaseColor, _ShadeColor, _MidColor, _SpecColor, _RimColor;
                float4 _Bands;
                half _BandSoftness, _SpecSize, _RimPower, _RimStrength, _Ambient, _Cutoff, _Surface;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // 0..1 light amount into three flat tones.
            half3 Bands(half l)
            {
                half s = _BandSoftness;
                half mid = smoothstep(_Bands.x - s, _Bands.x + s, l);
                half lit = smoothstep(_Bands.y - s, _Bands.y + s, l);
                return lerp(lerp(_ShadeColor.rgb, _MidColor.rgb, mid), half3(1, 1, 1), lit);
            }

            // A point or spot light in three flat steps of its real brightness, keeping its colour.
            half3 ToonLight(Light light, float3 n)
            {
                half3 lc = light.color * light.distanceAttenuation * light.shadowAttenuation;
                half peak = max(lc.r, max(lc.g, lc.b));
                half e = peak * saturate(dot(n, light.direction) * 0.5 + 0.5);
                half steps = smoothstep(0.04, 0.07, e) * 0.35 + smoothstep(0.3, 0.34, e) * 0.35 + smoothstep(0.8, 0.84, e) * 0.3;
                return lc / max(peak, 1e-4) * steps * max(1.0, e * 0.5);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(tex.a - _Cutoff); // decals: only the painted parts
                #endif
                half3 albedo = tex.rgb;
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half wrap = dot(n, sun.direction) * 0.5 + 0.5;                  // half-lambert: soft side light
                half l = wrap * lerp(0.35, 1.0, sun.shadowAttenuation);           // cast shadows drop a band
                half3 tone = Bands(l);
                half3 col = albedo * (tone * sun.color + SampleSH(n) * _Ambient);

                // A crisp highlight, only where the sun reaches.
                half nh = saturate(dot(n, normalize(sun.direction + v)));
                half spec = smoothstep(_SpecSize - 0.005, _SpecSize + 0.005, nh) * step(_Bands.y, l);
                col += _SpecColor.rgb * spec * sun.color * 0.6;

                // Point and spot lights, banded the same way.
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                uint count = GetAdditionalLightsCount();
                #if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                {
                    Light dl = GetAdditionalLight(li, i.positionWS, half4(1, 1, 1, 1));
                    col += albedo * ToonLight(dl, n);
                }
                #endif
                LIGHT_LOOP_BEGIN(count)
                    Light pl = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    col += albedo * ToonLight(pl, n);
                LIGHT_LOOP_END
                #endif

                // Rim: a thin band on the edges that face up the screen (roof line, spoiler, shoulders). Not on
                // the sides: a flat side panel turning edge-on in a corner would otherwise flood with colour.
                half rim = pow(1.0 - saturate(dot(n, v)), _RimPower);
                half3 screenUp = UNITY_MATRIX_V[1].xyz;
                rim = smoothstep(0.58, 0.62, rim) * saturate(dot(n, screenUp) * 2.0) * _RimStrength * lerp(0.5, 1.0, saturate(l * 1.4));
                col += _RimColor.rgb * rim;

                col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb; // lamps, glowing trim
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Shadows, depth and normals (the outline reads these) exactly as URP/Lit draws them.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Universal Render Pipeline/Lit"
}
