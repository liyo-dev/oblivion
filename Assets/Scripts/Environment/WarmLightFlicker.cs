using UnityEngine;

/// <summary>
/// Añade un efecto de parpadeo cálido a una luz, simulando una vela, farol o chimenea.
/// Añádelo al GameObject que tenga el componente Light.
/// </summary>
[RequireComponent(typeof(Light))]
public class WarmLightFlicker : MonoBehaviour
{
    [Header("Configuración de Luz Base")]
    [Tooltip("Color cálido de la luz (naranja/amarillo típico de velas)")]
    [SerializeField] private Color warmColor = new Color(1f, 0.6f, 0.3f, 1f);
    
    [Tooltip("Intensidad base de la luz")]
    [SerializeField] private float baseIntensity = 1.5f;
    
    [Tooltip("Rango de la luz")]
    [SerializeField] private float lightRange = 10f;
    
    [Header("Configuración de Renderizado")]
    [Tooltip("Modo de renderizado de la luz (Auto recomendado para evitar artefactos)")]
    [SerializeField] private LightRenderMode renderMode = LightRenderMode.Auto;
    
    [Tooltip("Tipo de sombras (None = sin sombras, más rendimiento)")]
    [SerializeField] private LightShadows shadowType = LightShadows.None;
    
    [Header("Efecto de Parpadeo")]
    [Tooltip("Activar efecto de parpadeo")]
    [SerializeField] private bool enableFlicker = true;
    
    [Tooltip("Variación máxima de intensidad (0.1 = 10% de variación)")]
    [Range(0f, 0.5f)]
    [SerializeField] private float intensityVariation = 0.15f;
    
    [Tooltip("Velocidad del parpadeo (más alto = más rápido)")]
    [Range(0.5f, 10f)]
    [SerializeField] private float flickerSpeed = 3f;
    
    [Tooltip("Suavidad del parpadeo (más alto = más suave)")]
    [Range(0.1f, 2f)]
    [SerializeField] private float flickerSmoothness = 0.5f;
    
    [Header("Variación de Color (Opcional)")]
    [Tooltip("Activar variación sutil de color")]
    [SerializeField] private bool enableColorVariation = true;
    
    [Tooltip("Color secundario para variar (más rojo/naranja)")]
    [SerializeField] private Color secondaryColor = new Color(1f, 0.4f, 0.2f, 1f);
    
    [Tooltip("Intensidad de la variación de color")]
    [Range(0f, 1f)]
    [SerializeField] private float colorVariationAmount = 0.2f;
    
    [Header("Presets")]
    [Tooltip("Aplicar preset al iniciar")]
    [SerializeField] private LightPreset preset = LightPreset.Lantern;
    
    public enum LightPreset
    {
        Custom,      // Usar valores configurados manualmente
        Candle,      // Vela pequeña, parpadeo rápido
        Lantern,     // Farol, parpadeo medio y suave
        Fireplace,   // Chimenea, parpadeo lento y cálido
        Torch        // Antorcha, parpadeo fuerte e irregular
    }
    
    private Light _light;
    private float _noiseOffset;
    private float _targetIntensity;
    private float _currentIntensity;
    
    private void Awake()
    {
        _light = GetComponent<Light>();
        _noiseOffset = Random.Range(0f, 1000f); // Offset aleatorio para que no todas las luces parpadeen igual
        
        // Aplicar preset si no es Custom
        if (preset != LightPreset.Custom)
        {
            ApplyPreset(preset);
        }
        
        // Configurar tipo de luz como Point para iluminación omnidireccional
        _light.type = LightType.Point;
        
        // Configurar luz inicial
        _light.color = warmColor;
        _light.intensity = baseIntensity;
        _light.range = lightRange;
        
        // Configurar modo de renderizado y sombras
        _light.renderMode = renderMode;
        _light.shadows = shadowType;
        
        _currentIntensity = baseIntensity;
        _targetIntensity = baseIntensity;
    }
    
    private void Update()
    {
        if (!enableFlicker)
        {
            _light.intensity = baseIntensity;
            _light.color = warmColor;
            return;
        }
        
        // Usar Perlin Noise para un parpadeo más natural
        float noise1 = Mathf.PerlinNoise(Time.time * flickerSpeed + _noiseOffset, 0f);
        float noise2 = Mathf.PerlinNoise(Time.time * flickerSpeed * 0.5f + _noiseOffset + 100f, 0f);
        
        // Combinar dos frecuencias de ruido para más variación
        float combinedNoise = (noise1 * 0.7f + noise2 * 0.3f);
        
        // Calcular intensidad objetivo
        float intensityOffset = (combinedNoise - 0.5f) * 2f * intensityVariation * baseIntensity;
        _targetIntensity = baseIntensity + intensityOffset;
        
        // Suavizar la transición
        _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, Time.deltaTime / flickerSmoothness);
        _light.intensity = _currentIntensity;
        
