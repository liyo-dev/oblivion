// UI-CircularSoftMask.shader
//
// Copia del shader integrado "UI/Default" con un unico cambio: el alfa se
// desvanece suavemente cerca del borde del rectangulo (usando la distancia
// al centro en espacio UV) antes de llegar al recorte "duro" del componente
// Mask/RectMask2D que lo contiene.
//
// Por que hace falta: el componente UI.Mask de Unity recorta por stencil,
// que es un test binario por pixel sin antialiasing. En un Canvas
// "Screen Space - Overlay" (como el usado en menus y HUD de este proyecto)
// esa mascara nunca se suaviza por MSAA, porque el Overlay se compone
// directamente sobre el backbuffer fuera del paso multisample de la camara.
// El resultado es el borde "cortado"/escalonado que se ve en el minimapa
// (y que ya se reporto antes en elementos circulares/redondeados del menu).
//
// La solucion: en vez de depender solo del stencil para dibujar el circulo,
// este shader atenua el alfa de la textura a 0 un poco ANTES del borde real
// de la mascara (ver _Radius / _EdgeSoftness). Así el pixel ya es
// transparente cuando llega al test de stencil, y el escalonado del stencil
// queda oculto detras de una zona ya invisible en vez de ser el borde
// visible del circulo.
//
// Uso: asignar el material que use este shader al campo "Material" del
// Image/RawImage que rellena el circulo (p. ej. la RawImage del minimapa).
// Ajustar _Radius/_EdgeSoftness en el Inspector si el circulo no coincide
// exactamente con el sprite de mascara usado (por defecto asume que el
// RectTransform es cuadrado y el circulo visible toca los 4 lados).
Shader "UI/CircularSoftMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _Radius ("Radio del circulo (UV, 0.5 = borde del rect)", Range(0.0, 0.5)) = 0.49
        _EdgeSoftness ("Suavizado del borde (UV)", Range(0.0, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Radius;
            float _EdgeSoftness;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Distancia del pixel al centro del rect, en espacio UV (0..0.5 desde el centro
                // hasta cada lado). Si el RectTransform es cuadrado y el sprite de mascara es un
                // circulo inscrito en el, esta distancia coincide con el radio visible del circulo.
                float2 centered = IN.texcoord - 0.5;
                float dist = length(centered);
                float edgeFade = 1.0 - smoothstep(_Radius - _EdgeSoftness, _Radius, dist);
                color.a *= edgeFade;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
