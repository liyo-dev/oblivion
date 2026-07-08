// Height Fog shader — actualizado para URP 17.x (Unity 6)
// Original: SKGames (URP 7.4.x). Ported to URP 17 by project maintainer.
Shader "SKGames/Height Fog (URP 17)"
{
    Properties
    {
        [Header(Fog properties)]
        [PerRendererData][Enum(World,1,Local,0)] _FogRelativeWorldOrLocal("Fog Simulation Space", Int) = 1
        [PerRendererData] _FogColor("Fog Color", Color) = (1,1,1,1)
        [PerRendererData][HDR] _FogEmissionColor("Fog Emission Color", Color) = (1,1,1,1)
        [PerRendererData] _FogMin("Height Fog Min", Float) = -20
        [PerRendererData] _FogMax("Height Fog Max", Float) = 0
        [PerRendererData] _EmissionPower("Emission Power", Range(0, 1)) = 1
        [PerRendererData][PowerSlider(3.0)] _FogEmissionPower("Fog Emission Power", Range(0, 100)) = 20
        [PerRendererData][PowerSlider(3.0)] _FogEmissionFalloff("Fog Emission Falloff", Range(0.01, 20)) = 0.5
        [PerRendererData][PowerSlider(3.0)] _FogFalloff("Fog Falloff", Range(0.01, 20)) = 1
        [Header(STANDARD fog properties overrides)]
        [PerRendererData] _STANDARD_FOG("Combine with STANDARD fog", Float) = 0
        [PerRendererData] _OVERRIDE_FOG_COLOR("Override STANDARD fog color (forward only)", Float) = 0
        [Header(Fog animation properties)]
        [PerRendererData] _ANIMATION("Use fog animation", Float) = 0
        [PerRendererData] _FogWaveSpeedX("Fog Wave Speed X", Range(-50, 50)) = 2
        [PerRendererData] _FogWaveSpeedZ("Fog Wave Speed Z", Range(-50, 50)) = 2
        [PerRendererData] _FogWaveAmplitudeX("Fog Wave Amplitude X", Range(0, 1)) = 0.3
        [PerRendererData] _FogWaveAmplitudeZ("Fog Wave Amplitude Z", Range(0, 1)) = 0.3
        [PerRendererData] _FogWaveFreqX("Fog Frequency X", Range(0, 20)) = 0.5
        [PerRendererData] _FogWaveFreqZ("Fog Frequency Z", Range(0, 20)) = 0.5

        [HideInInspector] _WorkflowMode("WorkflowMode", Float) = 1.0
        _Color("Color", Color) = (0.5,0.5,0.5,1)
        _MainTex("Albedo", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0
        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
        _ReceiveShadows("Receive Shadows", Float) = 1.0
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma multi_compile_instancing

            // Material keywords
            #pragma shader_feature_local        _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature_local        _SPECULAR_SETUP
            #pragma shader_feature_local        _RECEIVE_SHADOWS_OFF

            // URP 17 light/shadow keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ---- CBUFFER (SRP Batcher compatible) ----
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MainTex_ST;
                float4 _EmissionColor;
                float4 _FogColor;
                float4 _FogEmissionColor;
                half   _Glossiness;
                half   _GlossMapScale;
                half   _Metallic;
                half   _BumpScale;
                half   _OcclusionStrength;
                float  _Cutoff;
                float  _FogMin;
                float  _FogMax;
                float  _FogFalloff;
                float  _FogEmissionPower;
                float  _FogEmissionFalloff;
                float  _FogRelativeWorldOrLocal;
                float  _EmissionPower;
                float  _FogWaveSpeedX;
                float  _FogWaveSpeedZ;
                float  _FogWaveAmplitudeX;
                float  _FogWaveAmplitudeZ;
                float  _FogWaveFreqX;
                float  _FogWaveFreqZ;
                float  _ANIMATION;
                float  _STANDARD_FOG;
                float  _OVERRIDE_FOG_COLOR;
            CBUFFER_END

            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);

            // ---- Structs ----
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float2 uvLM       : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uvLM        : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                half3  normalWS    : TEXCOORD3;
                half3  positionOS  : TEXCOORD4;
            #if _NORMALMAP
                half4  tangentWS   : TEXCOORD5;  // xyz: tangent, w: sign
            #endif
                float4 shadowCoord : TEXCOORD6;
                half   fogFactor   : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- Height fog logic (idéntica al original) ----
            float3 WaveOffset(float3 p)
            {
                if (_ANIMATION > 0)
                {
                    float tx = _Time.x * 20.0 * -_FogWaveSpeedX;
                    float tz = _Time.x * 20.0 * -_FogWaveSpeedZ;
                    float dy = (sin(tx + p.x * _FogWaveFreqX) * _FogWaveAmplitudeX
                              + sin(tz + p.z * _FogWaveFreqZ) * _FogWaveAmplitudeZ) * 0.5;
                    return float3(p.x, p.y + dy, p.z);
                }
                return p;
            }

            float3 ApplyHeightFog(float3 color, float3 posOS, float3 posWS)
            {
                float3 lp  = WaveOffset(posOS);
                float3 wp  = WaveOffset(posWS);
                float  y   = lp.y * saturate(1.0 - _FogRelativeWorldOrLocal)
                           + wp.y * saturate(_FogRelativeWorldOrLocal);
                float  lv  = 1.0 - pow(saturate((y - _FogMin) / (_FogMax - _FogMin)), _FogFalloff);
                float3 em  = _FogColor.rgb + _FogEmissionColor.rgb * _FogEmissionPower;
                float3 fc  = lerp(_FogColor.rgb, em, pow(lv, _FogEmissionFalloff));
                return lerp(color, fc, lv);
            }

            // ---- Vertex ----
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.uvLM       = input.uvLM.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                output.normalWS   = vni.normalWS;
                output.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
            #if _NORMALMAP
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS  = half4(vni.tangentWS, sign);
            #endif
                output.shadowCoord = GetShadowCoord(vpi);
                return output;
            }

            // ---- Fragment ----
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Albedo + alpha
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half3 albedo = albedoAlpha.rgb;
                half  alpha  = albedoAlpha.a;
            #if _ALPHATEST_ON
                clip(alpha - _Cutoff);
            #endif

                // Normal
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            #if _NORMALMAP
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangentWS = input.tangentWS.w
                                  * cross(half3(input.normalWS), half3(input.tangentWS.xyz));
                normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(
                    normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));
            #endif

                // Metallic / smoothness
                half metallic   = _Metallic;
                half smoothness = _Glossiness;
            #if _METALLICSPECGLOSSMAP
                half4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                metallic   = mg.r;
                smoothness = mg.a * _GlossMapScale;
            #endif

                // Occlusion
                half occlusion = 1;
            #if _OCCLUSIONMAP
                occlusion = lerp(1, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g,
                                 _OcclusionStrength);
            #endif

                // Emission
                half3 emission = 0;
            #if _EMISSION
                emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb
                         * _EmissionColor.rgb;
            #endif

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // BRDF
                BRDFData brdfData;
                InitializeBRDFData(albedo, metallic, half3(0,0,0), smoothness, alpha, brdfData);

                // GI (SH — compatible con cualquier configuración de lightmap)
                half3 bakedGI = SampleSH(normalWS) * occlusion;
                half3 color   = bakedGI * brdfData.diffuse;

                // Luz principal
                Light mainLight = GetMainLight(input.shadowCoord);
                color += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirWS);

                // Luces adicionales
            #ifdef _ADDITIONAL_LIGHTS
                int count = GetAdditionalLightsCount();
                for (int i = 0; i < count; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    color += LightingPhysicallyBased(brdfData, light, normalWS, viewDirWS);
                }
            #endif

                color += emission;

                // Fog de escena (RenderSettings)
                color = MixFog(color, input.fogFactor);

                // Height fog
                color = ApplyHeightFog(color, input.positionOS, input.positionWS);

                return half4(color, alpha);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/Meta"
    }

    FallBack "Hidden/InternalErrorShader"
    CustomEditor "HeightFogLWRPGUI"
}
