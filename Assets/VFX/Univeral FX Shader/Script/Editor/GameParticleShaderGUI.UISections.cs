using System;
using UnityEditor;
using UnityEngine;
using GameParticles.Editor;

namespace GameParticles.Editor
{
public partial class GameParticleShaderGUI
{
    // === UI UTILITIES ===
    protected void SetKeyword(Material mat, string keyword, bool state)
    {
        if (state) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    protected bool IsKeywordEnabled(Material[] mats, string keyword)
    {
        foreach (var mat in mats)
            if (mat.IsKeywordEnabled(keyword))
                return true;
        return false;
    }

    protected bool DrawKeywordToggle(Material[] mats, string label, string keyword)
    {
        bool enabled = IsKeywordEnabled(mats, keyword);
        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.ToggleLeft(label, enabled);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var mat in mats)
                SetKeyword(mat, keyword, enabled);
        }
        return enabled;
    }

    protected void DrawKeywordSection(Material[] mats, string label, string keyword, Action content)
    {
        bool enabled = IsKeywordEnabled(mats, keyword);
        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.ToggleLeft(label, enabled, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var mat in mats)
                SetKeyword(mat, keyword, enabled);
        }

        if (enabled && content != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;
            content.Invoke();
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }

    // === SECTION DRAWERS ===
    protected void DrawRenderSettingsSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Render Settings");
        DrawRenderSettings(editor, mats);
    }

    protected void DrawAlphaGrayscaleSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Alpha From Grayscale");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        bool grayscaleEnabled = IsKeywordEnabled(mats, "ALPHA_FROM_GRAYSCALE");
        EditorGUI.BeginChangeCheck();
        grayscaleEnabled = EditorGUILayout.ToggleLeft("Alpha From Grayscale", grayscaleEnabled, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var mat in mats)
                SetKeyword(mat, "ALPHA_FROM_GRAYSCALE", grayscaleEnabled);
        }

        if (grayscaleEnabled && _GrayscaleAlphaPower != null)
        {
            EditorGUI.indentLevel++;
            editor.RangeProperty(_GrayscaleAlphaPower, "Grayscale Alpha Power");
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.HelpBox("Uses Main Texture brightness as alpha transparency.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    protected void DrawCutoutSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Cutout");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        bool cutoutEnabled = IsKeywordEnabled(mats, "_ALPHA_TEST");
        EditorGUI.BeginChangeCheck();
        cutoutEnabled = EditorGUILayout.ToggleLeft("Enable Cutout", cutoutEnabled, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var mat in mats)
            {
                SetKeyword(mat, "_ALPHA_TEST", cutoutEnabled);
                if (mat.HasProperty("_Cutoff"))
                    mat.SetFloat("_Cutoff", cutoutEnabled ? Mathf.Max(mat.GetFloat("_Cutoff"), 0.5f) : 0f);
                EditorUtility.SetDirty(mat);
            }
        }

        if (cutoutEnabled && _Cutoff != null)
        {
            EditorGUI.indentLevel++;
            editor.RangeProperty(_Cutoff, "Alpha Cutoff");
            EditorGUI.indentLevel--;
        }

        if (cutoutEnabled && !IsKeywordEnabled(mats, "ALPHA_FROM_GRAYSCALE"))
        {
            EditorGUILayout.HelpBox("Cutout works only when 'Alpha From Grayscale' is enabled.", MessageType.Info);
        }
        
        EditorGUILayout.EndVertical();
    }

    protected void DrawGlobalAlphaSection(MaterialEditor editor)
    {
        if (_GlobalAlpha != null)
            editor.RangeProperty(_GlobalAlpha, "Global Alpha");
    }

    protected void DrawColorSettingsSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Color Settings");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool dualColorsEnabled = IsKeywordEnabled(mats, "ENABLE_DUAL_COLORS");
        EditorGUI.BeginChangeCheck();
        dualColorsEnabled = EditorGUILayout.ToggleLeft("Dual Colors", dualColorsEnabled);
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var mat in mats)
            {
                SetKeyword(mat, "ENABLE_DUAL_COLORS", dualColorsEnabled);
                if (_DualColorsEnabled != null)
                    _DualColorsEnabled.floatValue = dualColorsEnabled ? 1.0f : 0.0f;
            }
        }

        if (dualColorsEnabled)
        {
            EditorGUI.indentLevel++;
            if (_ColorA != null) editor.ColorProperty(_ColorA, "Color A");
            if (_ColorB != null) editor.ColorProperty(_ColorB, "Color B");
            if (_ColorThreshold != null) editor.RangeProperty(_ColorThreshold, "Threshold");
            if (_ColorSmoothness != null) editor.RangeProperty(_ColorSmoothness, "Smoothness");
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUI.indentLevel++;
            if (_TintColor != null) editor.ColorProperty(_TintColor, "Tint Color");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    protected void DrawBrightnessContrastSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Brightness & Contrast");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool dualColorsEnabled = IsKeywordEnabled(mats, "ENABLE_DUAL_COLORS");
        if (dualColorsEnabled)
        {
            EditorGUILayout.HelpBox("Brightness and Contrast affect the distribution of Color A and Color B", MessageType.Info);
        }

        if (_MainTexBrightness != null && _MainTexContrast != null)
        {
            editor.RangeProperty(_MainTexBrightness, "Brightness");
            editor.RangeProperty(_MainTexContrast, "Contrast");
        }

        EditorGUILayout.EndVertical();
    }

    protected void DrawMainTextureSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Main Texture");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_MainTex != null)
            editor.TexturePropertySingleLine(new GUIContent("Main Texture"), _MainTex);

        if (_MainTexSpeedX != null && _MainTexSpeedY != null)
        {
            Vector2 scrollSpeed = new Vector2(_MainTexSpeedX.floatValue, _MainTexSpeedY.floatValue);
            scrollSpeed = EditorGUILayout.Vector2Field("Scroll Speed", scrollSpeed);
            _MainTexSpeedX.floatValue = scrollSpeed.x;
            _MainTexSpeedY.floatValue = scrollSpeed.y;
        }

        if (_MainTex != null)
            editor.TextureScaleOffsetProperty(_MainTex);
            
        EditorGUILayout.EndVertical();
    }

    protected void DrawMaskSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Mask");
        DrawKeywordSection(mats, "Enable Mask", "ENABLE_MASK", () =>
        {
            if (_MaskTex != null)
                editor.TexturePropertySingleLine(new GUIContent("Mask Texture"), _MaskTex);

            if (_MaskTex != null)
                editor.TextureScaleOffsetProperty(_MaskTex);

            if (_MaskRotation != null)
                editor.RangeProperty(_MaskRotation, "Mask Rotation");

            DrawKeywordSection(mats, "Enable Flow Mask", "ENABLE_MASK_FLOW", () =>
            {
                if (_MaskFlowTex != null)
                    editor.TexturePropertySingleLine(new GUIContent("Flow Texture"), _MaskFlowTex);

                // Important: TextureScaleOffsetProperty call for flow texture
                if (_MaskFlowTex != null)
                    editor.TextureScaleOffsetProperty(_MaskFlowTex);

                if (_MaskFlowSpeed != null)
                {
                    Vector4 flowSpeed = _MaskFlowSpeed.vectorValue;
                    Vector2 xy = new Vector2(flowSpeed.x, flowSpeed.y);
                    xy = EditorGUILayout.Vector2Field("Flow Speed (X, Y)", xy);
                    _MaskFlowSpeed.vectorValue = new Vector4(xy.x, xy.y, 0, 0);
                }

                if (_MaskFlowStrength != null)
                    editor.RangeProperty(_MaskFlowStrength, "Flow Strength");
                if (_MaskFlowSmoothness != null)
                    editor.RangeProperty(_MaskFlowSmoothness, "Flow Smoothness");
            });

            DrawKeywordSection(mats, "Enable Mask Noise", "ENABLE_MASK_NOISE", () =>
            {
                if (_MaskNoiseTex != null)
                    editor.TexturePropertySingleLine(new GUIContent("Mask Noise Texture"), _MaskNoiseTex);

                if (_MaskNoiseTex != null)
                    editor.TextureScaleOffsetProperty(_MaskNoiseTex);

                if (_MaskNoiseIntensity != null)
                    editor.RangeProperty(_MaskNoiseIntensity, "Noise Intensity");

                if (_MaskNoiseSpeed != null)
                {
                    Vector4 noiseSpeed = _MaskNoiseSpeed.vectorValue;
                    Vector2 xy = new Vector2(noiseSpeed.x, noiseSpeed.y);
                    xy = EditorGUILayout.Vector2Field("Noise Speed (X, Y)", xy);
                    _MaskNoiseSpeed.vectorValue = new Vector4(xy.x, xy.y, 0, 0);
                }
            });
        });
    }

    protected void DrawDissolveSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Dissolve");
        DrawKeywordSection(mats, "Enable Dissolve", "ENABLE_DISSOLVE", () =>
        {
            if (_DissolveTex != null)
                editor.TexturePropertySingleLine(new GUIContent("Dissolve Texture"), _DissolveTex);

            if (_DissolveTex != null)
                editor.TextureScaleOffsetProperty(_DissolveTex);

            if (!IsKeywordEnabled(mats, "ENABLE_DISSOLVE_VERTEX_COLOR") && _DissolveAmount != null)
                editor.RangeProperty(_DissolveAmount, "Dissolve Amount");

            if (_DissolveSmoothness != null)
                editor.RangeProperty(_DissolveSmoothness, "Dissolve Smoothness");

            DrawKeywordSection(mats, "Enable Dissolve Remap", "ENABLE_DISSOLVE_REMAP", () =>
            {
                if (_DissolveRemapMin != null && _DissolveRemapMax != null)
                {
                    Vector2 remap = new Vector2(_DissolveRemapMin.floatValue, _DissolveRemapMax.floatValue);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Remap Range");
                    remap = EditorGUILayout.Vector2Field("", remap);
                    EditorGUILayout.EndHorizontal();
                    _DissolveRemapMin.floatValue = remap.x;
                    _DissolveRemapMax.floatValue = remap.y;
                    EditorGUILayout.HelpBox("Remaps dissolve texture values from [Min, Max] to [0, 1]", MessageType.Info);
                }
            });

            DrawKeywordSection(mats, "Enable Outline", "ENABLE_DISSOLVE_OUTLINE", () =>
            {
                if (_DissolveOutlineStep != null)
                    editor.RangeProperty(_DissolveOutlineStep, "Outline Step");
                if (_DissolveOutlineColor != null)
                    editor.ColorProperty(_DissolveOutlineColor, "Outline Color");
            });

            DrawKeywordToggle(mats, "Use Custom Data for Dissolve", "ENABLE_DISSOLVE_VERTEX_COLOR");
        });
    }

    protected void DrawUVNoiseSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("UV Noise");
        DrawKeywordSection(mats, "Enable UV Noise", "ENABLE_UV_NOISE", () =>
        {
            if (_UVNoise != null)
                editor.TexturePropertySingleLine(new GUIContent("UV Noise Texture"), _UVNoise);

            if (_UVNoise != null)
                editor.TextureScaleOffsetProperty(_UVNoise);

            if (_UVNoiseBias != null)
                editor.FloatProperty(_UVNoiseBias, "Bias");
            if (_UVNoiseIntensity != null)
                editor.FloatProperty(_UVNoiseIntensity, "Intensity");

            if (_UVNoiseSpeed != null)
            {
                Vector4 uvNoiseSpeed = _UVNoiseSpeed.vectorValue;
                Vector2 zw = new Vector2(uvNoiseSpeed.z, uvNoiseSpeed.w);
                zw = EditorGUILayout.Vector2Field("Noise Speed (Z, W)", zw);
                _UVNoiseSpeed.vectorValue = new Vector4(0, 0, zw.x, zw.y);
            }
        });
    }


