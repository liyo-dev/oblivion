using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sistema avanzado de ciclo día/noche con transiciones suaves entre múltiples periodos del día,
/// usando todos los skyboxes disponibles y con soporte para efectos de clima como lluvia.
/// </summary>
[DisallowMultipleComponent]
public class DayNightCycle : MonoBehaviour
{
    public enum TimeOfDay
    {
        AfterNoon,      // SkyAfterNoon
        BrightMorning,  // SkyBrightMorning
        Cloudy,         // SkyCloudy (clima nublado)
        EarlyDusk,      // SkyEarlyDusk
        HaloSky,        // SkyHaloSky
        Midnight,       // SkyMidnight
        Morning,        // SkyMorning
        Night,          // SkyNight
        Sunset          // SkySunset
    }

    [System.Serializable]
    public class TimeOfDaySettings
    {
        public TimeOfDay timeOfDay;
        public Material skybox;
        public Color lightColor = Color.white;
        [Range(0f, 2f)] public float lightIntensity = 1f;
        [Range(0f, 360f)] public float sunRotationX = 50f; // Rotación del sol
        public float duration = 60f; // Duración en segundos
        [Tooltip("Si es true, este periodo SIEMPRE tendrá lluvia activa")]
        public bool forceRain = false;
    }

    [Header("Periodos del día")]
    [SerializeField] private TimeOfDaySettings[] timeSettings = new TimeOfDaySettings[]
    {
        new TimeOfDaySettings { timeOfDay = TimeOfDay.Morning, duration = 120f, lightIntensity = 1.2f, sunRotationX = 50f },
        new TimeOfDaySettings { timeOfDay = TimeOfDay.BrightMorning, duration = 90f, lightIntensity = 1.4f, sunRotationX = 60f },
        new TimeOfDaySettings { timeOfDay = TimeOfDay.AfterNoon, duration = 120f, lightIntensity = 1.3f, sunRotationX = 80f },
        new TimeOfDaySettings { timeOfDay = TimeOfDay.EarlyDusk, duration = 60f, lightIntensity = 0.9f, sunRotationX = 100f },
        new TimeOfDaySettings { timeOfDay = TimeOfDay.Sunset, duration = 45f, lightIntensity = 0.6f, sunRotationX = 110f },
        new TimeOfDaySettings { timeOfDay = TimeOfDay.Night, duration = 90f, lightIntensity = 0.3f, sunRotationX = 140f },
        new TimeOfDaySettings { timeOfDay = TimeOfDay.Midnight, duration = 60f, lightIntensity = 0.2f, sunRotationX = 180f }
    };

    [Header("Luz direccional")]
    [SerializeField] private Light directionalLight;

    [Header("Transiciones")]
    [Tooltip("Duración de la transición entre periodos del día en segundos.")]
    [SerializeField] private float transitionDuration = 10f;
    
    [Tooltip("Usar transiciones suaves entre skyboxes (requiere más recursos).")]
    [SerializeField] private bool useSmoothTransitions = true;

    [Header("Clima - Lluvia")]
    [Tooltip("Prefab del sistema de partículas de lluvia.")]
    [SerializeField] private GameObject rainPrefab;
    
    [Tooltip("Si es true, la lluvia dura todo el periodo. Si es false, tiene duración aleatoria.")]
    [SerializeField] private bool rainLastsWholePeriod = true;

    [Header("Ciclo")]
    [Tooltip("Si es falso, no avanza automáticamente el ciclo.")]
    [SerializeField] private bool autoAdvance = true;
    
    [Tooltip("Índice del periodo inicial (0 = primero en la lista).")]
    [SerializeField] private int startingTimeIndex = 0;

    [Header("Eventos")]
    [SerializeField] private UnityEvent<TimeOfDay> onTimeOfDayChanged;
    [SerializeField] private UnityEvent onRainStarted;
    [SerializeField] private UnityEvent onRainStopped;

    public event Action<TimeOfDay> TimeOfDayChanged;
    public event Action RainStarted;
    public event Action RainStopped;

