using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Domo de estrellas doradas hechas de GameObjects reales (no un truco de shader en el skybox ni
/// un ParticleSystem): mallas de "estrella" (destello de 6 puntas, ver <see cref="GenerateStarMesh"/>)
/// pequeñas repartidas sobre una esfera alrededor del jugador, concentradas hacia la parte alta del
/// cielo (ver <see cref="topBiasExponent"/>), tema "Sendero de las Estrellas". Mismo patrón
/// estructural que CloudCoverSpawner (rejilla/reparto +
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
/// 25 ago 2026 — con la oclusión ya resuelta ("ahora ya esta mejor"), Raúl pidió tres afinados de
/// diseño puramente estéticos (nada que ver con la oclusión, que no se toca en esta pasada):
/// **(1)** menos estrellas en total (<see cref="starCount"/> baja de 220 a 120: el cielo se veía
/// "todo lleno" y poco natural); **(2)** concentradas más hacia la parte alta del cielo en vez de
/// repartidas uniformemente por todo el domo visible — se sustituye el muestreo por rechazo
/// (generar candidatos y descartar los que caían bajo minElevationDegrees, que podía tardar varios
/// intentos por estrella) por generación DIRECTA: la altura <c>y</c> de cada estrella en la esfera
/// se sortea ya dentro de rango con <see cref="topBiasExponent"/> como exponente de sesgo — con
/// exponente 1 el reparto en altura es uniforme (como antes), y cuanto más alto, más estrellas caen
/// cerca del cenit y menos cerca del horizonte, igual que en un cielo real donde la vía láctea y la
/// mayor densidad de estrellas visibles se perciben más arriba; **(3)** la esfera lisa se sustituye
/// por una malla de "estrella"/destello real generada en código (ver <see cref="GenerateStarMesh"/>
/// y <see cref="SpawnStar"/>): tres pares de puntas opuestas a lo largo de los ejes locales X/Y/Z
/// que se cruzan en el centro (como un "pincho" de 6 puntas), con las puntas del eje Y algo más
/// cortas que las de X/Z para que la silueta no sea un octaedro perfectamente simétrico — cada
/// estrella recibe además una rotación aleatoria (<c>UnityEngine.Random.rotation</c>) para que el
/// domo no se vea con todas las puntas alineadas. Esta malla se genera UNA sola vez en Awake (igual
/// que el material, ver <see cref="_starMesh"/>) y se comparte vía <c>MeshFilter.sharedMesh</c> en
/// las ~120 instancias, así que el coste de generarla no se repite por estrella. El nombre del juego
/// es literalmente "El Sendero de las Estrellas", de ahí el cuidado en que las estrellas se lean
/// como estrellas y no como bolas.
///
/// 25 ago 2026 (mismo día, pasada siguiente) — Raúl preguntó si el nuevo script de constelaciones
/// (<c>NightSkyConstellationSpawner</c>, ver su comentario de clase) se complementaba con este polvo
/// de fondo o si corrían el riesgo de "pisarse". Respuesta corta: sin coordinación, sí — el sesgo
/// hacia el cenit de este mismo fix (<see cref="topBiasExponent"/>) concentra polvo justo en la misma
/// franja de elevación (50-60°) donde viven las constelaciones. Fix: <see cref="AvoidConstellationZones"/>
/// consulta <c>NightSkyConstellationSpawner.ExclusionZones</c> (lista <c>static</c> publicada por ese
/// script) y aparta cualquier estrella de polvo que caiga dentro del radio de una constelación,
/// rotándola justo lo mínimo a lo largo del mismo círculo máximo que la une con el centro de la zona —
/// conserva su rumbo original, no vuelve a sortear nada. Acoplamiento deliberado y de un solo sentido
/// (este script SÍ referencia el tipo <c>NightSkyConstellationSpawner</c>; al revés no) y con caída
/// elegante: si ese componente no está en la escena, la lista queda vacía y el comportamiento es
/// idéntico al de antes de este fix.
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
///
/// 25 ago 2026 (pasada 5) — REGRESIÓN "vuelve a pasar lo de las montañas, y las estrellas no deben
/// moverse con la cámara": Raúl reportó que la oclusión (ya dada por resuelta en la pasada 3) había
/// vuelto a fallar. Esta sesión SÍ tiene acceso al repo Unity completo (incluida la escena
/// `MainWorld.unity` en texto), a diferencia de las pasadas anteriores — eso permitió, por primera
/// vez, leer los valores REALMENTE serializados en el GameObject de la escena de Raúl en vez de solo
/// razonar sobre el código. Causa raíz confirmada por inspección directa de la escena: el
/// `MonoBehaviour` de este componente en `MainWorld.unity` seguía con `starCount: 220`,
/// `domeRadius: 220`, `minElevationDegrees: 8` y el `starColor` apagado de ANTES de las pasadas 1-4
/// — exactamente el problema ya descrito arriba ("Unity no aplica retroactivamente un nuevo valor
/// por defecto"), nunca corregido a mano en el Inspector porque ninguna sesión anterior podía abrir
/// el Editor ni editar la escena directamente. El campo `domeRadius: 220` en concreto es el MISMO
/// número que causaba el bug original del 23 ago (mountañas más lejos que el domo) — y aunque ya no
/// se usa para el cálculo normal (ver <see cref="ComputeActiveDomeRadius"/>, anclado al
/// farClipPlane), SÍ se usa como último recurso si <c>Camera.main</c> es null en el momento exacto
/// de <see cref="BuildDomeIfNeeded"/>. En un arranque en frío (partida nueva o carga directa a una
/// escena que empieza ya de noche) eso puede pasar durante el único frame de margen de
/// <see cref="CheckInitialStateDelayed"/>, sobre todo si otros sistemas de arranque (ver
/// `WorldBootstrap.cs`) tardan más de un frame en asignar la cámara real — y como
/// <see cref="_built"/> pasa a <c>true</c> en el primer build y el domo nunca se reconstruye, un
/// radio erróneo en ese primer build quedaba fijado el resto de la sesión, reintroduciendo
/// exactamente el bug de las montañas de forma intermitente (más probable en partidas nuevas que en
/// partidas cargadas, lo que encaja con que pareciera "iba bien" y luego "ha vuelto"). Dos arreglos:
/// **(1)** los cuatro valores obsoletos de `MainWorld.unity` se han corregido directamente en el
/// archivo de escena (starCount 120, domeRadius 900, minElevationDegrees 24, starColor dorado
/// vivo) — ya no hace falta que Raúl toque nada a mano en el Inspector. **(2)**
/// <see cref="CheckInitialStateDelayed"/> ahora espera activamente (<see cref="WaitForMainCamera"/>,
/// hasta <see cref="MaxCameraWaitFrames"/> frames) a que <c>Camera.main</c> exista de verdad antes
/// de construir el domo, en vez de asumir que un solo frame de margen basta — así el fallback de
/// <see cref="domeRadius"/> queda reservado de verdad para el caso raro que su tooltip siempre dijo
/// que era, no para una carrera de inicialización que podía perderse en cualquier arranque en frío.
/// Sobre "las estrellas no deben moverse con la cámara": revisado — este script nunca ha atado el
/// domo a la cámara (<see cref="_followTransform"/> sigue al jugador, con recentrado solo cada
/// <see cref="recenterCheckInterval"/> segundos y solo si se aleja más de la mitad del radio activo,
/// ver <see cref="CheckRecenter"/>); no se ha encontrado ningún código que lo ate a la cámara. Se
/// deja constancia aquí por si Raúl lo seguía viendo tras esta pasada, para no perder el aviso.
/// Además, a petición explícita ("las estrellas deben estar más altas"): <see cref="minElevationDegrees"/>
/// sube de 16 a 24 y <see cref="topBiasExponent"/> de 2.4 a 3.2 (ver tooltips de cada campo).
/// </summary>
public class NightSkyStarSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Domo de estrellas (malla de estrella real — reescrito 25 ago 2026, ver comentario de clase)")]
    [Tooltip("Número de estrellas del domo. Se construye UNA vez y queda fijo (igual que el techo de nubes), con recentrado periódico para seguir cubriendo el cielo según el jugador explora. Bajado de 220 a 120 el 25 ago 2026: con 220 el cielo se veía 'todo lleno' y poco natural.")]
    [SerializeField] private int starCount = 120;
    [Tooltip("Radio de la esfera de estrellas SOLO cuando no hay ninguna cámara activa todavía (caso raro, p.ej. el domo se construye antes de que Camera.main exista). Con cámara activa este valor NO se usa: el radio real se calcula siempre a partir del farClipPlane de la cámara — ver ComputeActiveDomeRadius y el comentario de clase.")]
    [SerializeField] private float domeRadius = 900f;
    [Tooltip("Elevación mínima sobre el horizonte (grados) a la que se colocan estrellas. Puramente estético (evita amontonar estrellas justo donde es más probable que haya monte cerca) — la garantía real de que ninguna estrella quede delante de una montaña la da ComputeActiveDomeRadius, ver comentario de clase, no este valor. Subido de 16 a 24 el 25 ago 2026 (pasada 5): Raúl pidió explícitamente 'las estrellas deben estar más altas' tras la pasada 4.")]
    [SerializeField, Range(0f, 40f)] private float minElevationDegrees = 24f;
    [Tooltip("Sesga el reparto en ALTURA de las estrellas hacia la parte alta del cielo (cenit). 1 = reparto uniforme entre minElevationDegrees y el cenit (como antes del 25 ago 2026). Más alto = cada vez más estrellas se concentran cerca de la parte alta y menos cerca del horizonte, para que el domo no se lea como 'todo el cielo lleno por igual'. Se aplica en generación directa (sin descartar candidatos) en BuildDomeIfNeeded. Subido de 2.4 a 3.2 el 25 ago 2026 (pasada 5), junto con minElevationDegrees, para reforzar el mismo pedido ('más altas').")]
    [SerializeField, Range(1f, 6f)] private float topBiasExponent = 3.2f;
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

    // Frames máximos que CheckInitialStateDelayed espera a que Camera.main exista antes de construir
    // el domo con el radio de último recurso (domeRadius) — ver WaitForMainCamera y el comentario de
    // clase (25 ago 2026, pasada 5). 30 frames son medio segundo a 60fps, tiempo de sobra para que
    // cualquier sistema de arranque normal (WorldBootstrap, etc.) asigne la cámara real.
    private const int MaxCameraWaitFrames = 30;

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
    // Malla de "estrella" compartida por TODAS las instancias vía MeshFilter.sharedMesh — generada
    // UNA sola vez en Awake (ver GenerateStarMesh) y nunca duplicada por estrella, igual que
    // _starMaterial. Ver comentario de clase (25 ago 2026).
    private Mesh _starMesh;
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
        GenerateStarMesh();
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
            // FIX (25 ago 2026, pasada 5) — ver comentario de clase: en un arranque en frío
            // Camera.main podía seguir sin existir en este único frame de margen, haciendo caer
            // ComputeActiveDomeRadius() en el domeRadius de último recurso para SIEMPRE (el domo no
            // se reconstruye una vez _built = true). Se espera aquí, unos pocos frames como mucho, a
            // que Camera.main esté listo de verdad.
            yield return WaitForMainCamera();
            BuildDomeIfNeeded();
            StartFade(1f);
        }
    }

    /// <summary>
    /// Espera hasta <see cref="MaxCameraWaitFrames"/> frames a que <c>Camera.main</c> deje de ser
    /// null. Con la cámara ya asignada, <see cref="ComputeActiveDomeRadius"/> siempre calcula el
    /// radio real a partir de su <c>farClipPlane</c> — el <see cref="domeRadius"/> de último recurso
    /// deja de entrar en juego salvo en el caso verdaderamente raro de que ni tras medio segundo haya
    /// ninguna cámara activa. Ver comentario de clase (25 ago 2026, pasada 5) para el porqué de este
    /// arreglo.
    /// </summary>
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
            Debug.LogWarning("[NightSkyStarSpawner] Camera.main sigue sin existir tras esperar " + MaxCameraWaitFrames + " frames; el domo se construirá con el radio de último recurso (domeRadius), que puede quedar más cerca que alguna montaña de fondo.");
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

        DestroyDome();
    }

    void OnDestroy()
    {
        if (_starMaterial != null) Destroy(_starMaterial);
        if (_starMesh != null) Destroy(_starMesh);
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
        // FIX (25 ago 2026): mismo hueco encontrado y confirmado KO en AmbientCloudDirector (ver
        // su Update()) — OnInteriorEntered/OnInteriorExited (ver OnEnable) solo cubren el flujo
        // "andando", nunca el cinemático (CinematicSequencerBase → BeginCinematicOverride +
        // ApplyInteriorForCinematic, p.ej. TabernaSequencer). Se corrige aquí de forma preventiva
        // (misma familia de sistemas de cielo, misma causa raíz) con el patrón ya establecido en
        // DayNightCycle.Update(). Sondeado ANTES del return de arriba a propósito: aunque no haya
        // domo construido todavía, hay que mantener _suppressedIndoors al día para que, si el domo
        // se construye más tarde (BuildDomeIfNeeded), nazca ya oculto si seguimos en un interior.
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

        // Reparto en la mitad superior de una esfera (espiral de Fibonacci, ángulo dorado) con un
        // pequeño jitter aleatorio por estrella (a petición explícita de Raúl, "de forma aleatoria
        // por el cielo") — el Fibonacci da una cobertura pareja sin huecos ni amontonamientos (lo
        // que un muestreo puramente aleatorio SÍ produciría con solo ~120 puntos), y el jitter rompe
        // la regularidad exacta del patrón matemático para que no se note como una rejilla
        // geométrica. Fix 25 ago 2026: generación DIRECTA de la altura "y" ya dentro del rango
        // válido [sin(minElevationDegrees), 1] con sesgo hacia el cenit (topBiasExponent) — ya no
        // hay muestreo por rechazo ni bucle de "attempts": cada índice produce siempre una estrella
        // válida, así que "placed" y "starCount" coinciden siempre.
        float minElevationRad = minElevationDegrees * Mathf.Deg2Rad;
        float minY = Mathf.Sin(minElevationRad);
        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        int denom = Mathf.Max(1, starCount - 1);
        float biasExponent = 1f / Mathf.Max(0.01f, topBiasExponent);

        float verticalFovDeg = Camera.main != null ? Camera.main.fieldOfView : 60f;
        float screenHeight = Mathf.Max(1f, Screen.height);

        for (int i = 0; i < starCount; i++)
        {
            // u en [0,1] uniforme (índice de Fibonacci); uBiased empuja la masa hacia 1 (cenit)
            // cuanto mayor es topBiasExponent — con topBiasExponent = 1 no hay sesgo (uBiased == u).
            float u = i / (float)denom;
            float uBiased = Mathf.Pow(u, biasExponent);
            float y = Mathf.Lerp(minY, 1f, uBiased);

            float radiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = goldenAngle * i;
            Vector3 dir = new Vector3(Mathf.Cos(theta) * radiusAtY, y, Mathf.Sin(theta) * radiusAtY);
            dir = (dir + UnityEngine.Random.insideUnitSphere * 0.06f).normalized;
            dir = AvoidConstellationZones(dir);

            SpawnStar(dir, verticalFovDeg, screenHeight);
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
    /// Aparta <paramref name="dir"/> de cualquier zona de constelación registrada en
    /// <see cref="NightSkyConstellationSpawner.ExclusionZones"/> (25 ago 2026 — ver el comentario de
    /// clase de ese script para el porqué: sin esto, el sesgo hacia el cenit de este mismo domo podía
    /// hacer que polvo de fondo cayera encima de una estrella guía o de una línea de constelación,
    /// justo la misma franja alta del cielo donde viven ambas cosas). Si <paramref name="dir"/> cae
    /// dentro de una zona, se rota a lo largo del mismo círculo máximo que la une con el centro de esa
    /// zona hasta quedar justo fuera de su radio (con un margen extra de 1°) — así se conserva el
    /// rumbo/dispersión original de la estrella, solo se la aleja lo mínimo imprescindible, sin volver
    /// a sortear nada ni perder ningún hueco en el reparto. Si <c>NightSkyConstellationSpawner</c> no
    /// está en la escena, la lista está vacía y esta función no cambia nada — comportamiento idéntico
    /// al de antes de este fix.
    /// </summary>
    static Vector3 AvoidConstellationZones(Vector3 dir)
    {
        var zones = NightSkyConstellationSpawner.ExclusionZones;
        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            float angle = Vector3.Angle(dir, zone.direction);
            if (angle >= zone.radiusDegrees) continue;

            Vector3 axis = Vector3.Cross(zone.direction, dir);
            if (axis.sqrMagnitude < 1e-6f)
                axis = Vector3.Cross(zone.direction, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f)
                axis = Vector3.Cross(zone.direction, Vector3.right);
            axis.Normalize();

            float targetAngle = zone.radiusDegrees + 1f;
            dir = Quaternion.AngleAxis(targetAngle, axis) * zone.direction;
        }
        return dir;
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
        // Malla de "estrella" real compartida (fix 25 ago 2026, ver GenerateStarMesh y comentario de
        // clase) — GameObject a pelo + MeshFilter/MeshRenderer en vez de CreatePrimitive: así nunca
        // se crea ningún Collider que luego haya que destruir (CreatePrimitive siempre añade uno).
        var instance = new GameObject("Star");
        instance.transform.SetParent(_root, false);
        instance.transform.localPosition = direction * _activeDomeRadius;
        // Rotación aleatoria por estrella para que las puntas no queden todas alineadas en el domo.
        instance.transform.localRotation = UnityEngine.Random.rotation;

        // Sorteo sesgado hacia el extremo pequeño (ver sizeBiasExponent): la mayoría de estrellas
        // quedan como puntos discretos y solo unas pocas llegan cerca del máximo del rango.
        float t = Mathf.Pow(UnityEngine.Random.value, Mathf.Max(0.01f, sizeBiasExponent));
        float pixelSize = Mathf.Lerp(starScreenSizePixelsRange.x, starScreenSizePixelsRange.y, t);
        float size = PixelSizeToWorldSize(pixelSize, verticalFovDeg, screenHeight, _activeDomeRadius);
        instance.transform.localScale = Vector3.one * size;

        var meshFilter = instance.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = _starMesh;

        var renderer = instance.AddComponent<MeshRenderer>();
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

    /// <summary>
    /// Genera UNA sola vez (llamado desde Awake) la malla de "estrella" que comparten todas las
    /// instancias vía <c>MeshFilter.sharedMesh</c> — fix 25 ago 2026 (ver comentario de clase), en
    /// respuesta a "habría que cambiar la esfera por algo que sea más una estrella". Es una malla
    /// muy barata (24 vértices, 24 triángulos): tres pares de puntas piramidales opuestas a lo largo
    /// de los ejes locales X, Y y Z que comparten el centro, como un "pincho"/destello de 6 puntas.
    /// Cada par de puntas es en realidad una bipirámide (dos pirámides de base cuadrada pegadas por
    /// esa base, con la base encogida a un rombo diminuto en el centro) — así una sola figura ya
    /// cubre las dos puntas opuestas de ese eje sin vértices duplicados en el centro. Las puntas del
    /// eje Y se dejan más cortas que las de X/Z (ver <c>armShort</c> más abajo) para que la silueta no
    /// sea un octaedro perfectamente simétrico, más parecida a un destello de 4 puntas con dos
    /// puntas extra más discretas. La rotación aleatoria por estrella (ver <see cref="SpawnStar"/>)
    /// hace que cuál de las puntas "se note más" desde cada ángulo varíe estrella a estrella.
    /// </summary>
    void GenerateStarMesh()
    {
        const float armLong = 1f;      // longitud de las puntas de los ejes X y Z.
        const float armShort = 0.55f;  // longitud de las puntas del eje Y (algo más cortas).
        const float waist = 0.09f;     // medio-ancho del rombo central compartido por las 3 bipirámides.

        var vertices = new List<Vector3>(24);
        var triangles = new List<int>(24);

        AddStarSpike(vertices, triangles, Vector3.right, Vector3.up, Vector3.forward, armLong, waist);    // puntas ±X
        AddStarSpike(vertices, triangles, Vector3.up, Vector3.right, Vector3.forward, armShort, waist);   // puntas ±Y (más cortas)
        AddStarSpike(vertices, triangles, Vector3.forward, Vector3.right, Vector3.up, armLong, waist);    // puntas ±Z

        _starMesh = new Mesh { name = "ProceduralStar" };
        _starMesh.SetVertices(vertices);
        _starMesh.SetTriangles(triangles, 0);
        _starMesh.RecalculateNormals();
        _starMesh.RecalculateBounds();
    }

    /// <summary>
    /// Añade a <paramref name="vertices"/>/<paramref name="triangles"/> una bipirámide (dos puntas
    /// opuestas a lo largo de <paramref name="axis"/>) cuyo "rombo" central vive en el plano
    /// perpendicular a ese eje, formado por <paramref name="waistA"/>/<paramref name="waistB"/> (los
    /// otros dos ejes locales) — ver <see cref="GenerateStarMesh"/> para el porqué de esta forma.
    /// </summary>
    static void AddStarSpike(List<Vector3> vertices, List<int> triangles, Vector3 axis, Vector3 waistA, Vector3 waistB, float armLength, float waist)
    {
        int baseIndex = vertices.Count;
        Vector3 tipPos = axis * armLength;
        Vector3 tipNeg = -axis * armLength;

        // Rombo central: 4 vértices en +waistA, +waistB, -waistA, -waistB.
        vertices.Add(waistA * waist);
        vertices.Add(waistB * waist);
        vertices.Add(-waistA * waist);
        vertices.Add(-waistB * waist);
        // Las dos puntas.
        vertices.Add(tipPos);
        vertices.Add(tipNeg);

        int r0 = baseIndex, r1 = baseIndex + 1, r2 = baseIndex + 2, r3 = baseIndex + 3;
        int tipPosIndex = baseIndex + 4, tipNegIndex = baseIndex + 5;

        // 4 caras hacia la punta positiva.
        triangles.Add(r0); triangles.Add(r1); triangles.Add(tipPosIndex);
        triangles.Add(r1); triangles.Add(r2); triangles.Add(tipPosIndex);
        triangles.Add(r2); triangles.Add(r3); triangles.Add(tipPosIndex);
        triangles.Add(r3); triangles.Add(r0); triangles.Add(tipPosIndex);

        // 4 caras hacia la punta negativa (orden invertido para que la normal mire hacia fuera).
        triangles.Add(r1); triangles.Add(r0); triangles.Add(tipNegIndex);
        triangles.Add(r2); triangles.Add(r1); triangles.Add(tipNegIndex);
        triangles.Add(r3); triangles.Add(r2); triangles.Add(tipNegIndex);
        triangles.Add(r0); triangles.Add(r3); triangles.Add(tipNegIndex);
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
