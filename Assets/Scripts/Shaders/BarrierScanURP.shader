Shader "Liyo/BarrierScanURP"
{
    Properties
    {
        _BaseColor   ("Base Color (RGBA = color+alpha)", Color) = (0.3,0.5,1,0.15)
        _BandColor   ("Scan/Band Color", Color) = (1,1,1,1)
        _BandWidth   ("Band Width", Range(0.001, 2)) = 0.35
        _BandSpeed   ("Band Speed (ups)", Range(-10,10)) = 1.5
        _RimIntensity("Rim Intensity", Range(0,3)) = 0.6
        _RimPower    ("Rim Power", Range(0.1,8)) = 2.2
        _MinY        ("Min Y", Float) = 0
        _MaxY        ("Max Y", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BandColor;
                float  _BandWidth;
                float  _BandSpeed;
                float  _RimIntensity;
                float  _RimPower;
                float  _MinY;
                float  _MaxY;
            CBUFFER_END

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.positionWS  = positionWS;
                OUT.normalWS    = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.viewDirWS   = GetWorldSpaceViewDir(positionWS);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                half4 col = _BaseColor;

                // Fresnel (borde)
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float  rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimIntensity;

                // Banda vertical (scan) entre MinY..MaxY
                float height = max(_MaxY - _MinY, 1e-4);
                float t = frac(_Time.y * _BandSpeed);          // 0..1
                float bandCenter = _MinY + t * height;
                float w = _BandWidth * 0.5;
                float band = smoothstep(bandCenter - w, bandCenter, IN.positionWS.y)
                           * (1.0 - smoothstep(bandCenter, bandCenter + w, IN.positionWS.y));

                float bandGlow = band;
                float3 rgb = col.rgb + _BandColor.rgb * bandGlow + rim * col.rgb;
                float  a   = saturate(col.a + bandGlow * 0.15);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
