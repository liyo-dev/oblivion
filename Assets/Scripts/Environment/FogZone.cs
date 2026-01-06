using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;

/// <summary>
/// Zona que modifica el fog y opcionalmente la música cuando el jugador entra.
/// Coloca este componente en un GameObject con un Collider (trigger).
/// </summary>
[RequireComponent(typeof(Collider))]
public class FogZone : MonoBehaviour
{
    [Header("Preset (Opcional)")]
    [Tooltip("Usar un preset predefinido. Si se asigna, ignora la configuración manual de fog.")]
    [SerializeField] private FogPreset fogPreset;
    
    [Header("Fog Settings (Manual - ignorado si hay Preset)")]
    [Tooltip("¿Activar fog en esta zona?")]
    [SerializeField] private bool enableFog = true;
    
    [Tooltip("Color del fog en esta zona")]
    [SerializeField] private Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    
    [Tooltip("Densidad del fog (0-1). Mayor = más denso")]
    [Range(0f, 0.1f)]
    [SerializeField] private float fogDensity = 0.02f;
    
    [Tooltip("Distancia de inicio del fog (solo para Linear)")]
    [SerializeField] private float fogStartDistance = 0f;
    
    [Tooltip("Distancia final del fog (solo para Linear)")]
    [SerializeField] private float fogEndDistance = 100f;
    
    [Tooltip("Modo del fog")]
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
    
    [Header("Music Settings")]
    [Tooltip("¿Cambiar la música al entrar en esta zona?")]
    [SerializeField] private bool changeMusic = true;
    
    [Tooltip("ID para buscar la música en AudioGraphProfile → Ambient Zones. Debe coincidir con el 'Zone Id' configurado allí.")]
    [SerializeField] private string musicZoneId = "";
    
    [Header("Transition")]
    [Tooltip("Duración de la transición del fog (segundos)")]
    [SerializeField] private float transitionDuration = 1.5f;
    
    [Tooltip("Ease de la transición")]
    [SerializeField] private Ease transitionEase = Ease.InOutSine;
    
    [Header("Priority")]
    [Tooltip("Prioridad de esta zona (mayor = más importante si hay solapamiento)")]
    [SerializeField] private int priority = 0;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Estado global del fog por defecto (se guarda al iniciar el juego)
    private static bool _defaultFogEnabled;
    private static Color _defaultFogColor;
    private static float _defaultFogDensity;
    private static float _defaultFogStart;
    private static float _defaultFogEnd;
    private static FogMode _defaultFogMode;
    private static bool _defaultsCaptured = false;
    
    // Control de zonas activas
    private static FogZone _currentActiveZone;
    private static Tween _currentTween;
    
    // Música anterior para restaurar
    private static AudioClip _previousMusic;
    private static bool _wasMusicPlaying;
    
    /// <summary>
    /// Zona de fog actualmente activa (donde está el jugador)
    /// </summary>
    public static FogZone CurrentActiveZone => _currentActiveZone;
    
    /// <summary>
    /// ID de la música configurada para esta zona
    /// </summary>
    public string MusicZoneId => musicZoneId;
    
    private void Awake()
    {
        // Asegurar que el collider sea trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[FogZone] Collider en '{gameObject.name}' configurado como trigger automáticamente");
        }
        
