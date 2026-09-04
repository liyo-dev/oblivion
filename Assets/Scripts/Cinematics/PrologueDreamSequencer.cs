using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Sendero.Core.Feedback;

/// <summary>
/// Orquestador del prólogo — el sueño de Will. Sustituye al `DramaticText_Prolog` (solo texto)
/// por una escena real: Mago Oscuro a la izquierda, Will Original a la derecha, con blur intenso
/// y el choque final de sus hechizos (ver GDD, "La verdadera historia de Will"). Sin diálogo
/// ni texto — todo el peso es visual y sonoro.
///
/// Estructura en 6 planos, cada uno separado por un corte a negro (ver Co_Sequence):
/// A) Primer plano estático de la cara del Mago Oscuro, borroso y tembloroso — asusta.
/// B) Mago Oscuro a la izquierda del encuadre, slow-mo, conjurando el hechizo en sus manos.
/// C) Media cara de Will a la derecha del encuadre, grande, borrosa — plano estático.
/// D) Will a lo lejos y centrado, slow-mo, solo preparando el hechizo (no llega a lanzarlo).
/// E) Enfrentados de perfil, muy cerca, borroso; el zoom se aleja y los dos preparan y lanzan a
///    la vez, uno hacia el otro — colisión/explosión central.
/// F) Corte a negro y despertar.
///
/// A propósito, NO transcurre en ningún lugar del mundo: es un sueño, así que todo el escenario
/// (cámara, actores, luces, Volume de post-proceso) se construye por código en Awake(), en un
/// punto alejado de cualquier geometría real. No hay nada que colocar a mano en el Editor más
/// allá de arrastrar los dos prefabs y (opcional) los VFX/clips de audio.
///
/// El fondo es el mismo "modo sueño" (nebulosa + chispas) que ya usan `DramaticTextOverlayUI` y
/// `CreditsSceneController` en otras pantallas: `DreamBackgroundController` + `DreamSparkleOverlay`
/// como hijos de este mismo GameObject (mismo patrón que `CreditsSceneController._dreamBackground`/
/// `_dreamSparkles`), sobre un Canvas propio en Screen Space Overlay creado por código en Awake —
/// no reutiliza el overlay de `DramaticTextOverlayUI`, que vive en otra escena/objeto.
///
/// Señal de entrada / salida: los campos `_signalIn`/`_signalOut` heredados de
/// `CinematicSequencerBase`. Punto de integración: sustituye al `DramaticTextNode` que hoy dispara
/// `DramaticText_Prolog` en `MainNarrative.asset` por un `RaiseCustomEventNode(_signalIn)`, seguido
/// de un `WaitCustomEventNode(_signalOut)` antes de continuar hacia "La Casa de Will".
/// </summary>
[DisallowMultipleComponent]
public class PrologueDreamSequencer : CinematicSequencerBase
{
    // ── Actores — solo el prefab, nada que colocar en la escena ────────────────

    [Header("Actores (arrastra el prefab, no una instancia de escena)")]
    [SerializeField] private GameObject magoOscuroPrefab;
    [SerializeField] private string     magoOscuroAnimState = "Cast";
    [Tooltip("Las animaciones de conjurar/gesto de los NPC viven en la capa UpperBody (no la base), igual que MagicRight en StarAwakeningSequencer. Ajusta el índice si tu controller usa otro layer.")]
    [SerializeField] private int        magoOscuroAnimLayer = 1;
    [SerializeField] private float      magoOscuroAnimSpeed = 0.6f;

    [SerializeField] private GameObject willOriginalPrefab;
    [SerializeField] private string     willOriginalAnimState = "Guard";
    [SerializeField] private int        willOriginalAnimLayer = 1;

    // ── Fase A — Primer plano estático de la cara del Mago Oscuro (nuevo) ───────

    [Header("Fase A — Cara del Mago Oscuro, fija y borrosa (asusta)")]
    [Tooltip("Cabeza y hombros, no un macro de la cara — deja aire alrededor.")]
    [SerializeField] private float villainFaceCloseFov      = 30f;
    [SerializeField] private float villainFaceCloseDistance = 1.8f;
    [SerializeField] private float villainFaceDuration      = 1.8f;
    [Tooltip("Peso del Volume de blur/aberración cromática/viñeta durante este plano — 1 = máximo.")]
    [SerializeField] private float villainFaceBlurWeight    = 1f;
    [Tooltip("Camera shake durante todo el plano — 'que se vea menos', más incómodo de mirar.")]
    [SerializeField] private float villainFaceShakeIntensity = 0.15f;
    [Tooltip("Atenúa el rim light del Mago Oscuro durante este plano (1 = sin cambio, 0 = apagado) — que cueste verlo con claridad, no un retrato bien iluminado.")]
    [SerializeField] private float villainFaceRimDimFactor   = 0.45f;

    // ── Fase B — El Mago Oscuro a la izquierda, cargando el hechizo ─────────────

    [Header("Fase B — Mago Oscuro a la izquierda, slow-mo, conjurando")]
    [Tooltip("Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect2H01 - Load.fbx (el sub-asset AnimationClip dentro del fbx). Se reproduce vía Playables, fuera del Animator Controller — no hace falta añadir ningún estado nuevo al controller. También se reutiliza como gesto de preparación en la Fase E.")]
    [SerializeField] private AnimationClip magoOscuroLoadClip;
    [Tooltip("Assets/Plugins/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Spellcasting Animations/Prefabs/Spells/Human_SpellAura_Lightning.prefab")]
    [SerializeField] private GameObject    magoOscuroLoadVfx;
    [Tooltip("Offset en espacio de MUNDO respecto al hueso de la mano derecha (X = izq/dcha, Y = arriba/abajo, Z = adelante/atrás) — ajusta aquí dónde aparece el hechizo cargando, en Play y viendo el resultado en la Game view. Se sigue cada frame, así que tampoco importa cómo gire la mano durante el gesto.")]
    [SerializeField] private Vector3 magoOscuroLoadVfxOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float openingSlowMotionScale = 0.3f;
    [Tooltip("Duración del plano en segundos de JUEGO (se ve más larga en tiempo real por el slow-mo).")]
    [SerializeField] private float openingDuration = 3.5f;
    [Tooltip("Retardo (tiempo de juego) antes de que aparezca el hechizo cargando en sus manos — ajusta para que coincida con el momento en que levanta los brazos en el clip.")]
    [SerializeField] private float openingVfxDelay = 0.6f;
    [Tooltip("FOV/distancia de arranque del plano — deja sitio suficiente para ver las manos y el gesto de conjurar, no solo la cara.")]
    [SerializeField] private float openingCloseFov      = 34f;
    [SerializeField] private float openingCloseDistance = 2.9f;
    [Tooltip("Cuánto se desplaza el Mago Oscuro hacia la derecha de su posición final antes de empezar a moverse hacia la izquierda.")]
    [SerializeField] private float openingStartOffsetX  = 1.1f;
    [Tooltip("Desplaza el punto de mira a la DERECHA del Mago Oscuro para mantenerlo compuesto en la mitad IZQUIERDA de la pantalla durante todo el plano.")]
    [SerializeField] private float openingHorizontalOffset = 0.55f;
    [Tooltip("Peso del Volume de blur/post-pro durante este plano.")]
    [SerializeField] private float openingBlurWeight = 0.7f;
    [Tooltip("Instante (tiempo real) que se mantiene el hechizo ya cargado en pantalla antes de empezar el fundido a negro — asegura que se llegue a ver terminado, no que desaparezca justo antes del corte.")]
    [SerializeField] private float openingSpellHoldDuration = 0.4f;

    // ── Fase C — Media cara de Will, grande, a la derecha ────────────────────────

    [Header("Fase C — Media cara de Will a la derecha, grande y borrosa")]
    [Tooltip("Cabeza y hombros, no un macro de la cara — deja aire alrededor.")]
    [SerializeField] private float willCloseFov               = 28f;
    [SerializeField] private float willCloseDistance          = 1.6f;
    [Tooltip("Desplaza el punto de mira a la IZQUIERDA de la cabeza de Will para que la cara quede compuesta en la mitad DERECHA de la pantalla. Valor pequeño: la cabeza ocupa poco espacio de mundo, un offset grande la saca del encuadre por completo.")]
    [SerializeField] private float willCloseHorizontalOffset  = 0.32f;
    [SerializeField] private float willFaceDuration           = 1.8f;
    [Tooltip("Peso del Volume de blur/post-pro durante este plano.")]
    [SerializeField] private float willFaceBlurWeight = 1f;
    [Tooltip("Expresión facial para este plano (vía NPCEmotionController) — evita que se le vea sonriendo en una escena de tensión.")]
    [SerializeField] private NPCEmotion willFaceExpression = NPCEmotion.Scared;

    // ── Fase D — Will a lo lejos, solo preparación (no llega a lanzar) ──────────

