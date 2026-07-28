using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia completa del primer encuentro con Estela:
///   1. Guerrero ordena a las arañas atacar.
///   2. Estela elimina las arañas.
///   3. Los guerreros la amenazan y le piden todo lo que lleva.
///   4. Estela hace el numerito de princesita asustada.
///   5. Los guerreros se burlan ("princefea"). Estela pierde los papeles.
///   6. Ráfaga de proyectiles. Los guerreros salen por patas.
///   7. Will aplaude. Estela hace la reverencia. "¿Eso era todo?"
/// Señal de entrada: "ESTELA_APPEARS_START".
/// Señal de salida:  "ESTELA_APPEARS_DONE".
[DisallowMultipleComponent]
public class EstelaAppearsSequencer : CinematicSequencerBase
{
    [Header("Personaje — Estela")]
    [SerializeField] private Transform _estelaTransform;
    [Tooltip("Punto de lanzamiento del hechizo (hueso de la mano o equivalente)")]
    [SerializeField] private Transform _estelaSpawnPoint;
    [Tooltip("Hechizo que Estela lanza (MagicSpellSO)")]
    [SerializeField] private MagicSpellSO _bolaFuego;

    [Header("Arañas — en orden de muerte")]
    [Tooltip("Transform de cada araña (posición objetivo y VFX de impacto)")]
    [SerializeField] private Transform[] _spiderTargets;
    [Tooltip("GameObject de cada araña (se desactiva al morir)")]
    [SerializeField] private GameObject[] _spiderObjects;

    [Header("VFX de impacto en araña (opcional)")]
    [SerializeField] private GameObject _spiderImpactVFX;
    [SerializeField] private float _vfxLifetime = 2f;

    [Header("Guerreros — los dos antagonistas")]
    [SerializeField] private Transform _warrior1Transform;
    [SerializeField] private Transform _warrior2Transform;
    [Tooltip("Punto al que huye el guerrero 1 al salir por patas")]
    [SerializeField] private Transform _warrior1FleeTarget;
    [Tooltip("Punto al que huye el guerrero 2 al salir por patas")]
    [SerializeField] private Transform _warrior2FleeTarget;
    [SerializeField] private float _fleeSpeed   = 5f;
    [SerializeField] private float _fleeTimeout = 3.5f;

    [Header("Cámara — planos")]
    [Tooltip("Plano wide del bosque para la entrada")]
    [SerializeField] private Transform _shotWide;
    [Tooltip("Plano medio de Estela")]
    [SerializeField] private Transform _shotEstela;
    [Tooltip("Plano de los dos guerreros juntos")]
    [SerializeField] private Transform _shotWarriors;
    [Tooltip("Plano de cada araña (se corta a él justo después de que Estela dispara)")]
    [SerializeField] private Transform[] _shotPerSpider;

    // ── Fase 0: Orden de ataque ───────────────────────────────────────────────

    [Header("Fase 0 — Guerrero ordena el ataque")]
    [Tooltip("Ej: EVT_W1_ARANAS_ATACAD — '¡Arañas, atacad!'")]
    [SerializeField] private string _attackOrderKey = "EVT_W1_ARANAS_ATACAD";
    [SerializeField] private NPCEmotion _attackOrderEmotion = NPCEmotion.Angry;
    [SerializeField] private string _attackOrderAnim;
    [SerializeField] private float _attackOrderDuration = 2f;

    // ── Fase 1: Arañas ────────────────────────────────────────────────────────

    [Header("Fase 1 — Frases de Estela al matar arañas (una por araña)")]
    [Tooltip("Claves de localización. Se usa el texto raw si LocalizationManager no está disponible.")]
    [SerializeField] private string[] _killLineKeys;
    [SerializeField] private NPCEmotion[] _killEmotions;
    [SerializeField] private string[] _killAnims;
    [SerializeField] private float _killLineDuration = 2.2f;

    // ── Fase 2: Guerreros fanfarronean ────────────────────────────────────────

    [Header("Fase 2 — Guerreros fanfarronean")]
    [Tooltip("Ej: EVT_W1_PANCOMI — '¿Pan comido? A ver si te resulta tan fácil nuestra técnica'")]
    [SerializeField] private string _w1TauntKey = "EVT_W1_PANCOMI";
    [SerializeField] private NPCEmotion _w1TauntEmotion = NPCEmotion.Happy;
    [SerializeField] private string _w1TauntAnim;
    [SerializeField] private float _w1TauntDuration = 3.5f;

    [Tooltip("Ej: EVT_W2_AMENAZA — 'Danos todo lo que llevas y no te haremos daño'")]
    [SerializeField] private string _w2TauntKey = "EVT_W2_AMENAZA";
    [SerializeField] private NPCEmotion _w2TauntEmotion = NPCEmotion.Angry;
    [SerializeField] private string _w2TauntAnim;
    [SerializeField] private float _w2TauntDuration = 3.5f;

    // ── Fase 3: Estela actúa ──────────────────────────────────────────────────

