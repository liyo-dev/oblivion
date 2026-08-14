using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia de la montaña:
///   0. Estela dice su frase (opcional). Estela queda congelada en su sitio.
///   1. Will, Eldran y Liam corren a sus puntos de huida (Eldran grita
///      "¡CORREEEEDDDDD!" en bocadillo nada más arrancar la huida); al llegar
///      los tres, corte al plano de huida y hacen la animación de miedo.
///   2. Primer plano de Estela (el plano inicial acercado) lanzando la ráfaga.
///   3. Flash + shake de impacto + SFX de explosión ("Mountain_Impact") → fade a negro
///      (la explosión sigue sonando durante el black out, no se corta con el fundido).
/// El grafo narrativo conecta MOUNTAIN_DONE con MOUNTAIN_EXPLOSION_EVENT.
/// Señal de entrada: "MOUNTAIN_START".
/// Señal de salida:  "MOUNTAIN_DONE".
[DisallowMultipleComponent]
public class MountainSequencer : CinematicSequencerBase
{
    [Header("Personaje — Estela")]
    [SerializeField] private Transform _estelaTransform;
    [Tooltip("Punto de lanzamiento del hechizo (hueso de mano o equivalente)")]
    [SerializeField] private Transform _estelaSpawnPoint;
    [SerializeField] private MagicSpellSO _bolaFuego;

    [Header("Personaje — Will")]
    [SerializeField] private Transform _willTransform;
    [Tooltip("Punto al que corre Will (delante, hacia cámara)")]
    [SerializeField] private Transform _willFleeTarget;

    [Header("Personaje — Eldran")]
    [SerializeField] private Transform _eldranTransform;
    [Tooltip("Punto al que corre Eldran (delante, hacia cámara)")]
    [SerializeField] private Transform _eldranFleeTarget;

    [Header("Personaje — Liam")]
    [SerializeField] private Transform _liamTransform;
    [Tooltip("Punto al que corre Liam (delante, hacia cámara)")]
    [SerializeField] private Transform _liamFleeTarget;

    [Header("Cámara — planos")]
    [Tooltip("Plano de Estela. También es la base del primer plano de la Fase 2 (se acerca al personaje).")]
    [SerializeField] private Transform _shotEstela;
    [Tooltip("Plano del grupo asustado. Se corta cuando los tres han llegado a sus puntos.")]
    [SerializeField] private Transform _shotFlee;

    [Header("Objetivo")]
    [Tooltip("Transform clavado en el punto de impacto de la montaña")]
    [SerializeField] private Transform _mountainTarget;

    // ── Fase 0 — Estela habla ─────────────────────────────────────────────────

    [Header("Fase 0 — Estela habla (opcional)")]
    [Tooltip("Clave de localización. Vacío = sin globo, pasa directamente a la huida.")]
    [SerializeField] private string     _keyLine      = "EVT_MOUNTAIN_01";
    [SerializeField] private NPCEmotion _emotionLine  = NPCEmotion.Angry;
    [SerializeField] private string     _animLine     = "Angry01";
    [SerializeField] private float      _lineDuration = 2.5f;

    // ── Fase 1 — Huida del grupo ──────────────────────────────────────────────

    [Header("Fase 1 — Huida del grupo")]
    [SerializeField] private float _fleeSpeed   = 5f;
    [Tooltip("Tiempo máximo de la huida antes de continuar aunque alguien no haya llegado")]
    [SerializeField] private float _fleeTimeout = 4f;
    [Tooltip("Animación de miedo que hacen los tres tras el corte al plano de huida")]
    [SerializeField] private string _animFear   = "Fear01";
    [Tooltip("Tiempo en el plano de huida viendo las poses de miedo antes del primer plano")]
    [SerializeField] private float _fleeSettleBeat = 3.0f;

    [Tooltip("Clave de localización del grito de Eldran al arrancar la huida. Vacío = sin bocadillo.")]
    [SerializeField] private string _keyLineEldranRun = "EVT_MOUNTAIN_ELDRAN_RUN";
    [SerializeField] private string _animLineEldranRun = "Fear01";
    [SerializeField] private float  _lineDurationEldranRun = 1.8f;

    // ── Fase 2 — Ráfaga en primer plano ───────────────────────────────────────

