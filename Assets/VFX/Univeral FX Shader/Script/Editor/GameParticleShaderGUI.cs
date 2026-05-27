using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;

namespace GameParticles.Editor
{
[CanEditMultipleObjects]
public partial class GameParticleShaderGUI : ShaderGUI
{
    // ================================
    // CACHE SYSTEM
    // ================================
    private Dictionary<string, MaterialProperty> _propertyCache = new Dictionary<string, MaterialProperty>();
    private bool _propertiesCached = false;
    private int _lastPropertyCount = 0;
    
    private static RenderSettingsConfig _config;
    private static bool _configLoaded = false;

    // ================================
    // ENUMS
    // ================================
    public enum RenderingMode { Transparent = 0, Additive = 1, SoftAdditive = 2, Blend = 3, Opaque = 4, Other = 5  }
    public enum ApplicationMode { Game3D = 0, Sprite2D = 1, UI = 2 }

    public enum ZTestMode { Disabled = 0, Never = 1, Less = 2, Equal = 3, LEqual = 4, Greater = 5, NotEqual = 6, GEqual = 7, Always = 8 }

    // ================================
    // MATERIAL STATE
    // ================================
    public class MaterialState
    {
        public int srcBlend, dstBlend, zWrite, zTest;
        public bool rimEnabled, rimLightEnabled, softParticlesEnabled, vertexOffsetEnabled;
        public float renderingMode, globalAlpha;
    }

    protected MaterialState _previousState;
    protected Material _lastSavedMaterial;

    // ================================
    // MATERIAL PROPERTIES
    // ================================
    // Shine Effect
    protected MaterialProperty _ShineMask, _ShineColor, _ShineIntensity, _ShineWidth, _ShineRotation,
                              _ShineWorldDirection, _ShineSpeed, _ShineWorldSpace, _ShineSharpnessLeft,
                              _ShineSharpnessRight, _ShineDelay;

    // Core Settings
    protected MaterialProperty _ApplicationMode, _RenderingMode, _CullMode, _ZWrite, _ZTestMode,
                              _Cutoff, _GlobalAlpha, _GrayscaleAlphaPower;

    // Main Texture & Color
    protected MaterialProperty _MainTex, _TintColor, _MainTexSpeedX, _MainTexSpeedY,
                              _MainTexBrightness, _MainTexContrast;

    // Dual Colors
    protected MaterialProperty _DualColorsEnabled, _ColorA, _ColorB, _ColorThreshold, _ColorSmoothness;

    // Mask System
    protected MaterialProperty _MaskTex, _MaskRotation, _MaskFlowTex, _MaskFlowSpeed,
                              _MaskFlowStrength, _MaskFlowSmoothness;
    protected MaterialProperty _MaskNoiseTex, _MaskNoiseIntensity, _MaskNoiseSpeed;

    // Dissolve Effect
    protected MaterialProperty _DissolveTex, _DissolveAmount, _DissolveSmoothness,
                              _DissolveOutlineStep, _DissolveOutlineColor;
    protected MaterialProperty _DissolveRemapMin, _DissolveRemapMax;

    // UV Noise
    protected MaterialProperty _UVNoise, _UVNoiseBias, _UVNoiseIntensity, _UVNoiseSpeed;

    protected MaterialProperty _DistortionTex, _DistortionTiling, _DistortionIntensity, _DistortionSpeed;

    // Glow Effect
    protected MaterialProperty _GlowTex, _GlowSpeedX, _GlowSpeedY, _GlowColor;
    protected MaterialProperty _GlowBlinkEnabled, _GlowBlinkMinAlpha, _GlowBlinkMaxAlpha, _GlowBlinkSpeed;

    // Rim Effects
    protected MaterialProperty _RimColor, _RimIntensity, _RimFresnel;
    protected MaterialProperty _RimLightColor, _RimLightIntensity, _RimLightFresnel;

    // Soft Particles & Vertex Offset
    // Soft Particles Properties
    protected MaterialProperty _SoftParticlesNearFadeDistance, _SoftParticlesFarFadeDistance;
    protected MaterialProperty _InvFade;
    protected MaterialProperty _OffsetNoiseTex, _OffsetAmount, _OffsetPower, _ScrollSpeedX, _ScrollSpeedY;