    [Header("Fase 3 — Estela hace el numerito de princesita")]
    [Tooltip("Ej: EVT_ESTELA_DRAMATIC — 'Oh nooo, que alguien me ayude...'")]
    [SerializeField] private string _dramaticLineKey = "EVT_ESTELA_DRAMATIC";
    [SerializeField] private NPCEmotion _dramaticEmotion = NPCEmotion.Scared;
    [SerializeField] private string _dramaticAnim;
    [SerializeField] private float _dramaticLineDuration = 5.5f;

    // ── Fase 4: El insulto ────────────────────────────────────────────────────

    [Header("Fase 4 — El insulto y la rabia")]
    [Tooltip("Ej: EVT_W1_PRINCEFEA — '¿Princesita? Dirás princefea, ¿no?'")]
    [SerializeField] private string _insultLineKey = "EVT_W1_PRINCEFEA";
    [SerializeField] private NPCEmotion _insultEmotion = NPCEmotion.Happy;
    [SerializeField] private string _insultAnim;
    [SerializeField] private float _insultLineDuration = 2.5f;
    [Tooltip("Pausa de risa de ambos guerreros antes de que Estela explote")]
    [SerializeField] private float _laughPause = 1.2f;

    [Tooltip("Emoción de Estela al explotar de rabia")]
    [SerializeField] private NPCEmotion _rageEmotion = NPCEmotion.Angry;
    [SerializeField] private string _rageAnim;
    [Tooltip("Número total de proyectiles de la ráfaga (alternando entre guerrero 1 y 2)")]
    [SerializeField] private int _rageShots = 6;
    [SerializeField] private float _rageShotInterval = 0.2f;

    [Tooltip("Prefab de VFX de escudo/protección que aparece sobre el guerrero al recibir cada disparo de la ráfaga")]
    [SerializeField] private GameObject _warriorShieldVFX;
    [SerializeField] private float _warriorShieldVfxLifetime = 0.6f;
    [Tooltip("Offset del VFX de escudo respecto a la posición del guerrero (altura aprox. del pecho)")]
    [SerializeField] private Vector3 _warriorShieldVfxOffset = new Vector3(0f, 0.4f, 0f);

    // ── Fase 5: Will aplaude + reverencia de Estela ───────────────────────────

    [Header("Fase 5 — Will aplaude y Estela hace la reverencia")]
    [Tooltip("Transform raíz de Will. Si se deja vacío se busca automáticamente por PlayerService.")]
    [SerializeField] private Transform _willTransform;
    [Tooltip("Ej: EVT_WILL_MADREMIA — 'Madre mía...'")]
    [SerializeField] private string _willLineKey = "EVT_WILL_MADREMIA";
    [SerializeField] private NPCEmotion _willEmotion = NPCEmotion.Surprised;
    [SerializeField] private string _willAnim;
    [SerializeField] private float _willLineDuration = 2.5f;
    [Tooltip("Gesto de reverencia para Estela (NPCSimpleAnimator.PlaySocialGesture)")]
    [SerializeField] private string _bowAnim = "Reverence01";
    [SerializeField] private float _bowDuration = 2.5f;

    // ── Fase 6: Victoria ──────────────────────────────────────────────────────

    [Header("Fase 6 — Estela, línea final")]
    [Tooltip("Ej: EVT_ESTELA_ESO_ERA — '...¿Eso era todo?'")]
    [SerializeField] private string _victoryLineKey = "EVT_ESTELA_ESO_ERA";
    [SerializeField] private NPCEmotion _victoryEmotion = NPCEmotion.Smirk;
    [SerializeField] private string _victoryAnim;
    [SerializeField] private float _victoryLineDuration = 3f;

    // ── Timings generales ─────────────────────────────────────────────────────

    [Header("Timings")]
    [Tooltip("Pausa desde que Estela se gira hasta que dispara (arañas)")]
    [SerializeField] private float _aimDelay             = 0.4f;
    [Tooltip("Delay desde el disparo hasta que aparece el bocadillo")]
    [SerializeField] private float _lineDelay            = 0.3f;
    [Tooltip("Tiempo de vuelo del proyectil hasta que la araña muere")]
    [SerializeField] private float _projectileFlightTime = 0.6f;
    [SerializeField] private float _betweenSpidersDelay  = 0.5f;
    [Tooltip("Pausa entre las arañas y la entrada de los guerreros")]
    [SerializeField] private float _pauseBeforeWarriors  = 0.8f;
    [SerializeField] private float _holdAfterVictory     = 1.2f;

    // ── Cache privado ─────────────────────────────────────────────────────────

    private NPCEmotionController _estelaEmotion;
    private NPCSimpleAnimator    _estelaSimpleAnim;
    private NavMeshAgent         _estelaAgent;
    private NPCEmotionController _warrior1Emotion;
    private NPCEmotionController _warrior2Emotion;
    private NPCSimpleAnimator    _warrior1SimpleAnim;
    private NPCSimpleAnimator    _warrior2SimpleAnim;
    private NavMeshAgent         _warrior1Agent;
    private NavMeshAgent         _warrior2Agent;
    private Game.NPC.NPCBehaviourManagerV2 _warrior1Npc;
    private Game.NPC.NPCBehaviourManagerV2 _warrior2Npc;
    private ObstacleAvoidanceType _warrior1OriginalAvoidance;
    private ObstacleAvoidanceType _warrior2OriginalAvoidance;
    private int _enemyHitLayers;
    private int _enemyCollisionLayers;

