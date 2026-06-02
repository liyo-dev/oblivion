using UnityEngine;
using DG.Tweening;

/// <summary>
/// Preset de configuración de fog reutilizable.
/// Crea diferentes presets para bosque, pantano, cueva, etc.
/// </summary>
[CreateAssetMenu(fileName = "AmbientPreset_New", menuName = "Environment/Ambient Preset", order = 1)]
public class AmbientPreset : ScriptableObject
{
    [Header("Fog Settings")]
    [Tooltip("¿Activar fog con este preset?")]
    public bool enableFog = true;
    
    [Tooltip("Color del fog")]
    public Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    
    [Tooltip("Densidad del fog (0-1). Mayor = más denso. Para niebla cercana usa Linear con fogEnd bajo.")]
    [Range(0f, 1f)]
    public float fogDensity = 0.02f;
    
    [Tooltip("Modo del fog")]
    public FogMode fogMode = FogMode.ExponentialSquared;
    
    [Header("Linear Fog (solo si fogMode = Linear)")]
    [Tooltip("Distancia de inicio del fog")]
    public float fogStartDistance = 0f;
    
    [Tooltip("Distancia final del fog")]
    public float fogEndDistance = 100f;
    
    [Header("Transition")]
    [Tooltip("Duración de la transición (segundos)")]
    public float transitionDuration = 1.5f;
    
    [Tooltip("Ease de la transición")]
    public Ease transitionEase = Ease.InOutSine;
    
    [Header("Luz Ambiente")]
    [Tooltip("¿Controlar la luz ambiente con este preset?")]
    public bool controlAmbientLight = false;

    [Tooltip("Color de la luz ambiente")]
    public Color ambientLightColor = Color.white;

    [Tooltip("Intensidad de la luz ambiente")]
    [Range(0f, 2f)]
    public float ambientLightIntensity = 1f;

    [Header("Metadata")]
    [TextArea(2, 4)]
    [Tooltip("Descripción del preset para referencia")]
    public string description = "";

    public void ApplyImmediate()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.fogEndDistance = fogEndDistance;

        if (controlAmbientLight)
        {
            RenderSettings.ambientLight = ambientLightColor;
            RenderSettings.ambientIntensity = ambientLightIntensity;
        }
    }

    public Tween ApplyWithTransition()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogMode = fogMode;

        float startDensity = RenderSettings.fogDensity;
        Color startFogColor = RenderSettings.fogColor;
        float startFogStart = RenderSettings.fogStartDistance;
        float startFogEnd = RenderSettings.fogEndDistance;
        Color startAmbient = RenderSettings.ambientLight;
        float startAmbientI = RenderSettings.ambientIntensity;

        return DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity = Mathf.Lerp(startDensity, fogDensity, t);
                RenderSettings.fogColor = Color.Lerp(startFogColor, fogColor, t);
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, fogStartDistance, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, fogEndDistance, t);

                if (controlAmbientLight)
                {
                    RenderSettings.ambientLight = Color.Lerp(startAmbient, ambientLightColor, t);
                    RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientI, ambientLightIntensity, t);
                }
            },
            1f,
            transitionDuration
        ).SetEase(transitionEase).SetUpdate(true);
    }
    
#if UNITY_EDITOR
    [ContextMenu("Preview in Editor")]
    private void PreviewInEditor()
    {
        ApplyImmediate();
        Debug.Log($"[AmbientPreset] Preview aplicado: {name}");
    }
#endif
}