    // ================================
    // MAIN GUI
    // ================================
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        // Check if properties need refreshing
        bool materialsChanged = !_propertiesCached ||
                                editor.targets.Length != _lastPropertyCount ||
                                (editor.targets.Length > 0 && (_MainTex == null || !_propertyCache.ContainsKey("_MainTex")));

        if (materialsChanged || props == null || _ShineMask == null)
        {
            _propertiesCached = false;
            FindProperties(props);
        }

        var mats = editor.targets.Cast<Material>().ToArray();

        // Draw all sections in order
        if (_ApplicationMode != null && _RenderingMode != null)
            DrawRenderSettingsSection(editor, mats);

        DrawAlphaGrayscaleSection(editor, mats);
        DrawCutoutSection(editor, mats);

        if (_GlobalAlpha != null)
            editor.RangeProperty(_GlobalAlpha, "Global Alpha");

        DrawColorSettingsSection(editor, mats);
        DrawBrightnessContrastSection(editor, mats);
        DrawMainTextureSection(editor, mats);
        DrawMaskSection(editor, mats);
        DrawDissolveSection(editor, mats);
        DrawDistortionSection(editor, mats);
        DrawUVNoiseSection(editor, mats);
        DrawGlowSection(editor, mats);
        DrawRimSection(editor, mats);
        DrawRimLightSection(editor, mats);
        DrawVertexOffsetSection(editor, mats);
        DrawShineSection(editor, mats);
        DrawSoftParticlesSection(editor, mats);

        if (_ApplicationMode != null)
            DrawDepthSettingsSection(editor, mats);

        // Auto-manage UI_MODE keyword based on application mode
        foreach (Material mat in editor.targets)
        {
            if (_ApplicationMode != null && _ApplicationMode.floatValue == (float)ApplicationMode.UI)
                mat.EnableKeyword("UI_MODE");
            else
                mat.DisableKeyword("UI_MODE");
        }
    }

    protected void DrawHeader(string label)
    {
        EditorGUILayout.Space(7);
    }