    [Header("Fase D — Will a lo lejos y centrado, slow-mo: solo preparación")]
    [Tooltip("Gesto de preparación/carga — mismo patrón que magoOscuroLoadClip. Si se deja vacío, se usa el gesto normal de willOriginalAnimState/willOriginalAnimLayer en su lugar.")]
    [SerializeField] private AnimationClip willLoadClip;
    [Tooltip("El hechizo cargando en la mano de Will — mismo patrón que magoOscuroLoadVfx en la Fase B (por defecto reutiliza el mismo asset que lightImpactVfx, la 'energía clara' ya asociada a Will).")]
    [SerializeField] private GameObject willLoadVfx;
    [Tooltip("Offset en espacio de MUNDO respecto al hueso de la mano derecha — igual que magoOscuroLoadVfxOffset en la Fase B. Ajusta en Play viendo el resultado en la Game view.")]
    [SerializeField] private Vector3 willLoadVfxOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("DURACIÓN TOTAL del plano (tiempo de juego, bajo el slow-mo): desde que empieza el gesto de preparación hasta que corta a negro. El lanzamiento real ya no ocurre en esta fase — pasa en la Fase E, donde Will y el Mago Oscuro disparan a la vez.")]
    [SerializeField] private float willPreparationHoldDuration = 1.0f;
    [Tooltip("En qué momento, DENTRO de la duración de arriba, aparece el hechizo cargando en su mano (ej. 0.6 = a los 0.6s de que empiece el plano). Tiene que ser menor que 'Will Preparation Hold Duration', si no el hechizo no llega a aparecer.")]
    [SerializeField] private float willVfxDelay = 0.6f;
    [Tooltip("FOV/distancia amplios a propósito — tiene que verse el gesto completo, no solo el torso.")]
    [SerializeField] private float willDistantFov      = 40f;
    [SerializeField] private float willDistantDistance = 4.5f;
    [Tooltip("El punto de mira apunta un poco por DEBAJO de la cabeza real (hueso) — deja a Will compuesto algo más arriba del encuadre en vez de justo en el centro.")]
    [SerializeField] private float willDistantLookOffset = 0.25f;
    [SerializeField] private float willSlowMotionScale = 0.35f;

    // ── Escenario procedural ─────────────────────────────────────────────────

    [Header("Escenario — se construye entero por código")]
    [Tooltip("Punto en el espacio, lejos de cualquier escena real, donde se monta el sueño. No debería coincidir nunca con geometría del mundo.")]
    [SerializeField] private Vector3 stageAnchorPosition = new Vector3(0f, 4000f, 0f);
    [SerializeField] private float   actorHorizontalOffset = 1.4f;
    [SerializeField] private float   cameraDistance = 4.2f;
    [SerializeField] private float   cameraHeight   = 1.15f;
    [SerializeField] private float   cameraFov      = 34f;
    [Tooltip("Mismo azul índigo que usa DreamBackgroundController, bastante más oscuro que antes — color base mientras no hay nebulosa/chispas cubriendo el encuadre.")]
    [SerializeField] private Color   voidBackgroundColor = new Color(0.012f, 0.02f, 0.06f, 1f);

    [Header("Fondo de sueño (mismo patrón que CreditsSceneController._dreamBackground/_dreamSparkles)")]
    [Tooltip("Nebulosa procedural. Instancia ya colocada como hija de este GameObject (RectTransform, ver DreamNebula en la escena) — no se reinventa por código. Si se deja vacía, la Fase de sueño simplemente no tiene nebulosa.")]
    [SerializeField] private DreamBackgroundController _dreamBackground;
    [Tooltip("Chispas/estrellas procedurales. Mismo criterio que _dreamBackground (hija ya colocada, ver DreamSparkles en la escena).")]
    [SerializeField] private DreamSparkleOverlay _dreamSparkles;

    [Header("Iluminación (rim light por actor + relleno suave) — tono oscuro/tenue a propósito")]
    [SerializeField] private Color magoRimColor   = new Color(0.35f, 0.6f, 1f);
    [SerializeField] private Color willRimColor   = new Color(1f, 0.85f, 0.55f);
    [SerializeField] private float rimIntensity   = 2f;
    [SerializeField] private float fillIntensity  = 0.2f;

    [Header("Blur intenso (Fases A y C — primeros planos que 'apenas se ven')")]
    [Tooltip("Receta de DepthOfField más agresiva que la base (usada en la Fase E) — arranca el blur mucho más cerca de cámara.")]
    [SerializeField] private float intenseBlurGaussianStart = 0.3f;
    [SerializeField] private float intenseBlurGaussianEnd   = 2.2f;
    [SerializeField] private float intenseBlurMaxRadius     = 2.2f;

    // ── Fase E — Enfrentamiento de perfil + colisión ──────────────────────────

    [Header("Fase E — Enfrentados de perfil, muy cerca, luego el zoom se aleja")]
    [Tooltip("Plano muy cerrado, de perfil, ocupando casi toda la pantalla — arranca aquí antes de alejar el zoom.")]
    [SerializeField] private float confrontationCloseFov        = 30f;
    [SerializeField] private float confrontationCloseDistance   = 3.6f;
    [SerializeField] private float confrontationZoomOutDuration = 1.2f;
    [SerializeField] private float presentationDuration = 3.7f;
    [Tooltip("Intervalo entre pulsos de blur.")]
    [SerializeField] private float flickerOnMin   = 0.10f;
    [SerializeField] private float flickerOnMax   = 0.30f;
    [SerializeField] private float shockWeightMin = 0.35f;
    [SerializeField] private float shockWeightMax = 0.85f;

    [Header("Fase E — Flashes de guerra (opcional; si se deja vacío, usa como fallback lightImpactVfx/darkImpactVfx)")]
    [Tooltip("Si se asigna a mano, estos GameObjects ya colocados en la escena se activan uno a uno (comportamiento original). Si se deja vacío (caso por defecto hoy), Co_WarFlashes() cae en un fallback: reutiliza lightImpactVfx/darkImpactVfx (los mismos VFX que ya dispara Co_Collision) como 2-3 destellos de choque extra, vía VfxPoolService — sin necesidad de colocar nada a mano en el Editor. Decisión 4 sep 2026.")]
    [SerializeField] private GameObject[] warFlashVisuals;
    [SerializeField] private float flashOnDuration      = 0.15f;
    [SerializeField] private float backToActorsDuration = 0.10f;
    [SerializeField] private float flashShakeIntensity  = 0.12f;

    [Header("Fase E — Preparación mutua, luego lanzamiento uno hacia el otro")]
    [Tooltip("Cuánto se mantiene el gesto de preparación EN BUCLE (tiempo real) — magoOscuroLoadClip/willLoadClip se reinician automáticamente cada vez que terminan, hasta cumplir esta duración. Durante este tiempo solo se ve la POSE: el VFX del hechizo no se instancia hasta soltarlo al terminar (SpawnAndLaunchMutualVfx), a la vez para los dos.")]
    [SerializeField] private float mutualPreparationHoldDuration = 0.5f;
    [Tooltip("Animación de SOLTAR el hechizo del Mago Oscuro, al terminar la preparación — sustituye al gesto de magoOscuroLoadClip en bucle. Si se deja vacío, se usa el gesto normal de magoOscuroAnimState/magoOscuroAnimLayer.")]
    [SerializeField] private AnimationClip magoOscuroReleaseClip;
    [Tooltip("Animación de SOLTAR el hechizo de Will, al terminar la preparación — sustituye al gesto de willLoadClip en bucle. Si se deja vacío, se usa el gesto normal de willOriginalAnimState/willOriginalAnimLayer.")]
    [SerializeField] private AnimationClip willReleaseClip;
    [Tooltip("Cuánto tarda cada hechizo en viajar desde la mano del actor hasta el punto de choque (centro), en tiempo real.")]
    [SerializeField] private float mutualCastTravelDuration    = 1.1f;
    [Tooltip("Nº de pasos de camera shake mientras los dos hechizos se acercan el uno al otro — la intensidad crece en cada paso.")]
    [SerializeField] private int   mutualCastShakeSteps        = 5;
    [SerializeField] private float mutualCastShakeMaxIntensity = 0.3f;

    [Header("Fase E — Colisión de hechizos (origen y punto de choque calculados por código)")]
    [Tooltip("VFX de energía clara, ej. Light Orb — sale desde Will Original.")]
    [SerializeField] private GameObject lightImpactVfx;
    [Tooltip("VFX de energía oscura, ej. Plasma Sphere Cinematic — sale desde el Mago Oscuro.")]
    [SerializeField] private GameObject darkImpactVfx;
    [SerializeField] private Color lightFlashColor = new Color(1f, 0.95f, 0.75f, 1f);
    [SerializeField] private Color darkFlashColor  = new Color(0.25f, 0f, 0.4f, 1f);
    [SerializeField] private float collisionHoldDuration = 1.2f;
    [SerializeField] private float collisionZoomFovFactor = 0.6f;
    [SerializeField] private float collisionZoomDuration  = 0.4f;

    // ── Audio (opcional) ─────────────────────────────────────────────────────

    // Todo el audio de esta cinemática pasa por AudioGraphProfile/AudioService.Instance.PlaySFX —
    // los campos son claves de evento (string), no AudioClip directos. Ver eventSfx en
    // Assets/_AUDIOPROFILE/AudioGraphProfile.asset (prefijo "Prologue_").
    [Header("Audio (claves del Audio Graph Profile — dejar vacío para omitir)")]
    [SerializeField] private string      tinnitusSfxKey = "";
    [SerializeField] private float       tinnitusVolume = 0.6f;
    [Tooltip("Bucle de latido de corazón durante toda la secuencia. NOTA: 'Prologue_Heartbeat' está registrada en el AudioGraphProfile pero sin clip asignado todavía — no hay ningún SFX de latido en la librería de audio del proyecto. Asignar uno en el AudioGraphProfile en cuanto se consiga.")]
    [SerializeField] private string      heartbeatSfxKey = "Prologue_Heartbeat";
    [SerializeField] private string[]    warClashStingerKeys = { "Prologue_WarClashStinger_A", "Prologue_WarClashStinger_B" };
    [Tooltip("One-shot cada vez que un actor se revela en pantalla (SetMagoVisible/SetWillVisible a true) — Fases A, B, C, D y E. No espacializado, es un sueño.")]
    [SerializeField] private string      actorAppearSfxKey = "Prologue_ActorAppear";
    [Tooltip("One-shot en el instante exacto en que se instancia el VFX del hechizo cargando en la mano (Co_SpawnChargingVfxDelayed / Co_SpawnWillChargingVfxDelayed).")]
    [SerializeField] private string      spellInstantiateSfxKey = "Prologue_SpellInstantiate";
    [Tooltip("Bucle que suena mientras el hechizo está cargando en la mano — arranca junto con spellInstantiateSfxKey y se corta al soltar el hechizo (Fase E) o al cortar el plano (Fases B/D). Usa AudioService.PlayLoopingSFX con loopId propio, compartido por los dos actores.")]
    [SerializeField] private string      spellChargeLoopSfxKey = "Prologue_SpellChargeLoop";
    [SerializeField] private float       spellChargeLoopVolume = 0.5f;
    [Tooltip("One-shot al soltar el hechizo a la vez, en Co_MutualCastAndBlackout — justo cuando se corta el bucle de carga.")]
    [SerializeField] private string      spellReleaseSfxKey = "Prologue_SpellRelease";
    [Tooltip("One-shot en el pico de la colisión (Co_Collision), a la vez que el pico del tinnitus.")]
    [SerializeField] private string      explosionSfxKey = "Prologue_Explosion";

    // ── Fase F — Corte y despertar ────────────────────────────────────────────

    [Header("Fase F — Corte y despertar")]
    [SerializeField] private float fadeToBlackDuration = 0.15f;
    [SerializeField] private float silenceDuration      = 0.3f;

    // ── Estado runtime (construido en Awake, nada de esto se configura a mano) ─

    private Transform _stageRoot;
    private GameObject _magoInstance;
    private GameObject _willInstance;
    private Animator  _magoAnimator;
    private Animator  _willAnimator;
    private Renderer[] _magoRenderers;
    private Renderer[] _willRenderers;
    private NPCEmotionController _willEmotion;
    private Camera _stageCamera;
    private Camera _worldMainCamera;
    private Volume _shockVolume;
    private VolumeProfile _runtimeProfile;
    private DepthOfField _shockDof;
    private Light _magoRimLight;
    private Light _willRimLight;
    // loopId propios para AudioService.PlayLoopingSFX/StopLoopingSFX (no hace falta AudioSource
    // propia: el AudioService gestiona la fuente dedicada a cada loopId internamente).
    private const string HeartbeatLoopId = "PrologueHeartbeat";
    private const string SpellChargeLoopId = "PrologueSpellCharge";
    private PlayableGraph _magoLoadGraph;
    private PlayableGraph _willCastGraph;
    private GameObject _magoChargingVfxInstance;
    private GameObject _willChargingVfxInstance;
    private Coroutine _magoVfxTrackRoutine;
    private Coroutine _willVfxTrackRoutine;
    private Coroutine _magoPrepLoopRoutine;
    private Coroutine _willPrepLoopRoutine;

    protected override void Awake()
    {
        base.Awake();
        EnsureDreamCanvas();
        BuildStage();
    }

    /// `_dreamBackground`/`_dreamSparkles` son RectTransforms colocados como hijos de este mismo
    /// GameObject (ver DreamNebula/DreamSparkles en la escena) — para que se vean necesitan un
    /// Canvas propio en algún antepasado, y este objeto no cuelga de ninguno (vive junto a los
    /// demás sequencers bajo "DIRECTOR", que no es UI). Mismo Canvas en Screen Space Overlay que
    /// construye CreditsSceneController.BuildUI() por código, así que se dibuja encima de la
    /// cámara del escenario (_stageCamera) sin depender de DramaticTextOverlayUI.
    private void EnsureDreamCanvas()
    {
        if (GetComponent<Canvas>() != null) return;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Cleanup();
    }

    /// Ver CinematicSequencerBase.OnSkipCleanup(). Cleanup() (más abajo) ya es una limpieza de
    /// emergencia completa y probada: resetea Time.timeScale, para el loop del latido, destruye
    /// los PlayableGraph y VFX de conjuración, REACTIVA Camera.main del mundo real (esta secuencia
    /// la desactiva al entrar y es la única forma de que vuelva a encenderse si no se llega a
    /// Co_Awaken) y destruye por completo el escenario procedural (_stageRoot: actores, cámara
    /// propia, luces, Volume de post-proceso). Cleanup() no para la MÚSICA del sueño en sí (solo el
    /// loop de latido) — se añade aquí en seco, igual que hace Co_Awaken en el cierre normal.
    protected override void OnSkipCleanup()
    {
        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(0.05f);
        Cleanup();
    }

    // El cierre normal (Co_Awaken) nunca restaura música de escena, solo la para en seco
    // (silencio total = "vende el golpe del despertar") — el skip respeta el mismo criterio.
    protected override bool SkipRestoresMusic => false;

    // ══════════════════════════════════════════════════════════════════════════
    // Construcción del escenario (todo por código, una sola vez en Awake)
    // ══════════════════════════════════════════════════════════════════════════

    private void BuildStage()
    {
        var rootGO = new GameObject("PrologueDreamStage") { hideFlags = HideFlags.DontSave };
        // Clave: se instancia todo con el root DESACTIVADO. Awake() de los hijos se sigue
        // llamando igual, pero OnEnable() se difiere hasta que activemos el root más abajo — así
        // podemos deshabilitar el NavMeshAgent ANTES de que intente engancharse a un NavMesh que,
        // a 4000 unidades de cualquier escena real, nunca va a encontrar ("Failed to create agent
        // because it is not close enough to the NavMesh").
        rootGO.SetActive(false);
        rootGO.transform.position = stageAnchorPosition;
        _stageRoot = rootGO.transform;

        Vector3 leftPos  = stageAnchorPosition + Vector3.left  * actorHorizontalOffset;
        Vector3 rightPos = stageAnchorPosition + Vector3.right * actorHorizontalOffset;
        Vector3 camPos   = stageAnchorPosition + Vector3.back * cameraDistance + Vector3.up * cameraHeight;

        if (magoOscuroPrefab != null)
        {
            _magoInstance = Instantiate(magoOscuroPrefab, leftPos, Quaternion.identity, _stageRoot);
            PrepareActorInstance(_magoInstance);
            // includeInactive: true en ambas — el root (y por tanto esta instancia) sigue
            // desactivado en este punto, ver comentario sobre NavMeshAgent más arriba.
            _magoAnimator  = _magoInstance.GetComponentInChildren<Animator>(true);
            _magoRenderers = _magoInstance.GetComponentsInChildren<Renderer>(true);
            // Toda la posición/rotación de esta cinemática la controla el código (FaceCameraFlat,
            // FaceEachOther, Co_MoveActor...). Con Root Motion activo, el propio clip puede
            // reorientar el transform por encima de lo que fijamos nosotros — de ahí el bug
            // reportado de que en la Fase E siguen mirando a cámara en vez de mirarse entre ellos.
            if (_magoAnimator != null) _magoAnimator.applyRootMotion = false;
        }
        else
        {
            Debug.LogError("[PrologueDreamSequencer] Falta asignar magoOscuroPrefab.", this);
        }

        if (willOriginalPrefab != null)
        {
            _willInstance = Instantiate(willOriginalPrefab, rightPos, Quaternion.identity, _stageRoot);
            PrepareActorInstance(_willInstance);
            _willAnimator  = _willInstance.GetComponentInChildren<Animator>(true);
            _willRenderers = _willInstance.GetComponentsInChildren<Renderer>(true);
            _willEmotion   = _willInstance.GetComponentInChildren<NPCEmotionController>(true);
            if (_willAnimator != null) _willAnimator.applyRootMotion = false;
        }
        else
        {
            Debug.LogError("[PrologueDreamSequencer] Falta asignar willOriginalPrefab.", this);
        }

        FaceCameraFlat(_magoInstance, camPos);
        FaceCameraFlat(_willInstance, camPos);

        BuildCamera(camPos);
        BuildLighting(leftPos, rightPos, camPos);
        BuildShockVolume();

        // Ahora sí: activamos el root. Los NavMeshAgent ya están deshabilitados (PrepareActorInstance
        // se llamó mientras todo estaba inactivo), así que su OnEnable nunca llega a dispararse.
        _stageRoot.gameObject.SetActive(true);

        SetActorsVisible(false);
    }

    /// Desactiva NavMeshAgent (no hay NavMesh en el punto donde vive el sueño) para que no
    /// escupa warnings; el Animator sigue funcionando con normalidad.
    private static void PrepareActorInstance(GameObject actor)
    {
        // includeInactive: true — se llama mientras el stage root (y por tanto 'actor') sigue
        // desactivado a propósito, ver comentario sobre NavMeshAgent en BuildStage().
        var agent = actor.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        if (agent != null) agent.enabled = false;

        // La FSM del NPC (Game.NPC.NPCBehaviourManagerV2 → NPCBrain → INPCState) sigue activa por
        // defecto en el prefab completo — algún estado (idle/social/diálogo) reorienta al personaje
        // en su propio Update() y pisa cualquier rotación que fijemos por código, frame tras frame.
        // De ahí el bug reportado: por más que FaceEachOther() fije la rotación, vuelven a mirar a
        // cámara al frame siguiente. En este escenario falso, puramente procedural, no debe correr
        // ninguna IA — igual que el NavMeshAgent, se desactiva entero.
        var behaviourManager = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (behaviourManager != null) behaviourManager.enabled = false;

        // CAUSA REAL de "siguen mirando a cámara en la Fase E": NPCSimpleAnimator (el único
        // responsable de animación del NPC, ver su propio comentario de cabecera) tiene su PROPIO
        // LateUpdate() con rotación suave hacia un `_targetRotation` interno
        // (`ApplySmoothRotation()`), completamente independiente de NPCBehaviourManagerV2. Ese
        // `_targetRotation` se congela en Awake() con la rotación de spawn (Quaternion.identity,
        // porque Instantiate() se llama con esa rotación) — ANTES de que FaceCameraFlat()/
        // FaceEachOther() toquen el transform. Resultado: cada frame, NPCSimpleAnimator tira la
        // rotación de vuelta hacia esa `Quaternion.identity` COMPARTIDA por los dos actores (mismo
        // target para Mago y Will), peleando contra las asignaciones de este sequencer — de ahí que
        // ambos acaben con la misma orientación de cara a cámara en vez de mirarse de perfil.
        // Deshabilitar NavMeshAgent/NPCBehaviourManagerV2 no basta: hace falta este componente
        // aparte. NPCSimpleAnimator ya expone DisableAutoRotation() para exactamente este caso
        // ("útil durante diálogos cuando otro sistema controla la rotación").
        var simpleAnimator = actor.GetComponentInChildren<NPCSimpleAnimator>(true);
        if (simpleAnimator != null) simpleAnimator.DisableAutoRotation();
    }

    private static void FaceCameraFlat(GameObject actor, Vector3 camPos)
    {
        if (actor == null) return;
        Vector3 dir = camPos - actor.transform.position;
        dir.y = 0f;
        // El forward del actor (+Z local) debe apuntar HACIA la cámara (dir, sin negar). Con el
        // signo invertido el personaje queda mirando en la misma dirección que ve la cámara, es
        // decir, de espaldas.
        if (dir.sqrMagnitude > 0.0001f)
            actor.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void BuildCamera(Vector3 camPos)
    {
        var camGO = new GameObject("PrologueDreamCamera") { hideFlags = HideFlags.DontSave };
        camGO.transform.SetParent(_stageRoot);
        camGO.transform.position = camPos;
        camGO.transform.LookAt(stageAnchorPosition + Vector3.up * cameraHeight);

        _stageCamera = camGO.AddComponent<Camera>();
        _stageCamera.fieldOfView    = cameraFov;
        _stageCamera.clearFlags     = CameraClearFlags.SolidColor;
        _stageCamera.backgroundColor = voidBackgroundColor;
        _stageCamera.gameObject.SetActive(false);

        var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        // Al desactivar la cámara del mundo (que lleva el AudioListener) durante la cinemática,
        // hace falta uno propio o Unity se queda sin listener activo ("There are no audio
        // listeners in the scene") y todo el audio de la secuencia (heartbeat, tinnitus, stingers)
        // se queda mudo. Como ambas cámaras alternan su SetActive en espejo, nunca hay dos a la vez.
        camGO.AddComponent<AudioListener>();
    }

    private void BuildLighting(Vector3 leftPos, Vector3 rightPos, Vector3 camPos)
    {
        var fillGO = new GameObject("Fill") { hideFlags = HideFlags.DontSave };
        fillGO.transform.SetParent(_stageRoot);
        fillGO.transform.position = camPos;
        fillGO.transform.LookAt(stageAnchorPosition);
        var fill = fillGO.AddComponent<Light>();
        fill.type      = LightType.Directional;
        fill.intensity = fillIntensity;
        fill.color     = Color.white;
        fill.shadows   = LightShadows.None;

        // Guardamos la referencia de cada rim para poder apagar/encender solo la del actor que
        // corresponda en cada fase — antes quedaban las DOS siempre encendidas, así que el actor
        // oculto en cada plano seguía iluminando el vacío del lado que le tocaba (bug reportado:
        // "vemos el lado derecho iluminado y debería ser todo más oscuro").
        _magoRimLight = CreateRim("Rim_MagoOscuro", leftPos  + Vector3.up * 1.6f + Vector3.back * 0.5f + Vector3.left  * 0.8f, leftPos,  magoRimColor);
        _willRimLight = CreateRim("Rim_WillOriginal", rightPos + Vector3.up * 1.6f + Vector3.back * 0.5f + Vector3.right * 0.8f, rightPos, willRimColor);
    }

    private Light CreateRim(string name, Vector3 pos, Vector3 lookAt, Color color)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(_stageRoot);
        go.transform.position = pos;
        go.transform.LookAt(lookAt);

        var light = go.AddComponent<Light>();
        light.type      = LightType.Spot;
        light.color     = color;
        light.intensity = rimIntensity;
        light.range     = 8f;
        light.spotAngle = 60f;
        light.shadows   = LightShadows.None;
        return light;
    }

    /// Construye el Volume de blur (DepthOfField + ChromaticAberration + Vignette al máximo,
    /// misma receta que usa ShockEffectsController en el resto del juego) enteramente en código:
    /// no hace falta ningún .asset ni arrastrar nada en el Inspector. Se reutiliza en las Fases
    /// A, B, C (blur "de miedo"/post-pro) y E (blur de la presentación dual + colisión). El
    /// DepthOfField se guarda aparte (_shockDof) para poder endurecer la receta en A/C — ver
    /// SetBlurStrength().
    private void BuildShockVolume()
    {
        _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();

        var dof = _runtimeProfile.Add<DepthOfField>(true);
        dof.mode.overrideState             = true; dof.mode.value             = DepthOfFieldMode.Gaussian;
        dof.gaussianStart.overrideState    = true; dof.gaussianStart.value    = 1.5f;
        dof.gaussianEnd.overrideState      = true; dof.gaussianEnd.value      = 6f;
        dof.gaussianMaxRadius.overrideState = true; dof.gaussianMaxRadius.value = 1.5f;
        _shockDof = dof;

        var ca = _runtimeProfile.Add<ChromaticAberration>(true);
        ca.intensity.overrideState = true; ca.intensity.value = 1f;

        var vig = _runtimeProfile.Add<Vignette>(true);
        vig.intensity.overrideState  = true; vig.intensity.value  = 0.55f;
        vig.smoothness.overrideState = true; vig.smoothness.value = 0.3f;

        var volGO = new GameObject("Volume_PrologueShock") { hideFlags = HideFlags.DontSave };
        volGO.transform.SetParent(_stageRoot);
        _shockVolume = volGO.AddComponent<Volume>();
        _shockVolume.isGlobal      = true;
        _shockVolume.weight        = 0f;
        _shockVolume.priority      = 100f;
        _shockVolume.sharedProfile = _runtimeProfile;
    }

    /// Endurece (o devuelve a la receta base) el DepthOfField del Volume compartido. Las Fases A
    /// y C piden explícitamente "que se vea menos, más difuminado" — más que el blur base que usa
    /// la Fase E para el pulso de tensión.
    private void SetBlurStrength(bool intense)
    {
        if (_shockDof == null) return;
        if (intense)
        {
            _shockDof.gaussianStart.value    = intenseBlurGaussianStart;
            _shockDof.gaussianEnd.value      = intenseBlurGaussianEnd;
            _shockDof.gaussianMaxRadius.value = intenseBlurMaxRadius;
        }
        else
        {
            _shockDof.gaussianStart.value    = 1.5f;
            _shockDof.gaussianEnd.value      = 6f;
            _shockDof.gaussianMaxRadius.value = 1.5f;
        }
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    protected override IEnumerator Co_Sequence()
    {
        // Fundido propio garantizado: no dependemos de que _entryTransition esté configurado
        // en el Inspector (puede no estarlo, ya que esta secuencia no requiere ningún ajuste).
        if (!FeedbackService.IsScreenFaded)
            yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        yield return Co_BeginCinematicWithTransition(() =>
        {
            _worldMainCamera = Camera.main;
            if (_worldMainCamera != null) _worldMainCamera.gameObject.SetActive(false);
            _stageCamera.gameObject.SetActive(true);
            // Todos ocultos: cada plano revela solo al actor que le toca.
            SetActorsVisible(false);
        });

        PlaySequenceMusic();
        _dreamBackground?.StartDream();
        _dreamSparkles?.StartSparkles();

        if (AudioService.Instance != null && !string.IsNullOrWhiteSpace(heartbeatSfxKey))
            AudioService.Instance.PlayLoopingSFX(HeartbeatLoopId, heartbeatSfxKey);

        PlayTinnitus();

        // ── Fase A: primer plano estático de la cara del Mago Oscuro (asusta) ──
        yield return Co_VillainFaceCloseup();

        // ── Fase B: Mago Oscuro a la izquierda, slow-mo, conjurando ────────────
        yield return Co_VillainCastLeft();

        // ── Fase C: media cara de Will a la derecha, grande, borrosa ────────────
        yield return Co_WillFaceCloseup();

        // ── Fase D: Will a lo lejos, centrado, slow-mo: prepara y dispara ──────
        // (termina ya en negro)
        yield return Co_WillCastFromDistance();

        // ── Fase E: enfrentados de perfil + colisión/explosión central ─────────
        yield return Co_DualBlurredCollision();

        // ── Fase F: corte a negro y despertar ───────────────────────────────────
        yield return Co_Awaken();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase A — Primer plano estático de la cara del Mago Oscuro (asusta)
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_VillainFaceCloseup()
    {
        if (_magoInstance == null) yield break;

        Transform head = GetHeadBone(_magoAnimator);
        Vector3 headPos = head != null ? head.position : _magoInstance.transform.position + Vector3.up * 1.6f;

        _stageCamera.transform.position = headPos + Vector3.back * villainFaceCloseDistance;
        _stageCamera.transform.LookAt(headPos);
        _stageCamera.fieldOfView = villainFaceCloseFov;

        // Encara la cámara de ESTE plano, no la del plano dual calculada una vez en BuildStage —
        // a esta distancia cualquier pequeño desvío angular se nota mucho (bug reportado:
        // "mirando a la derecha" en vez de mirar directamente a cámara).
        FaceCameraFlat(_magoInstance, _stageCamera.transform.position);

        SetMagoVisible(true);
        SetWillVisible(false);

        if (_shockVolume != null) _shockVolume.weight = villainFaceBlurWeight;
        SetBlurStrength(true);
        if (_magoRimLight != null) _magoRimLight.intensity = rimIntensity * villainFaceRimDimFactor;

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: false);

        FeedbackService.CameraShake(_stageCamera, villainFaceShakeIntensity, villainFaceDuration);
        yield return new WaitForSecondsRealtime(villainFaceDuration);

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        SetMagoVisible(false);
        if (_shockVolume != null) _shockVolume.weight = 0f;
        SetBlurStrength(false);
        if (_magoRimLight != null) _magoRimLight.intensity = rimIntensity;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase B — Mago Oscuro a la izquierda, slow-mo, conjurando el hechizo
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_VillainCastLeft()
    {
        if (_magoInstance == null || _magoAnimator == null) yield break;

        Vector3 finalPos = _magoInstance.transform.position; // leftPos, fijado en BuildStage
        Vector3 startPos = finalPos + Vector3.right * openingStartOffsetX + Vector3.forward * 0.3f;
        _magoInstance.transform.position = startPos;

        // Altura real de la cabeza (hueso), no un "+up*1.5" fijo que no encaja con este modelo en
        // concreto — de ahí venía el "falta parte del cuerpo": el punto de mira no correspondía a
        // dónde está realmente el personaje (cascos/cuernos altos, proporciones distintas).
        Transform head = GetHeadBone(_magoAnimator);
        float headHeight = head != null ? (head.position.y - startPos.y) : 1.6f;

        // Mantiene al Mago Oscuro compuesto en la mitad izquierda de la pantalla durante todo el
        // plano: el punto de mira se desplaza a la derecha de su posición real.
        Vector3 closeLookAt = startPos + Vector3.up * headHeight + Vector3.right * openingHorizontalOffset;
        Vector3 closeCamPos = startPos + Vector3.up * headHeight + Vector3.back  * openingCloseDistance;
        Vector3 medLookAt   = finalPos + Vector3.up * headHeight + Vector3.right * openingHorizontalOffset;
        Vector3 medCamPos   = finalPos + Vector3.up * headHeight + Vector3.back  * cameraDistance;

        _stageCamera.transform.position = closeCamPos;
        _stageCamera.transform.LookAt(closeLookAt);
        _stageCamera.fieldOfView = openingCloseFov;

        FaceCameraFlat(_magoInstance, closeCamPos);

        SetMagoVisible(true);
        SetWillVisible(false);

        if (_shockVolume != null) _shockVolume.weight = openingBlurWeight;

        Time.timeScale = openingSlowMotionScale;

        PlayRawClip(_magoAnimator, magoOscuroLoadClip, ref _magoLoadGraph);
        if (magoOscuroLoadClip == null)
        {
            // Sin clip asignado: al menos que se vea el gesto normal en vez de quedarse en T-pose/idle puro.
            _magoAnimator.SetLayerWeight(magoOscuroAnimLayer, 1f);
            _magoAnimator.Play(magoOscuroAnimState, magoOscuroAnimLayer, 0f);
        }
        StartCoroutine(Co_SpawnChargingVfxDelayed(SafeVfxDelay(openingVfxDelay, openingDuration)));
        StartCoroutine(Co_MoveActor(_magoInstance.transform, startPos, finalPos, openingDuration));

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: false);
        yield return Co_LerpCamera(closeCamPos, medCamPos, closeLookAt, medLookAt, openingCloseFov, cameraFov, openingDuration);

        // Deja un instante el hechizo ya cargado, bien visible, antes de cortar — si el fundido a
        // negro llega DESPUÉS de destruir el VFX/hacer Rebind() del Animator (como pasaba antes),
        // el corte a negro tapa un plano que ya lleva un frame o dos con la mano vacía y no se
        // llega a ver el hechizo terminado.
        yield return new WaitForSecondsRealtime(openingSpellHoldDuration);

        // Fundido a negro CON el hechizo y la pose todavía en pantalla — el pop de Rebind()/Destroy
        // de abajo pasa ya tapado por el negro, no antes.
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        Time.timeScale = 1f;
        StopRawClip(_magoAnimator, ref _magoLoadGraph);
        StopChargeLoop();
        if (_magoChargingVfxInstance != null) { Destroy(_magoChargingVfxInstance); _magoChargingVfxInstance = null; }
        _magoInstance.transform.position = finalPos;

        SetMagoVisible(false);
        if (_shockVolume != null) _shockVolume.weight = 0f;
    }

    private IEnumerator Co_SpawnChargingVfxDelayed(float delay)
    {
        // Tiempo de JUEGO (no realtime): debe coincidir con el momento del clip afectado por el
        // slow-mo, no con el reloj real.
        yield return new WaitForSeconds(delay);
        if (_magoAnimator == null || magoOscuroLoadVfx == null) yield break;

        Transform hand = GetHandBone(_magoAnimator);
        Vector3 spawnPos = hand != null
            ? hand.position + magoOscuroLoadVfxOffset
            : _magoAnimator.transform.position + Vector3.up * 1.2f + magoOscuroLoadVfxOffset;
        _magoChargingVfxInstance = Instantiate(magoOscuroLoadVfx, spawnPos, Quaternion.identity, _stageRoot);
        PlaySpellInstantiate();
        StartChargeLoop();
        _magoVfxTrackRoutine = StartCoroutine(Co_TrackVfxAboveHand(_magoChargingVfxInstance, _magoAnimator, magoOscuroLoadVfxOffset));
    }

    /// Clampa el retardo pedido para que SIEMPRE quede por debajo de la duración del hold, con un
    /// margen mínimo — si el retardo es igual o mayor que el hold (mala combinación de valores en
    /// el Inspector), el VFX nunca llega a instanciarse porque la fase ya ha cortado a negro y
    /// limpiado todo antes de que le toque aparecer (bug reportado: "no se está instanciando el
    /// hechizo").
    private static float SafeVfxDelay(float requestedDelay, float holdDuration)
    {
        return Mathf.Clamp(requestedDelay, 0f, Mathf.Max(0f, holdDuration - 0.1f));
    }

    /// Sigue al hueso de la mano en espacio de MUNDO cada frame, en vez de dejar el VFX como hijo
    /// del hueso con un offset LOCAL — con eso, el offset se giraba junto con la rotación de la
    /// mano durante el gesto de conjurar y el hechizo terminaba flotando en un punto que no era la
    /// mano (bug reportado). Se corta sola cuando el vfx se destruye (Destroy marca null).
    /// 'offset' es en espacio de MUNDO (X/Y/Z), configurable desde magoOscuroLoadVfxOffset en el
    /// Inspector — ajústalo en Play viendo directamente dónde cae respecto a la mano.
    private static IEnumerator Co_TrackVfxAboveHand(GameObject vfx, Animator animator, Vector3 offset)
    {
        while (vfx != null && animator != null)
        {
            Transform hand = GetHandBone(animator);
            if (hand != null) vfx.transform.position = hand.position + offset;
            yield return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase C — Media cara de Will, grande, a la derecha (plano estático)
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WillFaceCloseup()
    {
        if (_willInstance == null) yield break;

        Transform head = GetHeadBone(_willAnimator);
        Vector3 headPos = head != null ? head.position : _willInstance.transform.position + Vector3.up * 1.6f;

        // Media cara grande en la mitad derecha: el punto de mira se desplaza a la IZQUIERDA de
        // la cabeza para que la cara quede compuesta en el lado DERECHO del encuadre.
        Vector3 closeLookAt = headPos + Vector3.left * willCloseHorizontalOffset;
        Vector3 closeCamPos = headPos + Vector3.back * willCloseDistance;

        _stageCamera.transform.position = closeCamPos;
        _stageCamera.transform.LookAt(closeLookAt);
        _stageCamera.fieldOfView = willCloseFov;

        FaceCameraFlat(_willInstance, closeCamPos);

        SetMagoVisible(false);
        SetWillVisible(true);

        // Expresión seria/asustada para esta escena — por defecto el NPC puede tener una sonrisa
        // de diálogo normal, que desentona en un plano de tensión como este.
        _willEmotion?.SetEmotion(willFaceExpression);

        if (_shockVolume != null) _shockVolume.weight = willFaceBlurWeight;
        SetBlurStrength(true);

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: false);
        yield return new WaitForSecondsRealtime(willFaceDuration);
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        SetWillVisible(false);
        if (_shockVolume != null) _shockVolume.weight = 0f;
        SetBlurStrength(false);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase D — Will a lo lejos, centrado, slow-mo: SOLO preparación
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WillCastFromDistance()
    {
        if (_willInstance == null || _willAnimator == null) yield break;

        Vector3 rightPos = _willInstance.transform.position; // fijado en BuildStage, no se mueve en este plano

        // Hueso real de la cabeza — un "+up*1.5" fijo no correspondía a la altura real de este
        // personaje y el punto de mira quedaba muy por encima de él, empujándolo hacia abajo del
        // encuadre (bug reportado: "Will está abajo, debería estar centrado un poco más arriba").
        Transform head = GetHeadBone(_willAnimator);
        Vector3 headPos = head != null ? head.position : rightPos + Vector3.up * 1.4f;
        // Apunta un poco por DEBAJO de la cabeza real: lo deja compuesto algo más arriba del
        // encuadre en vez de justo en el centro.
        Vector3 lookAtPoint = headPos - Vector3.up * willDistantLookOffset;

        Vector3 distantCamPos = headPos + Vector3.back * willDistantDistance;

        _stageCamera.transform.position = distantCamPos;
        _stageCamera.transform.LookAt(lookAtPoint);
        _stageCamera.fieldOfView = willDistantFov;

        FaceCameraFlat(_willInstance, distantCamPos);

        SetMagoVisible(false);
        SetWillVisible(true);

        Time.timeScale = willSlowMotionScale;

        // Solo preparación en esta fase — el lanzamiento real (hacia el Mago Oscuro, no hacia
        // cámara) pasa en la Fase E, donde los dos disparan a la vez. Mismo patrón que la Fase B:
        // clip crudo vía Playables si hay uno asignado, si no el gesto normal del controller.
        if (willLoadClip != null)
        {
            PlayRawClip(_willAnimator, willLoadClip, ref _willCastGraph);
        }
        else
        {
            _willAnimator.SetLayerWeight(willOriginalAnimLayer, 1f);
            _willAnimator.Play(willOriginalAnimState, willOriginalAnimLayer, 0f);
        }

        // El hechizo cargando en su mano — mismo patrón que Co_SpawnChargingVfxDelayed en la Fase B.
        // Clampado contra willPreparationHoldDuration: si el retardo configurado fuera igual o
        // mayor que el hold, el corte a negro llegaría antes de que el VFX tuviera ocasión de
        // aparecer.
        StartCoroutine(Co_SpawnWillChargingVfxDelayed(SafeVfxDelay(willVfxDelay, willPreparationHoldDuration)));

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: false);
        // Tiempo de JUEGO: coincide con el gesto de preparación bajo el slow-mo. Mientras esta
        // espera dure más que willVfxDelay, el hechizo ya estará cargado y visible antes de cortar.
        yield return new WaitForSeconds(willPreparationHoldDuration);

        // Fundido a negro CON el hechizo todavía en pantalla — la limpieza de abajo (Rebind/Destroy)
        // pasa ya tapada por el negro, no antes.
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        Time.timeScale = 1f;
        StopRawClip(_willAnimator, ref _willCastGraph);
        StopChargeLoop();
        if (_willChargingVfxInstance != null) { Destroy(_willChargingVfxInstance); _willChargingVfxInstance = null; }
        SetWillVisible(false);
    }

    private IEnumerator Co_SpawnWillChargingVfxDelayed(float delay)
    {
        // Tiempo de JUEGO (no realtime): debe coincidir con el momento del clip afectado por el
        // slow-mo, no con el reloj real.
        yield return new WaitForSeconds(delay);
        if (_willAnimator == null || willLoadVfx == null) yield break;

        Transform hand = GetHandBone(_willAnimator);
        Vector3 spawnPos = hand != null
            ? hand.position + willLoadVfxOffset
            : _willAnimator.transform.position + Vector3.up * 1.2f + willLoadVfxOffset;
        _willChargingVfxInstance = Instantiate(willLoadVfx, spawnPos, Quaternion.identity, _stageRoot);
        PlaySpellInstantiate();
        StartChargeLoop();
        _willVfxTrackRoutine = StartCoroutine(Co_TrackVfxAboveHand(_willChargingVfxInstance, _willAnimator, willLoadVfxOffset));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers de cámara / actor (compartidos por las Fases A-D)
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_LerpCamera(Vector3 fromPos, Vector3 toPos, Vector3 fromLookAt, Vector3 toLookAt,
                                       float fromFov, float toFov, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _stageCamera.transform.position = Vector3.Lerp(fromPos, toPos, k);
            _stageCamera.transform.LookAt(Vector3.Lerp(fromLookAt, toLookAt, k));
            _stageCamera.fieldOfView = Mathf.Lerp(fromFov, toFov, k);
            yield return null;
        }
        _stageCamera.transform.position = toPos;
        _stageCamera.transform.LookAt(toLookAt);
        _stageCamera.fieldOfView = toFov;
    }

    private static IEnumerator Co_MoveActor(Transform actor, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (actor == null) yield break;
            actor.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        if (actor != null) actor.position = to;
    }

    /// Reproduce un AnimationClip suelto en el Animator, saltándose el Animator Controller por
    /// completo (Playables). Así no hace falta añadir ningún estado nuevo al controller compartido
    /// de NPC solo para este plano puntual.
    private void PlayRawClip(Animator animator, AnimationClip clip, ref PlayableGraph graph)
    {
        if (animator == null || clip == null) return;
        if (graph.IsValid()) graph.Destroy();
        AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
    }

    /// Destruye el graph de Playables y devuelve el Animator a su Controller normal.
    private static void StopRawClip(Animator animator, ref PlayableGraph graph)
    {
        if (graph.IsValid()) graph.Destroy();
        if (animator != null) animator.Rebind();
    }

    /// GetBoneTransform lanza/loguea error si el Animator no es Humanoid — con esto, un rig
    /// Generic simplemente no tiene VFX en la mano (fallback a headPos/transform) en vez de
    /// abortar la coroutine con una excepción silenciosa (ver el bug de _actionManager: una
    /// excepción a mitad de Co_Sequence deja la pantalla en negro para siempre).
    private static Transform GetHandBone(Animator animator)
    {
        if (animator == null || !animator.isHuman) return null;
        return animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    /// Igual que GetHandBone pero para la cabeza — se usa para encuadrar los primeros planos
    /// faciales y calcular la altura real del personaje en vez de un offset fijo que no encaja
    /// con la altura real del rig. Rig Generic → null, fallback a una altura aproximada.
    private static Transform GetHeadBone(Animator animator)
    {
        if (animator == null || !animator.isHuman) return null;
        return animator.GetBoneTransform(HumanBodyBones.Head);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase E — Enfrentados de perfil + colisión de hechizos
    // ══════════════════════════════════════════════════════════════════════════

    /// Gira a los dos actores para que se miren el uno al otro DE PERFIL (no a cámara) — el
    /// enfrentamiento cara a cara, "como si fuese un nivel 2D", que pedía el diseño para esta fase.
    private void FaceEachOther()
    {
        // Mago a la izquierda mirando a la derecha (+X, hacia Will); Will a la derecha mirando a
        // la izquierda (-X, hacia el Mago).
        if (_magoInstance != null) _magoInstance.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        if (_willInstance != null) _willInstance.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
    }

    private IEnumerator Co_DualBlurredCollision()
    {
        if (_magoInstance == null || _willInstance == null) yield break;

        // Enfrentados de perfil, no mirando a cámara — seguimos en negro tras la Fase D.
        FaceEachOther();

        Vector3 lookAtCenter = stageAnchorPosition + Vector3.up * cameraHeight;
        Vector3 closeCamPos  = lookAtCenter + Vector3.back * confrontationCloseDistance;
        Vector3 wideCamPos   = stageAnchorPosition + Vector3.back * cameraDistance + Vector3.up * cameraHeight;

        _stageCamera.transform.position = closeCamPos;
        _stageCamera.transform.LookAt(lookAtCenter);
        _stageCamera.fieldOfView = confrontationCloseFov;

        SetMagoVisible(true);
        SetWillVisible(true);

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: false);

        // Empiezan a preparar el hechizo YA, nada más revelarse — antes se quedaban en idle
        // durante todo el plano cerrado y los flashes, porque el bucle de pose no arrancaba hasta
        // después (bug reportado: "hay una fase donde aparecen en idle y deberían estar
        // preparando el hechizo"). El bucle se corta explícitamente en Co_MutualCastAndBlackout,
        // cuando toca soltar el hechizo. Solo se ve la POSE durante toda esta fase — el VFX del
        // hechizo no se instancia aquí, aparece recién en Co_MutualCastAndBlackout, a la vez para
        // los dos (ver comentario en BeginMutualPreparation).
        BeginMutualPreparation();

        // Plano muy cerrado, de perfil, ocupando casi toda la pantalla, borroso — "no se distingue
        // bien, ¿qué pasa?". El blur pulsa en vez de parpadear la visibilidad de los actores.
        // Ya están cargando el hechizo en bucle durante todo este tramo.
        yield return Co_PulseBlur(presentationDuration);

        yield return Co_WarFlashes();

        // Y AHORA alejamos la cámara para ver cómo se lo disparan el uno al otro — siguen
        // preparando el hechizo (en bucle) durante todo el zoom.
        yield return Co_LerpCamera(closeCamPos, wideCamPos, lookAtCenter, lookAtCenter, confrontationCloseFov, cameraFov, confrontationZoomOutDuration);

        // Último instante de preparación visible, ya con la cámara abierta, antes de soltar el
        // hechizo.
        yield return new WaitForSecondsRealtime(mutualPreparationHoldDuration);

        // Lanzamiento a la vez, uno hacia el otro, tensión creciente — termina ya en negro, justo
        // cuando están a punto de encontrarse en el centro.
        yield return Co_MutualCastAndBlackout();

        // La explosión se revela DESPUÉS del corte, no antes — "nos vamos a negro y ya vemos el
        // hechizo explotando de los dos en el centro".
        yield return Co_Collision();
    }

    /// Arranca el bucle de la POSE de preparación para ambos actores — mismo patrón que las Fases
    /// B/D (clip crudo vía Playables), pero SIN límite de tiempo: el bucle sigue corriendo
    /// (totalDuration = float.MaxValue) hasta que Co_MutualCastAndBlackout lo corta explícitamente
    /// vía _magoPrepLoopRoutine/_willPrepLoopRoutine. Así se les ve "preparando" durante TODO el
    /// tramo cerrado — blur, flashes y zoom out — en vez de quedarse en idle hasta el último
    /// momento. Si les asignas el MISMO AnimationClip a magoOscuroLoadClip y willLoadClip, hacen
    /// literalmente el mismo gesto.
    ///
    /// A propósito, esta fase NO instancia el VFX del hechizo — antes sí lo hacía (con
    /// openingVfxDelay/willVfxDelay) y se veía "cargando" en la mano durante todo este tramo, pero
    /// queda más limpio que solo se vea la pose de preparación y el hechizo aparezca de golpe al
    /// soltarlo, los dos a la vez (spawn + lanzamiento en Co_MutualCastAndBlackout).
    private void BeginMutualPreparation()
    {
        if (magoOscuroLoadClip != null)
        {
            _magoPrepLoopRoutine = StartCoroutine(Co_LoopRawClip(_magoAnimator, magoOscuroLoadClip,
                () => PlayRawClip(_magoAnimator, magoOscuroLoadClip, ref _magoLoadGraph), float.MaxValue));
        }
        else if (_magoAnimator != null)
        {
            _magoAnimator.SetLayerWeight(magoOscuroAnimLayer, 1f);
            _magoAnimator.Play(magoOscuroAnimState, magoOscuroAnimLayer, 0f);
        }

        if (willLoadClip != null)
        {
            _willPrepLoopRoutine = StartCoroutine(Co_LoopRawClip(_willAnimator, willLoadClip,
                () => PlayRawClip(_willAnimator, willLoadClip, ref _willCastGraph), float.MaxValue));
        }
        else if (_willAnimator != null)
        {
            _willAnimator.SetLayerWeight(willOriginalAnimLayer, 1f);
            _willAnimator.Play(willOriginalAnimState, willOriginalAnimLayer, 0f);
        }
    }

    /// AnimationPlayableUtilities.PlayClip no repite el clip solo — un AnimationClipPlayable no
    /// tiene loop propio como sí lo tienen los estados del Animator Controller. Lo simulamos
    /// reiniciando el clip cada vez que termina, hasta cumplir 'totalDuration' (pequeño salto en
    /// cada vuelta, aceptable para un gesto de carga repetido).
    private IEnumerator Co_LoopRawClip(Animator animator, AnimationClip clip, System.Action replay, float totalDuration)
    {
        if (animator == null || clip == null) yield break;
        float clipLength = Mathf.Max(0.05f, clip.length);
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            replay();
            float wait = Mathf.Min(clipLength, totalDuration - elapsed);
            yield return new WaitForSecondsRealtime(wait);
            elapsed += wait;
        }
    }

    /// Ambos actores disparan hacia el punto medio entre los dos — el Mago Oscuro desde la
    /// izquierda, Will desde la derecha, y explotan en el centro. El hechizo NO estaba cargando en
    /// la mano durante la preparación (esa fase es solo pose, ver BeginMutualPreparation): se
    /// instancia AQUÍ MISMO, en el instante exacto en que se suelta, a la vez para los dos
    /// (SpawnAndLaunchMutualVfx), y sale disparado directo hacia el centro.
    private IEnumerator Co_MutualCastAndBlackout()
    {
        if (_magoInstance == null || _willInstance == null) yield break;

        Vector3 collisionPoint = Vector3.Lerp(_magoInstance.transform.position, _willInstance.transform.position, 0.5f)
                                 + Vector3.up * 1.2f;

        // Corta el bucle de preparación — ahora toca la animación de SOLTAR el hechizo.
        if (_magoPrepLoopRoutine != null) { StopCoroutine(_magoPrepLoopRoutine); _magoPrepLoopRoutine = null; }
        if (_willPrepLoopRoutine != null) { StopCoroutine(_willPrepLoopRoutine); _willPrepLoopRoutine = null; }
        StopChargeLoop();
        PlaySpellRelease();

        if (magoOscuroReleaseClip != null)
        {
            PlayRawClip(_magoAnimator, magoOscuroReleaseClip, ref _magoLoadGraph);
        }
        else if (_magoAnimator != null)
        {
            _magoAnimator.SetLayerWeight(magoOscuroAnimLayer, 1f);
            _magoAnimator.Play(magoOscuroAnimState, magoOscuroAnimLayer, 0f);
            _magoAnimator.speed = magoOscuroAnimSpeed;
        }
        if (willReleaseClip != null)
        {
            PlayRawClip(_willAnimator, willReleaseClip, ref _willCastGraph);
        }
        else if (_willAnimator != null)
        {
            _willAnimator.SetLayerWeight(willOriginalAnimLayer, 1f);
            _willAnimator.Play(willOriginalAnimState, willOriginalAnimLayer, 0f);
        }

        // Enfrentados de perfil, no a cámara — por si algo (Root Motion, un Rebind) hubiera movido
        // la rotación durante la preparación.
        FaceEachOther();

        // Ya no hace falta cortar un seguimiento de mano previo — el hechizo no existía hasta este
        // instante (ver SpawnAndLaunchMutualVfx). Se limpian las corrutinas solo por si acaso quedó
        // alguna viva de un ciclo anterior.
        if (_magoVfxTrackRoutine != null) { StopCoroutine(_magoVfxTrackRoutine); _magoVfxTrackRoutine = null; }
        if (_willVfxTrackRoutine != null) { StopCoroutine(_willVfxTrackRoutine); _willVfxTrackRoutine = null; }

        SpawnAndLaunchMutualVfx(collisionPoint);

        // Tensión creciente mientras los dos hechizos se acercan el uno al otro — "más tensión,
        // todo tiembla" pero ahora desde los dos lados a la vez.
        int steps = Mathf.Max(1, mutualCastShakeSteps);
        float stepDuration = mutualCastTravelDuration / steps;
        for (int i = 0; i < steps; i++)
        {
            float t = (i + 1f) / steps;
            float intensity = Mathf.Lerp(0.05f, mutualCastShakeMaxIntensity, t);
            FeedbackService.CameraShake(_stageCamera, intensity, stepDuration);
            yield return new WaitForSecondsRealtime(stepDuration);
        }

        // Corte a negro justo cuando están a punto de encontrarse — el choque en sí se revela en
        // Co_Collision, no aquí.
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        StopRawClip(_magoAnimator, ref _magoLoadGraph);
        StopRawClip(_willAnimator, ref _willCastGraph);
        if (_magoChargingVfxInstance != null) { Destroy(_magoChargingVfxInstance); _magoChargingVfxInstance = null; }
        if (_willChargingVfxInstance != null) { Destroy(_willChargingVfxInstance); _willChargingVfxInstance = null; }
        SetActorsVisible(false);
    }

    /// Instancia el hechizo de cada uno EN ESTE INSTANTE — no antes — y lo lanza directo al punto
    /// de colisión. Antes se instanciaba ya en BeginMutualPreparation y se le veía "cargando" en la
    /// mano durante todo el plano cerrado y el zoom out; ahora solo se ve la pose de preparación
    /// hasta este momento, en el que aparece y sale disparado para los dos a la vez. Usa el mismo
    /// prefab "de carga" (magoOscuroLoadVfx/willLoadVfx) que antes seguía la mano; si no está
    /// asignado, cae de vuelta a darkImpactVfx/lightImpactVfx como fallback.
    private void SpawnAndLaunchMutualVfx(Vector3 collisionPoint)
    {
        if (magoOscuroLoadVfx != null && _magoAnimator != null)
        {
            Transform hand = GetHandBone(_magoAnimator);
            Vector3 spawnPos = hand != null
                ? hand.position + magoOscuroLoadVfxOffset
                : _magoAnimator.transform.position + Vector3.up * 1.2f + magoOscuroLoadVfxOffset;
            _magoChargingVfxInstance = Instantiate(magoOscuroLoadVfx, spawnPos, Quaternion.identity, _stageRoot);
            PlaySpellInstantiate();
            StartCoroutine(Co_TravelToPoint(_magoChargingVfxInstance.transform, spawnPos, collisionPoint, mutualCastTravelDuration));
        }
        else if (darkImpactVfx != null)
        {
            Transform hand = GetHandBone(_magoAnimator);
            Vector3 spawnPos = hand != null ? hand.position : _magoInstance.transform.position + Vector3.up * 1.4f;
            _magoChargingVfxInstance = Instantiate(darkImpactVfx, spawnPos, Quaternion.identity);
            StartCoroutine(Co_TravelToPoint(_magoChargingVfxInstance.transform, spawnPos, collisionPoint, mutualCastTravelDuration));
        }

        if (willLoadVfx != null && _willAnimator != null)
        {
            Transform hand = GetHandBone(_willAnimator);
            Vector3 spawnPos = hand != null
                ? hand.position + willLoadVfxOffset
                : _willAnimator.transform.position + Vector3.up * 1.2f + willLoadVfxOffset;
            _willChargingVfxInstance = Instantiate(willLoadVfx, spawnPos, Quaternion.identity, _stageRoot);
            StartCoroutine(Co_TravelToPoint(_willChargingVfxInstance.transform, spawnPos, collisionPoint, mutualCastTravelDuration));
        }
        else if (lightImpactVfx != null)
        {
            Transform hand = GetHandBone(_willAnimator);
            Vector3 spawnPos = hand != null ? hand.position : _willInstance.transform.position + Vector3.up * 1.4f;
            _willChargingVfxInstance = Instantiate(lightImpactVfx, spawnPos, Quaternion.identity);
            StartCoroutine(Co_TravelToPoint(_willChargingVfxInstance.transform, spawnPos, collisionPoint, mutualCastTravelDuration));
        }
    }

    private static IEnumerator Co_TravelToPoint(Transform vfx, Vector3 from, Vector3 to, float duration)
    {
        Vector3 startScale = vfx.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            if (vfx == null) yield break;
            vfx.position = Vector3.Lerp(from, to, k);
            vfx.localScale = startScale * Mathf.Lerp(1f, 2f, k);
            yield return null;
        }
    }

    private IEnumerator Co_WarFlashes()
    {
        if (warFlashVisuals != null && warFlashVisuals.Length > 0)
        {
            for (int i = 0; i < warFlashVisuals.Length; i++)
            {
                SetWarFlashVisualsActive(i);
                FeedbackService.CameraShake(_stageCamera, flashShakeIntensity, flashOnDuration);
                PlayRandomStinger();

                yield return new WaitForSecondsRealtime(flashOnDuration);

                SetWarFlashVisualsActive(-1);
                // Vuelta breve a los actores centrales entre cada corte — "memoria interrumpida".
                yield return new WaitForSecondsRealtime(backToActorsDuration);
            }
            yield break;
        }

        // FALLBACK (4 sep 2026): sin warFlashVisuals asignado a mano en el Editor, reutiliza los
        // mismos VFX de energía que ya se ven en esta cinemática (lightImpactVfx/darkImpactVfx —
        // los mismos que dispara Co_Collision) como destellos de "choque" extra durante el plano
        // cerrado, en vez de dejar el efecto completamente desactivado. Mantiene coherencia
        // visual con el resto de la secuencia sin requerir colocar nada a mano en el Editor. Si
        // en el futuro se asigna warFlashVisuals desde el Inspector, el camino de arriba vuelve a
        // tener prioridad automáticamente.
        if ((lightImpactVfx == null && darkImpactVfx == null) || VfxPoolService.Instance == null) yield break;

        GameObject[] fallbackFlashPrefabs = { lightImpactVfx, darkImpactVfx, lightImpactVfx };
        Vector3 flashCenter = stageAnchorPosition + Vector3.up * cameraHeight;

        for (int i = 0; i < fallbackFlashPrefabs.Length; i++)
        {
            GameObject prefab = fallbackFlashPrefabs[i];
            if (prefab == null) continue;

            // VfxPoolService gestiona su propio ciclo de vida — no hace falta SetActive/Destroy
            // manual ni limpieza en Cleanup() si el skip corta a mitad (se autolimpia a los
            // flashOnDuration segundos igualmente).
            VfxPoolService.Instance.Play(prefab, flashCenter, Quaternion.identity, flashOnDuration);
            FeedbackService.CameraShake(_stageCamera, flashShakeIntensity, flashOnDuration);
            PlayRandomStinger();

            yield return new WaitForSecondsRealtime(flashOnDuration);

            // Vuelta breve a los actores centrales entre cada corte — "memoria interrumpida".
            yield return new WaitForSecondsRealtime(backToActorsDuration);
        }
    }

    private IEnumerator Co_Collision()
    {
        // Se llama justo después de Co_MutualCastAndBlackout, con la pantalla en negro — la
        // explosión se revela aquí ("nos vamos a negro y ya vemos el hechizo explotando").
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: false);

        float startFov  = _stageCamera.fieldOfView;
        float targetFov = startFov * collisionZoomFovFactor;
        float elapsedZoom = 0f;
        while (elapsedZoom < collisionZoomDuration)
        {
            elapsedZoom += Time.unscaledDeltaTime;
            _stageCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, elapsedZoom / collisionZoomDuration);
            yield return null;
        }
        _stageCamera.fieldOfView = targetFov;

        _shockVolume.weight = 1f;
        PlayTinnitus(); // pico del pitido, coincide con el choque
        PlayExplosion();
        FeedbackService.CameraShake(_stageCamera, 0.4f, collisionHoldDuration);

        Vector3 collisionPoint = Vector3.Lerp(_magoInstance.transform.position, _willInstance.transform.position, 0.5f)
                                 + Vector3.up * 1.2f;

        if (lightImpactVfx != null)
            VfxPoolService.Instance.Play(lightImpactVfx, collisionPoint, Quaternion.identity, collisionHoldDuration);
        if (darkImpactVfx != null)
            VfxPoolService.Instance.Play(darkImpactVfx, collisionPoint, Quaternion.identity, collisionHoldDuration);

        FeedbackService.ScreenFlash(lightFlashColor, collisionHoldDuration * 0.4f);
        yield return new WaitForSecondsRealtime(collisionHoldDuration * 0.4f);
        FeedbackService.ScreenFlash(darkFlashColor, collisionHoldDuration * 0.6f);

        yield return new WaitForSecondsRealtime(collisionHoldDuration * 0.6f);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase F — Corte a negro y despertar
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_Awaken()
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        // Corte de silencio total: vende el golpe del despertar más que cualquier sonido.
        if (AudioService.Instance != null)
        {
            AudioService.Instance.StopLoopingSFX(HeartbeatLoopId);
            AudioService.Instance.StopMusic(0.05f);
        }
        if (_shockVolume != null) _shockVolume.weight = 0f;
        SetActorsVisible(false);
        _dreamBackground?.StopDream();
        _dreamSparkles?.StopSparkles();

        _stageCamera.gameObject.SetActive(false);
        if (_worldMainCamera != null) _worldMainCamera.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(silenceDuration);

        // FIX (revisado): la versión anterior de este comentario decía que el nodo siguiente
        // (DramaticTextNode) no hacía ningún fundido propio y que por eso había que revelar
        // aquí — pero revelar AQUÍ, antes de EndCinematic()/RaiseSignalOut(), reactivaba
        // Camera.main (ya reactivada arriba) y la exponía sin candado real durante el hueco hasta
        // que el grafo avanzara al DramaticTextNode: el "salto a gameplay" que se veía entre el
        // sueño y el texto dramático.
        //
        // El patrón correcto (mismo que Co_EndCinematicStayBlack en el resto de cinemáticas): NO
        // revelar aquí, dejar la pantalla cubierta por FeedbackService, y que sea el sistema
        // siguiente quien revele cuando su propio contenido ya esté listo. DramaticTextOverlayUI
        // ahora sí hace esa parte (ver "screenAlreadyCovered" en DramaticTextOverlayUI.RunSequence):
        // si la frase 0 tiene fondo FullBlack y la pantalla ya está cubierta, salta el fade de
        // entrada y suelta el overlay de FeedbackService en el mismo instante en que su propio
        // fondo negro ya es opaco — sin hueco intermedio.
        EndCinematic();
        RaiseSignalOut();
    }

    // ── Blur pulsante (Fase E) ────────────────────────────────────────────────

    private IEnumerator Co_PulseBlur(float duration)
    {
        if (_shockVolume == null) { yield return new WaitForSecondsRealtime(duration); yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            _shockVolume.weight = Random.Range(shockWeightMin, shockWeightMax);
            float interval = Random.Range(flickerOnMin, flickerOnMax);
            yield return new WaitForSecondsRealtime(interval);
            elapsed += interval;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private void PlayTinnitus()
    {
        if (AudioService.Instance == null || string.IsNullOrWhiteSpace(tinnitusSfxKey)) return;
        AudioService.Instance.PlaySFX(tinnitusSfxKey, tinnitusVolume);
    }

    private void PlayRandomStinger()
    {
        if (warClashStingerKeys == null || warClashStingerKeys.Length == 0) return;
        if (AudioService.Instance == null) return;
        string key = warClashStingerKeys[Random.Range(0, warClashStingerKeys.Length)];
        if (!string.IsNullOrWhiteSpace(key)) AudioService.Instance.PlaySFX(key);
    }

    /// Se llama desde SetMagoVisible/SetWillVisible cada vez que un actor pasa a estar visible —
    /// un actor "aparece" en cada plano nuevo (Fases A-E), no solo una vez en toda la secuencia.
    private void PlayActorAppear()
    {
        if (AudioService.Instance == null || string.IsNullOrWhiteSpace(actorAppearSfxKey)) return;
        AudioService.Instance.PlaySFX(actorAppearSfxKey);
    }

    private void PlaySpellInstantiate()
    {
        if (AudioService.Instance == null || string.IsNullOrWhiteSpace(spellInstantiateSfxKey)) return;
        AudioService.Instance.PlaySFX(spellInstantiateSfxKey);
    }

    /// Un único loop (loopId propio) compartido por los dos actores — en las Fases B/D solo carga
    /// uno, en la Fase E cargan los dos a la vez pero basta con un bucle (es el "aire zumbando",
    /// no un sonido pegado a la mano de cada uno). PlayLoopingSFX ya es idempotente: si ya está
    /// sonando el mismo loopId, no lo reinicia salvo que se llame de nuevo explícitamente.
    private void StartChargeLoop()
    {
        if (AudioService.Instance == null || string.IsNullOrWhiteSpace(spellChargeLoopSfxKey)) return;
        AudioService.Instance.PlayLoopingSFX(SpellChargeLoopId, spellChargeLoopSfxKey, spellChargeLoopVolume);
    }

    private void StopChargeLoop()
    {
        AudioService.Instance?.StopLoopingSFX(SpellChargeLoopId);
    }

    private void PlaySpellRelease()
    {
        if (AudioService.Instance == null || string.IsNullOrWhiteSpace(spellReleaseSfxKey)) return;
        AudioService.Instance.PlaySFX(spellReleaseSfxKey);
    }

    private void PlayExplosion()
    {
        if (AudioService.Instance == null || string.IsNullOrWhiteSpace(explosionSfxKey)) return;
        AudioService.Instance.PlaySFX(explosionSfxKey);
    }

    private void SetActorsVisible(bool visible)
    {
        SetMagoVisible(visible);
        SetWillVisible(visible);
    }

    /// Enciende/apaga al Mago Oscuro Y su rim light a la vez — antes solo se tocaba el renderer,
    /// así que su luz se quedaba encendida iluminando el vacío incluso con él oculto (bug
    /// reportado: iluminación del lado que no tocaba).
    private void SetMagoVisible(bool visible)
    {
        SetRenderersEnabled(_magoRenderers, visible);
        if (_magoRimLight != null) _magoRimLight.enabled = visible;
        if (visible) PlayActorAppear();
    }

    private void SetWillVisible(bool visible)
    {
        SetRenderersEnabled(_willRenderers, visible);
        if (_willRimLight != null) _willRimLight.enabled = visible;
        if (visible) PlayActorAppear();
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = enabled;
        }
    }

    private void SetWarFlashVisualsActive(int index)
    {
        if (warFlashVisuals == null) return;
        for (int i = 0; i < warFlashVisuals.Length; i++)
        {
            if (warFlashVisuals[i] != null)
                warFlashVisuals[i].SetActive(i == index);
        }
    }

    private void Cleanup()
    {
        // Por si la cinemática se interrumpe a mitad de una fase en slow-mo (Fase B/D).
        Time.timeScale = 1f;
        StopChargeLoop();
        if (AudioService.Instance != null) AudioService.Instance.StopLoopingSFX(HeartbeatLoopId);

        // Por si se interrumpe antes de llegar a Co_Awaken (que ya los para en el flujo normal).
        _dreamBackground?.StopDream();
        _dreamSparkles?.StopSparkles();

        // FIX (16 ago 2026 — auditoría de skip en todas las cinemáticas): si el skip llega a mitad
        // de la Fase E (preparación mutua / guerra), estas dos corrutinas fire-and-forget solo se
        // paraban al llegar a Co_MutualCastAndBlackout() en el flujo normal — sin esto, quedaban
        // en bucle indefinido tras saltar. Y warFlashVisuals[] solo se apagan por su propio timer
        // dentro de Co_WarFlashes(): si el skip corta mientras uno está activo, se queda visible
        // en la escena para siempre.
        if (_magoPrepLoopRoutine != null) { StopCoroutine(_magoPrepLoopRoutine); _magoPrepLoopRoutine = null; }
        if (_willPrepLoopRoutine != null) { StopCoroutine(_willPrepLoopRoutine); _willPrepLoopRoutine = null; }
        SetWarFlashVisualsActive(-1);

        if (_magoLoadGraph.IsValid()) _magoLoadGraph.Destroy();
        if (_willCastGraph.IsValid()) _willCastGraph.Destroy();
        if (_magoChargingVfxInstance != null) Destroy(_magoChargingVfxInstance);
        if (_willChargingVfxInstance != null) Destroy(_willChargingVfxInstance);

        if (_worldMainCamera != null) _worldMainCamera.gameObject.SetActive(true);

        if (_runtimeProfile != null)
        {
            foreach (var component in _runtimeProfile.components)
                if (component != null) Destroy(component);
            Destroy(_runtimeProfile);
            _runtimeProfile = null;
        }

        if (_stageRoot != null)
            Destroy(_stageRoot.gameObject);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void OnValidate()
    {
        if (warFlashVisuals != null && warFlashVisuals.Length == 0)
            Debug.Log("[PrologueDreamSequencer] Sin warFlashVisuals asignados — la Fase E usará como fallback lightImpactVfx/darkImpactVfx (vía VfxPoolService) para los destellos de choque. Asigna GameObjects aquí solo si quieres un flash de guerra distinto al de la colisión final. Es opcional, no bloquea el resto de la secuencia.", this);
    }
#endif
}
