using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Sendero.Core.Feedback;
using Game.NPC;

/// Orquestador de la escena de la taberna:
///   0. El grupo discute lo ocurrido mientras espera la comida.
///   1. El estómago de Estela ruge de forma épica.
///   2. Llega la comida (fade a negro para activar los platos).
///   3. Estela come de forma caótica y exagerada.
///   4. Will y Eldran reaccionan con sorpresa y asco.
///   5. Liam se acerca e intenta hablar.
///   6. Estela explota: emite un VFX de rayos sin dejar de comer.
///   7. Los tres se sorprenden.
///   8. Eldran dice su frase y transición al gameplay.
/// Señal de entrada: "TABERNA_START".
/// Señal de salida:  "TABERNA_DONE".
[DisallowMultipleComponent]
public class TabernaSequencer : CinematicSequencerBase
{
    [Header("Personaje — Will")]
    [SerializeField] private Transform _willTransform;
    [Tooltip("Componente del jugador para gestionar su actividad ambiental (sentarse)")]
    [SerializeField] private PlayerAmbientActivityHandler _willActivityHandler;

    [Header("Personaje — Eldran")]
    [SerializeField] private Transform _eldranTransform;
    [Tooltip("Punto al que huye Eldran al final")]
    [SerializeField] private Transform _eldranFleeTarget;

    [Header("Personaje — Estela")]
    [SerializeField] private Transform _estelaTransform;
    [Tooltip("Dirección a la que mira Estela mientras está sentada (mesa, plato). Si no se asigna, no se fuerza rotación.")]
    [SerializeField] private Transform _estelaFoodFacingTarget;

    [Header("Personaje — Liam")]
    [SerializeField] private Transform _liamTransform;
    [Tooltip("Posición junto a la mesa a la que llega Liam caminando")]
    [SerializeField] private Transform _liamApproachTarget;
    [Tooltip("Punto al que huye Liam al final")]
    [SerializeField] private Transform _liamFleeTarget;

    [Header("Asientos — NPCWorldPoints")]
    [Tooltip("NPCWorldPoint de la silla de Will (el jugador se teletransporta aquí al inicio)")]
    [SerializeField] private NPCWorldPoint _willSeat;
    [Tooltip("NPCWorldPoint de la silla de Eldran")]
    [SerializeField] private NPCWorldPoint _eldranSeat;
    [Tooltip("NPCWorldPoint de la silla de Estela")]
    [SerializeField] private NPCWorldPoint _estelaSeat;

    [Header("Mesa — comida")]
    [Tooltip("Platos que aparecen en la mesa al llegar la comida. Deben estar inactivos por defecto.")]
    [SerializeField] private GameObject[] _foodObjects;
    [SerializeField] private float _foodFadeDuration = 0.35f;

    [Header("Skip — reveal manual")]
    [Tooltip("Duración del fundido que retira el negro tras saltar la secuencia con el botón global de skip (ver OnSkipCleanup).")]
    [SerializeField] private float _skipRevealDuration = 0.3f;

    [Header("Cámara — planos")]
    [Tooltip("Los tres sentados en la mesa")]
    [SerializeField] private Transform _shotGroup;
    [Tooltip("Plano medio o close de Estela")]
    [SerializeField] private Transform _shotEstela;
    [Tooltip("Two-shot Will y Eldran")]
    [SerializeField] private Transform _shotWillEldran;
    [Tooltip("Liam acercándose o llegando a la mesa")]
    [SerializeField] private Transform _shotLiam;
    [Tooltip("Plano general de la taberna para la huida")]
    [SerializeField] private Transform _shotWide;

    // ── Fase 0 — El grupo discute lo ocurrido ─────────────────────────────────

    [Header("Fase 0 — El grupo discute")]
    [SerializeField] private string     _keyEldran01      = "EVT_TAB_ELDRAN_01";
    [SerializeField] private NPCEmotion _emotionEldran01  = NPCEmotion.Thinking;
    [SerializeField] private float      _eldran01Duration = 3.5f;

    [SerializeField] private string     _keyWill01        = "EVT_TAB_WILL_01";
    [SerializeField] private NPCEmotion _emotionWill01    = NPCEmotion.Thinking;
    [SerializeField] private string     _animWill01       = "Thinking01";
    [SerializeField] private float      _will01Duration   = 2.5f;

    [SerializeField] private string     _keyEstela01      = "EVT_TAB_ESTELA_01";
    [SerializeField] private NPCEmotion _emotionEstela01  = NPCEmotion.Happy;
    [SerializeField] private string     _animEstela01     = "Happy01";
    [SerializeField] private float      _estela01Duration = 2.5f;

    // ── Fase 1 — El estómago de Estela ruge ──────────────────────────────────

    [Header("Fase 1 — Estómago de Estela")]
    [SerializeField] private string     _keyEstela02      = "EVT_TAB_ESTELA_02";
    [SerializeField] private NPCEmotion _emotionEstela02  = NPCEmotion.Scared;
    [SerializeField] private float      _estela02Duration = 2.0f;

    // ── Fase 2 — Llega la comida ──────────────────────────────────────────────

    [Header("Fase 2 — Llega la comida")]
    [SerializeField] private string     _keyEstela03      = "EVT_TAB_ESTELA_03";
    [SerializeField] private NPCEmotion _emotionEstela03  = NPCEmotion.Happy;
    [SerializeField] private string     _animEstela03     = "Happy01";
    [SerializeField] private float      _estela03Duration = 1.5f;
    [SerializeField] private float      _foodHold         = 1.0f;
    [Tooltip("ID de la SequenceRule en el AudioGraphProfile. Esa música arranca cuando llega la comida y el minijuego la sustituirá.")]
    [SerializeField] private string     _comidaMusicId;

