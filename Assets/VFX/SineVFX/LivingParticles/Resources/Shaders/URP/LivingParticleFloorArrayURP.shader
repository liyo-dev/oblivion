// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SineVFX/LivingParticles/LivingParticleFloorArrayURP"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		_FinalTexture("Final Texture", 2D) = "white" {}
		_FinalColor("Final Color", Color) = (1,0,0,1)
		_FinalColor2("Final Color 2", Color) = (0,0,0,0)
		_AffectorCount("Affector Count", Float) = 5
		_FinalPower("Final Power", Range( 0 , 10)) = 6
		_FinalExp("Final Exp", Range( 0.2 , 4)) = 2
		_FinalMaskMultiply("Final Mask Multiply", Range( 0 , 10)) = 2
		[Toggle(_RAMPENABLED_ON)] _RampEnabled("Ramp Enabled", Float) = 0
		_Ramp("Ramp", 2D) = "white" {}
		_Distance("Distance", Float) = 1
		_DistancePower("Distance Power", Range( 0.2 , 4)) = 1
		_OffsetPower("Offset Power", Float) = 0
		[Toggle(_IGNOREYAXIS_ON)] _IgnoreYAxis("Ignore Y Axis", Float) = 0
		_Noise01("Noise 01", 2D) = "white" {}
		_Noise02("Noise 02", 2D) = "white" {}
		_Noise01ScrollSpeed("Noise 01 Scroll Speed", Float) = 0.1
		_Noise02ScrollSpeed("Noise 02 Scroll Speed", Float) = 0.15
		_NoiseDistortion("Noise Distortion", 2D) = "white" {}
		_NoiseDistortionScrollSpeed("Noise Distortion Scroll Speed", Float) = 0.05
		_NoiseDistortionPower("Noise Distortion Power", Range( 0 , 0.2)) = 0.1
		_NoisePower("Noise Power", Range( 0 , 10)) = 4
		_NoiseTiling("Noise Tiling", Float) = 0.25
		[Toggle(_ZYUV_ON)] _ZYUV("ZY UV", Float) = 0
		[Toggle(_XYUV_ON)] _XYUV("XY UV", Float) = 0
		[Toggle(_AUDIOSPECTRUMENABLED2_ON)] _AudioSpectrumEnabled2("Audio Spectrum Enabled", Float) = 0
		[HideInInspector]_AudioSpectrum("Audio Spectrum", 2D) = "white" {}
		_AudioSpectrumPower2("Audio Spectrum Power", Range( 0 , 1)) = 1
		_AudioSpectrumDistanceTiling2("Audio Spectrum Distance Tiling", Float) = 4
		[Toggle(_AUDIOMASKENABLED2_ON)] _AudioMaskEnabled2("Audio Mask Enabled", Float) = 0
		_AudioMaskExp2("Audio Mask Exp", Range( 0.1 , 4)) = 1
		_AudioMaskMultiply2("Audio Mask Multiply", Range( 1 , 4)) = 1
		_AudioMaskAffectsAmplitude2("Audio Mask Affects Amplitude", Range( 0 , 1)) = 0
		[Toggle(_AUDIOAMPLITUDEENABLED2_ON)] _AudioAmplitudeEnabled2("Audio Amplitude Enabled", Float) = 0
		_AudioAmplitudeEmissionPower2("Audio Amplitude Emission Power", Range( 0 , 4)) = 1
		_AudioAmplitudeOffsetPower2("Audio Amplitude Offset Power", Range( 0 , 4)) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}


		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

		//[HideInInspector][ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 1
		[HideInInspector][ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }

		Cull Back
		AlphaToMask Off

		

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			#pragma multi_compile_local _ALPHATEST_ON
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile_fragment _ DEBUG_DISPLAY

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS SHADERPASS_UNLIT

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#pragma shader_feature _IGNOREYAXIS_ON
			#pragma shader_feature _ZYUV_ON
			#pragma shader_feature _XYUV_ON
			#pragma shader_feature _AUDIOSPECTRUMENABLED2_ON
			#pragma shader_feature _AUDIOMASKENABLED2_ON
			#pragma shader_feature _AUDIOAMPLITUDEENABLED2_ON
			#pragma shader_feature _RAMPENABLED_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD1;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD2;
				#endif
				#ifdef ASE_FOG
					float fogFactor : TEXCOORD3;
				#endif
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FinalColor;
			float4 _FinalColor2;
			float4 _FinalTexture_ST;
			float _AffectorCount;
			float _FinalPower;
			float _FinalExp;
			float _AudioMaskAffectsAmplitude2;
			float _AudioAmplitudeOffsetPower2;
			float _OffsetPower;
			float _AudioSpectrumPower2;
			float _AudioMaskMultiply2;
			float _AudioMaskExp2;
			float _NoisePower;
			float _Noise01ScrollSpeed;
			float _NoiseDistortionPower;
			float _NoiseDistortionScrollSpeed;
			float _NoiseTiling;
			float _Noise02ScrollSpeed;
			float _FinalMaskMultiply;
			float _DistancePower;
			float _Distance;
			float _AudioSpectrumDistanceTiling2;
			float _AudioAmplitudeEmissionPower2;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			float4 _Affectors[20];
			sampler2D _Noise02;
			sampler2D _NoiseDistortion;
			sampler2D _Noise01;
			float3 _AudioPosition2;
			sampler2D _AudioSpectrum;
			float _AudioAverageAmplitude;
			sampler2D _Ramp;
			sampler2D _FinalTexture;


			float CE1149( float4 ParticleCenterCE, float4 AffectorsCE, float3 Fix )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w] * Fix);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w] * Fix) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float4 appendResult17 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				#ifdef _IGNOREYAXIS_ON
				float3 staticSwitch93 = float3(1,0,1);
				#else
				float3 staticSwitch93 = float3(1,1,1);
				#endif
				float4 ParticleCenterCE149 = ( appendResult17 * float4( staticSwitch93 , 0.0 ) );
				float4 AffectorsCE149 = ( _Affectors[0] * float4( staticSwitch93 , 0.0 ) );
				float3 Fix149 = staticSwitch93;
				float localCE1149 = CE1149( ParticleCenterCE149 , AffectorsCE149 , Fix149 );
				float DistanceMask45 = localCE1149;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				float4 break102 = appendResult17;
				float2 appendResult104 = (float2(break102.x , break102.z));
				float2 appendResult142 = (float2(break102.x , break102.y));
				#ifdef _XYUV_ON
				float2 staticSwitch140 = appendResult142;
				#else
				float2 staticSwitch140 = appendResult104;
				#endif
				float2 appendResult143 = (float2(break102.z , break102.y));
				#ifdef _ZYUV_ON
				float2 staticSwitch141 = appendResult143;
				#else
				float2 staticSwitch141 = staticSwitch140;
				#endif
				float2 ParticlePositionUV106 = staticSwitch141;
				float2 temp_output_119_0 = ( ParticlePositionUV106 * _NoiseTiling );
				float2 panner109 = ( ( ( _TimeParameters.x ) * _Noise02ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 panner131 = ( ( ( _TimeParameters.x ) * _NoiseDistortionScrollSpeed ) * float2( 0.05,0.05 ) + temp_output_119_0);
				float4 tex2DNode132 = tex2Dlod( _NoiseDistortion, float4( panner131, 0, 0.0) );
				float2 temp_cast_2 = (tex2DNode132.r).xx;
				float2 lerpResult127 = lerp( panner109 , temp_cast_2 , _NoiseDistortionPower);
				float2 panner110 = ( ( ( _TimeParameters.x ) * _Noise01ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 temp_cast_3 = (tex2DNode132.r).xx;
				float2 lerpResult128 = lerp( panner110 , temp_cast_3 , _NoiseDistortionPower);
				float ResultNoise111 = ( tex2Dlod( _Noise02, float4( lerpResult127, 0, 0.0) ).r * tex2Dlod( _Noise01, float4( lerpResult128, 0, 0.0) ).r * _NoisePower );
				float4 appendResult174 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				float temp_output_176_0 = distance( appendResult174 , float4( _AudioPosition2 , 0.0 ) );
				float clampResult183 = clamp( (0.0 + (( -temp_output_176_0 + _AudioSpectrumDistanceTiling2 ) - 0.0) * (1.0 - 0.0) / (_AudioSpectrumDistanceTiling2 - 0.0)) , 0.0 , 1.0 );
				float clampResult191 = clamp( ( pow( clampResult183 , _AudioMaskExp2 ) * _AudioMaskMultiply2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOMASKENABLED2_ON
				float staticSwitch193 = clampResult191;
				#else
				float staticSwitch193 = 1.0;
				#endif
				float2 appendResult190 = (float2(( temp_output_176_0 * ( 1.0 / _AudioSpectrumDistanceTiling2 ) ) , 0.0));
				float clampResult197 = clamp( ( staticSwitch193 * tex2Dlod( _AudioSpectrum, float4( appendResult190, 0, 0.0) ).r * _AudioSpectrumPower2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOSPECTRUMENABLED2_ON
				float staticSwitch198 = clampResult197;
				#else
				float staticSwitch198 = 0.0;
				#endif
				float clampResult88 = clamp( ( ( ResultMask53 * _FinalMaskMultiply ) + ResultNoise111 + staticSwitch198 ) , 0.0 , 1.0 );
				float ResultMaskModified90 = clampResult88;
				float3 appendResult138 = (float3(input.ase_texcoord1.y , input.ase_texcoord1.z , input.ase_texcoord1.w));
				float3 normalizeResult139 = normalize( appendResult138 );
				#ifdef _AUDIOAMPLITUDEENABLED2_ON
				float staticSwitch161 = 1.0;
				#else
				float staticSwitch161 = 0.0;
				#endif
				float clampResult163 = clamp( ( ( 1.0 - _AudioMaskAffectsAmplitude2 ) + staticSwitch193 ) , 0.0 , 1.0 );
				
				output.ase_texcoord4 = input.ase_texcoord;
				output.ase_texcoord5 = input.ase_texcoord1;
				output.ase_color = input.ase_color;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = ( ( 1.0 - ResultMaskModified90 ) * _OffsetPower * normalizeResult139 * ( ( _AudioAverageAmplitude * _AudioAmplitudeOffsetPower2 * staticSwitch161 * clampResult163 ) + 1.0 ) );

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = vertexInput.positionWS;
				#endif

				#ifdef ASE_FOG
					output.fogFactor = ComputeFogFactor( vertexInput.positionCS.z );
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;
				output.clipPosV = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.vertex = input.positionOS;
				output.normalOS = input.normalOS;
				output.ase_texcoord = input.ase_texcoord;
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag ( PackedVaryings input
				#ifdef _WRITE_RENDERING_LAYERS
				, out float4 outRenderingLayers : SV_Target1
				#endif
				 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = input.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float4 appendResult17 = (float4(input.ase_texcoord4.z , input.ase_texcoord4.w , input.ase_texcoord5.x , 0.0));
				#ifdef _IGNOREYAXIS_ON
				float3 staticSwitch93 = float3(1,0,1);
				#else
				float3 staticSwitch93 = float3(1,1,1);
				#endif
				float4 ParticleCenterCE149 = ( appendResult17 * float4( staticSwitch93 , 0.0 ) );
				float4 AffectorsCE149 = ( _Affectors[0] * float4( staticSwitch93 , 0.0 ) );
				float3 Fix149 = staticSwitch93;
				float localCE1149 = CE1149( ParticleCenterCE149 , AffectorsCE149 , Fix149 );
				float DistanceMask45 = localCE1149;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				float4 break102 = appendResult17;
				float2 appendResult104 = (float2(break102.x , break102.z));
				float2 appendResult142 = (float2(break102.x , break102.y));
				#ifdef _XYUV_ON
				float2 staticSwitch140 = appendResult142;
				#else
				float2 staticSwitch140 = appendResult104;
				#endif
				float2 appendResult143 = (float2(break102.z , break102.y));
				#ifdef _ZYUV_ON
				float2 staticSwitch141 = appendResult143;
				#else
				float2 staticSwitch141 = staticSwitch140;
				#endif
				float2 ParticlePositionUV106 = staticSwitch141;
				float2 temp_output_119_0 = ( ParticlePositionUV106 * _NoiseTiling );
				float2 panner109 = ( ( ( _TimeParameters.x ) * _Noise02ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 panner131 = ( ( ( _TimeParameters.x ) * _NoiseDistortionScrollSpeed ) * float2( 0.05,0.05 ) + temp_output_119_0);
				float4 tex2DNode132 = tex2D( _NoiseDistortion, panner131 );
				float2 temp_cast_2 = (tex2DNode132.r).xx;
				float2 lerpResult127 = lerp( panner109 , temp_cast_2 , _NoiseDistortionPower);
				float2 panner110 = ( ( ( _TimeParameters.x ) * _Noise01ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 temp_cast_3 = (tex2DNode132.r).xx;
				float2 lerpResult128 = lerp( panner110 , temp_cast_3 , _NoiseDistortionPower);
				float ResultNoise111 = ( tex2D( _Noise02, lerpResult127 ).r * tex2D( _Noise01, lerpResult128 ).r * _NoisePower );
				float4 appendResult174 = (float4(input.ase_texcoord4.z , input.ase_texcoord4.w , input.ase_texcoord5.x , 0.0));
				float temp_output_176_0 = distance( appendResult174 , float4( _AudioPosition2 , 0.0 ) );
				float clampResult183 = clamp( (0.0 + (( -temp_output_176_0 + _AudioSpectrumDistanceTiling2 ) - 0.0) * (1.0 - 0.0) / (_AudioSpectrumDistanceTiling2 - 0.0)) , 0.0 , 1.0 );
				float clampResult191 = clamp( ( pow( clampResult183 , _AudioMaskExp2 ) * _AudioMaskMultiply2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOMASKENABLED2_ON
				float staticSwitch193 = clampResult191;
				#else
				float staticSwitch193 = 1.0;
				#endif
				float2 appendResult190 = (float2(( temp_output_176_0 * ( 1.0 / _AudioSpectrumDistanceTiling2 ) ) , 0.0));
				float clampResult197 = clamp( ( staticSwitch193 * tex2D( _AudioSpectrum, appendResult190 ).r * _AudioSpectrumPower2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOSPECTRUMENABLED2_ON
				float staticSwitch198 = clampResult197;
				#else
				float staticSwitch198 = 0.0;
				#endif
				float clampResult88 = clamp( ( ( ResultMask53 * _FinalMaskMultiply ) + ResultNoise111 + staticSwitch198 ) , 0.0 , 1.0 );
				float ResultMaskModified90 = clampResult88;
				float4 lerpResult37 = lerp( _FinalColor2 , _FinalColor , pow( ResultMaskModified90 , _FinalExp ));
				float2 appendResult83 = (float2(ResultMaskModified90 , 0.0));
				#ifdef _RAMPENABLED_ON
				float4 staticSwitch81 = tex2D( _Ramp, appendResult83 );
				#else
				float4 staticSwitch81 = lerpResult37;
				#endif
				float2 uv_FinalTexture = input.ase_texcoord4.xy * _FinalTexture_ST.xy + _FinalTexture_ST.zw;
				#ifdef _AUDIOAMPLITUDEENABLED2_ON
				float staticSwitch161 = 1.0;
				#else
				float staticSwitch161 = 0.0;
				#endif
				float clampResult163 = clamp( ( ( 1.0 - _AudioMaskAffectsAmplitude2 ) + staticSwitch193 ) , 0.0 , 1.0 );
				
				float3 BakedAlbedo = 0;
				float3 BakedEmission = 0;
				float3 Color = ( staticSwitch81 * input.ase_color * _FinalPower * tex2D( _FinalTexture, uv_FinalTexture ).r * ( 1.0 + ( _AudioAverageAmplitude * _AudioAmplitudeEmissionPower2 * staticSwitch161 * clampResult163 ) ) ).rgb;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#if defined(_DBUFFER)
					ApplyDecalToBaseColor(input.positionCS, Color);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODFadeCrossFade( input.positionCS );
				#endif

				#ifdef ASE_FOG
					Color = MixFog( Color, input.fogFactor );
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif

				return half4( Color, Alpha );
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual
			AlphaToMask Off
			ColorMask 0

			HLSLPROGRAM

			#pragma multi_compile_local _ALPHATEST_ON
			#pragma multi_compile_instancing
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			#pragma vertex vert
			#pragma fragment frag

			#define SHADERPASS SHADERPASS_SHADOWCASTER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#pragma shader_feature _IGNOREYAXIS_ON
			#pragma shader_feature _ZYUV_ON
			#pragma shader_feature _XYUV_ON
			#pragma shader_feature _AUDIOSPECTRUMENABLED2_ON
			#pragma shader_feature _AUDIOMASKENABLED2_ON
			#pragma shader_feature _AUDIOAMPLITUDEENABLED2_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD1;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FinalColor;
			float4 _FinalColor2;
			float4 _FinalTexture_ST;
			float _AffectorCount;
			float _FinalPower;
			float _FinalExp;
			float _AudioMaskAffectsAmplitude2;
			float _AudioAmplitudeOffsetPower2;
			float _OffsetPower;
			float _AudioSpectrumPower2;
			float _AudioMaskMultiply2;
			float _AudioMaskExp2;
			float _NoisePower;
			float _Noise01ScrollSpeed;
			float _NoiseDistortionPower;
			float _NoiseDistortionScrollSpeed;
			float _NoiseTiling;
			float _Noise02ScrollSpeed;
			float _FinalMaskMultiply;
			float _DistancePower;
			float _Distance;
			float _AudioSpectrumDistanceTiling2;
			float _AudioAmplitudeEmissionPower2;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			float4 _Affectors[20];
			sampler2D _Noise02;
			sampler2D _NoiseDistortion;
			sampler2D _Noise01;
			float3 _AudioPosition2;
			sampler2D _AudioSpectrum;
			float _AudioAverageAmplitude;


			float CE1149( float4 ParticleCenterCE, float4 AffectorsCE, float3 Fix )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w] * Fix);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w] * Fix) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			

			float3 _LightDirection;
			float3 _LightPosition;

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float4 appendResult17 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				#ifdef _IGNOREYAXIS_ON
				float3 staticSwitch93 = float3(1,0,1);
				#else
				float3 staticSwitch93 = float3(1,1,1);
				#endif
				float4 ParticleCenterCE149 = ( appendResult17 * float4( staticSwitch93 , 0.0 ) );
				float4 AffectorsCE149 = ( _Affectors[0] * float4( staticSwitch93 , 0.0 ) );
				float3 Fix149 = staticSwitch93;
				float localCE1149 = CE1149( ParticleCenterCE149 , AffectorsCE149 , Fix149 );
				float DistanceMask45 = localCE1149;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				float4 break102 = appendResult17;
				float2 appendResult104 = (float2(break102.x , break102.z));
				float2 appendResult142 = (float2(break102.x , break102.y));
				#ifdef _XYUV_ON
				float2 staticSwitch140 = appendResult142;
				#else
				float2 staticSwitch140 = appendResult104;
				#endif
				float2 appendResult143 = (float2(break102.z , break102.y));
				#ifdef _ZYUV_ON
				float2 staticSwitch141 = appendResult143;
				#else
				float2 staticSwitch141 = staticSwitch140;
				#endif
				float2 ParticlePositionUV106 = staticSwitch141;
				float2 temp_output_119_0 = ( ParticlePositionUV106 * _NoiseTiling );
				float2 panner109 = ( ( ( _TimeParameters.x ) * _Noise02ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 panner131 = ( ( ( _TimeParameters.x ) * _NoiseDistortionScrollSpeed ) * float2( 0.05,0.05 ) + temp_output_119_0);
				float4 tex2DNode132 = tex2Dlod( _NoiseDistortion, float4( panner131, 0, 0.0) );
				float2 temp_cast_2 = (tex2DNode132.r).xx;
				float2 lerpResult127 = lerp( panner109 , temp_cast_2 , _NoiseDistortionPower);
				float2 panner110 = ( ( ( _TimeParameters.x ) * _Noise01ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 temp_cast_3 = (tex2DNode132.r).xx;
				float2 lerpResult128 = lerp( panner110 , temp_cast_3 , _NoiseDistortionPower);
				float ResultNoise111 = ( tex2Dlod( _Noise02, float4( lerpResult127, 0, 0.0) ).r * tex2Dlod( _Noise01, float4( lerpResult128, 0, 0.0) ).r * _NoisePower );
				float4 appendResult174 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				float temp_output_176_0 = distance( appendResult174 , float4( _AudioPosition2 , 0.0 ) );
				float clampResult183 = clamp( (0.0 + (( -temp_output_176_0 + _AudioSpectrumDistanceTiling2 ) - 0.0) * (1.0 - 0.0) / (_AudioSpectrumDistanceTiling2 - 0.0)) , 0.0 , 1.0 );
				float clampResult191 = clamp( ( pow( clampResult183 , _AudioMaskExp2 ) * _AudioMaskMultiply2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOMASKENABLED2_ON
				float staticSwitch193 = clampResult191;
				#else
				float staticSwitch193 = 1.0;
				#endif
				float2 appendResult190 = (float2(( temp_output_176_0 * ( 1.0 / _AudioSpectrumDistanceTiling2 ) ) , 0.0));
				float clampResult197 = clamp( ( staticSwitch193 * tex2Dlod( _AudioSpectrum, float4( appendResult190, 0, 0.0) ).r * _AudioSpectrumPower2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOSPECTRUMENABLED2_ON
				float staticSwitch198 = clampResult197;
				#else
				float staticSwitch198 = 0.0;
				#endif
				float clampResult88 = clamp( ( ( ResultMask53 * _FinalMaskMultiply ) + ResultNoise111 + staticSwitch198 ) , 0.0 , 1.0 );
				float ResultMaskModified90 = clampResult88;
				float3 appendResult138 = (float3(input.ase_texcoord1.y , input.ase_texcoord1.z , input.ase_texcoord1.w));
				float3 normalizeResult139 = normalize( appendResult138 );
				#ifdef _AUDIOAMPLITUDEENABLED2_ON
				float staticSwitch161 = 1.0;
				#else
				float staticSwitch161 = 0.0;
				#endif
				float clampResult163 = clamp( ( ( 1.0 - _AudioMaskAffectsAmplitude2 ) + staticSwitch193 ) , 0.0 , 1.0 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = ( ( 1.0 - ResultMaskModified90 ) * _OffsetPower * normalizeResult139 * ( ( _AudioAverageAmplitude * _AudioAmplitudeOffsetPower2 * staticSwitch161 * clampResult163 ) + 1.0 ) );

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = positionWS;
				#endif

				float3 normalWS = TransformObjectToWorldDir( input.normalOS );

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif

				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

				//code for UNITY_REVERSED_Z is moved into Shadows.hlsl from 6000.0.22 and or higher
				positionCS = ApplyShadowClamping(positionCS);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = positionCS;
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = positionCS;

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.vertex = input.positionOS;
				output.normalOS = input.normalOS;
				output.ase_texcoord = input.ase_texcoord;
				output.ase_texcoord1 = input.ase_texcoord1;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = input.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef _ALPHATEST_ON
					#ifdef _ALPHATEST_SHADOW_ON
						clip(Alpha - AlphaClipThresholdShadow);
					#else
						clip(Alpha - AlphaClipThreshold);
					#endif
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODFadeCrossFade( input.positionCS );
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask 0
			AlphaToMask Off

			HLSLPROGRAM

			#pragma multi_compile_local _ALPHATEST_ON
			#pragma multi_compile_instancing
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#pragma shader_feature _IGNOREYAXIS_ON
			#pragma shader_feature _ZYUV_ON
			#pragma shader_feature _XYUV_ON
			#pragma shader_feature _AUDIOSPECTRUMENABLED2_ON
			#pragma shader_feature _AUDIOMASKENABLED2_ON
			#pragma shader_feature _AUDIOAMPLITUDEENABLED2_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 positionWS : TEXCOORD1;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
				float4 shadowCoord : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FinalColor;
			float4 _FinalColor2;
			float4 _FinalTexture_ST;
			float _AffectorCount;
			float _FinalPower;
			float _FinalExp;
			float _AudioMaskAffectsAmplitude2;
			float _AudioAmplitudeOffsetPower2;
			float _OffsetPower;
			float _AudioSpectrumPower2;
			float _AudioMaskMultiply2;
			float _AudioMaskExp2;
			float _NoisePower;
			float _Noise01ScrollSpeed;
			float _NoiseDistortionPower;
			float _NoiseDistortionScrollSpeed;
			float _NoiseTiling;
			float _Noise02ScrollSpeed;
			float _FinalMaskMultiply;
			float _DistancePower;
			float _Distance;
			float _AudioSpectrumDistanceTiling2;
			float _AudioAmplitudeEmissionPower2;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			float4 _Affectors[20];
			sampler2D _Noise02;
			sampler2D _NoiseDistortion;
			sampler2D _Noise01;
			float3 _AudioPosition2;
			sampler2D _AudioSpectrum;
			float _AudioAverageAmplitude;


			float CE1149( float4 ParticleCenterCE, float4 AffectorsCE, float3 Fix )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w] * Fix);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w] * Fix) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float4 appendResult17 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				#ifdef _IGNOREYAXIS_ON
				float3 staticSwitch93 = float3(1,0,1);
				#else
				float3 staticSwitch93 = float3(1,1,1);
				#endif
				float4 ParticleCenterCE149 = ( appendResult17 * float4( staticSwitch93 , 0.0 ) );
				float4 AffectorsCE149 = ( _Affectors[0] * float4( staticSwitch93 , 0.0 ) );
				float3 Fix149 = staticSwitch93;
				float localCE1149 = CE1149( ParticleCenterCE149 , AffectorsCE149 , Fix149 );
				float DistanceMask45 = localCE1149;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				float4 break102 = appendResult17;
				float2 appendResult104 = (float2(break102.x , break102.z));
				float2 appendResult142 = (float2(break102.x , break102.y));
				#ifdef _XYUV_ON
				float2 staticSwitch140 = appendResult142;
				#else
				float2 staticSwitch140 = appendResult104;
				#endif
				float2 appendResult143 = (float2(break102.z , break102.y));
				#ifdef _ZYUV_ON
				float2 staticSwitch141 = appendResult143;
				#else
				float2 staticSwitch141 = staticSwitch140;
				#endif
				float2 ParticlePositionUV106 = staticSwitch141;
				float2 temp_output_119_0 = ( ParticlePositionUV106 * _NoiseTiling );
				float2 panner109 = ( ( ( _TimeParameters.x ) * _Noise02ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 panner131 = ( ( ( _TimeParameters.x ) * _NoiseDistortionScrollSpeed ) * float2( 0.05,0.05 ) + temp_output_119_0);
				float4 tex2DNode132 = tex2Dlod( _NoiseDistortion, float4( panner131, 0, 0.0) );
				float2 temp_cast_2 = (tex2DNode132.r).xx;
				float2 lerpResult127 = lerp( panner109 , temp_cast_2 , _NoiseDistortionPower);
				float2 panner110 = ( ( ( _TimeParameters.x ) * _Noise01ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 temp_cast_3 = (tex2DNode132.r).xx;
				float2 lerpResult128 = lerp( panner110 , temp_cast_3 , _NoiseDistortionPower);
				float ResultNoise111 = ( tex2Dlod( _Noise02, float4( lerpResult127, 0, 0.0) ).r * tex2Dlod( _Noise01, float4( lerpResult128, 0, 0.0) ).r * _NoisePower );
				float4 appendResult174 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				float temp_output_176_0 = distance( appendResult174 , float4( _AudioPosition2 , 0.0 ) );
				float clampResult183 = clamp( (0.0 + (( -temp_output_176_0 + _AudioSpectrumDistanceTiling2 ) - 0.0) * (1.0 - 0.0) / (_AudioSpectrumDistanceTiling2 - 0.0)) , 0.0 , 1.0 );
				float clampResult191 = clamp( ( pow( clampResult183 , _AudioMaskExp2 ) * _AudioMaskMultiply2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOMASKENABLED2_ON
				float staticSwitch193 = clampResult191;
				#else
				float staticSwitch193 = 1.0;
				#endif
				float2 appendResult190 = (float2(( temp_output_176_0 * ( 1.0 / _AudioSpectrumDistanceTiling2 ) ) , 0.0));
				float clampResult197 = clamp( ( staticSwitch193 * tex2Dlod( _AudioSpectrum, float4( appendResult190, 0, 0.0) ).r * _AudioSpectrumPower2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOSPECTRUMENABLED2_ON
				float staticSwitch198 = clampResult197;
				#else
				float staticSwitch198 = 0.0;
				#endif
				float clampResult88 = clamp( ( ( ResultMask53 * _FinalMaskMultiply ) + ResultNoise111 + staticSwitch198 ) , 0.0 , 1.0 );
				float ResultMaskModified90 = clampResult88;
				float3 appendResult138 = (float3(input.ase_texcoord1.y , input.ase_texcoord1.z , input.ase_texcoord1.w));
				float3 normalizeResult139 = normalize( appendResult138 );
				#ifdef _AUDIOAMPLITUDEENABLED2_ON
				float staticSwitch161 = 1.0;
				#else
				float staticSwitch161 = 0.0;
				#endif
				float clampResult163 = clamp( ( ( 1.0 - _AudioMaskAffectsAmplitude2 ) + staticSwitch193 ) , 0.0 , 1.0 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = ( ( 1.0 - ResultMaskModified90 ) * _OffsetPower * normalizeResult139 * ( ( _AudioAverageAmplitude * _AudioAmplitudeOffsetPower2 * staticSwitch161 * clampResult163 ) + 1.0 ) );

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = vertexInput.positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;
				output.clipPosV = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.vertex = input.positionOS;
				output.normalOS = input.normalOS;
				output.ase_texcoord = input.ase_texcoord;
				output.ase_texcoord1 = input.ase_texcoord1;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
				float3 WorldPosition = input.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODFadeCrossFade( input.positionCS );
				#endif
				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull Off
			AlphaToMask Off

			HLSLPROGRAM

			#pragma multi_compile_local _ALPHATEST_ON
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#pragma shader_feature _IGNOREYAXIS_ON
			#pragma shader_feature _ZYUV_ON
			#pragma shader_feature _XYUV_ON
			#pragma shader_feature _AUDIOSPECTRUMENABLED2_ON
			#pragma shader_feature _AUDIOMASKENABLED2_ON
			#pragma shader_feature _AUDIOAMPLITUDEENABLED2_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FinalColor;
			float4 _FinalColor2;
			float4 _FinalTexture_ST;
			float _AffectorCount;
			float _FinalPower;
			float _FinalExp;
			float _AudioMaskAffectsAmplitude2;
			float _AudioAmplitudeOffsetPower2;
			float _OffsetPower;
			float _AudioSpectrumPower2;
			float _AudioMaskMultiply2;
			float _AudioMaskExp2;
			float _NoisePower;
			float _Noise01ScrollSpeed;
			float _NoiseDistortionPower;
			float _NoiseDistortionScrollSpeed;
			float _NoiseTiling;
			float _Noise02ScrollSpeed;
			float _FinalMaskMultiply;
			float _DistancePower;
			float _Distance;
			float _AudioSpectrumDistanceTiling2;
			float _AudioAmplitudeEmissionPower2;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			float4 _Affectors[20];
			sampler2D _Noise02;
			sampler2D _NoiseDistortion;
			sampler2D _Noise01;
			float3 _AudioPosition2;
			sampler2D _AudioSpectrum;
			float _AudioAverageAmplitude;


			float CE1149( float4 ParticleCenterCE, float4 AffectorsCE, float3 Fix )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w] * Fix);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w] * Fix) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			

			int _ObjectId;
			int _PassValue;

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float4 appendResult17 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				#ifdef _IGNOREYAXIS_ON
				float3 staticSwitch93 = float3(1,0,1);
				#else
				float3 staticSwitch93 = float3(1,1,1);
				#endif
				float4 ParticleCenterCE149 = ( appendResult17 * float4( staticSwitch93 , 0.0 ) );
				float4 AffectorsCE149 = ( _Affectors[0] * float4( staticSwitch93 , 0.0 ) );
				float3 Fix149 = staticSwitch93;
				float localCE1149 = CE1149( ParticleCenterCE149 , AffectorsCE149 , Fix149 );
				float DistanceMask45 = localCE1149;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				float4 break102 = appendResult17;
				float2 appendResult104 = (float2(break102.x , break102.z));
				float2 appendResult142 = (float2(break102.x , break102.y));
				#ifdef _XYUV_ON
				float2 staticSwitch140 = appendResult142;
				#else
				float2 staticSwitch140 = appendResult104;
				#endif
				float2 appendResult143 = (float2(break102.z , break102.y));
				#ifdef _ZYUV_ON
				float2 staticSwitch141 = appendResult143;
				#else
				float2 staticSwitch141 = staticSwitch140;
				#endif
				float2 ParticlePositionUV106 = staticSwitch141;
				float2 temp_output_119_0 = ( ParticlePositionUV106 * _NoiseTiling );
				float2 panner109 = ( ( ( _TimeParameters.x ) * _Noise02ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 panner131 = ( ( ( _TimeParameters.x ) * _NoiseDistortionScrollSpeed ) * float2( 0.05,0.05 ) + temp_output_119_0);
				float4 tex2DNode132 = tex2Dlod( _NoiseDistortion, float4( panner131, 0, 0.0) );
				float2 temp_cast_2 = (tex2DNode132.r).xx;
				float2 lerpResult127 = lerp( panner109 , temp_cast_2 , _NoiseDistortionPower);
				float2 panner110 = ( ( ( _TimeParameters.x ) * _Noise01ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 temp_cast_3 = (tex2DNode132.r).xx;
				float2 lerpResult128 = lerp( panner110 , temp_cast_3 , _NoiseDistortionPower);
				float ResultNoise111 = ( tex2Dlod( _Noise02, float4( lerpResult127, 0, 0.0) ).r * tex2Dlod( _Noise01, float4( lerpResult128, 0, 0.0) ).r * _NoisePower );
				float4 appendResult174 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				float temp_output_176_0 = distance( appendResult174 , float4( _AudioPosition2 , 0.0 ) );
				float clampResult183 = clamp( (0.0 + (( -temp_output_176_0 + _AudioSpectrumDistanceTiling2 ) - 0.0) * (1.0 - 0.0) / (_AudioSpectrumDistanceTiling2 - 0.0)) , 0.0 , 1.0 );
				float clampResult191 = clamp( ( pow( clampResult183 , _AudioMaskExp2 ) * _AudioMaskMultiply2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOMASKENABLED2_ON
				float staticSwitch193 = clampResult191;
				#else
				float staticSwitch193 = 1.0;
				#endif
				float2 appendResult190 = (float2(( temp_output_176_0 * ( 1.0 / _AudioSpectrumDistanceTiling2 ) ) , 0.0));
				float clampResult197 = clamp( ( staticSwitch193 * tex2Dlod( _AudioSpectrum, float4( appendResult190, 0, 0.0) ).r * _AudioSpectrumPower2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOSPECTRUMENABLED2_ON
				float staticSwitch198 = clampResult197;
				#else
				float staticSwitch198 = 0.0;
				#endif
				float clampResult88 = clamp( ( ( ResultMask53 * _FinalMaskMultiply ) + ResultNoise111 + staticSwitch198 ) , 0.0 , 1.0 );
				float ResultMaskModified90 = clampResult88;
				float3 appendResult138 = (float3(input.ase_texcoord1.y , input.ase_texcoord1.z , input.ase_texcoord1.w));
				float3 normalizeResult139 = normalize( appendResult138 );
				#ifdef _AUDIOAMPLITUDEENABLED2_ON
				float staticSwitch161 = 1.0;
				#else
				float staticSwitch161 = 0.0;
				#endif
				float clampResult163 = clamp( ( ( 1.0 - _AudioMaskAffectsAmplitude2 ) + staticSwitch193 ) , 0.0 , 1.0 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = ( ( 1.0 - ResultMaskModified90 ) * _OffsetPower * normalizeResult139 * ( ( _AudioAverageAmplitude * _AudioAmplitudeOffsetPower2 * staticSwitch161 * clampResult163 ) + 1.0 ) );

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );

				output.positionCS = TransformWorldToHClip(positionWS);

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.vertex = input.positionOS;
				output.normalOS = input.normalOS;
				output.ase_texcoord = input.ase_texcoord;
				output.ase_texcoord1 = input.ase_texcoord1;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				

				surfaceDescription.Alpha = 1;
				surfaceDescription.AlphaClipThreshold = 0.5;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				return outColor;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			#pragma multi_compile_local _ALPHATEST_ON
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT

			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#pragma shader_feature _IGNOREYAXIS_ON
			#pragma shader_feature _ZYUV_ON
			#pragma shader_feature _XYUV_ON
			#pragma shader_feature _AUDIOSPECTRUMENABLED2_ON
			#pragma shader_feature _AUDIOMASKENABLED2_ON
			#pragma shader_feature _AUDIOAMPLITUDEENABLED2_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FinalColor;
			float4 _FinalColor2;
			float4 _FinalTexture_ST;
			float _AffectorCount;
			float _FinalPower;
			float _FinalExp;
			float _AudioMaskAffectsAmplitude2;
			float _AudioAmplitudeOffsetPower2;
			float _OffsetPower;
			float _AudioSpectrumPower2;
			float _AudioMaskMultiply2;
			float _AudioMaskExp2;
			float _NoisePower;
			float _Noise01ScrollSpeed;
			float _NoiseDistortionPower;
			float _NoiseDistortionScrollSpeed;
			float _NoiseTiling;
			float _Noise02ScrollSpeed;
			float _FinalMaskMultiply;
			float _DistancePower;
			float _Distance;
			float _AudioSpectrumDistanceTiling2;
			float _AudioAmplitudeEmissionPower2;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			float4 _Affectors[20];
			sampler2D _Noise02;
			sampler2D _NoiseDistortion;
			sampler2D _Noise01;
			float3 _AudioPosition2;
			sampler2D _AudioSpectrum;
			float _AudioAverageAmplitude;


			float CE1149( float4 ParticleCenterCE, float4 AffectorsCE, float3 Fix )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w] * Fix);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w] * Fix) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			

			float4 _SelectionID;

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float4 appendResult17 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				#ifdef _IGNOREYAXIS_ON
				float3 staticSwitch93 = float3(1,0,1);
				#else
				float3 staticSwitch93 = float3(1,1,1);
				#endif
				float4 ParticleCenterCE149 = ( appendResult17 * float4( staticSwitch93 , 0.0 ) );
				float4 AffectorsCE149 = ( _Affectors[0] * float4( staticSwitch93 , 0.0 ) );
				float3 Fix149 = staticSwitch93;
				float localCE1149 = CE1149( ParticleCenterCE149 , AffectorsCE149 , Fix149 );
				float DistanceMask45 = localCE1149;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				float4 break102 = appendResult17;
				float2 appendResult104 = (float2(break102.x , break102.z));
				float2 appendResult142 = (float2(break102.x , break102.y));
				#ifdef _XYUV_ON
				float2 staticSwitch140 = appendResult142;
				#else
				float2 staticSwitch140 = appendResult104;
				#endif
				float2 appendResult143 = (float2(break102.z , break102.y));
				#ifdef _ZYUV_ON
				float2 staticSwitch141 = appendResult143;
				#else
				float2 staticSwitch141 = staticSwitch140;
				#endif
				float2 ParticlePositionUV106 = staticSwitch141;
				float2 temp_output_119_0 = ( ParticlePositionUV106 * _NoiseTiling );
				float2 panner109 = ( ( ( _TimeParameters.x ) * _Noise02ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 panner131 = ( ( ( _TimeParameters.x ) * _NoiseDistortionScrollSpeed ) * float2( 0.05,0.05 ) + temp_output_119_0);
				float4 tex2DNode132 = tex2Dlod( _NoiseDistortion, float4( panner131, 0, 0.0) );
				float2 temp_cast_2 = (tex2DNode132.r).xx;
				float2 lerpResult127 = lerp( panner109 , temp_cast_2 , _NoiseDistortionPower);
				float2 panner110 = ( ( ( _TimeParameters.x ) * _Noise01ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 temp_cast_3 = (tex2DNode132.r).xx;
				float2 lerpResult128 = lerp( panner110 , temp_cast_3 , _NoiseDistortionPower);
				float ResultNoise111 = ( tex2Dlod( _Noise02, float4( lerpResult127, 0, 0.0) ).r * tex2Dlod( _Noise01, float4( lerpResult128, 0, 0.0) ).r * _NoisePower );
				float4 appendResult174 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				float temp_output_176_0 = distance( appendResult174 , float4( _AudioPosition2 , 0.0 ) );
				float clampResult183 = clamp( (0.0 + (( -temp_output_176_0 + _AudioSpectrumDistanceTiling2 ) - 0.0) * (1.0 - 0.0) / (_AudioSpectrumDistanceTiling2 - 0.0)) , 0.0 , 1.0 );
				float clampResult191 = clamp( ( pow( clampResult183 , _AudioMaskExp2 ) * _AudioMaskMultiply2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOMASKENABLED2_ON
				float staticSwitch193 = clampResult191;
				#else
				float staticSwitch193 = 1.0;
				#endif
				float2 appendResult190 = (float2(( temp_output_176_0 * ( 1.0 / _AudioSpectrumDistanceTiling2 ) ) , 0.0));
				float clampResult197 = clamp( ( staticSwitch193 * tex2Dlod( _AudioSpectrum, float4( appendResult190, 0, 0.0) ).r * _AudioSpectrumPower2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOSPECTRUMENABLED2_ON
				float staticSwitch198 = clampResult197;
				#else
				float staticSwitch198 = 0.0;
				#endif
				float clampResult88 = clamp( ( ( ResultMask53 * _FinalMaskMultiply ) + ResultNoise111 + staticSwitch198 ) , 0.0 , 1.0 );
				float ResultMaskModified90 = clampResult88;
				float3 appendResult138 = (float3(input.ase_texcoord1.y , input.ase_texcoord1.z , input.ase_texcoord1.w));
				float3 normalizeResult139 = normalize( appendResult138 );
				#ifdef _AUDIOAMPLITUDEENABLED2_ON
				float staticSwitch161 = 1.0;
				#else
				float staticSwitch161 = 0.0;
				#endif
				float clampResult163 = clamp( ( ( 1.0 - _AudioMaskAffectsAmplitude2 ) + staticSwitch193 ) , 0.0 , 1.0 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = ( ( 1.0 - ResultMaskModified90 ) * _OffsetPower * normalizeResult139 * ( ( _AudioAverageAmplitude * _AudioAmplitudeOffsetPower2 * staticSwitch161 * clampResult163 ) + 1.0 ) );

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
				output.positionCS = TransformWorldToHClip(positionWS);
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.vertex = input.positionOS;
				output.normalOS = input.normalOS;
				output.ase_texcoord = input.ase_texcoord;
				output.ase_texcoord1 = input.ase_texcoord1;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input ) : SV_TARGET
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				

				surfaceDescription.Alpha = 1;
				surfaceDescription.AlphaClipThreshold = 0.5;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;
				outColor = _SelectionID;

				return outColor;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormalsOnly" }

			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

        	#pragma multi_compile_local _ALPHATEST_ON
        	#pragma multi_compile_instancing
        	#define ASE_VERSION 19701
        	#define ASE_SRP_VERSION 170003


        	#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

			#pragma vertex vert
			#pragma fragment frag

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define VARYINGS_NEED_NORMAL_WS

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#pragma shader_feature _IGNOREYAXIS_ON
			#pragma shader_feature _ZYUV_ON
			#pragma shader_feature _XYUV_ON
			#pragma shader_feature _AUDIOSPECTRUMENABLED2_ON
			#pragma shader_feature _AUDIOMASKENABLED2_ON
			#pragma shader_feature _AUDIOAMPLITUDEENABLED2_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FinalColor;
			float4 _FinalColor2;
			float4 _FinalTexture_ST;
			float _AffectorCount;
			float _FinalPower;
			float _FinalExp;
			float _AudioMaskAffectsAmplitude2;
			float _AudioAmplitudeOffsetPower2;
			float _OffsetPower;
			float _AudioSpectrumPower2;
			float _AudioMaskMultiply2;
			float _AudioMaskExp2;
			float _NoisePower;
			float _Noise01ScrollSpeed;
			float _NoiseDistortionPower;
			float _NoiseDistortionScrollSpeed;
			float _NoiseTiling;
			float _Noise02ScrollSpeed;
			float _FinalMaskMultiply;
			float _DistancePower;
			float _Distance;
			float _AudioSpectrumDistanceTiling2;
			float _AudioAmplitudeEmissionPower2;
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			float4 _Affectors[20];
			sampler2D _Noise02;
			sampler2D _NoiseDistortion;
			sampler2D _Noise01;
			float3 _AudioPosition2;
			sampler2D _AudioSpectrum;
			float _AudioAverageAmplitude;


			float CE1149( float4 ParticleCenterCE, float4 AffectorsCE, float3 Fix )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w] * Fix);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w] * Fix) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			

			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float4 appendResult17 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				#ifdef _IGNOREYAXIS_ON
				float3 staticSwitch93 = float3(1,0,1);
				#else
				float3 staticSwitch93 = float3(1,1,1);
				#endif
				float4 ParticleCenterCE149 = ( appendResult17 * float4( staticSwitch93 , 0.0 ) );
				float4 AffectorsCE149 = ( _Affectors[0] * float4( staticSwitch93 , 0.0 ) );
				float3 Fix149 = staticSwitch93;
				float localCE1149 = CE1149( ParticleCenterCE149 , AffectorsCE149 , Fix149 );
				float DistanceMask45 = localCE1149;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				float4 break102 = appendResult17;
				float2 appendResult104 = (float2(break102.x , break102.z));
				float2 appendResult142 = (float2(break102.x , break102.y));
				#ifdef _XYUV_ON
				float2 staticSwitch140 = appendResult142;
				#else
				float2 staticSwitch140 = appendResult104;
				#endif
				float2 appendResult143 = (float2(break102.z , break102.y));
				#ifdef _ZYUV_ON
				float2 staticSwitch141 = appendResult143;
				#else
				float2 staticSwitch141 = staticSwitch140;
				#endif
				float2 ParticlePositionUV106 = staticSwitch141;
				float2 temp_output_119_0 = ( ParticlePositionUV106 * _NoiseTiling );
				float2 panner109 = ( ( ( _TimeParameters.x ) * _Noise02ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 panner131 = ( ( ( _TimeParameters.x ) * _NoiseDistortionScrollSpeed ) * float2( 0.05,0.05 ) + temp_output_119_0);
				float4 tex2DNode132 = tex2Dlod( _NoiseDistortion, float4( panner131, 0, 0.0) );
				float2 temp_cast_2 = (tex2DNode132.r).xx;
				float2 lerpResult127 = lerp( panner109 , temp_cast_2 , _NoiseDistortionPower);
				float2 panner110 = ( ( ( _TimeParameters.x ) * _Noise01ScrollSpeed ) * float2( 1,1 ) + temp_output_119_0);
				float2 temp_cast_3 = (tex2DNode132.r).xx;
				float2 lerpResult128 = lerp( panner110 , temp_cast_3 , _NoiseDistortionPower);
				float ResultNoise111 = ( tex2Dlod( _Noise02, float4( lerpResult127, 0, 0.0) ).r * tex2Dlod( _Noise01, float4( lerpResult128, 0, 0.0) ).r * _NoisePower );
				float4 appendResult174 = (float4(input.ase_texcoord.z , input.ase_texcoord.w , input.ase_texcoord1.x , 0.0));
				float temp_output_176_0 = distance( appendResult174 , float4( _AudioPosition2 , 0.0 ) );
				float clampResult183 = clamp( (0.0 + (( -temp_output_176_0 + _AudioSpectrumDistanceTiling2 ) - 0.0) * (1.0 - 0.0) / (_AudioSpectrumDistanceTiling2 - 0.0)) , 0.0 , 1.0 );
				float clampResult191 = clamp( ( pow( clampResult183 , _AudioMaskExp2 ) * _AudioMaskMultiply2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOMASKENABLED2_ON
				float staticSwitch193 = clampResult191;
				#else
				float staticSwitch193 = 1.0;
				#endif
				float2 appendResult190 = (float2(( temp_output_176_0 * ( 1.0 / _AudioSpectrumDistanceTiling2 ) ) , 0.0));
				float clampResult197 = clamp( ( staticSwitch193 * tex2Dlod( _AudioSpectrum, float4( appendResult190, 0, 0.0) ).r * _AudioSpectrumPower2 ) , 0.0 , 1.0 );
				#ifdef _AUDIOSPECTRUMENABLED2_ON
				float staticSwitch198 = clampResult197;
				#else
				float staticSwitch198 = 0.0;
				#endif
				float clampResult88 = clamp( ( ( ResultMask53 * _FinalMaskMultiply ) + ResultNoise111 + staticSwitch198 ) , 0.0 , 1.0 );
				float ResultMaskModified90 = clampResult88;
				float3 appendResult138 = (float3(input.ase_texcoord1.y , input.ase_texcoord1.z , input.ase_texcoord1.w));
				float3 normalizeResult139 = normalize( appendResult138 );
				#ifdef _AUDIOAMPLITUDEENABLED2_ON
				float staticSwitch161 = 1.0;
				#else
				float staticSwitch161 = 0.0;
				#endif
				float clampResult163 = clamp( ( ( 1.0 - _AudioMaskAffectsAmplitude2 ) + staticSwitch193 ) , 0.0 , 1.0 );
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = ( ( 1.0 - ResultMaskModified90 ) * _OffsetPower * normalizeResult139 * ( ( _AudioAverageAmplitude * _AudioAmplitudeOffsetPower2 * staticSwitch161 * clampResult163 ) + 1.0 ) );

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				output.positionCS = vertexInput.positionCS;
				output.clipPosV = vertexInput.positionCS;
				output.normalWS = TransformObjectToWorldNormal( input.normalOS );
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.vertex = input.positionOS;
				output.normalOS = input.normalOS;
				output.ase_texcoord = input.ase_texcoord;
				output.ase_texcoord1 = input.ase_texcoord1;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].vertex, input[1].vertex, input[2].vertex, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].vertex * bary.x + patch[1].vertex * bary.y + patch[2].vertex * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].vertex.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			void frag( PackedVaryings input
				, out half4 outNormalWS : SV_Target0
			#ifdef _WRITE_RENDERING_LAYERS
				, out float4 outRenderingLayers : SV_Target1
			#endif
				 )
			{
				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#if _ALPHATEST_ON
					clip( Alpha - AlphaClipThreshold );
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(_GBUFFER_NORMALS_OCT)
					float3 normalWS = normalize(input.normalWS);
					float2 octNormalWS = PackNormalOctQuadEncode(normalWS);           // values between [-1, +1], must use fp32 on some platforms
					float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);   // values between [ 0,  1]
					half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);      // values between [ 0,  1]
					outNormalWS = half4(packedNormalWS, 0.0);
				#else
					float3 normalWS = input.normalWS;
					outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
				#endif
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "MotionVectors"
			Tags { "LightMode"="MotionVectors" }

			ColorMask RG

			HLSLPROGRAM

			#pragma multi_compile_local _ALPHATEST_ON
			#pragma multi_compile_instancing
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
		    #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
		    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
		    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
			#endif

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

			

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 positionOld : TEXCOORD4;
				#if _ADD_PRECOMPUTED_VELOCITY
					float3 alembicMotionVector : TEXCOORD5;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				float4 positionCSNoJitter : TEXCOORD0;
				float4 previousPositionCSNoJitter : TEXCOORD1;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _FinalColor;
			float4 _FinalColor2;
			float4 _FinalTexture_ST;
			float _AffectorCount;
			float _FinalPower;
			float _FinalExp;
			float _AudioMaskAffectsAmplitude2;
			float _AudioAmplitudeOffsetPower2;
			float _OffsetPower;
			float _AudioSpectrumPower2;
			float _AudioMaskMultiply2;
			float _AudioMaskExp2;
			float _NoisePower;
			float _Noise01ScrollSpeed;
			float _NoiseDistortionPower;
			float _NoiseDistortionScrollSpeed;
			float _NoiseTiling;
			float _Noise02ScrollSpeed;
			float _FinalMaskMultiply;
			float _DistancePower;
			float _Distance;
			float _AudioSpectrumDistanceTiling2;
			float _AudioAmplitudeEmissionPower2;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				// Jittered. Match the frame.
				output.positionCS = vertexInput.positionCS;
				output.positionCSNoJitter = mul( _NonJitteredViewProjMatrix, mul( UNITY_MATRIX_M, input.positionOS ) );

				float4 prevPos = ( unity_MotionVectorsParams.x == 1 ) ? float4( input.positionOld, 1 ) : input.positionOS;

				#if _ADD_PRECOMPUTED_VELOCITY
					prevPos = prevPos - float4(input.alembicMotionVector, 0);
				#endif

				output.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, mul( UNITY_PREV_MATRIX_M, prevPos ) );

				return output;
			}

			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}

			half4 frag(	PackedVaryings input  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#ifdef LOD_FADE_CROSSFADE
					LODFadeCrossFade( input.positionCS );
				#endif

				return float4( CalcNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 0, 0 );
			}
			ENDHLSL
		}
		
	}
	
	CustomEditor "UnityEditor.ShaderGraphUnlitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback "Hidden/InternalErrorShader"
}
/*ASEBEGIN
Version=19701
Node;AmplifyShaderEditor.TexCoordVertexDataNode;153;-3339.958,-1293.189;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;154;-3344.958,-1014.189;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode;17;-2987.389,-1125.477;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;173;-5429.458,1564.886;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;172;-5452.424,1329.569;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.BreakToComponentsNode;102;-2749.313,-1303.455;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.DynamicAppendNode;104;-2470.998,-1297.124;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector3Node;175;-5141.458,1724.886;Inherit;False;Global;_AudioPosition2;_AudioPosition;33;0;Create;True;0;0;0;False;0;False;0,0,0;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;174;-5093.458,1500.886;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector3Node;97;-3298.546,-659.4002;Float;False;Constant;_Vector2;Vector 2;11;0;Create;True;0;0;0;False;0;False;1,1,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.Vector3Node;96;-3304.546,-809.4006;Float;False;Constant;_Vector1;Vector 1;11;0;Create;True;0;0;0;False;0;False;1,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DynamicAppendNode;142;-2469.166,-1404.684;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;143;-2459.352,-1524.997;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GlobalArrayNode;150;-3055.731,-873.068;Inherit;False;_Affectors;0;20;2;False;False;0;1;False;Object;-1;4;0;INT;0;False;2;INT;0;False;1;INT;0;False;3;INT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StaticSwitch;140;-2226.134,-1435.527;Float;False;Property;_XYUV;XY UV;23;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StaticSwitch;93;-3067.546,-737.4005;Float;False;Property;_IgnoreYAxis;Ignore Y Axis;12;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DistanceOpNode;176;-4821.458,1612.886;Inherit;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;141;-1977.886,-1514.628;Float;False;Property;_ZYUV;ZY UV;22;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;94;-2744.945,-1111.9;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;151;-2733.321,-913.8816;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.NegateNode;177;-4341.458,2156.886;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;178;-4437.458,1836.886;Float;False;Property;_AudioSpectrumDistanceTiling2;Audio Spectrum Distance Tiling;27;0;Create;True;0;0;0;False;0;False;4;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;106;-1796.388,-1303.287;Float;False;ParticlePositionUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CustomExpressionNode;149;-2507.039,-1023.095;Float;False;float DistanceMaskMY@$$for (int w = 0@ w < _AffectorCount@ w++) {$if(w == 0){$DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w] * Fix)@$}else{$DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w] * Fix) )@	$}$}$DistanceMaskMY = 1.0 - DistanceMaskMY@$return DistanceMaskMY@;1;Create;3;True;ParticleCenterCE;FLOAT4;0,0,0,0;In;;Float;False;True;AffectorsCE;FLOAT4;0,0,0,0;In;;Float;False;True;Fix;FLOAT3;0,0,0;In;;Float;False;CE1;True;False;0;;False;3;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;179;-4181.457,2220.886;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;25;-3429.446,384.8477;Float;False;Property;_Distance;Distance;9;0;Create;True;0;0;0;False;0;False;1;1.75;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;120;-3786.271,-1925.718;Float;False;Property;_NoiseTiling;Noise Tiling;21;0;Create;True;0;0;0;False;0;False;0.25;0.05;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TimeNode;124;-3988.08,-2455.51;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;134;-4061.681,-2638.726;Float;False;Property;_NoiseDistortionScrollSpeed;Noise Distortion Scroll Speed;18;0;Create;True;0;0;0;False;0;False;0.05;0.04;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;107;-3865.778,-2001.611;Inherit;False;106;ParticlePositionUV;1;0;OBJECT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;45;-2055.711,-1049.219;Float;False;DistanceMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;180;-4037.458,2220.886;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;27;-3428.891,487.6396;Float;False;Constant;_Float0;Float 0;4;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;47;-3491.936,289.7124;Inherit;False;45;DistanceMask;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;182;-4309.458,1756.886;Float;False;Constant;_Float7;Float 4;28;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;183;-3861.458,2220.886;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;181;-4005.458,2412.886;Float;False;Property;_AudioMaskExp2;Audio Mask Exp;29;0;Create;True;0;0;0;False;0;False;1;1;0.1;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;119;-3581.271,-1988.717;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;26;-3238.78,427.2463;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;122;-4014.08,-2538.51;Float;False;Property;_Noise02ScrollSpeed;Noise 02 Scroll Speed;16;0;Create;True;0;0;0;False;0;False;0.15;0.12;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;133;-3582.353,-2614.693;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;121;-4012.08,-2307.51;Float;False;Property;_Noise01ScrollSpeed;Noise 01 Scroll Speed;15;0;Create;True;0;0;0;False;0;False;0.1;0.08;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;126;-3587.08,-2380.51;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;186;-3653.458,2252.886;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;125;-3586.08,-2505.51;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;31;-3010.98,308.6247;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;185;-4133.458,1788.886;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;131;-3242.619,-2882.083;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0.05,0.05;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;184;-3781.458,2508.886;Float;False;Property;_AudioMaskMultiply2;Audio Mask Multiply;30;0;Create;True;0;0;0;False;0;False;1;1;1;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;24;-2990.681,359.3308;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;132;-3037.619,-2899.083;Inherit;True;Property;_NoiseDistortion;Noise Distortion;17;0;Create;True;0;0;0;False;0;False;-1;None;19bfab0886d4ce348ba29f17a191277b;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.PannerNode;109;-2909.619,-2349.673;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;1,1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;188;-3957.458,1596.886;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;187;-3397.458,2380.886;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;110;-2906.136,-2089.58;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;1,1;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;129;-3014.997,-2687.215;Float;False;Property;_NoiseDistortionPower;Noise Distortion Power;19;0;Create;True;0;0;0;False;0;False;0.1;0.2;0;0.2;0;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;28;-2731.626,273.8232;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;189;-3653.458,2108.886;Float;False;Constant;_Float10;Float 8;29;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;190;-3781.458,1596.886;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;128;-2440.74,-2356.307;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ClampOpNode;191;-3253.458,2364.886;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;23;-2547.444,274.4107;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;34;-2749.334,444.881;Float;False;Property;_DistancePower;Distance Power;10;0;Create;True;0;0;0;False;0;False;1;1;0.2;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;127;-2444.489,-2599.995;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;194;-3621.458,1772.886;Inherit;False;Property;_AudioSpectrumPower2;Audio Spectrum Power;26;0;Create;True;0;0;0;False;0;False;1;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;193;-3429.458,2124.886;Float;False;Property;_AudioMaskEnabled2;Audio Mask Enabled;28;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;192;-3637.458,1564.886;Inherit;True;Property;_AudioSpectrum;Audio Spectrum;25;1;[HideInInspector];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;114;-1901.047,-2176.647;Float;False;Property;_NoisePower;Noise Power;20;0;Create;True;0;0;0;False;0;False;4;6;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;33;-2312.334,354.8807;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;98;-1922.118,-2380.805;Inherit;True;Property;_Noise01;Noise 01;13;0;Create;True;0;0;0;False;0;False;-1;None;e16f8e2dd5ea82044bade391afc45676;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;99;-1920.546,-2647.616;Inherit;True;Property;_Noise02;Noise 02;14;0;Create;True;0;0;0;False;0;False;-1;None;fe9b27216f3b18f499e61ce73ae8dad2;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;195;-3285.458,1516.886;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;53;-2122.962,348.3112;Float;False;ResultMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;105;-1462.533,-2492.596;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;54;-3005.466,753.4695;Inherit;False;53;ResultMask;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;111;-1273.442,-2496.126;Float;False;ResultNoise;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;197;-3013.458,1596.886;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;86;-3078.416,835.646;Float;False;Property;_FinalMaskMultiply;Final Mask Multiply;6;0;Create;True;0;0;0;False;0;False;2;3;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;196;-2789.458,1580.886;Float;False;Constant;_Float4;Float 2;26;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;156;-755.3256,332.5497;Inherit;False;Property;_AudioMaskAffectsAmplitude2;Audio Mask Affects Amplitude;31;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;118;-2847.183,1110.938;Inherit;False;111;ResultNoise;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;85;-2771.413,785.6462;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;198;-2613.458,1596.886;Float;False;Property;_AudioSpectrumEnabled2;Audio Spectrum Enabled;24;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;117;-2532.342,980.4859;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;157;-392.5369,390.2305;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;158;-756.0567,3.094849;Inherit;False;Constant;_Float9;Float 7;32;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;159;-193.7681,472.3416;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;88;-2297.238,775.8156;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;160;-755.0567,-69.90524;Inherit;False;Constant;_Float8;Float 6;32;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;161;-575.1207,-54.72275;Inherit;False;Property;_AudioAmplitudeEnabled2;Audio Amplitude Enabled;32;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;162;-232.9443,64.93274;Float;False;Property;_AudioAmplitudeOffsetPower2;Audio Amplitude Offset Power;34;0;Create;True;0;0;0;False;0;False;1;1;0;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;137;-284.2064,939.9142;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;90;-2130.609,775.3364;Float;False;ResultMaskModified;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;163;-59.53748,477.2304;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;164;-218.6464,-129.685;Float;False;Global;_AudioAverageAmplitude;_AudioAverageAmplitude;26;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;166;103.1589,48.35495;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;165;100.149,192.1474;Float;False;Constant;_Float6;Float 3;28;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;138;-56.71106,984.5818;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;92;-198.335,717.9164;Inherit;False;90;ResultMaskModified;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;43;46.69995,814.0266;Float;False;Property;_OffsetPower;Offset Power;11;0;Create;True;0;0;0;False;0;False;0;-1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.NormalizeNode;139;86.19614,984.4117;Inherit;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;167;298.0264,95.44534;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;89;59.82315,722.0725;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;4;-43.23003,-796.5675;Float;False;Property;_FinalPower;Final Power;4;0;Create;True;0;0;0;False;0;False;6;3;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;136;-817.7505,-1089.046;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;81;29.55398,-1088.316;Float;False;Property;_RampEnabled;Ramp Enabled;7;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;84;-1022.638,-873.7692;Float;False;Constant;_Float5;Float 5;16;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;83;-812.5833,-919.587;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode;14;-871.1545,-1259.316;Float;False;Property;_FinalColor;Final Color;1;0;Create;True;0;0;0;False;0;False;1,0,0,1;1,0,0,1;False;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;36;-876.8575,-1445.834;Float;False;Property;_FinalColor2;Final Color 2;2;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;False;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;148;-3006.642,-610.7631;Float;False;Property;_AffectorCount;Affector Count;3;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;37;-544.9852,-1361.165;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;135;-1155.75,-1041.046;Float;False;Property;_FinalExp;Final Exp;5;0;Create;True;0;0;0;False;0;False;2;2;0.2;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;147;-59.34668,-705.358;Inherit;True;Property;_FinalTexture;Final Texture;0;0;Create;True;0;0;0;False;0;False;-1;None;a8b8a3b14f2650542890347948aafe87;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;82;-437.946,-1036.915;Inherit;True;Property;_Ramp;Ramp;8;0;Create;True;0;0;0;False;0;False;-1;None;96f1785558043bf48bacf57b6511b602;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;441.4877,-984.6585;Inherit;False;5;5;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;42;365.1351,736.2018;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;171;-238.6174,-35.62854;Float;False;Property;_AudioAmplitudeEmissionPower2;Audio Amplitude Emission Power;33;0;Create;True;0;0;0;False;0;False;1;1;0;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;170;76.26575,-228.8945;Float;False;Constant;_Float3;Float 1;27;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;168;303.2668,-186.8945;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;169;101.3843,-91.62854;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;52;69.31149,-969.7088;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;91;-1132.582,-956.051;Inherit;False;90;ResultMaskModified;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;201;993.235,-582.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;199;993.235,-582.7628;Float;False;True;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;13;SineVFX/LivingParticles/LivingParticleFloorArrayURP;2992e84f91cbeb14eab234972e07ea9d;True;Forward;0;1;Forward;8;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;255;False;;255;False;;255;False;;7;False;;1;False;;1;False;;1;False;;7;False;;1;False;;1;False;;1;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;Hidden/InternalErrorShader;0;0;Standard;25;Surface;0;0;  Blend;0;0;Two Sided;1;0;Forward Only;0;0;Alpha Clipping;1;0;  Use Shadow Threshold;0;0;Cast Shadows;1;0;Receive Shadows;1;0;Motion Vectors;1;0;  Add Precomputed Velocity;0;0;GPU Instancing;1;0;LOD CrossFade;0;0;Built-in Fog;0;0;Meta Pass;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Vertex Position,InvertActionOnDeselection;1;0;0;11;False;True;True;True;False;False;True;True;True;False;True;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;200;993.235,-582.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;202;993.235,-582.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;203;993.235,-582.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;204;993.235,-542.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;205;993.235,-542.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;SceneSelectionPass;0;6;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;206;993.235,-542.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;ScenePickingPass;0;7;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;207;993.235,-542.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormals;0;8;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormalsOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;208;993.235,-542.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;DepthNormalsOnly;0;9;DepthNormalsOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormalsOnly;False;True;9;d3d11;metal;vulkan;xboxone;xboxseries;playstation;ps4;ps5;switch;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;209;993.235,-482.7628;Float;False;False;-1;2;UnityEditor.ShaderGraphUnlitGUI;0;1;New Amplify Shader;2992e84f91cbeb14eab234972e07ea9d;True;MotionVectors;0;10;MotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;False;False;False;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Unlit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;False;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=MotionVectors;False;False;0;;0;0;Standard;0;False;0
WireConnection;17;0;153;3
WireConnection;17;1;153;4
WireConnection;17;2;154;1
WireConnection;102;0;17;0
WireConnection;104;0;102;0
WireConnection;104;1;102;2
WireConnection;174;0;172;3
WireConnection;174;1;172;4
WireConnection;174;2;173;1
WireConnection;142;0;102;0
WireConnection;142;1;102;1
WireConnection;143;0;102;2
WireConnection;143;1;102;1
WireConnection;140;1;104;0
WireConnection;140;0;142;0
WireConnection;93;1;97;0
WireConnection;93;0;96;0
WireConnection;176;0;174;0
WireConnection;176;1;175;0
WireConnection;141;1;140;0
WireConnection;141;0;143;0
WireConnection;94;0;17;0
WireConnection;94;1;93;0
WireConnection;151;0;150;0
WireConnection;151;1;93;0
WireConnection;177;0;176;0
WireConnection;106;0;141;0
WireConnection;149;0;94;0
WireConnection;149;1;151;0
WireConnection;149;2;93;0
WireConnection;179;0;177;0
WireConnection;179;1;178;0
WireConnection;45;0;149;0
WireConnection;180;0;179;0
WireConnection;180;2;178;0
WireConnection;183;0;180;0
WireConnection;119;0;107;0
WireConnection;119;1;120;0
WireConnection;26;0;25;0
WireConnection;26;1;27;0
WireConnection;133;0;124;2
WireConnection;133;1;134;0
WireConnection;126;0;124;2
WireConnection;126;1;121;0
WireConnection;186;0;183;0
WireConnection;186;1;181;0
WireConnection;125;0;124;2
WireConnection;125;1;122;0
WireConnection;31;0;25;0
WireConnection;185;0;182;0
WireConnection;185;1;178;0
WireConnection;131;0;119;0
WireConnection;131;1;133;0
WireConnection;24;0;47;0
WireConnection;24;1;26;0
WireConnection;132;1;131;0
WireConnection;109;0;119;0
WireConnection;109;1;125;0
WireConnection;188;0;176;0
WireConnection;188;1;185;0
WireConnection;187;0;186;0
WireConnection;187;1;184;0
WireConnection;110;0;119;0
WireConnection;110;1;126;0
WireConnection;28;0;24;0
WireConnection;28;2;31;0
WireConnection;190;0;188;0
WireConnection;128;0;110;0
WireConnection;128;1;132;1
WireConnection;128;2;129;0
WireConnection;191;0;187;0
WireConnection;23;0;28;0
WireConnection;127;0;109;0
WireConnection;127;1;132;1
WireConnection;127;2;129;0
WireConnection;193;1;189;0
WireConnection;193;0;191;0
WireConnection;192;1;190;0
WireConnection;33;0;23;0
WireConnection;33;1;34;0
WireConnection;98;1;128;0
WireConnection;99;1;127;0
WireConnection;195;0;193;0
WireConnection;195;1;192;1
WireConnection;195;2;194;0
WireConnection;53;0;33;0
WireConnection;105;0;99;1
WireConnection;105;1;98;1
WireConnection;105;2;114;0
WireConnection;111;0;105;0
WireConnection;197;0;195;0
WireConnection;85;0;54;0
WireConnection;85;1;86;0
WireConnection;198;1;196;0
WireConnection;198;0;197;0
WireConnection;117;0;85;0
WireConnection;117;1;118;0
WireConnection;117;2;198;0
WireConnection;157;0;156;0
WireConnection;159;0;157;0
WireConnection;159;1;193;0
WireConnection;88;0;117;0
WireConnection;161;1;160;0
WireConnection;161;0;158;0
WireConnection;90;0;88;0
WireConnection;163;0;159;0
WireConnection;166;0;164;0
WireConnection;166;1;162;0
WireConnection;166;2;161;0
WireConnection;166;3;163;0
WireConnection;138;0;137;2
WireConnection;138;1;137;3
WireConnection;138;2;137;4
WireConnection;139;0;138;0
WireConnection;167;0;166;0
WireConnection;167;1;165;0
WireConnection;89;0;92;0
WireConnection;136;0;91;0
WireConnection;136;1;135;0
WireConnection;81;1;37;0
WireConnection;81;0;82;0
WireConnection;83;0;91;0
WireConnection;83;1;84;0
WireConnection;37;0;36;0
WireConnection;37;1;14;0
WireConnection;37;2;136;0
WireConnection;82;1;83;0
WireConnection;3;0;81;0
WireConnection;3;1;52;0
WireConnection;3;2;4;0
WireConnection;3;3;147;1
WireConnection;3;4;168;0
WireConnection;42;0;89;0
WireConnection;42;1;43;0
WireConnection;42;2;139;0
WireConnection;42;3;167;0
WireConnection;168;0;170;0
WireConnection;168;1;169;0
WireConnection;169;0;164;0
WireConnection;169;1;171;0
WireConnection;169;2;161;0
WireConnection;169;3;163;0
WireConnection;199;2;3;0
WireConnection;199;5;42;0
ASEEND*/
//CHKSM=45B0F08DF21728BB4F90FF0FE7E98EBE2F583607