using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera un techo de nubes estilizadas (mallas con el shader Quibli/Cloud3D — recomendado, ver
/// prefabs QuibliRainCloud3D_1..4 en Assets/Prefabs/VFX/ — quads con Quibli/Cloud2D, o cualquier
/// otro prefab de nube que se asigne) alrededor del jugador la PRIMERA vez que el cielo se está
/// nublando, para que se vea un cielo literalmente cubierto de nubes (sin skybox visible entre
/// huecos) en vez de solo un cambio de material de skybox.
///
/// El techo se construye UNA sola vez y luego queda FIJO en el mundo (no sigue al jugador frame
/// a frame). En lluvias posteriores se reutiliza el mismo conjunto de mallas ya instanciadas
/// (pool manual vía SetActive, sin volver a golpear Instantiate/Destroy) y solo se anima la
/// llegada/partida de cada nube (ver PARTE NUEVA más abajo).
///
/// Totalmente desacoplado de DayNightCycle: se limita a escuchar sus eventos
/// (CloudsBuildingUp / RainStopped), tal como pide la arquitectura del proyecto (comunicación
/// entre sistemas por eventos C#, no referencias directas entre managers).
///
/// 13 ago 2026: restaurado el modo QuibliCloud3D (había existido antes, en el commit
/// cf6ca2002/bfb27c983, y se revirtió en 81d65a9cc por daño colateral en OTROS materiales del
/// proyecto — no por un problema de este sistema en sí). El alcance de ESTE componente sigue
/// siendo el mismo que ya existía: el techo completo como aviso previo a la lluvia/tormenta real.
///
/// 15 ago 2026: la nubosidad ambiental independiente (TDD.md §16 Parte B, antes aparcada) ya está
/// implementada, pero en un componente aparte: <see cref="AmbientCloudDirector"/> +
/// <see cref="AmbientCloudDrifter"/>. Ese sistema se ocupa de las nubes sueltas que van y vienen
/// sin relación con la lluvia (y de dejar el cielo nublado un rato tras una tormenta, aclarando
/// poco a poco); este sigue encargándose solo del techo denso de tormenta real.
///
/// 20 ago 2026 — FIX "las nubes aparecen y desaparecen de golpe": antes de este fix, TODA la
/// rejilla (hasta <see cref="maxCloudInstances"/> nubes) se construía ya en su posición final y
/// solo se animaba un <c>_currentAlpha</c> GLOBAL compartido por todas las mallas a la vez —
/// sincronizado además con <c>DayNightCycle.RainDarkenTransitionDuration</c> (4s por defecto en el
/// Inspector de las escenas de mundo), así que las 250 nubes se materializaban/disipaban todas
/// juntas en un puñado de segundos: se leía como un parpadeo de pantalla, no como nubes viniendo
/// y yéndose. Ahora cada nube tiene su PROPIA posición "lejana" (fuera de la rejilla, más alta) y
/// su propio retraso de inicio dentro de una "ola" de llegada/partida: la primera nube empieza a
/// moverse/formarse en el instante 0, la última <see cref="waveSpreadDuration"/> segundos después,
/// y cada una tarda <see cref="perCloudTravelDuration"/> segundos en recorrer su propio trayecto
/// lejos→hueco (o hueco→lejos al irse) con una ligera variación aleatoria
/// (<see cref="durationJitter"/>) para que no se note la sincronía. El resultado: las nubes se ven
/// llegar de lejos y acumularse poco a poco, y al escampar se ven alejarse y "salir del mundo" una
/// a una en vez de desvanecerse todas en el sitio a la vez. <see cref="syncWithRainDarken"/> y
/// <see cref="fadeDuration"/> quedan SIN USO por esta corrutina (se dejan serializados sin más para
/// no perder el valor ya ajustado en las escenas existentes, ver comentario en esos campos).
///
/// 20 ago 2026 (segunda pasada, misma tarde) — el intento anterior de arreglar "en Game view se
/// corta de golpe" fue adelantar el fundido de alfa para que llegara a 0 antes de que la nube
/// terminara su trayecto (<see cref="departureAlphaFraction"/>). Probado en build real, el corte
/// seguía notándose: por un lado, para nubes que la cámara SÍ sigue viendo todo el trayecto (mirando
/// al cielo fijamente) el alfa se apagaba "porque sí" mientras la nube aún se veía perfectamente en
/// pantalla, lejos de cualquier borde — un fundido raro en vez de un corte, pero igual de artificial;
/// por otro, para nubes que salen de encuadre ANTES de llegar a ese 45% del trayecto (frecuente: el
/// umbral es un porcentaje FIJO del recorrido, no depende de por dónde mire realmente la cámara en
/// cada caso concreto) el corte original seguía intacto. En resumen: adivinar CUÁNDO sale una nube
/// de cámara con un temporizador fijo no puede funcionar para todos los ángulos de cámara y todas
/// las posiciones de nube a la vez. Sustituido por la solución correcta: comprobar de verdad, cada
/// pocos frames, si el Bounds de cada nube en PARTIDA sigue dentro del frustum de la cámara de
/// JUEGO (<see cref="GeometryUtility.TestPlanesAABB"/> contra <c>Camera.main</c>, nunca contra la
/// cámara libre del Editor) — en cuanto deja de estarlo, se completa su <c>formationT</c> al
/// instante porque, por definición, nadie puede estar viendo ese salto. Mientras siga dentro de
/// encuadre, sigue viajando a su ritmo normal con el alfa ligado 1:1 a la posición (sin adelantarlo
/// como antes): si el jugador se queda mirando cómo se aleja una nube hasta perderla de vista de
/// verdad, la ve alejarse y encogerse de forma continua, nunca desvanecerse de golpe a media
/// distancia. <see cref="departureAlphaFraction"/> queda SIN USO por el mismo motivo que
/// <see cref="syncWithRainDarken"/>/<see cref="fadeDuration"/> (se deja serializado para no perder
/// el valor ya ajustado en las escenas existentes). Ver <see cref="cutDepartingCloudsOutsideCamera"/>.
/// </summary>
public class CloudCoverSpawner : MonoBehaviour
{
    public enum CloudShaderMode { QuibliCloud3D, QuibliCloud2D, LegacyBaseColor }

    /// <summary>
    /// Estado de UNA nube del techo: su malla instanciada, sus renderers (para el fundido), su
    /// posición de reposo final (dentro de la rejilla) y su posición "lejana" de llegada/partida,
    /// más el retraso y la duración individual (con jitter) que le tocaron dentro de la ola actual.
    /// <c>formationT</c> (0 = en la posición lejana e invisible, 1 = en su hueco y visible del
    /// todo) persiste entre olas: si se interrumpe una partida a medio camino porque vuelve a
    /// llover, la siguiente ola de llegada continúa desde donde se quedó (ver FormationWaveRoutine)
    /// en vez de teletransportar la nube de vuelta a la posición lejana.
    /// </summary>
    private class CloudUnit
    {
        public Transform transform;
        public Renderer[] renderers;
        public Vector3 targetLocalPos;
        public Vector3 farLocalPos;
        public float delayOffset01;
        public float durationMultiplier;
        public float formationT;
    }

    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Nubes")]
    [Tooltip("Prefabs de nube a repartir por el techo. Recomendado: QuibliRainCloud3D_1..4 (Assets/Prefabs/VFX/), mallas Cloud3D de Quibli como las del [Demo] SampleSceneWithQuibli. Se elige uno al azar por instancia.")]
    [SerializeField] private GameObject[] cloudPrefabs;
    [Tooltip("Altura sobre el jugador a la que se coloca el CENTRO del techo de nubes la PRIMERA vez que se construye (después el techo queda fijo en el mundo, no vuelve a recalcularse aunque el jugador se mueva). Las nubes NO tienen collider, así que si el jugador puede volar (PlayerFlyingController) las atraviesa sin más: por debajo se ve el cielo cubierto, por encima el cielo/skybox normal (el skybox nunca se toca, así que el sol sigue ahí arriba). Si minClearanceAboveFollowTarget detecta que esta altura no basta para las mallas ya escaladas, se sube automáticamente.")]
    [SerializeField] private float cloudHeight = 90f;
    [Tooltip("Radio horizontal alrededor del jugador que cubre el techo de nubes. Cuanto más grande, menos se nota el borde del área cubierta, pero más instancias hacen falta.")]
    [SerializeField] private float coverRadius = 150f;
    [Tooltip("Separación aproximada entre nubes de la rejilla. Más bajo = más denso = tapa mejor el cielo, pero más nubes instanciadas (y más overdraw con quads transparentes).")]
    [SerializeField] private float cellSize = 30f;
    [Tooltip("Variación aleatoria de posición dentro de cada celda de la rejilla, para que no se note el patrón regular.")]
    [SerializeField, Range(0f, 1f)] private float jitter = 0.5f;
    [Tooltip("Escala mínima/máxima aplicada a cada nube, MULTIPLICANDO la escala base del prefab (los QuibliRainCloud3D_X ya vienen normalizados a ~25-33 unidades de ancho a escala 1). Con 0.8-1.5 y cellSize 30 las nubes se tocan/solapan lo justo para leerse como un techo de tormenta sin dejar huecos grandes. minClearanceAboveFollowTarget protege contra el caso de que la cámara acabe dentro de una nube.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.5f);
    [Tooltip("Límite de seguridad de instancias, por si coverRadius/cellSize generan una rejilla enorme.")]
    [SerializeField] private int maxCloudInstances = 300;
    [Tooltip("Variación aleatoria de altura (±) de cada nube respecto al plano del techo. Rompe el plano perfecto (más natural) y evita que todos los quads transparentes queden coplanares, lo que provoca artefactos de ordenación al mirarlos desde abajo.")]
    [SerializeField] private float heightJitter = 12f;
    [Tooltip("Margen mínimo, en unidades de mundo, entre el punto más bajo de la malla de nubes ya instanciada/escalada y el jugador. Tras construir el techo se mide su altura REAL (no solo cloudHeight) y si no deja este margen, se sube el techo entero lo que haga falta. Es la protección contra 'la cámara se queda dentro de la nube' si cloudHeight/scaleRange quedan mal calibrados para el prefab que uses.")]
    [SerializeField] private float minClearanceAboveFollowTarget = 25f;
    [Tooltip("FIX 20 ago 2026 — 'las nubes se ven demasiado bajas / no se ven al pasar'. Investigado: la cámara en tercera persona (Invector) solo puede inclinarse hasta 40° hacia arriba, y con el FOV de 60° eso da un máximo de 70° de elevación — nunca justo encima de la cabeza (90°). Con cloudHeight como techo PLANO (comportamiento antiguo), una nube justo encima del jugador (radio≈0) exige 90° de elevación (siempre invisible) y hace falta alejarse mucho del centro para que se vea sin mirar hacia arriba. Ahora la altura de cada nube YA NO es plana: cerca del centro se queda en cloudHeight (como antes, siempre habrá un pequeño punto ciego justo encima, inevitable con cualquier altura), pero a partir de cierto radio la altura SUBE con la distancia para mantener más o menos esta elevación (grados) vista desde el centro del techo — así las nubes lejanas se ven altas y grandes de verdad (como una pared de tormenta en el horizonte) sin dejar de estar dentro del ángulo que la cámara alcanza sin forzar la vista hacia arriba. Bajar este valor agranda la zona 'sin mirar arriba'; subirlo hace que las nubes lejanas suban más rápido (más dramático, pero necesita más inclinación de cámara para verlas).")]
    [SerializeField, Range(5f, 45f)] private float horizonElevationDegrees = 30f;

    [Header("Cobertura total del mundo (FIX INC-074)")]
    [Tooltip("El techo se construye UNA vez y queda fijo (ver comentario de clase), así que solo cubre 'coverRadius' unidades alrededor del punto donde se activó por primera vez. Si el jugador se aleja lo bastante, sale por el borde y ve el skybox despejado en vez de nubes. Cada 'recenterCheckInterval' segundos se comprueba la distancia del jugador al centro actual y, si supera la mitad de coverRadius, se recoloca el techo YA CONSTRUIDO (solo se mueve _root, sin volver a instanciar nada) centrado en la nueva posición — así las nubes acaban cubriendo todo el mundo según se explora, sin volver a seguir al jugador cada frame (que era lo que causaba el temblor que motivó fijarlo originalmente).")]
    [SerializeField] private float recenterCheckInterval = 2f;
    [Tooltip("FIX 20 ago 2026 — otra causa de 'las nubes desaparecen de golpe': el recentrado de arriba movía _root de golpe, en un solo frame (sin interpolar). Si el jugador cruzaba el umbral de recentrado MIENTRAS el techo ya era visible (p.ej. a mitad de una tormenta larga), todo el techo se TELETRANSPORTABA de una posición del mundo a otra en el mismo frame — desde la cámara de juego eso se ve exactamente como 'las nubes de aquí desaparecen y aparecen allí', aunque el fundido en sí ya fuera gradual. Ahora el recentrado se anima suavemente durante este tiempo (segundos) en vez de saltar de golpe.")]
    [SerializeField] private float recenterPanDuration = 4f;

    private float _recenterTimer;
    private Coroutine _recenterCoroutine;

    [Header("Aspecto de tormenta")]
    [Tooltip("Color de sombreado de tormenta. Solo se usa en modo QuibliCloud2D (_ShadowColor) y LegacyBaseColor (_BaseColor). En QuibliCloud3D no hace falta: el tono tormentoso lo pone la propia luz de la escena al oscurecerse con la lluvia.")]
    [SerializeField] private Color stormCloudColor = new Color(0.42f, 0.43f, 0.47f);
    [Tooltip("Cuánto sombreado de tormenta (_ShadowAmount de Quibli/Cloud2D) tienen las nubes una vez formadas del todo. 0 = nubes blancas de buen tiempo, 1 = panza de tormenta muy marcada. Se anima junto a la formación: mientras la nube llega también se va oscureciendo.")]
    [SerializeField, Range(0f, 1f)] private float stormShadowAmount = 0.55f;
    [Tooltip("Shader de los prefabs de nube. QuibliCloud3D (por defecto y recomendado): mallas del Foliage Generator con Quibli/Cloud3D; el fundido 'erosiona' el recorte de alfa (_AlphaThreshold), con efecto de materializarse/disiparse. QuibliCloud2D: quads con Quibli/Cloud2D (_Opacity + _ShadowColor/_ShadowAmount). LegacyBaseColor: comportamiento antiguo (_BaseColor con alfa) para mallas tipo Low Poly.")]
    [SerializeField] private CloudShaderMode cloudShaderMode = CloudShaderMode.QuibliCloud3D;
    [Tooltip("Solo con QuibliCloud3D: valor de _AlphaThreshold cuando la nube está formada del todo (0.5 en el material del demo de Quibli, SampleScene_Cloud3D.mat). El fundido anima desde 1 (invisible) hasta este valor.")]
    [SerializeField, Range(0.05f, 1f)] private float visibleAlphaThreshold = 0.5f;
    [Tooltip("Solo con QuibliCloud2D: activa el billboard del shader (_Billboard) para que cada quad mire siempre a cámara. Si se desactiva, los quads se tumban mirando al suelo con giro aleatorio (aspecto de 'techo' plano).")]
    [SerializeField] private bool billboard = true;

    [Header("Llegada y partida (nubes progresivas)")]
    [Tooltip("Distancia EXTRA (más allá de su hueco en la rejilla, medida en horizontal desde el centro del techo) a la que espera cada nube antes de que le toque formarse, y hasta la que se aleja al irse. Así las nubes 'vienen de lejos' hacia su sitio en vez de aparecer ya puestas.")]
    [SerializeField] private float arrivalExtraDistance = 220f;
    [Tooltip("Altura EXTRA sobre su posición final desde la que desciende cada nube al llegar (y a la que vuelve a subir al irse), para reforzar la sensación de que vienen 'de lo alto y lejos' en vez de solo cruzar en horizontal.")]
    [SerializeField] private float arrivalExtraHeight = 50f;
    [Tooltip("Cuánto tarda en completarse la OLA de llegada/partida a lo largo de TODA la rejilla: la primera nube empieza a moverse en el instante 0, la última 'waveSpreadDuration' segundos después. Cuanto más alto, más se nota que las nubes se van acumulando una a una en vez de aparecer todas a la vez. Campo nuevo — no lo pisa ninguna escena existente, así que este valor por defecto ya se aplica tal cual.")]
    [SerializeField] private float waveSpreadDuration = 14f;
    [Tooltip("Cuánto tarda CADA nube, individualmente, en recorrer su propio trayecto lejos→hueco (o hueco→lejos al irse) una vez le toca el turno dentro de la ola. Se multiplica por un factor aleatorio por nube (ver durationJitter) para que no todas tarden exactamente lo mismo. Campo nuevo, mismo motivo que el anterior.")]
    [SerializeField] private float perCloudTravelDuration = 9f;
    [Tooltip("Variación aleatoria (±) del factor que multiplica perCloudTravelDuration en cada nube, para romper la sincronía perfecta entre nubes.")]
    [SerializeField, Range(0f, 0.9f)] private float durationJitter = 0.35f;
    [Tooltip("Multiplicador de perCloudTravelDuration SOLO al irse (tras RainStopped), para que la despedida se sienta un poco más pausada que la llegada ('se van yendo poco a poco'). 1 = misma duración que al llegar.")]
    [SerializeField, Range(1f, 3f)] private float departureDurationMultiplier = 1.3f;

    [Header("Corte por cámara (fix real de 'las nubes desaparecen de golpe', 2ª pasada)")]
    [Tooltip("Sustituye a departureAlphaFraction (ver comentario de clase): en vez de adivinar con un porcentaje fijo cuándo una nube en PARTIDA va a salir del encuadre, se comprueba de verdad cada 'cameraCullCheckEveryNFrames' frames si su Bounds sigue dentro del frustum de la cámara de juego (Camera.main). En cuanto deja de estarlo, esa nube completa su formationT al instante (nadie puede ver el salto). Si se desactiva, las nubes en partida vuelven a animar alfa y posición 1:1 hasta el final de su trayecto SIN corte alguno — solo apto si algún día se resuelve lo del FOV limitado por otra vía (p.ej. una cámara de juego que sí mire hacia arriba sin límite).")]
    [SerializeField] private bool cutDepartingCloudsOutsideCamera = true;
    [Tooltip("Cada cuántos frames se recalcula el frustum de la cámara y se comprueba qué nubes en partida han salido de plano. TestPlanesAABB es barato incluso con maxCloudInstances alto, pero no hace falta comprobarlo en TODOS los frames: 1 = cada frame (más preciso, el corte se nota como muchísimo 2-3 frames tarde, imperceptible), 3-5 = casi igual de preciso y bastante más barato con rejillas grandes.")]
    [SerializeField, Range(1, 10)] private int cameraCullCheckEveryNFrames = 3;

    [Header("Transición (heredado, ya no lo usa la ola de llegada/partida)")]
    [Tooltip("OBSOLETO desde el fix del 20 ago 2026 (ver comentario de clase): la ola de llegada/partida ahora usa siempre waveSpreadDuration/perCloudTravelDuration, nube a nube. Se deja este campo serializado tal cual para no perder el valor ya ajustado en las escenas existentes, pero ya no se lee en ningún sitio.")]
    [SerializeField] private bool syncWithRainDarken = true;
    [Tooltip("OBSOLETO — ver tooltip de syncWithRainDarken. Ya no se lee en ningún sitio.")]
    [SerializeField] private float fadeDuration = 6f;
    [Tooltip("OBSOLETO desde la 2ª pasada del fix del 20 ago 2026 (ver comentario de clase): adelantar el alfa un porcentaje fijo del trayecto no funcionaba para todos los ángulos de cámara. Sustituido por cutDepartingCloudsOutsideCamera, que comprueba el frustum de verdad en vez de adivinar. Se deja serializado para no perder el valor ya ajustado en las escenas existentes, pero ya no se lee en ningún sitio.")]
    [SerializeField, Range(0.1f, 0.95f)] private float departureAlphaFraction = 0.55f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // Propiedades del shader Quibli/Cloud2D (ver Assets/Plugins/Quibli/Shaders/Cloud2D.shadergraph).
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int ShadowAmountId = Shader.PropertyToID("_ShadowAmount");
    private static readonly int BillboardId = Shader.PropertyToID("_Billboard");
    // Propiedad del shader Quibli/Cloud3D (recorte de alfa que anima la formación/disipación).
    private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");

    private Transform _root;
    private Transform _followTransform;
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<CloudUnit> _units = new List<CloudUnit>();
    private MaterialPropertyBlock _mpb;
    private Coroutine _formationCoroutine;
    /// <summary>0 = objetivo actual es "todas fuera/invisibles", 1 = objetivo actual es "techo formado del todo". Solo indica hacia dónde se dirige la ola en curso, no el estado real de cada nube (ver CloudUnit.formationT para eso).</summary>
    private float _targetFormation;
    private float _safetyHeightBonus;
    // Corte por cámara (ver cutDepartingCloudsOutsideCamera): cámara de JUEGO cacheada (nunca la
    // del Editor) y buffer de planos de frustum reutilizado cada comprobación para no generar
    // basura de GC en cada ola de partida.
    private Camera _gameCamera;
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private int _cullFrameCounter;
    private bool _built;
    // FIX (16 ago 2026): "las nubes se ven en un interior" + "sigue lloviendo pero no hay nubes
    // tras teletransportarse fuera". Este componente estaba "totalmente desacoplado de
    // DayNightCycle" (ver doc de clase) a propósito, pero eso significaba que NUNCA se enteraba
    // de si el jugador está en un interior: DayNightCycle.HandleInteriorEntered() sí oculta la
    // lluvia/niebla (SetRainVisualActive/SetMistVisualActive) al entrar, pero ese método no toca
    // el techo de nubes de este componente en absoluto — de ahí que las nubes seguían viéndose
    // por encima del techo del interior aunque la lluvia/niebla ya se hubieran cortado
    // correctamente. Nos suscribimos aquí a los mismos eventos de EnvironmentController que ya
    // usa DayNightCycle, con el mismo patrón (ocultar al entrar, restaurar al salir).
    private bool _hiddenByInterior;

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        _mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp += HandleCloudsBuildingUp;
            dayNightCycle.RainStopped += HandleRainStopped;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning("[CloudCoverSpawner] No se encontró ningún DayNightCycle en la escena; el techo de nubes nunca se activará.");
        }
#endif

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        // Si ya estábamos en un interior al activarnos (p.ej. esta escena se cargó directamente
        // dentro de un interior), arrancar ya suprimidos — mismo criterio que usa
        // DayNightCycle.OnEnable() para _outdoorWeatherSuppressedIndoors.
        var ec = EnvironmentController.Instance;
        _hiddenByInterior = ec != null && ec.IsEffectivelyInterior;

        // FIX "sigue lloviendo pero no hay nubes tras teletransportarse": este componente vive en
        // la escena de mundo y se destruye/recrea con ella (ver DestroyCover() en OnDisable()).
        // Antes de este fix solo se enteraba de que hay que mostrar nubes vía el evento
        // CloudsBuildingUp, que DayNightCycle dispara al EMPEZAR a nublarse — pero si la escena se
        // recarga (p.ej. una recarga de escena disparada por un teletransporte) mientras YA estaba
        // lloviendo, DayNightCycle.InitializeCycle() restaura IsRaining=true directamente sin pasar
        // otra vez por esa transición de "empezando a nublarse", así que el evento nunca volvía a
        // dispararse y el techo de nubes no se reconstruía aunque la lluvia siguiera sonando/
        // cayendo con toda normalidad. Nos ponemos al día aquí explícitamente si ya está lloviendo.
        if (!_hiddenByInterior && dayNightCycle != null && dayNightCycle.IsRaining)
            HandleCloudsBuildingUp();
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp -= HandleCloudsBuildingUp;
            dayNightCycle.RainStopped -= HandleRainStopped;
        }

        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        if (_formationCoroutine != null)
        {
            StopCoroutine(_formationCoroutine);
            _formationCoroutine = null;
        }

        if (_recenterCoroutine != null)
        {
            StopCoroutine(_recenterCoroutine);
            _recenterCoroutine = null;
        }

        DestroyCover();
    }

    void HandleInteriorEntered()
    {
        _hiddenByInterior = true;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    void HandleInteriorExited()
    {
        _hiddenByInterior = false;
        // Solo reactivar si de verdad hay techo construido y con alguna nube visible (o llegando)
        // que mostrar — si nunca ha llovido en esta sesión, o ya se disipó del todo, no hay nada
        // que revelar.
        if (_built && _root != null && (_targetFormation > 0f || AnyUnitVisible()))
            _root.gameObject.SetActive(true);
    }

    bool AnyUnitVisible()
    {
        for (int i = 0; i < _units.Count; i++)
        {
            if (_units[i].formationT > 0.001f) return true;
        }
        return false;
    }

    void HandleCloudsBuildingUp()
    {
        if (!_built)
        {
            // _followTransform solo se usa aquí, para anclar el techo la primera vez que se
            // construye. Una vez construido queda fijo en el mundo: no hay LateUpdate que lo
            // reposicione por frame (eso era lo que hacía que las nubes "acompañaran" al jugador).
            _followTransform = PlayerService.Player != null ? PlayerService.Player.transform :
                                Camera.main != null ? Camera.main.transform : null;
            BuildCoverIfNeeded();
        }
        else if (_root != null)
        {
            // Pool: reactivar las mallas ya instanciadas en vez de Instantiate de nuevo.
            _root.gameObject.SetActive(true);
        }

        StartFormationWave(1f);

        // Si empieza a llover (o nos ponemos al día con una tormenta ya en marcha, ver OnEnable())
        // mientras el jugador está dentro de un interior, no revelar el techo todavía — se
        // mostrará solo, ya en el punto de la ola que le toque, en cuanto HandleInteriorExited()
        // lo reactive.
        if (_hiddenByInterior && _root != null)
            _root.gameObject.SetActive(false);
    }

    void HandleRainStopped()
    {
        StartFormationWave(0f);
    }

    void Update()
    {
        // Solo merece la pena comprobar el recentrado mientras el techo existe y hay algo visible
        // o en marcha (formándose o disipándose).
        if (!_built || _root == null) return;
        if (_targetFormation <= 0f && _formationCoroutine == null) return;

        _recenterTimer += Time.deltaTime;
        if (_recenterTimer < recenterCheckInterval) return;
        _recenterTimer = 0f;

        CheckRecenter();
    }

    /// <summary>
    /// FIX INC-074: ver tooltip de recenterCheckInterval. Recoloca el techo YA CONSTRUIDO (mover
    /// _root, sin Instantiate) cuando el jugador se acerca al borde de la cobertura actual, para
    /// que las nubes terminen cubriendo todo el mundo explorado en vez de quedarse ancladas al
    /// punto donde empezó a llover la primera vez. Mover _root no afecta a las posiciones LOCALES
    /// (targetLocalPos/farLocalPos) que usa la ola de llegada/partida, así que no interfiere con
    /// una animación en curso.
    ///
    /// 20 ago 2026: antes esto movía _root en el sitio, de golpe, en un solo frame — ver tooltip de
    /// recenterPanDuration para por qué eso se veía como "las nubes desaparecen de golpe" desde la
    /// cámara de juego. Ahora solo arranca (o redirige) una animación suave hacia la nueva posición.
    /// </summary>
    void CheckRecenter()
    {
        Transform playerT = PlayerService.Player != null ? PlayerService.Player.transform : _followTransform;
        if (playerT == null) return;

        Vector3 rootPos = _root.position;
        float dx = playerT.position.x - rootPos.x;
        float dz = playerT.position.z - rootPos.z;
        float distSqr = dx * dx + dz * dz;

        float recenterThreshold = coverRadius * 0.5f;
        if (distSqr <= recenterThreshold * recenterThreshold) return;

        Vector3 newPos = playerT.position;
        newPos.y = rootPos.y; // mantiene la altura ya calculada (incluye _safetyHeightBonus)
        _followTransform = playerT;

        if (_recenterCoroutine != null)
            StopCoroutine(_recenterCoroutine);
        _recenterCoroutine = StartCoroutine(SmoothRecenterRoutine(newPos));
    }

    IEnumerator SmoothRecenterRoutine(Vector3 targetPos)
    {
        Vector3 start = _root.position;
        float duration = Mathf.Max(0.05f, recenterPanDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            _root.position = Vector3.Lerp(start, targetPos, eased);
            yield return null;
        }

        _root.position = targetPos;
        _recenterCoroutine = null;
    }

    void BuildCoverIfNeeded()
    {
        if (_built || cloudPrefabs == null || cloudPrefabs.Length == 0) return;

        _root = new GameObject("[CloudCover]").transform;

        // Colocar ya el contenedor en su altura objetivo ANTES de instanciar, para que las nubes
        // nazcan en su posición de mundo real y podamos medir su altura real más abajo (si se
        // instancian en el origen y luego se mueve el root, mediríamos bounds que no representan
        // nada del mundo real todavía).
        _safetyHeightBonus = 0f;
        if (_followTransform != null)
        {
            Vector3 rootPos = _followTransform.position;
            rootPos.y += cloudHeight;
            _root.position = rootPos;
        }

        int half = Mathf.Max(1, Mathf.CeilToInt(coverRadius / Mathf.Max(1f, cellSize)));
        float radiusSqr = coverRadius * coverRadius;
        float jitterRange = cellSize * jitter * 0.5f;
        // Fix 20 ago 2026 (ver tooltip de horizonElevationDegrees): tan del ángulo al que queremos
        // que se vean las nubes lejanas desde el centro del techo. Más allá del radio en el que
        // radial*tanElev supera cloudHeight, la altura de cada nube SUBE con la distancia para
        // mantener ese ángulo — en vez del techo plano de antes, que era invisible salvo mirando
        // casi verticalmente hacia arriba en cuanto te acercabas al centro.
        float tanElev = Mathf.Tan(horizonElevationDegrees * Mathf.Deg2Rad);
        int spawned = 0;

        for (int gx = -half; gx <= half && spawned < maxCloudInstances; gx++)
        {
            for (int gz = -half; gz <= half && spawned < maxCloudInstances; gz++)
            {
                float baseX = gx * cellSize;
                float baseZ = gz * cellSize;
                if (baseX * baseX + baseZ * baseZ > radiusSqr) continue;

                var prefab = cloudPrefabs[UnityEngine.Random.Range(0, cloudPrefabs.Length)];
                if (prefab == null) continue;

                float x = baseX + UnityEngine.Random.Range(-jitterRange, jitterRange);
                float z = baseZ + UnityEngine.Random.Range(-jitterRange, jitterRange);
                float radial = Mathf.Sqrt(baseX * baseX + baseZ * baseZ);
                float extraHeight = Mathf.Max(0f, radial * tanElev - cloudHeight);
                float y = extraHeight + UnityEngine.Random.Range(-heightJitter, heightJitter);
                var targetLocalPos = new Vector3(x, y, z);

                // Se instancia YA en su posición final (targetLocalPos): así ApplySafetyClearance()
                // de más abajo mide los bounds reales del techo formado, antes de que cada unidad
                // se desplace a su posición lejana de espera (ver bucle justo después de esa
                // llamada). CloudRotation()/scaleRange no cambian entre "lejos" y "en su hueco",
                // solo la posición se anima.
                var instance = Instantiate(prefab, _root);
                instance.transform.localPosition = targetLocalPos;
                instance.transform.localRotation = CloudRotation();
                instance.transform.localScale = prefab.transform.localScale * UnityEngine.Random.Range(scaleRange.x, scaleRange.y);

                var unit = new CloudUnit
                {
                    transform = instance.transform,
                    targetLocalPos = targetLocalPos,
                    delayOffset01 = UnityEngine.Random.value,
                    durationMultiplier = 1f + UnityEngine.Random.Range(-durationJitter, durationJitter),
                    formationT = 0f,
                };
                unit.renderers = CollectRenderers(instance);
                _units.Add(unit);

                spawned++;
            }
        }

        ApplySafetyClearance();

        // Ahora que ya sabemos la posición final REAL de cada nube (incluyendo el ajuste de altura
        // de seguridad de ApplySafetyClearance, que solo desplaza _root, no las posiciones locales),
        // calculamos dónde espera cada una antes de que le toque formarse: lejos del centro del
        // techo, radialmente, y más alta — así "vienen de lejos" en vez de aparecer ya en su hueco.
        for (int i = 0; i < _units.Count; i++)
        {
            var unit = _units[i];
            Vector2 horizontal = new Vector2(unit.targetLocalPos.x, unit.targetLocalPos.z);
            Vector2 outwardDir = horizontal.sqrMagnitude > 0.01f
                ? horizontal.normalized
                : UnityEngine.Random.insideUnitCircle.normalized;

            Vector3 far = unit.targetLocalPos + new Vector3(outwardDir.x, 0f, outwardDir.y) * arrivalExtraDistance;
            far.y += arrivalExtraHeight;
            unit.farLocalPos = far;

            // Dejarla ya esperando en su posición lejana, invisible (formationT sigue en 0) — no
            // se ve ningún "salto" porque esto pasa antes de que _built pase a true y antes de que
            // el objeto llegue a renderizarse con ninguna otra pose.
            unit.transform.localPosition = far;
            ApplyUnitVisual(unit);
        }

        _built = true;
    }

    /// <summary>
    /// Rotación inicial de cada nube. Con el shader Quibli en modo billboard la orientación real
    /// la decide el shader (siempre de cara a cámara), así que se deja identidad. Sin billboard,
    /// los quads se tumban mirando al suelo (90º en X) con giro aleatorio para variar. En modo
    /// Cloud3D/legacy (mallas 3D) se mantiene el giro aleatorio en Y de siempre.
    /// </summary>
    Quaternion CloudRotation()
    {
        if (cloudShaderMode == CloudShaderMode.QuibliCloud2D)
            return billboard
                ? Quaternion.identity
                : Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);

        // Mallas 3D (QuibliCloud3D o LegacyBaseColor): giro aleatorio en Y para variar siluetas.
        return Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
    }

    /// <summary>
    /// Mide la altura REAL (bounds de los renderers ya instanciados y escalados, no solo
    /// cloudHeight) del techo de nubes recién construido. Si su punto más bajo queda a menos de
    /// minClearanceAboveFollowTarget del jugador, sube todo el techo lo que haga falta. Protege
    /// contra el caso "la cámara se queda literalmente dentro de una nube" (pantalla gris plana)
    /// si scaleRange/cloudHeight no encajan con el tamaño real de los prefabs asignados.
    /// </summary>
    void ApplySafetyClearance()
    {
        if (_followTransform == null || _renderers.Count == 0) return;

        Bounds combined = default;
        bool has = false;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            if (!has) { combined = r.bounds; has = true; }
            else combined.Encapsulate(r.bounds);
        }
        if (!has) return;

        float requiredMinY = _followTransform.position.y + minClearanceAboveFollowTarget;
        float deficit = requiredMinY - combined.min.y;
        if (deficit <= 0f) return;

        _safetyHeightBonus = deficit;
        _root.position += Vector3.up * deficit;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[CloudCoverSpawner] El techo de nubes no dejaba suficiente margen sobre el jugador (faltaban {deficit:F1} unidades); se ha subido automáticamente. Considera aumentar cloudHeight o reducir scaleRange para no depender de esta corrección.");
#endif
    }

    Renderer[] CollectRenderers(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            // Techo de nubes lejano: ni necesita proyectar sombra ni recibirla.
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            _renderers.Add(r);
        }
        return renderers;
    }

    void StartFormationWave(float target)
    {
        _targetFormation = target;
        if (_formationCoroutine != null)
            StopCoroutine(_formationCoroutine);
        _formationCoroutine = StartCoroutine(FormationWaveRoutine(target));
    }

    /// <summary>
    /// Cámara de JUEGO (nunca la del Editor) contra la que se comprueba si una nube en partida
    /// sigue dentro de plano — ver cutDepartingCloudsOutsideCamera. Se cachea pero se vuelve a
    /// resolver si deja de estar activa (cambio de escena, cámara desactivada, etc.).
    /// </summary>
    Camera ResolveGameCamera()
    {
        if (_gameCamera != null && _gameCamera.isActiveAndEnabled) return _gameCamera;
        _gameCamera = Camera.main;
        return _gameCamera;
    }

    /// <summary>Bounds de mundo combinados de todos los renderers de UNA nube, para el test de frustum.</summary>
    static Bounds GetUnitWorldBounds(CloudUnit unit)
    {
        var renderers = unit.renderers;
        Bounds combined = default;
        bool has = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            if (!has) { combined = r.bounds; has = true; }
            else combined.Encapsulate(r.bounds);
        }
        return combined;
    }

    /// <summary>
    /// Anima TODAS las nubes hacia <paramref name="target"/> (1 = formarse en su hueco, 0 =
    /// alejarse e invisibilizarse) en una ola escalonada: cada nube tiene su propio retraso de
    /// inicio (CloudUnit.delayOffset01 * waveSpreadDuration) y su propia duración de trayecto
    /// (perCloudTravelDuration * durationMultiplier, estirada con departureDurationMultiplier si
    /// target es 0). Usa Mathf.MoveTowards sobre formationT en vez de un Lerp con tiempo de inicio
    /// fijo: así, si esta ola interrumpe a la ANTERIOR a medio camino (p.ej. vuelve a llover justo
    /// cuando las nubes aún se estaban yendo), cada nube continúa suavemente desde su formationT
    /// actual hacia el nuevo objetivo en vez de saltar a la posición lejana de golpe. Mientras
    /// target es 0 (partida), también comprueba cada cameraCullCheckEveryNFrames frames si cada
    /// nube ya salió del frustum de la cámara de juego para completarla al instante (ver
    /// cutDepartingCloudsOutsideCamera y el comentario de clase, 2ª pasada del 20 ago 2026).
    /// </summary>
    IEnumerator FormationWaveRoutine(float target)
    {
        bool departing = target <= 0f;
        float perCloudBase = perCloudTravelDuration;
        if (departing)
            perCloudBase *= Mathf.Max(1f, departureDurationMultiplier);

        // Cota de seguridad por si algo deja alguna nube sin converger (p.ej. Time.deltaTime en un
        // frame larguísimo): span de la ola + la mayor duración individual posible + margen.
        float maxTime = waveSpreadDuration + perCloudBase * (1f + durationJitter) + 1f;
        float elapsed = 0f;
        bool allDone = false;
        _cullFrameCounter = 0;

        while (!allDone && elapsed < maxTime)
        {
            elapsed += Time.deltaTime;
            allDone = true;

            // Ver cutDepartingCloudsOutsideCamera: solo comprobamos el frustum mientras las nubes se
            // están YENDO — al llegar sí queremos que se vean materializarse, eso nunca fue la queja.
            bool doCullCheck = false;
            if (departing && cutDepartingCloudsOutsideCamera)
            {
                var gameCam = ResolveGameCamera();
                if (gameCam != null)
                {
                    _cullFrameCounter++;
                    if (_cullFrameCounter >= Mathf.Max(1, cameraCullCheckEveryNFrames))
                    {
                        _cullFrameCounter = 0;
                        GeometryUtility.CalculateFrustumPlanes(gameCam, _frustumPlanes);
                        doCullCheck = true;
                    }
                }
            }

            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                float cloudDelay = unit.delayOffset01 * waveSpreadDuration;
                if (elapsed < cloudDelay)
                {
                    allDone = false;
                    continue;
                }

                float duration = Mathf.Max(0.05f, perCloudBase * unit.durationMultiplier);
                float maxDelta = Time.deltaTime / duration;
                unit.formationT = Mathf.MoveTowards(unit.formationT, target, maxDelta);

                // La nube ya no está en pantalla: completarla al instante. El salto es invisible por
                // definición (nadie la está mirando), y así dejamos de depender de un porcentaje fijo
                // de trayecto que nunca podía acertar para todas las cámaras/posiciones a la vez.
                if (doCullCheck && unit.formationT > 0f
                    && !GeometryUtility.TestPlanesAABB(_frustumPlanes, GetUnitWorldBounds(unit)))
                {
                    unit.formationT = 0f;
                }

                ApplyUnitVisual(unit);

                if (Mathf.Abs(unit.formationT - target) > 0.001f)
                    allDone = false;
            }

            yield return null;
        }

        // Asentar exactamente en el valor final por si el bucle salió por la cota de seguridad.
        for (int i = 0; i < _units.Count; i++)
        {
            _units[i].formationT = target;
            ApplyUnitVisual(_units[i]);
        }

        _formationCoroutine = null;

        if (target <= 0f)
            DeactivateCover();
    }

    /// <summary>
    /// Aplica la posición (Lerp suavizado lejos↔hueco) y el fundido de material de UNA nube según
    /// su formationT actual. Sustituye al antiguo ApplyAlpha(float) global: cada nube ahora anima
    /// su propio recorrido en vez de compartir un único valor de alfa con todas las demás.
    ///
    /// Posición y alfa usan SIEMPRE el mismo progreso (eased, smoothstep de formationT) — a
    /// diferencia del intento anterior (departureAlphaFraction, ahora sin uso), aquí ya no se
    /// adelanta el alfa respecto a la posición: si una nube en partida sigue dentro de cámara todo
    /// su trayecto, se ve alejarse y desvanecerse de forma continua hasta el final. El corte real
    /// para cuando sale de plano antes de terminar lo hace FormationWaveRoutine, completando su
    /// formationT al instante en cuanto detecta que ya no está en el frustum de la cámara de juego
    /// (ver cutDepartingCloudsOutsideCamera) — momento en el que esta función ya ni se llama con un
    /// t intermedio para esa nube.
    /// </summary>
    void ApplyUnitVisual(CloudUnit unit)
    {
        float t = Mathf.Clamp01(unit.formationT);
        float eased = t * t * (3f - 2f * t); // smoothstep: acelera al salir, se asienta al llegar

        unit.transform.localPosition = Vector3.Lerp(unit.farLocalPos, unit.targetLocalPos, eased);

        Color legacyColor = stormCloudColor;
        legacyColor.a = eased;

        float shadowAmount = stormShadowAmount * eased;
        float billboardValue = billboard ? 1f : 0f;
        // Con Cloud3D, bajar el umbral desde >1 hasta el valor visible hace que la nube se
        // 'materialice' pixel a pixel (y se erosione al disiparse) — reforzando el efecto de
        // "llegar/irse" que ya aporta el movimiento de posición.
        float alphaThreshold = Mathf.Lerp(1.01f, visibleAlphaThreshold, eased);

        var renderers = unit.renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);
            switch (cloudShaderMode)
            {
                case CloudShaderMode.QuibliCloud3D:
                    _mpb.SetFloat(AlphaThresholdId, alphaThreshold);
                    break;
                case CloudShaderMode.QuibliCloud2D:
                    _mpb.SetFloat(OpacityId, eased);
                    _mpb.SetColor(ShadowColorId, stormCloudColor);
                    _mpb.SetFloat(ShadowAmountId, shadowAmount);
                    _mpb.SetFloat(BillboardId, billboardValue);
                    break;
                default:
                    _mpb.SetColor(BaseColorId, legacyColor);
                    break;
            }
            r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// Oculta el techo de nubes sin destruir las mallas (pool): las deja desactivadas (todas ya en
    /// su posición lejana e invisibles, formationT en 0) y fijas en su posición de mundo, listas
    /// para reaparecer en la próxima lluvia con solo un SetActive + su propia ola de llegada, sin
    /// volver a pagar el coste de Instantiate.
    /// </summary>
    void DeactivateCover()
    {
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    /// <summary>
    /// Limpieza real (destruye las mallas). Solo se usa cuando el propio componente se
    /// desactiva/destruye (p.ej. descarga de escena), no en cada ciclo de lluvia.
    /// </summary>
    void DestroyCover()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _root = null;
        _renderers.Clear();
        _units.Clear();
        _built = false;
        _targetFormation = 0f;
    }

    [ContextMenu("Activar/Desactivar techo de nubes (debug)")]
    public void DebugToggleCover()
    {
        if (_built && (_targetFormation > 0f || AnyUnitVisible()))
            HandleRainStopped();
        else
            HandleCloudsBuildingUp();
    }

    /// <summary>
    /// Activa/desactiva SOLO los renderers del techo ya construido, sin tocar el estado de
    /// formación en curso ni el de la lluvia/tormenta real. Pensado para ocultar el techo un
    /// momento durante un enfoque de cámara puntual (ver FocusCameraNode) sin interferir con
    /// CloudsBuildingUp/RainStopped: al restaurar (visible=true) el techo vuelve exactamente al
    /// punto de la ola en que estaba antes de ocultarlo.
    /// </summary>
    public void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r != null) r.enabled = visible;
        }
    }
}
