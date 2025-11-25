// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "SineVFX/LivingParticles/LivingParticleMaskedMorphPbrArrayURP"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		_AffectorCount("Affector Count", Float) = 5
		[Toggle(_FLIPMORPHMASK_ON)] _FlipMorphMask("Flip Morph Mask", Float) = 0
		[Toggle(_FLIPEMISSIONMASK_ON)] _FlipEmissionMask("Flip Emission Mask", Float) = 0
		[Toggle(_FLIPOFFSETMASK_ON)] _FlipOffsetMask("Flip Offset Mask", Float) = 0
		_MorphMain("Morph Main", 2D) = "white" {}
		_MorphNormal("Morph Normal", 2D) = "white" {}
		_Albedo("Albedo", 2D) = "white" {}
		_ColorTint("Color Tint", Color) = (1,1,1,1)
		_MetallicSmoothness("MetallicSmoothness", 2D) = "white" {}
		_Metallic("Metallic", Range( 0 , 1)) = 0.5
		_Smoothness("Smoothness", Range( 0 , 1)) = 0.5
		_Normal("Normal", 2D) = "bump" {}
		_Emission("Emission", 2D) = "white" {}
		_FinalColorOne("Final Color One", Color) = (1,0,0,1)
		_FinalColorTwo("Final Color Two", Color) = (0,0,0,0)
		_FinalPower("Final Power", Range( 0 , 20)) = 6
		_FinalMaskMultiply("Final Mask Multiply", Range( 0 , 10)) = 2
		[Toggle(_RAMPENABLED_ON)] _RampEnabled("Ramp Enabled", Float) = 0
		_Ramp("Ramp", 2D) = "white" {}
		_Distance("Distance", Float) = 1
		_DistancePower("Distance Power", Range( 0.2 , 4)) = 1
		_DistanceRemap("Distance Remap", Range( 1 , 4)) = 1
		[Toggle(_OFFSETYLOCK_ON)] _OffsetYLock("Offset Y Lock", Float) = 0
		_OffsetPower("Offset Power", Float) = 0
		[Toggle(_CENTERMASKENABLED_ON)] _CenterMaskEnabled("Center Mask Enabled", Float) = 0
		_CenterMaskMultiply("Center Mask Multiply", Float) = 4
		_CenterMaskSubtract("Center Mask Subtract", Float) = 0.75
		[Toggle(_VERTEXDISTORTIONENABLED_ON)] _VertexDistortionEnabled("Vertex Distortion Enabled", Float) = 0
		_VertexOffsetTexture("Vertex Offset Texture", 2D) = "white" {}
		_VertexDistortionPower("Vertex Distortion Power", Float) = 0.1
		_VertexDistortionTiling("Vertex Distortion Tiling", Float) = 1
		[Toggle(_USEGAMMARENDERING_ON)] _UseGammaRendering("Use Gamma Rendering", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}


		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5
		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		[HideInInspector][ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1
		[HideInInspector][ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1
		[HideInInspector][ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}

		//[HideInInspector][ToggleUI] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 1
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }

		Cull Back
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
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

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile _ _FORWARD_PLUS

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
			#pragma multi_compile_fragment _ DEBUG_DISPLAY

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_FORWARD

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON
			#pragma shader_feature _RAMPENABLED_ON
			#pragma shader_feature _FLIPEMISSIONMASK_ON


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float4 lightmapUVOrVertexSH : TEXCOORD1;
				half4 fogFactorAndVertexLight : TEXCOORD2;
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					float4 shadowCoord : TEXCOORD6;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD7;
				#endif	
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD8;
				#endif
				float4 ase_texcoord9 : TEXCOORD9;
				float4 ase_texcoord10 : TEXCOORD10;
				float4 ase_texcoord11 : TEXCOORD11;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;
			sampler2D _Albedo;
			sampler2D _Normal;
			sampler2D _Ramp;
			sampler2D _Emission;
			sampler2D _MetallicSmoothness;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.texcoord1.w , input.texcoord2.x , input.texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.texcoord2.z , input.texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.texcoord1.x , ( input.texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				
				output.ase_texcoord9.xy = input.texcoord.xy;
				output.ase_texcoord10 = input.texcoord1;
				output.ase_texcoord11 = input.texcoord2;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord9.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = VertexNormalsFinal222;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				output.tSpace0 = float4( normalInput.normalWS, vertexInput.positionWS.x );
				output.tSpace1 = float4( normalInput.tangentWS, vertexInput.positionWS.y );
				output.tSpace2 = float4( normalInput.bitangentWS, vertexInput.positionWS.z );

				#if defined(LIGHTMAP_ON)
					OUTPUT_LIGHTMAP_UV( input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy );
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif

				OUTPUT_SH4( vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir( vertexInput.positionWS ), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion );

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					output.lightmapUVOrVertexSH.zw = input.texcoord.xy;
					output.lightmapUVOrVertexSH.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );

				#ifdef ASE_FOG
					half fogFactor = ComputeFogFactor( vertexInput.positionCS.z );
				#else
					half fogFactor = 0;
				#endif

				output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
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
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
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
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
				output.texcoord = input.texcoord;
				output.ase_texcoord3 = input.ase_texcoord3;
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
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
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
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float2 sampleCoords = (input.lightmapUVOrVertexSH.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					float3 WorldNormal = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					float3 WorldTangent = -cross(GetObjectToWorldMatrix()._13_23_33, WorldNormal);
					float3 WorldBiTangent = cross(WorldNormal, -WorldTangent);
				#else
					float3 WorldNormal = normalize( input.tSpace0.xyz );
					float3 WorldTangent = input.tSpace1.xyz;
					float3 WorldBiTangent = input.tSpace2.xyz;
				#endif

				float3 WorldPosition = float3(input.tSpace0.w,input.tSpace1.w,input.tSpace2.w);
				float3 WorldViewDirection = _WorldSpaceCameraPos.xyz  - WorldPosition;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				float2 NormalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = input.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#endif

				WorldViewDirection = SafeNormalize( WorldViewDirection );

				float2 uv_Albedo = input.ase_texcoord9.xy * _Albedo_ST.xy + _Albedo_ST.zw;
				
				float2 uv_Normal = input.ase_texcoord9.xy * _Normal_ST.xy + _Normal_ST.zw;
				
				float4 appendResult17 = (float4(input.ase_texcoord10.w , input.ase_texcoord11.x , input.ase_texcoord11.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPEMISSIONMASK_ON
				float staticSwitch223 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch223 = ResultMask53;
				#endif
				float clampResult88 = clamp( ( staticSwitch223 * _FinalMaskMultiply ) , 0.0 , 1.0 );
				float4 lerpResult37 = lerp( _FinalColorTwo , _FinalColorOne , clampResult88);
				float2 appendResult83 = (float2(clampResult88 , 0.0));
				#ifdef _RAMPENABLED_ON
				float4 staticSwitch81 = tex2D( _Ramp, appendResult83 );
				#else
				float4 staticSwitch81 = lerpResult37;
				#endif
				float2 uv_Emission = input.ase_texcoord9.xy * _Emission_ST.xy + _Emission_ST.zw;
				
				float2 uv_MetallicSmoothness = input.ase_texcoord9.xy * _MetallicSmoothness_ST.xy + _MetallicSmoothness_ST.zw;
				float4 tex2DNode90 = tex2D( _MetallicSmoothness, uv_MetallicSmoothness );
				

				float3 BaseColor = ( _ColorTint * tex2D( _Albedo, uv_Albedo ) ).rgb;
				float3 Normal = UnpackNormalScale( tex2D( _Normal, uv_Normal ), 1.0f );
				float3 Emission = ( staticSwitch81 * input.ase_color * _FinalPower * tex2D( _Emission, uv_Emission ).r * input.ase_color.a ).rgb;
				float3 Specular = 0.5;
				float Metallic = ( _Metallic * tex2DNode90.r );
				float Smoothness = ( tex2DNode90.a * _Smoothness );
				float Occlusion = 1;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _CLEARCOAT
					float CoatMask = 0;
					float CoatSmoothness = 0;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = WorldPosition;
				inputData.positionCS = input.positionCS;
				inputData.viewDirectionWS = WorldViewDirection;

				#ifdef _NORMALMAP
						#if _NORMAL_DROPOFF_TS
							inputData.normalWS = TransformTangentToWorld(Normal, half3x3(WorldTangent, WorldBiTangent, WorldNormal));
						#elif _NORMAL_DROPOFF_OS
							inputData.normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							inputData.normalWS = Normal;
						#endif
					inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				#else
					inputData.normalWS = WorldNormal;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					inputData.shadowCoord = ShadowCoords;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
				#else
					inputData.shadowCoord = float4(0, 0, 0, 0);
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = input.fogFactorAndVertexLight.x;
				#endif
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI( SH, GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask );
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				inputData.normalizedScreenSpaceUV = NormalizedScreenSpaceUV;

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
						#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				SurfaceData surfaceData;
				surfaceData.albedo              = BaseColor;
				surfaceData.metallic            = saturate(Metallic);
				surfaceData.specular            = Specular;
				surfaceData.smoothness          = saturate(Smoothness),
				surfaceData.occlusion           = Occlusion,
				surfaceData.emission            = Emission,
				surfaceData.alpha               = saturate(Alpha);
				surfaceData.normalTS            = Normal;
				surfaceData.clearCoatMask       = 0;
				surfaceData.clearCoatSmoothness = 1;

				#ifdef _CLEARCOAT
					surfaceData.clearCoatMask       = saturate(CoatMask);
					surfaceData.clearCoatSmoothness = saturate(CoatSmoothness);
				#endif

				#ifdef _DBUFFER
					ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
				#endif

				#ifdef _ASE_LIGHTING_SIMPLE
					half4 color = UniversalFragmentBlinnPhong( inputData, surfaceData);
				#else
					half4 color = UniversalFragmentPBR( inputData, surfaceData);
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = _TransmissionShadow;

					#define SUM_LIGHT_TRANSMISSION(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 transmission = max( 0, -dot( inputData.normalWS, Light.direction ) ) * atten * Transmission;\
						color.rgb += BaseColor * transmission;

					SUM_LIGHT_TRANSMISSION( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSMISSION( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSMISSION( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = _TransShadow;
					float normal = _TransNormal;
					float scattering = _TransScattering;
					float direct = _TransDirect;
					float ambient = _TransAmbient;
					float strength = _TransStrength;

					#define SUM_LIGHT_TRANSLUCENCY(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 lightDir = Light.direction + inputData.normalWS * normal;\
						half VdotL = pow( saturate( dot( inputData.viewDirectionWS, -lightDir ) ), scattering );\
						half3 translucency = atten * ( VdotL * direct + inputData.bakedGI * ambient ) * Translucency;\
						color.rgb += BaseColor * translucency * strength;

					SUM_LIGHT_TRANSLUCENCY( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							[loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSLUCENCY( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSLUCENCY( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_REFRACTION
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, float4( WorldNormal,0 ) ).xyz * ( 1.0 - dot( WorldNormal, WorldViewDirection ) );
					projScreenPos.xy += refractionOffset.xy;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos.xy ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3( 0, 0, 0 ), input.fogFactorAndVertexLight.x );
					#else
						color.rgb = MixFog(color.rgb, input.fogFactorAndVertexLight.x);
					#endif
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif

				return color;
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

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_SHADOWCASTER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
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
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			

			float3 _LightDirection;
			float3 _LightPosition;

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.ase_texcoord1.w , input.ase_texcoord2.x , input.ase_texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.ase_texcoord2.z , input.ase_texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.ase_texcoord1.x , ( input.ase_texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.ase_texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = positionWS;
				#endif

				float3 normalWS = TransformObjectToWorldDir(input.normalOS);

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
				output.clipPosV = positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;

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
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_texcoord2 = input.ase_texcoord2;
				output.ase_texcoord3 = input.ase_texcoord3;
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
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
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

			half4 frag(	PackedVaryings input
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_TARGET
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

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					#ifdef _ALPHATEST_SHADOW_ON
						clip(Alpha - AlphaClipThresholdShadow);
					#else
						clip(Alpha - AlphaClipThreshold);
					#endif
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
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

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
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

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
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
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.ase_texcoord1.w , input.ase_texcoord2.x , input.ase_texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.ase_texcoord2.z , input.ase_texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.ase_texcoord1.x , ( input.ase_texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.ase_texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;

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
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;

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
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_texcoord2 = input.ase_texcoord2;
				output.ase_texcoord3 = input.ase_texcoord3;
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
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
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

			half4 frag(	PackedVaryings input
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_TARGET
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

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003

			#pragma shader_feature EDITOR_VISUALIZATION

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_META

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON
			#pragma shader_feature _RAMPENABLED_ON
			#pragma shader_feature _FLIPEMISSIONMASK_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
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
				#ifdef EDITOR_VISUALIZATION
					float4 VizUV : TEXCOORD2;
					float4 LightCoord : TEXCOORD3;
				#endif
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;
			sampler2D _Albedo;
			sampler2D _Ramp;
			sampler2D _Emission;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.texcoord1.w , input.texcoord2.x , input.texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.texcoord2.z , input.texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.texcoord1.x , ( input.texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				
				output.ase_texcoord4.xy = input.texcoord0.xy;
				output.ase_texcoord5 = input.texcoord1;
				output.ase_texcoord6 = input.texcoord2;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord4.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = positionWS;
				#endif

				output.positionCS = MetaVertexPosition( input.positionOS, input.texcoord1.xy, input.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );

				#ifdef EDITOR_VISUALIZATION
					float2 VizUV = 0;
					float4 LightCoord = 0;
					UnityEditorVizData(input.positionOS.xyz, input.texcoord0.xy, input.texcoord1.xy, input.texcoord2.xy, VizUV, LightCoord);
					output.VizUV = float4(VizUV, 0, 0);
					output.LightCoord = LightCoord;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = output.positionCS;
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
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
				output.texcoord0 = input.texcoord0;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
				output.ase_texcoord3 = input.ase_texcoord3;
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
				output.texcoord0 = patch[0].texcoord0 * bary.x + patch[1].texcoord0 * bary.y + patch[2].texcoord0 * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
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

			half4 frag(PackedVaryings input  ) : SV_TARGET
			{
				UNITY_SETUP_INSTANCE_ID(input);
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

				float2 uv_Albedo = input.ase_texcoord4.xy * _Albedo_ST.xy + _Albedo_ST.zw;
				
				float4 appendResult17 = (float4(input.ase_texcoord5.w , input.ase_texcoord6.x , input.ase_texcoord6.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPEMISSIONMASK_ON
				float staticSwitch223 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch223 = ResultMask53;
				#endif
				float clampResult88 = clamp( ( staticSwitch223 * _FinalMaskMultiply ) , 0.0 , 1.0 );
				float4 lerpResult37 = lerp( _FinalColorTwo , _FinalColorOne , clampResult88);
				float2 appendResult83 = (float2(clampResult88 , 0.0));
				#ifdef _RAMPENABLED_ON
				float4 staticSwitch81 = tex2D( _Ramp, appendResult83 );
				#else
				float4 staticSwitch81 = lerpResult37;
				#endif
				float2 uv_Emission = input.ase_texcoord4.xy * _Emission_ST.xy + _Emission_ST.zw;
				

				float3 BaseColor = ( _ColorTint * tex2D( _Albedo, uv_Albedo ) ).rgb;
				float3 Emission = ( staticSwitch81 * input.ase_color * _FinalPower * tex2D( _Emission, uv_Emission ).r * input.ase_color.a ).rgb;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = BaseColor;
				metaInput.Emission = Emission;
				#ifdef EDITOR_VISUALIZATION
					metaInput.VizUV = input.VizUV.xy;
					metaInput.LightCoord = input.LightCoord;
				#endif

				return UnityMetaFragment(metaInput);
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_2D

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord : TEXCOORD0;
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
				float4 ase_texcoord2 : TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;
			sampler2D _Albedo;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_TRANSFER_INSTANCE_ID( input, output );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.ase_texcoord1.w , input.ase_texcoord2.x , input.ase_texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.ase_texcoord2.z , input.ase_texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.ase_texcoord1.x , ( input.ase_texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.ase_texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				
				output.ase_texcoord2.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord2.zw = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = vertexInput.positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord : TEXCOORD0;

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
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_texcoord2 = input.ase_texcoord2;
				output.ase_texcoord3 = input.ase_texcoord3;
				output.ase_texcoord = input.ase_texcoord;
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
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
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

				float2 uv_Albedo = input.ase_texcoord2.xy * _Albedo_ST.xy + _Albedo_ST.zw;
				

				float3 BaseColor = ( _ColorTint * tex2D( _Albedo, uv_Albedo ) ).rgb;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				half4 color = half4(BaseColor, Alpha );

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				return color;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormals" }

			ZWrite On
			Blend One Zero
			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
			//#define SHADERPASS SHADERPASS_DEPTHNORMALS

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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float3 worldNormal : TEXCOORD1;
				float4 worldTangent : TEXCOORD2;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD3;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD4;
				#endif
				float4 ase_texcoord5 : TEXCOORD5;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;
			sampler2D _Normal;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.ase_texcoord1.w , input.ase_texcoord2.x , input.ase_texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.ase_texcoord2.z , input.ase_texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.ase_texcoord1.x , ( input.ase_texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.ase_texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				
				output.ase_texcoord5.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord5.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				float3 normalWS = TransformObjectToWorldNormal( input.normalOS );
				float4 tangentWS = float4( TransformObjectToWorldDir( input.tangentOS.xyz ), input.tangentOS.w );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = vertexInput.positionWS;
				#endif

				output.worldNormal = normalWS;
				output.worldTangent = tangentWS;

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
				float4 tangentOS : TANGENT;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord : TEXCOORD0;

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
				output.tangentOS = input.tangentOS;
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_texcoord2 = input.ase_texcoord2;
				output.ase_texcoord3 = input.ase_texcoord3;
				output.ase_texcoord = input.ase_texcoord;
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
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
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

			void frag(	PackedVaryings input
						, out half4 outNormalWS : SV_Target0
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = input.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float3 WorldNormal = input.worldNormal;
				float4 WorldTangent = input.worldTangent;

				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 uv_Normal = input.ase_texcoord5.xy * _Normal_ST.xy + _Normal_ST.zw;
				

				float3 Normal = UnpackNormalScale( tex2D( _Normal, uv_Normal ), 1.0f );
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				#if defined(_GBUFFER_NORMALS_OCT)
					float2 octNormalWS = PackNormalOctQuadEncode(WorldNormal);
					float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
					half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
					outNormalWS = half4(packedNormalWS, 0.0);
				#else
					#if defined(_NORMALMAP)
						#if _NORMAL_DROPOFF_TS
							float crossSign = (WorldTangent.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
							float3 bitangent = crossSign * cross(WorldNormal.xyz, WorldTangent.xyz);
							float3 normalWS = TransformTangentToWorld(Normal, half3x3(WorldTangent.xyz, bitangent, WorldNormal.xyz));
						#elif _NORMAL_DROPOFF_OS
							float3 normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							float3 normalWS = Normal;
						#endif
					#else
						float3 normalWS = WorldNormal;
					#endif
					outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "GBuffer"
			Tags { "LightMode"="UniversalGBuffer" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile_fragment _ DEBUG_DISPLAY

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_GBUFFER

			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif
			
			#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_FRAG_COLOR
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON
			#pragma shader_feature _RAMPENABLED_ON
			#pragma shader_feature _FLIPEMISSIONMASK_ON


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float4 lightmapUVOrVertexSH : TEXCOORD1;
				half4 fogFactorAndVertexLight : TEXCOORD2;
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord : TEXCOORD6;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
				float2 dynamicLightmapUV : TEXCOORD7;
				#endif
				#if defined(USE_APV_PROBE_OCCLUSION)
					float4 probeOcclusion : TEXCOORD8;
				#endif
				float4 ase_texcoord9 : TEXCOORD9;
				float4 ase_texcoord10 : TEXCOORD10;
				float4 ase_texcoord11 : TEXCOORD11;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;
			sampler2D _Albedo;
			sampler2D _Normal;
			sampler2D _Ramp;
			sampler2D _Emission;
			sampler2D _MetallicSmoothness;


			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.texcoord1.w , input.texcoord2.x , input.texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.texcoord2.z , input.texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.texcoord1.x , ( input.texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				
				output.ase_texcoord9.xy = input.texcoord.xy;
				output.ase_texcoord10 = input.texcoord1;
				output.ase_texcoord11 = input.texcoord2;
				output.ase_color = input.ase_color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord9.zw = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				output.tSpace0 = float4( normalInput.normalWS, vertexInput.positionWS.x);
				output.tSpace1 = float4( normalInput.tangentWS, vertexInput.positionWS.y);
				output.tSpace2 = float4( normalInput.bitangentWS, vertexInput.positionWS.z);

				#if defined(LIGHTMAP_ON)
					OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif

				OUTPUT_SH4( vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir( vertexInput.positionWS ), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion );

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					output.lightmapUVOrVertexSH.zw = input.texcoord.xy;
					output.lightmapUVOrVertexSH.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );

				output.fogFactorAndVertexLight = half4(0, vertexLight);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
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
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
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
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
				output.texcoord = input.texcoord;
				output.ase_texcoord3 = input.ase_texcoord3;
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
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
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

			FragmentOutput frag ( PackedVaryings input
								#ifdef ASE_DEPTH_WRITE_ON
								,out float outputDepth : ASE_SV_DEPTH
								#endif
								 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float2 sampleCoords = (input.lightmapUVOrVertexSH.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					float3 WorldNormal = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					float3 WorldTangent = -cross(GetObjectToWorldMatrix()._13_23_33, WorldNormal);
					float3 WorldBiTangent = cross(WorldNormal, -WorldTangent);
				#else
					float3 WorldNormal = normalize( input.tSpace0.xyz );
					float3 WorldTangent = input.tSpace1.xyz;
					float3 WorldBiTangent = input.tSpace2.xyz;
				#endif

				float3 WorldPosition = float3(input.tSpace0.w,input.tSpace1.w,input.tSpace2.w);
				float3 WorldViewDirection = _WorldSpaceCameraPos.xyz  - WorldPosition;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				float2 NormalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = input.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#else
					ShadowCoords = float4(0, 0, 0, 0);
				#endif

				WorldViewDirection = SafeNormalize( WorldViewDirection );

				float2 uv_Albedo = input.ase_texcoord9.xy * _Albedo_ST.xy + _Albedo_ST.zw;
				
				float2 uv_Normal = input.ase_texcoord9.xy * _Normal_ST.xy + _Normal_ST.zw;
				
				float4 appendResult17 = (float4(input.ase_texcoord10.w , input.ase_texcoord11.x , input.ase_texcoord11.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPEMISSIONMASK_ON
				float staticSwitch223 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch223 = ResultMask53;
				#endif
				float clampResult88 = clamp( ( staticSwitch223 * _FinalMaskMultiply ) , 0.0 , 1.0 );
				float4 lerpResult37 = lerp( _FinalColorTwo , _FinalColorOne , clampResult88);
				float2 appendResult83 = (float2(clampResult88 , 0.0));
				#ifdef _RAMPENABLED_ON
				float4 staticSwitch81 = tex2D( _Ramp, appendResult83 );
				#else
				float4 staticSwitch81 = lerpResult37;
				#endif
				float2 uv_Emission = input.ase_texcoord9.xy * _Emission_ST.xy + _Emission_ST.zw;
				
				float2 uv_MetallicSmoothness = input.ase_texcoord9.xy * _MetallicSmoothness_ST.xy + _MetallicSmoothness_ST.zw;
				float4 tex2DNode90 = tex2D( _MetallicSmoothness, uv_MetallicSmoothness );
				

				float3 BaseColor = ( _ColorTint * tex2D( _Albedo, uv_Albedo ) ).rgb;
				float3 Normal = UnpackNormalScale( tex2D( _Normal, uv_Normal ), 1.0f );
				float3 Emission = ( staticSwitch81 * input.ase_color * _FinalPower * tex2D( _Emission, uv_Emission ).r * input.ase_color.a ).rgb;
				float3 Specular = 0.5;
				float Metallic = ( _Metallic * tex2DNode90.r );
				float Smoothness = ( tex2DNode90.a * _Smoothness );
				float Occlusion = 1;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = WorldPosition;
				inputData.positionCS = input.positionCS;
				inputData.shadowCoord = ShadowCoords;

				#ifdef _NORMALMAP
					#if _NORMAL_DROPOFF_TS
						inputData.normalWS = TransformTangentToWorld(Normal, half3x3( WorldTangent, WorldBiTangent, WorldNormal ));
					#elif _NORMAL_DROPOFF_OS
						inputData.normalWS = TransformObjectToWorldNormal(Normal);
					#elif _NORMAL_DROPOFF_WS
						inputData.normalWS = Normal;
					#endif
				#else
					inputData.normalWS = WorldNormal;
				#endif

				inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				inputData.viewDirectionWS = SafeNormalize( WorldViewDirection );

				inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
					inputData.bakedGI = SAMPLE_GI( SH, GetAbsolutePositionWS(inputData.positionWS),
						inputData.normalWS,
						inputData.viewDirectionWS,
						input.positionCS.xy,
						input.probeOcclusion,
						inputData.shadowMask );
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
					inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				inputData.normalizedScreenSpaceUV = NormalizedScreenSpaceUV;

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
						#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
					#if defined(USE_APV_PROBE_OCCLUSION)
						inputData.probeOcclusion = input.probeOcclusion;
					#endif
				#endif

				#ifdef _DBUFFER
					ApplyDecal(input.positionCS,
						BaseColor,
						Specular,
						inputData.normalWS,
						Metallic,
						Occlusion,
						Smoothness);
				#endif

				BRDFData brdfData;
				InitializeBRDFData
				(BaseColor, Metallic, Specular, Smoothness, Alpha, brdfData);

				Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
				half4 color;
				MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
				color.rgb = GlobalIllumination(brdfData, inputData.bakedGI, Occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
				color.a = Alpha;

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				return BRDFDataToGbuffer(brdfData, inputData, Smoothness, Emission + color.rgb, Occlusion);
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

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SCENESELECTIONPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
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

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.ase_texcoord1.w , input.ase_texcoord2.x , input.ase_texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.ase_texcoord2.z , input.ase_texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.ase_texcoord1.x , ( input.ase_texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.ase_texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );

				output.positionCS = TransformWorldToHClip(positionWS);

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;

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
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_texcoord2 = input.ase_texcoord2;
				output.ase_texcoord3 = input.ase_texcoord3;
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
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
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

				#ifdef SCENESELECTIONPASS
					outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				#elif defined(SCENEPICKINGPASS)
					outColor = _SelectionID;
				#endif

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

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

		    #define SCENEPICKINGPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#define ASE_NEEDS_VERT_NORMAL
			#pragma shader_feature _VERTEXDISTORTIONENABLED_ON
			#pragma shader_feature _CENTERMASKENABLED_ON
			#pragma shader_feature _FLIPOFFSETMASK_ON
			#pragma shader_feature _OFFSETYLOCK_ON
			#pragma shader_feature _USEGAMMARENDERING_ON
			#pragma shader_feature _FLIPMORPHMASK_ON


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

			sampler2D _VertexOffsetTexture;
			float4 _Affectors[20];
			sampler2D _MorphMain;
			sampler2D _MorphNormal;


			inline float4 TriplanarSampling111( sampler2D topTexMap, float3 worldPos, float3 worldNormal, float falloff, float2 tiling, float3 normalScale, float3 index )
			{
				float3 projNormal = ( pow( abs( worldNormal ), falloff ) );
				projNormal /= ( projNormal.x + projNormal.y + projNormal.z ) + 0.00001;
				float3 nsign = sign( worldNormal );
				half4 xNorm; half4 yNorm; half4 zNorm;
				xNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.zy * float2(  nsign.x, 1.0 ), 0, 0) );
				yNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xz * float2(  nsign.y, 1.0 ), 0, 0) );
				zNorm = tex2Dlod( topTexMap, float4(tiling * worldPos.xy * float2( -nsign.z, 1.0 ), 0, 0) );
				return xNorm * projNormal.x + yNorm * projNormal.y + zNorm * projNormal.z;
			}
			
			float CE1231( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float DistanceMaskMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w]);
				}else{
				DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) );	
				}
				}
				DistanceMaskMY = 1.0 - DistanceMaskMY;
				return DistanceMaskMY;
			}
			
			float4 CE2235( float4 ParticleCenterCE, float4 AffectorsCE )
			{
				float4 normalizeResultMY;
				for (int w = 0; w < _AffectorCount; w++) {
				if(w == 0){
				normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}else{
				normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 );
				}
				}
				return normalize(normalizeResultMY);
			}
			
			float3 RotateAroundAxis( float3 center, float3 original, float3 u, float angle )
			{
				original -= center;
				float C = cos( angle );
				float S = sin( angle );
				float t = 1 - C;
				float m00 = t * u.x * u.x + C;
				float m01 = t * u.x * u.y - S * u.z;
				float m02 = t * u.x * u.z + S * u.y;
				float m10 = t * u.x * u.y + S * u.z;
				float m11 = t * u.y * u.y + C;
				float m12 = t * u.y * u.z - S * u.x;
				float m20 = t * u.x * u.z - S * u.y;
				float m21 = t * u.y * u.z + S * u.x;
				float m22 = t * u.z * u.z + C;
				float3x3 finalMatrix = float3x3( m00, m01, m02, m10, m11, m12, m20, m21, m22 );
				return mul( finalMatrix, original ) + center;
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

				float3 temp_cast_0 = (0.0).xxx;
				float3 ase_worldPos = TransformObjectToWorld( (input.positionOS).xyz );
				float3 ase_worldNormal = TransformObjectToWorldNormal(input.normalOS);
				float4 triplanar111 = TriplanarSampling111( _VertexOffsetTexture, ase_worldPos, ase_worldNormal, 1.0, _VertexDistortionTiling, 1.0, 0 );
				float4 break114 = triplanar111;
				float3 appendResult115 = (float3(break114.x , break114.y , break114.z));
				#ifdef _VERTEXDISTORTIONENABLED_ON
				float3 staticSwitch120 = ( _VertexDistortionPower * (float3( -1,-1,-1 ) + (appendResult115 - float3( 0,0,0 )) * (float3( 1,1,1 ) - float3( -1,-1,-1 )) / (float3( 1,1,1 ) - float3( 0,0,0 ))) );
				#else
				float3 staticSwitch120 = temp_cast_0;
				#endif
				float4 appendResult17 = (float4(input.ase_texcoord1.w , input.ase_texcoord2.x , input.ase_texcoord2.y , 0.0));
				float4 ParticleCenterCE231 = appendResult17;
				float4 AffectorsCE231 = _Affectors[0];
				float localCE1231 = CE1231( ParticleCenterCE231 , AffectorsCE231 );
				float DistanceMask45 = localCE1231;
				float clampResult23 = clamp( (0.0 + (( DistanceMask45 + ( _Distance - 1.0 ) ) - 0.0) * (_DistanceRemap - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 );
				float ResultMask53 = pow( clampResult23 , _DistancePower );
				#ifdef _FLIPOFFSETMASK_ON
				float staticSwitch225 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch225 = ResultMask53;
				#endif
				float clampResult105 = clamp( ( staticSwitch225 - _CenterMaskSubtract ) , 0.0 , 1.0 );
				#ifdef _CENTERMASKENABLED_ON
				float staticSwitch109 = ( staticSwitch225 - ( clampResult105 * _CenterMaskMultiply ) );
				#else
				float staticSwitch109 = staticSwitch225;
				#endif
				float4 ParticleCenterCE235 = appendResult17;
				float4 AffectorsCE235 = _Affectors[0];
				float4 localCE2235 = CE2235( ParticleCenterCE235 , AffectorsCE235 );
				float4 CenterVector44 = localCE2235;
				float3 temp_cast_2 = (1.0).xxx;
				#ifdef _OFFSETYLOCK_ON
				float3 staticSwitch49 = float3(1,0,1);
				#else
				float3 staticSwitch49 = temp_cast_2;
				#endif
				float3 appendResult218 = (float3(input.ase_texcoord2.z , input.ase_texcoord2.w , input.ase_texcoord3.x));
				float3 RotationInRadian216 = -appendResult218;
				float3 break207 = RotationInRadian216;
				#ifdef _FLIPMORPHMASK_ON
				float staticSwitch202 = ( 1.0 - ResultMask53 );
				#else
				float staticSwitch202 = ResultMask53;
				#endif
				float2 appendResult177 = (float2(input.ase_texcoord1.x , ( input.ase_texcoord1.y + (-0.5 + (staticSwitch202 - 0.0) * (0.5 - -0.5) / (1.0 - 0.0)) )));
				float4 tex2DNode132 = tex2Dlod( _MorphMain, float4( appendResult177, 0, 0.0) );
				float3 gammaToLinear230 = FastSRGBToLinear( tex2DNode132.rgb );
				#ifdef _USEGAMMARENDERING_ON
				float4 staticSwitch229 = float4( gammaToLinear230 , 0.0 );
				#else
				float4 staticSwitch229 = tex2DNode132;
				#endif
				float4 break179 = staticSwitch229;
				float4 appendResult184 = (float4(( break179.r * -1.0 ) , ( break179.g * -1.0 ) , ( break179.b * 1.0 ) , ( break179.a * 1.0 )));
				float4 Morph186 = ( appendResult184 * input.ase_texcoord1.z );
				float3 rotatedValue208 = RotateAroundAxis( float3( 0,0,0 ), Morph186.xyz, float3( 0,0,-1 ), break207.z );
				float3 rotatedValue210 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue208, float3( -1,0,0 ), break207.x );
				float3 rotatedValue213 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue210, float3( 0,-1,0 ), break207.y );
				float4 OffsetFinal154 = ( ( float4( staticSwitch120 , 0.0 ) + ( staticSwitch109 * CenterVector44 * _OffsetPower * float4( staticSwitch49 , 0.0 ) ) ) + float4( rotatedValue213 , 0.0 ) );
				
				float3 break206 = RotationInRadian216;
				float4 break191 = tex2Dlod( _MorphNormal, float4( appendResult177, 0, 0.0) );
				float4 appendResult196 = (float4(( break191.r * 1.0 ) , ( break191.g * 1.0 ) , ( break191.b * 1.0 ) , ( break191.a * 1.0 )));
				float4 MorphNormals152 = (float4( 1,1,-1,-1 ) + (appendResult196 - float4( 0,0,0,0 )) * (float4( -1,-1,1,1 ) - float4( 1,1,-1,-1 )) / (float4( 1,1,1,1 ) - float4( 0,0,0,0 )));
				float3 rotatedValue209 = RotateAroundAxis( float3( 0,0,0 ), MorphNormals152.xyz, float3( 0,0,-1 ), break206.z );
				float3 rotatedValue211 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue209, float3( -1,0,0 ), break206.x );
				float3 rotatedValue212 = RotateAroundAxis( float3( 0,0,0 ), rotatedValue211, float3( 0,-1,0 ), break206.y );
				float3 VertexNormalsFinal222 = rotatedValue212;
				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = OffsetFinal154.xyz;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = VertexNormalsFinal222;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
				output.positionCS = TransformWorldToHClip(positionWS);

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 vertex : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;

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
				output.ase_texcoord1 = input.ase_texcoord1;
				output.ase_texcoord2 = input.ase_texcoord2;
				output.ase_texcoord3 = input.ase_texcoord3;
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
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				output.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				output.ase_texcoord3 = patch[0].ase_texcoord3 * bary.x + patch[1].ase_texcoord3 * bary.y + patch[2].ase_texcoord3 * bary.z;
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

				#ifdef SCENESELECTIONPASS
					outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				#elif defined(SCENEPICKINGPASS)
					outColor = _SelectionID;
				#endif

				return outColor;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "MotionVectors"
			Tags { "LightMode"="MotionVectors" }

			ColorMask RG

			HLSLPROGRAM

			#pragma multi_compile_local_fragment _ALPHATEST_ON
			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#pragma multi_compile _ LOD_FADE_CROSSFADE
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19701
			#define ASE_SRP_VERSION 170003


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif
	
            #define SHADERPASS SHADERPASS_MOTION_VECTORS

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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
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
			float4 _ColorTint;
			float4 _Emission_ST;
			float4 _FinalColorOne;
			float4 _FinalColorTwo;
			float4 _Normal_ST;
			float4 _Albedo_ST;
			float4 _MetallicSmoothness_ST;
			float _Metallic;
			float _FinalPower;
			float _FinalMaskMultiply;
			float _AffectorCount;
			float _CenterMaskMultiply;
			float _CenterMaskSubtract;
			float _DistancePower;
			float _DistanceRemap;
			float _Distance;
			float _VertexDistortionTiling;
			float _VertexDistortionPower;
			float _OffsetPower;
			float _Smoothness;
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

				#if defined(APLICATION_SPACE_WARP_MOTION)
					// We do not need jittered position in ASW
					output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));;
					output.positionCS = output.positionCSNoJitter;
				#else
					// Jittered. Match the frame.
					output.positionCS = vertexInput.positionCS;
					output.positionCSNoJitter = mul( _NonJitteredViewProjMatrix, mul( UNITY_MATRIX_M, input.positionOS));
				#endif

				float4 prevPos = ( unity_MotionVectorsParams.x == 1 ) ? float4( input.positionOld, 1 ) : input.positionOS;

				#if _ADD_PRECOMPUTED_VELOCITY
					prevPos = prevPos - float4(input.alembicMotionVector, 0);
				#endif

				output.previousPositionCSNoJitter = mul( _PrevViewProjMatrix, mul( UNITY_PREV_MATRIX_M, prevPos ) );
				// removed in ObjectMotionVectors.hlsl found in unity 6000.0.23 and higher
				//ApplyMotionVectorZBias( output.positionCS );
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

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(APLICATION_SPACE_WARP_MOTION)
					return float4( CalcAswNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 1 );
				#else
					return float4( CalcNdcMotionVectorFromCsPositions( input.positionCSNoJitter, input.previousPositionCSNoJitter ), 0, 0 );
				#endif
			}		
			ENDHLSL
		}
		
	}
	
	CustomEditor "UnityEditor.ShaderGraphLitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback "Hidden/InternalErrorShader"
}
/*ASEBEGIN
Version=19701
Node;AmplifyShaderEditor.TexCoordVertexDataNode;174;-3176.217,-1688.963;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;175;-3175.581,-1518.185;Inherit;False;2;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GlobalArrayNode;234;-2935.444,-1318.834;Inherit;False;_Affectors;0;20;2;False;False;0;1;False;Object;-1;4;0;INT;0;False;2;INT;0;False;1;INT;0;False;3;INT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode;17;-2857.391,-1571.479;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CustomExpressionNode;231;-2383.148,-1195.862;Float;False;float DistanceMaskMY@$$for (int w = 0@ w < _AffectorCount@ w++) {$if(w == 0){$DistanceMaskMY = distance(ParticleCenterCE, _Affectors[w])@$}else{$DistanceMaskMY = min( DistanceMaskMY, distance(ParticleCenterCE, _Affectors[w]) )@	$}$}$DistanceMaskMY = 1.0 - DistanceMaskMY@$return DistanceMaskMY@;1;Create;2;True;ParticleCenterCE;FLOAT4;0,0,0,0;In;;Float;False;True;AffectorsCE;FLOAT4;0,0,0,0;In;;Float;False;CE1;True;False;0;;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;27;-3646.39,25.94802;Float;False;Constant;_Float0;Float 0;4;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;25;-3646.945,-76.84381;Float;False;Property;_Distance;Distance;19;0;Create;True;0;0;0;False;0;False;1;3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;45;-1842.31,-1417.22;Float;False;DistanceMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;26;-3456.28,-34.4452;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;47;-3709.436,-171.9792;Inherit;False;45;DistanceMask;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;227;-3401.574,-299.4631;Float;False;Property;_DistanceRemap;Distance Remap;21;0;Create;True;0;0;0;False;0;False;1;1;1;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;31;-3228.48,-153.0669;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;24;-3208.18,-102.3606;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;28;-2949.126,-187.8686;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;0;False;4;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;34;-2966.835,-16.81061;Float;False;Property;_DistancePower;Distance Power;20;0;Create;True;0;0;0;False;0;False;1;1;0.2;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;23;-2764.943,-187.2811;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;33;-2529.836,-106.8108;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;53;-2343.614,-113.7636;Float;False;ResultMask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;200;-3366.347,1857.43;Inherit;False;53;ResultMask;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;203;-3180.155,2059.244;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;202;-3005.155,1959.243;Float;False;Property;_FlipMorphMask;Flip Morph Mask;1;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TFHCRemapNode;187;-2707.247,1962.092;Inherit;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;-0.5;False;4;FLOAT;0.5;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;189;-2697.35,1611.004;Inherit;False;1;2;0;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;176;-2474.264,1856.568;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;177;-2280.953,1743.604;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SamplerNode;132;-1947.373,1486.319;Inherit;True;Property;_MorphMain;Morph Main;4;0;Create;True;0;0;0;False;0;False;-1;None;0375bff8996bd734e96bd48295092eeb;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GammaToLinearNode;230;-2028.139,1064.319;Inherit;False;0;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch;229;-1797.834,1032.073;Float;False;Property;_UseGammaRendering;Use Gamma Rendering;31;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;55;-1046.615,398.8686;Inherit;False;53;ResultMask;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;220;-2960.051,528.9196;Inherit;False;3;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;226;-829.6511,528.6901;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;179;-1603.782,1491.728;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.SamplerNode;133;-1928.339,1978.218;Inherit;True;Property;_MorphNormal;Morph Normal;5;0;Create;True;0;0;0;False;0;False;-1;None;9415ef6bad22e604bae687c3adc0ee3f;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.TexCoordVertexDataNode;219;-2961.073,252.4816;Inherit;False;2;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;180;-1256.783,1482.728;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;-1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;122;-948.1693,174.6511;Float;False;Property;_VertexDistortionTiling;Vertex Distortion Tiling;30;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;113;-927.8683,-18.3929;Float;True;Property;_VertexOffsetTexture;Vertex Offset Texture;28;0;Create;True;0;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;181;-1253.783,1583.728;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;182;-1251.782,1688.729;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;103;-535.1007,624.9363;Float;False;Property;_CenterMaskSubtract;Center Mask Subtract;26;0;Create;True;0;0;0;False;0;False;0.75;0.85;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;183;-1260.783,1389.728;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;-1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;218;-2674.05,399.9194;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch;225;-635.4039,395.0158;Float;False;Property;_FlipOffsetMask;Flip Offset Mask;3;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;191;-1579.565,1989.366;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.DynamicAppendNode;184;-991.3842,1501.629;Inherit;True;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TexCoordVertexDataNode;199;-983.8941,1728.685;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;101;-284.7973,531.0767;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;195;-1264.679,2127.093;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;194;-1271.831,2026.093;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TriplanarNode;111;-637.6868,55.03267;Inherit;True;Spherical;World;False;Top Texture 0;_TopTexture0;white;0;None;Mid Texture 0;_MidTexture0;white;-1;None;Bot Texture 0;_BotTexture0;white;-1;None;Triplanar Sampler;Tangent;10;0;SAMPLER2D;;False;5;FLOAT;1;False;1;SAMPLER2D;;False;6;FLOAT;0;False;2;SAMPLER2D;;False;7;FLOAT;0;False;9;FLOAT3;0,0,0;False;8;FLOAT;1;False;3;FLOAT;1;False;4;FLOAT;1;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.NegateNode;217;-2517.39,398.2504;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;192;-1268.178,1933.093;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;193;-1263.429,2226.967;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;216;-2370.564,393.3731;Float;False;RotationInRadian;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;114;-277.9813,55.32092;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.ClampOpNode;105;-139.673,529.8535;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;196;-1020.052,2037.466;Inherit;True;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;185;-631.4807,1623.868;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;107;-230.0661,656.0699;Float;False;Property;_CenterMaskMultiply;Center Mask Multiply;25;0;Create;True;0;0;0;False;0;False;4;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;186;-472.9491,1621.167;Float;False;Morph;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;106;56.45676,575.2776;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;235;-2387.384,-1788.9;Float;False;float4 normalizeResultMY@$$for (int w = 0@ w < _AffectorCount@ w++) {$if(w == 0){$normalizeResultMY = normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 )@$}else{$normalizeResultMY = normalizeResultMY + normalize( ParticleCenterCE - _Affectors[w] ) * ( clamp( (0.0 + (( 1.0 - distance( ParticleCenterCE , _Affectors[w] ) + ( _Distance - 1.0 ) ) - 0.0) * (1.0 - 0.0) / (_Distance - 0.0)) , 0.0 , 1.0 ) + 0.00001 )@$}$}$return normalize(normalizeResultMY)@;4;Create;2;True;ParticleCenterCE;FLOAT4;0,0,0,0;In;;Float;False;True;AffectorsCE;FLOAT4;0,0,0,0;In;;Float;False;CE2;True;False;0;;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TFHCRemapNode;197;-763.6547,2035.365;Inherit;False;5;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT4;1,1,1,1;False;3;FLOAT4;1,1,-1,-1;False;4;FLOAT4;-1,-1,1,1;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;205;440.1099,1470.168;Inherit;False;216;RotationInRadian;1;0;OBJECT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode;115;25.90511,52.74559;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;108;249.4375,451.1259;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;152;-558.2183,2031.605;Float;False;MorphNormals;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;214;759.9273,1599.658;Inherit;False;186;Morph;1;0;OBJECT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector3Node;51;302.2741,764.8003;Float;False;Constant;_Vector0;Vector 0;8;0;Create;True;0;0;0;False;0;False;1,0,1;0,0,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.BreakToComponentsNode;207;701.7459,1473.542;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.TFHCRemapNode;119;215.9899,48.42035;Inherit;False;5;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;1,1,1;False;3;FLOAT3;-1,-1,-1;False;4;FLOAT3;1,1,1;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;50;330.274,920.8004;Float;False;Constant;_Float1;Float 1;8;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;44;-1843.529,-1571.902;Float;False;CenterVector;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;118;125.9526,-48.14551;Float;False;Property;_VertexDistortionPower;Vertex Distortion Power;29;0;Create;True;0;0;0;False;0;False;0.1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;204;434.9919,2093.339;Inherit;False;216;RotationInRadian;1;0;OBJECT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StaticSwitch;109;505.4623,524.6801;Float;False;Property;_CenterMaskEnabled;Center Mask Enabled;24;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;117;511.1227,42.56478;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;215;735.9254,2223.337;Inherit;False;152;MorphNormals;1;0;OBJECT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;43;586.7905,730.4666;Float;False;Property;_OffsetPower;Offset Power;23;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RotateAboutAxisNode;208;1080.517,1622.996;Inherit;False;False;4;0;FLOAT3;0,0,-1;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;46;548.5768,645.7773;Inherit;False;44;CenterVector;1;0;OBJECT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;121;512.0356,157.9077;Float;False;Constant;_Float2;Float 2;24;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;49;566.657,823.687;Float;False;Property;_OffsetYLock;Offset Y Lock;22;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.BreakToComponentsNode;206;696.6832,2099.552;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RotateAboutAxisNode;210;1087.143,1312.103;Inherit;False;False;4;0;FLOAT3;-1,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RotateAboutAxisNode;209;1096.626,2230.124;Inherit;False;False;4;0;FLOAT3;0,0,-1;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;42;916.3195,638.3792;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT;0;False;3;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.StaticSwitch;120;744.2634,78.4812;Float;False;Property;_VertexDistortionEnabled;Vertex Distortion Enabled;27;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RotateAboutAxisNode;213;1091.009,1459.852;Inherit;False;False;4;0;FLOAT3;0,-1,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;116;1370.645,339.3983;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RotateAboutAxisNode;211;1096.92,1926.641;Inherit;False;False;4;0;FLOAT3;-1,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;150;1752.162,919.6628;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RotateAboutAxisNode;212;1096.626,2072.916;Inherit;False;False;4;0;FLOAT3;0,-1,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ColorNode;100;1435.369,-1678.405;Float;False;Property;_ColorTint;Color Tint;7;0;Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,1;False;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;89;1209.255,-1494.071;Inherit;True;Property;_Albedo;Albedo;6;0;Create;True;0;0;0;False;0;False;-1;None;20d714081e9fac74e99ad3c851a18b66;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RegisterLocalVarNode;154;1913.964,917.308;Float;False;OffsetFinal;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;222;1464.209,2073.557;Float;False;VertexNormalsFinal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ClampOpNode;88;-614.2828,-883.5568;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;37;-371.767,-1167.51;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;82;-264.7276,-843.2589;Inherit;True;Property;_Ramp;Ramp;18;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;14;-697.9361,-1065.661;Float;False;Property;_FinalColorOne;Final Color One;13;0;Create;True;0;0;0;False;0;False;1,0,0,1;1,0.517647,0,1;False;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.DynamicAppendNode;83;-425.3648,-819.9307;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ColorNode;36;-703.6391,-1252.178;Float;False;Property;_FinalColorTwo;Final Color Two;14;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;False;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;85;-771.1036,-879.9979;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;84;-621.119,-752.5731;Float;False;Constant;_Float5;Float 5;16;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;81;202.7726,-894.6599;Float;False;Property;_RampEnabled;Ramp Enabled;17;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;COLOR;0,0,0,0;False;0;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;5;COLOR;0,0,0,0;False;6;COLOR;0,0,0,0;False;7;COLOR;0,0,0,0;False;8;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StaticSwitch;223;-1057.479,-1005.835;Float;False;Property;_FlipEmissionMask;Flip Emission Mask;2;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT;0;False;0;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;86;-1078.105,-829.9976;Float;False;Property;_FinalMaskMultiply;Final Mask Multiply;16;0;Create;True;0;0;0;False;0;False;2;1;0;10;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;54;-1461.157,-1010.175;Inherit;False;53;ResultMask;1;0;OBJECT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;224;-1232.336,-923.7797;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;173;134.2945,-512.4421;Inherit;True;Property;_Emission;Emission;12;0;Create;True;0;0;0;False;0;False;-1;None;7c2971a298af5674ea309ad2022930e8;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;99;1738.369,-1584.404;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode;90;663.0696,-445.9392;Inherit;True;Property;_MetallicSmoothness;MetallicSmoothness;8;0;Create;True;0;0;0;False;0;False;-1;None;c96026a5450b02c4b90d63fd9238e59f;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;91;1299.092,-1279.551;Inherit;True;Property;_Normal;Normal;11;0;Create;True;0;0;0;False;0;False;-1;None;c1b2383bc664c7c46bfbac4fe51e810b;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;96;847.4246,-533.884;Float;False;Property;_Metallic;Metallic;9;0;Create;True;0;0;0;False;0;False;0.5;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;52;233.7712,-787.0007;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;155;1618.419,-601.6031;Inherit;False;154;OffsetFinal;1;0;OBJECT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RangedFloatNode;98;835.4246,-234.8844;Float;False;Property;_Smoothness;Smoothness;10;0;Create;True;0;0;0;False;0;False;0.5;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;232;-2897.552,-1405.328;Float;False;Property;_AffectorCount;Affector Count;0;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;4;146.9107,-601.3729;Float;False;Property;_FinalPower;Final Power;15;0;Create;True;0;0;0;False;0;False;6;12;0;20;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;153;1562.148,-504.5751;Inherit;False;222;VertexNormalsFinal;1;0;OBJECT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;97;1185.497,-294.6173;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;95;1188.15,-466.882;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;614.7059,-791.0023;Inherit;False;5;5;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;242;1942.617,-985.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;243;1942.617,-985.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;240;1942.617,-985.7353;Float;False;True;-1;2;UnityEditor.ShaderGraphLitGUI;0;12;SineVFX/LivingParticles/LivingParticleMaskedMorphPbrArrayURP;94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;255;False;;255;False;;255;False;;7;False;;1;False;;1;False;;1;False;;7;False;;1;False;;1;False;;1;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;Hidden/InternalErrorShader;0;0;Standard;45;Lighting Model;0;0;Workflow;1;0;Surface;0;0;  Refraction Model;0;0;  Blend;0;0;Two Sided;1;0;Alpha Clipping;1;0;  Use Shadow Threshold;0;0;Fragment Normal Space,InvertActionOnDeselection;0;0;Forward Only;0;0;Transmission;0;0;  Transmission Shadow;0.5,False,;0;Translucency;0;0;  Translucency Strength;1,False,;0;  Normal Distortion;0.5,False,;0;  Scattering;2,False,;0;  Direct;0.9,False,;0;  Ambient;0.1,False,;0;  Shadow;0.5,False,;0;Cast Shadows;1;0;Receive Shadows;1;0;Receive SSAO;1;0;Motion Vectors;1;0;  Add Precomputed Velocity;0;0;GPU Instancing;1;0;LOD CrossFade;1;0;Built-in Fog;1;0;_FinalColorxAlpha;0;0;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Write Depth;0;0;  Early Z;0;0;Vertex Position,InvertActionOnDeselection;1;0;Debug Display;0;0;Clear Coat;0;0;0;11;False;True;True;True;True;True;True;True;True;True;True;False;;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;241;1942.617,-985.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;244;1942.617,-985.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;Hidden/InternalErrorShader;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;245;1942.617,-985.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;246;1942.617,-935.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormals;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;247;1942.617,-935.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;255;False;;255;False;;255;False;;7;False;;1;False;;1;False;;1;False;;7;False;;1;False;;1;False;;1;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;248;1942.617,-935.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;249;1942.617,-935.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;250;1942.617,-885.7353;Float;False;False;-1;2;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;MotionVectors;0;10;MotionVectors;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;False;False;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=MotionVectors;False;False;0;;0;0;Standard;0;False;0
WireConnection;17;0;174;4
WireConnection;17;1;175;1
WireConnection;17;2;175;2
WireConnection;231;0;17;0
WireConnection;231;1;234;0
WireConnection;45;0;231;0
WireConnection;26;0;25;0
WireConnection;26;1;27;0
WireConnection;31;0;25;0
WireConnection;24;0;47;0
WireConnection;24;1;26;0
WireConnection;28;0;24;0
WireConnection;28;2;31;0
WireConnection;28;4;227;0
WireConnection;23;0;28;0
WireConnection;33;0;23;0
WireConnection;33;1;34;0
WireConnection;53;0;33;0
WireConnection;203;0;200;0
WireConnection;202;1;200;0
WireConnection;202;0;203;0
WireConnection;187;0;202;0
WireConnection;176;0;189;2
WireConnection;176;1;187;0
WireConnection;177;0;189;1
WireConnection;177;1;176;0
WireConnection;132;1;177;0
WireConnection;230;0;132;0
WireConnection;229;1;132;0
WireConnection;229;0;230;0
WireConnection;226;0;55;0
WireConnection;179;0;229;0
WireConnection;133;1;177;0
WireConnection;180;0;179;1
WireConnection;181;0;179;2
WireConnection;182;0;179;3
WireConnection;183;0;179;0
WireConnection;218;0;219;3
WireConnection;218;1;219;4
WireConnection;218;2;220;1
WireConnection;225;1;55;0
WireConnection;225;0;226;0
WireConnection;191;0;133;0
WireConnection;184;0;183;0
WireConnection;184;1;180;0
WireConnection;184;2;181;0
WireConnection;184;3;182;0
WireConnection;101;0;225;0
WireConnection;101;1;103;0
WireConnection;195;0;191;2
WireConnection;194;0;191;1
WireConnection;111;0;113;0
WireConnection;111;3;122;0
WireConnection;217;0;218;0
WireConnection;192;0;191;0
WireConnection;193;0;191;3
WireConnection;216;0;217;0
WireConnection;114;0;111;0
WireConnection;105;0;101;0
WireConnection;196;0;192;0
WireConnection;196;1;194;0
WireConnection;196;2;195;0
WireConnection;196;3;193;0
WireConnection;185;0;184;0
WireConnection;185;1;199;3
WireConnection;186;0;185;0
WireConnection;106;0;105;0
WireConnection;106;1;107;0
WireConnection;235;0;17;0
WireConnection;235;1;234;0
WireConnection;197;0;196;0
WireConnection;115;0;114;0
WireConnection;115;1;114;1
WireConnection;115;2;114;2
WireConnection;108;0;225;0
WireConnection;108;1;106;0
WireConnection;152;0;197;0
WireConnection;207;0;205;0
WireConnection;119;0;115;0
WireConnection;44;0;235;0
WireConnection;109;1;225;0
WireConnection;109;0;108;0
WireConnection;117;0;118;0
WireConnection;117;1;119;0
WireConnection;208;1;207;2
WireConnection;208;3;214;0
WireConnection;49;1;50;0
WireConnection;49;0;51;0
WireConnection;206;0;204;0
WireConnection;210;1;207;0
WireConnection;210;3;208;0
WireConnection;209;1;206;2
WireConnection;209;3;215;0
WireConnection;42;0;109;0
WireConnection;42;1;46;0
WireConnection;42;2;43;0
WireConnection;42;3;49;0
WireConnection;120;1;121;0
WireConnection;120;0;117;0
WireConnection;213;1;207;1
WireConnection;213;3;210;0
WireConnection;116;0;120;0
WireConnection;116;1;42;0
WireConnection;211;1;206;0
WireConnection;211;3;209;0
WireConnection;150;0;116;0
WireConnection;150;1;213;0
WireConnection;212;1;206;1
WireConnection;212;3;211;0
WireConnection;154;0;150;0
WireConnection;222;0;212;0
WireConnection;88;0;85;0
WireConnection;37;0;36;0
WireConnection;37;1;14;0
WireConnection;37;2;88;0
WireConnection;82;1;83;0
WireConnection;83;0;88;0
WireConnection;83;1;84;0
WireConnection;85;0;223;0
WireConnection;85;1;86;0
WireConnection;81;1;37;0
WireConnection;81;0;82;0
WireConnection;223;1;54;0
WireConnection;223;0;224;0
WireConnection;224;0;54;0
WireConnection;99;0;100;0
WireConnection;99;1;89;0
WireConnection;97;0;90;4
WireConnection;97;1;98;0
WireConnection;95;0;96;0
WireConnection;95;1;90;1
WireConnection;3;0;81;0
WireConnection;3;1;52;0
WireConnection;3;2;4;0
WireConnection;3;3;173;1
WireConnection;3;4;52;4
WireConnection;240;0;99;0
WireConnection;240;1;91;0
WireConnection;240;2;3;0
WireConnection;240;3;95;0
WireConnection;240;4;97;0
WireConnection;240;8;155;0
WireConnection;240;10;153;0
ASEEND*/
//CHKSM=B1430F4FD4A3F0564EC7BA78A5B3D77A7B6E1BBE