    // ── Fase 3 — Estela come (exagerado) ─────────────────────────────────────
    // El bucle de comer arranca aquí y sigue activo hasta la Fase 6

    [Header("Fase 3 — Estela come (exagerado; el bucle sigue hasta Fase 6)")]
    [SerializeField] private string _animEstelaEat       = "Eat01";
    [SerializeField] private float  _eatHold             = 3.0f;
    [Tooltip("Multiplicador de velocidad de la animación de comer")]
    [SerializeField] private float  _eatSpeedMultiplier  = 2.5f;
    [Tooltip("Cada cuántos segundos se relanza el gesto de comer")]
    [SerializeField] private float  _eatRepeatInterval   = 0.6f;

    // ── Fase 4 — Will y Eldran reaccionan ────────────────────────────────────

    [Header("Fase 4 — Will y Eldran reaccionan")]
    [SerializeField] private string     _keyWill02       = "EVT_TAB_WILL_02";
    [SerializeField] private NPCEmotion _emotionWill02   = NPCEmotion.Surprised;
    [SerializeField] private string     _animWill02;
    [SerializeField] private float      _will02Duration  = 2.5f;

    [SerializeField] private string     _keyEldran02     = "EVT_TAB_ELDRAN_02";
    [SerializeField] private NPCEmotion _emotionEldran02 = NPCEmotion.Scared;
    [SerializeField] private string     _animEldran02;
    [SerializeField] private float      _eldran02Duration = 2.5f;

    // ── Fase 5 — Liam se acerca ───────────────────────────────────────────────

    [Header("Fase 5 — Liam se acerca")]
    [Tooltip("El texto se divide en páginas por '\\n': la primera se dice desde lejos, el resto en primer plano.")]
    [SerializeField] private string     _keyLiam01      = "EVT_TAB_LIAM_01";
    [SerializeField] private NPCEmotion _emotionLiam01  = NPCEmotion.Thinking;
    [SerializeField] private string     _animLiam01     = "Thinking01";
    [Tooltip("Duración de cada página del bocadillo")]
    [SerializeField] private float      _liam01Duration = 3.5f;
    [Tooltip("Distancia de la cámara a Liam en el primer plano")]
    [SerializeField] private float      _liamCloseupDistance = 2f;
    [Tooltip("Altura del punto de mira sobre el pivote de Liam (pecho/cara)")]
    [SerializeField] private float      _liamCloseupLookHeight = 1.5f;
    [Tooltip("Duración del acercamiento de cámara; transcurre mientras dice la segunda frase")]
    [SerializeField] private float      _liamCloseupBlendTime = 1.2f;
    [Tooltip("Ángulo lateral del primer plano en grados (0 = de frente, + = cámara hacia la derecha de Liam)")]
    [SerializeField] private float      _liamCloseupYawOffset = 0f;

    // ── Fase 6 — Estela explota sin dejar de comer ───────────────────────────

    [Header("Fase 6 — Estela explota (sin dejar de comer)")]
    [SerializeField] private string     _keyEstelaRage    = "EVT_TAB_ESTELA_RAGE";
    [SerializeField] private NPCEmotion _emotionEstelaRage = NPCEmotion.Angry;
    [SerializeField] private float      _rageDuration     = 2.5f;
    [SerializeField] private float      _aimDelay         = 0.5f;
    [Tooltip("VFX de rayos/electricidad que aparece en Estela cuando explota. Debe autodestruirse.")]
    [SerializeField] private GameObject _lightningVFX;
    [Tooltip("Plano cerrado de Estela para el 'SE VA A ENTERAR'. Si no se asigna permanece en _shotEstela.")]
    [SerializeField] private Transform  _shotEstelaCloseup;
    [Tooltip("VFX de aura/rabia que se instancia sobre Estela en el primer plano. Debe autodestruirse.")]
    [SerializeField] private GameObject _estelaRageAuraVFX;
    [Tooltip("Animación de rabia de Estela durante el primer plano. Se asigna por inspector.")]
    [SerializeField] private string     _animEstelaRage       = "Rage01";
    [SerializeField] private string     _keyEstelaSeVaEnterar = "EVT_TAB_ESTELA_SEVAENTERAR";
    [SerializeField] private float      _seVaEnterarDuration  = 2.5f;
    [Tooltip("Escala de tiempo durante el primer plano (0.3 = cámara lenta intensa, 0.5 = moderada)")]
    [SerializeField] private float      _slowMotionScale      = 0.3f;
    [Tooltip("Animación de reacción de Liam al ver a Estela explotar")]
    [SerializeField] private string     _animLiamDodge        = "Dodge01";

    // ── Fase 7 — Sorpresa ─────────────────────────────────────────────────────

    [Header("Fase 7 — Sorpresa (aplica a Will, Eldran y Liam)")]
    [SerializeField] private string     _keyWill03       = "EVT_TAB_WILL_03";
    [SerializeField] private NPCEmotion _emotionSurprise = NPCEmotion.Scared;
    [SerializeField] private string     _animWill03;
    [SerializeField] private float      _will03Duration  = 1.5f;

    // ── Fase 8 — ¡Corred! ─────────────────────────────────────────────────────

