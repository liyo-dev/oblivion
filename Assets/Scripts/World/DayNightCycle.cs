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
        [Tooltip("Probabilidad (0-1) de que aparezca niebla espesa 'de vez en cuando' al entrar en este periodo, independiente de la lluvia. Solo se sortea si no está lloviendo.")]
        [Range(0f, 1f)] public float fogChance = 0f;
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

    [Header("Clima - Nubosidad previa a la lluvia")]
    [Tooltip("OPCIONAL. Skybox de cielo nublado/tormenta que se muestra mientras el cielo se cubre de nubes, antes de que empiece a llover. Si es null (recomendado si usas CloudCoverSpawner para nubes 3D reales), el skybox nunca cambia: el cielo real de fondo sigue teniendo sol, así que si el jugador vuela por encima del techo de nubes lo verá despejado, como pasaría de verdad. Solo asigna esto si prefieres un cambio de skybox global en vez de (o además de) las nubes 3D.")]
    [SerializeField] private Material stormSkybox;

    [Header("Clima - Oscurecimiento por lluvia")]
    [Tooltip("Multiplicador de la intensidad de la luz direccional mientras llueve a tope (1 = sin cambio).")]
    [SerializeField, Range(0f, 1f)] private float rainLightIntensityMultiplier = 0.55f;
    [Tooltip("Multiplicador de la densidad de niebla mientras llueve a tope (1 = sin cambio, más alto = más densa).")]
    [SerializeField, Range(1f, 6f)] private float rainFogDensityMultiplier = 2.5f;
    [Tooltip("Color hacia el que se tiñe la niebla mientras llueve (mezclado según rainFogColorBlend).")]
    [SerializeField] private Color rainFogColorTint = new Color(0.45f, 0.47f, 0.5f);
    [Range(0f, 1f)] [SerializeField] private float rainFogColorBlend = 0.5f;
    [Tooltip("Suelo ABSOLUTO de intensidad de la luz direccional mientras llueve a tope. Sin esto, rainLightIntensityMultiplier se aplica sobre la intensidad que ya tenga el periodo actual, así que un periodo ya oscuro (Night, Midnight, Sunset) puede quedarse en negro casi total. Con este suelo, la luz nunca baja de este valor por mucho que se multiplique.")]
    [SerializeField, Range(0f, 1f)] private float rainMinLightIntensity = 0.28f;
    [Tooltip("Segundos que tarda el cielo en nublarse (oscurecer + espesar niebla + cambiar a stormSkybox) ANTES de que arranque la lluvia, y lo que tarda en despejarse otra vez al terminar. La lluvia no empieza a caer hasta que termina esta transición.")]
    [SerializeField] private float rainDarkenTransitionDuration = 4f;

    [Header("Clima - Niebla ocasional")]
    [Tooltip("Prefab opcional de niebla volumétrica (partículas) para el evento de niebla ocasional. Si es null, solo se espesa la niebla global (RenderSettings.fog), sin partículas.")]
    [SerializeField] private GameObject mistPrefab;
    [Tooltip("Si es true, la niebla ocasional dura todo el periodo. Si es false, tiene duración aleatoria dentro de mistDurationRange.")]
    [SerializeField] private bool mistLastsWholePeriod = false;
    [Tooltip("Duración aleatoria (min, max) en segundos de la niebla ocasional cuando no dura todo el periodo.")]
    [SerializeField] private Vector2 mistDurationRange = new Vector2(20f, 45f);
    [Tooltip("Segundos que tardan en desaparecer las partículas al detener la niebla ocasional.")]
    [SerializeField] private float mistFadeOutTime = 4f;
    [Tooltip("Multiplicador de la densidad de niebla mientras está activa la niebla ocasional a tope (1 = sin cambio).")]
    [SerializeField, Range(1f, 8f)] private float mistFogDensityMultiplier = 4f;
    [Tooltip("Segundos que tarda en espesar/disipar la niebla ocasional.")]
    [SerializeField] private float mistTransitionDuration = 8f;

    [Header("Clima - Audio (grafo sonoro)")]
    [Tooltip("Event Key del AudioGraphProfile (lista 'Event Sfx') que se reproduce cuando el cielo empieza a nublarse, ANTES de que arranque la lluvia. Déjalo vacío para no reproducir nada.")]
    [SerializeField] private string cloudsBuildingUpSfxKey;
    [Tooltip("Event Key del AudioGraphProfile del SFX/ambiente de lluvia. Se reproduce en LOOP (vía AudioService.PlayLoopingSFX) desde que arranca la lluvia de verdad hasta que para, no como one-shot: así no importa si el clip asignado es más largo o más corto que la lluvia real.")]
    [SerializeField] private string rainStartedSfxKey;
    [Tooltip("Event Key opcional del AudioGraphProfile para un one-shot adicional cuando para de llover (p.ej. un cue corto de viento amainando). El loop de rainStartedSfxKey se detiene siempre, tenga o no clave este campo.")]
    [SerializeField] private string rainStoppedSfxKey;
    /// <summary>Clave interna usada en AudioService.PlayLoopingSFX/StopLoopingSFX para el loop de ambiente de lluvia.</summary>
    const string RainWeatherSfxLoopId = "Weather_Rain";
    [Tooltip("Event Key del AudioGraphProfile que se reproduce al empezar un evento de niebla ocasional.")]
    [SerializeField] private string mistStartedSfxKey;
    [Tooltip("Event Key del AudioGraphProfile que se reproduce al disiparse la niebla ocasional.")]
    [SerializeField] private string mistStoppedSfxKey;

    [Header("Ciclo")]
    [Tooltip("Si es falso, no avanza automáticamente el ciclo.")]
    [SerializeField] private bool autoAdvance = true;
    [Tooltip("Índice del periodo inicial (0 = primero en la lista).")]
    [SerializeField] private int startingTimeIndex = 0;

    [Header("Eventos")]
    [SerializeField] private UnityEvent<TimeOfDay> onTimeOfDayChanged;
    [SerializeField] private UnityEvent onCloudsBuildingUp;
    [SerializeField] private UnityEvent onRainStarted;
    [SerializeField] private UnityEvent onRainStopped;
    [SerializeField] private UnityEvent onMistStarted;
    [SerializeField] private UnityEvent onMistStopped;

    public event Action<TimeOfDay> TimeOfDayChanged;
    /// <summary>Se dispara al empezar a nublarse el cielo, ANTES de que arranque la lluvia. Útil para SFX de viento/truenos.</summary>
    public event Action CloudsBuildingUp;
    public event Action RainStarted;
    public event Action RainStopped;
    public event Action MistStarted;
    public event Action MistStopped;

    public TimeOfDay CurrentTimeOfDay { get; private set; }
    public bool IsRaining { get; private set; }
    public bool IsMisty { get; private set; }
    public float TimeProgress => _currentDuration > 0 ? Mathf.Clamp01(_timeElapsed / _currentDuration) : 1f;

    /// <summary>Segundos que tarda el cielo en nublarse/despejarse. Expuesto para que sistemas
    /// externos (p.ej. un spawner de nubes 3D) sincronicen su propio fundido con este tiempo.</summary>
    public float RainDarkenTransitionDuration => rainDarkenTransitionDuration;

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

    // True mientras el cielo se está nublando (skybox de tormenta + oscurecimiento) pero la lluvia
    // todavía no ha empezado a caer (IsRaining sigue en false hasta que termina la transición).
    private bool _isCloudBuildingUp;
    // Skybox que había activo justo antes de aplicar stormSkybox, para poder restaurarlo si ninguna
    // transición de periodo lo ha pisado mientras tanto.
    private Material _preStormSkybox;

    // Cámara principal cacheada en Awake (nunca Camera.main en Update/LateUpdate). Se usa como red
    // de seguridad: si no hay stormSkybox asignado, el skybox despejado (con sol/rayos) sigue
    // siendo visible más allá del radio de CloudCoverSpawner, y RenderSettings.fog NO tiñe el
    // skybox por mucho que se suba la densidad. Forzamos entonces Clear Flags a color sólido
    // (tintado igual que la niebla de lluvia) mientras dura la tormenta, para que el horizonte se
    // vea completamente cubierto sin necesidad de crear un material de skybox nuevo.
    private Camera _mainCamera;
    private CameraClearFlags _preStormClearFlags;
    private Color _preStormBackgroundColor;
    private bool _cameraOverrideActive;

    private GameObject _activeMistInstance;
    private Coroutine _mistCoroutine;
    private Coroutine _mistFadeCoroutine;
    private Coroutine _mistDarkenCoroutine;
    private float _mistAmount;

    // Niebla "base" del periodo actual (sin el oscurecimiento de lluvia/niebla ocasional aplicado).
    // LateUpdate() recalcula el fog final SIEMPRE a partir de esta base, en vez de multiplicar el
    // RenderSettings.fogDensity ya mutado del frame anterior — eso causaba un crecimiento exponencial
    // frame a frame (density *= multiplier cada frame) que saturaba la pantalla entera de niebla en
    // cuestión de segundos.
    private float _baseFogDensity;
    private Color _baseFogColor;

    // Suprime la lluvia y la niebla VISUALMENTE mientras el jugador está en un interior
    // (AnchorEnvironment.isInterior), sin tocar el ciclo lógico (IsRaining/IsMisty, temporizadores)
    // para que al salir se reanuden si el clima sigue activo.
    // OJO: esto solo se actualiza vía OnInteriorEntered/OnInteriorExited, que EnvironmentController
    // dispara únicamente desde ApplyInterior/ApplyExterior (el flujo "real" de entrar/salir andando).
    // NO se actualiza durante un override cinemático (BeginCinematicOverride + ApplyInteriorForCinematic,
    // ver CinematicSequencerBase/SimpleCinematicDirector), porque ese flujo no toca _mode a propósito.
    // Por eso IsSkyboxLockedByEnvironment() de abajo comprueba también IsCinematicOverrideActive.
    private bool _outdoorWeatherSuppressedIndoors;

    // Para detectar cuándo un override cinemático termina y poder re-aplicar el skybox correcto
    // (periodo actual o tormenta) que se haya quedado pendiente mientras estaba bloqueado.
    private bool _wasCinematicOverrideActive;

    // Para detectar el inicio de un minijuego (TagMinigameController.IsAnyMinigameActive) y cortar
    // la lluvia que ya estuviera cayendo en ese instante. StartRain() ya bloquea que arranque lluvia
    // NUEVA mientras haya un minijuego activo, pero eso no cubre el caso de que empezara a llover
    // justo antes de que el jugador entrara en el minijuego.
    private bool _wasMinigameActive;

    void Awake()
    {
        if (timeSettings == null || timeSettings.Length == 0)
        {
            Debug.LogError("[DayNightCycle] No hay periodos configurados en timeSettings.");
            enabled = false;
            return;
        }

        _currentIndex = Mathf.Clamp(startingTimeIndex, 0, timeSettings.Length - 1);

        _mainCamera = Camera.main;

        if (controlAmbientLight)
            RenderSettings.ambientMode = AmbientMode.Flat;

        // Si controlFog está desactivado, forzamos el fog a apagado en vez de dejarlo tal cual
        // estuviera horneado en la escena — así "desactivar niebla" en el Inspector apaga de
        // verdad la niebla, en lugar de depender de lo último que hubiera en Lighting Settings.
        RenderSettings.fog = controlFog;
    }

    void OnEnable()
    {
        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        // Si ya estábamos en un interior al activarnos (p.ej. carga directa a una escena de
        // interior), arrancar ya suprimidos para no mostrar/oír lluvia un frame de más.
        var ec = EnvironmentController.Instance;
        _outdoorWeatherSuppressedIndoors = ec != null && ec.CurrentMode == EnvironmentMode.Interior;

        // Un frame de margen antes de aplicar el periodo inicial (y, si forceRain/rainChance
        // dispara lluvia ya en ese primer periodo, antes de arrancarla). Si esta escena se abre y
        // se le da Play directamente (AutoBootstrapOnPlay carga 'Start' aditivamente ANTES de
        // entrar en PlayMode, ver Editor/AutoBootstrapOnPlay.cs), el orden de Awake/OnEnable entre
        // la escena 'Start' y esta escena no está garantizado, y tampoco lo está el orden relativo
        // a otros scripts de esta misma escena con prioridad por defecto (0), como
        // CloudCoverSpawner. Si InitializeCycle() corriera ya mismo de forma síncrona:
        //  - CloudCoverSpawner.OnEnable() podría no haberse suscrito aún a CloudsBuildingUp/
        //    RainStopped → el evento se dispara al vacío y el techo de nubes nunca aparece.
        //  - AudioService.Awake() (en 'Start') podría no haber corrido aún → AudioService.Instance
        //    sigue siendo null y PlayLoopingSFX/PlaySFX no hacen nada.
        // Resultado: "le doy a Play y sale lluvia directamente pero sin nubes y sin sfx". Esperar
        // un frame garantiza que todos los Awake/OnEnable de la carga inicial ya han corrido (mismo
        // patrón que usa WorldBootstrap.InitializeWorldDelayed / AmbientZone.CheckInitialOverlapNextFrame).
        StartCoroutine(InitializeCycleDelayed());
    }

    IEnumerator InitializeCycleDelayed()
    {
        yield return null;
        InitializeCycle();
    }

    void OnDisable()
    {
        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        StopAllCoroutines();
        IsRaining = false;
        _isCloudBuildingUp = false;
        _rainDarkenAmount = 0f;
        _rainDarkenCoroutine = null;
        _preStormSkybox = null;
        if (_cameraOverrideActive && _mainCamera != null)
        {
            _mainCamera.clearFlags = _preStormClearFlags;
            _mainCamera.backgroundColor = _preStormBackgroundColor;
            _cameraOverrideActive = false;
        }
        if (_activeRainInstance != null)
        {
            Destroy(_activeRainInstance);
            _activeRainInstance = null;
        }

        IsMisty = false;
        _mistAmount = 0f;
        _mistDarkenCoroutine = null;
        if (_activeMistInstance != null)
        {
            Destroy(_activeMistInstance);
            _activeMistInstance = null;
        }
    }

    /// <summary>
    /// Aplica el oscurecimiento por lluvia DESPUÉS de que Update haya fijado los valores del
    /// periodo del día actual, para no pelearse con la lógica de transición existente: primero se
    /// pone el "look" base del periodo, y aquí se atenúa por encima si está lloviendo.
    /// </summary>
    void LateUpdate()
    {
        if (_rainDarkenAmount <= 0f && _mistAmount <= 0f) return;

        if (_rainDarkenAmount > 0f && directionalLight != null)
        {
            float baseIntensity = directionalLight.intensity;
            float darkened = baseIntensity * rainLightIntensityMultiplier;
            // Suelo absoluto: en periodos ya oscuros (Night, Midnight, Sunset...) el multiplicador
            // por sí solo puede dejar la luz casi a cero. Nunca baja de rainMinLightIntensity.
            float floored = Mathf.Max(darkened, rainMinLightIntensity);
            directionalLight.intensity = Mathf.Lerp(baseIntensity, floored, _rainDarkenAmount);
        }

        if (controlFog)
        {
            // Siempre se parte de la densidad/color BASE del periodo actual, nunca del valor ya
            // escrito en RenderSettings el frame anterior (eso era lo que compondía exponencialmente).
            float density = _baseFogDensity;
            Color color   = _baseFogColor;

            if (_rainDarkenAmount > 0f)
            {
                density *= Mathf.Lerp(1f, rainFogDensityMultiplier, _rainDarkenAmount);
                color    = Color.Lerp(color, rainFogColorTint, _rainDarkenAmount * rainFogColorBlend);
            }

            if (_mistAmount > 0f)
                density *= Mathf.Lerp(1f, mistFogDensityMultiplier, _mistAmount);

            RenderSettings.fogDensity = density;
            RenderSettings.fogColor   = color;
        }
    }

    void HandleInteriorEntered()
    {
        _outdoorWeatherSuppressedIndoors = true;
        SetRainVisualActive(false);
        SetMistVisualActive(false);
    }

    void HandleInteriorExited()
    {
        _outdoorWeatherSuppressedIndoors = false;
        SetRainVisualActive(true);
        SetMistVisualActive(true);

        // Si la tormenta arrancó mientras estábamos dentro, o cambió el periodo del día,
        // ApplyStormSkybox()/ApplySettingsImmediate()/TransitionToSettings() se saltaron el cambio
        // de skybox (ver IsSkyboxLockedByEnvironment). Al volver a exterior hay que aplicarlo ahora,
        // si no el cielo se queda con el look de antes de entrar aunque haya cambiado mientras dentro.
        ReapplyPendingSkybox();
    }

    void SetRainVisualActive(bool active)
    {
        if (_activeRainInstance != null)
            _activeRainInstance.SetActive(active);
    }

    void SetMistVisualActive(bool active)
    {
        if (_activeMistInstance != null)
            _activeMistInstance.SetActive(active);
    }

    /// <summary>
    /// Reproduce un SFX por Event Key del AudioGraphProfile (AudioService.PlaySFX), si hay una
    /// clave configurada. Llamada directa a AudioService.Instance, igual que hace AmbientZone
    /// para sus propios sonidos de ambiente.
    /// </summary>
    void PlayWeatherSfx(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey)) return;
        AudioService.Instance?.PlaySFX(eventKey);
    }

    void RaiseCloudsBuildingUp()
    {
        onCloudsBuildingUp?.Invoke();
        CloudsBuildingUp?.Invoke();
        PlayWeatherSfx(cloudsBuildingUpSfxKey);
    }

    void Update()
    {
        // Detectar el final de un override cinemático (cinemática en un interior vía
        // CinematicSequencerBase/SimpleCinematicDirector) para re-aplicar aquí el skybox que se
        // haya quedado pendiente (ver IsSkyboxLockedByEnvironment). EnvironmentController no avisa
        // de esto con un evento, así que se sondea igual que hace el propio EnvironmentController
        // con su _cinematicReapplyPending.
        var ec = EnvironmentController.Instance;
        bool cinematicActiveNow = ec != null && ec.IsCinematicOverrideActive;
        if (_wasCinematicOverrideActive && !cinematicActiveNow)
            ReapplyPendingSkybox();
        _wasCinematicOverrideActive = cinematicActiveNow;

        // FIX: la supresión visual de lluvia/niebla (_outdoorWeatherSuppressedIndoors) solo se
        // actualizaba vía OnInteriorEntered/OnInteriorExited, que EnvironmentController dispara
        // únicamente desde ApplyInterior/ApplyExterior (entrar andando). El flujo cinemático
        // (CinematicSequencerBase → BeginCinematicOverride + ApplyInteriorForCinematic, ver p.ej.
        // TabernaSequencer) nunca pasa por ahí, así que si llovía justo al entrar en una cinemática
        // de interior, la lluvia seguía cayendo "dentro" durante toda la secuencia. Sondeamos aquí
        // IsEffectivelyInterior (que sí tiene en cuenta el override cinemático) y sincronizamos la
        // supresión con el mismo patrón edge-triggered que ya usa el resto de este método.
        bool effectivelyInteriorNow = ec != null && ec.IsEffectivelyInterior;
        if (effectivelyInteriorNow != _outdoorWeatherSuppressedIndoors)
        {
            _outdoorWeatherSuppressedIndoors = effectivelyInteriorNow;
            SetRainVisualActive(!effectivelyInteriorNow);
            SetMistVisualActive(!effectivelyInteriorNow);
            if (!effectivelyInteriorNow) ReapplyPendingSkybox();
        }

        // Minijuegos: durante un minijuego activo no puede llover (ver StartRain). Ese guard
        // solo bloquea lluvia NUEVA; aquí cortamos también la que ya estuviera cayendo justo al
        // entrar en el minijuego. Sondeo edge-triggered porque IsAnyMinigameActive es un flag
        // estático sin evento propio de inicio/fin (mismo patrón que el resto de este método).
        bool minigameActiveNow = TagMinigameController.IsAnyMinigameActive;
        if (minigameActiveNow && !_wasMinigameActive && (IsRaining || _isCloudBuildingUp))
            StopRain();
        _wasMinigameActive = minigameActiveNow;

        if (!autoAdvance || _isTransitioning) return;

        _timeElapsed += Time.deltaTime;

        if (_timeElapsed >= _currentDuration)
            AdvanceToNextPeriod();
    }

    /// <summary>
    /// True mientras algo ajeno al ciclo día/noche debe tener el control exclusivo de
    /// RenderSettings.skybox: el jugador está físicamente en un interior (AnchorEnvironment), o hay
    /// una cinemática con override activo (BeginCinematicOverride, típicamente con un anchor de
    /// interior propio vía ApplyInteriorForCinematic). En ambos casos escribir el skybox del periodo
    /// o de la tormenta aquí pisaría lo que EnvironmentController ya está mostrando — el bug de
    /// "sale un azul de fondo en medio de la secuencia" era justo esto: la transición de periodo o el
    /// inicio de lluvia ignoraban por completo el override cinemático.
    /// </summary>
    bool IsSkyboxLockedByEnvironment()
    {
        return _outdoorWeatherSuppressedIndoors
            || (EnvironmentController.Instance != null && EnvironmentController.Instance.IsCinematicOverrideActive);
    }

    /// <summary>
    /// Re-aplica el skybox correcto (tormenta si está lloviendo/nublando, si no el del periodo
    /// actual) cuando algo que lo tenía bloqueado (interior real o cinemática) deja de bloquearlo.
    /// </summary>
    void ReapplyPendingSkybox()
    {
        if (IsSkyboxLockedByEnvironment()) return; // seguimos bloqueados por otro motivo, no tocar

        if (IsRaining || _isCloudBuildingUp)
        {
            ApplyStormSkybox();
        }
        else if (timeSettings[_currentIndex].skybox != null && RenderSettings.skybox != timeSettings[_currentIndex].skybox)
        {
            RenderSettings.skybox = timeSettings[_currentIndex].skybox;
            DynamicGI.UpdateEnvironment();
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
        if (IsRaining || _isCloudBuildingUp) StopRain();
        else StartRain();
    }

    /// <summary>
    /// Arranca la lluvia. Por defecto, primero se nubla el cielo (skybox de tormenta +
    /// oscurecimiento, ver rainDarkenTransitionDuration) y solo cuando termina esa transición
    /// empiezan a caer las partículas. Con immediate=true (carga de escena / test mode) se salta
    /// la nubosidad previa y la lluvia queda activa desde el primer frame.
    /// </summary>
    public void StartRain(float? duration = null, bool immediate = false)
    {
        if (rainPrefab == null || IsRaining || _isCloudBuildingUp) return;

        // Durante los minijuegos no puede llover (p.ej. TagMinigameController): bloqueamos aquí
        // cualquier intento de arrancar lluvia, tanto el sorteo automático de ApplyTimeOfDay como
        // una llamada manual/narrativa a StartRain o ToggleRain.
        if (TagMinigameController.IsAnyMinigameActive) return;

        if (_rainCoroutine != null)
            StopCoroutine(_rainCoroutine);

        float rainDuration = duration ?? 60f;
        _rainCoroutine = StartCoroutine(RainRoutine(rainDuration, immediate));
    }

    public void StopRain()
    {
        if (!IsRaining && !_isCloudBuildingUp) return;

        if (_rainCoroutine != null)
        {
            StopCoroutine(_rainCoroutine);
            _rainCoroutine = null;
        }

        if (_isCloudBuildingUp)
        {
            // Se canceló mientras el cielo aún se estaba nublando: la lluvia nunca llegó a caer,
            // así que solo revertimos el look sin disparar onRainStarted/onRainStopped.
            _isCloudBuildingUp = false;
            RevertStormSkybox();
            StartRainDarken(0f);
            return;
        }

        BeginRainFadeOut();
    }

    public void ToggleMist()
    {
        if (IsMisty) StopMist();
        else StartMist();
    }

    /// <summary>Arranca un evento de niebla ocasional, independiente de la lluvia.</summary>
    public void StartMist(float? duration = null)
    {
        if (IsMisty) return;

        if (_mistCoroutine != null)
            StopCoroutine(_mistCoroutine);

        float mistDuration = duration ?? UnityEngine.Random.Range(mistDurationRange.x, mistDurationRange.y);
        _mistCoroutine = StartCoroutine(MistRoutine(mistDuration));
    }

    public void StopMist()
    {
        if (!IsMisty) return;

        if (_mistCoroutine != null)
        {
            StopCoroutine(_mistCoroutine);
            _mistCoroutine = null;
        }

        BeginMistFadeOut();
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
            StartRain(rainLastsWholePeriod ? settings.duration : (float?)null, immediate);
        else if (IsRaining || _isCloudBuildingUp)
            StopRain();

        // Niebla ocasional: no se sortea si ya va a llover (la lluvia ya espesa la niebla por su
        // cuenta vía rainFogDensityMultiplier) para no solapar los dos efectos.
        bool shouldMist = !shouldRain && settings.fogChance > 0f && UnityEngine.Random.value < settings.fogChance;

        if (shouldMist)
            StartMist(mistLastsWholePeriod ? settings.duration : (float?)null);
        else if (IsMisty)
            StopMist();
    }

    void ApplySettingsImmediate(TimeOfDaySettings settings)
    {
        // No pisar el skybox si un interior (real o cinemático) tiene el control ahora mismo — ver
        // IsSkyboxLockedByEnvironment. Se re-aplicará solo al salir/terminar (ReapplyPendingSkybox).
        if (settings.skybox != null && !IsSkyboxLockedByEnvironment())
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
            _baseFogColor = settings.fogColor;
            _baseFogDensity = settings.fogDensity;
        }
    }

    IEnumerator TransitionToSettings(TimeOfDaySettings target, bool invokeEvents)
    {
        _isTransitioning = true;

        // El skybox cambia al inicio para que cielo y luz evolucionen juntos, evitando el "pop" al final.
        // No pisar el skybox si un interior (real o cinemático) tiene el control ahora mismo.
        if (target.skybox != null && RenderSettings.skybox != target.skybox && !IsSkyboxLockedByEnvironment())
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
        // Partimos de la base guardada, no de RenderSettings.fogColor/fogDensity: si hay lluvia o
        // niebla ocasional activa en ese instante, esos valores ya están inflados por LateUpdate()
        // y arrastrarían el multiplicador a la transición.
        Color startFogColor = _baseFogColor;
        float startFogDensity = _baseFogDensity;

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
                Color lerpedColor   = Color.Lerp(startFogColor, target.fogColor, t);
                float lerpedDensity = Mathf.Lerp(startFogDensity, target.fogDensity, t);

                // Actualizamos la base ya, no solo RenderSettings: si en este mismo frame hay
                // lluvia/niebla ocasional activa, LateUpdate() (que corre después) recalculará el
                // fog final multiplicando sobre esta base, en vez de dejar el valor sin oscurecer.
                _baseFogColor   = lerpedColor;
                _baseFogDensity = lerpedDensity;

                RenderSettings.fogColor   = lerpedColor;
                RenderSettings.fogDensity = lerpedDensity;
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

    IEnumerator RainRoutine(float duration, bool immediate)
    {
        if (immediate)
        {
            // Carga de escena / test mode: sin nubosidad previa, igual que el resto del "look"
            // del periodo se aplica de golpe con ApplySettingsImmediate.
            if (_rainDarkenCoroutine != null)
            {
                StopCoroutine(_rainDarkenCoroutine);
                _rainDarkenCoroutine = null;
            }
            ApplyStormSkybox();
            _rainDarkenAmount = 1f;
            RaiseCloudsBuildingUp();
        }
        else
        {
            yield return CloudBuildUpRoutine();
        }

        ActivateRain();
        yield return new WaitForSeconds(duration);
        BeginRainFadeOut();
        _rainCoroutine = null;
    }

    /// <summary>
    /// Cubre el cielo de nubes (cambia a stormSkybox y empieza a oscurecer luz/niebla) ANTES de
    /// que arranque la lluvia. Se puede cancelar desde StopRain() mientras está en curso.
    /// </summary>
    IEnumerator CloudBuildUpRoutine()
    {
        _isCloudBuildingUp = true;

        ApplyStormSkybox();
        RaiseCloudsBuildingUp();
        StartRainDarken(1f);

        yield return new WaitForSeconds(Mathf.Max(0.01f, rainDarkenTransitionDuration));

        _isCloudBuildingUp = false;
    }

    void ApplyStormSkybox()
    {
        // No tocar cámara/skybox mientras el jugador está en un interior (real o cinemático):
        // EnvironmentController ya está aplicando el fondo/skybox de la AnchorEnvironment actual
        // (ver ApplyInteriorTo / ApplyInteriorForCinematic). Pisarlo aquí sin comprobar esto causaba
        // el bug "llueve dentro de la casa" / "sale un azul de fondo en medio de la secuencia": la
        // lluvia VISUAL sí se suprimía (ver ActivateRain), pero el fondo de cámara se sobrescribía
        // con el stormSkybox / el tinte gris de lluvia igualmente, en cuanto empezaba a nublarse.
        if (IsSkyboxLockedByEnvironment()) return;

        if (stormSkybox != null && RenderSettings.skybox != stormSkybox)
        {
            _preStormSkybox = RenderSettings.skybox;
            RenderSettings.skybox = stormSkybox;
            DynamicGI.UpdateEnvironment();
        }

        // Red de seguridad: sin un stormSkybox asignado, el skybox despejado (con sol y rayos)
        // sigue viéndose en el horizonte, más allá de donde llega CloudCoverSpawner, y la niebla
        // de RenderSettings no lo tiñe. Forzamos color sólido en la cámara para tapar ese hueco.
        if (stormSkybox == null && _mainCamera != null && !_cameraOverrideActive)
        {
            _preStormClearFlags = _mainCamera.clearFlags;
            _preStormBackgroundColor = _mainCamera.backgroundColor;
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = rainFogColorTint;
            _cameraOverrideActive = true;
        }
    }

    void RevertStormSkybox()
    {
        if (stormSkybox != null && RenderSettings.skybox == stormSkybox)
        {
            // Si ninguna transición de periodo cambió el skybox mientras tanto, volvemos al que
            // había antes de nublarse (o al del periodo actual si no se guardó ninguno).
            RenderSettings.skybox = _preStormSkybox != null ? _preStormSkybox : timeSettings[_currentIndex].skybox;
            DynamicGI.UpdateEnvironment();
            _preStormSkybox = null;
        }

        if (_cameraOverrideActive && _mainCamera != null)
        {
            _mainCamera.clearFlags = _preStormClearFlags;
            _mainCamera.backgroundColor = _preStormBackgroundColor;
            _cameraOverrideActive = false;
        }
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

        // Si el jugador ya está en un interior (real o cinemático) cuando empieza a llover, que no
        // se vea/oiga hasta que salga (evita el problema de "llueve dentro de la casa").
        if (IsSkyboxLockedByEnvironment())
            _activeRainInstance.SetActive(false);

        IsRaining = true;
        onRainStarted?.Invoke();
        RainStarted?.Invoke();
        // El SFX de lluvia (rain-sfx.mp3 en el profile) es una pista de ambiente, no un one-shot
        // corto: si se reproduce con PlaySFX normal, el AudioSource se autodevuelve al pool cuando
        // el CLIP termina, no cuando deja de llover. Con lluvias cortas o interrumpidas por
        // StopRain(), eso deja el SFX sonando de fondo mucho después de que IsRaining ya es false
        // (bug reportado: "ha terminado de llover y no ha parado el sfx"). PlayLoopingSFX usa una
        // fuente dedicada que solo se detiene explícitamente en BeginRainFadeOut vía StopLoopingSFX.
        AudioService.Instance?.PlayLoopingSFX(RainWeatherSfxLoopId, rainStartedSfxKey);
        // El oscurecimiento (luz + niebla) y el cambio a stormSkybox ya se aplicaron durante la
        // nubosidad previa (CloudBuildUpRoutine) o de golpe si immediate=true, así que aquí solo
        // queda instanciar las partículas de lluvia.
    }

    void BeginRainFadeOut()
    {
        if (!IsRaining) return;

        IsRaining = false;
        onRainStopped?.Invoke();
        RainStopped?.Invoke();
        // Corta el loop de ambiente arrancado en ActivateRain (ver comentario allí). Fundido con
        // la misma duración que el fade-out visual de las partículas para que no se note el corte.
        AudioService.Instance?.StopLoopingSFX(RainWeatherSfxLoopId, rainFadeOutTime);
        PlayWeatherSfx(rainStoppedSfxKey);
        StartRainDarken(0f);
        RevertStormSkybox();

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

    IEnumerator MistRoutine(float duration)
    {
        ActivateMist();
        yield return new WaitForSeconds(duration);
        BeginMistFadeOut();
        _mistCoroutine = null;
    }

    void ActivateMist()
    {
        if (IsMisty) return;

        // Cancelar fade-out activo antes de instanciar nueva niebla
        if (_mistFadeCoroutine != null)
        {
            StopCoroutine(_mistFadeCoroutine);
            _mistFadeCoroutine = null;
            if (_activeMistInstance != null)
            {
                Destroy(_activeMistInstance);
                _activeMistInstance = null;
            }
        }

        if (mistPrefab != null)
        {
            Transform parent = PlayerService.Player != null ? PlayerService.Player.transform :
                               Camera.main != null ? Camera.main.transform : null;

            if (parent != null)
            {
                _activeMistInstance = Instantiate(mistPrefab, parent);
                _activeMistInstance.transform.localPosition = Vector3.zero;
            }
            else
            {
                _activeMistInstance = Instantiate(mistPrefab, transform.position, Quaternion.identity);
                Debug.LogWarning("[DayNightCycle] No se encontró jugador ni cámara, niebla instanciada sin padre.");
            }

            // Igual que con la lluvia: si el jugador ya está en un interior (real o cinemático),
            // que no se vea hasta salir.
            if (IsSkyboxLockedByEnvironment())
                _activeMistInstance.SetActive(false);
        }

        IsMisty = true;
        onMistStarted?.Invoke();
        MistStarted?.Invoke();
        PlayWeatherSfx(mistStartedSfxKey);
        StartMistAmount(1f);
    }

    void BeginMistFadeOut()
    {
        if (!IsMisty) return;

        IsMisty = false;
        onMistStopped?.Invoke();
        MistStopped?.Invoke();
        PlayWeatherSfx(mistStoppedSfxKey);
        StartMistAmount(0f);

        if (_mistFadeCoroutine != null)
            StopCoroutine(_mistFadeCoroutine);

        if (_activeMistInstance != null)
            _mistFadeCoroutine = StartCoroutine(MistFadeOutRoutine(_activeMistInstance));
    }

    void StartMistAmount(float target)
    {
        if (_mistDarkenCoroutine != null)
            StopCoroutine(_mistDarkenCoroutine);
        _mistDarkenCoroutine = StartCoroutine(MistAmountRoutine(target));
    }

    IEnumerator MistAmountRoutine(float target)
    {
        float start = _mistAmount;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, mistTransitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _mistAmount = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        _mistAmount = target;
        _mistDarkenCoroutine = null;
    }

    IEnumerator MistFadeOutRoutine(GameObject mistInstance)
    {
        var particles = mistInstance.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particles)
        {
            var emission = ps.emission;
            emission.enabled = false;
        }

        yield return new WaitForSeconds(mistFadeOutTime);

        if (mistInstance != null)
            Destroy(mistInstance);

        if (_activeMistInstance == mistInstance)
            _activeMistInstance = null;

        _mistFadeCoroutine = null;
    }

    [ContextMenu("Avanzar al siguiente periodo")]
    public void DebugAdvanceTime() => AdvanceToNextPeriod();

    [ContextMenu("Activar/Desactivar lluvia")]
    public void DebugToggleRain() => ToggleRain();

    [ContextMenu("Activar/Desactivar niebla")]
    public void DebugToggleMist() => ToggleMist();
}