protected void DrawDistortionSection(MaterialEditor editor, Material[] mats)
{
    DrawHeader("Distortion");
    DrawKeywordSection(mats, "Enable Distortion", "ENABLE_DISTORTION", () =>
    {
        if (_DistortionTex != null)
            editor.TexturePropertySingleLine(new GUIContent("Distortion Texture"), _DistortionTex);

        if (_DistortionTex != null)
            editor.TextureScaleOffsetProperty(_DistortionTex);

        if (_DistortionIntensity != null)
            editor.FloatProperty(_DistortionIntensity, "Distortion Intensity");

        if (_DistortionSpeed != null)
        {
            Vector2 speed = new Vector2(_DistortionSpeed.vectorValue.x, _DistortionSpeed.vectorValue.y);
            speed = EditorGUILayout.Vector2Field("Distortion Speed", speed);
            _DistortionSpeed.vectorValue = new Vector4(speed.x, speed.y, 0, 0);
        }
    });
}

    protected void DrawGlowSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Glow");
        DrawKeywordSection(mats, "Enable Glow", "ENABLE_GLOW", () =>
        {
            if (_GlowTex != null && _GlowColor != null)
                editor.TexturePropertySingleLine(new GUIContent("Glow Texture"), _GlowTex, _GlowColor);

            if (_GlowTex != null)
                editor.TextureScaleOffsetProperty(_GlowTex);

            if (_GlowSpeedX != null && _GlowSpeedY != null)
            {
                Vector2 glowSpeed = new Vector2(_GlowSpeedX.floatValue, _GlowSpeedY.floatValue);
                glowSpeed = EditorGUILayout.Vector2Field("Glow Speed (X,Y)", glowSpeed);
                _GlowSpeedX.floatValue = glowSpeed.x;
                _GlowSpeedY.floatValue = glowSpeed.y;
            }

            bool blinkEnabled = DrawKeywordToggle(mats, "Enable Blink", "ENABLE_GLOW_BLINK");
            
            // Sync material property with keyword state
            if (_GlowBlinkEnabled != null)
            {
                foreach (var mat in mats)
                {
                    _GlowBlinkEnabled.floatValue = blinkEnabled ? 1.0f : 0.0f;
                }
            }

            if (blinkEnabled)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.indentLevel++;
                if (_GlowBlinkMinAlpha != null) 
                    editor.RangeProperty(_GlowBlinkMinAlpha, "Min Alpha");
                if (_GlowBlinkMaxAlpha != null) 
                    editor.RangeProperty(_GlowBlinkMaxAlpha, "Max Alpha");
                if (_GlowBlinkSpeed != null) 
                    editor.FloatProperty(_GlowBlinkSpeed, "Blink Speed");
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        });
    }

    protected void DrawRimSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Rim");
        DrawKeywordSection(mats, "Enable Rim", "ENABLE_RIM", () =>
        {
            if (_RimColor != null)
                editor.ColorProperty(_RimColor, "Color");
            if (_RimIntensity != null)
                editor.RangeProperty(_RimIntensity, "Intensity");
            if (_RimFresnel != null)
                editor.RangeProperty(_RimFresnel, "Fresnel");
        });
    }

    protected void DrawRimLightSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Rim Light");
        DrawKeywordSection(mats, "Enable Rim Light", "ENABLE_RIM_LIGHT", () =>
        {
            if (_RimLightColor != null)
                editor.ColorProperty(_RimLightColor, "Color");
            if (_RimLightIntensity != null)
                editor.RangeProperty(_RimLightIntensity, "Intensity");
            if (_RimLightFresnel != null)
                editor.RangeProperty(_RimLightFresnel, "Fresnel");
        });
    }

    protected void DrawVertexOffsetSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Vertex Offset");
        DrawKeywordSection(mats, "Enable Vertex Offset", "ENABLE_VERTEX_OFFSET", () =>
        {
            if (_OffsetNoiseTex != null)
                editor.TexturePropertySingleLine(new GUIContent("Offset Noise Texture"), _OffsetNoiseTex);

            if (_OffsetNoiseTex != null)
                editor.TextureScaleOffsetProperty(_OffsetNoiseTex);

            if (_OffsetAmount != null)
                editor.RangeProperty(_OffsetAmount, "Offset Amount");
            if (_OffsetPower != null)
                editor.RangeProperty(_OffsetPower, "Offset Power");
            if (_ScrollSpeedX != null)
                editor.FloatProperty(_ScrollSpeedX, "Scroll Speed X");
            if (_ScrollSpeedY != null)
                editor.FloatProperty(_ScrollSpeedY, "Scroll Speed Y");
        });
    }

