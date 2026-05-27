using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using GameParticles.Editor;

namespace GameParticles.Editor
{
public partial class GameParticleShaderGUI
{
    private void LoadConfig()
    {
        if (_configLoaded && _config != null) return;

        _configLoaded = true;

#if UNITY_EDITOR
        if (_config == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:RenderSettingsConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _config = AssetDatabase.LoadAssetAtPath<RenderSettingsConfig>(path);
            }
        }
#endif

        if (_config == null)
        {
            CreateFallbackConfig();
        }
    }

    // Clear cache on script recompilation
#if UNITY_EDITOR
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        _configLoaded = false;
        // Don't clear _cachedConfig - it can be reused
    }
#endif

    private void CreateFallbackConfig()
    {
        _config = ScriptableObject.CreateInstance<RenderSettingsConfig>();
        Debug.Log("Using fallback render config");
    }

    private RenderSettingsConfig.RenderModeSettings GetRenderSettings(ApplicationMode appMode, RenderingMode renderMode)
    {
        LoadConfig();
        if (_config == null) return null;

        var appConfig = appMode switch
        {
            ApplicationMode.Game3D => _config.Game3D,
            ApplicationMode.UI => _config.UI,
            ApplicationMode.Sprite2D => _config.Sprite2D,
            _ => _config.Game3D
        };

        return renderMode switch
        {
            RenderingMode.Transparent => appConfig?.Transparent,
            RenderingMode.Additive => appConfig?.Additive,
            RenderingMode.SoftAdditive => appConfig?.SoftAdditive,
            RenderingMode.Blend => appConfig?.Blend,
            RenderingMode.Opaque => appConfig?.Opaque,
            _ => appConfig?.Transparent
        };
    }

    // === RENDER SETTINGS MANAGEMENT ===
    protected void DrawRenderSettings(MaterialEditor editor, Material[] mats)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_ApplicationMode == null || _RenderingMode == null)
        {
            EditorGUILayout.HelpBox("Required properties not found in shader", MessageType.Error);
            EditorGUILayout.EndVertical();
            return;
        }

        ApplicationMode appMode = (ApplicationMode)_ApplicationMode.floatValue;
        ApplicationMode previousAppMode = appMode;

        EditorGUI.BeginChangeCheck();
        appMode = (ApplicationMode)EditorGUILayout.EnumPopup("Application Mode", appMode);

        if (EditorGUI.EndChangeCheck())
        {
            _ApplicationMode.floatValue = (float)appMode;

            if (previousAppMode == ApplicationMode.UI && appMode != ApplicationMode.UI)
                SaveMaterialState(mats[0]);

            foreach (var mat in mats)
                SetApplicationMode(mat, appMode);
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label($"Queue: {mats[0].renderQueue}", EditorStyles.miniLabel);

        RenderingMode mode = (RenderingMode)_RenderingMode.floatValue;

        EditorGUI.BeginChangeCheck();
        mode = (RenderingMode)EditorGUILayout.EnumPopup("Rendering Mode", mode);

        if (EditorGUI.EndChangeCheck())
        {
            _RenderingMode.floatValue = (float)mode;
            foreach (var mat in mats)
                SetRenderMode(mat, mode, appMode);
        }

        // Manual Blend Settings for RenderingMode.Other
        if (mode == RenderingMode.Other)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Manual Blend Settings", EditorStyles.boldLabel);

            foreach (var mat in mats)
            {
                int src = mat.HasProperty("_SrcBlend") ?
                    mat.GetInt("_SrcBlend") : (int)UnityEngine.Rendering.BlendMode.One;

                int dst = mat.HasProperty("_DstBlend") ?
                    mat.GetInt("_DstBlend") : (int)UnityEngine.Rendering.BlendMode.Zero;

                EditorGUI.BeginChangeCheck();

                src = EditorGUILayout.IntPopup("Src Blend", src,
                    System.Enum.GetNames(typeof(UnityEngine.Rendering.BlendMode)),
                    (int[])System.Enum.GetValues(typeof(UnityEngine.Rendering.BlendMode)));

                dst = EditorGUILayout.IntPopup("Dst Blend", dst,
                    System.Enum.GetNames(typeof(UnityEngine.Rendering.BlendMode)),
                    (int[])System.Enum.GetValues(typeof(UnityEngine.Rendering.BlendMode)));

                if (EditorGUI.EndChangeCheck())
                {
                    mat.SetInt("_SrcBlend", src);
                    mat.SetInt("_DstBlend", dst);
                }
            }
        }

        if (_CullMode != null)
        {
            CullMode cull = (CullMode)(int)_CullMode.floatValue;

            EditorGUI.BeginChangeCheck();
            cull = (CullMode)EditorGUILayout.EnumPopup("Cull Mode", cull);

            if (EditorGUI.EndChangeCheck())
            {
                _CullMode.floatValue = (float)(int)cull;
                foreach (var mat in mats)
                    mat.SetInt("_CullMode", (int)cull);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void SetApplicationMode(Material mat, ApplicationMode appMode)
    {
        if (_RenderingMode == null) return;

        if (appMode != ApplicationMode.UI)
            SaveMaterialState(mat);

        RenderingMode currentMode = (RenderingMode)_RenderingMode.floatValue;
        SetRenderMode(mat, currentMode, appMode);
    }

    private void SetRenderMode(Material mat, RenderingMode mode, ApplicationMode appMode)
    {
        mat.DisableKeyword("_ALPHA_TEST");

        var settings = GetRenderSettings(appMode, mode);

        if (settings != null)
        {
            mat.SetOverrideTag("RenderType", settings.RenderType);
            mat.SetInt("_SrcBlend", (int)settings.SrcBlend);
            mat.SetInt("_DstBlend", (int)settings.DstBlend);
            mat.SetInt("_ZWrite", settings.ZWrite);
            mat.SetInt("_ZTestMode", settings.ZTest);
            mat.renderQueue = settings.RenderQueue;
        }
        else if (mode == RenderingMode.Other)
        {
            // Do nothing - user manually controls blend settings
        }
        else
        {
            Debug.LogWarning($"Render settings not found for {appMode}.{mode}, using fallback");
            SetRenderMode(mat, mode, ApplicationMode.Game3D);
        }
    }

    protected void SaveMaterialState(Material mat)
    {
        if (mat == null) return;

        // Reset state if saving different material
        if (_lastSavedMaterial != null && _lastSavedMaterial != mat)
        {
            _previousState = null;
        }

        _previousState = new MaterialState
        {
            srcBlend = mat.HasProperty("_SrcBlend") ? mat.GetInt("_SrcBlend") : 1,
            dstBlend = mat.HasProperty("_DstBlend") ? mat.GetInt("_DstBlend") : 0,

            // Safe ZWrite read
            zWrite = mat.HasProperty("_ZWrite") ? mat.GetInt("_ZWrite") : 0,

            // Safe ZTest read
            zTest = mat.HasProperty("_ZTestMode") ?
                mat.GetInt("_ZTestMode") : (int)CompareFunction.Always,

            rimEnabled = mat.IsKeywordEnabled("ENABLE_RIM"),
            rimLightEnabled = mat.IsKeywordEnabled("ENABLE_RIM_LIGHT"),
            softParticlesEnabled = mat.IsKeywordEnabled("ENABLE_SOFTPARTICLES"),
            vertexOffsetEnabled = mat.IsKeywordEnabled("ENABLE_VERTEX_OFFSET"),

            renderingMode = mat.HasProperty("_RenderingMode") ?
                mat.GetFloat("_RenderingMode") : 0f,
            globalAlpha = mat.HasProperty("_GlobalAlpha") ?
                mat.GetFloat("_GlobalAlpha") : 1f
        };

        _lastSavedMaterial = mat;
    }

    protected void DrawRenderQueue(Material[] mats)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Render Queue", EditorStyles.boldLabel);

        int queue = (int)mats[0].renderQueue;
        int newQueue = EditorGUILayout.IntSlider("Render Queue", queue, 1000, 5000);

        if (newQueue != queue)
        {
            foreach (var mat in mats)
                mat.renderQueue = newQueue;
        }
    }
}
}