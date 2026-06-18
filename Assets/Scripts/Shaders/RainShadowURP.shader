Shader "Liyo/RainShadowURP"
{
    // Indicador de área de impacto para la lluvia de ataques del ImpDemon (2ª aparición).
    // Se instancia en RainAttack() y se escala de 0→1 vía ImpDemonAI.
    // Blend soft-additive: no bloquea la visión del suelo, se suma a la escena.
    Properties
    {
        _Color      ("Color relleno (RGBA)",  Color)         = (1, 0.45, 0, 0.35)
        _RingColor  ("Color anillo (RGBA)",   Color)         = (1, 0.75, 0.1, 1)
        _RingWidth  ("Ancho anillo",          Range(0.01, 0.3)) = 0.07
        _FillAlpha  ("Alfa relleno interior", Range(0, 1))   = 0.30
        _PulseSpeed ("Velocidad pulso",       Range(0, 8))   = 2.5
        _GlobalAlpha("Alfa global",           Range(0, 1))   = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Cull   Off      // visible desde arriba y abajo
        ZWrite Off
        Blend  SrcAlpha One   // soft-additive: Source.rgb * Source.a + Dest.rgb
        Offset -1, -1         // evita z-fighting con el suelo

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RingColor;
                float  _RingWidth;
                float  _FillAlpha;
                float  _PulseSpeed;
                float  _GlobalAlpha;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // UV centradas en [-1, 1] → distancia radial al centro
                float2 centered = IN.uv * 2.0 - 1.0;
                float  dist     = length(centered); // 0=centro, 1=borde del círculo

                // Máscara circular con borde suave
                float circleMask = 1.0 - smoothstep(0.95, 1.05, dist);
                clip(circleMask - 0.001); // descarte temprano fuera del círculo

                // ── Relleno interior ─────────────────────────────────────────
                // Gradiente lineal: pleno en el centro, cero en el borde
                float fill = saturate(1.0 - dist) * circleMask * _FillAlpha;

                // ── Anillo exterior brillante ────────────────────────────────
                // Banda entre 80-96 % del radio, con bordes suavizados
                float ring = smoothstep(0.80, 0.84, dist)
                           * (1.0 - smoothstep(0.90, 0.96, dist));
                // Fluctuación de energía: leve latido del anillo
                ring *= 0.85 + sin(_Time.y * 4.5) * 0.15;

                // ── Pulso radial animado ─────────────────────────────────────
                // Anillo fino que se expande desde el centro hasta el anillo exterior
                float pulsePhase = frac(_Time.y * _PulseSpeed);
                float pulseR     = pulsePhase * 0.80; // viaja de 0 → 80 % del radio
                float pW         = 0.04;
                float pulse = smoothstep(pulseR - pW, pulseR, dist)
                            * (1.0 - smoothstep(pulseR, pulseR + pW, dist));
                pulse *= circleMask * (1.0 - pulsePhase); // desvanece al expandirse

                // ── Composición ──────────────────────────────────────────────
                float3 col = _Color.rgb    * fill
                           + _RingColor.rgb * (ring + pulse * 0.7);

                float alpha = (_Color.a    * fill
                             + _RingColor.a * ring
                             + 0.5          * pulse) * _GlobalAlpha;

                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
