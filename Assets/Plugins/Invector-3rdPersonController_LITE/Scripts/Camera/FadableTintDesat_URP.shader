Shader "URP/FadableTintDesat"
{
    Properties
    {
        _BaseMap   ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        // Controlados por el script (MaterialPropertyBlock)
        _TintColor ("Tint (A = Alpha)", Color) = (1,1,1,1)
        _Desat     ("Desaturation", Range(0,1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma prefer_hlslcc gles
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float4 _BaseColor;

            float4 _TintColor; // (rgb = tinte, a = alpha final multiplicativa)
            float   _Desat;    // 0 = color, 1 = B/N

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            float3 Desaturate(float3 rgb, float amount)
            {
                // luma aprox. (Rec.601)
                float gray = dot(rgb, float3(0.299, 0.587, 0.114));
                return lerp(rgb, gray.xxx, saturate(amount));
            }

            half4 frag (Varyings i) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // desaturar
                albedo.rgb = Desaturate(albedo.rgb, _Desat);

                // tinte + alfa controlado por _TintColor
                albedo.rgb *= _TintColor.rgb;
                albedo.a   *= _TintColor.a;

                return albedo;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