    [Header("Fase 2 — Ráfaga en primer plano")]
    [Tooltip("Distancia de la cámara a Estela en el primer plano")]
    [SerializeField] private float _closeupDistance = 3f;
    [Tooltip("Altura del punto de mira sobre el pivote de Estela (pecho/cara)")]
    [SerializeField] private float _closeupLookHeight = 1.4f;
    [Tooltip("Duración del acercamiento desde el plano inicial (0 = corte seco al primer plano)")]
    [SerializeField] private float _closeupBlendTime = 0.8f;
    [SerializeField] private float _aimDelay         = 0.5f;
    [Tooltip("Número de proyectiles de la ráfaga")]
    [SerializeField] private int   _rageShots        = 6;
    [Tooltip("Segundos entre cada proyectil")]
    [SerializeField] private float _rageShotInterval = 0.2f;

    // ── Fase 3 — Impacto ──────────────────────────────────────────────────────

    [Header("Fase 3 — Impacto")]
    [Tooltip("Tiempo de vuelo del último proyectil antes del flash de impacto")]
    [SerializeField] private float _mountainImpactDelay = 1.0f;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private NPCEmotionController _estelaEmotion;
    private NPCSimpleAnimator    _estelaSimpleAnim;
    private NavMeshAgent         _estelaAgent;
    private NPCBehaviourManagerV2 _eldranManager;
    private NPCBehaviourManagerV2 _liamManager;
    private NPCSimpleAnimator    _eldranSimpleAnim;
    private NPCSimpleAnimator    _liamSimpleAnim;
    private NavMeshAgent         _eldranAgent;
    private NavMeshAgent         _liamAgent;
    private CharacterController  _willCharController;
    private Animator             _willAnimator;
    private PlayerDialogueAnimator _willDialogueAnim;
    private Transform _closeupShot;   // generado en runtime a partir de _shotEstela
    private int _enemyHitLayers;
    private int _enemyCollisionLayers;

    // Hashes del animator del jugador (compartidos con FollowPlayerState)
    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    private static readonly int HashLocomotion     = Animator.StringToHash("Free Locomotion");