    // ── Posiciones/rotaciones "de diseño" (las colocadas a mano en el editor) ──
    // Igual que en LiamCrystalBallSequencer._liamDesignPosition: si Estela o los guerreros
    // tienen NavMeshAgent y el punto exacto donde están colocados no cae sobre el NavMesh
    // baked, Unity los desplaza al punto válido más cercano en cuanto el agente se activa —
    // a nivel de motor, sin pasar por ningún script nuestro. Los planos de cámara de esta
    // secuencia (_shotEstela sobre todo, son primeros planos/medios muy ajustados) están
    // aimados a mano contra la posición exacta que se ve en el editor; si el NPC se corrige
    // aunque sea unos centímetros al arrancar la partida, el encuadre deja de coincidir con
    // lo que se ve en el editor (el "Live Preview" de CinematicShot sí engaña, porque samplea
    // en Editor antes de que el agente llegue a corregir nada). Ver Co_RestoreDesignTransforms().
    private Vector3    _estelaDesignPosition;
    private Quaternion _estelaDesignRotation;
    private Vector3    _warrior1DesignPosition;
    private Quaternion _warrior1DesignRotation;
    private Vector3    _warrior2DesignPosition;
    private Quaternion _warrior2DesignRotation;

    // ── Persistencia de "ya jugada" ──────────────────────────────────────────
    // Flag para saber si esta secuencia ya se reprodujo en esta partida. Sin esto, al cargar
    // una partida guardada cerca de este punto (p.ej. tras morir después de la secuencia),
    // las arañas y los dos guerreros volvían a aparecer: la secuencia solo los desactiva en
    // memoria (SetActive/gameObject.SetActive), y ese estado no se guarda ni se re-aplica al
    // recargar la escena. Al marcar el flag al terminar y comprobarlo en Awake, si la escena se
    // recarga tras haber visto la secuencia, estos GameObjects se ocultan inmediatamente.
    private const string SeenFlag = "CINEMATIC_SEEN:ESTELA_APPEARS";

    private static bool HasSequencePlayed()
    {
        var preset = GameBootService.Profile != null ? GameBootService.Profile.GetActivePresetResolved() : null;
        return preset != null && preset.flags != null && preset.flags.Contains(SeenFlag);
    }

    private static void MarkSequencePlayed()
    {
        var preset = GameBootService.Profile != null ? GameBootService.Profile.GetActivePresetResolved() : null;
        if (preset == null) return;
        if (preset.flags == null) preset.flags = new System.Collections.Generic.List<string>();
        if (!preset.flags.Contains(SeenFlag)) preset.flags.Add(SeenFlag);
    }

    protected override void Awake()
    {
        base.Awake();

        // Si la secuencia ya se reprodujo antes en esta partida, aplicar de inmediato el estado
        // final (arañas y guerreros ocultos) por si la escena se acaba de recargar desde un
        // punto de guardado posterior a la secuencia.
        if (HasSequencePlayed())
        {
            if (_spiderObjects != null)
            {
                foreach (var spider in _spiderObjects)
                    if (spider != null) spider.SetActive(false);
            }
            if (_warrior1Transform != null) _warrior1Transform.gameObject.SetActive(false);
            if (_warrior2Transform != null) _warrior2Transform.gameObject.SetActive(false);
        }

        if (_estelaTransform != null)
        {
            _estelaEmotion    = _estelaTransform.GetComponentInChildren<NPCEmotionController>();
            _estelaSimpleAnim = _estelaTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _estelaAgent      = _estelaTransform.GetComponent<NavMeshAgent>();

            // Capturar YA, en Awake, antes de que el NavMeshAgent de Estela pueda corregir su
            // posición por su cuenta al activarse (ver comentario en la declaración de campos).
            _estelaDesignPosition = _estelaTransform.position;
            _estelaDesignRotation = _estelaTransform.rotation;
        }

        if (_warrior1Transform != null)
        {
            _warrior1Emotion    = _warrior1Transform.GetComponentInChildren<NPCEmotionController>();
            _warrior1SimpleAnim = _warrior1Transform.GetComponentInChildren<NPCSimpleAnimator>();
            _warrior1Agent      = _warrior1Transform.GetComponent<NavMeshAgent>();
            _warrior1Npc        = _warrior1Transform.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
            _warrior1DesignPosition = _warrior1Transform.position;
            _warrior1DesignRotation = _warrior1Transform.rotation;
        }
        if (_warrior2Transform != null)
        {
            _warrior2Emotion    = _warrior2Transform.GetComponentInChildren<NPCEmotionController>();
            _warrior2SimpleAnim = _warrior2Transform.GetComponentInChildren<NPCSimpleAnimator>();
            _warrior2Agent      = _warrior2Transform.GetComponent<NavMeshAgent>();
            _warrior2Npc        = _warrior2Transform.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
            _warrior2DesignPosition = _warrior2Transform.position;
            _warrior2DesignRotation = _warrior2Transform.rotation;
        }

        _enemyHitLayers       = LayerMask.GetMask("Enemy", "Boss");
        _enemyCollisionLayers = LayerMask.GetMask("Enemy", "Boss", "Default");
    }

