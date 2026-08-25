using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Constelaciones decorativas de noche — formas inventadas hechas de "estrellas guía" (más grandes y
/// brillantes que el polvo de <see cref="NightSkyStarSpawner"/>) conectadas por líneas reales en el
/// mundo 3D, como en un mapa estelar clásico (Orión, la Osa Mayor...). Petición de Raúl, 25 ago 2026:
/// *"quiero que te inventes una [forma] para nuestro juego o algunas que se vean a lo lejos [...]
/// porque en un futuro scope tendremos que hacer la forma del personaje de Will"* — es decir, **esta
/// pasada NO dibuja la silueta de Will** (eso queda para un scope futuro, cuando haya que reconstruir
/// su silueta punto a punto); por ahora son formas propias inventadas para el juego, pensadas para
/// leerse bien de lejos: una espada, una corona y una estrella guía (compás), con temática coherente
/// con "El Sendero de las Estrellas" — ver <see cref="BuildConstellationDefinitions"/>.
///
/// Mismo patrón estructural y las mismas dos garantías ya probadas en <c>NightSkyStarSpawner</c> (ver
/// ese script para el razonamiento completo de cada una — no se repiten aquí por brevedad):
///
/// 1) **GameObjects 3D reales, no Quads/billboards** — cada "estrella guía" es una malla propia
///    generada en código (ver <see cref="GenerateBeaconMesh"/>), variante más grande y simétrica de la
///    malla de <c>NightSkyStarSpawner.GenerateStarMesh</c> (6 puntas iguales en vez de 2 largas + 1
///    corta) para que se distingan a simple vista de las estrellas de fondo — como en un mapa estelar
///    real, donde las estrellas con nombre se marcan distinto que el resto.
/// 2) **Oclusión garantizada por el mismo anclaje al farClipPlane de la cámara** (ver
///    <see cref="ComputeActiveRadius"/>) — ninguna estrella guía ni ninguna línea puede quedar delante
///    de una montaña o terreno, por el mismo razonamiento matemático (nada que la cámara dibuje puede
///    estar más lejos que su propio farClipPlane).
///
/// **Técnica nueva de este script**: cada constelación se define como un boceto 2D plano (un punto
/// (0,0) central y una lista de puntos alrededor, en unidades locales) y se proyecta sobre la esfera
/// del cielo mediante una dirección de centro (acimut/elevación) más una proyección gnómica (de plano
/// tangente) — ver <see cref="ComputeDirection"/>. Para el tamaño angular pequeño de estas
/// constelaciones (6-9°) la distorsión es despreciable, así que la forma dibujada se ve tal cual se
/// diseñó, sin deformarse por estar "pegada" a una esfera.
///
/// Las líneas que conectan las estrellas de cada constelación son <c>LineRenderer</c> reales en el
/// mundo (world space, no UI), con el mismo material <c>Sprites/Default</c> ya confirmado compatible
/// con URP en este proyecto (ver comentario de clase de <c>NightSkyStarSpawner</c>) — así que también
/// quedan correctamente ocluidas contra montañas/terreno por el mismo test de profundidad.
///
/// **Nota de instalación**: este componente es nuevo (no existía antes en la escena de Raúl), así que
/// hay que añadirlo a mano a un GameObject de la escena de mundo (por ejemplo, el mismo que ya lleva
/// <see cref="NightSkyStarSpawner"/>) desde el Editor — esta sesión no tiene Editor de Unity para
/// hacerlo automáticamente. Si <see cref="dayNightCycle"/> se deja vacío, se busca solo en Awake.
///
/// 25 ago 2026 — **coordinación con el polvo de fondo (<c>NightSkyStarSpawner</c>)**: Raúl preguntó
/// explícitamente si esto se complementaba con las estrellas ambientales o si se podían "pisar". Sin
/// coordinación, sí podían: el polvo de fondo se generó (pasada anterior, mismo día) con un sesgo
/// hacia la parte alta del cielo (<c>topBiasExponent</c>) que cae justo en la misma franja de
/// elevación (50-60°) donde viven estas constelaciones, así que había bastante probabilidad de que
/// alguna estrella de polvo cayera encima o muy cerca de una estrella guía o de una línea, rompiendo
/// la lectura de la forma. Fix: este script publica una zona de exclusión por constelación (centro +
/// radio angular, calculado del punto más lejano del boceto) en <see cref="ExclusionZones"/>, una
/// lista <c>static</c> que <c>NightSkyStarSpawner</c> consulta al colocar cada estrella de polvo para
/// apartarla si cae dentro — ver el comentario de clase de <c>NightSkyStarSpawner</c> para el otro
/// lado de esta coordinación. Acoplamiento deliberadamente en un solo sentido (el polvo, mucho más
/// numeroso y genérico, es quien evita a las constelaciones, con nombre y forma propios — no al
/// revés) y con caída elegante: si este componente no está añadido a la escena, la lista queda vacía
/// y <c>NightSkyStarSpawner</c> se comporta exactamente igual que antes de este fix.
///
/// 25 ago 2026 (pasada siguiente) — endurecimiento preventivo: Raúl reportó la misma regresión de
/// oclusión ("estrellas delante de las montañas") en <c>NightSkyStarSpawner</c>, causada por una
/// carrera de inicialización en arranques en frío (<c>Camera.main</c> aún null en el único frame de
/// margen de <c>CheckInitialStateDelayed</c>) combinada con valores obsoletos ya serializados en la
/// escena — ver el comentario de clase de <c>NightSkyStarSpawner</c> para el análisis completo. Este
/// script comparte exactamente el mismo patrón (mismo <c>CheckInitialStateDelayed</c>, mismo
/// <c>fallbackRadius</c> de último recurso), así que se aplica aquí el mismo arreglo preventivo
/// (<see cref="WaitForMainCamera"/>) aunque esta regresión concreta no se reportó en este script —
/// los valores de <c>fallbackRadius</c> en la escena de Raúl ya estaban al día (900, sin problema de
/// staleness aquí porque este componente se añadió después de todas las pasadas anteriores), pero la
/// carrera de inicialización en sí es la misma.
/// </summary>
public class NightSkyConstellationSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Estrellas guía (puntos de cada constelación)")]
    [Tooltip("Tamaño en PÍXELES DE PANTALLA de cada estrella guía — deliberadamente más grande que el rango de NightSkyStarSpawner.starScreenSizePixelsRange para que se lean como 'estrellas con nombre' frente al polvo de fondo.")]
    [SerializeField] private float constellationStarSizePixels = 22f;
    [Tooltip("Color de las estrellas guía — blanco plateado/azulado a propósito (distinto del dorado del polvo de fondo) para que las constelaciones destaquen como algo especial. Canales por encima de 1 para aprovechar el Bloom de la escena, igual que NightSkyStarSpawner.")]
    [SerializeField] private Color constellationStarColor = new Color(1.7f, 1.8f, 2.1f);
    [Tooltip("Cuánto varía el brillo de cada estrella guía con el tiempo. Deliberadamente más sutil que el parpadeo del polvo de fondo (NightSkyStarSpawner.twinkleIntensity) para que las constelaciones se lean como puntos de referencia estables, no como ruido animado.")]
    [SerializeField, Range(0f, 1f)] private float twinkleIntensity = 0.25f;
    [Tooltip("Rango de velocidad de parpadeo (ciclos por segundo, aprox.), deliberadamente más lento que el del polvo de fondo.")]
    [SerializeField] private Vector2 twinkleSpeedRange = new Vector2(0.15f, 0.4f);
    [Tooltip("Cada cuántos segundos se recalcula el parpadeo. Igual razonamiento que NightSkyStarSpawner.twinkleUpdateInterval.")]
    [SerializeField] private float twinkleUpdateInterval = 0.05f;

    [Header("Líneas de conexión")]
    [Tooltip("Ancho en PÍXELES DE PANTALLA de las líneas que conectan las estrellas guía de cada constelación.")]
    [SerializeField] private float lineWidthPixels = 2.2f;
    [Tooltip("Brillo relativo de las líneas frente a las estrellas guía (0-1) — más tenues a propósito para que el ojo se vaya primero a las estrellas, como en un mapa estelar real donde las líneas de unión son solo una guía discreta.")]
    [SerializeField, Range(0f, 1f)] private float lineBrightness = 0.5f;

    [Header("Radio (sin usar con cámara activa — ver comentario de clase)")]
    [Tooltip("Radio SOLO cuando no hay ninguna cámara activa todavía (caso raro). Con cámara activa el radio real siempre se ancla al farClipPlane — ver ComputeActiveRadius y el comentario de clase de NightSkyStarSpawner.")]
    [SerializeField] private float fallbackRadius = 900f;

    [Header("Cobertura total del mundo")]
    [Tooltip("Igual que NightSkyStarSpawner: cada 'recenterCheckInterval' segundos se comprueba la distancia del jugador al centro actual y, si supera la mitad del radio activo, se recoloca (solo se traslada la raíz, sin volver a instanciar nada ni perder las direcciones fijas de cada constelación).")]
    [SerializeField] private float recenterCheckInterval = 2f;

    [Header("Transición")]
    [Tooltip("Segundos que tardan las constelaciones en aparecer/disiparse.")]
    [SerializeField] private float fadeDuration = 6f;

    // Mismo margen y misma razón que NightSkyStarSpawner.FarClipMarginFactor — a propósito NO es un
    // [SerializeField], ver ese comentario de clase para el porqué.
    private const float FarClipMarginFactor = 0.99f;

    // Mismo arreglo y misma razón que NightSkyStarSpawner.MaxCameraWaitFrames (25 ago 2026, ver el
    // comentario de clase de ese script, pasada 5): evita que un arranque en frío sin Camera.main
    // todavía listo deje las constelaciones ancladas para siempre al fallbackRadius de último recurso.
    private const int MaxCameraWaitFrames = 30;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform _root;
    private Transform _followTransform;
    private readonly List<Renderer> _starRenderers = new List<Renderer>();
    private readonly List<float> _starBrightness = new List<float>();
    private readonly List<float> _twinklePhase = new List<float>();
    private readonly List<float> _twinkleSpeed = new List<float>();
    private readonly List<LineRenderer> _lineRenderers = new List<LineRenderer>();
    private MaterialPropertyBlock _mpb;
    private Material _starMaterial;
    private Material _lineMaterial;
    private Mesh _beaconMesh;
    private Coroutine _fadeCoroutine;
    private float _currentAlpha;
    private float _recenterTimer;
    private float _twinkleTimer;
    private bool _built;
    private bool _suppressedIndoors;
    private float _activeRadius;

    /// <summary>Boceto plano de una constelación: un centro en el cielo (acimut/elevación) más una
    /// lista de puntos locales alrededor de (0,0), dibujados con una o varias líneas continuas
    /// (<see cref="polylines"/>, cada una una lista de índices en <see cref="points"/>). Ver
    /// <see cref="ComputeDirection"/> para cómo se proyecta esto sobre la esfera del cielo.</summary>
    private class ConstellationDefinition
    {
        public string name;
        public float azimuthDegrees;
        public float elevationDegrees;
        public float angularScaleDegrees;
        public Vector2[] points;
        public int[][] polylines;
    }

    /// <summary>Zona del cielo (dirección de centro + radio angular en grados) que
    /// <c>NightSkyStarSpawner</c> debe evitar al colocar polvo de fondo, para no "pisar" ninguna
    /// estrella guía ni línea de una constelación — ver comentario de clase (25 ago 2026).</summary>
    public struct SkyExclusionZone
    {
        public Vector3 direction;
        public float radiusDegrees;
    }

    /// <summary>Zonas de exclusión de TODAS las constelaciones activas, publicadas por
    /// <see cref="RegisterExclusionZones"/> en Awake — <c>NightSkyStarSpawner</c> las consulta al
    /// construir su domo de polvo. Lista <c>static</c> a propósito (acoplamiento de un solo sentido
    /// entre los dos scripts, ver comentario de clase); vacía si este componente no está en la
    /// escena, así que <c>NightSkyStarSpawner</c> sigue funcionando igual sin él.</summary>
    public static readonly List<SkyExclusionZone> ExclusionZones = new List<SkyExclusionZone>();

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        _mpb = new MaterialPropertyBlock();
        BuildMaterials();
        GenerateBeaconMesh();
        RegisterExclusionZones();
    }

    /// <summary>
    /// Calcula y publica en <see cref="ExclusionZones"/> la zona de cada constelación: dirección de
    /// su centro y un radio angular igual a la distancia (en grados) de su punto más lejano del
    /// centro, más un margen fijo de 2° para el grosor de las líneas y el tamaño de las estrellas
    /// guía. Se recalcula entera cada Awake (limpiando antes) para que quede correcta aunque el
    /// componente se recargue (cambio de escena, recompilación en el Editor, etc.) — no depende de
    /// que BuildConstellationsIfNeeded haya llegado a ejecutarse (las zonas deben existir ANTES de
    /// que NightSkyStarSpawner construya su domo, y Awake de todos los componentes de la escena se
    /// ejecuta antes que ninguna corrutina/Update, así que el orden siempre es seguro).
    /// </summary>
    void RegisterExclusionZones()
    {
        ExclusionZones.Clear();
        foreach (var c in BuildConstellationDefinitions())
        {
            float maxMagnitude = 0f;
            foreach (var p in c.points)
                maxMagnitude = Mathf.Max(maxMagnitude, p.magnitude);

            Vector3 center = ComputeDirection(c.azimuthDegrees, c.elevationDegrees, Vector2.zero, c.angularScaleDegrees);
            float radiusDegrees = maxMagnitude * c.angularScaleDegrees + 2f;
            ExclusionZones.Add(new SkyExclusionZone { direction = center, radiusDegrees = radiusDegrees });
        }
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
            dayNightCycle.TimeOfDayChanged += HandleTimeOfDayChanged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
            Debug.LogWarning("[NightSkyConstellationSpawner] No se encontró ningún DayNightCycle en la escena; las constelaciones nunca se activarán.");
#endif

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        var ec = EnvironmentController.Instance;
        _suppressedIndoors = ec != null && ec.CurrentMode == EnvironmentMode.Interior;

        StartCoroutine(CheckInitialStateDelayed());
    }

    IEnumerator CheckInitialStateDelayed()
    {
        yield return null;
        if (dayNightCycle != null && dayNightCycle.CurrentTimeOfDay == DayNightCycle.TimeOfDay.Night)
        {
            // Mismo arreglo y misma razón que NightSkyStarSpawner.CheckInitialStateDelayed (25 ago
            // 2026, pasada 5) — esperar a que Camera.main exista de verdad antes de construir, en vez
            // de asumir que un solo frame de margen basta.
            yield return WaitForMainCamera();
            BuildConstellationsIfNeeded();
            StartFade(1f);
        }
    }

    /// <summary>Igual que NightSkyStarSpawner.WaitForMainCamera — ver ese comentario para el
    /// razonamiento completo.</summary>
    IEnumerator WaitForMainCamera()
    {
        int frames = 0;
        while (Camera.main == null && frames < MaxCameraWaitFrames)
        {
            frames++;
            yield return null;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Camera.main == null)
            Debug.LogWarning("[NightSkyConstellationSpawner] Camera.main sigue sin existir tras esperar " + MaxCameraWaitFrames + " frames; las constelaciones se construirán con el radio de último recurso (fallbackRadius), que puede quedar más cerca que alguna montaña de fondo.");
#endif
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
            dayNightCycle.TimeOfDayChanged -= HandleTimeOfDayChanged;

        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        DestroyConstellations();
    }

    void OnDestroy()
    {
        if (_starMaterial != null) Destroy(_starMaterial);
        if (_lineMaterial != null) Destroy(_lineMaterial);
        if (_beaconMesh != null) Destroy(_beaconMesh);

        // Limpia las zonas publicadas para que no queden "fantasmas" afectando a NightSkyStarSpawner
        // si esta escena se descarga y la siguiente no tiene ningún NightSkyConstellationSpawner.
        ExclusionZones.Clear();
    }

    void HandleTimeOfDayChanged(DayNightCycle.TimeOfDay t)
    {
        if (t == DayNightCycle.TimeOfDay.Night)
        {
            BuildConstellationsIfNeeded();
            StartFade(1f);
        }
        else if (t == DayNightCycle.TimeOfDay.Morning)
        {
            StartFade(0f);
        }
    }

    void HandleInteriorEntered()
    {
        _suppressedIndoors = true;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    void HandleInteriorExited()
    {
        _suppressedIndoors = false;
        if (_root != null && _currentAlpha > 0f) _root.gameObject.SetActive(true);
    }

    void Update()
    {
        // FIX (25 ago 2026): mismo hueco encontrado y confirmado KO en AmbientCloudDirector (ver
        // su Update()) — OnInteriorEntered/OnInteriorExited (ver OnEnable) solo cubren el flujo
        // "andando", nunca el cinemático (CinematicSequencerBase → BeginCinematicOverride +
        // ApplyInteriorForCinematic, p.ej. TabernaSequencer). Se corrige aquí de forma preventiva
        // (misma familia de sistemas de cielo, misma causa raíz) con el patrón ya establecido en
        // DayNightCycle.Update(). Sondeado ANTES del return de arriba a propósito, mismo criterio
        // que NightSkyStarSpawner.Update().
        var ec = EnvironmentController.Instance;
        bool effectivelyInteriorNow = ec != null && ec.IsEffectivelyInterior;
        if (effectivelyInteriorNow != _suppressedIndoors)
        {
            if (effectivelyInteriorNow) HandleInteriorEntered();
            else HandleInteriorExited();
        }

        if (!_built || _currentAlpha <= 0f || _root == null) return;

        _recenterTimer += Time.deltaTime;
        if (_recenterTimer >= recenterCheckInterval)
        {
            _recenterTimer = 0f;
            CheckRecenter();
        }

        _twinkleTimer += Time.deltaTime;
        if (_twinkleTimer >= twinkleUpdateInterval)
        {
            _twinkleTimer = 0f;
            ApplyAlpha(_currentAlpha);
        }
    }

    /// <summary>Mismo mecanismo que NightSkyStarSpawner.CheckRecenter: traslada la raíz YA CONSTRUIDA
    /// cuando el jugador se acerca al borde, sin volver a instanciar nada ni recalcular direcciones —
    /// como es una traslación pura (nunca rotación), cada constelación conserva su acimut/elevación
    /// fijos en el cielo.</summary>
    void CheckRecenter()
    {
        Transform playerT = PlayerService.Player != null ? PlayerService.Player.transform : _followTransform;
        if (playerT == null) return;

        float recenterThreshold = _activeRadius * 0.5f;
        if ((playerT.position - _root.position).sqrMagnitude <= recenterThreshold * recenterThreshold) return;

        _root.position = playerT.position;
        _followTransform = playerT;
    }

    void BuildConstellationsIfNeeded()
    {
        if (_built) return;

        _followTransform = PlayerService.Player != null ? PlayerService.Player.transform :
                            Camera.main != null ? Camera.main.transform : null;

        _root = new GameObject("[NightSkyConstellations]").transform;
        if (_followTransform != null)
            _root.position = _followTransform.position;

        _activeRadius = ComputeActiveRadius();

        float verticalFovDeg = Camera.main != null ? Camera.main.fieldOfView : 60f;
        float screenHeight = Mathf.Max(1f, Screen.height);
        float starSize = PixelSizeToWorldSize(constellationStarSizePixels, verticalFovDeg, screenHeight, _activeRadius);
        float lineWidth = PixelSizeToWorldSize(lineWidthPixels, verticalFovDeg, screenHeight, _activeRadius);

        var constellations = BuildConstellationDefinitions();
        foreach (var c in constellations)
            BuildOneConstellation(c, starSize, lineWidth);

        _currentAlpha = 0f;
        ApplyAlpha(0f);
        _built = true;

        if (_suppressedIndoors)
            _root.gameObject.SetActive(false);
    }

    void BuildOneConstellation(ConstellationDefinition c, float starSize, float lineWidth)
    {
        var constellationRoot = new GameObject("Constellation_" + c.name).transform;
        constellationRoot.SetParent(_root, false);

        // Dirección (en el mundo) de cada punto local de la constelación — se calcula una vez y se
        // reutiliza tanto para colocar la estrella guía como los extremos de cada línea.
        var directions = new Vector3[c.points.Length];
        for (int i = 0; i < c.points.Length; i++)
            directions[i] = ComputeDirection(c.azimuthDegrees, c.elevationDegrees, c.points[i], c.angularScaleDegrees);

        for (int i = 0; i < directions.Length; i++)
            SpawnBeaconStar(constellationRoot, directions[i], starSize);

        foreach (var polyline in c.polylines)
            SpawnLine(constellationRoot, directions, polyline, lineWidth);
    }

    void SpawnBeaconStar(Transform parent, Vector3 direction, float size)
    {
        var instance = new GameObject("Star");
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = direction * _activeRadius;
        instance.transform.localRotation = UnityEngine.Random.rotation;
        instance.transform.localScale = Vector3.one * size;

        var meshFilter = instance.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = _beaconMesh;

        var renderer = instance.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = _starMaterial;
        // FIX (25 ago 2026) — INC-101, "mirando de frente a una constelación se borran algunas
        // estrellas guía, girando la cámara reaparecen": cada estrella guía vive en el MISMO punto
        // exacto que el extremo de línea que la conecta (ver SpawnLine, misma expresión
        // direction * _activeRadius) — ambos materiales son Sprites/Default (transparente,
        // alpha-blended, sin ZWrite), así que Unity los ordena por distancia a cámara como
        // desempate dentro de la misma sortingLayer/sortingOrder. En un punto compartido esa
        // distancia es prácticamente idéntica para los dos renderers, así que el orden queda
        // inestable y depende de redondeos que cambian con el ángulo de vista — justo cuando se
        // mira derecho al vértice (máxima coincidencia) es cuando más fácil es que la línea (más
        // tenue, lineBrightness) gane el desempate y tape a la estrella; girar la cámara rompe el
        // empate y la estrella reaparece. Fix: sortingOrder explícito para que las estrellas SIEMPRE
        // dibujen después de (encima de) las líneas, sin depender de ningún desempate por distancia
        // — además refuerza la intención de diseño ya declarada arriba ("las líneas son solo una
        // guía discreta, el ojo se va primero a las estrellas").
        renderer.sortingOrder = 1;

        _starRenderers.Add(renderer);
        _starBrightness.Add(1f - UnityEngine.Random.value * 0.25f);
        _twinklePhase.Add(UnityEngine.Random.Range(0f, Mathf.PI * 2f));
        _twinkleSpeed.Add(UnityEngine.Random.Range(twinkleSpeedRange.x, twinkleSpeedRange.y));
    }

    void SpawnLine(Transform parent, Vector3[] directions, int[] polyline, float width)
    {
        if (polyline == null || polyline.Length < 2) return;

        var instance = new GameObject("Line");
        instance.transform.SetParent(parent, false);

        // useWorldSpace = false a propósito: el GameObject "Line" está parentado (posición local
        // identidad) bajo el mismo constellationRoot que las estrellas guía, así que sus coordenadas
        // locales coinciden numéricamente con las world-space de antes SOLO en el instante de
        // construir el domo. Si fueran world-space de verdad, CheckRecenter (que traslada _root para
        // seguir al jugador) dejaría las líneas "clavadas" en su posición original mientras las
        // estrellas sí se mueven con la jerarquía — con espacio local, Unity recalcula la posición
        // renderizada de la línea a partir del transform cada frame, así que sigue el recentrado
        // automáticamente igual que cualquier otro hijo de _root.
        var lr = instance.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = _lineMaterial;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        // FIX (25 ago 2026) — INC-101: sortingOrder explícito (por debajo del de las estrellas guía,
        // ver SpawnBeaconStar) para que nunca gane el desempate de orden transparente en los puntos
        // donde una línea comparte posición exacta con una estrella guía.
        lr.sortingOrder = 0;
        lr.positionCount = polyline.Length;

        for (int i = 0; i < polyline.Length; i++)
            lr.SetPosition(i, directions[polyline[i]] * _activeRadius);

        _lineRenderers.Add(lr);
    }

    /// <summary>
    /// Radio real de las constelaciones: SIEMPRE anclado al <c>farClipPlane</c> de la cámara activa
    /// (mismo razonamiento y mismo margen que <c>NightSkyStarSpawner.ComputeActiveDomeRadius</c> — ver
    /// ese comentario de clase). Solo si no hay ninguna cámara activa se usa <see cref="fallbackRadius"/>.
    /// </summary>
    float ComputeActiveRadius()
    {
        if (Camera.main == null) return fallbackRadius;
        return Camera.main.farClipPlane * FarClipMarginFactor;
    }

    /// <summary>
    /// Convierte un punto local (x,y) de un boceto de constelación a una dirección unitaria en el
    /// mundo, mediante una proyección gnómica (de plano tangente) desde una dirección de centro dada
    /// por <paramref name="azimuthDeg"/>/<paramref name="elevationDeg"/>. Acimut: 0° = norte (+Z),
    /// 90° = este (+X), en sentido horario. Cada unidad de <paramref name="localPoint"/> equivale a
    /// <paramref name="angularScaleDeg"/> grados de cielo. Para los tamaños angulares pequeños de
    /// estas constelaciones (unos pocos grados) la distorsión de esta proyección es despreciable, así
    /// que la forma dibujada en el boceto 2D se ve prácticamente igual una vez proyectada en la esfera.
    /// </summary>
    static Vector3 ComputeDirection(float azimuthDeg, float elevationDeg, Vector2 localPoint, float angularScaleDeg)
    {
        float azimuthRad = azimuthDeg * Mathf.Deg2Rad;
        float elevationRad = elevationDeg * Mathf.Deg2Rad;
        Vector3 center = new Vector3(
            Mathf.Sin(azimuthRad) * Mathf.Cos(elevationRad),
            Mathf.Sin(elevationRad),
            Mathf.Cos(azimuthRad) * Mathf.Cos(elevationRad));

        Vector3 right = Vector3.Cross(Vector3.up, center).normalized;
        Vector3 up = Vector3.Cross(center, right).normalized;

        float angleX = localPoint.x * angularScaleDeg * Mathf.Deg2Rad;
        float angleY = localPoint.y * angularScaleDeg * Mathf.Deg2Rad;

        Vector3 offsetDir = center + right * Mathf.Tan(angleX) + up * Mathf.Tan(angleY);
        return offsetDir.normalized;
    }

    /// <summary>Igual que NightSkyStarSpawner.PixelSizeToWorldSize — ver ese comentario. Duplicado a
    /// propósito para mantener este script autocontenido (mismo criterio que el resto del proyecto:
    /// scripts de mundo hermanos y desacoplados en vez de una utilidad compartida nueva).</summary>
    float PixelSizeToWorldSize(float pixels, float verticalFovDeg, float screenHeight, float distance)
    {
        float angularSizeDeg = pixels / screenHeight * verticalFovDeg;
        return 2f * distance * Mathf.Tan(Mathf.Clamp(angularSizeDeg, 0f, 179f) * 0.5f * Mathf.Deg2Rad);
    }

    void BuildMaterials()
    {
        _starMaterial = new Material(Shader.Find("Sprites/Default"));
        _starMaterial.enableInstancing = true;
        _lineMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    /// <summary>
    /// Malla de "estrella guía" — variante más simétrica y con más cuerpo que
    /// <c>NightSkyStarSpawner.GenerateStarMesh</c> (mismo principio de bipirámides por eje, pero las
    /// 3 con el mismo brazo y un rombo central más ancho) para que se distinga de las estrellas de
    /// fondo. Se genera UNA vez en Awake y se comparte vía <c>MeshFilter.sharedMesh</c>.
    /// </summary>
    void GenerateBeaconMesh()
    {
        const float arm = 1f;
        const float waist = 0.16f;

        var vertices = new List<Vector3>(24);
        var triangles = new List<int>(24);

        AddSpike(vertices, triangles, Vector3.right, Vector3.up, Vector3.forward, arm, waist);
        AddSpike(vertices, triangles, Vector3.up, Vector3.right, Vector3.forward, arm, waist);
        AddSpike(vertices, triangles, Vector3.forward, Vector3.right, Vector3.up, arm, waist);

        _beaconMesh = new Mesh { name = "ProceduralConstellationBeacon" };
        _beaconMesh.SetVertices(vertices);
        _beaconMesh.SetTriangles(triangles, 0);
        _beaconMesh.RecalculateNormals();
        _beaconMesh.RecalculateBounds();
    }

    static void AddSpike(List<Vector3> vertices, List<int> triangles, Vector3 axis, Vector3 waistA, Vector3 waistB, float armLength, float waist)
    {
        int baseIndex = vertices.Count;
        Vector3 tipPos = axis * armLength;
        Vector3 tipNeg = -axis * armLength;

        vertices.Add(waistA * waist);
        vertices.Add(waistB * waist);
        vertices.Add(-waistA * waist);
        vertices.Add(-waistB * waist);
        vertices.Add(tipPos);
        vertices.Add(tipNeg);

        int r0 = baseIndex, r1 = baseIndex + 1, r2 = baseIndex + 2, r3 = baseIndex + 3;
        int tipPosIndex = baseIndex + 4, tipNegIndex = baseIndex + 5;

        triangles.Add(r0); triangles.Add(r1); triangles.Add(tipPosIndex);
        triangles.Add(r1); triangles.Add(r2); triangles.Add(tipPosIndex);
        triangles.Add(r2); triangles.Add(r3); triangles.Add(tipPosIndex);
        triangles.Add(r3); triangles.Add(r0); triangles.Add(tipPosIndex);

        triangles.Add(r1); triangles.Add(r0); triangles.Add(tipNegIndex);
        triangles.Add(r2); triangles.Add(r1); triangles.Add(tipNegIndex);
        triangles.Add(r3); triangles.Add(r2); triangles.Add(tipNegIndex);
        triangles.Add(r0); triangles.Add(r3); triangles.Add(tipNegIndex);
    }

    /// <summary>
    /// Bocetos de las constelaciones inventadas para esta pasada — NINGUNA es la silueta de Will (ver
    /// comentario de clase, eso queda para un scope futuro). Tres formas simples y reconocibles de
    /// lejos, con temática de "El Sendero de las Estrellas": una espada, una corona y una estrella
    /// guía/compás. Repartidas cada ~120° en acimut y a bastante altura (50-60° de elevación, muy por
    /// encima de NightSkyStarSpawner.minElevationDegrees) para que no queden cerca del perfil de
    /// ninguna montaña — mismo criterio que el sesgo hacia el cenit del polvo de fondo.
    /// </summary>
    static List<ConstellationDefinition> BuildConstellationDefinitions()
    {
        var list = new List<ConstellationDefinition>();

        // "La Espada del Sendero": punta arriba, guarda cruzada, pomo abajo.
        list.Add(new ConstellationDefinition
        {
            name = "Espada",
            azimuthDegrees = 40f,
            elevationDegrees = 55f,
            angularScaleDegrees = 7f,
            points = new[]
            {
                new Vector2(0f, 1.0f),    // 0 punta
                new Vector2(0f, 0.25f),   // 1 hoja baja (donde cruza la guarda)
                new Vector2(-0.4f, 0.25f),// 2 guarda izquierda
                new Vector2(0.4f, 0.25f), // 3 guarda derecha
                new Vector2(0f, -0.05f),  // 4 empuñadura
                new Vector2(0f, -0.45f),  // 5 pomo
            },
            polylines = new[]
            {
                new[] { 0, 1, 4, 5 }, // punta -> hoja -> empuñadura -> pomo
                new[] { 2, 1, 3 },    // guarda cruzada
            },
        });

        // "La Corona del Sendero": tres picos sobre una base.
        list.Add(new ConstellationDefinition
        {
            name = "Corona",
            azimuthDegrees = 160f,
            elevationDegrees = 60f,
            angularScaleDegrees = 9f,
            points = new[]
            {
                new Vector2(-1.0f, -0.3f), // 0 base izquierda
                new Vector2(-0.6f, 0.25f), // 1 pico 1
                new Vector2(-0.3f, -0.05f),// 2 valle 1
                new Vector2(0f, 0.5f),     // 3 pico central (el más alto)
                new Vector2(0.3f, -0.05f), // 4 valle 2
                new Vector2(0.6f, 0.25f),  // 5 pico 3
                new Vector2(1.0f, -0.3f),  // 6 base derecha
            },
            polylines = new[]
            {
                new[] { 0, 1, 2, 3, 4, 5, 6 }, // zigzag de picos
                new[] { 0, 6 },                // línea de base, cierra la "copa"
            },
        });

        // "La Estrella Guía del Sendero": compás de 4 puntas con el centro brillante — guiño directo
        // al nombre del juego (marca el "camino"/sendero).
        list.Add(new ConstellationDefinition
        {
            name = "EstrellaGuia",
            azimuthDegrees = 280f,
            elevationDegrees = 50f,
            angularScaleDegrees = 6f,
            points = new[]
            {
                new Vector2(0f, 0f),   // 0 centro
                new Vector2(0f, 1f),   // 1 norte
                new Vector2(1f, 0f),   // 2 este
                new Vector2(0f, -1f),  // 3 sur
                new Vector2(-1f, 0f),  // 4 oeste
            },
            polylines = new[]
            {
                new[] { 1, 0, 3 }, // eje norte-sur
                new[] { 4, 0, 2 }, // eje oeste-este
            },
        });

        return list;
    }

    void StartFade(float target)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        if (target > 0f && _root != null && !_suppressedIndoors)
            _root.gameObject.SetActive(true);

        float duration = Mathf.Max(0.01f, fadeDuration);
        float start = _currentAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _currentAlpha = Mathf.Lerp(start, target, elapsed / duration);
            ApplyAlpha(_currentAlpha);
            yield return null;
        }

        _currentAlpha = target;
        ApplyAlpha(_currentAlpha);
        _fadeCoroutine = null;

        if (target <= 0f && _root != null)
            _root.gameObject.SetActive(false);
    }

    /// <summary>Recalcula color+alfa de las estrellas guía (con parpadeo, igual que
    /// NightSkyStarSpawner) y de las líneas (sin parpadeo, solo el fundido global × lineBrightness,
    /// para que se lean como una guía estable y no compitan visualmente con las estrellas).</summary>
    void ApplyAlpha(float alpha)
    {
        float time = Time.time;
        for (int i = 0; i < _starRenderers.Count; i++)
        {
            var r = _starRenderers[i];
            if (r == null) continue;

            float brightness = i < _starBrightness.Count ? _starBrightness[i] : 1f;
            float phase = i < _twinklePhase.Count ? _twinklePhase[i] : 0f;
            float speed = i < _twinkleSpeed.Count ? _twinkleSpeed[i] : 1f;
            float twinkle = Mathf.Lerp(1f - twinkleIntensity, 1f, Mathf.Sin(time * speed + phase) * 0.5f + 0.5f);

            Color c = constellationStarColor;
            c.a = alpha * brightness * twinkle;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }

        Color lineColor = constellationStarColor;
        lineColor.a = alpha * lineBrightness;
        for (int i = 0; i < _lineRenderers.Count; i++)
        {
            var lr = _lineRenderers[i];
            if (lr == null) continue;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
        }
    }

    void DestroyConstellations()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _root = null;
        _starRenderers.Clear();
        _starBrightness.Clear();
        _twinklePhase.Clear();
        _twinkleSpeed.Clear();
        _lineRenderers.Clear();
        _built = false;
        _currentAlpha = 0f;
    }

    [ContextMenu("Activar/Desactivar constelaciones (debug)")]
    public void DebugToggleConstellations()
    {
        if (_built && _currentAlpha > 0f)
            StartFade(0f);
        else
        {
            BuildConstellationsIfNeeded();
            StartFade(1f);
        }
    }
}
