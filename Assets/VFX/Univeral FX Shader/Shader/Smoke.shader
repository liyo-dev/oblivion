Shader "VFX/SmokeProcedular"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Frequency ("Frequency", Float) = 0.5
        _Phase ("Phase", Float) = 0.0
        _Amplitude ("Amplitude", Float) = 0.1
 
        _Frequency_2 ("Frequency_2", Float) = 0.5
        _Phase_2 ("Phase_2", Float) = 0.0
        _Amplitude_2 ("Amplitude_2", Float) = 0.1
 
        _Frequency_3 ("Frequency_3", Float) = 0.5
        _Phase_3 ("Phase_3", Float) = 0.0
        _Amplitude_3 ("Amplitude_3", Float) = 0.1
 
        _RotationAngle ("Rotation Angle (Degrees)", Float) = 0.0
        
        // Добавляем свойства для цвета и прозрачности
        _SmokeColor ("Smoke Color", Color) = (1,1,1,1)
        _Transparency ("Transparency", Range(0, 1)) = 1.0
        _ColorIntensity ("Color Intensity", Range(0, 5)) = 1.0
        
        // Параметры для уникальности каждого экземпляра
        _UVOffsetX ("UV Offset X", Range(-1, 1)) = 0.0
        _UVOffsetY ("UV Offset Y", Range(-1, 1)) = 0.0
        _RandomSeed ("Random Seed", Range(0, 100)) = 0.0
    }
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #define pi 3.14159
 
            #include "UnityCG.cginc"
 
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
 
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
 
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Frequency, _Phase, _Amplitude;
            float _Frequency_2, _Phase_2, _Amplitude_2;
			float _Frequency_3, _Phase_3, _Amplitude_3;
            float _RotationAngle;
            float _Transparency;
            float4 _SmokeColor;
            float _ColorIntensity;
            
            // Параметры для уникальности
            float _UVOffsetX;
            float _UVOffsetY;
            float _RandomSeed;
 
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
 
            float2 RotateUV(float2 uv, float angle)
            {
                float rad = angle * (pi / 180.0);
 
                float2 centered = uv - 0.5;
 
                float cosA = cos(rad);
                float sinA = sin(rad);
                float2 rotated;
                rotated.x = centered.x * cosA - centered.y * sinA;
                rotated.y = centered.x * sinA + centered.y * cosA;
 
                return rotated + 0.5;
            }
 
            float CalculateOffset(float freq, float phase, float2 uv)
            {
                // Добавляем случайное семя в расчет
                float x = (uv.y * freq) + phase - _Time.y * 2 + _RandomSeed;
				float a = (frac(x) * 2.0) - 1.0;
				float circle = sqrt(1.0 - (a * a));
				float sign = ((smoothstep(0.5, 0.5, frac(x / 2.0))) * 2.0) - 1.0;
				float circleWave = circle * sign;
				return circleWave;
            }
 
            float4 frag (v2f i) : SV_Target
            {
                // Применяем UV смещение
                float2 uv = i.uv + float2(_UVOffsetX, _UVOffsetY);
                
                uv = RotateUV(uv, _RotationAngle);

                float amplitude_1 = (uv.y) * _Amplitude;
                float uvOffsetX_1 = CalculateOffset(_Frequency, _Phase, uv) * amplitude_1;

                float amplitude_2 = uv.y * _Amplitude_2;
                float uvOffsetX_2 = CalculateOffset(_Frequency_2, _Phase_2, uv) * amplitude_2;

				float amplitude_3 = uv.y * _Amplitude_3;
				float uvOffsetX_3 = CalculateOffset(_Frequency_3, _Phase_3, uv) * amplitude_3;

                float totalOffset = uvOffsetX_1 + uvOffsetX_2 + uvOffsetX_3;

                float4 col = tex2D(_MainTex, float2(i.uv.x + totalOffset, i.uv.y));
                
                // Применяем цвет дыма
                float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
                
                col.rgb = col.rgb * _SmokeColor.rgb * _ColorIntensity;
                col.a = luminance * _SmokeColor.a * _Transparency;

                return col;
            }
            ENDCG
        }
    }
}