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
    private NPCEmotionController _warrior1Emotion;
    private NPCEmotionController _warrior2Emotion;
    private NPCSimpleAnimator    _warrior1SimpleAnim;
    private NPCSimpleAnimator    _warrior2SimpleAnim;
    private NavMeshAgent         _warrior1Agent;
    private NavMeshAgent         _warrior2Agent;
    private int _enemyHitLayers;
    private int _enemyCollisionLayers;

    protected override void Awake()
    {
        base.Awake();

        if (_estelaTransform != null)
        {
            _estelaEmotion    = _estelaTransform.GetComponentInChildren<NPCEmotionController>();
            _estelaSimpleAnim = _estelaTransform.GetComponentInChildren<NPCSimpleAnimator>();
        }

        if (_warrior1Transform != null)
        {
            _warrior1Emotion    = _warrior1Transform.GetComponentInChildren<NPCEmotionController>();
            _warrior1SimpleAnim = _warrior1Transform.GetComponentInChildren<NPCSimpleAnimator>();
            _warrior1Agent      = _warrior1Transform.GetComponent<NavMeshAgent>();
        }
        if (_warrior2Transform != null)
        {
            _warrior2Emotion    = _warrior2Transform.GetComponentInChildren<NPCEmotionController>();
            _warrior2SimpleAnim = _warrior2Transform.GetComponentInChildren<NPCSimpleAnimator>();
            _warrior2Agent      = _warrior2Transform.GetComponent<NavMeshAgent>();
        }

        _enemyHitLayers       = LayerMask.GetMask("Enemy", "Boss");
        _enemyCollisionLayers = LayerMask.GetMask("Enemy", "Boss", "Default");
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
        BeginCinematic();

        yield return FeedbackService.ScreenFadeAsync(Color.black, _fadeInDuration, fadeIn: false);

        PlaySequenceMusic();

        // ── Fase 0: Guerrero da la orden de ataque ────────────────────────────
        _cinematicCamera.Cut(_shotWarriors);
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

        // Fade a negro y restaurar
        yield return FeedbackService.ScreenFadeAsync(Color.black, _fadeOutDuration, fadeIn: true);

        RestoreMusic();
        EndCinematic();

        yield return FeedbackService.ScreenFadeAsync(Color.black, _fadeInDuration, fadeIn: false);

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

        // Ráfaga alternando entre los dos guerreros
        for (int i = 0; i < _rageShots; i++)
        {
            // A mitad de la ráfaga cortamos a los guerreros para ver cómo reciben los disparos
            if (i == _rageShots / 2 && _shotWarriors != null)
                _cinematicCamera.Cut(_shotWarriors);

            Transform rageTarget = (i % 2 == 0) ? _warrior1Transform : _warrior2Transform;
            if (rageTarget != null)
                FireAtTarget(rageTarget);

            yield return new WaitForSeconds(_rageShotInterval);
        }

        // Corte a wide: los guerreros salen corriendo
        _cinematicCamera.Cut(_shotWide);

        FaceAway(_warrior1Transform, _estelaTransform);
        FaceAway(_warrior2Transform, _estelaTransform);

        // Activar animación de carrera antes de mover el agente
        if (_warrior1SimpleAnim != null) { _warrior1SimpleAnim.SetBattleMode(false); _warrior1SimpleAnim.TransitionToLocomotion(); _warrior1SimpleAnim.SetMovementSpeed(1f, 0f); }
        if (_warrior2SimpleAnim != null) { _warrior2SimpleAnim.SetBattleMode(false); _warrior2SimpleAnim.TransitionToLocomotion(); _warrior2SimpleAnim.SetMovementSpeed(1f, 0f); }

        StartCoroutine(Co_FleeWarrior(_warrior1Transform, _warrior1Agent, _warrior1FleeTarget));
        StartCoroutine(Co_FleeWarrior(_warrior2Transform, _warrior2Agent, _warrior2FleeTarget));

        yield return new WaitForSeconds(_fleeTimeout);
    }

    private IEnumerator Co_FleeWarrior(Transform warrior, NavMeshAgent agent, Transform fleeTarget)
    {
        if (warrior == null || fleeTarget == null) yield break;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = _fleeSpeed;
            agent.SetDestination(fleeTarget.position);
        }

        float elapsed = 0f;
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
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

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

    private void FireAtTarget(Transform target)
    {
        if (_bolaFuego == null || _bolaFuego.prefab == null || target == null) return;

        Transform origin  = _estelaSpawnPoint != null ? _estelaSpawnPoint : _estelaTransform;
        Vector3 spawnPos  = origin.position + origin.TransformDirection(_bolaFuego.positionOffset);
        Vector3 targetPos = target.position + Vector3.up;
        Vector3 dir       = (targetPos - spawnPos).normalized;

        if (_bolaFuego.flattenDirection) { dir.y = 0; dir.Normalize(); }

        FaceTarget(_estelaTransform, target);
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
