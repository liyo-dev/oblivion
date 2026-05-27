Shader "VFX/UniversalShader-URP"
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

        _DualColorsState("Dual Colors State", Float) = 0.0
        [HDR]_ColorA("Color A", Color) = (1,0,0,1)
        [HDR]_ColorB("Color B", Color) = (0,0,1,1)
        _ColorThreshold("Color Threshold", Range(0, 1)) = 0.5
        _ColorSmoothness("Color Smoothness", Range(0, 1)) = 0.1

        _MaskTex("Mask Texture", 2D) = "white" {}
        _MaskRotation("Mask Rotation", Range(0, 360)) = 0.0
        _MaskFlowTex("Mask Flow Texture", 2D) = "white" {}
        _MaskFlowSpeed("Mask Flow Speed", Vector) = (0, 0, 0, 0)
        _MaskFlowStrength("Mask Flow Strength", Range(0, 10)) = 0.5
        _MaskFlowSmoothness("Mask Flow Smoothness", Range(0.02, 1)) = 0.1

        _MaskNoiseTex("Mask Noise Texture", 2D) = "white" {}
        _MaskNoiseSpeed("Mask Noise Speed", Vector) = (0, 0, 0, 0)
        _MaskNoiseIntensity("Mask Noise Intensity", Range(0, 1)) = 0.25

        _DissolveTex("Dissolve Texture", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0.0, 1.01)) = 0.1
        _DissolveSmoothness("Dissolve Smoothness", Range(0.0, 1.0)) = 0.1
        _DissolveRemapMin("Dissolve Remap Min", Range(0.0, 1.0)) = 0.0
        _DissolveRemapMax("Dissolve Remap Max", Range(0.0, 1.0)) = 1.0
        _DissolveOutlineStep("Dissolve Outline Step", Range(0.0, 3.0)) = 0.1
        [HDR]_DissolveOutlineColor("Dissolve Outline Color", Color) = (1, 1, 1, 1)

        _UVNoise("UV Noise", 2D) = "black" {}
        _UVNoiseBias("UV Noise Bias", Range(-1, 1)) = 0.6
        _UVNoiseIntensity("UV Noise Intensity", Range(0, 1)) = 0.5
        _UVNoiseSpeed("UV Noise Speed", Vector) = (0, 0, 0, 0)

        _DistortionTex("Distortion Texture", 2D) = "gray" {}
        _DistortionIntensity("Distortion Intensity", Float) = 0.5
        _DistortionSpeed("Distortion Speed", Vector) = (0.1,0.1,0,0)

        _GlowTex("Glow Texture", 2D) = "black" {}
        _GlowSpeedX("Glow Speed X", Float) = 0.0
        _GlowSpeedY("Glow Speed Y", Float) = 0.0
        [HDR]_GlowColor("Glow Color", Color) = (1, 1, 1, 1)
        _GlowBlinkMinAlpha("Glow Blink Min Alpha", Range(0.0, 1.0)) = 0.2
        _GlowBlinkMaxAlpha("Glow Blink Max Alpha", Range(0.0, 1.0)) = 1.0
        _GlowBlinkSpeed("Glow Blink Speed", Float) = 1.0

        [HDR]_RimColor("Rim Color", Color) = (1,1,1,1)
        _RimIntensity("Rim Intensity", Range(0, 10)) = 1
        _RimFresnel("Rim Fresnel", Range(0, 5)) = 1
        [HDR]_RimLightColor("Rim Light Color", Color) = (1,1,1,1)
        _RimLightIntensity("Rim Light Intensity", Range(0, 10)) = 1
        _RimLightFresnel("Rim Light Fresnel", Range(0, 5)) = 1
        
        _OffsetNoiseTex("Offset Noise Texture", 2D) = "white" {}
        _OffsetAmount("Offset Amount", Range(0, 2)) = 0.0
        _OffsetPower("Offset Power", Range(0, 5)) = 1.0
        _ScrollSpeedX("Scroll Speed X", Float) = 0.0
        _ScrollSpeedY("Scroll Speed Y", Float) = 0.0

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
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Main"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTestMode]
            Cull [_CullMode]
            ColorMask [_ColorMask]
            Lighting Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma shader_feature _ _ALPHA_TEST
            #pragma shader_feature _ ALPHA_FROM_GRAYSCALE
            #pragma shader_feature _ ENABLE_DUAL_COLORS
            #pragma shader_feature _ ENABLE_MASK
            #pragma shader_feature _ ENABLE_MASK_FLOW
            #pragma shader_feature _ ENABLE_MASK_NOISE
            #pragma shader_feature _ ENABLE_DISSOLVE
            #pragma shader_feature _ ENABLE_DISSOLVE_VERTEX_COLOR
            #pragma shader_feature _ ENABLE_DISSOLVE_OUTLINE
            #pragma shader_feature _ ENABLE_DISSOLVE_REMAP
            #pragma shader_feature _ ENABLE_UV_NOISE
            #pragma shader_feature _ ENABLE_PARTICLE_UV_ANIMATION
            #pragma shader_feature _ ENABLE_GLOW
            #pragma shader_feature _ ENABLE_GLOW_BLINK
            #pragma shader_feature _ ENABLE_RIM
            #pragma shader_feature _ ENABLE_RIM_LIGHT
            #pragma shader_feature _ ENABLE_VERTEX_OFFSET
            #pragma shader_feature _ ENABLE_SHINE
            #pragma shader_feature _ SHINE_WORLD_SPACE
            #pragma shader_feature _ ENABLE_SOFTPARTICLES
            #pragma shader_feature _ ENABLE_DISTORTION
            #pragma shader_feature _ UI_MODE

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _MainTexSpeedX;
            float _MainTexSpeedY;
            half _MainTexBrightness;
            half _MainTexContrast;
            half4 _MainTex_ST;
            half4 _TintColor;

            #ifdef ENABLE_DUAL_COLORS
            half4 _ColorA;
            half4 _ColorB;
            half _ColorThreshold;
            half _ColorSmoothness;
            #endif

            TEXTURE2D(_OffsetNoiseTex);
            SAMPLER(sampler_OffsetNoiseTex);
            float4 _OffsetNoiseTex_ST;
            float _OffsetAmount;
            float _OffsetPower;
            float _ScrollSpeedX;
            float _ScrollSpeedY;

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            half4 _MaskTex_ST;
            float _MaskRotation;

            #ifdef ENABLE_MASK_FLOW
            TEXTURE2D(_MaskFlowTex);
            SAMPLER(sampler_MaskFlowTex);
            half4 _MaskFlowTex_ST;
            half4 _MaskFlowSpeed;
            half _MaskFlowStrength;
            half _MaskFlowSmoothness;
            #endif

            #ifdef ENABLE_MASK_NOISE
            TEXTURE2D(_MaskNoiseTex);
            SAMPLER(sampler_MaskNoiseTex);
            float4 _MaskNoiseTex_ST;
            float4 _MaskNoiseSpeed;
            float _MaskNoiseIntensity;
            #endif

            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);
            half4 _DissolveTex_ST;
            half _DissolveAmount;
            half _DissolveOutlineStep;
            half3 _DissolveOutlineColor;
            half _DissolveSmoothness;

            #ifdef ENABLE_DISSOLVE_REMAP
            half _DissolveRemapMin;
            half _DissolveRemapMax;
            #endif
            
            #ifdef ENABLE_DISTORTION
            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);
            float4 _DistortionTex_ST;
            float _DistortionIntensity;
            float2 _DistortionSpeed;
            #endif
            
            TEXTURE2D(_ShineMask);
            SAMPLER(sampler_ShineMask);
            float4 _ShineMask_ST;
            #ifdef ENABLE_SHINE
            half4 _ShineColor;
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

            TEXTURE2D(_UVNoise);
            SAMPLER(sampler_UVNoise);
            float4 _UVNoise_ST;
            half _UVNoiseBias;
            half _UVNoiseIntensity;
            half4 _UVNoiseSpeed;

            TEXTURE2D(_GlowTex);
            SAMPLER(sampler_GlowTex);
            half4 _GlowTex_ST;
            float _GlowSpeedX;
            float _GlowSpeedY;
            half4 _GlowColor;

            #ifdef ENABLE_GLOW_BLINK
            half _GlowBlinkMinAlpha;
            half _GlowBlinkMaxAlpha;
            half _GlowBlinkSpeed;
            #endif

            half4 _RimColor;
            half _RimFresnel;
            half _RimIntensity;
            half4 _RimLightColor;
            half _RimLightIntensity;
            half _RimLightFresnel;

            half _AlphaFactor;
            half _GlobalAlpha;
            half _Alpha;
            float _InvFade;
            float _SoftParticlesNearFadeDistance;
            float _SoftParticlesFarFadeDistance;
            float _GrayscaleAlphaPower;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                half3 normal : NORMAL;
                half4 uv : TEXCOORD0;
                half4 color : COLOR0;
                float4 custom1 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 uvData : TEXCOORD0;
                half4 effectsUV : TEXCOORD1;
                half4 packedData : TEXCOORD2;
                half3 worldNormal : TEXCOORD3;
                half4 color : COLOR0;
                float4 projPos : TEXCOORD4;
                float4 customData : TEXCOORD5;
                half2 shineUV : TEXCOORD6;
                float3 worldPos : TEXCOORD7;
                half2 distortionUV : TEXCOORD8;
            };

            v2f vert(appdata v)
            {
                v2f o;
                
                #ifdef ENABLE_VERTEX_OFFSET
                float2 noiseUV = v.uv.xy * _OffsetNoiseTex_ST.xy + _OffsetNoiseTex_ST.zw;
                noiseUV += _Time.y * float2(_ScrollSpeedX, _ScrollSpeedY);
                float noiseSample = SAMPLE_TEXTURE2D_LOD(_OffsetNoiseTex, sampler_OffsetNoiseTex, noiseUV, 0).r;
                noiseSample = pow(noiseSample, _OffsetPower);
                float offset = noiseSample * _OffsetAmount;
                #ifdef UI_MODE
                offset *= 0.1;
                #endif
                v.vertex.xyz += v.normal * offset;
                #endif
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                o.pos = vertexInput.positionCS;

                #if !defined(UI_MODE) && (defined(ENABLE_RIM) || defined(ENABLE_RIM_LIGHT))
                o.worldPos = vertexInput.positionWS;
                #else
                o.worldPos = float3(0, 0, 0);
                #endif

                o.color = v.color;
                o.projPos = ComputeScreenPos(o.pos);

                half2 mainUV = v.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                #ifdef ENABLE_PARTICLE_UV_ANIMATION
                mainUV += v.uv.zw;
                #endif
                mainUV += frac(_Time.y * half2(_MainTexSpeedX, _MainTexSpeedY));

                o.uvData.xy = mainUV;
                #ifdef ENABLE_UV_NOISE
                o.uvData.zw = v.uv.xy * _UVNoise_ST.xy + _UVNoise_ST.zw;
                #else
                o.uvData.zw = 0;
                #endif

                o.effectsUV.xy = v.uv.xy * _MaskTex_ST.xy + _MaskTex_ST.zw;
                o.effectsUV.zw = v.uv.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;

                o.packedData.xy = v.uv.xy * _GlowTex_ST.xy + _GlowTex_ST.zw;

                #if !defined(UI_MODE) && (defined(ENABLE_RIM) || defined(ENABLE_RIM_LIGHT))
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normal);
                o.worldNormal = normalInput.normalWS;
                #else
                o.worldNormal = half3(0, 0, 0);
                #endif

                o.customData = float4(v.uv.zw, 0, 0);
                
                o.shineUV = v.uv.xy * _ShineMask_ST.xy + _ShineMask_ST.zw;
                
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
                #if defined(_ALPHA_TEST)
                half earlyAlpha = i.color.a * _TintColor.a;
                #ifndef ALPHA_FROM_GRAYSCALE
                if (earlyAlpha < _Cutoff * 0.3) discard;
                #endif
                #endif

                #ifdef ENABLE_UV_NOISE
                half2 uvNoise = i.uvData.zw + _Time.y * _UVNoiseSpeed.zw;
                half2 uvDistort = _UVNoiseBias + SAMPLE_TEXTURE2D(_UVNoise, sampler_UVNoise, uvNoise).rg;
                uvDistort *= _UVNoiseIntensity;
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uvData.xy + uvDistort);
                #else
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uvData.xy);
                #endif

                half4 originalColor = col;

                half luminance = dot(originalColor.rgb, half3(0.299, 0.587, 0.114));
                half processedLuminance = (luminance - 0.5h) * _MainTexContrast + 0.5h;
                processedLuminance *= _MainTexBrightness;
                processedLuminance = saturate(processedLuminance);

                #ifdef ENABLE_DUAL_COLORS
                half blendFactor = smoothstep(
                    _ColorThreshold - _ColorSmoothness, 
                    _ColorThreshold + _ColorSmoothness, 
                    processedLuminance
                );
                col.rgb = lerp(_ColorA.rgb, _ColorB.rgb, blendFactor);
                col.rgb *= i.color.rgb;
                #else
                col.rgb = originalColor.rgb;
                col.rgb = (col.rgb - 0.5h) * _MainTexContrast + 0.5h;
                col.rgb *= _MainTexBrightness;
                col.rgb = saturate(col.rgb);
                col.rgb *= i.color.rgb * _TintColor.rgb;
                #endif

                col.a = originalColor.a * i.color.a * _TintColor.a * _AlphaFactor;

                #ifdef ALPHA_FROM_GRAYSCALE
                half gray = dot(originalColor.rgb, half3(0.299, 0.587, 0.114));
                gray = saturate(pow(gray, _GrayscaleAlphaPower));
                gray = smoothstep(0.05, 1.0, gray);
                col.a *= gray;
                #endif

                #if defined(_ALPHA_TEST)
                if (col.a < _Cutoff) discard;
                #endif

                #ifdef ENABLE_MASK
                half2 maskUV = i.effectsUV.xy;

                float rotRad = _MaskRotation * 0.0174532924;
                float cosRot = cos(rotRad);
                float sinRot = sin(rotRad);

                #ifdef ENABLE_MASK_NOISE
                half2 nUV = i.customData.zw;
                nUV += _Time.y * _MaskNoiseSpeed.xy;
                half noise = SAMPLE_TEXTURE2D(_MaskNoiseTex, sampler_MaskNoiseTex, nUV).r - 0.5h;
                float2 centered = maskUV - 0.5;
                centered += normalize(centered) * noise * _MaskNoiseIntensity * 0.25h;
                maskUV = centered + 0.5;
                #endif

                #ifdef ENABLE_DISTORTION
                half2 distortionUV = i.distortionUV + _Time.y * _DistortionSpeed;
                half4 distortionSample = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUV);

                half distortionValue = (distortionSample.r - distortionSample.g) * _DistortionIntensity;

                float2 centeredUV = maskUV - 0.5;
                float distanceFromCenter = length(centeredUV);

                centeredUV += normalize(centeredUV) * distortionValue * distanceFromCenter * 0.15;
                maskUV = centeredUV + 0.5;
                #endif

                maskUV = float2(
                    cosRot * (maskUV.x - 0.5) - sinRot * (maskUV.y - 0.5) + 0.5,
                    sinRot * (maskUV.x - 0.5) + cosRot * (maskUV.y - 0.5) + 0.5
                );

                half baseMask = dot(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).rgb, half3(0.299, 0.587, 0.114));

                #ifdef ENABLE_MASK_FLOW
                half2 flowUV = i.packedData.zw;
                flowUV += _Time.y * _MaskFlowSpeed.xy;

                flowUV = float2(
                    cosRot * (flowUV.x - 0.5) - sinRot * (flowUV.y - 0.5) + 0.5,
                    sinRot * (flowUV.x - 0.5) + cosRot * (flowUV.y - 0.5) + 0.5
                );

                half flowMask = dot(SAMPLE_TEXTURE2D(_MaskFlowTex, sampler_MaskFlowTex, flowUV).rgb, half3(0.299, 0.587, 0.114));
                half m = baseMask - flowMask * _MaskFlowStrength;
                half s = max(_MaskFlowSmoothness, 0.0001h);
                half soft = smoothstep(0.0h, s, m);
                col.a *= soft;
                #else
                col.a *= baseMask;
                #endif
                #endif

                #ifdef ENABLE_DISSOLVE
                half2 dissUV = i.effectsUV.zw;
                half4 dv = SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, dissUV);

                half L = dv.a;
                if (dv.a > 0.999h) {
                    L = dot(dv.rgb, half3(0.299, 0.587, 0.114));
                }

                #ifdef ENABLE_DISSOLVE_REMAP
                L = (L - _DissolveRemapMin) / (_DissolveRemapMax - _DissolveRemapMin);
                L = saturate(L);
                #endif

                half T = _DissolveAmount;
                #ifdef ENABLE_DISSOLVE_VERTEX_COLOR
                T = saturate(i.customData.x);
                #endif

                half smoothWidth = _DissolveSmoothness * 0.5h;
                half dissolveMask = smoothstep(T - smoothWidth, T + smoothWidth, L);
                col.a *= dissolveMask;

                #ifdef ENABLE_DISSOLVE_OUTLINE
                half outlineWidth = _DissolveOutlineStep * 0.1h;
                half outlineStart = T - outlineWidth;
                half outlineEnd = T + outlineWidth;

                half outlineMask = smoothstep(outlineStart, T, L) - smoothstep(T, outlineEnd, L);
                col.rgb = lerp(col.rgb, _DissolveOutlineColor.rgb, outlineMask);
                #endif
                #endif

                #ifdef ENABLE_GLOW
                half2 glowUV = i.packedData.xy;
                if (_GlowSpeedX != 0.0 || _GlowSpeedY != 0.0)
                {
                    glowUV += half2(_GlowSpeedX, _GlowSpeedY) * _Time.y;
                }

                half3 glow = SAMPLE_TEXTURE2D(_GlowTex, sampler_GlowTex, glowUV).rgb * _GlowColor.rgb;
                half glowAlpha = _GlowColor.a;

                #ifdef ENABLE_GLOW_BLINK
                half blinkFactor = (_GlowBlinkMaxAlpha - _GlowBlinkMinAlpha) * 0.5;
                half blinkBase = (_GlowBlinkMaxAlpha + _GlowBlinkMinAlpha) * 0.5;
                glowAlpha = blinkBase + blinkFactor * sin(_Time.y * _GlowBlinkSpeed * 6.28318);
                glowAlpha = saturate(glowAlpha);
                #endif

                col.rgb += col.a * glow * glowAlpha;
                #endif

                #if (ENABLE_RIM || ENABLE_RIM_LIGHT) && !defined(UI_MODE)
                half3 worldNormal = normalize(i.worldNormal);
                half3 viewDir = normalize(GetWorldSpaceViewDir(i.worldPos));
                half rimDot = dot(viewDir, worldNormal);

                #ifdef ENABLE_RIM
                half rim = pow(1.0 - saturate(abs(rimDot)), _RimFresnel);
                col.rgb = lerp(col.rgb, _RimColor.rgb * _RimIntensity, rim * col.a);
                #endif

                #ifdef ENABLE_RIM_LIGHT
                half fresnel = pow(1.0 - saturate(abs(rimDot)), _RimLightFresnel);
                half3 rimLightCol = _RimLightColor.rgb * _RimLightIntensity * fresnel;
                rimLightCol *= col.a;
                col.rgb += rimLightCol;
                col.rgb = saturate(col.rgb);
                #endif
                #endif


                #if !defined(UI_MODE)
                #ifdef ENABLE_SOFTPARTICLES
                    float2 screenUV = i.projPos.xy / i.projPos.w;
                    
                    #ifdef SHADER_API_D3D11
                        // URP path
                        float sceneDepth = SampleSceneDepth(screenUV);
                        float linearSceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                        float linearSurfaceDepth = i.projPos.w;
                        float depthDiff = linearSceneDepth - linearSurfaceDepth;
                        float fade = saturate(depthDiff * _InvFade);
                        col.a *= fade;
                    #else
                        // Built-in path
                        float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                        float partZ = i.projPos.z;
                        float fade = saturate(_InvFade * (sceneZ - partZ));
                        col.a *= fade;
                    #endif
                #endif
                #endif
                
                #ifdef ENABLE_SHINE
                float totalCycle = 3.0 + _ShineDelay;
                float rawTime = fmod(_Time.y * _ShineSpeed * 0.5, totalCycle);
                float movingPoint = rawTime - 1.5;
                float isActivePhase = rawTime < 3.0 ? 1.0 : 0.0;

                float2 rayDirection = normalize(_ShineRotation.xy);
                float projection = dot(i.shineUV - 0.5, rayDirection) * 2.0;

                #ifdef SHINE_WORLD_SPACE
                #ifndef UI_MODE
                float2 screenPos = i.projPos.xy / i.projPos.w;
                rayDirection = normalize(_ShineWorldDirection.xz);
                projection = dot(screenPos - 0.5, rayDirection) * 2.0;
                #endif
                #endif

                float distanceToPoint = projection - movingPoint;
                float edgeSharpness = lerp(_ShineSharpnessRight, _ShineSharpnessLeft, distanceToPoint > 0.0);
                float normalizedWidth = _ShineWidth * 0.08;

                float shine = 1.0 - saturate(abs(distanceToPoint) / normalizedWidth);
                shine = pow(shine, edgeSharpness) * isActivePhase;

                float mask = SAMPLE_TEXTURE2D(_ShineMask, sampler_ShineMask, i.shineUV).r;
                float shineValue = shine * mask * i.color.a * _ShineColor.a;
                float shineBrightness = lerp(1.5, 4.0, _ShineIntensity);

                col.rgb += _ShineColor.rgb * shineValue * shineBrightness;
                #endif

                col.a *= _Alpha;
                col.a *= _GlobalAlpha;
                col.rgb *= col.a;
                
                return col;
            }
            ENDHLSL
        }
    }

    CustomEditor "GameParticles.Editor.GameParticleShaderGUI"
}