        // Variación de color
        if (enableColorVariation)
        {
            float colorBlend = combinedNoise * colorVariationAmount;
            _light.color = Color.Lerp(warmColor, secondaryColor, colorBlend);
        }
    }
    
    /// <summary>
    /// Aplica un preset de luz predefinido
    /// </summary>
    public void ApplyPreset(LightPreset newPreset)
    {
        preset = newPreset;
        
        switch (newPreset)
        {
            case LightPreset.Candle:
                warmColor = new Color(1f, 0.65f, 0.35f);
                secondaryColor = new Color(1f, 0.5f, 0.2f);
                baseIntensity = 0.08f;
                lightRange = 5f;
                intensityVariation = 0.25f;
                flickerSpeed = 5f;
                flickerSmoothness = 0.3f;
                enableColorVariation = true;
                colorVariationAmount = 0.3f;
                break;
                
            case LightPreset.Lantern:
                warmColor = new Color(1f, 0.7f, 0.4f);
                secondaryColor = new Color(1f, 0.55f, 0.3f);
                baseIntensity = 1.5f;
                lightRange = 10f;
                intensityVariation = 0.12f;
                flickerSpeed = 2.5f;
                flickerSmoothness = 0.6f;
                enableColorVariation = true;
                colorVariationAmount = 0.15f;
                break;
                
            case LightPreset.Fireplace:
                warmColor = new Color(1f, 0.55f, 0.25f);
                secondaryColor = new Color(1f, 0.4f, 0.15f);
                baseIntensity = 2f;
                lightRange = 15f;
                intensityVariation = 0.2f;
                flickerSpeed = 1.5f;
                flickerSmoothness = 0.8f;
                enableColorVariation = true;
                colorVariationAmount = 0.25f;
                break;
                
            case LightPreset.Torch:
                warmColor = new Color(1f, 0.6f, 0.2f);
                secondaryColor = new Color(1f, 0.35f, 0.1f);
                baseIntensity = 2.5f;
                lightRange = 12f;
                intensityVariation = 0.35f;
                flickerSpeed = 4f;
                flickerSmoothness = 0.25f;
                enableColorVariation = true;
                colorVariationAmount = 0.4f;
                break;
        }
        
        // Actualizar la luz si ya existe
        if (_light != null)
        {
            _light.color = warmColor;
            _light.intensity = baseIntensity;
            _light.range = lightRange;
        }
    }
    
    /// <summary>
    /// Enciende o apaga la luz con fade
    /// </summary>
    public void SetLightEnabled(bool isEnabled, float fadeDuration = 0.5f)
    {
        StopAllCoroutines();
        StartCoroutine(FadeLight(isEnabled, fadeDuration));
    }
    
    private System.Collections.IEnumerator FadeLight(bool targetOn, float duration)
    {
        float startIntensity = _light.intensity;
        float endIntensity = targetOn ? baseIntensity : 0f;
        float elapsed = 0f;
        
        // Desactivar parpadeo durante el fade
        bool wasFlickering = enableFlicker;
        enableFlicker = false;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _light.intensity = Mathf.Lerp(startIntensity, endIntensity, t);
            yield return null;
        }
        
        _light.intensity = endIntensity;
        
        // Restaurar parpadeo si se encendió
        if (targetOn)
        {
            enableFlicker = wasFlickering;
            _currentIntensity = baseIntensity;
        }
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Aplicar cambios en el editor
        if (_light == null)
            _light = GetComponent<Light>();
            
        if (_light != null && !Application.isPlaying)
        {
            _light.type = LightType.Point;
            _light.color = warmColor;
            _light.intensity = baseIntensity;
            _light.range = lightRange;
            _light.renderMode = renderMode;
            _light.shadows = shadowType;
        }
    }
    
    [ContextMenu("🕯️ Aplicar Preset: Vela")]
    private void ApplyPresetCandle() => ApplyPreset(LightPreset.Candle);
    
    [ContextMenu("🏮 Aplicar Preset: Farol")]
    private void ApplyPresetLantern() => ApplyPreset(LightPreset.Lantern);
    
    [ContextMenu("🔥 Aplicar Preset: Chimenea")]
    private void ApplyPresetFireplace() => ApplyPreset(LightPreset.Fireplace);
    
    [ContextMenu("🔦 Aplicar Preset: Antorcha")]
    private void ApplyPresetTorch() => ApplyPreset(LightPreset.Torch);
#endif
}