    // ================================
    // PROPERTY CACHE MANAGEMENT
    // ================================
    protected virtual void FindProperties(MaterialProperty[] props)
    {
        if (_propertiesCached && _propertyCache.Count == _lastPropertyCount)
            return;

        _propertyCache.Clear();
        _lastPropertyCount = props?.Length ?? 0;

        if (props == null || props.Length == 0)
            return;

        // Cache all properties for quick access
        foreach (var prop in props)
            _propertyCache[prop.name] = prop;

        // Core Properties
        _ApplicationMode = GetCachedProperty("_ApplicationMode");
        _RenderingMode = GetCachedProperty("_RenderingMode");
        _CullMode = GetCachedProperty("_CullMode");
        _ZWrite = GetCachedProperty("_ZWrite");
        _ZTestMode = GetCachedProperty("_ZTestMode");
        _Cutoff = GetCachedProperty("_Cutoff");
        _GlobalAlpha = GetCachedProperty("_GlobalAlpha");
        _GrayscaleAlphaPower = GetCachedProperty("_GrayscaleAlphaPower");

        // Main Texture & Color Properties
        _MainTex = GetCachedProperty("_MainTex");
        _MainTexSpeedX = GetCachedProperty("_MainTexSpeedX");
        _MainTexSpeedY = GetCachedProperty("_MainTexSpeedY");
        _MainTexBrightness = GetCachedProperty("_MainTexBrightness");
        _MainTexContrast = GetCachedProperty("_MainTexContrast");
        _TintColor = GetCachedProperty("_TintColor");

        // Dual Colors Properties
        _DualColorsEnabled = GetCachedProperty("_DualColorsEnabled");
        _ColorA = GetCachedProperty("_ColorA");
        _ColorB = GetCachedProperty("_ColorB");
        _ColorThreshold = GetCachedProperty("_ColorThreshold");
        _ColorSmoothness = GetCachedProperty("_ColorSmoothness");

        // Mask System Properties
        _MaskTex = GetCachedProperty("_MaskTex");
        _MaskRotation = GetCachedProperty("_MaskRotation");
        _MaskFlowTex = GetCachedProperty("_MaskFlowTex");
        _MaskFlowSpeed = GetCachedProperty("_MaskFlowSpeed");
        _MaskFlowStrength = GetCachedProperty("_MaskFlowStrength");
        _MaskFlowSmoothness = GetCachedProperty("_MaskFlowSmoothness");
        _MaskNoiseTex = GetCachedProperty("_MaskNoiseTex");
        _MaskNoiseIntensity = GetCachedProperty("_MaskNoiseIntensity");
        _MaskNoiseSpeed = GetCachedProperty("_MaskNoiseSpeed");

        // Dissolve Properties
        _DissolveTex = GetCachedProperty("_DissolveTex");
        _DissolveAmount = GetCachedProperty("_DissolveAmount");
        _DissolveSmoothness = GetCachedProperty("_DissolveSmoothness");
        _DissolveOutlineStep = GetCachedProperty("_DissolveOutlineStep");
        _DissolveOutlineColor = GetCachedProperty("_DissolveOutlineColor");
        _DissolveRemapMin = GetCachedProperty("_DissolveRemapMin");
        _DissolveRemapMax = GetCachedProperty("_DissolveRemapMax");

        // UV Noise Properties
        _UVNoise = GetCachedProperty("_UVNoise");
        _UVNoiseBias = GetCachedProperty("_UVNoiseBias");
        _UVNoiseIntensity = GetCachedProperty("_UVNoiseIntensity");
        _UVNoiseSpeed = GetCachedProperty("_UVNoiseSpeed");

        // Glow Properties
        _GlowTex = GetCachedProperty("_GlowTex");
        _GlowSpeedX = GetCachedProperty("_GlowSpeedX");
        _GlowSpeedY = GetCachedProperty("_GlowSpeedY");
        _GlowColor = GetCachedProperty("_GlowColor");
        _GlowBlinkEnabled = GetCachedProperty("_GlowBlinkEnabled");
        _GlowBlinkMinAlpha = GetCachedProperty("_GlowBlinkMinAlpha");
        _GlowBlinkMaxAlpha = GetCachedProperty("_GlowBlinkMaxAlpha");
        _GlowBlinkSpeed = GetCachedProperty("_GlowBlinkSpeed");

        _DistortionTex = GetCachedProperty("_DistortionTex");
        _DistortionTiling = GetCachedProperty("_DistortionTiling");
        _DistortionIntensity = GetCachedProperty("_DistortionIntensity");
        _DistortionSpeed = GetCachedProperty("_DistortionSpeed");

        // Rim Effects Properties
        _RimColor = GetCachedProperty("_RimColor");
        _RimIntensity = GetCachedProperty("_RimIntensity");
        _RimFresnel = GetCachedProperty("_RimFresnel");
        _RimLightColor = GetCachedProperty("_RimLightColor");
        _RimLightIntensity = GetCachedProperty("_RimLightIntensity");
        _RimLightFresnel = GetCachedProperty("_RimLightFresnel");

        // Soft Particles & Vertex Offset Properties
        _InvFade = GetCachedProperty("_InvFade");
        _OffsetNoiseTex = GetCachedProperty("_OffsetNoiseTex");
        _OffsetAmount = GetCachedProperty("_OffsetAmount");
        _OffsetPower = GetCachedProperty("_OffsetPower");
        _ScrollSpeedX = GetCachedProperty("_ScrollSpeedX");
        _ScrollSpeedY = GetCachedProperty("_ScrollSpeedY");

        // Shine Properties
        _ShineMask = GetCachedProperty("_ShineMask");
        _ShineColor = GetCachedProperty("_ShineColor");
        _ShineIntensity = GetCachedProperty("_ShineIntensity");
        _ShineWidth = GetCachedProperty("_ShineWidth");
        _ShineRotation = GetCachedProperty("_ShineRotation");
        _ShineWorldDirection = GetCachedProperty("_ShineWorldDirection");
        _ShineWorldSpace = GetCachedProperty("_ShineWorldSpace"); 
        _ShineSpeed = GetCachedProperty("_ShineSpeed");
        _ShineSharpnessLeft = GetCachedProperty("_ShineSharpnessLeft");
        _ShineSharpnessRight = GetCachedProperty("_ShineSharpnessRight");
        _ShineDelay = GetCachedProperty("_ShineDelay");


        _propertiesCached = true;
    }

    private MaterialProperty GetCachedProperty(string name)
    {
        return _propertyCache.TryGetValue(name, out var prop) ? prop : null;
    }
}
}