    [Header("Fase 8 — ¡Corred!")]
    [SerializeField] private string     _keyEldranFlee    = "EVT_TABERNA_10";
    [SerializeField] private NPCEmotion _emotionEldranFlee = NPCEmotion.Scared;
    [SerializeField] private string     _animEldranFlee   = "Scared01";
    [SerializeField] private float      _fleeDuration     = 3.0f;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private NPCEmotionController _willEmotion;
    private NPCEmotionController _eldranEmotion;
    private NPCEmotionController _estelaEmotion;
    private NPCEmotionController _liamEmotion;
    private NPCSimpleAnimator    _estelaSimpleAnim;
    private NPCSimpleAnimator    _eldranSimpleAnim;
    private NPCSimpleAnimator    _liamSimpleAnim;
    private Animator             _estelaAnimator;
    private NavMeshAgent         _eldranAgent;
    private NavMeshAgent         _estelaAgent;
    private NavMeshAgent         _liamAgent;
    private NPCBehaviourManagerV2 _eldranBehaviour;
    private NPCBehaviourManagerV2 _estelaBehaviour;
    private Coroutine            _eatLoopCoroutine;
    private Coroutine            _estelaFacingLock;
    private Coroutine            _liamFacingLock;
    private Transform            _liamCloseupShot;   // generado en runtime a partir de _shotLiam
    private GameObject           _rageAuraVFXInstance;   // instancias de VFX sobre Estela: se destruyen
    private GameObject           _lightningVFXInstance;  // en el cleanup por si el prefab no se autodestruye

    private CharacterController _willCharController;
    private Vector3    _willPreSequencePosition;
    private Quaternion _willPreSequenceRotation;

    // ✅ FIX: true mientras Eldran/Estela están sentados por esta secuencia. Permite
    // liberar el asiento de forma defensiva en OnDestroy si la secuencia se interrumpe
    // antes de llegar al blackout de salida (ver CleanupSeatsIfNeeded).
    private bool _seatsActive;

    protected override void Awake()
    {
        base.Awake();
        if (_willTransform != null)
        {
            _willEmotion        = _willTransform.GetComponentInChildren<NPCEmotionController>();
            _willCharController = _willTransform.GetComponent<CharacterController>();
        }
        if (_eldranTransform != null)
        {
            _eldranEmotion    = _eldranTransform.GetComponentInChildren<NPCEmotionController>();
            _eldranSimpleAnim = _eldranTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _eldranAgent      = _eldranTransform.GetComponent<NavMeshAgent>();
            _eldranBehaviour  = _eldranTransform.GetComponent<NPCBehaviourManagerV2>();
        }
        if (_estelaTransform != null)
        {
            _estelaEmotion    = _estelaTransform.GetComponentInChildren<NPCEmotionController>();
            _estelaSimpleAnim = _estelaTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _estelaAnimator   = _estelaTransform.GetComponentInChildren<Animator>();
            _estelaAgent      = _estelaTransform.GetComponent<NavMeshAgent>();
            _estelaBehaviour  = _estelaTransform.GetComponent<NPCBehaviourManagerV2>();
        }
        if (_liamTransform != null)
        {
            _liamEmotion    = _liamTransform.GetComponentInChildren<NPCEmotionController>();
            _liamSimpleAnim = _liamTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _liamAgent      = _liamTransform.GetComponent<NavMeshAgent>();
        }

    }

    /// Ver CinematicSequencerBase.OnSkipCleanup(). Reproduce exactamente la misma limpieza que ya
    /// hace el callback del cierre normal (additionalOnCut de Co_EndCinematicWithTransition, al
    /// final de Co_Sequence()) — bucle de comer, locks de rotación y el plano generado en runtime
    /// son corrutinas/objetos fire-and-forget que StopCoroutine() del cierre genérico no toca. Se
    /// añade además el reset de Time.timeScale (por si el skip corta durante la cámara lenta de la
    /// Fase 6) que hoy solo vive en OnDestroy().
    protected override void OnSkipCleanup()
    {
        StopEatLoop();
        if (_estelaFacingLock != null) { StopCoroutine(_estelaFacingLock); _estelaFacingLock = null; }
        if (_liamFacingLock   != null) { StopCoroutine(_liamFacingLock);  _liamFacingLock  = null; }
        if (_liamCloseupShot  != null) { Destroy(_liamCloseupShot.gameObject); _liamCloseupShot = null; }

        // FIX (16 ago 2026 — "Coroutine couldn't be started because '_LIAM' is inactive"): el
        // callback de entrada (Co_BeginCinematicWithTransition, más abajo) desactiva el
        // GameObject de Liam incondicionalmente para que no se vea hasta su turno en la Fase 5
        // (Co_LiamApproaches), que es quien lo reactiva y lo teletransporta a _liamApproachTarget.
        // Si el jugador salta la secuencia ANTES de llegar a la Fase 5, ese SetActive(true) nunca
        // llega a ejecutarse y Liam se queda desactivado para siempre — cualquier cinemática
        // posterior que intente moverlo (ej. MountainSequencer.Co_GroupFlees vía
        // NPCBehaviourManagerV2.MoveToPosition) revienta con "Coroutine couldn't be started
        // because the game object is inactive", porque StartCoroutine no puede arrancar sobre un
        // objeto inactivo. Reproduce aquí la misma reaparición + teletransporte que hace
        // Co_LiamApproaches, para que el estado final sea el mismo que si se hubiese visto la
        // secuencia entera (si el skip llega después de la Fase 5, Liam ya está activo y esto no
        // hace nada).
        if (_liamTransform != null && !_liamTransform.gameObject.activeSelf)
        {
            _liamTransform.gameObject.SetActive(true);
            if (_liamApproachTarget != null)
            {
                if (_liamAgent != null && _liamAgent.isOnNavMesh)
                {
                    _liamAgent.Warp(_liamApproachTarget.position);
                    _liamAgent.ResetPath();
                }
                else
                {
                    _liamTransform.position = _liamApproachTarget.position;
                }
            }
        }

        if (_rageAuraVFXInstance  != null) { Destroy(_rageAuraVFXInstance);  _rageAuraVFXInstance  = null; }
        if (_lightningVFXInstance != null) { Destroy(_lightningVFXInstance); _lightningVFXInstance = null; }

        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;

        CleanupSeatsIfNeeded();
        if (_liamAgent != null && _liamAgent.isOnNavMesh && _liamTransform != null)
        {
            _liamAgent.nextPosition   = _liamTransform.position;
            _liamAgent.updatePosition = true;
            _liamAgent.updateRotation = true;
        }
        _liamSimpleAnim?.EnableAutoRotation();
        RestorePlayerPosition();

        // FIX (16 ago 2026 — "pantalla en negro tras saltar la secuencia"): el cierre NORMAL
        // (línea ~339, Co_EndCinematicWithTransition) revela la pantalla él solo cuando termina su
        // transición, después de emitir la señal durante el blackout. Pero RequestSkip() (botón
        // global de "mantener para saltar") usa el cierre genérico de la clase base
        // (Co_SkipToEnd -> Co_EndCinematicStayBlack), que deliberadamente NO revela — pensado para
        // secuencias cuyo sistema siguiente gestiona su propio reveal (p.ej. intro de un boss).
        // Aquí el sistema siguiente es el minijuego "Estela Furiosa" (StartTagMinigameNode ->
        // TagMinigameController), que no hace ningún fundido de entrada al arrancar. Sin este fade
        // manual, saltar esta secuencia deja al jugador con la pantalla en negro para siempre (solo
        // se oye la música), aunque el grafo narrativo y el minijuego sigan avanzando por debajo.
        StartCoroutine(Co_RevealAfterSkip());
    }

