using UnityEngine;
using DG.Tweening;

/// <summary>
/// Preset de configuración de fog reutilizable.
/// Crea diferentes presets para bosque, pantano, cueva, etc.
/// </summary>
[CreateAssetMenu(fileName = "FogPreset_New", menuName = "Environment/Fog Preset", order = 1)]
public class FogPreset : ScriptableObject
{
    [Header("Fog Settings")]
    [Tooltip("¿Activar fog con este preset?")]
    public bool enableFog = true;
    
    [Tooltip("Color del fog")]
    public Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    
    [Tooltip("Densidad del fog (0-0.1). Mayor = más denso")]
    [Range(0f, 0.1f)]
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
    
    [Header("Metadata")]
    [TextArea(2, 4)]
    [Tooltip("Descripción del preset para referencia")]
    public string description = "";
    
    /// <summary>
    /// Aplica este preset inmediatamente (sin transición)
    /// </summary>
    public void ApplyImmediate()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.fogEndDistance = fogEndDistance;
    }
    
    /// <summary>
    /// Aplica este preset con transición suave
    /// </summary>
    public Tween ApplyWithTransition()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogMode = fogMode;
        
        float startDensity = RenderSettings.fogDensity;
        Color startColor = RenderSettings.fogColor;
        float startFogStart = RenderSettings.fogStartDistance;
        float startFogEnd = RenderSettings.fogEndDistance;
        
        return DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity = Mathf.Lerp(startDensity, fogDensity, t);
                RenderSettings.fogColor = Color.Lerp(startColor, fogColor, t);
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, fogStartDistance, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, fogEndDistance, t);
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
        Debug.Log($"[FogPreset] Preview aplicado: {name}");
    }
#endif
}