protected void DrawSoftParticlesSection(MaterialEditor editor, Material[] mats)
{
    DrawHeader("Soft Particles");
    
    if (DrawKeywordToggle(mats, "Enable Soft Particles", "ENABLE_SOFTPARTICLES"))
    {
        if (_InvFade != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.indentLevel++;
            
            editor.RangeProperty(_InvFade, "Soft Particles Factor");
            
            #if UNITY_2019_3_OR_NEWER
            bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (isURP)
            {
                EditorGUILayout.HelpBox("Controls soft particle fade intensity in URP", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("Higher values create softer particles in Built-in RP", MessageType.None);
            }
            #endif
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
    }
}

    private bool IsUsingURP()
    {

        #if UNITY_2019_3_OR_NEWER
        var renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        return renderPipeline != null && renderPipeline.GetType().Name.Contains("Universal");
        #else
        return false;
        #endif
    }

    protected void DrawDepthSettingsSection(MaterialEditor editor, Material[] mats)
    {
        if (_ApplicationMode == null)
            return;

        ApplicationMode currentAppMode = (ApplicationMode)_ApplicationMode.floatValue;

        // UI Mode fix - disable ZWrite and hide depth settings
        if (currentAppMode == ApplicationMode.UI)
        {
            foreach (var mat in mats)
            {
                if (mat.HasProperty("_ZWrite"))
                    mat.SetInt("_ZWrite", 0);

                if (mat.HasProperty("_ZTestMode"))
                    mat.SetInt("_ZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            return;
        }

        // Depth settings for non-UI modes
        DrawHeader("Depth Settings");
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_ZWrite != null)
        {
            bool zWriteEnabled = _ZWrite.floatValue > 0.5f;
            EditorGUI.BeginChangeCheck();
            zWriteEnabled = EditorGUILayout.Toggle("Z Write", zWriteEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                _ZWrite.floatValue = zWriteEnabled ? 1f : 0f;
                foreach (var mat in mats)
                    mat.SetInt("_ZWrite", zWriteEnabled ? 1 : 0);
            }
        }

        if (_ZTestMode != null)
        {
            ZTestMode ztest = (ZTestMode)(int)_ZTestMode.floatValue;
            EditorGUI.BeginChangeCheck();
            ztest = (ZTestMode)EditorGUILayout.EnumPopup("ZTest Mode", ztest);
            if (EditorGUI.EndChangeCheck())
            {
                _ZTestMode.floatValue = (float)(int)ztest;
                foreach (var mat in mats)
                {
                    mat.SetInt("_ZTestMode", (int)ztest);
                    EditorUtility.SetDirty(mat);
                }
            }
        }

        EditorGUILayout.EndVertical();

        if (currentAppMode != ApplicationMode.Sprite2D)
        {
            DrawRenderQueue(mats);
        }
    }

    protected void DrawShineSection(MaterialEditor editor, Material[] mats)
    {
        DrawHeader("Shine Effect");

        // Exit if shine properties are missing
        if (_ShineMask == null && _ShineColor == null)
            return;

        DrawKeywordSection(mats, "Enable Shine", "ENABLE_SHINE", () =>
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);

            if (_ShineMask != null)
                editor.TexturePropertySingleLine(new GUIContent("Shine Mask"), _ShineMask);

            if (_ShineColor != null)
                editor.ColorProperty(_ShineColor, "Shine Color");

            if (_ShineIntensity != null)
                editor.FloatProperty(_ShineIntensity, "Intensity");

            if (_ShineWidth != null)
                editor.FloatProperty(_ShineWidth, "Width");

            if (_ShineSpeed != null)
                editor.FloatProperty(_ShineSpeed, "Speed");

            if (_ShineDelay != null)
                editor.FloatProperty(_ShineDelay, "Delay Between");

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Asymmetric Edge Sharpness", EditorStyles.boldLabel);

            if (_ShineSharpnessLeft != null)
                editor.FloatProperty(_ShineSharpnessLeft, "Left Edge");

            if (_ShineSharpnessRight != null)
                editor.FloatProperty(_ShineSharpnessRight, "Right Edge");

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Space & Direction", EditorStyles.boldLabel);

            if (_ShineWorldSpace != null && (_ShineWorldDirection != null || _ShineRotation != null))
            {
                bool useWorld = _ShineWorldSpace.floatValue > 0.5f;
                EditorGUI.BeginChangeCheck();
                useWorld = EditorGUILayout.Toggle("World Space", useWorld);
                if (EditorGUI.EndChangeCheck())
                {
                    _ShineWorldSpace.floatValue = useWorld ? 1f : 0f;
                    foreach (var m in mats)
                        SetKeyword(m, "SHINE_WORLD_SPACE", useWorld);
                }

                Vector4 direction = useWorld ? _ShineWorldDirection?.vectorValue ?? Vector4.zero : _ShineRotation?.vectorValue ?? Vector4.zero;
                Vector2 dirDisplay = new Vector2(direction.x, useWorld ? direction.z : direction.y);

                EditorGUI.BeginChangeCheck();
                dirDisplay = EditorGUILayout.Vector2Field(useWorld ? "Direction (X,Z)" : "Direction (X,Y)", dirDisplay);
                if (EditorGUI.EndChangeCheck())
                {
                    if (useWorld && _ShineWorldDirection != null)
                        _ShineWorldDirection.vectorValue = new Vector4(dirDisplay.x, 0, dirDisplay.y, 0);
                    else if (_ShineRotation != null)
                        _ShineRotation.vectorValue = new Vector4(dirDisplay.x, dirDisplay.y, 0, 0);
                }
            }

            EditorGUILayout.EndVertical();
        });
    }
}
}