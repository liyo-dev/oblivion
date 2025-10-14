Shader "URP/BlackHoleProjectile"
{
    Properties
    {
        _MainTex("Mask (radial/optional)", 2D) = "white" {}
        _NoiseTex("Noise", 2D) = "white" {}
        _GlowColor("Glow Color", Color) = (0.6,0.7,1,1)
        _GlowIntensity("Glow Intensity", Range(0,8)) = 2.5

        _CoreRadius("Core Radius", Range(0,0.49)) = 0.18
        _EdgeWidth("Edge Width", Range(0.001,0.5)) = 0.12

        _Distortion("Distortion Strength", Range(0,0.25)) = 0.12
        _DistFalloff("Distortion Falloff", Range(0.1,8)) = 3.0

        _NoiseAmount("Noise Amount", Range(0,1)) = 0.35
        _NoiseSpeed("Noise Scroll", Vector) = (0.2, 0.1, 0, 0)

        _Chromatic("Chromatic Split", Range(0,2)) = 0.6
        _Opacity("Overall Opacity", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ _USE_OPAQUE_TEXTURE

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 uvNoise    : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);     SAMPLER(sampler_NoiseTex);

            float4 _GlowColor;
            float  _GlowIntensity;

            float  _CoreRadius;
            float  _EdgeWidth;

            float  _Distortion;
            float  _DistFalloff;

            float  _NoiseAmount;
            float4 _NoiseSpeed;

            float  _Chromatic;
            float  _Opacity;

            float4 _MainTex_ST;
            float4 _NoiseTex_ST;
            // NOTE: _Time is defined by Unity; do NOT redeclare it.

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                o.uvNoise = TRANSFORM_TEX(IN.uv, _NoiseTex) + (_NoiseSpeed.xy * _Time.y);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            // Radial helpers
            float radialMask(float2 uv, float2 center, float radius, float width)
            {
                float d = distance(uv, center);
                return smoothstep(radius, radius + width, d);
            }

            float edgeRing(float2 uv, float2 center, float radius, float width)
            {
                float d = distance(uv, center);
                float inner = smoothstep(radius, radius - width*0.5, d);
                float outer = smoothstep(radius + width, radius + width*0.5, d);
                return saturate(inner - outer);
            }

            float falloff(float2 uv, float2 center, float radius, float width, float k)
            {
                float d = distance(uv, center);
                float t = saturate((d - radius) / (radius + width));
                return pow(1.0 - t, k);
            }

            float4 frag (Varyings IN) : SV_Target
            {
                float2 uvScreen = IN.screenPos.xy / IN.screenPos.w;
                float2 center = float2(0.5, 0.5);

                float mainMask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).r;
                float n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uvNoise).r;

                float ring = edgeRing(uvScreen, center, _CoreRadius, _EdgeWidth);
                float edgeFall = falloff(uvScreen, center, _CoreRadius, _EdgeWidth, _DistFalloff);
                float outsideMask = radialMask(uvScreen, center, _CoreRadius, _EdgeWidth);
                float insideMask = 1.0 - outsideMask;

                float2 dirToCenter = normalize(center - uvScreen + 1e-5);
                float noiseFactor = lerp(1.0, 1.0 + (n - 0.5) * 2.0 * _NoiseAmount, edgeFall);
                float2 offset = dirToCenter * _Distortion * edgeFall * noiseFactor;

                float4 sceneC = SampleSceneColor(uvScreen + offset);

                float2 offR = offset * (_Chromatic * 0.66);
                float2 offB = -offset * (_Chromatic * 0.33);
                float r = SampleSceneColor(uvScreen + offR).r;
                float g = sceneC.g;
                float b = SampleSceneColor(uvScreen + offB).b;
                float3 distorted = float3(r, g, b);

                float3 darkened = distorted * (1.0 - insideMask * 0.98);

                float glow = ring * _GlowIntensity * (0.6 + 0.4 * n) * (0.5 + 0.5 * edgeFall);
                float3 glowCol = _GlowColor.rgb * glow;

                float3 col = darkened + glowCol;

                float alpha = saturate(glow * 0.5 + insideMask * 0.95) * _Opacity;

                col = lerp(distorted, col, mainMask);
                alpha *= mainMask;

                return float4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}