    private void Start()
    {
        StartCoroutine(Co_RestoreDesignTransforms());
    }

    /// Un frame después de Awake, cualquier NavMeshAgent (Estela, guerreros) ya habrá aplicado
    /// (si iba a hacerlo) su corrección automática al activarse. Si alguno se alejó de la
    /// posición/rotación colocada a mano, lo devolvemos ahí — con Warp si tiene agente (evita que
    /// NavMesh/física generen un nuevo path), o directamente por Transform si no. Mismo patrón que
    /// LiamCrystalBallSequencer.Co_RestoreLiamDesignPosition; aquí se aplica a los tres actores
    /// porque todos los shots de esta secuencia (sobre todo _shotEstela, muy ajustados) dependen
    /// de que estén exactamente donde se ven en el editor.
    private IEnumerator Co_RestoreDesignTransforms()
    {
        yield return null;

        RestoreDesignTransform(_estelaTransform, _estelaAgent, _estelaDesignPosition, _estelaDesignRotation, "Estela");
        RestoreDesignTransform(_warrior1Transform, _warrior1Agent, _warrior1DesignPosition, _warrior1DesignRotation, "Guerrero1");
        RestoreDesignTransform(_warrior2Transform, _warrior2Agent, _warrior2DesignPosition, _warrior2DesignRotation, "Guerrero2");
    }