    private IEnumerator Co_RevealAfterSkip()
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, _skipRevealDuration, fadeIn: false);
    }

    // FIX: mismo motivo documentado en el cierre normal de Co_Sequence (ver "Sin RestoreMusic" más
    // abajo) — la música de comida debe seguir en bucle hasta que el minijuego tome el control.
    protected override bool SkipRestoresMusic => false;

    // ── Secuencia principal ───────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Test — iniciar sin señal")]
    private void TestStartDirect() => StartCoroutine(Co_Sequence());
#endif

    protected override IEnumerator Co_Sequence()
    {
        if (_willTransform != null)
        {
            _willPreSequencePosition = _willTransform.position;
            _willPreSequenceRotation = _willTransform.rotation;
        }

        // Los personajes se sientan durante el blackout de entrada; la cámara ya está en posición al revelar
        yield return Co_BeginCinematicWithTransition(_shotGroup, () =>
        {
            SeatAll();
            _seatsActive = true;
            // Liam no debe verse hasta su turno (Fase 5): ocultarlo durante el blackout
            // por si su comportamiento/posición actual lo deja a la vista en la taberna
            if (_liamTransform != null) _liamTransform.gameObject.SetActive(false);
        });
        PlaySequenceMusic();

        // Estela debe mirar al plato/mesa mientras está sentada, no al jugador
        if (_estelaFoodFacingTarget != null)
            _estelaFacingLock = StartCoroutine(Co_LockFacing(_estelaTransform, _estelaFoodFacingTarget.position));

        // ── Fase 0: El grupo discute lo ocurrido ──────────────────────────────
        yield return Co_GroupDiscussion();

        // ── Fase 1: El estómago de Estela ruge ────────────────────────────────
        yield return Co_StomachRumble();

        // ── Fase 2: Llega la comida ────────────────────────────────────────────
        yield return Co_FoodArrives();

        // ── Fase 3: Estela come de forma caótica ──────────────────────────────
        // El bucle de comer arranca aquí y sigue activo hasta la Fase 6
        yield return Co_EstelaEats();

        // ── Fase 4: Will y Eldran reaccionan ──────────────────────────────────
        yield return Co_WillEldranReact();

        // ── Fase 5: Liam se acerca e intenta hablar ───────────────────────────
        yield return Co_LiamApproaches();

        // ── Fase 6: Estela explota (sin dejar de comer) ───────────────────────
        yield return Co_EstelaExplodes();

        // ── Fase 7: Los tres se sorprenden ────────────────────────────────────
        yield return Co_Surprise();

        // ── Fase 8: ¡Corred! — transición inmediata al gameplay ───────────────
        yield return Co_Flee();

        yield return Co_EndCinematicWithTransition(() =>
        {
            // Limpiar locks de rotación antes de restaurar el control del brain
            if (_estelaFacingLock != null) { StopCoroutine(_estelaFacingLock); _estelaFacingLock = null; }
            if (_liamFacingLock   != null) { StopCoroutine(_liamFacingLock);  _liamFacingLock  = null; }
            if (_liamCloseupShot  != null) { Destroy(_liamCloseupShot.gameObject); _liamCloseupShot = null; }

            // Quitar los VFX de rabia/rayos de Estela: el prefab debería autodestruirse
            // pero si no lo hace se quedan colgando de ella durante el minijuego y la montaña
            if (_rageAuraVFXInstance  != null) { Destroy(_rageAuraVFXInstance);  _rageAuraVFXInstance  = null; }
            if (_lightningVFXInstance != null) { Destroy(_lightningVFXInstance); _lightningVFXInstance = null; }

            // Snap a idle durante el blackout: sin animación de levantarse, sincrónico para no bloquear locomoción
            CleanupSeatsIfNeeded();
            if (_liamAgent != null && _liamAgent.isOnNavMesh)
            {
                _liamAgent.nextPosition   = _liamTransform.position;
                _liamAgent.updatePosition = true;
                _liamAgent.updateRotation = true;
            }
            // Devolver a Liam el control de rotación del brain (se desactivó en Co_LiamApproaches)
            _liamSimpleAnim?.EnableAutoRotation();
            // Restaurar al jugador a donde estaba antes de la secuencia para el minijuego
            RestorePlayerPosition();
            // Sin RestoreMusic: la música de comida sigue en bucle hasta que el minijuego tome el control
            // La señal se emite durante el blackout para que el sistema siguiente prepare el gameplay antes del reveal
            RaiseSignalOut();
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 0 — El grupo discute lo ocurrido
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_GroupDiscussion()
    {
        FaceTarget(_willTransform,   _eldranTransform);
        FaceTarget(_eldranTransform, _willTransform);
        FaceTarget(_estelaTransform, _eldranTransform);

        bool done;
        _eldranEmotion?.SetEmotion(_emotionEldran01);
        // ✅ FIX: la línea es casi el doble de larga que cualquier otra de la Fase 0 (dos ideas:
        // quién invocó al demonio/golem, y por qué van a por ellos) y no cabía bien en un solo
        // bocadillo de _eldran01Duration. Se pagina en dos con '\n' en la clave de localización,
        // igual que ya se hace con EVT_TAB_LIAM_01. Sin animTrigger: Eldran está sentado y los
        // gestos sociales son a cuerpo completo de pie (PlaySocialGesture en layer 0), lo
        // levantarían de la silla
        yield return ShowBubblePaged(_eldranTransform, Loc(_keyEldran01), _eldran01Duration);

        _cinematicCamera.Cut(_shotWillEldran);
        _willEmotion?.SetEmotion(_emotionWill01);
        done = false;
        SpeechBubbleUI.Instance.Show(_willTransform, Loc(_keyWill01),
            duration: _will01Duration, onComplete: () => done = true,
            animTrigger: _animWill01);
        yield return new WaitUntil(() => done);

        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(_emotionEstela01);
        done = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_keyEstela01),
            duration: _estela01Duration, onComplete: () => done = true,
            animTrigger: _animEstela01);
        yield return new WaitUntil(() => done);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 1 — El estómago de Estela ruge
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_StomachRumble()
    {
        // Cámara ya en _shotEstela desde la fase anterior
        FeedbackService.CameraShake(0.15f, 0.5f);
        AudioService.Instance?.PlaySFX("Taberna_StomachRumble", 1f, _estelaTransform.position);
        yield return new WaitForSeconds(0.3f);

        _estelaEmotion?.SetEmotion(_emotionEstela02);
        bool done = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_keyEstela02),
            duration: _estela02Duration, onComplete: () => done = true);
        yield return new WaitUntil(() => done);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 2 — Llega la comida
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_FoodArrives()
    {
        // Fade a negro → activar platos → revelar → Estela dice "POR FIN"
        yield return FeedbackService.ScreenFadeAsync(Color.black, _foodFadeDuration, fadeIn: true);
        foreach (var obj in _foodObjects)
            if (obj != null) obj.SetActive(true);
        AudioService.Instance?.PlaySFX("Taberna_FoodArrives", 1f, _estelaTransform.position);
        yield return FeedbackService.ScreenFadeAsync(Color.black, _foodFadeDuration, fadeIn: false);

        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(_emotionEstela03);

        if (!string.IsNullOrEmpty(_comidaMusicId))
            PlaySequenceMusic(_comidaMusicId);

        bool done = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_keyEstela03),
            duration: _estela03Duration, onComplete: () => done = true,
            animTrigger: _animEstela03);
        yield return new WaitUntil(() => done);

        yield return new WaitForSeconds(_foodHold);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 3 — Estela come de forma caótica
    // El bucle de comer se inicia aquí y sigue activo hasta Co_EstelaExplodes
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_EstelaEats()
    {
        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(NPCEmotion.Happy);

        // Will y Eldran reaccionan en cuanto Estela se pone a comer: la cara cambia
        // ya (aunque estén fuera de plano); sus frases y gestos llegan en la Fase 4
        _willEmotion?.SetEmotion(_emotionWill02);
        _eldranEmotion?.SetEmotion(_emotionEldran02);

        _eatLoopCoroutine = StartCoroutine(Co_EatLoop());
        yield return new WaitForSeconds(_eatHold);
    }

    private IEnumerator Co_EatLoop()
    {
        if (_estelaAnimator != null) _estelaAnimator.speed = _eatSpeedMultiplier;
        while (true)
        {
            _estelaSimpleAnim?.PlaySocialGesture(_animEstelaEat);
            AudioService.Instance?.PlaySFX("Taberna_EatLoop", 0.8f, _estelaTransform.position);
            yield return new WaitForSeconds(_eatRepeatInterval);
        }
    }

    private void StopEatLoop()
    {
        if (_eatLoopCoroutine != null)
        {
            StopCoroutine(_eatLoopCoroutine);
            _eatLoopCoroutine = null;
        }
        if (_estelaAnimator != null) _estelaAnimator.speed = 1f;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 4 — Will y Eldran reaccionan
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WillEldranReact()
    {
        // Las emociones ya se aplicaron en la Fase 3 al empezar a comer Estela;
        // aquí solo dicen sus frases con el gesto correspondiente
        _cinematicCamera.Cut(_shotWillEldran);

        bool done = false;
        SpeechBubbleUI.Instance.Show(_willTransform, Loc(_keyWill02),
            duration: _will02Duration, onComplete: () => done = true,
            animTrigger: _animWill02);
        yield return new WaitUntil(() => done);

        done = false;
        SpeechBubbleUI.Instance.Show(_eldranTransform, Loc(_keyEldran02),
            duration: _eldran02Duration, onComplete: () => done = true,
            animTrigger: _animEldran02);
        yield return new WaitUntil(() => done);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 5 — Liam se acerca e intenta hablar
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_LiamApproaches()
    {
        // Reaparecer a Liam (oculto desde el inicio de la secuencia) justo antes de su plano
        if (_liamTransform != null && !_liamTransform.gameObject.activeSelf)
            _liamTransform.gameObject.SetActive(true);

        // Teletransportar a Liam a la taberna antes de que la cámara lo muestre
        if (_liamTransform != null && _liamApproachTarget != null)
        {
            if (_liamAgent != null && _liamAgent.isOnNavMesh)
            {
                _liamAgent.Warp(_liamApproachTarget.position);
                _liamAgent.ResetPath();
                // Bloquear posición y rotación para que el brain no mueva ni rote a Liam
                _liamAgent.updatePosition = false;
                _liamAgent.updateRotation = false;
            }
            else
                _liamTransform.position = _liamApproachTarget.position;
        }

        _cinematicCamera.Cut(_shotLiam);
        Vector3 groupCenter = (_willTransform.position + _eldranTransform.position + _estelaTransform.position) / 3f;

        // Desactivar la rotación automática del brain para que no se pelee con el lock
        // (mismo conflicto que con los NPCs sentados: ApplySmoothRotation vs Co_LockFacing → temblor)
        _liamSimpleAnim?.DisableAutoRotation();
        // Mantener a Liam mirando al grupo hasta el final de la secuencia (el lock se para en el cleanup)
        _liamFacingLock = StartCoroutine(Co_LockFacing(_liamTransform, groupCenter));

        _liamEmotion?.SetEmotion(_emotionLiam01);

        // El texto se divide en páginas por '\n' en la clave de localización
        string[] pages = Loc(_keyLiam01).Split('\n');

        // Primera frase: desde lejos, en el plano _shotLiam
        bool done = false;
        SpeechBubbleUI.Instance.Show(_liamTransform, pages[0].Trim(),
            duration: _liam01Duration, onComplete: () => done = true,
            animTrigger: _animLiam01);
        yield return new WaitUntil(() => done);

        // Resto de frases: la misma cámara se acerca a primer plano mientras habla
        if (pages.Length > 1)
        {
            BuildLiamCloseupShot();
            if (_liamCloseupShot != null)
                _cinematicCamera.MoveTo(_liamCloseupShot, _liamCloseupBlendTime);

            for (int i = 1; i < pages.Length; i++)
            {
                string page = pages[i].Trim();
                if (string.IsNullOrEmpty(page)) continue;
                done = false;
                SpeechBubbleUI.Instance.Show(_liamTransform, page,
                    duration: _liam01Duration, onComplete: () => done = true);
                yield return new WaitUntil(() => done);
            }
        }
    }

    /// Genera el primer plano DELANTE de Liam usando su forward (está mirando al
    /// grupo por el facing lock), no el eje de _shotLiam: ese plano es cenital y
    /// acercarse por su eje acababa enfocando el cogote. _liamCloseupYawOffset
    /// permite angular el plano para un 3/4 en lugar de un frontal puro.
    private void BuildLiamCloseupShot()
    {
        if (_liamTransform == null) return;

        Vector3 fwd = _liamTransform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return;
        fwd.Normalize();
        if (_liamCloseupYawOffset != 0f)
            fwd = Quaternion.AngleAxis(_liamCloseupYawOffset, Vector3.up) * fwd;

        Vector3 lookPoint = _liamTransform.position + Vector3.up * _liamCloseupLookHeight;

        if (_liamCloseupShot == null)
            _liamCloseupShot = new GameObject("TabernaSequencer_LiamCloseupShot").transform;

        _liamCloseupShot.SetPositionAndRotation(
            lookPoint + fwd * _liamCloseupDistance,
            Quaternion.LookRotation(-fwd));
    }

    private IEnumerator Co_LockFacing(Transform t, Vector3 worldPos)
    {
        while (true)
        {
            FaceTarget(t, worldPos);
            yield return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 6 — Estela explota sin dejar de comer
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_EstelaExplodes()
    {
        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(_emotionEstelaRage);

        // Sin animTrigger: el bucle de comer sigue activo mientras dice la frase
        bool done = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_keyEstelaRage),
            duration: _rageDuration, onComplete: () => done = true);
        yield return new WaitUntil(() => done);

        // Para de comer y libera el lock antes del primer plano
        StopEatLoop();
        if (_estelaFacingLock != null) { StopCoroutine(_estelaFacingLock); _estelaFacingLock = null; }

        // ── Primer plano con slow motion ──────────────────────────────────────
        if (_estelaRageAuraVFX != null)
            _rageAuraVFXInstance = Instantiate(_estelaRageAuraVFX, _estelaTransform.position, _estelaTransform.rotation, _estelaTransform);
        AudioService.Instance?.PlaySFX("Taberna_RageAura", 1f, _estelaTransform.position);

        Time.timeScale      = _slowMotionScale;
        Time.fixedDeltaTime = 0.02f * _slowMotionScale;

        _estelaSimpleAnim?.PlaySocialGesture(_animEstelaRage);
        if (_shotEstelaCloseup != null) _cinematicCamera.Cut(_shotEstelaCloseup);

        done = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_keyEstelaSeVaEnterar),
            duration: _seVaEnterarDuration, onComplete: () => done = true);
        yield return new WaitUntil(() => done);

        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
        // ─────────────────────────────────────────────────────────────────────

        yield return new WaitForSeconds(_aimDelay);
        FaceTarget(_estelaTransform, _liamTransform);

        if (_lightningVFX != null)
            _lightningVFXInstance = Instantiate(_lightningVFX, _estelaTransform.position, _estelaTransform.rotation, _estelaTransform);
        _liamSimpleAnim?.PlaySocialGesture(_animLiamDodge);
        FeedbackService.ScreenFlash(new Color(0.8f, 0.9f, 1f, 0.5f), 0.5f);
        FeedbackService.CameraShake(0.3f, 0.6f);
        AudioService.Instance?.PlaySFX("Taberna_Lightning", 1f, _estelaTransform.position);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 7 — Sorpresa
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_Surprise()
    {
        _cinematicCamera.Cut(_shotWillEldran);
        _willEmotion?.SetEmotion(_emotionSurprise);
        _eldranEmotion?.SetEmotion(_emotionSurprise);
        _liamEmotion?.SetEmotion(_emotionSurprise);

        bool done = false;
        SpeechBubbleUI.Instance.Show(_willTransform, Loc(_keyWill03),
            duration: _will03Duration, onComplete: () => done = true,
            animTrigger: _animWill03);
        yield return new WaitUntil(() => done);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 8 — ¡Corred!
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_Flee()
    {
        // Eldran dice su línea; el blackout de salida se encarga del resto
        _cinematicCamera.Cut(_shotWide);
        _eldranEmotion?.SetEmotion(_emotionEldranFlee);

        bool done = false;
        SpeechBubbleUI.Instance.Show(_eldranTransform, Loc(_keyEldranFlee),
            duration: _fleeDuration, onComplete: () => done = true,
            animTrigger: _animEldranFlee);
        yield return new WaitUntil(() => done);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers — posición del jugador
    // ══════════════════════════════════════════════════════════════════════════

    private void RestorePlayerPosition()
    {
        if (_willTransform == null) return;
        if (_willCharController != null) _willCharController.enabled = false;
        _willTransform.SetPositionAndRotation(_willPreSequencePosition, _willPreSequenceRotation);
        if (_willCharController != null) _willCharController.enabled = true;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers — sentado
    // ══════════════════════════════════════════════════════════════════════════

    private void SeatAll()
    {
        // ✅ FIX: cada NPC se sienta en su propio try/catch. Antes, si SeatNPC(Eldran) lanzaba
        // una excepción a mitad (p.ej. dentro de PlayAmbientActivity → AttachPropToOccupant →
        // GetBoneTransform, o cualquier otro punto tras posicionarlo), la excepción cortaba
        // SeatAll() entero y SeatNPC(Estela) — que va DESPUÉS en esta lista — nunca llegaba a
        // ejecutarse. Eldran se veía sentado con normalidad (ya estaba posicionado antes del
        // fallo) mientras Estela se quedaba de pie sin ningún log que lo explicara, porque su
        // llamada ni siquiera arrancaba. Aislar cada NPC evita que un fallo en uno tumbe a los demás.
        SeatPlayer(_willSeat);
        TrySeatNPC(_eldranTransform, _eldranSeat, _eldranSimpleAnim, _eldranAgent, _eldranBehaviour);
        TrySeatNPC(_estelaTransform, _estelaSeat, _estelaSimpleAnim, _estelaAgent, _estelaBehaviour);
    }

    private void TrySeatNPC(Transform npc, NPCWorldPoint seat, NPCSimpleAnimator simAnim, NavMeshAgent agent, NPCBehaviourManagerV2 behaviour)
    {
        try
        {
            SeatNPC(npc, seat, simAnim, agent, behaviour);
        }
        catch (System.Exception e)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string npcName = npc != null ? npc.name : "NULL";
            Debug.LogError($"[TabernaSequencer] ❌ SeatNPC('{npcName}') lanzó una excepción y se abortó a " +
                $"mitad — el NPC puede haber quedado a medio sentar (posición sin animación, o FSM " +
                $"pausada sin reanudar). Excepción: {e}");
#endif
        }
    }

    private void SeatPlayer(NPCWorldPoint seat)
    {
        if (seat == null || _willActivityHandler == null || _willTransform == null) return;
        if (seat.TryOccupy(_willTransform))
            _willActivityHandler.StartActivity(seat);
    }

    private void SeatNPC(Transform npc, NPCWorldPoint seat, NPCSimpleAnimator simAnim, NavMeshAgent agent, NPCBehaviourManagerV2 behaviour)
    {
        // ✅ DIAGNÓSTICO TEMPORAL: el bug "Estela no se sienta" ha sobrevivido a dos rondas de
        // fix a nivel de código (limpieza de asiento en interrupciones + normalización de
        // IsGrounded/isFlying) sin resolverse, y no tengo forma de inspeccionar el wiring del
        // Inspector de Cinematic_Taberna.unity desde aquí. Estos logs identifican en qué punto
        // exacto falla SeatNPC() para cada NPC — quitar una vez encontrada la causa real.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string npcName = npc != null ? npc.name : "NULL";
        if (seat == null)
        {
            Debug.LogWarning($"[TabernaSequencer] ⚠️ SeatNPC('{npcName}'): el campo _*Seat no tiene " +
                "asignado ningún NPCWorldPoint en el Inspector de Cinematic_Taberna.unity. El NPC no se moverá.");
            return;
        }
        if (npc == null)
        {
            Debug.LogWarning($"[TabernaSequencer] ⚠️ SeatNPC: la referencia al Transform del NPC es null " +
                $"(seat destino: '{seat.name}'). Revisar _eldranTransform/_estelaTransform en el Inspector.");
            return;
        }
        if (!seat.TryOccupy(npc))
        {
            Debug.LogWarning($"[TabernaSequencer] ⚠️ SeatNPC('{npcName}'): seat.TryOccupy() devolvió false — " +
                $"el NPCWorldPoint '{seat.name}' (activityType={seat.activityType}) ya está marcado como ocupado " +
                "(IsOccupied=true) por otro transform. Puede ser: (a) el mismo NPCWorldPoint asignado por error " +
                "a dos personajes distintos, o (b) quedó 'ocupado' de una sesión de Play anterior que no liberó " +
                "el asiento (Reload Domain desactivado). El NPC NO se sentará.");
            return;
        }
        Debug.Log($"[TabernaSequencer] ✅ SeatNPC('{npcName}'): asiento '{seat.name}' ocupado correctamente. " +
            $"Posición destino: {seat.InteractionPosition}, activityType: {seat.activityType}.");
#else
        if (seat == null || npc == null) return;
        if (!seat.TryOccupy(npc)) return;
#endif

        // ✅ FIX: Pausar la FSM (NPCBehaviourManagerV2) ANTES de fijar la posición de asiento.
        // Eldran/Estela están en el party y su brain sigue en FollowPlayerState mientras
        // dura toda la secuencia de la taberna. Ese estado detecta cada frame que la
        // distancia a Will supera "distanciaParaPararse * 1.2" (normal en una mesa con
        // sillas separadas), reactiva agent.updatePosition y fija un nuevo destino hacia
        // el jugador — deshaciendo el "sentado" casi de inmediato (el NPC no llega a
        // sentarse o se levanta solo). ForceIdle() antes de deshabilitar garantiza una
        // salida limpia de FollowPlayerState (su OnExit ya restaura updatePosition=true
        // correctamente); deshabilitar el componente evita que la FSM vuelva a tomar el
        // control del NavMeshAgent mientras el NPC esté sentado.
        if (behaviour != null && behaviour.enabled)
        {
            behaviour.ForceIdle();
            behaviour.enabled = false;
        }

        npc.position = seat.InteractionPosition;
        if (seat.overrideFacing)
            npc.rotation = seat.InteractionRotation;

        if (agent != null && agent.enabled)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.nextPosition   = seat.InteractionPosition;
        }

        // El brain sigue en FollowPlayerState durante la secuencia y su ApplySmoothRotation
        // (LateUpdate) gira al NPC hacia el jugador cada frame, peleándose con los
        // Co_LockFacing del sequencer → temblor visible. Desactivar mientras esté sentado.
        simAnim?.DisableAutoRotation();
        simAnim?.PlayAmbientActivity(seat.activityType, seat);
    }

    // Libera el asiento con snap instantáneo a idle, sin animación de levantarse.
    // Se usa en el blackout de salida para que el jugador no vea a los personajes levantándose.
    private void UnseatNPCSnap(Transform npc, NPCWorldPoint seat, NavMeshAgent agent, NPCSimpleAnimator simAnim, NPCBehaviourManagerV2 behaviour)
    {
        if (seat == null || npc == null) return;
        seat.DetachProp();
        seat.Release(npc);
        // Reactivar la rotación automática del brain (EnableAutoRotation sincroniza
        // _targetRotation con la rotación actual, así no hay snap visible)
        simAnim?.EnableAutoRotation();
        simAnim?.PlayIdleNormal();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.nextPosition   = npc.position;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }

        // ✅ FIX: Rehabilitar la FSM que se pausó en SeatNPC. ForceIdle() lo deja en un
        // estado Idle limpio; NPCPartyMember.Update() (chequeo cada 0.5s) detectará que
        // está en Idle, en party y sin combate/cinemática activos, y llamará a
        // StartFollowing() automáticamente para que retome el seguimiento normal.
        if (behaviour != null && !behaviour.enabled)
        {
            behaviour.enabled = true;
            behaviour.ForceIdle();
        }
    }

    // Libera los asientos de Eldran/Estela y reactiva su FSM. Idempotente: no hace
    // nada si ya se liberaron (_seatsActive en false).
    //
    // ✅ FIX: red de seguridad ante interrupción anómala de la secuencia (excepción,
    // StopAllRunners en test mode al recargar el preset — Regla 2 de CLAUDE.md—,
    // descarga de la escena aditiva a mitad de reproducción, etc.). Antes de este
    // fix, el bloque que libera el asiento y reactiva NPCBehaviourManagerV2 solo
    // vivía dentro del callback de Co_EndCinematicWithTransition, que únicamente se
    // ejecuta si Co_Sequence() llega hasta el final. Si algo cortaba la secuencia
    // antes, el NPCWorldPoint de Estela se quedaba con _isOccupied=true para
    // siempre (o hasta un domain reload) y su NPCBehaviourManagerV2 deshabilitado.
    // El siguiente intento de reproducir la taberna llamaba a SeatNPC() →
    // seat.TryOccupy() devolvía false en silencio → Estela nunca se sentaba. Esto
    // explica por qué la incidencia "Estela no está sentada en su sitio" se repetía
    // en cada sesión de testeo que interrumpía la cinemática a medias.
    private void CleanupSeatsIfNeeded()
    {
        if (!_seatsActive) return;
        _seatsActive = false;

        _willActivityHandler?.ForceStopActivityImmediate();
        UnseatNPCSnap(_eldranTransform, _eldranSeat, _eldranAgent, _eldranSimpleAnim, _eldranBehaviour);
        UnseatNPCSnap(_estelaTransform, _estelaSeat, _estelaAgent, _estelaSimpleAnim, _estelaBehaviour);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
        CleanupSeatsIfNeeded();
    }

}
