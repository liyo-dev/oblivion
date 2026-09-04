using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nubosidad ambiental independiente de la lluvia (implementa la idea aparcada en TDD.md §16
/// Parte B: <c>CloudCoverage</c> progresivo + nubes sueltas de vez en cuando).
///
/// Vive en la misma escena que <see cref="DayNightCycle"/> y se limita a escuchar sus eventos
/// (<see cref="DayNightCycle.CloudsBuildingUp"/> / <see cref="DayNightCycle.RainStopped"/>), igual
/// que ya hace <see cref="CloudCoverSpawner"/> — sin referencias directas entre managers, tal como
/// pide CLAUDE.md §3.
///
/// Dos cosas suceden en paralelo, ambas dirigidas por un único valor interno de cobertura
/// (0 = despejado, 1 = tormenta):
///
/// 1. <b>Paseo aleatorio lento de cobertura</b> (<see cref="CoverageWalkLoop"/>): sube y baja sola
///    con pausas entre pasos — "se nubla un poco, se despeja, vuelve a nublarse". Si en algún paso
///    supera el umbral de tormenta, cede el control a <see cref="DayNightCycle.StartRain"/> tal
///    cual funciona hoy (no se duplica lógica de lluvia; si ya está lloviendo, StartRain no hace
///    nada). Cuando la lluvia de verdad termina (RainStopped), la cobertura se deja alta a
///    propósito para que decaiga sola paso a paso — así el cielo "se queda nublado y poco a poco
///    va saliendo el sol" en vez de despejarse de golpe. (El brillo de la luz/niebla ya se anima
///    aparte en DayNightCycle vía RainDarkenTransitionDuration; si se quiere que el sol tarde más
///    en volver, ese valor se sube ahí, sin tocar este script.)
///
/// 2. <b>Nubes sueltas cruzando el cielo</b> (<see cref="CloudSpawnLoop"/>): instancias de
///    <see cref="AmbientCloudDrifter"/> sacadas de un pool fijo, que nacen lejos del jugador,
///    cruzan por delante/encima y se alejan disipándose antes de reciclarse (nunca aparecen ni
///    desaparecen en mitad de la pantalla). La frecuencia escala con la cobertura actual: casi
///    nada con cielo despejado (alguna nube bonita pasando de higos a brevas), más seguido cuando
///    está nublado.
///
/// Mientras dura una tormenta real (entre CloudsBuildingUp y RainStopped) ambos bucles se pausan:
/// el techo de nubes de tormenta lo sigue llevando CloudCoverSpawner, este script no compite con
/// él. Las nubes sueltas que ya estuvieran en vuelo no se cortan — terminan su ruta con normalidad.
///
/// FIX (25 ago 2026): lo mismo aplica mientras el jugador está dentro de un interior
/// (<see cref="EnvironmentController.IsEffectivelyInterior"/>) — no se generan nubes nuevas y las
/// que ya estuvieran en vuelo se ocultan (no se cortan) hasta salir. Antes de este fix este script
/// era el único de la familia (CloudCoverSpawner/DayNightCycle/NightSkyStarSpawner/
/// NightSkyConstellationSpawner ya lo hacían) que no se enteraba de los interiores, reproduciendo
/// el mismo bug "las nubes se ven en un interior" que ya se había arreglado en CloudCoverSpawner
/// el 16 ago. Ver HandleInteriorEntered/HandleInteriorExited.
/// </summary>
public class AmbientCloudDirector : MonoBehaviour
{
    #region Singleton
    // FIX (25 ago 2026): expone una Instance para que sistemas de cámara puntuales (diálogos,
    // cinemáticas) puedan pedir SetAmbientCloudsVisible(false) sin tener que buscar/cachear este
    // componente por su cuenta. SetAmbientCloudsVisible ya existía (pensado para FocusCameraNode)
    // pero nada lo llamaba todavía — quedaba sin efecto real. Patrón de reseteo obligatorio para
    // singletons con estado estático (CLAUDE.md §3), evita contaminación entre sesiones de
    // PlayMode en el Editor.
    public static AmbientCloudDirector Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif
    #endregion

    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Nubes sueltas")]
    [Tooltip("Prefabs de nube para los vuelos ambientales (recomendado: los mismos QuibliRainCloud3D_1..4 de Assets/Prefabs/VFX, o variantes 'de buen tiempo'). Cada hueco del pool elige uno al azar UNA vez en Awake y lo reutiliza siempre — mismo patrón de pool manual que CloudCoverSpawner.")]
    [SerializeField] private GameObject[] ambientCloudPrefabs;
    [Tooltip("Nº máximo de nubes sueltas vivas a la vez. Si se necesita una nueva y el pool está lleno (todas en pleno vuelo), ese ciclo de spawn simplemente se salta.")]
    [SerializeField] private int poolSize = 6;
    [Tooltip("Distancia desde el jugador a la que nace/muere cada nube. La ruta completa mide el doble (nace a un lado, muere al otro).")]
    [SerializeField] private float spawnDistance = 180f;
    [Tooltip("Radio horizontal en el que puede caer el punto de paso más cercano al jugador (0 = pasa justo por encima, mayor = puede cruzar más de lado). Desde el FIX del 23 ago 2026 (ver horizonElevationDegrees) la altura real de cada nube depende de este offset, así que agrandar este radio también permite nubes más altas antes de tocar el techo de cloudAltitude.")]
    [SerializeField] private float passOffsetRadius = 90f;
    [Tooltip("FIX 23 ago 2026, 2ª pasada — techo MÁXIMO de altura de las nubes sueltas (ya no es la altura fija de antes). La 1ª pasada de este mismo día forzaba un offset mínimo para que el punto de paso no quedara nunca a más de maxViewableElevationDegrees (65°) del jugador — técnicamente dentro de lo que la cámara PUEDE alcanzar, pero Raúl probó en el juego y seguía sin ver ninguna: con cloudAltitude=100 fijo, incluso al offset máximo (90) la elevación mínima posible ya rondaba 48-53°, y en la práctica nadie inclina tanto la cámara jugando normal. La referencia correcta era otra: CloudCoverSpawner (el techo de tormenta, que sí se ve bien en juego) no apunta al límite máximo de la cámara sino a horizonElevationDegrees = 30° por defecto, un ángulo que se ve con solo alzar un poco la vista. Ahora cloudAltitude es el techo de seguridad (por si algún día se sube mucho passOffsetRadius o horizonElevationDegrees), pero la altura real que se usa en cada pasada es la que mantiene ese ángulo cómodo — ver SpawnPassingCloud.")]
    [SerializeField] private float cloudAltitude = 100f;
    [Tooltip("Altura mínima de una pasada (para offsets muy cercanos a 0, donde horizonElevationDegrees pediría una altura casi nula): evita que una nube 'de paso cercano' vuele pegada al suelo o atraviese al jugador. FIX 25 ago 2026 — subido de 20 a 32: con el altitudeJitter de 15 que tenía la escena (por encima incluso del default de este script), una pasada cercana podía bajar hasta 5m sobre el jugador — de sobra para cortar la cámara de diálogo, que suele mirar desde más cerca/arriba que la cámara de exploración normal. 32 (con altitudeJitter también bajado a su default de 8) deja un suelo real de 24m.")]
    [SerializeField] private float minCloudAltitude = 32f;
    [SerializeField] private float altitudeJitter = 8f;
    [Tooltip("FIX 23 ago 2026, 2ª pasada — mismo parámetro (nombre y valor por defecto) que ya usa CloudCoverSpawner para el techo de tormenta: el ángulo de elevación, visto desde el jugador, al que deben verse las nubes sin necesidad de inclinar mucho la cámara. La altura real de cada pasada se calcula como offset horizontal × tan(este ángulo), acotada entre minCloudAltitude y cloudAltitude — así una pasada muy cercana al jugador (offset pequeño) vuela baja y una pasada lejana (offset grande, hasta passOffsetRadius) vuela más alta, pero SIEMPRE dentro de un ángulo cómodo de ver, nunca cerca de los ~70° que ya sabemos que la cámara en tercera persona (Invector, 40° de inclinación + FOV 60°) no alcanza en juego normal.")]
    [SerializeField, Range(5f, 45f)] private float horizonElevationDegrees = 30f;
    [SerializeField] private Vector2 speedRange = new Vector2(3f, 6f);

    [Header("Viento")]
    [Tooltip("FIX 4 sep 2026 (nubes cruzándose en direcciones opuestas): dirección de viento compartida por TODAS las nubes de paso, en grados (0 = +Z, 90 = +X). Antes cada nube tiraba un ángulo 0-360° totalmente independiente — ver windDirectionJitterDegrees para la variación individual permitida.")]
    [SerializeField, Range(0f, 360f)] private float windDirectionDegrees = 45f;
    [Tooltip("Desviación aleatoria máxima (± grados) que cada nube individual puede tomar respecto a windDirectionDegrees, para que no todas vuelen en una línea perfectamente recta.")]
    [SerializeField, Range(0f, 90f)] private float windDirectionJitterDegrees = 20f;
    [SerializeField] private float fadeInDuration = 6f;
    [SerializeField] private float fadeOutDuration = 6f;
    [Tooltip("Probabilidad de que, tras spawnear una nube, salgan 1-2 más detrás en vez de una suelta — para que a veces se vea 'un grupito' bonito pasando.")]
    [SerializeField, Range(0f, 1f)] private float clusterChance = 0.3f;
    [SerializeField] private int clusterExtraMax = 2;
    [SerializeField] private Vector2 clusterDelayRange = new Vector2(4f, 10f);
    [Tooltip("Rango de espera entre spawns con el cielo despejado (cobertura ~0): así es como se consigue el 'de vez en cuando pasa alguna nube bonita'. FIX 20 ago 2026: con 90-160s (1.5-2.7 min) de media, en una sesión de demo corta era fácil no ver NINGUNA — el usuario jugó la demo entera y no vio ni una nube suelta. Bajado a un rango que garantiza varias apariciones incluso en una sesión de pocos minutos, sin que se sienta como spam.")]
    [SerializeField] private Vector2 idleSpawnIntervalRange = new Vector2(30f, 55f);
    [Tooltip("Rango de espera entre spawns con cobertura alta (cielo nublado/aclarando tras lluvia): mucho más seguido que en despejado. Ajustado 20 ago 2026 junto con idleSpawnIntervalRange.")]
    [SerializeField] private Vector2 busySpawnIntervalRange = new Vector2(10f, 20f);

    [Header("Paseo aleatorio de cobertura")]
    [Tooltip("Cobertura (0-1) a partir de la cual la frecuencia de nubes sueltas ya está al máximo (busySpawnIntervalRange).")]
    [SerializeField, Range(0.05f, 1f)] private float lightCloudThreshold = 0.4f;
    [Tooltip("Cobertura a partir de la cual, en cada paso, hay una tirada para pasar a tormenta de verdad (StartRain).")]
    [SerializeField, Range(0.1f, 1f)] private float stormThreshold = 0.8f;
    [Tooltip("Probabilidad de esa tirada por cada paso que la cobertura pase de stormThreshold. Que no sea 1 evita que SIEMPRE acabe lloviendo nada más rozar el umbral.")]
    [SerializeField, Range(0f, 1f)] private float stormRollChance = 0.35f;
    [SerializeField] private Vector2 walkStepIntervalRange = new Vector2(10f, 25f);
    [Tooltip("Máximo cambio aleatorio de cobertura por paso (hacia arriba o hacia abajo).")]
    [SerializeField] private float walkStepSize = 0.15f;
    [Tooltip("Cuánto tira la cobertura hacia 0 en cada paso (por encima del paso aleatorio), para que un cielo nublado no se quede así para siempre sin motivo.")]
    [SerializeField] private float coverageDecayPerStep = 0.05f;
    [Tooltip("Cobertura a la que se fija el paseo aleatorio justo cuando termina una lluvia de verdad, para que el cielo tarde un rato en despejarse del todo en vez de volver a 0 de golpe.")]
    [SerializeField, Range(0f, 1f)] private float postRainResidualCoverage = 0.6f;

    private readonly List<AmbientCloudDrifter> _pool = new List<AmbientCloudDrifter>();
    private readonly Queue<AmbientCloudDrifter> _free = new Queue<AmbientCloudDrifter>();

    private Coroutine _walkRoutine;
    private Coroutine _spawnRoutine;
    private float _coverage;
    private bool _stormActive;
    // FIX (25 ago 2026): "las nubes salen en interiores" — ver nota de clase más arriba.
    // _hiddenByInterior es el estado real de interior (EnvironmentController); _visibleRequested
    // es la última petición externa vía SetAmbientCloudsVisible (enfoque de cámara puntual, ver
    // DialogueCinematicController). La visibilidad real aplicada es la Y lógica de ambas (ver
    // ApplyCloudVisibility), así que un enfoque de cámara que termine (visible=true) mientras
    // seguimos dentro de un interior no revela las nubes por encima del techo por error.
    private bool _hiddenByInterior;
    private bool _visibleRequested = true;
    // 1 sep 2026 (ver SetZoneCloudBoost): true mientras el jugador está dentro de una
    // AmbientZone con forcesMist activo -- fuerza la cadencia de CloudSpawnLoop al ritmo
    // 'nublado' sin tocar _coverage (no puede disparar una tormenta por sí solo).
    private bool _zoneCloudBoostActive;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        BuildPool();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp += HandleStormStarting;
            dayNightCycle.RainStopped += HandleRainStopped;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning("[AmbientCloudDirector] No se encontró ningún DayNightCycle en la escena; la nubosidad ambiental no sabrá cuándo hay tormenta real.");
        }
#endif

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        // Si ya estábamos en un interior al activarnos (p.ej. esta escena se cargó directamente
        // dentro de un interior), arrancar ya ocultos — mismo criterio que usa
        // CloudCoverSpawner.OnEnable() para _hiddenByInterior.
        var ec = EnvironmentController.Instance;
        _hiddenByInterior = ec != null && ec.IsEffectivelyInterior;
        ApplyCloudVisibility();

        if (_walkRoutine == null)
            _walkRoutine = StartCoroutine(CoverageWalkLoop());
        if (_spawnRoutine == null)
            _spawnRoutine = StartCoroutine(CloudSpawnLoop());
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp -= HandleStormStarting;
            dayNightCycle.RainStopped -= HandleRainStopped;
        }

        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        if (_walkRoutine != null) { StopCoroutine(_walkRoutine); _walkRoutine = null; }
        if (_spawnRoutine != null) { StopCoroutine(_spawnRoutine); _spawnRoutine = null; }
    }

    void HandleStormStarting() => _stormActive = true;

    void HandleRainStopped()
    {
        _stormActive = false;
        _coverage = Mathf.Max(_coverage, postRainResidualCoverage);
    }

    void HandleInteriorEntered()
    {
        _hiddenByInterior = true;
        ApplyCloudVisibility();
    }

    void HandleInteriorExited()
    {
        _hiddenByInterior = false;
        ApplyCloudVisibility();
    }

    void Update()
    {
        // FIX (25 ago 2026, 2ª pasada): confirmado KO en juego por Raúl — las nubes sueltas seguían
        // viéndose dentro de la taberna durante su cinemática (TabernaSequencer). Causa: la
        // suscripción a OnInteriorEntered/OnInteriorExited (ver OnEnable) SOLO cubre el flujo
        // "andando" (EnvironmentController.ApplyInterior/ApplyExterior, disparado desde
        // TeleportService/AnchorSetter) — el flujo cinemático (CinematicSequencerBase →
        // BeginCinematicOverride + ApplyInteriorForCinematic) nunca dispara esos eventos a
        // propósito (ver comentario de EnvironmentController.ApplyInteriorForCinematic). Mismo
        // hueco que ya tuvo DayNightCycle con la lluvia/niebla — arreglado ahí sondeando
        // IsEffectivelyInterior en Update() con patrón edge-triggered (ver DayNightCycle.Update()).
        // Replicado aquí: IsEffectivelyInterior sí tiene en cuenta el override cinemático, así que
        // este sondeo cubre tanto el flujo andando (aunque ya lo cubre el evento, sin coste extra
        // real) como el cinemático (que antes se quedaba sin cubrir).
        var ec = EnvironmentController.Instance;
        bool effectivelyInteriorNow = ec != null && ec.IsEffectivelyInterior;
        if (effectivelyInteriorNow != _hiddenByInterior)
        {
            if (effectivelyInteriorNow) HandleInteriorEntered();
            else HandleInteriorExited();
        }
    }

    void BuildPool()
    {
        if (ambientCloudPrefabs == null || ambientCloudPrefabs.Length == 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[AmbientCloudDirector] ambientCloudPrefabs está vacío; no habrá nubes sueltas ambientales (la lluvia/tormenta de CloudCoverSpawner no se ve afectada).");
#endif
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            var prefab = ambientCloudPrefabs[UnityEngine.Random.Range(0, ambientCloudPrefabs.Length)];
            if (prefab == null) continue;

            var instance = Instantiate(prefab, transform);
            instance.SetActive(false);

            var drifter = instance.GetComponent<AmbientCloudDrifter>();
            if (drifter == null)
                drifter = instance.AddComponent<AmbientCloudDrifter>();

            _pool.Add(drifter);
            _free.Enqueue(drifter);
        }
    }

    IEnumerator CoverageWalkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(walkStepIntervalRange.x, walkStepIntervalRange.y));

            if (_stormActive || dayNightCycle == null || dayNightCycle.IsRaining)
                continue;

            float delta = UnityEngine.Random.Range(-walkStepSize, walkStepSize);
            _coverage = Mathf.Clamp01(_coverage + delta);
            _coverage = Mathf.MoveTowards(_coverage, 0f, coverageDecayPerStep);

            if (_coverage >= stormThreshold && UnityEngine.Random.value < stormRollChance)
            {
                dayNightCycle.StartRain();
                // StartRain ya dispara CloudsBuildingUp -> HandleStormStarting nos pausará solos.
            }
        }
    }

    IEnumerator CloudSpawnLoop()
    {
        while (true)
        {
            float coverageFactor = _zoneCloudBoostActive ? 1f : Mathf.Clamp01(_coverage / Mathf.Max(0.01f, lightCloudThreshold));
            float lo = Mathf.Lerp(idleSpawnIntervalRange.x, busySpawnIntervalRange.x, coverageFactor);
            float hi = Mathf.Lerp(idleSpawnIntervalRange.y, busySpawnIntervalRange.y, coverageFactor);
            yield return new WaitForSeconds(UnityEngine.Random.Range(lo, hi));

            if (_stormActive || _hiddenByInterior)
                continue;

            SpawnPassingCloud();

            if (UnityEngine.Random.value < clusterChance)
            {
                int extra = UnityEngine.Random.Range(1, clusterExtraMax + 1);
                for (int i = 0; i < extra; i++)
                {
                    yield return new WaitForSeconds(UnityEngine.Random.Range(clusterDelayRange.x, clusterDelayRange.y));
                    if (_stormActive || _hiddenByInterior) break;
                    SpawnPassingCloud();
                }
            }
        }
    }

    void SpawnPassingCloud()
    {
        Transform followT = PlayerService.Player != null ? PlayerService.Player.transform : null;
        if (followT == null) return;

        if (_free.Count == 0) return; // pool lleno de nubes ya en vuelo: se salta este ciclo.

        var drifter = _free.Dequeue();

        Vector2 offset2D = UnityEngine.Random.insideUnitCircle * passOffsetRadius;
        Vector3 passPoint = followT.position + new Vector3(offset2D.x, 0f, offset2D.y);

        // Ver tooltip de horizonElevationDegrees: la altura de esta pasada en concreto se deriva de
        // lo lejos que queda horizontalmente del jugador (offset2D), no es un valor fijo — así el
        // ángulo de elevación desde el jugador se queda siempre en una zona cómoda de ver, igual que
        // ya hace CloudCoverSpawner con su techo de tormenta.
        float offsetMagnitude = offset2D.magnitude;
        float elevationHeight = offsetMagnitude * Mathf.Tan(horizonElevationDegrees * Mathf.Deg2Rad);
        float baseHeight = Mathf.Clamp(elevationHeight, minCloudAltitude, cloudAltitude);
        passPoint.y = followT.position.y + baseHeight + UnityEngine.Random.Range(-altitudeJitter, altitudeJitter);

        // FIX 4 sep 2026: antes cada nube tiraba un ángulo 0-360° totalmente independiente,
        // lo que producía nubes cruzándose en direcciones opuestas a la vez (reportado por Raúl).
        // Ahora todas comparten windDirectionDegrees, con solo una pequeña desviación individual.
        float angle = (windDirectionDegrees + UnityEngine.Random.Range(-windDirectionJitterDegrees, windDirectionJitterDegrees)) * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        Vector3 start = passPoint - dir * spawnDistance;
        Vector3 end = passPoint + dir * spawnDistance;

        float speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);

        drifter.gameObject.SetActive(true);
        drifter.Play(start, end, speed, fadeInDuration, fadeOutDuration, OnCloudFinished);
    }

    void OnCloudFinished(AmbientCloudDrifter drifter)
    {
        drifter.gameObject.SetActive(false);
        _free.Enqueue(drifter);
    }

    [ContextMenu("Forzar una nube ambiental ahora (debug)")]
    void DebugForceSpawnCloud() => SpawnPassingCloud();

    /// <summary>
    /// Pide mostrar/ocultar las nubes sueltas del pool por un enfoque de cámara puntual (ver
    /// FocusCameraNode), sin tocar sus corrutinas de vuelo ni el paseo aleatorio de cobertura —
    /// mismo uso puntual que CloudCoverSpawner.SetRenderersVisible. No es la última palabra sobre
    /// la visibilidad real: ver <see cref="ApplyCloudVisibility"/>.
    /// </summary>
    public void SetAmbientCloudsVisible(bool visible)
    {
        _visibleRequested = visible;
        ApplyCloudVisibility();
    }

    /// <summary>
    /// 1 sep 2026 -- pedido por Raúl: que las AmbientZone con niebla de nubes bajas
    /// (AmbientPreset.forcesMist) hagan que se vean nubes reales cruzando el cielo con más
    /// frecuencia mientras el jugador esté dentro, no solo niebla de distancia (ver
    /// AmbientZone.ApplyZoneTransition/OnTriggerExit, que llaman a esto igual que ya llaman a
    /// DayNightCycle.SetZoneMistOverride). Mismo patrón: la zona pide un estado por método
    /// público, sin guardar referencias directas entre managers (CLAUDE.md §3).
    /// Deliberadamente NO toca <see cref="_coverage"/> (el paseo aleatorio que decide si
    /// empieza una tormenta real en CoverageWalkLoop) -- solo fuerza la cadencia de
    /// CloudSpawnLoop al ritmo 'nublado' (busySpawnIntervalRange) mientras dure. Así entrar en
    /// una zona de niebla nunca puede disparar lluvia por sí sola.
    /// </summary>
    public void SetZoneCloudBoost(bool active) => _zoneCloudBoostActive = active;

    /// <summary>
    /// FIX (25 ago 2026): visibilidad real de TODAS las nubes del pool (volando o no) = lo último
    /// pedido por <see cref="SetAmbientCloudsVisible"/> Y no estar dentro de un interior
    /// (<see cref="_hiddenByInterior"/>). Sin este AND, un enfoque de cámara que termine
    /// (SetAmbientCloudsVisible(true)) mientras el jugador sigue dentro de un interior revelaría
    /// las nubes por encima del techo del interior.
    /// </summary>
    void ApplyCloudVisibility()
    {
        bool visible = _visibleRequested && !_hiddenByInterior;
        for (int i = 0; i < _pool.Count; i++)
        {
            var drifter = _pool[i];
            if (drifter != null) drifter.SetRenderersVisible(visible);
        }
    }
}
