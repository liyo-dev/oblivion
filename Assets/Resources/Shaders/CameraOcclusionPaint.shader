Shader "Custom/CameraOcclusionPaint"
{
    Properties
    {
        _BaseMap    ("Base Map",   2D)            = "white" {}
        _BaseColor  ("Base Color", Color)         = (1,1,1,1)
        _Reveal     ("Reveal",     Range(0,1))    = 0
        _NoiseScale ("Noise Scale",Float)         = 18
        _EdgeWidth  ("Edge Width", Range(0.001,1))= 0.07
        _EdgeColor  ("Edge Color", Color)         = (0.6,0.85,1,1)
        _EdgeGlow   ("Edge Glow Intensity", Float)= 3.0
    }
    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _EdgeColor;
                float  _Reveal;
                float  _NoiseScale;
                float  _EdgeWidth;
                float  _EdgeGlow;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Hash y noise de valor clásico — sin _Time para que el patrón sea estático
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Noise en espacio mundo sin animación → patrón estático y consistente
                float n = Noise(IN.positionWS.xz * _NoiseScale);

                // Zona visible (n > _Reveal), zona disuelta (n < _Reveal)
                float visible  = smoothstep(_Reveal - _EdgeWidth * 0.5, _Reveal + _EdgeWidth * 0.5, n);

                // Máscara de borde: intensidad máxima justo en el límite del dissolve
                float distEdge = abs(n - _Reveal);
                float edgeMask = saturate(1.0 - distEdge / max(_EdgeWidth, 0.001));
                edgeMask = edgeMask * edgeMask; // curva cuadrática → borde más definido

                // Aplicar glow solo en la zona parcialmente visible para que el borde brille
                col.rgb = lerp(col.rgb, _EdgeColor.rgb * _EdgeGlow, edgeMask * visible);

                // Alpha: combine el alpha global (desde C# / minimumAlpha) con el dissolve
                col.a *= visible;

                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