        // Capturar valores por defecto solo una vez
        CaptureDefaults();
    }
    
    private static void CaptureDefaults()
    {
        if (_defaultsCaptured) return;
        
        _defaultFogEnabled = RenderSettings.fog;
        _defaultFogColor = RenderSettings.fogColor;
        _defaultFogDensity = RenderSettings.fogDensity;
        _defaultFogStart = RenderSettings.fogStartDistance;
        _defaultFogEnd = RenderSettings.fogEndDistance;
        _defaultFogMode = RenderSettings.fogMode;
        _defaultsCaptured = true;

        Debug.Log($"[FogZone] Valores por defecto capturados - Enabled:{_defaultFogEnabled}, Density:{_defaultFogDensity}, Color:{_defaultFogColor}");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Verificar prioridad
        if (_currentActiveZone != null && _currentActiveZone.priority > priority)
        {
            if (showDebugLogs)
                Debug.Log($"[FogZone] '{gameObject.name}' ignorada - zona '{_currentActiveZone.gameObject.name}' tiene mayor prioridad");
            return;
        }
        
        if (showDebugLogs)
            Debug.Log($"[FogZone] Jugador entró en zona '{gameObject.name}' - Aplicando fog y música");
        
        _currentActiveZone = this;
        TransitionToZoneFog();
        
        // Diferir cambio de música al siguiente frame para evitar micro-freeze
        StartCoroutine(DeferredMusicTransition());
    }
    
    private System.Collections.IEnumerator DeferredMusicTransition()
    {
        yield return null; // Esperar un frame
        TransitionToZoneMusic();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Solo restaurar si somos la zona activa
        if (_currentActiveZone != this) return;
        
        if (showDebugLogs)
            Debug.Log($"[FogZone] Jugador salió de zona '{gameObject.name}' - Restaurando fog y música");
        
        _currentActiveZone = null;
        TransitionToDefaultFog();
        RestorePreviousMusic();
    }
    
    private void TransitionToZoneFog()
    {
        // Cancelar transición anterior si existe
        _currentTween?.Kill();
        
        // Obtener valores del preset o de la configuración manual
        bool targetEnableFog = fogPreset != null ? fogPreset.enableFog : enableFog;
        Color targetColor = fogPreset != null ? fogPreset.fogColor : fogColor;
        float targetDensity = fogPreset != null ? fogPreset.fogDensity : fogDensity;
        FogMode targetMode = fogPreset != null ? fogPreset.fogMode : fogMode;
        float targetStart = fogPreset != null ? fogPreset.fogStartDistance : fogStartDistance;
        float targetEnd = fogPreset != null ? fogPreset.fogEndDistance : fogEndDistance;
        float duration = fogPreset != null ? fogPreset.transitionDuration : transitionDuration;
        Ease ease = fogPreset != null ? fogPreset.transitionEase : transitionEase;
        
        // Activar fog si no está activo
        RenderSettings.fog = targetEnableFog;
        RenderSettings.fogMode = targetMode;
        
        // Transición suave de los valores
        float startDensity = RenderSettings.fogDensity;
        Color startColor = RenderSettings.fogColor;
        float startFogStart = RenderSettings.fogStartDistance;
        float startFogEnd = RenderSettings.fogEndDistance;
        
        _currentTween = DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity = Mathf.Lerp(startDensity, targetDensity, t);
                RenderSettings.fogColor = Color.Lerp(startColor, targetColor, t);
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, targetStart, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, targetEnd, t);
            },
            1f,
            duration
        ).SetEase(ease).SetUpdate(true);
    }
    
    private void TransitionToDefaultFog()
    {
        // Cancelar transición anterior si existe
        _currentTween?.Kill();
        
        float startDensity = RenderSettings.fogDensity;
        Color startColor = RenderSettings.fogColor;
        float startFogStart = RenderSettings.fogStartDistance;
        float startFogEnd = RenderSettings.fogEndDistance;
        
        _currentTween = DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity = Mathf.Lerp(startDensity, _defaultFogDensity, t);
                RenderSettings.fogColor = Color.Lerp(startColor, _defaultFogColor, t);
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, _defaultFogStart, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(startFogEnd, _defaultFogEnd, t);
            },
            1f,
            transitionDuration
        ).SetEase(transitionEase).SetUpdate(true).OnComplete(() => {
            RenderSettings.fog = _defaultFogEnabled;
            RenderSettings.fogMode = _defaultFogMode;
        });
    }
    
    /// <summary>
    /// Cambia la música al entrar en la zona
    /// </summary>
    private void TransitionToZoneMusic()
    {
        if (!changeMusic) return;
        
        // Verificar que hay un ID configurado
        if (string.IsNullOrEmpty(musicZoneId))
        {
            if (showDebugLogs)
                Debug.Log($"[FogZone] '{gameObject.name}' no tiene musicZoneId configurado - no se cambia música");
            return;
        }
        
        var audioService = AudioService.Instance;
        if (audioService == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[FogZone] AudioService no disponible para cambiar música");
            return;
        }
        
        // Guardar música actual antes de cambiar
        _previousMusic = audioService.CurrentMusicClip;
        _wasMusicPlaying = _previousMusic != null;
        
        // Buscar en AudioGraphProfile por el musicZoneId
        if (audioService.profile != null)
        {
            var rule = audioService.profile.GetAmbientZoneRule(musicZoneId);
            if (rule?.music != null)
            {
                if (showDebugLogs)
                    Debug.Log($"[FogZone] Cambiando música a '{rule.music.name}' (musicZoneId: '{musicZoneId}')");
                
                audioService.PlayMusic(rule.music, rule.fade);
            }
            else if (showDebugLogs)
            {
                Debug.LogWarning($"[FogZone] No se encontró música para musicZoneId '{musicZoneId}' en AudioGraphProfile");
            }
        }
    }
    
    /// <summary>
    /// Restaura la música anterior al salir de la zona
    /// </summary>
    private void RestorePreviousMusic()
    {
        if (!changeMusic) return;
        
        var audioService = AudioService.Instance;
        if (audioService == null) return;
        
        // Obtener el fade del profile si existe
        float fade = audioService.profile?.GetAmbientZoneRule(musicZoneId)?.fade ?? 1.5f;
        
        if (_wasMusicPlaying && _previousMusic != null)
        {
            if (showDebugLogs)
                Debug.Log($"[FogZone] Restaurando música anterior: '{_previousMusic.name}'");
            
            audioService.PlayMusic(_previousMusic, fade);
        }
        else
        {
            // Si no había música, intentar restaurar la música de la escena actual
            audioService.RestoreSceneMusic(fade);
        }
        
        _previousMusic = null;
        _wasMusicPlaying = false;
    }
    
    private void OnDestroy()
    {
        if (_currentActiveZone == this)
        {
            _currentActiveZone = null;
            // Restaurar inmediatamente si se destruye la zona activa
            RestoreDefaultsImmediate();
        }
    }
    
    /// <summary>
    /// Restaura los valores por defecto inmediatamente (sin transición)
    /// </summary>
    public static void RestoreDefaultsImmediate()
    {
        _currentTween?.Kill();
        
        if (!_defaultsCaptured) return;
        
        RenderSettings.fog = _defaultFogEnabled;
        RenderSettings.fogColor = _defaultFogColor;
        RenderSettings.fogDensity = _defaultFogDensity;
        RenderSettings.fogStartDistance = _defaultFogStart;
        RenderSettings.fogEndDistance = _defaultFogEnd;
        RenderSettings.fogMode = _defaultFogMode;
    }
    
    /// <summary>
    /// Fuerza una recaptura de los valores por defecto
    /// </summary>
    public static void RecaptureDefaults()
    {
        _defaultsCaptured = false;
        CaptureDefaults();
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        
        // Color semi-transparente basado en el color del fog
        Gizmos.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0.3f);
        
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Mostrar información en el editor
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
            $"FogZone: {gameObject.name}\nDensity: {fogDensity}\nPriority: {priority}");
    }
#endif
}