    public TimeOfDay CurrentTimeOfDay { get; private set; }
    public bool IsRaining { get; private set; }
    public float TimeProgress => _currentDuration > 0 ? Mathf.Clamp01(_timeElapsed / _currentDuration) : 1f;

    private int _currentIndex;
    private float _timeElapsed;
    private float _currentDuration;
    private bool _isTransitioning;
    private GameObject _activeRainInstance;
    private Coroutine _rainCoroutine;
    private Coroutine _transitionCoroutine;

    // Para transiciones de skybox suaves
    private Material _transitionMaterial;
    private Material _previousSkybox;
    private Material _targetSkybox;

    void Awake()
    {
        // Validar configuración
        if (timeSettings == null || timeSettings.Length == 0)
        {
            Debug.LogError("[DayNightCycle] No hay periodos configurados en timeSettings.");
            enabled = false;
            return;
        }

        _currentIndex = Mathf.Clamp(startingTimeIndex, 0, timeSettings.Length - 1);
    }

    void OnEnable()
    {
        InitializeCycle();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (_activeRainInstance != null)
        {
            Destroy(_activeRainInstance);
            _activeRainInstance = null;
        }
    }

    void Update()
    {
        if (!autoAdvance || _isTransitioning) return;

        _timeElapsed += Time.deltaTime;
        
        if (_timeElapsed >= _currentDuration)
        {
            AdvanceToNextPeriod();
        }
    }

    void InitializeCycle()
    {
        ApplyTimeOfDay(_currentIndex, immediate: true, invokeEvents: false);
    }

    public void AdvanceToNextPeriod()
    {
        int nextIndex = (_currentIndex + 1) % timeSettings.Length;
        ApplyTimeOfDay(nextIndex, immediate: false, invokeEvents: true);
    }

    public void SetTimeOfDay(TimeOfDay timeOfDay, bool immediate = false)
    {
        for (int i = 0; i < timeSettings.Length; i++)
        {
            if (timeSettings[i].timeOfDay == timeOfDay)
            {
                ApplyTimeOfDay(i, immediate, invokeEvents: true);
                return;
            }
        }
        Debug.LogWarning($"[DayNightCycle] TimeOfDay '{timeOfDay}' no encontrado en la configuración.");
    }

    // Método sin parámetros para invocar desde UnityEvent y forzar noche
    public void SetNight()
    {
        SetTimeOfDay(TimeOfDay.Night, immediate: false);
    }

    public void SetTimeOfDayByIndex(int index, bool immediate = false)
    {
        if (index < 0 || index >= timeSettings.Length)
        {
            Debug.LogWarning($"[DayNightCycle] Índice {index} fuera de rango.");
            return;
        }
        ApplyTimeOfDay(index, immediate, invokeEvents: true);
    }

    public void ToggleRain()
    {
        if (IsRaining)
            StopRain();
        else
            StartRain();
    }

    public void StartRain(float? duration = null)
    {
        if (IsRaining || rainPrefab == null) return;

        if (_rainCoroutine != null)
            StopCoroutine(_rainCoroutine);

        float rainDuration = duration ?? 60f; // Duración por defecto si no se especifica
        _rainCoroutine = StartCoroutine(RainRoutine(rainDuration));
    }

    public void StopRain()
    {
        if (!IsRaining) return;

        if (_rainCoroutine != null)
        {
            StopCoroutine(_rainCoroutine);
            _rainCoroutine = null;
        }

        DeactivateRain();
    }

    void ApplyTimeOfDay(int index, bool immediate, bool invokeEvents)
    {
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
            _isTransitioning = false;
        }

        _currentIndex = index;
        var settings = timeSettings[index];
        CurrentTimeOfDay = settings.timeOfDay;
        _currentDuration = Mathf.Max(1f, settings.duration);
        _timeElapsed = 0f;

        if (immediate || !useSmoothTransitions || !Application.isPlaying)
        {
            ApplySettingsImmediate(settings);
        }
        else
        {
            _transitionCoroutine = StartCoroutine(TransitionToSettings(settings));
        }

