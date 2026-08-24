using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Domo de estrellas doradas hechas de GameObjects reales (no un truco de shader en el skybox ni
/// un ParticleSystem): esferas 3D pequeñas repartidas sobre una esfera alrededor del jugador, tema
/// "Sendero de las Estrellas". Mismo patrón estructural que CloudCoverSpawner (rejilla/reparto +
/// pool — se construye UNA vez y se reutiliza sin volver a Instantiate en cada ciclo noche/día +
/// fundido de alfa vía MaterialPropertyBlock + recentrado periódico, no por frame) para no
/// introducir un patrón nuevo en el proyecto — ver ese script para el razonamiento completo de cada
/// decisión de rendimiento.
///
/// 20 ago 2026 — FIX "las estrellas no brillan y el color es muy apagado": antes de este fix cada
/// estrella tenía un brillo FIJO sorteado una vez (brightnessVariance) y no había animación de
/// parpadeo por diseño — el domo se veía como un cielo estrellado estático, sin vida. Ahora cada
/// estrella SÍ parpadea de verdad (ver <see cref="twinkleIntensity"/>/<see cref="twinkleSpeedRange"/>,
/// cada una con su propia fase y velocidad para que no parpadeen a la vez), recalculado cada
/// <see cref="twinkleUpdateInterval"/> segundos (no cada frame). Además, <see cref="starColor"/> usa
/// canales por encima de 1 — con el pipeline de este proyecto en HDR y Bloom activo (ver
/// Assets/Settings/PC_RPAsset.asset / Assets/Settings/DefaultVolumeProfile.asset) un color por
/// encima de 1 aporta brillo extra vía Bloom sin tocar el shader. Se añade también
/// <see cref="starColorAlt"/> (unas pocas estrellas plateadas/azuladas sueltas, ver
/// <see cref="altStarChance"/>) para romper la monotonía del domo completamente dorado.
///
/// 23 ago 2026 — Raúl pidió: color más dorado, forma como el icono del cursor y que brillen. Se
/// probaron varias pasadas con estrellas hechas de Quads planos con una textura de chispa de 4
/// puntas generada en memoria, orientados hacia cámara (billboard) y con `domeRadius` recortado al
/// `farClipPlane` de la cámara para intentar que quedaran detrás de las montañas. A pesar de 3
/// pasadas ese mismo día, el problema de fondo (estrellas pintándose delante de las montañas) y la
/// sensación de "cartón plano" seguían sin resolverse del todo — ver el historial completo de esas
/// pasadas en el control de versiones si hace falta el detalle; se ha simplificado este comentario
/// de clase el 24 de agosto (ver más abajo) porque el enfoque de Quads se sustituyó por completo.
///
/// 24 ago 2026 — Dos pasadas intermedias el mismo día (truco de cola de render "Background" tipo
/// Skybox, luego un Raycast por estrella para medir la montaña real en cada dirección) tampoco
/// resolvieron el problema de forma visible para Raúl ("yo lo sigo viendo igual"), y además seguían
/// siendo Quads planos, lo cual Raúl rechazó explícitamente: **"no quiero na en 2D, que se generen
/// objetos 3D mediante un pool de forma aleatoria por el cielo y que brillen, como si me pones
/// esferas pequeñitas ya me da igual pero arreglamelo ya"** — bloqueando la subida de una build.
/// Reescritura completa de la generación de estrellas con dos cambios de fondo:
///
/// 1) **Esferas 3D reales en vez de Quads con billboard**: cada estrella es ahora una malla
///    <c>PrimitiveType.Sphere</c> real (ver <see cref="SpawnStar"/>), sin textura generada, sin
///    billboard (ya no hace falta orientar nada hacia cámara: una esfera se ve igual de "3D" desde
///    cualquier ángulo, a diferencia de un plano). Esto resuelve la queja "no quiero na en 2D" de
///    raíz: no hay ángulo de cámara en el que una esfera real se vea "de canto" como sí le pasaba a
///    un plano mal orientado. El material sigue siendo <c>Sprites/Default</c> (ver
///    <see cref="BuildStarMaterial"/>) — a propósito, NO se cambia a otro shader: este proyecto usa
///    URP y Sprites/Default es el único shader de este script ya confirmado compatible con URP en
///    este proyecto (ver comentario del fix del 20 ago 2026); arriesgarse a un shader nuevo sin poder
///    verificarlo en el Editor (esta sesión no tiene Unity) podría dejar las estrellas invisibles o
///    en magenta de error en vez de solo "feas". Sin textura asignada, Sprites/Default usa su blanco
///    opaco por defecto — con una malla de esfera real (no un Quad) eso ya es justo lo que hace
///    falta, así que la generación de textura de la pasada anterior desaparece sin más.
/// 2) **Oclusión correcta SIN Raycast — anclada al propio farClipPlane de la cámara**: nada que la
///    cámara dibuje puede estar más lejos que su propio <c>farClipPlane</c> (si estuviera más lejos,
///    ni siquiera se vería, se recortaría). Colocar el domo justo en ese límite
///    (<see cref="ComputeActiveDomeRadius"/>, con un margen mínimo solo por precisión numérica, NO
///    como "colchón" de seguridad) GARANTIZA que las estrellas queden más lejos que cualquier
///    montaña o terreno visible. Sprites/Default no escribe profundidad propia (ZWrite Off, cola
///    "Transparent") pero SÍ compara su profundidad contra la ya escrita por la geometría opaca
///    (montañas, cola "Geometry", que se dibuja antes) — con las estrellas ancladas al borde mismo
///    del farClipPlane, esa comparación falla siempre que haya algo opaco delante, sea lo que sea y
///    esté a la distancia que esté, así que el fragmento de la estrella se descarta correctamente
///    ahí. No depende de que el fondo tenga collider (a diferencia del intento anterior con Raycast,
///    que si el fondo era un mesh puramente decorativo sin collider nunca lo detectaba — probable
///    explicación real de por qué "seguía viendo igual"). Se elimina por completo la maquinaria de
///    Raycast de la pasada anterior (<c>occlusionCheckLayers</c>, <c>occlusionSafetyMargin</c>,
///    <c>minStarDistance</c>, <c>raycastOriginHeight</c>, <c>ComputeStarDistance</c>) — más simple y
///    más fiable.
///
/// **Nota importante sobre por qué los cambios de las pasadas anteriores no se notaban en el
/// Editor**: varios de los campos que se estaban ajustando (tamaño en píxeles, forma, margen de
/// seguridad del farClipPlane...) son campos serializados de un componente que ya existía en la
/// escena de Raúl. Unity NO aplica retroactivamente un nuevo valor por defecto de C# a un campo que
/// ya tiene un valor guardado en la escena/prefab (el mismo tipo de bug ya documentado en este
/// proyecto para <c>PromoVideo01Sequencer</c>, ver `contexto-proyecto.md`) — así que aunque el
/// código cambiara, el Inspector podía seguir mostrando (y USANDO) los números viejos hasta que
/// alguien los tocara a mano o el campo se borrara/renombrara. Varios campos de este fix se han
/// renombrado o eliminado a propósito (<c>desiredScreenSizePixels</c> →
/// <see cref="starScreenSizePixelsRange"/>; <c>farClipSafetyMargin</c> pasa de campo serializado a
/// constante fija en código, ver <see cref="FarClipMarginFactor"/>; todos los campos de forma del
/// Quad desaparecen del todo) precisamente para que el valor nuevo se aplique de verdad sin depender
/// de que Raúl haga clic en "Reset" sobre el componente. Si en el futuro un ajuste de Inspector no
/// parece tener ningún efecto, sospechar primero de esto.
/// </summary>
public class NightSkyStarSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Domo de estrellas (esferas 3D reales — reescrito 24 ago 2026, ver comentario de clase)")]
    [Tooltip("Número de estrellas del domo. Se construye UNA vez y queda fijo (igual que el techo de nubes), con recentrado periódico para seguir cubriendo el cielo según el jugador explora.")]
    [SerializeField] private int starCount = 220;
    [Tooltip("Radio de la esfera de estrellas SOLO cuando no hay ninguna cámara activa todavía (caso raro, p.ej. el domo se construye antes de que Camera.main exista). Con cámara activa este valor NO se usa: el radio real se calcula siempre a partir del farClipPlane de la cámara — ver ComputeActiveDomeRadius y el comentario de clase.")]
    [SerializeField] private float domeRadius = 900f;
    [Tooltip("Elevación mínima sobre el horizonte (grados) a la que se colocan estrellas. Puramente estético (evita amontonar estrellas justo donde es más probable que haya monte cerca) — la garantía real de que ninguna estrella quede delante de una montaña la da ComputeActiveDomeRadius, ver comentario de clase, no este valor.")]
    [SerializeField, Range(0f, 40f)] private float minElevationDegrees = 16f;
    [Tooltip("Tamaño de cada estrella en PÍXELES DE PANTALLA aproximados (mín/máx, elegido al azar por estrella con sesgo hacia el mínimo — ver sizeBiasExponent) — NO en unidades de mundo. Se convierte a tamaño de mundo usando el FOV vertical de la cámara activa y el radio real del domo. Campo renombrado el 24 ago 2026 (antes 'desiredScreenSizePixels') a propósito para que este valor nuevo, más pequeño, se aplique de verdad en vez de quedar tapado por un valor antiguo ya serializado en la escena — ver comentario de clase.")]
    [SerializeField] private Vector2 starScreenSizePixelsRange = new Vector2(5f, 16f);
    [Tooltip("Sesga el sorteo del tamaño de cada estrella dentro de starScreenSizePixelsRange hacia el extremo pequeño. 1 = sorteo uniforme. Más alto = las estrellas grandes/brillantes son cada vez más excepcionales, como en un cielo real donde casi todas las estrellas son puntos pequeños y solo unas pocas destacan.")]
    [SerializeField, Range(1f, 6f)] private float sizeBiasExponent = 2.4f;
    [Tooltip("Color base de las estrellas — dorado cálido por defecto, coherente con 'El Sendero de las Estrellas'. Canales por encima de 1 a propósito: con Bloom activo en el Volume de la escena da un brillo extra; sin Bloom, se ve como un dorado saturado normal. Cada estrella varía ligeramente su brillo (ver brightnessVariance) para que el domo no se vea uniforme.")]
    [SerializeField] private Color starColor = new Color(1.9f, 1.55f, 0.35f);
    [Tooltip("Color alternativo 'frío' (plateado/azulado) que adoptan algunas estrellas sueltas — ver altStarChance. Rompe la monotonía de un domo enteramente dorado, como en un cielo real con estrellas de distinto tono.")]
    [SerializeField] private Color starColorAlt = new Color(1.3f, 1.4f, 1.8f);
    [Tooltip("Probabilidad (0-1) de que una estrella dada use starColorAlt en vez de starColor. Bajo a propósito (por defecto ~1 de cada 8) para que el domo siga leyéndose como 'dorado' con solo unos pocos acentos fríos.")]
    [SerializeField, Range(0f, 1f)] private float altStarChance = 0.12f;
    [Tooltip("Cuánto varía el brillo BASE de estrella a estrella (0 = todas iguales, 1 = algunas casi blancas y otras muy tenues). Se combina multiplicando con el parpadeo animado (ver twinkleIntensity).")]
    [SerializeField, Range(0f, 1f)] private float brightnessVariance = 0.6f;

    [Header("Parpadeo (fix 20 ago 2026 — antes las estrellas no brillaban, brillo fijo)")]
    [Tooltip("Cuánto varía el brillo de cada estrella con el tiempo. 0 = sin parpadeo (brillo fijo). 1 = en el valle de su ciclo casi se apaga del todo. Cada estrella tiene su propia fase y velocidad (ver twinkleSpeedRange) para que no parpadeen todas a la vez ni en fase.")]
    [SerializeField, Range(0f, 1f)] private float twinkleIntensity = 0.55f;
    [Tooltip("Rango de velocidad de parpadeo (ciclos por segundo, aprox.) — cada estrella sortea su propia velocidad dentro de este rango UNA vez, en BuildDomeIfNeeded.")]
    [SerializeField] private Vector2 twinkleSpeedRange = new Vector2(0.4f, 1.6f);
    [Tooltip("Cada cuántos segundos se recalcula el parpadeo de todas las estrellas. El parpadeo es lento (menos de 2 ciclos/seg como mucho), así que no hace falta todos los frames: con este intervalo se reparte mejor el coste de escribir hasta starCount MaterialPropertyBlock por Update.")]
    [SerializeField] private float twinkleUpdateInterval = 0.05f;

    [Header("Cobertura total del mundo")]
    [Tooltip("Igual que CloudCoverSpawner: cada 'recenterCheckInterval' segundos se comprueba la distancia del jugador al centro actual del domo y, si supera la mitad del radio activo, se recoloca (solo se mueve _root, sin volver a instanciar nada).")]
    [SerializeField] private float recenterCheckInterval = 2f;

    [Header("Transición")]
    [Tooltip("Segundos que tardan las estrellas en aparecer/disiparse.")]
    [SerializeField] private float fadeDuration = 6f;

    // Margen (0-1) aplicado al farClipPlane de la cámara para calcular el radio real del domo — ver
    // ComputeActiveDomeRadius y el comentario de clase. A propósito NO es un [SerializeField]: en la
    // pasada anterior este mismo valor SÍ era un campo de Inspector (0.85 por defecto) y es
    // precisamente el sospechoso número uno de por qué las estrellas seguían delante de las
    // montañas — un margen del 15% empuja el domo muy por dentro del farClipPlane, dejando una franja
    // ancha (justo donde suele estar el perfil de las montañas de fondo) en la que cualquier montaña
    // real queda MÁS LEJOS que las estrellas. Aquí solo hace falta un margen mínimo por precisión de
    // coma flotante al comparar contra el propio límite de dibujado de la cámara, así que se fija en
    // código (0.99) en vez de exponerlo como otro número que ajustar a ciegas sin Editor.
    private const float FarClipMarginFactor = 0.99f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform _root;
    private Transform _followTransform;
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<float> _rendererBrightness = new List<float>();
    private readonly List<float> _twinklePhase = new List<float>();
    private readonly List<float> _twinkleSpeed = new List<float>();
    private readonly List<Color> _starTint = new List<Color>();
    private MaterialPropertyBlock _mpb;
    private Material _starMaterial;
    private Coroutine _fadeCoroutine;
    private float _currentAlpha;
    private float _recenterTimer;
    private float _twinkleTimer;
    private bool _built;
    private bool _suppressedIndoors;
    // Radio REAL usado en la construcción actual del domo — ver ComputeActiveDomeRadius. Se usa
    // tanto para colocar las estrellas como para calcular su tamaño en mundo, así el tamaño en
    // pantalla no varía aunque el radio real sí lo haga (p.ej. entre escenas con distinto farClipPlane).
    private float _activeDomeRadius;

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        _mpb = new MaterialPropertyBlock();
        BuildStarMaterial();
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
            dayNightCycle.TimeOfDayChanged += HandleTimeOfDayChanged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
            Debug.LogWarning("[NightSkyStarSpawner] No se encontró ningún DayNightCycle en la escena; el domo de estrellas nunca se activará.");
#endif

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        var ec = EnvironmentController.Instance;
        _suppressedIndoors = ec != null && ec.CurrentMode == EnvironmentMode.Interior;

        // Igual que DayNightCycle.InitializeCycleDelayed: un frame de margen para que el orden de
        // Awake/OnEnable entre escenas/scripts no nos deje mirando un CurrentTimeOfDay todavía sin
        // inicializar. Si la escena arranca ya de noche, esto hace aparecer el domo sin esperar al
        // siguiente cambio de franja.
        StartCoroutine(CheckInitialStateDelayed());
    }

    IEnumerator CheckInitialStateDelayed()
    {
        yield return null;
        if (dayNightCycle != null && dayNightCycle.CurrentTimeOfDay == DayNightCycle.TimeOfDay.Night)
        {
            BuildDomeIfNeeded();
            StartFade(1f);
        }
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

        DestroyDome();
    }

    void OnDestroy()
    {
        if (_starMaterial != null) Destroy(_starMaterial);
    }

    void HandleTimeOfDayChanged(DayNightCycle.TimeOfDay t)
    {
        if (t == DayNightCycle.TimeOfDay.Night)
        {
            BuildDomeIfNeeded();
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
        if (!_built || _currentAlpha <= 0f || _root == null) return;

        _recenterTimer += Time.deltaTime;
        if (_recenterTimer >= recenterCheckInterval)
        {
            _recenterTimer = 0f;
            CheckRecenter();
        }

        // Parpadeo: no hace falta todos los frames (ver tooltip de twinkleUpdateInterval). Durante
        // un fundido en curso, FadeRoutine ya llama a ApplyAlpha cada frame (el parpadeo se anima
        // ahí también, de propina). Ya no hace falta reorientar nada hacia cámara (billboard): con
        // esferas 3D reales (ver comentario de clase, 24 ago 2026) se ven igual de "sólidas" desde
        // cualquier ángulo sin ningún cálculo extra.
        _twinkleTimer += Time.deltaTime;
        if (_twinkleTimer >= twinkleUpdateInterval)
        {
            _twinkleTimer = 0f;
            ApplyAlpha(_currentAlpha);
        }
    }

    /// <summary>Mismo mecanismo que CloudCoverSpawner.CheckRecenter (ver ese script): recoloca el
    /// domo YA CONSTRUIDO cuando el jugador se acerca al borde, sin volver a instanciar nada.</summary>
    void CheckRecenter()
    {
        Transform playerT = PlayerService.Player != null ? PlayerService.Player.transform : _followTransform;
        if (playerT == null) return;

        float recenterThreshold = _activeDomeRadius * 0.5f;
        if ((playerT.position - _root.position).sqrMagnitude <= recenterThreshold * recenterThreshold) return;

        _root.position = playerT.position;
        _followTransform = playerT;
    }

    void BuildDomeIfNeeded()
    {
        if (_built) return;

        _followTransform = PlayerService.Player != null ? PlayerService.Player.transform :
                            Camera.main != null ? Camera.main.transform : null;

        _root = new GameObject("[NightSkyStars]").transform;
        if (_followTransform != null)
            _root.position = _followTransform.position;

        // Radio real de esta construcción — anclado al farClipPlane de la cámara activa, ver
        // ComputeActiveDomeRadius y el comentario de clase (24 ago 2026).
        _activeDomeRadius = ComputeActiveDomeRadius();

        // Reparto uniforme en la mitad superior de una esfera (espiral de Fibonacci) con un pequeño
        // jitter aleatorio por estrella (a petición explícita de Raúl, "de forma aleatoria por el
        // cielo") — el Fibonacci da una cobertura pareja sin huecos ni amontonamientos (lo que un
        // muestreo puramente aleatorio SÍ produciría con solo ~220 puntos), y el jitter rompe la
        // regularidad exacta del patrón matemático para que no se note como una rejilla geométrica.
        float minElevationRad = minElevationDegrees * Mathf.Deg2Rad;
        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        int placed = 0;
        int attempts = 0;
        int maxAttempts = starCount * 4;

        float verticalFovDeg = Camera.main != null ? Camera.main.fieldOfView : 60f;
        float screenHeight = Mathf.Max(1f, Screen.height);

        while (placed < starCount && attempts < maxAttempts)
        {
            attempts++;
            // y en [0,1]: solo mitad superior de la esfera (cielo, no bajo el horizonte).
            float y = (attempts % starCount) / (float)Mathf.Max(1, starCount - 1);
            float radiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = goldenAngle * attempts;
            Vector3 dir = new Vector3(Mathf.Cos(theta) * radiusAtY, y, Mathf.Sin(theta) * radiusAtY);
            dir = (dir + UnityEngine.Random.insideUnitSphere * 0.06f).normalized;

            if (Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) < minElevationRad) continue;

            SpawnStar(dir, verticalFovDeg, screenHeight);
            placed++;
        }

        _currentAlpha = 0f;
        ApplyAlpha(0f);
        _built = true;

        if (_suppressedIndoors)
            _root.gameObject.SetActive(false);
    }

    /// <summary>
    /// Radio real del domo: SIEMPRE anclado al <c>farClipPlane</c> de la cámara activa (con
    /// <see cref="FarClipMarginFactor"/> como único margen, mínimo, solo por precisión numérica) —
    /// ver comentario de clase (24 ago 2026) para el razonamiento completo de por qué esto garantiza
    /// la oclusión correcta contra montañas/terreno sin adivinar ninguna distancia ni depender de que
    /// el fondo tenga collider. Solo si no hay ninguna cámara activa (caso raro) se usa
    /// <see cref="domeRadius"/> como último recurso.
    /// </summary>
    float ComputeActiveDomeRadius()
    {
        if (Camera.main == null) return domeRadius;
        return Camera.main.farClipPlane * FarClipMarginFactor;
    }

    /// <summary>
    /// Convierte un tamaño en <paramref name="pixels"/> de pantalla a unidades de mundo, usando el
    /// FOV vertical de la cámara activa y la <paramref name="distance"/> a la que va a quedar la
    /// estrella (siempre <see cref="_activeDomeRadius"/> en este diseño: todas las estrellas viven al
    /// mismo radio, anclado al farClipPlane — ver comentario de clase).
    /// </summary>
    float PixelSizeToWorldSize(float pixels, float verticalFovDeg, float screenHeight, float distance)
    {
        float angularSizeDeg = pixels / screenHeight * verticalFovDeg;
        return 2f * distance * Mathf.Tan(Mathf.Clamp(angularSizeDeg, 0f, 179f) * 0.5f * Mathf.Deg2Rad);
    }

    void SpawnStar(Vector3 direction, float verticalFovDeg, float screenHeight)
    {
        // Esfera 3D real (fix 24 ago 2026, ver comentario de clase) — no un Quad con billboard. Se
        // ve "sólida" desde cualquier ángulo sin ningún cálculo de orientación hacia cámara.
        var instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        instance.name = "Star";

        // No hace falta colisión para un punto de luz decorativo lejano — igual que las nubes, que
        // se marcan sin sombra propia ni recibida (ver CollectRenderers de CloudCoverSpawner).
        var collider = instance.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        instance.transform.SetParent(_root, false);
        instance.transform.localPosition = direction * _activeDomeRadius;

        // Sorteo sesgado hacia el extremo pequeño (ver sizeBiasExponent): la mayoría de estrellas
        // quedan como puntos discretos y solo unas pocas llegan cerca del máximo del rango.
        float t = Mathf.Pow(UnityEngine.Random.value, Mathf.Max(0.01f, sizeBiasExponent));
        float pixelSize = Mathf.Lerp(starScreenSizePixelsRange.x, starScreenSizePixelsRange.y, t);
        float size = PixelSizeToWorldSize(pixelSize, verticalFovDeg, screenHeight, _activeDomeRadius);
        instance.transform.localScale = Vector3.one * size;

        var renderer = instance.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = _starMaterial;

        _renderers.Add(renderer);
        _rendererBrightness.Add(1f - UnityEngine.Random.value * brightnessVariance);
        _twinklePhase.Add(UnityEngine.Random.Range(0f, Mathf.PI * 2f));
        _twinkleSpeed.Add(UnityEngine.Random.Range(twinkleSpeedRange.x, twinkleSpeedRange.y));
        _starTint.Add(UnityEngine.Random.value < altStarChance ? starColorAlt : starColor);
    }

    /// <summary>
    /// Material compartido por todas las estrellas — <c>Sprites/Default</c>, el mismo shader que ya
    /// usaba este script antes de este fix (built-in, alpha blend, sin cull, ya confirmado compatible
    /// con URP en este proyecto — ver comentario de clase). Sin <c>mainTexture</c> asignada usa su
    /// blanco opaco por defecto: con una malla de esfera real (no un Quad plano) eso ya da una
    /// estrella sólida sin necesitar ninguna textura generada. La oclusión contra montañas/terreno la
    /// garantiza <see cref="ComputeActiveDomeRadius"/> (anclado al farClipPlane de la cámara), no el
    /// shader — ver comentario de clase.
    /// </summary>
    void BuildStarMaterial()
    {
        _starMaterial = new Material(Shader.Find("Sprites/Default"));
        _starMaterial.enableInstancing = true;
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

    /// <summary>
    /// Recalcula el color+alfa de TODAS las estrellas para el nivel de fundido global dado. Se llama
    /// tanto desde FadeRoutine (cada frame, mientras el domo aparece/desaparece) como desde Update()
    /// cada twinkleUpdateInterval segundos en estado estable — así el parpadeo sigue vivo aunque no
    /// haya ningún fundido en curso. Sprites/Default hace blending por canal alfa de verdad (a
    /// diferencia de un shader opaco), así que el fundido sigue animando el canal alfa como siempre.
    /// </summary>
    void ApplyAlpha(float alpha)
    {
        float time = Time.time;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;

            float brightness = i < _rendererBrightness.Count ? _rendererBrightness[i] : 1f;
            float phase = i < _twinklePhase.Count ? _twinklePhase[i] : 0f;
            float speed = i < _twinkleSpeed.Count ? _twinkleSpeed[i] : 1f;
            // Oscila entre (1 - twinkleIntensity) y 1: nunca más brillante que el "techo" fijado
            // por brightnessVariance, solo se atenúa periódicamente.
            float twinkle = Mathf.Lerp(1f - twinkleIntensity, 1f, Mathf.Sin(time * speed + phase) * 0.5f + 0.5f);

            Color c = i < _starTint.Count ? _starTint[i] : starColor;
            c.a = alpha * brightness * twinkle;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    void DestroyDome()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _root = null;
        _renderers.Clear();
        _rendererBrightness.Clear();
        _twinklePhase.Clear();
        _twinkleSpeed.Clear();
        _starTint.Clear();
        _built = false;
        _currentAlpha = 0f;
    }

    [ContextMenu("Activar/Desactivar domo de estrellas (debug)")]
    public void DebugToggleDome()
    {
        if (_built && _currentAlpha > 0f)
            StartFade(0f);
        else
        {
            BuildDomeIfNeeded();
            StartFade(1f);
        }
    }
}
