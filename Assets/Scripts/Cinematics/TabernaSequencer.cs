using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Sendero.Core.Feedback;

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
    [SerializeField] private float _liamWalkSpeed = 2f;

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

    [Header("Huida")]
    [SerializeField] private float _fleeSpeed   = 5f;
    [SerializeField] private float _fleeTimeout = 3.5f;

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
    [SerializeField] private string     _animEldran01     = "Thinking01";
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
    [SerializeField] private string     _keyLiam01      = "EVT_TAB_LIAM_01";
    [SerializeField] private NPCEmotion _emotionLiam01  = NPCEmotion.Thinking;
    [SerializeField] private string     _animLiam01     = "Thinking01";
    [SerializeField] private float      _liam01Duration = 3.5f;

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
    private Coroutine            _eatLoopCoroutine;
    private Coroutine            _estelaFacingLock;
    private Coroutine            _liamFacingLock;

    private CharacterController _willCharController;
    private Vector3    _willPreSequencePosition;
    private Quaternion _willPreSequenceRotation;

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
        }
        if (_estelaTransform != null)
        {
            _estelaEmotion    = _estelaTransform.GetComponentInChildren<NPCEmotionController>();
            _estelaSimpleAnim = _estelaTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _estelaAnimator   = _estelaTransform.GetComponentInChildren<Animator>();
            _estelaAgent      = _estelaTransform.GetComponent<NavMeshAgent>();
        }
        if (_liamTransform != null)
        {
            _liamEmotion    = _liamTransform.GetComponentInChildren<NPCEmotionController>();
            _liamSimpleAnim = _liamTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _liamAgent      = _liamTransform.GetComponent<NavMeshAgent>();
        }

    }

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
        yield return Co_BeginCinematicWithTransition(_shotGroup, SeatAll);
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

            // Snap a idle durante el blackout: sin animación de levantarse, sincrónico para no bloquear locomoción
            _willActivityHandler?.ForceStopActivityImmediate();
            UnseatNPCSnap(_eldranTransform, _eldranSeat, _eldranAgent, _eldranSimpleAnim);
            UnseatNPCSnap(_estelaTransform, _estelaSeat, _estelaAgent, _estelaSimpleAnim);
            if (_liamAgent != null && _liamAgent.isOnNavMesh)
            {
                _liamAgent.nextPosition   = _liamTransform.position;
                _liamAgent.updatePosition = true;
                _liamAgent.updateRotation = true;
            }
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

        _eldranEmotion?.SetEmotion(_emotionEldran01);
        bool done = false;
        SpeechBubbleUI.Instance.Show(_eldranTransform, Loc(_keyEldran01),
            duration: _eldran01Duration, onComplete: () => done = true,
            animTrigger: _animEldran01);
        yield return new WaitUntil(() => done);

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
        _eatLoopCoroutine = StartCoroutine(Co_EatLoop());
        yield return new WaitForSeconds(_eatHold);
    }

    private IEnumerator Co_EatLoop()
    {
        if (_estelaAnimator != null) _estelaAnimator.speed = _eatSpeedMultiplier;
        while (true)
        {
            _estelaSimpleAnim?.PlaySocialGesture(_animEstelaEat);
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
        _cinematicCamera.Cut(_shotWillEldran);
        _willEmotion?.SetEmotion(_emotionWill02);

        bool done = false;
        SpeechBubbleUI.Instance.Show(_willTransform, Loc(_keyWill02),
            duration: _will02Duration, onComplete: () => done = true,
            animTrigger: _animWill02);
        yield return new WaitUntil(() => done);

        _eldranEmotion?.SetEmotion(_emotionEldran02);
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

        // Mantener a Liam mirando al grupo hasta el final de la secuencia (el lock se para en el cleanup)
        _liamFacingLock = StartCoroutine(Co_LockFacing(_liamTransform, groupCenter));

        _liamEmotion?.SetEmotion(_emotionLiam01);
        bool done = false;
        SpeechBubbleUI.Instance.Show(_liamTransform, Loc(_keyLiam01),
            duration: _liam01Duration, onComplete: () => done = true,
            animTrigger: _animLiam01);
        yield return new WaitUntil(() => done);
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
            Instantiate(_estelaRageAuraVFX, _estelaTransform.position, _estelaTransform.rotation, _estelaTransform);

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
            Instantiate(_lightningVFX, _estelaTransform.position, _estelaTransform.rotation, _estelaTransform);
        _liamSimpleAnim?.PlaySocialGesture(_animLiamDodge);
        FeedbackService.ScreenFlash(new Color(0.8f, 0.9f, 1f, 0.5f), 0.5f);
        FeedbackService.CameraShake(0.3f, 0.6f);
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
        SeatPlayer(_willSeat);
        SeatNPC(_eldranTransform, _eldranSeat, _eldranSimpleAnim, _eldranAgent);
        SeatNPC(_estelaTransform, _estelaSeat, _estelaSimpleAnim, _estelaAgent);
    }

    private void SeatPlayer(NPCWorldPoint seat)
    {
        if (seat == null || _willActivityHandler == null || _willTransform == null) return;
        if (seat.TryOccupy(_willTransform))
            _willActivityHandler.StartActivity(seat);
    }

    private void SeatNPC(Transform npc, NPCWorldPoint seat, NPCSimpleAnimator simAnim, NavMeshAgent agent)
    {
        if (seat == null || npc == null) return;
        if (!seat.TryOccupy(npc)) return;

        npc.position = seat.InteractionPosition;
        if (seat.overrideFacing)
            npc.rotation = seat.InteractionRotation;

        if (agent != null && agent.enabled)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.nextPosition   = seat.InteractionPosition;
        }

        simAnim?.PlayAmbientActivity(seat.activityType, seat);
    }

    // Libera el asiento con snap instantáneo a idle, sin animación de levantarse.
    // Se usa en el blackout de salida para que el jugador no vea a los personajes levantándose.
    private void UnseatNPCSnap(Transform npc, NPCWorldPoint seat, NavMeshAgent agent, NPCSimpleAnimator simAnim)
    {
        if (seat == null || npc == null) return;
        seat.DetachProp();
        seat.Release(npc);
        simAnim?.PlayIdleNormal();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.nextPosition   = npc.position;
            agent.updatePosition = true;
            agent.updateRotation = true;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

}