        // Manejar lluvia: si el periodo fuerza lluvia (ej: Cloudy), activarla
        if (settings.forceRain)
        {
            if (rainLastsWholePeriod)
            {
                StartRain(settings.duration);
            }
            else
            {
                StartRain();
            }
        }
        else if (IsRaining)
        {
            // Si cambió a un periodo sin lluvia, detenerla
            StopRain();
        }

        if (invokeEvents)
        {
            onTimeOfDayChanged?.Invoke(settings.timeOfDay);
            TimeOfDayChanged?.Invoke(settings.timeOfDay);
        }
    }

    void ApplySettingsImmediate(TimeOfDaySettings settings)
    {
        if (settings.skybox != null)
        {
            RenderSettings.skybox = settings.skybox;
            DynamicGI.UpdateEnvironment();
        }

        if (directionalLight != null)
        {
            directionalLight.color = settings.lightColor;
            directionalLight.intensity = settings.lightIntensity;
            
            Vector3 rotation = directionalLight.transform.eulerAngles;
            rotation.x = settings.sunRotationX;
            directionalLight.transform.eulerAngles = rotation;
        }
    }

    IEnumerator TransitionToSettings(TimeOfDaySettings targetSettings)
    {
        _isTransitioning = true;

        var light = directionalLight;
        Material startSkybox = RenderSettings.skybox;
        Color startColor = light ? light.color : Color.white;
        float startIntensity = light ? light.intensity : 1f;
        float startRotationX = light ? light.transform.eulerAngles.x : 0f;

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (light != null)
            {
                light.color = Color.Lerp(startColor, targetSettings.lightColor, smoothT);
                light.intensity = Mathf.Lerp(startIntensity, targetSettings.lightIntensity, smoothT);

                Vector3 rotation = light.transform.eulerAngles;
                rotation.x = Mathf.LerpAngle(startRotationX, targetSettings.sunRotationX, smoothT);
                light.transform.eulerAngles = rotation;
            }

            yield return null;
        }

        // Aplicar skybox y GI una sola vez al finalizar para evitar parpadeos
        if (targetSettings.skybox != null && RenderSettings.skybox != targetSettings.skybox)
        {
            RenderSettings.skybox = targetSettings.skybox;
            DynamicGI.UpdateEnvironment();
        }

        ApplySettingsImmediate(targetSettings);

        _isTransitioning = false;
        _transitionCoroutine = null;
    }

    IEnumerator RainRoutine(float duration)
    {
        ActivateRain();
        yield return new WaitForSeconds(duration);
        DeactivateRain();
        _rainCoroutine = null;
    }

    void ActivateRain()
    {
        if (IsRaining || rainPrefab == null) return;

        // Instanciar lluvia como hijo del jugador
        Transform parent = null;
        if (PlayerService.Player != null)
        {
            parent = PlayerService.Player.transform;
        }
        else if (Camera.main != null)
        {
            parent = Camera.main.transform;
        }

        if (parent != null)
        {
            _activeRainInstance = Instantiate(rainPrefab, parent);
            _activeRainInstance.transform.localPosition = Vector3.zero;
        }
        else
        {
            _activeRainInstance = Instantiate(rainPrefab, transform.position, Quaternion.identity);
            Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, lluvia instanciada sin padre.");
        }

        IsRaining = true;
        onRainStarted?.Invoke();
        RainStarted?.Invoke();
        
        Debug.Log("[DayNightCycle] Lluvia iniciada");
    }

    void DeactivateRain()
    {
        if (!IsRaining) return;

        if (_activeRainInstance != null)
        {
            Destroy(_activeRainInstance);
            _activeRainInstance = null;
        }

        IsRaining = false;
        onRainStopped?.Invoke();
        RainStopped?.Invoke();
        
        Debug.Log("[DayNightCycle] Lluvia detenida");
    }

    // Métodos públicos para debug/testing
    [ContextMenu("Avanzar al siguiente periodo")]
    public void DebugAdvanceTime()
    {
        AdvanceToNextPeriod();
    }

    [ContextMenu("Activar/Desactivar lluvia")]
    public void DebugToggleRain()
    {
        ToggleRain();
    }
}