    private void RestoreDesignTransform(Transform t, NavMeshAgent agent, Vector3 designPosition, Quaternion designRotation, string label)
    {
        if (t == null) return;
        if ((t.position - designPosition).sqrMagnitude <= 0.0001f)
        {
            t.rotation = designRotation;
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[EstelaAppearsSequencer] {label} se había desplazado de su posición diseñada " +
            $"({t.position} → objetivo {designPosition}), probablemente por corrección automática del " +
            $"NavMeshAgent al activarse. Restaurando (esto es lo que desencuadraba los planos de cámara).");
#endif

        if (agent != null && agent.isOnNavMesh)
            agent.Warp(designPosition);
        else
            t.position = designPosition;

        t.rotation = designRotation;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Secuencia principal
    // ══════════════════════════════════════════════════════════════════════════

    protected override IEnumerator Co_Sequence()
    {
        yield return Co_BeginCinematicWithTransition(_shotWarriors);

        PlaySequenceMusic();

        // ── Fase 0: Guerrero da la orden de ataque ────────────────────────────
        FaceTarget(_warrior1Transform, _estelaTransform);
        FaceTarget(_warrior2Transform, _estelaTransform);
        _warrior1Emotion?.SetEmotion(_attackOrderEmotion);

        bool attackOrderDone = false;
        SpeechBubbleUI.Instance.Show(_warrior1Transform, Loc(_attackOrderKey),
            duration: _attackOrderDuration, onComplete: () => attackOrderDone = true,
            animTrigger: _attackOrderAnim);
        yield return new WaitUntil(() => attackOrderDone);

        // ── Fase 1: Estela mata las arañas ────────────────────────────────────
        int count = _spiderTargets != null ? _spiderTargets.Length : 0;
        for (int i = 0; i < count; i++)
        {
            yield return Co_KillSpider(i);
            if (i < count - 1)
                yield return new WaitForSeconds(_betweenSpidersDelay);
        }

        // Estela celebra — justo antes de que los guerreros le devuelvan sus palabras
        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(NPCEmotion.Happy);
        bool pancomiDone = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc("EVT_ESTELA_PANCOMI"),
            duration: 2.5f, onComplete: () => pancomiDone = true, animTrigger: _victoryAnim);
        yield return new WaitUntil(() => pancomiDone);

        yield return new WaitForSeconds(_pauseBeforeWarriors);

        // ── Fase 2: Guerreros fanfarronean ────────────────────────────────────
        yield return Co_WarriorTaunts();

        // ── Fase 3: Estela hace el numerito ───────────────────────────────────
        yield return Co_EstelaActsDramatic();

        // ── Fase 4: El insulto → rabia → huida ───────────────────────────────
        yield return Co_InsultAndRage();

        // ── Fase 5: Will aplaude + Estela hace la reverencia ─────────────────
        yield return Co_WillAndBow();

        // ── Fase 6: Estela, línea final ───────────────────────────────────────
        yield return Co_EstelaVictory();

        yield return new WaitForSeconds(_holdAfterVictory);

        yield return Co_EndCinematicWithTransition(RestoreMusic);

        MarkSequencePlayed();
        RaiseSignalOut();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 2 — Guerreros fanfarronean
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WarriorTaunts()
    {
        _cinematicCamera.Cut(_shotWarriors);

        // Guerrero 1 habla
        FaceTarget(_warrior1Transform, _estelaTransform);
        FaceTarget(_warrior2Transform, _estelaTransform);

        _warrior1Emotion?.SetEmotion(_w1TauntEmotion);
        bool done1 = false;
        SpeechBubbleUI.Instance.Show(_warrior1Transform, Loc(_w1TauntKey),
            duration: _w1TauntDuration, onComplete: () => done1 = true,
            animTrigger: _w1TauntAnim);
        yield return new WaitUntil(() => done1);

        // Guerrero 2 habla
        _warrior2Emotion?.SetEmotion(_w2TauntEmotion);
        bool done2 = false;
        SpeechBubbleUI.Instance.Show(_warrior2Transform, Loc(_w2TauntKey),
            duration: _w2TauntDuration, onComplete: () => done2 = true,
            animTrigger: _w2TauntAnim);
        yield return new WaitUntil(() => done2);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 3 — Estela actúa la víctima
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_EstelaActsDramatic()
    {
        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(_dramaticEmotion);

        // Los guerreros siguen mirando a Estela durante todo el numerito.
        FaceTarget(_warrior1Transform, _estelaTransform);
        FaceTarget(_warrior2Transform, _estelaTransform);

        yield return ShowBubblePaged(_estelaTransform, Loc(_dramaticLineKey), _dramaticLineDuration, _dramaticAnim, loopAnim: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 4 — El insulto, la rabia y la huida
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_InsultAndRage()
    {
        // Guerrero 1 suelta el insulto, ambos se ríen
        _cinematicCamera.Cut(_shotWarriors);
        _warrior1Emotion?.SetEmotion(_insultEmotion);
        _warrior2Emotion?.SetEmotion(_insultEmotion);

        // Siguen mirando a Estela mientras se burlan de ella.
        FaceTarget(_warrior1Transform, _estelaTransform);
        FaceTarget(_warrior2Transform, _estelaTransform);

        bool insultDone = false;
        SpeechBubbleUI.Instance.Show(_warrior1Transform, Loc(_insultLineKey),
            duration: _insultLineDuration, onComplete: () => insultDone = true,
            animTrigger: _insultAnim);
        yield return new WaitUntil(() => insultDone);

        yield return new WaitForSeconds(_laughPause);

        // Estela explota — corte a ella
        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(_rageEmotion);
        if (!string.IsNullOrEmpty(_rageAnim))
        {
            // Pequeña pausa para que se vea la reacción antes de que empiece a disparar
            yield return new WaitForSeconds(0.4f);
        }

        // Ráfaga alternando entre los dos guerreros: cada uno se protege (animación +
        // VFX de escudo) justo cuando recibe su disparo, sin dejar de mirar a Estela
        // (de donde vienen los proyectiles).
        for (int i = 0; i < _rageShots; i++)
        {
            // A mitad de la ráfaga cortamos a los guerreros para ver cómo reciben los disparos
            if (i == _rageShots / 2 && _shotWarriors != null)
                _cinematicCamera.Cut(_shotWarriors);

            bool targetIsWarrior1 = (i % 2 == 0);
            Transform rageTarget = targetIsWarrior1 ? _warrior1Transform : _warrior2Transform;
            NPCSimpleAnimator rageTargetAnim = targetIsWarrior1 ? _warrior1SimpleAnim : _warrior2SimpleAnim;

            if (rageTarget != null)
            {
                FireAtTarget(rageTarget);
                PlayWarriorBlockReaction(rageTarget, rageTargetAnim);
            }

            yield return new WaitForSeconds(_rageShotInterval);
        }

        // Corte a wide: los guerreros salen corriendo
        _cinematicCamera.Cut(_shotWide);

        FaceAway(_warrior1Transform, _estelaTransform);
        FaceAway(_warrior2Transform, _estelaTransform);

        // FIX INC-060 (corregido): marcar solo Context.IsInCinematic no basta. IdleState.CheckTransitions
        // sí reacciona y pasa a CinematicState, pero como aquí nunca se llama CinematicState.StartSequence(),
        // ese estado no tiene _currentSequence asignada → se completa en el mismo OnEnter() y el siguiente
        // Update() vuelve a IdleState de inmediato. Una vez de vuelta en IdleState, su OnUpdate() (y el
        // safety-check de NPCBehaviourManagerV2.LateUpdate) fuerzan `agent.isStopped = true` +
        // `agent.ResetPath()` cada frame, peleando contra el SetDestination() de Co_FleeWarrior. El
        // resultado seguía siendo el mismo bug: el guerrero se desplazaba (o se atascaba) sin reproducir
        // nunca la animación de andar, porque NPCSimpleAnimator.SyncWithNavMeshAgent() ve el agente
        // detenido y fuerza la velocidad de animación a 0 cada frame.
        // Fix real: desactivar el propio NPCBehaviourManagerV2 (igual que TabernaSequencer.SeatNPC hace
        // con los NPCs sentados) para que ni Update() ni LateUpdate() de la FSM se ejecuten mientras esta
        // corrutina controla el NavMeshAgent directamente. No hace falta reactivarlo: el guerrero termina
        // con SetActive(false) al final de Co_FleeWarrior.
        if (_warrior1Npc != null) _warrior1Npc.enabled = false;
        if (_warrior2Npc != null) _warrior2Npc.enabled = false;

        // FIX: como en LiamCrystalBallSequencer.FreezeLiamNavigation / CinematicState.MoveToPositionSequence,
        // desactivar la obstacle avoidance (RVO) de ambos guerreros antes de moverlos. Con avoidance
        // activo, los dos guerreros (que salen desde posiciones muy cercanas, uno junto al otro) se
        // bloquean mutuamente el paso: el NavMeshAgent va corrigiendo la trayectoria para esquivarse y
        // NavMeshAgent.velocity se queda casi a 0 la mayor parte del tiempo aunque desiredVelocity no lo
        // esté. SyncWithNavMeshAgent (NPCSimpleAnimator) nunca ve velocidad suficiente y no dispara la
        // animación de andar, mientras el agente sigue arrastrándose muy despacio hacia el destino. Al
        // llegar _fleeTimeout, Co_FleeWarrior igualmente los desactiva con SetActive(false): el efecto
        // visual es que los guerreros "se van" de golpe al final sin haber caminado nunca.
        if (_warrior1Agent != null)
        {
            _warrior1OriginalAvoidance = _warrior1Agent.obstacleAvoidanceType;
            _warrior1Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }
        if (_warrior2Agent != null)
        {
            _warrior2OriginalAvoidance = _warrior2Agent.obstacleAvoidanceType;
            _warrior2Agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        // Activar animación de carrera antes de mover el agente
        if (_warrior1SimpleAnim != null) { _warrior1SimpleAnim.SetBattleMode(false); _warrior1SimpleAnim.TransitionToLocomotion(); _warrior1SimpleAnim.SetMovementSpeed(1f, 0f); }
        if (_warrior2SimpleAnim != null) { _warrior2SimpleAnim.SetBattleMode(false); _warrior2SimpleAnim.TransitionToLocomotion(); _warrior2SimpleAnim.SetMovementSpeed(1f, 0f); }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ⚠️ DIAGNÓSTICO: en la escena actual _warrior1FleeTarget y _warrior2FleeTarget apuntan
        // ambos al mismo Transform ("RunPoint"). Esto no debería impedir la animación por sí solo,
        // pero hace que los dos guerreros converjan al mismo punto exacto (se superponen al llegar
        // y pueden bloquearse mutuamente el resto del camino). Falta asignar un segundo punto de
        // huida distinto a _warrior2FleeTarget en el Inspector.
        if (_warrior1FleeTarget != null && _warrior1FleeTarget == _warrior2FleeTarget)
        {
            Debug.LogWarning("[EstelaAppearsSequencer] _warrior1FleeTarget y _warrior2FleeTarget " +
                "apuntan al MISMO Transform. Asigna un punto de huida distinto para cada guerrero.");
        }
#endif

        StartCoroutine(Co_FleeWarrior(_warrior1Transform, _warrior1Agent, _warrior1FleeTarget, _warrior1SimpleAnim, _warrior1OriginalAvoidance, "W1"));
        StartCoroutine(Co_FleeWarrior(_warrior2Transform, _warrior2Agent, _warrior2FleeTarget, _warrior2SimpleAnim, _warrior2OriginalAvoidance, "W2"));

        yield return new WaitForSeconds(_fleeTimeout);
    }

    /// Reacción de un guerrero al recibir un disparo de la ráfaga de rabia: se protege
    /// (animación de bloqueo, reutilizando NPCSimpleAnimator.PlayDefendHit) y aparece el VFX
    /// de escudo sobre él, sin dejar de mirar hacia Estela (origen del ataque).
    private void PlayWarriorBlockReaction(Transform warrior, NPCSimpleAnimator warriorAnim)
    {
        if (warrior == null) return;

        FaceTarget(warrior, _estelaTransform);
        warriorAnim?.PlayDefendHit();

        if (_warriorShieldVFX != null)
        {
            Vector3 vfxPos = warrior.position + _warriorShieldVfxOffset;
            VfxPoolService.Instance?.Play(_warriorShieldVFX, vfxPos, warrior.rotation, _warriorShieldVfxLifetime, warrior);
        }
    }

    private IEnumerator Co_FleeWarrior(Transform warrior, NavMeshAgent agent, Transform fleeTarget,
        NPCSimpleAnimator simpleAnim, ObstacleAvoidanceType originalAvoidance, string debugTag)
    {
        if (warrior == null || fleeTarget == null) yield break;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = _fleeSpeed;
            bool pathOk = agent.SetDestination(fleeTarget.position);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EstelaFlee:{debugTag}] SetDestination → {fleeTarget.position} | " +
                $"aceptado={pathOk} isOnNavMesh={agent.isOnNavMesh} isStopped={agent.isStopped} " +
                $"speed={agent.speed} avoidance={agent.obstacleAvoidanceType} " +
                $"syncedAgentCoincide={(simpleAnim != null && simpleAnim.DebugSyncedAgent == agent)}");
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning($"[EstelaFlee:{debugTag}] Sin NavMeshAgent válido (agent null={agent == null}, " +
                $"isOnNavMesh={(agent != null ? agent.isOnNavMesh.ToString() : "N/A")}). Se moverá el transform a mano.");
        }
#endif

        float elapsed = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float debugLogTimer = 0f;
#endif
        while (elapsed < _fleeTimeout && warrior != null &&
               Vector3.Distance(warrior.position, fleeTarget.position) > 1f)
        {
            // Si no hay agente, mover el transform directamente
            if (agent == null || !agent.isOnNavMesh)
            {
                Vector3 dir = (fleeTarget.position - warrior.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    warrior.rotation = Quaternion.LookRotation(dir);
                    warrior.position = Vector3.MoveTowards(
                        warrior.position, fleeTarget.position, _fleeSpeed * Time.deltaTime);

                    // Sin NavMeshAgent no hay SyncWithNavMeshAgent que anime el movimiento:
                    // forzar aquí la animación de carrera mientras el transform avanza a mano.
                    simpleAnim?.SetMovementSpeed(1f, 0f);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugLogTimer += Time.deltaTime;
            if (debugLogTimer >= 0.3f)
            {
                debugLogTimer = 0f;
                if (agent != null && agent.isOnNavMesh)
                {
                    Debug.Log($"[EstelaFlee:{debugTag}] t={elapsed:F1}s pos={warrior.position} " +
                        $"vel={agent.velocity.magnitude:F2} desiredVel={agent.desiredVelocity.magnitude:F2} " +
                        $"hasPath={agent.hasPath} pathPending={agent.pathPending} pathStatus={agent.pathStatus} " +
                        $"remaining={agent.remainingDistance:F2} isStopped={agent.isStopped} " +
                        $"animSpeed={(simpleAnim != null ? simpleAnim.DebugCurrentMovementSpeed.ToString("F2") : "N/A")}");
                }
                else
                {
                    Debug.Log($"[EstelaFlee:{debugTag}] t={elapsed:F1}s (sin agente) pos={warrior.position} " +
                        $"animSpeed={(simpleAnim != null ? simpleAnim.DebugCurrentMovementSpeed.ToString("F2") : "N/A")}");
                }
            }
#endif

            elapsed += Time.deltaTime;
            yield return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EstelaFlee:{debugTag}] Fin del bucle en t={elapsed:F1}s, pos={warrior?.position}, " +
            $"distanciaAlDestino={(warrior != null ? Vector3.Distance(warrior.position, fleeTarget.position).ToString("F2") : "N/A")}");
#endif

        // Restaurar la obstacle avoidance original por si el NPC se reactiva más adelante.
        if (agent != null)
            agent.obstacleAvoidanceType = originalAvoidance;

        if (warrior != null && warrior.gameObject != null)
            warrior.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 5 — Will aplaude + Estela hace la reverencia
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WillAndBow()
    {
        var willTransform = _willTransform != null
            ? _willTransform
            : (PlayerService.Player != null ? PlayerService.Player.transform : null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (willTransform == null)
            Debug.LogWarning("[EstelaAppearsSequencer] Co_WillAndBow: willTransform no encontrado. Asígnalo en el Inspector o verifica PlayerService.");
#endif

        if (willTransform != null)
        {
            // Liberar la cámara cinemática para que gameplay encuadre a Will
            _cinematicCamera.Deactivate();

            var willEmotion = willTransform.GetComponentInChildren<NPCEmotionController>();
            willEmotion?.SetEmotion(_willEmotion);

            bool willDone = false;
            SpeechBubbleUI.Instance.Show(willTransform, Loc(_willLineKey),
                duration: _willLineDuration, onComplete: () => willDone = true,
                animTrigger: _willAnim);

            float elapsed = 0f;
            while (!willDone && elapsed < _willLineDuration + 1f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Recuperar control cinemático para el plano de la reverencia
            _cinematicCamera.Activate();
        }

        // Plano de Estela haciendo la reverencia
        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(NPCEmotion.Happy);

        if (!string.IsNullOrEmpty(_bowAnim))
            _estelaSimpleAnim?.PlaySocialGesture(_bowAnim);

        yield return new WaitForSeconds(_bowDuration);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 6 — Victoria de Estela
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_EstelaVictory()
    {
        _cinematicCamera.Cut(_shotEstela);
        _estelaEmotion?.SetEmotion(_victoryEmotion);

        bool done = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_victoryLineKey),
            duration: _victoryLineDuration, onComplete: () => done = true,
            animTrigger: _victoryAnim);
        yield return new WaitUntil(() => done);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 1 — Araña individual
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_KillSpider(int index)
    {
        Transform spiderTarget = _spiderTargets != null && index < _spiderTargets.Length
            ? _spiderTargets[index] : null;

        _cinematicCamera.Cut(_shotEstela);

        FaceTarget(_estelaTransform, spiderTarget);
        yield return new WaitForSeconds(_aimDelay);

        if (spiderTarget != null)
            FireAtTarget(spiderTarget);

        // Corte a la araña justo después del disparo
        if (_shotPerSpider != null && index < _shotPerSpider.Length && _shotPerSpider[index] != null)
            _cinematicCamera.Cut(_shotPerSpider[index]);

        yield return new WaitForSeconds(_lineDelay);

        string lineKey = _killLineKeys  != null && index < _killLineKeys.Length  ? _killLineKeys[index]  : string.Empty;
        NPCEmotion emo = _killEmotions  != null && index < _killEmotions.Length  ? _killEmotions[index]  : NPCEmotion.None;
        string anim    = _killAnims     != null && index < _killAnims.Length     ? _killAnims[index]     : null;

        if (emo != NPCEmotion.None)
            _estelaEmotion?.SetEmotion(emo);

        float killDelay = Mathf.Max(0f, _projectileFlightTime - _lineDelay);

        if (!string.IsNullOrEmpty(lineKey))
        {
            bool lineDone   = false;
            bool spiderDead = false;
            float elapsed   = 0f;
            Vector3 spiderPos = spiderTarget != null ? spiderTarget.position : Vector3.zero;

            SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(lineKey),
                duration: _killLineDuration,
                onComplete: () => lineDone = true,
                animTrigger: anim);

            while (!lineDone)
            {
                elapsed += Time.deltaTime;
                if (!spiderDead && elapsed >= killDelay)
                {
                    KillSpider(index, spiderPos);
                    spiderDead = true;
                }
                yield return null;
            }

            if (!spiderDead)
                KillSpider(index, spiderTarget != null ? spiderTarget.position : Vector3.zero);
        }
        else
        {
            yield return new WaitForSeconds(killDelay);
            KillSpider(index, spiderTarget != null ? spiderTarget.position : Vector3.zero);
        }
    }

    private void KillSpider(int index, Vector3 position)
    {
        if (_spiderImpactVFX != null)
        {
            var vfx = Instantiate(_spiderImpactVFX, position, Quaternion.identity);
            Destroy(vfx, _vfxLifetime);
        }

        if (_spiderObjects != null && index < _spiderObjects.Length && _spiderObjects[index] != null)
            _spiderObjects[index].SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    /// Cálculo de spawn idéntico al de gameplay (MagicProjectileSpawner):
    /// forwardOffset sobre la dirección de tiro, Y del positionOffset en espacio
    /// mundial y X/Z en espacio local del PERSONAJE (no del hueso de la mano,
    /// cuyos ejes locales son arbitrarios y desplazaban el spawn hacia atrás).
    private void FireAtTarget(Transform target)
    {
        if (_bolaFuego == null || _bolaFuego.prefab == null || target == null) return;

        // Orientar a Estela antes de calcular nada para que su forward sea válido
        FaceTarget(_estelaTransform, target);

        // Sin spawn point: disparar desde la altura del pecho del personaje
        // (mismo criterio que AllyCombatState), no desde el pivote en los pies.
        Vector3 originPos = _estelaSpawnPoint != null
            ? _estelaSpawnPoint.position
            : _estelaTransform.position + Vector3.up * 1.2f;
        Vector3 targetPos = target.position + Vector3.up;
        Vector3 dir       = (targetPos - originPos).normalized;

        if (_bolaFuego.flattenDirection) { dir.y = 0; dir.Normalize(); }

        Vector3 spawnPos = originPos + dir * _bolaFuego.forwardOffset;
        if (_bolaFuego.positionOffset != Vector3.zero)
        {
            // Y siempre vertical (espacio mundial)
            spawnPos.y += _bolaFuego.positionOffset.y;
            // X (derecha) y Z (adelante) en espacio local del personaje
            if (_bolaFuego.positionOffset.x != 0f || _bolaFuego.positionOffset.z != 0f)
            {
                Vector3 localOffset = new Vector3(_bolaFuego.positionOffset.x, 0f, _bolaFuego.positionOffset.z);
                spawnPos += _estelaTransform.TransformDirection(localOffset);
            }
        }

        _estelaSimpleAnim?.PlaySpellCast();

        var go = Instantiate(_bolaFuego.prefab, spawnPos, Quaternion.LookRotation(dir));

        int projLayer = LayerMask.NameToLayer("Projectile");
        if (projLayer != -1)
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = projLayer;

        if (go.TryGetComponent<MagicProjectile>(out var proj))
        {
            var cfg = new MagicProjectile.ProjectileConfig
            {
                damage          = _bolaFuego.damage,
                aoeRadius       = _bolaFuego.aoeRadius,
                knockbackForce  = _bolaFuego.knockbackForce,
                hitLayers       = _enemyHitLayers,
                collisionLayers = _enemyCollisionLayers,
                destroyOnHit    = _bolaFuego.destroyOnHit,
                lifeTime        = _bolaFuego.lifeTime,
                maxRange        = _bolaFuego.maxRange,
                initialSpeed    = _bolaFuego.initialSpeed,
                useGravity      = _bolaFuego.useGravity,
                element         = _bolaFuego.element,
                impactVFX       = _bolaFuego.impactVFX,
                despawnVFX      = _bolaFuego.despawnVFX,
                vfxLifetime     = _bolaFuego.vfxLifetime,
                impactSFXKey    = _bolaFuego.impactSFXKey
            };
            proj.Configure(cfg, _estelaTransform.gameObject);
            proj.Launch(dir, _bolaFuego.initialSpeed, _bolaFuego.useGravity);
        }
    }

}
