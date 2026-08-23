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
/// </summary>
public class AmbientCloudDirector : MonoBehaviour
{
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
    [Tooltip("Radio horizontal en el que puede caer el punto de paso más cercano al jugador (0 = pasa justo por encima, mayor = puede cruzar más de lado).")]
    [SerializeField] private float passOffsetRadius = 90f;
    [Tooltip("Altura sobre el jugador a la que vuelan las nubes sueltas.")]
    [SerializeField] private float cloudAltitude = 100f;
    [SerializeField] private float altitudeJitter = 15f;
    [SerializeField] private Vector2 speedRange = new Vector2(3f, 6f);
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

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        BuildPool();
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

        if (_walkRoutine != null) { StopCoroutine(_walkRoutine); _walkRoutine = null; }
        if (_spawnRoutine != null) { StopCoroutine(_spawnRoutine); _spawnRoutine = null; }
    }

    void HandleStormStarting() => _stormActive = true;

    void HandleRainStopped()
    {
        _stormActive = false;
        _coverage = Mathf.Max(_coverage, postRainResidualCoverage);
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
            float coverageFactor = Mathf.Clamp01(_coverage / Mathf.Max(0.01f, lightCloudThreshold));
            float lo = Mathf.Lerp(idleSpawnIntervalRange.x, busySpawnIntervalRange.x, coverageFactor);
            float hi = Mathf.Lerp(idleSpawnIntervalRange.y, busySpawnIntervalRange.y, coverageFactor);
            yield return new WaitForSeconds(UnityEngine.Random.Range(lo, hi));

            if (_stormActive)
                continue;

            SpawnPassingCloud();

            if (UnityEngine.Random.value < clusterChance)
            {
                int extra = UnityEngine.Random.Range(1, clusterExtraMax + 1);
                for (int i = 0; i < extra; i++)
                {
                    yield return new WaitForSeconds(UnityEngine.Random.Range(clusterDelayRange.x, clusterDelayRange.y));
                    if (_stormActive) break;
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
        passPoint.y = followT.position.y + cloudAltitude + UnityEngine.Random.Range(-altitudeJitter, altitudeJitter);

        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
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
    /// Activa/desactiva los renderers de TODAS las nubes del pool (volando o no), sin tocar sus
    /// corrutinas de vuelo ni el paseo aleatorio de cobertura. Pensado para el mismo uso puntual
    /// que CloudCoverSpawner.SetRenderersVisible: un enfoque de cámara breve (ver
    /// FocusCameraNode) que no debe quedar tapado por una nube suelta que en ese instante esté
    /// cruzando por delante.
    /// </summary>
    public void SetAmbientCloudsVisible(bool visible)
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            var drifter = _pool[i];
            if (drifter != null) drifter.SetRenderersVisible(visible);
        }
    }
}
