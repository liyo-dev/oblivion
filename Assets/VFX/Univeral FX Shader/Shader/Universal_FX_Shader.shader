Shader "VFX/UniversalShader"
{
    Properties
    {
        [Enum(Game3D,0,UI,1,Sprite2D,2)] _ApplicationMode("Application Mode", Float) = 0
        [Enum(Transparent,0,Additive,1,SoftAdditive,2,Blend,3,Opaque,4,Other,5)] _RenderingMode("Rendering Mode", Float) = 3
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode("Z Test Mode", Float) = 4
        
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.1
        _GlobalAlpha("Global Alpha", Range(0.0, 1.0)) = 1.0
        _AlphaFactor("Alpha Factor", Float) = 1.0

        _MainTex("Main Texture", 2D) = "white" {}
        _MainTexSpeedX("Main Texture Speed X", Float) = 0.0
        _MainTexSpeedY("Main Texture Speed Y", Float) = 0.0

        [HDR]_TintColor("Tint Color", Color) = (0.5,0.5,0.5,1)
        _MainTexBrightness("Main Texture Brightness", Range(0, 4)) = 1.0
        _MainTexContrast("Main Texture Contrast", Range(0, 2)) = 1.0

        // Dual Colors
        _DualColorsState("Dual Colors State", Float) = 0.0
        [HDR]_ColorA("Color A", Color) = (1,0,0,1)
        [HDR]_ColorB("Color B", Color) = (0,0,1,1)
        _ColorThreshold("Color Threshold", Range(0, 1)) = 0.5
        _ColorSmoothness("Color Smoothness", Range(0, 1)) = 0.1

        // Mask
        _MaskTex("Mask Texture", 2D) = "white" {}
        _MaskRotation("Mask Rotation", Range(0, 360)) = 0.0
        _MaskFlowTex("Mask Flow Texture", 2D) = "white" {}
        _MaskFlowSpeed("Mask Flow Speed", Vector) = (0, 0, 0, 0)
        _MaskFlowStrength("Mask Flow Strength", Range(0, 10)) = 0.5
        _MaskFlowSmoothness("Mask Flow Smoothness", Range(0.02, 1)) = 0.1

        // Mask Noise
        _MaskNoiseTex("Mask Noise Texture", 2D) = "white" {}
        _MaskNoiseSpeed("Mask Noise Speed", Vector) = (0, 0, 0, 0)
        _MaskNoiseIntensity("Mask Noise Intensity", Range(0, 1)) = 0.25

        // Dissolve
        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0.0, 1.01)) = 0.1
        _DissolveSmoothness("Dissolve Smoothness", Range(0.0, 1.0)) = 0.1
        _DissolveRemapMin("Dissolve Remap Min", Range(0.0, 1.0)) = 0.0
        _DissolveRemapMax("Dissolve Remap Max", Range(0.0, 1.0)) = 1.0
        _DissolveOutlineStep("Dissolve Outline Step", Range(0.0, 3.0)) = 0.1
        [HDR]_DissolveOutlineColor("Dissolve Outline Color", Color) = (1, 1, 1, 1)

        // UV Noise
        _UVNoise("UV Noise", 2D) = "black" {}
        _UVNoiseBias("UV Noise Bias", Range(-1, 1)) = 0.6
        _UVNoiseIntensity("UV Noise Intensity", Range(0, 1)) = 0.5
        _UVNoiseSpeed("UV Noise Speed", Vector) = (0, 0, 0, 0)

        // Distortion Effect
        _DistortionTex("Distortion Texture", 2D) = "gray" {}
        _DistortionIntensity("Distortion Intensity", Float) = 0.5
        _DistortionSpeed("Distortion Speed", Vector) = (0.1,0.1,0,0)

        // Glow
        _GlowTex("Glow Texture", 2D) = "black" {}
        _GlowSpeedX("Glow Speed X", Float) = 0.0
        _GlowSpeedY("Glow Speed Y", Float) = 0.0
        [HDR]_GlowColor("Glow Color", Color) = (1, 1, 1, 1)
        _GlowBlinkMinAlpha("Glow Blink Min Alpha", Range(0.0, 1.0)) = 0.2
        _GlowBlinkMaxAlpha("Glow Blink Max Alpha", Range(0.0, 1.0)) = 1.0
        _GlowBlinkSpeed("Glow Blink Speed", Float) = 1.0

        // Rim Effects
        [HDR]_RimColor("Rim Color", Color) = (1,1,1,1)
        _RimIntensity("Rim Intensity", Range(0, 10)) = 1
        _RimFresnel("Rim Fresnel", Range(0, 5)) = 1
        [HDR]_RimLightColor("Rim Light Color", Color) = (1,1,1,1)
        _RimLightIntensity("Rim Light Intensity", Range(0, 10)) = 1
        _RimLightFresnel("Rim Light Fresnel", Range(0, 5)) = 1
        
        // Vertex Offset
        _OffsetNoiseTex("Offset Noise Texture", 2D) = "white" {}
        _OffsetAmount("Offset Amount", Range(0, 2)) = 0.0
        _OffsetPower("Offset Power", Range(0, 5)) = 1.0
        _ScrollSpeedX("Scroll Speed X", Float) = 0.0
        _ScrollSpeedY("Scroll Speed Y", Float) = 0.0

        // Shine Effect
        _ShineMask("Shine Mask", 2D) = "white" {}
        [HDR] _ShineColor("Shine Color", Color) = (3,3,3,1)
        _ShineIntensity("Shine Intensity", Float) = 1.0
        _ShineWidth("Shine Width", Float) = 1.0
        _ShineRotation("Shine Direction", Vector) = (1,0,0,0)
        _ShineWorldDirection("World Direction", Vector) = (1,0,0,0)
        _ShineWorldSpace("Use World Space", Float) = 0
        _ShineSpeed("Shine Speed", Float) = 1.0
        _ShineSharpnessLeft("Left Edge Sharpness", Float) = 5.0
        _ShineSharpnessRight("Right Edge Sharpness", Float) = 3.0
        _ShineDelay("Shine Delay", Float) = 0.0

        // Additional Settings
        _ColorMask("Color Mask", Float) = 15
        _InvFade("Soft Particles Factor", Range(0.01, 5.0)) = 1.0
        _Alpha("Alpha", Range(0.0, 1.0)) = 1.0
        _GrayscaleAlphaPower("Grayscale Alpha Power", Range(0.1, 5.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Main"
            Tags { "LightMode" = "ForwardBase" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTestMode]
            Cull [_CullMode]
            ColorMask [_ColorMask]
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // Feature Toggles
            #pragma shader_feature UI_MODE
            #pragma shader_feature _ALPHA_TEST
            #pragma shader_feature ALPHA_FROM_GRAYSCALE
            #pragma shader_feature ENABLE_DUAL_COLORS
            #pragma shader_feature ENABLE_MASK
            #pragma shader_feature ENABLE_MASK_FLOW
            #pragma shader_feature ENABLE_MASK_NOISE
            #pragma shader_feature ENABLE_DISSOLVE
            #pragma shader_feature ENABLE_DISSOLVE_VERTEX_COLOR
            #pragma shader_feature ENABLE_DISSOLVE_OUTLINE
            #pragma shader_feature ENABLE_DISSOLVE_REMAP
            #pragma shader_feature ENABLE_UV_NOISE
            #pragma shader_feature ENABLE_PARTICLE_UV_ANIMATION
            #pragma shader_feature ENABLE_GLOW
            #pragma shader_feature ENABLE_GLOW_BLINK
            #pragma shader_feature ENABLE_RIM
            #pragma shader_feature ENABLE_RIM_LIGHT
            #pragma shader_feature ENABLE_VERTEX_OFFSET
            #pragma shader_feature ENABLE_SHINE
            #pragma shader_feature SHINE_WORLD_SPACE
            #pragma shader_feature ENABLE_SOFTPARTICLES
            #pragma shader_feature ENABLE_DISTORTION
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            // Main Texture
            sampler2D _MainTex;
            float _MainTexSpeedX;
            float _MainTexSpeedY;
            half _MainTexBrightness;
            half _MainTexContrast;
            half4 _MainTex_ST;
            half4 _TintColor;

            // Dual Colors
            #ifdef ENABLE_DUAL_COLORS
            half4 _ColorA;
            half4 _ColorB;
            half _ColorThreshold;
            half _ColorSmoothness;
            #endif

            // Vertex Offset
            sampler2D _OffsetNoiseTex;
            float4 _OffsetNoiseTex_ST;
            float _OffsetAmount;
            float _OffsetPower;
            float _ScrollSpeedX;
            float _ScrollSpeedY;

            // Mask System
            sampler2D _MaskTex;
            half4 _MaskTex_ST;
            float _MaskRotation;

            #ifdef ENABLE_MASK_FLOW
            sampler2D _MaskFlowTex;
            half4 _MaskFlowTex_ST;
            half4 _MaskFlowSpeed;
            half _MaskFlowStrength;
            half _MaskFlowSmoothness;
            #endif

            #ifdef ENABLE_MASK_NOISE
            sampler2D _MaskNoiseTex;
            float4 _MaskNoiseTex_ST;
            float4 _MaskNoiseSpeed;
            float _MaskNoiseIntensity;
            #endif

            // Dissolve
            sampler2D _DissolveTex;
            half4 _DissolveTex_ST;
            fixed _DissolveAmount;
            half _DissolveOutlineStep;
            half3 _DissolveOutlineColor;
            half _DissolveSmoothness;

            #ifdef ENABLE_DISSOLVE_REMAP
            half _DissolveRemapMin;
            half _DissolveRemapMax;
            #endif
            
            // Distortion Effect
            #ifdef ENABLE_DISTORTION
            sampler2D _DistortionTex;
            float4 _DistortionTex_ST;
            float _DistortionIntensity;
            float2 _DistortionSpeed;
            #endif
            
            // Shine Effect
            sampler2D _ShineMask;
            float4 _ShineMask_ST;
            #ifdef ENABLE_SHINE
            fixed4 _ShineColor;
            half _ShineIntensity;
            half _ShineWidth;
            float4 _ShineRotation;
            float4 _ShineWorldDirection;
            half _ShineSpeed;
            half _ShineWorldSpace;
            half _ShineSharpnessLeft;
            half _ShineSharpnessRight;
            half _ShineDelay;
            #endif

            // UV Noise
            sampler2D _UVNoise;
            float4 _UVNoise_ST;
            half _UVNoiseBias;
            half _UVNoiseIntensity;
            half4 _UVNoiseSpeed;

            // Glow
            sampler2D _GlowTex;
            half4 _GlowTex_ST;
            float _GlowSpeedX;
            float _GlowSpeedY;
            fixed4 _GlowColor;

            #ifdef ENABLE_GLOW_BLINK
            half _GlowBlinkMinAlpha;
            half _GlowBlinkMaxAlpha;
            half _GlowBlinkSpeed;
            #endif

            // Rim Effects
            fixed4 _RimColor;
            fixed _RimFresnel;
            fixed _RimIntensity;
            fixed4 _RimLightColor;
            fixed _RimLightIntensity;
            fixed _RimLightFresnel;

            // Alpha & Depth
            half _AlphaFactor;
            fixed _GlobalAlpha;
            fixed _Alpha;
            float _InvFade;
            float _GrayscaleAlphaPower;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                half3 normal : NORMAL;
                half4 uv : TEXCOORD0;
                fixed4 color : COLOR0;
                float4 custom1 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 uvData : TEXCOORD0;        // xy: mainUV, zw: UV noise
                half4 effectsUV : TEXCOORD1;     // xy: maskUV, zw: dissolveUV  
                half4 packedData : TEXCOORD2;    // x: glowU, y: glowV, z: flowU, w: flowV
                half3 worldNormal : TEXCOORD3;
                half4 color : COLOR0;
                float4 projPos : TEXCOORD4;
                float4 customData : TEXCOORD5;   // xy: custom, zw: maskNoiseUV
                half2 shineUV : TEXCOORD6;
                float3 worldPos : TEXCOORD7;
                half2 distortionUV : TEXCOORD8;
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Vertex Offset Effect
                #ifdef ENABLE_VERTEX_OFFSET
                float2 noiseUV = v.uv.xy * _OffsetNoiseTex_ST.xy + _OffsetNoiseTex_ST.zw;
                noiseUV += _Time.y * float2(_ScrollSpeedX, _ScrollSpeedY);
                float noiseSample = tex2Dlod(_OffsetNoiseTex, float4(noiseUV, 0, 0)).r;
                noiseSample = pow(noiseSample, _OffsetPower);
                float offset = noiseSample * _OffsetAmount;
                #ifdef UI_MODE
                offset *= 0.1;
                #endif
                v.vertex.xyz += v.normal * offset;
                #endif
                
                o.pos = UnityObjectToClipPos(v.vertex);

                // World position for rim calculations
                #if !defined(UI_MODE) && (defined(ENABLE_RIM) || defined(ENABLE_RIM_LIGHT))
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                #else
                o.worldPos = float3(0, 0, 0);
                #endif

                o.color = v.color;
                o.projPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);

                // Main UV processing
                half2 mainUV = v.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                #ifdef ENABLE_PARTICLE_UV_ANIMATION
                mainUV += v.uv.zw;
                #endif
                mainUV += frac(_Time.y * half2(_MainTexSpeedX, _MainTexSpeedY));

                // Pack UV data
                o.uvData.xy = mainUV;
                #ifdef ENABLE_UV_NOISE
                o.uvData.zw = v.uv.xy * _UVNoise_ST.xy + _UVNoise_ST.zw;
                #else
                o.uvData.zw = 0;
                #endif

                // Effects UV
                o.effectsUV.xy = v.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
                o.effectsUV.zw = v.uv.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;

                // Packed data
                o.packedData.xy = v.uv.xy * _GlowTex_ST.xy + _GlowTex_ST.zw;

                // World normal
                #if !defined(UI_MODE) && (defined(ENABLE_RIM) || defined(ENABLE_RIM_LIGHT))
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                #else
                o.worldNormal = half3(0, 0, 0);
                #endif

                o.customData = float4(v.uv.zw, 0, 0);
                
                // Shine UV
                o.shineUV = v.uv.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
                
                // Proper UV for flow and noise with tiling/offset
                #ifdef ENABLE_MASK_FLOW
                o.packedData.zw = v.uv.xy * _MaskFlowTex_ST.xy + _MaskFlowTex_ST.zw;
                #else
                o.packedData.zw = 0;
                #endif
                
                #ifdef ENABLE_MASK_NOISE
                o.customData.zw = v.uv.xy * _MaskNoiseTex_ST.xy + _MaskNoiseTex_ST.zw;
                #else
                o.customData.zw = 0;
                #endif

                #ifdef ENABLE_DISTORTION
                o.distortionUV = v.uv.xy * _DistortionTex_ST.xy + _DistortionTex_ST.zw;
                #else
                o.distortionUV = 0;
                #endif
                return o;
            }

                half4 frag(v2f i) : SV_Target
                {
                   // Early exit for completely transparent pixels
                    #if defined(_ALPHA_TEST)
                    half earlyAlpha = i.color.a * _TintColor.a;
                    #ifndef ALPHA_FROM_GRAYSCALE
                    if (earlyAlpha < _Cutoff * 0.3) discard;
                    #endif
                    #endif


                // Sample Main Texture
                #ifdef ENABLE_UV_NOISE
                fixed2 uvNoise = i.uvData.zw + _Time.y * _UVNoiseSpeed.zw;
                fixed2 uvDistort = _UVNoiseBias + tex2D(_UVNoise, uvNoise).rg;
                uvDistort *= _UVNoiseIntensity;
                half4 col = tex2D(_MainTex, i.uvData.xy + uvDistort);
                #else
                half4 col = tex2D(_MainTex, i.uvData.xy);
                #endif



                half4 originalColor = col;

                // Common luminance processing
                half luminance = dot(originalColor.rgb, half3(0.299, 0.587, 0.114));
                half processedLuminance = (luminance - 0.5h) * _MainTexContrast + 0.5h;
                processedLuminance *= _MainTexBrightness;
                processedLuminance = saturate(processedLuminance);

                // Dual Colors or Standard
                #ifdef ENABLE_DUAL_COLORS
                // Dual colors - no tint color
                half blendFactor = smoothstep(
                    _ColorThreshold - _ColorSmoothness, 
                    _ColorThreshold + _ColorSmoothness, 
                    processedLuminance
                );
                col.rgb = lerp(_ColorA.rgb, _ColorB.rgb, blendFactor);
                col.rgb *= i.color.rgb; // Only vertex color
                #else
                // Standard - with tint color  
                col.rgb = originalColor.rgb;
                col.rgb = (col.rgb - 0.5h) * _MainTexContrast + 0.5h;
                col.rgb *= _MainTexBrightness;
                col.rgb = saturate(col.rgb);
                col.rgb *= i.color.rgb * _TintColor.rgb; // Vertex + tint color
                #endif

                // Common alpha processing
                col.a = originalColor.a * i.color.a * _TintColor.a * _AlphaFactor;

                // Alpha From Grayscale
                #ifdef ALPHA_FROM_GRAYSCALE
                half gray = dot(originalColor.rgb, half3(0.299, 0.587, 0.114));
                gray = saturate(pow(gray, _GrayscaleAlphaPower));
                gray = smoothstep(0.05, 1.0, gray);
                col.a *= gray;
                #endif

                #if defined(_ALPHA_TEST)
                if (col.a < _Cutoff) discard;
                #endif


                // Mask System
                #ifdef ENABLE_MASK
                half2 maskUV = i.effectsUV.xy;

                // PRE-CALCULATE rotation once
                float rotRad = _MaskRotation * 0.0174532924;
                float cosRot = cos(rotRad);
                float sinRot = sin(rotRad);

                // Mask noise - fixed version with proper tiling
                #ifdef ENABLE_MASK_NOISE
                // Use pre-computed UV with tiling/offset
                half2 nUV = i.customData.zw;
                nUV += _Time.y * _MaskNoiseSpeed.xy;
                half noise = tex2D(_MaskNoiseTex, nUV).r - 0.5h;
                float2 centered = maskUV - 0.5;
                centered += normalize(centered) * noise * _MaskNoiseIntensity * 0.25h;
                maskUV = centered + 0.5;
                #endif

                #ifdef ENABLE_DISTORTION
                half2 distortionUV = i.distortionUV + _Time.y * _DistortionSpeed;
                half4 distortionSample = tex2D(_DistortionTex, distortionUV);

                half distortionValue = (distortionSample.r - distortionSample.g) * _DistortionIntensity;

                float2 centeredUV = maskUV - 0.5;
                float distanceFromCenter = length(centeredUV);

                centeredUV += normalize(centeredUV) * distortionValue * distanceFromCenter * 0.15;
                maskUV = centeredUV + 0.5;
                #endif

                // Apply rotation
                maskUV = float2(
                    cosRot * (maskUV.x - 0.5) - sinRot * (maskUV.y - 0.5) + 0.5,
                    sinRot * (maskUV.x - 0.5) + cosRot * (maskUV.y - 0.5) + 0.5
                );

                // KEEP luminance calculation for same visual
                half baseMask = dot(tex2D(_MaskTex, maskUV).rgb, half3(0.299, 0.587, 0.114));

                // Mask flow - fixed version with proper tiling
                #ifdef ENABLE_MASK_FLOW
                // Use pre-computed UV with tiling/offset
                half2 flowUV = i.packedData.zw;
                flowUV += _Time.y * _MaskFlowSpeed.xy;

                // Apply same rotation to flow
                flowUV = float2(
                    cosRot * (flowUV.x - 0.5) - sinRot * (flowUV.y - 0.5) + 0.5,
                    sinRot * (flowUV.x - 0.5) + cosRot * (flowUV.y - 0.5) + 0.5
                );

                half flowMask = dot(tex2D(_MaskFlowTex, flowUV).rgb, half3(0.299, 0.587, 0.114));
                half m = baseMask - flowMask * _MaskFlowStrength;
                half s = max(_MaskFlowSmoothness, 0.0001h);
                half soft = smoothstep(0.0h, s, m);
                col.a *= soft;
                #else
                col.a *= baseMask;
                #endif
                #endif

                // Dissolve Effect
                #ifdef ENABLE_DISSOLVE
                half2 dissUV = i.effectsUV.zw;
                half4 dv = tex2D(_DissolveTex, dissUV);

                // Use alpha or luminance with single branch
                half L = dv.a;
                if (dv.a > 0.999h) {
                    L = dot(dv.rgb, half3(0.299, 0.587, 0.114));
                }

                // Dissolve remap
                #ifdef ENABLE_DISSOLVE_REMAP
                L = (L - _DissolveRemapMin) / (_DissolveRemapMax - _DissolveRemapMin);
                L = saturate(L);
                #endif

                // Dissolve amount
                half T = _DissolveAmount;
                #ifdef ENABLE_DISSOLVE_VERTEX_COLOR
                T = saturate(i.customData.x);
                #endif

                // Common dissolve calculations
                half smoothWidth = _DissolveSmoothness * 0.5h;
                half dissolveMask = smoothstep(T - smoothWidth, T + smoothWidth, L);
                col.a *= dissolveMask;

                // Dissolve outline
                #ifdef ENABLE_DISSOLVE_OUTLINE
                half outlineWidth = _DissolveOutlineStep * 0.1h;
                half outlineStart = T - outlineWidth;
                half outlineEnd = T + outlineWidth;

                half outlineMask = smoothstep(outlineStart, T, L) - smoothstep(T, outlineEnd, L);
                col.rgb = lerp(col.rgb, _DissolveOutlineColor.rgb, outlineMask);
                #endif
                #endif

                // Glow Effect
                #ifdef ENABLE_GLOW
                half2 glowUV = i.packedData.xy;
                if (_GlowSpeedX != 0.0 || _GlowSpeedY != 0.0)
                {
                    glowUV += half2(_GlowSpeedX, _GlowSpeedY) * _Time.y;
                }

                half3 glow = tex2D(_GlowTex, glowUV).rgb * _GlowColor.rgb;
                half glowAlpha = _GlowColor.a;

                #ifdef ENABLE_GLOW_BLINK
                half blinkFactor = (_GlowBlinkMaxAlpha - _GlowBlinkMinAlpha) * 0.5;
                half blinkBase = (_GlowBlinkMaxAlpha + _GlowBlinkMinAlpha) * 0.5;
                glowAlpha = blinkBase + blinkFactor * sin(_Time.y * _GlowBlinkSpeed * 6.28318);
                glowAlpha = saturate(glowAlpha);
                #endif

                col.rgb += col.a * glow * glowAlpha;
                #endif

                // Rim Effects
                #if (ENABLE_RIM || ENABLE_RIM_LIGHT) && !defined(UI_MODE)
                half3 worldNormal = normalize(i.worldNormal);
                half3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                half rimDot = dot(viewDir, worldNormal);

                // Rim Effect
                #ifdef ENABLE_RIM
                half rim = pow(1.0 - saturate(abs(rimDot)), _RimFresnel);
                col.rgb = lerp(col.rgb, _RimColor.rgb * _RimIntensity, rim * col.a);
                #endif

                // Rim Light Effect
                #ifdef ENABLE_RIM_LIGHT
                half fresnel = pow(1.0 - saturate(abs(rimDot)), _RimLightFresnel);
                half3 rimLightCol = _RimLightColor.rgb * _RimLightIntensity * fresnel;
                rimLightCol *= col.a;
                col.rgb += rimLightCol;
                col.rgb = saturate(col.rgb);
                #endif
                #endif

                // Soft Particles disabled for UI
                #if !defined(UI_MODE)
                #ifdef ENABLE_SOFTPARTICLES
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.projPos.xy / i.projPos.w));
                float fade = saturate((sceneZ - i.projPos.z) * _InvFade);
                col.a *= fade;
                #endif
                #endif

                // Shine Effect
                #ifdef ENABLE_SHINE
                // Pre-calculate common values
                float totalCycle = 3.0 + _ShineDelay;
                float rawTime = fmod(_Time.y * _ShineSpeed * 0.5, totalCycle);
                float movingPoint = rawTime - 1.5;
                float isActivePhase = rawTime < 3.0 ? 1.0 : 0.0;

                // UV-based projection (90% use case)
                float2 rayDirection = normalize(_ShineRotation.xy);
                float projection = dot(i.shineUV - 0.5, rayDirection) * 2.0;

                // World space projection (less common)
                #ifdef SHINE_WORLD_SPACE
                #ifndef UI_MODE
                float2 screenPos = i.projPos.xy / i.projPos.w;
                rayDirection = normalize(_ShineWorldDirection.xz);
                projection = dot(screenPos - 0.5, rayDirection) * 2.0;
                #endif
                #endif

                // Single branch for edge sharpness
                float distanceToPoint = projection - movingPoint;
                float edgeSharpness = lerp(_ShineSharpnessRight, _ShineSharpnessLeft, distanceToPoint > 0.0);
                float normalizedWidth = _ShineWidth * 0.08;

                float shine = 1.0 - saturate(abs(distanceToPoint) / normalizedWidth);
                shine = pow(shine, edgeSharpness) * isActivePhase;

                // Apply shine with mask
                float mask = tex2D(_ShineMask, i.shineUV).r;
                float shineValue = shine * mask * i.color.a * _ShineColor.a;
                float shineBrightness = lerp(1.5, 4.0, _ShineIntensity);

                col.rgb += _ShineColor.rgb * shineValue * shineBrightness;
                #endif

                // Final Alpha
                col.a *= _Alpha;
                col.a *= _GlobalAlpha;
                col.rgb *= col.a;
                
                return col;
            }
            ENDCG
        }
    }

    CustomEditor "GameParticles.Editor.GameParticleShaderGUI"
}