    protected override void Awake()
    {
        base.Awake();
        if (_estelaTransform != null)
        {
            _estelaEmotion    = _estelaTransform.GetComponentInChildren<NPCEmotionController>();
            _estelaSimpleAnim = _estelaTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _estelaAgent      = _estelaTransform.GetComponent<NavMeshAgent>();
        }
        if (_eldranTransform != null)
        {
            _eldranManager    = _eldranTransform.GetComponent<NPCBehaviourManagerV2>();
            _eldranSimpleAnim = _eldranTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _eldranAgent      = _eldranTransform.GetComponent<NavMeshAgent>();
        }
        if (_liamTransform != null)
        {
            _liamManager    = _liamTransform.GetComponent<NPCBehaviourManagerV2>();
            _liamSimpleAnim = _liamTransform.GetComponentInChildren<NPCSimpleAnimator>();
            _liamAgent      = _liamTransform.GetComponent<NavMeshAgent>();
        }
        if (_willTransform != null)
        {
            _willCharController = _willTransform.GetComponent<CharacterController>();
            _willAnimator       = _willTransform.GetComponent<Animator>();
            _willDialogueAnim   = _willTransform.GetComponentInChildren<PlayerDialogueAnimator>();
        }
        _enemyHitLayers       = LayerMask.GetMask("Enemy", "Boss");
        _enemyCollisionLayers = LayerMask.GetMask("Enemy", "Boss", "Default");
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Test — iniciar sin señal")]
    private void TestStartDirect() => StartCoroutine(Co_Sequence());
#endif

    protected override IEnumerator Co_Sequence()
    {
        yield return Co_BeginCinematicWithTransition(_shotEstela);
        PlaySequenceMusic();

        // Estela no huye: congelar su seguimiento de party (con el agent desactivado
        // FollowPlayerState no puede moverla ni rotarla) y su rotación automática.
        FreezeEstela();
        FaceTarget(_estelaTransform, _mountainTarget);

        // ── Fase 0: Estela habla ──────────────────────────────────────────────
        yield return Co_EstelaLine();

        // ── Fase 1: Huida del grupo ───────────────────────────────────────────
        yield return Co_GroupFlees();

        // ── Fase 2: Ráfaga en primer plano ────────────────────────────────────
        yield return Co_RageBurst();

        // ── Fase 3: Impacto ───────────────────────────────────────────────────
        yield return Co_Impact();

        // FIX parpadeo montaña→enfoque: antes se usaba Co_EndCinematicWithTransition, que revela
        // gameplay al terminar la cinemática. El grafo narrativo encadena MOUNTAIN_DONE con un
        // FocusCameraNode (focusId MOUNTAIN_EXPLOSION_EVENT) que vuelve a cortar la cámara casi
        // de inmediato, así que el jugador veía un salto a modo gameplay de por medio antes del
        // enfoque a la montaña. Mismo patrón que ya usa LiamGolemSummonSequencer para encadenar
        // con BossIntroPresentation: quedarse en negro y dejar que el sistema siguiente
        // (FocusCameraNode, ver FeedbackService.IsScreenFaded ahí) revele él mismo.
        yield return Co_EndCinematicStayBlack(() =>
        {
            RestoreMusic();
            UnfreezeEstela();
            if (_closeupShot != null) Destroy(_closeupShot.gameObject);
        });
        RaiseSignalOut();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 0 — Estela habla
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_EstelaLine()
    {
        if (string.IsNullOrEmpty(_keyLine)) yield break;

        _estelaEmotion?.SetEmotion(_emotionLine);
        bool done = false;
        SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_keyLine),
            duration: _lineDuration,
            onComplete: () => done = true,
            animTrigger: _animLine);
        yield return new WaitUntil(() => done);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 1 — Huida del grupo
    // Will, Eldran y Liam corren a sus puntos en paralelo. Cuando los tres han
    // llegado (o vence el timeout) se corta al plano de huida y hacen la
    // animación de miedo a la vez. Los NPCs se mueven vía MoveToPosition
    // (CinematicState), que pausa su brain y evita conflictos con
    // FollowPlayerState. Will (player) se mueve manualmente con el
    // CharacterController desactivado.
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_GroupFlees()
    {
        // Eldran grita al arrancar la huida. No bloquea la corrutina (fire-and-forget):
        // el grupo tiene que echar a correr en el mismo instante, no esperar a que
        // termine el bocadillo.
        ShowEldranRunLine();

        bool eldranDone = _eldranManager == null || _eldranFleeTarget == null;
        bool liamDone   = _liamManager   == null || _liamFleeTarget   == null;

        if (!eldranDone)
        {
            PrepNpcFlee(_eldranAgent, _eldranSimpleAnim);
            _eldranManager.MoveToPosition(_eldranFleeTarget.position,
                walkDuration: 999f, maxDuration: _fleeTimeout, turn: false,
                onComplete: () => eldranDone = true);
        }
        if (!liamDone)
        {
            PrepNpcFlee(_liamAgent, _liamSimpleAnim);
            _liamManager.MoveToPosition(_liamFleeTarget.position,
                walkDuration: 999f, maxDuration: _fleeTimeout, turn: false,
                onComplete: () => liamDone = true);
        }

        // Will: movimiento manual (el input está bloqueado por el modo Cinematic)
        bool willDone = _willTransform == null || _willFleeTarget == null;
        if (!willDone)
        {
            if (_willCharController != null) _willCharController.enabled = false;
            if (_willAnimator != null && _willAnimator.HasState(0, HashLocomotion))
                _willAnimator.CrossFade(HashLocomotion, 0.15f);
        }

        float elapsed = 0f;
        while (elapsed < _fleeTimeout && !(eldranDone && liamDone && willDone))
        {
            if (!willDone)
            {
                Vector3 toTarget = _willFleeTarget.position - _willTransform.position;
                Vector3 flat = toTarget; flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                    _willTransform.rotation = Quaternion.LookRotation(flat);

                _willTransform.position = Vector3.MoveTowards(
                    _willTransform.position, _willFleeTarget.position, _fleeSpeed * Time.deltaTime);
                _willAnimator?.SetFloat(HashInputMagnitude, 1f);

                if (toTarget.sqrMagnitude <= 0.04f)
                {
                    willDone = true;
                    FinishWillFlee();
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Timeout: cerrar la huida de Will aunque no haya llegado
        if (!willDone) FinishWillFlee();

        // Los tres han llegado → corte al plano de huida y miedo a la vez
        if (_shotFlee != null) _cinematicCamera.Cut(_shotFlee);
        _eldranSimpleAnim?.PlaySocialGesture(_animFear);
        _liamSimpleAnim?.PlaySocialGesture(_animFear);
        _willDialogueAnim?.PlayGesture(_animFear);

        yield return new WaitForSeconds(_fleeSettleBeat);
    }

    /// Prepara a un NPC para la huida: velocidad de carrera y rotación automática
    /// activa para que la secuencia de movimiento lo oriente hacia donde corre
    /// (mismo patrón que NPCInteractiveNarrativeExecutor antes de un MoveToPosition).
    private void PrepNpcFlee(NavMeshAgent agent, NPCSimpleAnimator simAnim)
    {
        if (agent != null) agent.speed = _fleeSpeed;
        if (simAnim != null)
        {
            simAnim.AllowManualRotation = false;
            simAnim.EnableAutoRotation();
        }
    }

    private void FinishWillFlee()
    {
        _willAnimator?.SetFloat(HashInputMagnitude, 0f);
        if (_willCharController != null) _willCharController.enabled = true;
    }

    /// Bocadillo de Eldran gritando al grupo que corra. emphasis:true usa el sprite
    /// de bocadillo explosivo (mismo recurso que los gritos de rabia de Estela) para
    /// que se lea como un grito, no como una línea de diálogo normal.
    private void ShowEldranRunLine()
    {
        if (string.IsNullOrEmpty(_keyLineEldranRun) || _eldranTransform == null
            || SpeechBubbleUI.Instance == null) return;

        SpeechBubbleUI.Instance.Show(_eldranTransform, Loc(_keyLineEldranRun),
            duration: _lineDurationEldranRun,
            animTrigger: _animLineEldranRun,
            emphasis: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 2 — Ráfaga en primer plano
    // El primer plano se construye en runtime a partir del plano inicial
    // (_shotEstela): mismo eje de cámara, acercado a _closeupDistance de Estela.
    // Los proyectiles siguen saliendo hacia la montaña fuera de plano.
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_RageBurst()
    {
        // Volver al plano inicial y acercarlo a Estela
        _cinematicCamera.Cut(_shotEstela);
        BuildCloseupShot();

        if (_closeupShot != null)
        {
            if (_closeupBlendTime > 0f)
                yield return _cinematicCamera.MoveTo(_closeupShot, _closeupBlendTime);
            else
                _cinematicCamera.Cut(_closeupShot);
        }

        yield return new WaitForSeconds(_aimDelay);
        for (int i = 0; i < _rageShots; i++)
        {
            FireAtTarget(_mountainTarget);
            yield return new WaitForSeconds(_rageShotInterval);
        }
    }

    /// Genera el transform del primer plano: sobre el eje que une el plano inicial
    /// con Estela, a _closeupDistance del personaje, mirando a su pecho/cara.
    private void BuildCloseupShot()
    {
        if (_estelaTransform == null || _shotEstela == null) return;

        Vector3 lookPoint = _estelaTransform.position + Vector3.up * _closeupLookHeight;
        Vector3 camDir    = _shotEstela.position - lookPoint;
        camDir.Normalize();
        if (camDir.sqrMagnitude < 0.001f) return;

        if (_closeupShot == null)
            _closeupShot = new GameObject("MountainSequencer_CloseupShot").transform;

        _closeupShot.SetPositionAndRotation(
            lookPoint + camDir * _closeupDistance,
            Quaternion.LookRotation(-camDir));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 3 — Impacto
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_Impact()
    {
        yield return new WaitForSeconds(_mountainImpactDelay);
        FeedbackService.ScreenFlash(new Color(1f, 0.35f, 0.05f, 0.75f), 0.9f);
        FeedbackService.CameraShake(0.6f, 1.0f);
        // FIX: sin posición mundial (2D, no espacial). El pool 3D de AudioService (Rent3D,
        // ver AudioService.cs) aplica rolloff lineal con maxDistance = 30: al pasar
        // _mountainTarget.position (la montaña, muy por detrás de la cámara del primer plano
        // de Estela y normalmente a mucha más distancia que eso) el SFX quedaba fuera de rango
        // y no sonaba nunca. Es un golpe de impacto cinemático como el flash/shake de arriba:
        // debe oírse siempre igual de fuerte, no atenuarse por posición.
        AudioService.Instance?.PlaySFX("Mountain_Impact", 1f);
    }

    // ── Helpers — Estela congelada ────────────────────────────────────────────

    /// Con el agent desactivado, FollowPlayerState hace early-return y no puede
    /// mover ni rotar a Estela aunque Will se aleje corriendo. DisableAutoRotation
    /// evita que ApplySmoothRotation gire hacia un _targetRotation obsoleto.
    private void FreezeEstela()
    {
        if (_estelaAgent != null) _estelaAgent.enabled = false;
        _estelaSimpleAnim?.DisableAutoRotation();
        _estelaSimpleAnim?.SetMovementSpeed(0f);
    }

    private void UnfreezeEstela()
    {
        if (_estelaAgent != null) _estelaAgent.enabled = true;
        _estelaSimpleAnim?.EnableAutoRotation();
    }

    // ── Helper — disparo ──────────────────────────────────────────────────────

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
        AudioService.Instance?.PlaySFX("Mountain_FireShot", 1f, spawnPos);

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
