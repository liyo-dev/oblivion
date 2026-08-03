using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class DayNightCycle : MonoBehaviour
{
    public enum TimeOfDay
    {
        AfterNoon,
        BrightMorning,
        Cloudy,
        EarlyDusk,
        HaloSky,
        Midnight,
        Morning,
        Night,
        Sunset
    }

    [System.Serializable]
    public class TimeOfDaySettings
    {
        public TimeOfDay timeOfDay;
        public Material skybox;

        [Header("Luz direccional")]
        public Color lightColor = Color.white;
        [Range(0f, 2f)] public float lightIntensity = 1f;
        [Range(0f, 360f)] public float sunRotationX = 50f;
        [Range(0f, 360f)] public float sunRotationY = 170f;

        [Header("Luz ambiental")]
        public Color ambientColor = new Color(0.2f, 0.2f, 0.25f);
        [Range(0f, 2f)] public float ambientIntensity = 1f;

        [Header("Niebla")]
        public Color fogColor = new Color(0.5f, 0.5f, 0.5f);
        [Range(0f, 0.1f)] public float fogDensity = 0.01f;

        [Header("Ciclo")]
        public float duration = 60f;
        [Tooltip("Si es true, este periodo SIEMPRE tendrá lluvia activa")]
        public bool forceRain = false;
        [Tooltip("Probabilidad (0-1) de que llueva 'de vez en cuando' al entrar en este periodo. Se sortea una vez por periodo, independiente de forceRain.")]
        [Range(0f, 1f)] public float rainChance = 0f;
    }

    [Header("Periodos del día")]
    [SerializeField] private TimeOfDaySettings[] timeSettings = new TimeOfDaySettings[]
    {
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Morning,
            duration = 120f,
            lightColor = new Color(1f, 0.88f, 0.68f), lightIntensity = 1.1f,
            sunRotationX = 15f, sunRotationY = 90f,
            ambientColor = new Color(0.28f, 0.3f, 0.42f), ambientIntensity = 0.8f,
            fogColor = new Color(0.68f, 0.78f, 0.92f), fogDensity = 0.008f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.BrightMorning,
            duration = 90f,
            lightColor = new Color(1f, 0.97f, 0.88f), lightIntensity = 1.4f,
            sunRotationX = 35f, sunRotationY = 120f,
            ambientColor = new Color(0.38f, 0.37f, 0.33f), ambientIntensity = 1.0f,
            fogColor = new Color(0.88f, 0.9f, 0.92f), fogDensity = 0.005f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.AfterNoon,
            duration = 120f,
            lightColor = new Color(1f, 0.97f, 0.85f), lightIntensity = 1.3f,
            sunRotationX = 65f, sunRotationY = 165f,
            ambientColor = new Color(0.4f, 0.38f, 0.33f), ambientIntensity = 1.1f,
            fogColor = new Color(0.85f, 0.85f, 0.85f), fogDensity = 0.004f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.EarlyDusk,
            duration = 60f,
            lightColor = new Color(1f, 0.78f, 0.48f), lightIntensity = 0.9f,
            sunRotationX = 82f, sunRotationY = 210f,
            ambientColor = new Color(0.36f, 0.28f, 0.22f), ambientIntensity = 0.85f,
            fogColor = new Color(0.78f, 0.65f, 0.52f), fogDensity = 0.009f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Sunset,
            duration = 45f,
            lightColor = new Color(1f, 0.5f, 0.18f), lightIntensity = 0.6f,
            sunRotationX = 96f, sunRotationY = 250f,
            ambientColor = new Color(0.35f, 0.18f, 0.1f), ambientIntensity = 0.65f,
            fogColor = new Color(0.9f, 0.55f, 0.3f), fogDensity = 0.013f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Night,
            duration = 90f,
            lightColor = new Color(0.55f, 0.65f, 1f), lightIntensity = 0.25f,
            sunRotationX = 140f, sunRotationY = 290f,
            ambientColor = new Color(0.08f, 0.08f, 0.18f), ambientIntensity = 0.45f,
            fogColor = new Color(0.08f, 0.08f, 0.18f), fogDensity = 0.016f
        },
        new TimeOfDaySettings {
            timeOfDay = TimeOfDay.Midnight,
            duration = 60f,
            lightColor = new Color(0.38f, 0.45f, 0.88f), lightIntensity = 0.12f,
            sunRotationX = 180f, sunRotationY = 355f,
            ambientColor = new Color(0.04f, 0.04f, 0.1f), ambientIntensity = 0.3f,
            fogColor = new Color(0.04f, 0.04f, 0.1f), fogDensity = 0.022f
        }
    };

    [Header("Luz direccional")]
    [SerializeField] private Light directionalLight;

    [Header("Transiciones")]
    [Tooltip("Duración de la transición entre periodos del día en segundos.")]
    [SerializeField] private float transitionDuration = 10f;
    [Tooltip("Usar transiciones suaves entre periodos (requiere más recursos).")]
    [SerializeField] private bool useSmoothTransitions = true;

    [Header("Control de entorno")]
    [Tooltip("Si es true, el ciclo controlará la luz ambiental global.")]
    [SerializeField] private bool controlAmbientLight = true;
    [Tooltip("Si es true, el ciclo controlará la niebla global.")]
    [SerializeField] private bool controlFog = true;

    [Header("Clima - Lluvia")]
    [Tooltip("Prefab del sistema de partículas de lluvia.")]
    [SerializeField] private GameObject rainPrefab;
    [Tooltip("Si es true, la lluvia dura todo el periodo. Si es false, tiene duración aleatoria.")]
    [SerializeField] private bool rainLastsWholePeriod = true;
    [Tooltip("Segundos que tardan en desaparecer las partículas al detener la lluvia.")]
    [SerializeField] private float rainFadeOutTime = 3f;

    [Header("Clima - Oscurecimiento por lluvia")]
    [Tooltip("Multiplicador de la intensidad de la luz direccional mientras llueve a tope (1 = sin cambio).")]
    [SerializeField, Range(0f, 1f)] private float rainLightIntensityMultiplier = 0.55f;
    [Tooltip("Multiplicador de la densidad de niebla mientras llueve a tope (1 = sin cambio, más alto = más densa).")]
    [SerializeField, Range(1f, 6f)] private float rainFogDensityMultiplier = 2.5f;
    [Tooltip("Color hacia el que se tiñe la niebla mientras llueve (mezclado según rainFogColorBlend).")]
    [SerializeField] private Color rainFogColorTint = new Color(0.45f, 0.47f, 0.5f);
    [Range(0f, 1f)] [SerializeField] private float rainFogColorBlend = 0.5f;
    [Tooltip("Segundos que tarda en oscurecer al empezar a llover / en aclarar de nuevo al parar.")]
    [SerializeField] private float rainDarkenTransitionDuration = 4f;

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
    private Coroutine _rainFadeCoroutine;
    private Coroutine _rainDarkenCoroutine;
    private float _rainDarkenAmount;

    // Suprime la lluvia VISUALMENTE mientras el jugador está en un interior (AnchorEnvironment.isInterior),
    // sin tocar el ciclo lógico (IsRaining, temporizadores) para que al salir se reanude si sigue lloviendo.
    private bool _rainSuppressedIndoors;

    void Awake()
    {
        if (timeSettings == null || timeSettings.Length == 0)
        {
            Debug.LogError("[DayNightCycle] No hay periodos configurados en timeSettings.");
            enabled = false;
            return;
        }

        _currentIndex = Mathf.Clamp(startingTimeIndex, 0, timeSettings.Length - 1);

        if (controlAmbientLight)
            RenderSettings.ambientMode = AmbientMode.Flat;

        if (controlFog)
            RenderSettings.fog = true;
    }

    void OnEnable()
    {
        InitializeCycle();

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        // Si ya estábamos en un interior al activarnos (p.ej. carga directa a una escena de
        // interior), arrancar ya suprimidos para no mostrar/oír lluvia un frame de más.
        var ec = EnvironmentController.Instance;
        _rainSuppressedIndoors = ec != null && ec.CurrentMode == EnvironmentMode.Interior;
    }

    void OnDisable()
    {
        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        StopAllCoroutines();
        IsRaining = false;
        _rainDarkenAmount = 0f;
        _rainDarkenCoroutine = null;
        if (_activeRainInstance != null)
        {
            Destroy(_activeRainInstance);
            _activeRainInstance = null;
        }
    }

    /// <summary>
    /// Aplica el oscurecimiento por lluvia DESPUÉS de que Update haya fijado los valores del
    /// periodo del día actual, para no pelearse con la lógica de transición existente: primero se
    /// pone el "look" base del periodo, y aquí se atenúa por encima si está lloviendo.
    /// </summary>
    void LateUpdate()
    {
        if (_rainDarkenAmount <= 0f) return;

        if (directionalLight != null)
            directionalLight.intensity *= Mathf.Lerp(1f, rainLightIntensityMultiplier, _rainDarkenAmount);

        if (controlFog)
        {
            RenderSettings.fogDensity *= Mathf.Lerp(1f, rainFogDensityMultiplier, _rainDarkenAmount);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, rainFogColorTint, _rainDarkenAmount * rainFogColorBlend);
        }
    }

    void HandleInteriorEntered()
    {
        _rainSuppressedIndoors = true;
        SetRainVisualActive(false);
    }

    void HandleInteriorExited()
    {
        _rainSuppressedIndoors = false;
        SetRainVisualActive(true);
    }

    void SetRainVisualActive(bool active)
    {
        if (_activeRainInstance != null)
            _activeRainInstance.SetActive(active);
    }

    void Update()
    {
        if (!autoAdvance || _isTransitioning) return;

        _timeElapsed += Time.deltaTime;

        if (_timeElapsed >= _currentDuration)
            AdvanceToNextPeriod();
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

    public void SetNight() => SetTimeOfDay(TimeOfDay.Night, immediate: false);

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
        if (IsRaining) StopRain();
        else StartRain();
    }

    public void StartRain(float? duration = null)
    {
        if (rainPrefab == null || IsRaining) return;

        if (_rainCoroutine != null)
            StopCoroutine(_rainCoroutine);

        float rainDuration = duration ?? 60f;
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

        BeginRainFadeOut();
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
        _currentDuration = Mathf.Max(1f, settings.duration);
        _timeElapsed = 0f;

        if (immediate || !useSmoothTransitions || !Application.isPlaying)
        {
            CurrentTimeOfDay = settings.timeOfDay;
            ApplySettingsImmediate(settings);
            if (invokeEvents)
            {
                onTimeOfDayChanged?.Invoke(settings.timeOfDay);
                TimeOfDayChanged?.Invoke(settings.timeOfDay);
            }
        }
        else
            _transitionCoroutine = StartCoroutine(TransitionToSettings(settings, invokeEvents));

        // forceRain = lluvia garantizada (usado por narrativa/tests).
        // rainChance = sorteo "de vez en cuando" cada vez que arranca el periodo, independiente de forceRain.
        bool shouldRain = settings.forceRain ||
                           (settings.rainChance > 0f && UnityEngine.Random.value < settings.rainChance);

        if (shouldRain)
            StartRain(rainLastsWholePeriod ? settings.duration : (float?)null);
        else if (IsRaining)
            StopRain();
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
            directionalLight.transform.eulerAngles = new Vector3(settings.sunRotationX, settings.sunRotationY, 0f);
        }

        if (controlAmbientLight)
            RenderSettings.ambientLight = settings.ambientColor * settings.ambientIntensity;

        if (controlFog)
        {
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogDensity = settings.fogDensity;
        }
    }

    IEnumerator TransitionToSettings(TimeOfDaySettings target, bool invokeEvents)
    {
        _isTransitioning = true;

        // El skybox cambia al inicio para que cielo y luz evolucionen juntos, evitando el "pop" al final
        if (target.skybox != null && RenderSettings.skybox != target.skybox)
        {
            RenderSettings.skybox = target.skybox;
            DynamicGI.UpdateEnvironment();
        }

        var light = directionalLight;
        Color startLightColor = light ? light.color : Color.white;
        float startIntensity = light ? light.intensity : 1f;
        float startRotX = light ? light.transform.eulerAngles.x : 0f;
        float startRotY = light ? light.transform.eulerAngles.y : 0f;
        Color startAmbient = RenderSettings.ambientLight;
        Color startFogColor = RenderSettings.fogColor;
        float startFogDensity = RenderSettings.fogDensity;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));

            if (light != null)
            {
                light.color = Color.Lerp(startLightColor, target.lightColor, t);
                light.intensity = Mathf.Lerp(startIntensity, target.lightIntensity, t);
                light.transform.eulerAngles = new Vector3(
                    Mathf.LerpAngle(startRotX, target.sunRotationX, t),
                    Mathf.LerpAngle(startRotY, target.sunRotationY, t),
                    0f
                );
            }

            if (controlAmbientLight)
                RenderSettings.ambientLight = Color.Lerp(startAmbient, target.ambientColor * target.ambientIntensity, t);

            if (controlFog)
            {
                RenderSettings.fogColor = Color.Lerp(startFogColor, target.fogColor, t);
                RenderSettings.fogDensity = Mathf.Lerp(startFogDensity, target.fogDensity, t);
            }

            yield return null;
        }

        // Asegurar valores exactos al finalizar y actualizar el estado lógico cuando la visual ya coincide
        ApplySettingsImmediate(target);
        CurrentTimeOfDay = target.timeOfDay;

        if (invokeEvents)
        {
            onTimeOfDayChanged?.Invoke(target.timeOfDay);
            TimeOfDayChanged?.Invoke(target.timeOfDay);
        }

        _isTransitioning = false;
        _transitionCoroutine = null;
    }

    IEnumerator RainRoutine(float duration)
    {
        ActivateRain();
        yield return new WaitForSeconds(duration);
        BeginRainFadeOut();
        _rainCoroutine = null;
    }

    void ActivateRain()
    {
        if (IsRaining || rainPrefab == null) return;

        // Cancelar fade-out activo antes de instanciar nueva lluvia
        if (_rainFadeCoroutine != null)
        {
            StopCoroutine(_rainFadeCoroutine);
            _rainFadeCoroutine = null;
            if (_activeRainInstance != null)
            {
                Destroy(_activeRainInstance);
                _activeRainInstance = null;
            }
        }

        Transform parent = PlayerService.Player != null ? PlayerService.Player.transform :
                           Camera.main != null ? Camera.main.transform : null;

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

        // Si el jugador ya está en un interior cuando empieza a llover, que no se vea/oiga
        // hasta que salga (evita el problema de "llueve dentro de la casa").
        if (_rainSuppressedIndoors)
            _activeRainInstance.SetActive(false);

        IsRaining = true;
        onRainStarted?.Invoke();
        RainStarted?.Invoke();
        StartRainDarken(1f);
    }

    void BeginRainFadeOut()
    {
        if (!IsRaining) return;

        IsRaining = false;
        onRainStopped?.Invoke();
        RainStopped?.Invoke();
        StartRainDarken(0f);

        if (_rainFadeCoroutine != null)
            StopCoroutine(_rainFadeCoroutine);

        if (_activeRainInstance != null)
            _rainFadeCoroutine = StartCoroutine(RainFadeOutRoutine(_activeRainInstance));
    }

    void StartRainDarken(float target)
    {
        if (_rainDarkenCoroutine != null)
            StopCoroutine(_rainDarkenCoroutine);
        _rainDarkenCoroutine = StartCoroutine(RainDarkenRoutine(target));
    }

    IEnumerator RainDarkenRoutine(float target)
    {
        float start = _rainDarkenAmount;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, rainDarkenTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _rainDarkenAmount = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        _rainDarkenAmount = target;
        _rainDarkenCoroutine = null;
    }

    IEnumerator RainFadeOutRoutine(GameObject rainInstance)
    {
        // Detener emisión para que las partículas existentes terminen de caer
        var particles = rainInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(rainFadeOutTime);

        if (rainInstance != null)
            Destroy(rainInstance);

        if (_activeRainInstance == rainInstance)
            _activeRainInstance = null;

        _rainFadeCoroutine = null;
    }

    [ContextMenu("Avanzar al siguiente periodo")]
    public void DebugAdvanceTime() => AdvanceToNextPeriod();

    [ContextMenu("Activar/Desactivar lluvia")]
    public void DebugToggleRain() => ToggleRain();
}
