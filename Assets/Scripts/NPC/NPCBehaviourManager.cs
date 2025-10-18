using System;
using System.Collections;
using System.Collections.Generic;
using Alex.NPC.Common;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Alex.NPC
{
    /// <summary>
    /// Gestor centralizado que orquesta el comportamiento de cada NPC. Se apoya en módulos serializados
    /// para ambientación, misiones y combates; cada módulo decide si está activo y ejecuta su lógica.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    [RequireComponent(typeof(Interactable))]
    [DisallowMultipleComponent]
    public sealed class NPCBehaviourManager : MonoBehaviour
    {
        [Header("Ambientación")]
        [SerializeField] AmbientModule ambientModule = new();

        [Header("Misiones")]
        [SerializeField] QuestModule questModule = new();

        [Header("Reto / Combate")]
        [SerializeField] CombatModule combatModule = new();

        [Header("Debug")]
        [SerializeField] bool logDebug = false;

        INPCBehaviourModule[] _modules;

        NavMeshAgent _agent;
        NPCSimpleAnimator _animator;
        Interactable _interactable;

        Transform _player;
        Transform _playerCamera;

        PlayerActionManager _actionManager; // agregado: cache para PlayerActionManager

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<NPCSimpleAnimator>();
            _interactable = GetComponent<Interactable>();

            _modules = new INPCBehaviourModule[] { ambientModule, questModule, combatModule };
            foreach (var module in _modules)
                module?.Initialize(this);

            PlayerService.OnPlayerRegistered += HandlePlayerRegistered;
            PlayerService.OnPlayerUnregistered += HandlePlayerUnregistered;

            ResolvePlayerReferences();
            DebugLog("Awake completado. NPC listo.");
        }

        void Start()
        {
            foreach (var module in _modules)
                module?.OnStart();
        }

        void OnEnable()
        {
            foreach (var module in _modules)
                module?.OnEnable();
        }

        void OnDisable()
        {
            foreach (var module in _modules)
                module?.OnDisable();

            _animator.ResetMovement();
            NavMeshAgentUtility.SafeSetStopped(_agent, true);
        }

        void OnDestroy()
        {
            PlayerService.OnPlayerRegistered -= HandlePlayerRegistered;
            PlayerService.OnPlayerUnregistered -= HandlePlayerUnregistered;
        }

        void Update()
        {
            foreach (var module in _modules)
                module?.Tick();
        }

        /// <summary>
        /// Permite que los módulos consuman la interacción antes de que el Interactable abra un diálogo genérico.
        /// </summary>
        public bool HandleInteraction(GameObject interactor)
        {
            foreach (var module in _modules)
            {
                if (module != null && module.HandleInteraction(interactor))
                    return true;
            }
            return false;
        }

        #region Helpers accesibles desde los módulos

        internal NavMeshAgent Agent => _agent;
        internal NPCSimpleAnimator Animator => _animator;
        internal Interactable Interactable => _interactable;
        internal Transform Player => _player;
        internal Transform PlayerCamera => _playerCamera;

        internal Coroutine RunCoroutine(IEnumerator routine) => StartCoroutine(routine);
        internal void StopCoroutineSafe(Coroutine routine)
        {
            if (routine != null)
                StopCoroutine(routine);
        }

        internal bool EnsureAgentOnNavMesh(float radius) =>
            NavMeshAgentUtility.EnsureAgentOnNavMesh(_agent, transform.position, radius);

        internal bool TryGetRandomPoint(float radius, out Vector3 destination) =>
            NavMeshAgentUtility.TryGetRandomPoint(transform.position, radius, out destination);

        internal void DebugLog(string message)
        {
            if (!logDebug) return;
            Debug.Log($"[NPCBehaviourManager:{name}] {message}", this);
        }

        internal PlayerActionManager GetActionManager()
        {
            if (_actionManager == null)
                PlayerService.TryGetComponent(out _actionManager);
            return _actionManager;
        }

        internal bool IsPlayerInFov(float radius, float fov)
        {
            if (_player == null)
                return false;

            Vector3 to = _player.position - transform.position;
            to.y = 0f;

            if (to.sqrMagnitude > radius * radius)
                return false;

            float dot = Vector3.Dot(transform.forward, to.normalized);
            float fovDot = Mathf.Cos(0.5f * fov * Mathf.Deg2Rad);
            return dot >= fovDot;
        }

        internal IEnumerator WaitDialogueToClose(float timeout = 60f)
        {
            var dm = DialogueManager.Instance;
            if (dm == null)
                yield break;

            float waitForOpen = 0f;
            while (!dm.IsOpen && waitForOpen < 2f)
            {
                waitForOpen += Time.deltaTime;
                yield return null;
            }

            float waitForClose = 0f;
            while (dm.IsOpen && waitForClose < timeout)
            {
                waitForClose += Time.deltaTime;
                yield return null;
            }
        }

        internal void PlayDialogue(DialogueAsset asset, Action onComplete = null)
        {
            if (!asset)
            {
                onComplete?.Invoke();
                return;
            }

            var dm = DialogueManager.Instance;
            if (dm == null)
            {
                DebugLog($"DialogueManager no disponible para reproducir {asset.name}.");
                onComplete?.Invoke();
                return;
            }

            dm.StartDialogue(asset, transform, onComplete);
        }

        #endregion

        #region Player resolution

        internal void EnsurePlayerReference()
        {
            if (_player == null)
                ResolvePlayerReferences();
        }

        void ResolvePlayerReferences()
        {
            var previous = _player;
            _player = PlayerLocator.ResolvePlayer();
            _playerCamera = PlayerLocator.ResolvePlayerCamera();
            _animator.SetPlayer(_player, _playerCamera);
            if (_actionManager == null)
                PlayerService.TryGetComponent(out _actionManager);

            if (_player != previous)
            {
                if (_player == null)
                    DebugLog("Player no resuelto (null).");
                else
                    DebugLog($"Player resuelto → {_player.name}");
            }
        }

        void HandlePlayerRegistered(GameObject playerGo)
        {
            if (playerGo != null)
                ResolvePlayerReferences();
        }

        void HandlePlayerUnregistered()
        {
            _player = null;
            _playerCamera = null;
        }

        #endregion

        #region Module definitions

        interface INPCBehaviourModule
        {
            void Initialize(NPCBehaviourManager context);
            void OnStart();
            void OnEnable();
            void OnDisable();
            void Tick();
            bool HandleInteraction(GameObject interactor);
        }

        [Serializable]
        sealed class AmbientModule : INPCBehaviourModule
        {
            [Tooltip("Si está activo, el NPC vagará dentro del radio indicado.")]
            public bool enableWander = true;

            [Min(0f)] public float wanderRadius = 6f;
            [Min(0f)] public float minIdleTime = 1.2f;
            [Min(0f)] public float maxIdleTime = 3.0f;
            public bool pickWhileMoving = false;

            NPCBehaviourManager _ctx;
            Coroutine _wanderRoutine;

            public void Initialize(NPCBehaviourManager context)
            {
                _ctx = context;
            }

            public void OnStart()
            {
                // Nada que hacer.
            }

            public void OnEnable()
            {
                if (!enableWander)
                    return;

                _wanderRoutine ??= _ctx.RunCoroutine(WanderLoop());
            }

            public void OnDisable()
            {
                if (_wanderRoutine != null)
                {
                    _ctx.StopCoroutineSafe(_wanderRoutine);
                    _wanderRoutine = null;
                }
                _ctx.Animator.ResetMovement();
                NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
            }

            public void Tick()
            {
                // Ambient no necesita lógica por frame; el coroutine maneja el movimiento.
            }

            public bool HandleInteraction(GameObject interactor) => false;

            IEnumerator WanderLoop()
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 0.6f));

                while (_ctx.isActiveAndEnabled && enableWander)
                {
                    float idleDelay = UnityEngine.Random.Range(minIdleTime, Mathf.Max(minIdleTime, maxIdleTime));
                    if (idleDelay > 0f)
                        yield return new WaitForSeconds(idleDelay);

                    if (!_ctx.EnsureAgentOnNavMesh(wanderRadius))
                    {
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }

                    if (!_ctx.TryGetRandomPoint(wanderRadius, out var destination))
                    {
                        yield return new WaitForSeconds(0.5f);
                        continue;
                    }

                    NavMeshAgentUtility.SetDestination(_ctx.Agent, destination);

                    while (ShouldContinueWalking())
                    {
                        float speed = NavMeshAgentUtility.ComputeSpeedFactor(_ctx.Agent);
                        _ctx.Animator.SetMovementSpeed(speed);

                        if (!pickWhileMoving && _ctx.Agent.remainingDistance <= _ctx.Agent.stoppingDistance + 0.1f)
                            break;

                        yield return null;
                    }

                    NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                    _ctx.Animator.ResetMovement();
                    yield return null;
                }
            }

            bool ShouldContinueWalking()
            {
                var agent = _ctx.Agent;
                return _ctx.isActiveAndEnabled &&
                       agent != null &&
                       agent.isOnNavMesh &&
                       !agent.pathPending &&
                       agent.remainingDistance > agent.stoppingDistance + 0.1f;
            }
        }

        [Serializable]
        sealed class QuestModule : INPCBehaviourModule
        {
            public bool enable = false;
            public QuestChainEntry[] chain = Array.Empty<QuestChainEntry>();

            [Header("Detección de ítems automáticos")]
            public bool enableItemDetection = true;
            [Min(0f)] public float detectionRadius = 3f;
            [Range(0f, 180f)] public float detectionAngle = 90f;
            public LayerMask detectionLayer = ~0;
            [Min(0.05f)] public float detectionInterval = 0.33f;

            NPCBehaviourManager _ctx;
            Coroutine _scanRoutine;
            readonly Collider[] _overlapBuffer = new Collider[16];
            readonly HashSet<GameObject> _consumed = new();

            public void Initialize(NPCBehaviourManager context)
            {
                _ctx = context;
            }

            public void OnStart() { }

            public void OnEnable()
            {
                if (!enable)
                    return;

                if (enableItemDetection)
                    _scanRoutine ??= _ctx.RunCoroutine(ScanRoutine());
            }

            public void OnDisable()
            {
                if (_scanRoutine != null)
                {
                    _ctx.StopCoroutineSafe(_scanRoutine);
                    _scanRoutine = null;
                }
                _consumed.Clear();
            }

            public void Tick()
            {
                // Quest no necesita lógica por frame si no hay detección continua.
            }

            public bool HandleInteraction(GameObject interactor)
            {
                if (!enable)
                    return false;

                var qm = QuestManager.Instance;
                if (qm == null || chain.Length == 0)
                {
                    _ctx.DebugLog("QuestManager no disponible o cadena vacía.");
                    return false;
                }

                if (TryGetCurrentEntry(qm, out var entry, out int index))
                {
                    var questId = entry.questData?.questId;
                    if (string.IsNullOrEmpty(questId))
                        return false;

                    switch (qm.GetState(questId))
                    {
                        case QuestState.Inactive:
                            _ctx.PlayDialogue(entry.dlgBefore);
                            break;
                        case QuestState.Active:
                            HandleActive(entry, qm, questId, index);
                            break;
                        case QuestState.Completed:
                            _ctx.PlayDialogue(entry.dlgCompleted, () => _ctx.RunCoroutine(StartNextQuestAfterDialogue(qm, index)));
                            break;
                    }
                }
                else
                {
                    var first = chain[0];
                    _ctx.PlayDialogue(first.dlgBefore);
                }

                return true;
            }

            void HandleActive(QuestChainEntry entry, QuestManager qm, string questId, int index)
            {
                switch (entry.completionMode)
                {
                    case QuestCompletionMode.AutoCompleteOnTalk:
                        CompleteAllSteps(entry, qm, questId, index);
                        break;
                    case QuestCompletionMode.CompleteOnTalkIfStepsReady:
                        if (qm.AreAllStepsCompleted(questId))
                            FinishQuest(entry, qm, questId, index);
                        else
                            _ctx.PlayDialogue(entry.dlgInProgress);
                        break;
                    default:
                        if (qm.AreAllStepsCompleted(questId))
                            FinishQuest(entry, qm, questId, index);
                        else
                            _ctx.PlayDialogue(entry.dlgInProgress);
                        break;
                }
            }

            void CompleteAllSteps(QuestChainEntry entry, QuestManager qm, string questId, int index)
            {
                foreach (var request in qm.GetAll())
                {
                    if (request.Id != questId) continue;
                    var steps = request.Steps;
                    if (steps == null) break;
                    for (int i = 0; i < steps.Length; i++)
                        if (!steps[i].completed) qm.MarkStepDone(questId, i);
                    break;
                }

            FinishQuest(entry, qm, questId, index);
        }

            void FinishQuest(QuestChainEntry entry, QuestManager qm, string questId, int index)
            {
                qm.CompleteQuest(questId);
                entry.onQuestCompleted?.Invoke();

                _ctx.PlayDialogue(entry.dlgTurnIn, () => _ctx.RunCoroutine(StartNextQuestAfterDialogue(qm, index)));
            }

            IEnumerator StartNextQuestAfterDialogue(QuestManager qm, int currentIndex)
            {
                yield return null;

                for (int nextIndex = currentIndex + 1; nextIndex < chain.Length; nextIndex++)
                {
                    var entry = chain[nextIndex];
                    var nextId = entry.questData ? entry.questData.questId : null;
                    if (string.IsNullOrEmpty(nextId)) continue;

                    var state = qm.GetState(nextId);
                    if (state == QuestState.Completed) continue;

                    if (state == QuestState.Inactive)
                    {
                        qm.AddQuest(entry.questData);
                        qm.StartQuest(nextId);
                        if (entry.dlgBefore) _ctx.PlayDialogue(entry.dlgBefore);
                    }
                    else if (state == QuestState.Active)
                    {
                        if (entry.dlgInProgress) _ctx.PlayDialogue(entry.dlgInProgress);
                    }
                    yield break;
                }

                _ctx.DebugLog("Cadena de quests completada.");
            }

            IEnumerator ScanRoutine()
            {
                var wait = new WaitForSeconds(detectionInterval);
                while (_ctx.isActiveAndEnabled && enable && enableItemDetection)
                {
                    TryDetectItems();
                    yield return wait;
                }
            }

            void TryDetectItems()
            {
                var qm = QuestManager.Instance;
                if (qm == null)
                    return;

                if (!TryGetCurrentEntry(qm, out var entry, out int index))
                    return;

                if (!entry.autoDetectItemDelivery || entry.questData == null)
                    return;

                if (qm.GetState(entry.questData.questId) != QuestState.Active)
                    return;

                int hits = Physics.OverlapSphereNonAlloc(_ctx.transform.position, detectionRadius,
                    _overlapBuffer, detectionLayer, QueryTriggerInteraction.Collide);

                if (hits <= 0) return;

                Vector3 origin = _ctx.transform.position;
                Vector3 forward = _ctx.transform.forward;
                float halfAngle = detectionAngle * 0.5f;
                float radiusSqr = detectionRadius * detectionRadius;

                for (int i = 0; i < hits; i++)
                {
                    var collider = _overlapBuffer[i];
                    if (!collider) continue;

                    var go = collider.attachedRigidbody ? collider.attachedRigidbody.gameObject : collider.gameObject;
                    if (!go || _consumed.Contains(go))
                        continue;

                    if (!string.IsNullOrEmpty(entry.itemTag) && !go.CompareTag(entry.itemTag))
                        continue;

                    Vector3 dir = go.transform.position - origin;
                    if (dir.sqrMagnitude > radiusSqr)
                        continue;

                    if (Vector3.Angle(forward, dir) > halfAngle)
                        continue;

                    if (IsHeldByPlayer(go))
                        continue;

                    OnItemDetected(go, entry, qm, index);
                }
            }

            void OnItemDetected(GameObject item, QuestChainEntry entry, QuestManager qm, int index)
            {
                _consumed.Add(item);
                UnityEngine.Object.Destroy(item);

                string questId = entry.questData.questId;
                int stepsCount = GetStepsCount(qm, questId);

                if (stepsCount == 0)
                {
                    FinishQuest(entry, qm, questId, index);
                    return;
                }

                int step = Mathf.Clamp(entry.itemDeliveryStepIndex, 0, stepsCount - 1);
                qm.MarkStepDone(questId, step);

                if (qm.AreAllStepsCompleted(questId))
                    FinishQuest(entry, qm, questId, index);
            }

            bool TryGetCurrentEntry(QuestManager qm, out QuestChainEntry entry, out int index)
            {
                for (int i = chain.Length - 1; i >= 0; i--)
                {
                    var candidate = chain[i];
                    if (!candidate.questData) continue;
                    var state = qm.GetState(candidate.questData.questId);
                    if (state == QuestState.Active || state == QuestState.Completed)
                    {
                        entry = candidate;
                        index = i;
                        return true;
                    }
                }

                entry = null;
                index = -1;
                return false;
            }

            bool IsHeldByPlayer(GameObject item)
            {
                if (_ctx.Player == null)
                    return false;

                Transform parent = item.transform.parent;
                while (parent != null)
                {
                    if (parent == _ctx.Player)
                        return true;
                    parent = parent.parent;
                }
                return false;
            }

            int GetStepsCount(QuestManager qm, string questId)
            {
                foreach (var request in qm.GetAll())
                {
                    if (request.Id == questId)
                        return request.Steps?.Length ?? 0;
                }
                return 0;
            }

            [Serializable]
            public class QuestChainEntry
            {
                [Tooltip("Quest correspondiente a esta etapa.")]
                public QuestData questData;

                [Tooltip("Modo de completado de la quest.")]
                public QuestCompletionMode completionMode = QuestCompletionMode.Manual;

                [Header("Detección de objetos")]
                public bool autoDetectItemDelivery = false;
                public int itemDeliveryStepIndex = 1;
                public string itemTag = "Untagged";

                [Header("Diálogos")]
                public DialogueAsset dlgBefore;
                public DialogueAsset dlgInProgress;
                public DialogueAsset dlgTurnIn;
                public DialogueAsset dlgCompleted;

                [Header("Eventos")]
                public UnityEvent onQuestCompleted;
            }
        }

        [Serializable]
        sealed class CombatModule : INPCBehaviourModule
        {
            public bool enable = false;

            [Header("Detección")]
            [Min(0f)] public float sightRadius = 8f;
            [Range(1f, 180f)] public float fovDegrees = 120f;

            [Header("Aproximación")]
            public float challengeStopDistance = 2.2f;
            public float approachRepathInterval = 0.25f;
            public float loseSightGraceSeconds = 1.5f;

            [Header("Animaciones")]
            public string challengeAlertState = "SenseSomethingStart_NoWeapon";
            public float challengeAlertMinSeconds = 0.75f;
            public string challengeState = "Challenging_NoWeapon";

            [Header("UI / Feedback")]
            public GameObject exclamationPrefab;
            public Vector3 exclamationOffset = new Vector3(0f, 2f, 0f);
            public float exclamationSeconds = 2f;

            [Header("Bloqueo de jugador")]
            public bool lockPlayer = true;
            public bool lockOnSight = true;
            public ActionMode lockMode = ActionMode.Stunned;

            [Header("Giro estilo 'entrenador Pokémon'")]
            public bool turnPlayerOnSight = true;     // ← NUEVO: activa el giro
            [Min(0f)] public float turnDelaySeconds = 1.0f;   // ← NUEVO: espera antes de girar
            [Min(0f)] public float turnDurationSeconds = 0.35f; // ← NUEVO: cuánto tarda en girar

            [Header("Fallback")]
            [TextArea] public string fallbackDialogue;

            public UnityEvent onChallengeStarted;
            public UnityEvent onPlayerLock;
            public UnityEvent onPlayerUnlock;
            public StringEvent onDialogueRequest;

            NPCBehaviourManager _ctx;
            Coroutine _challengeRoutine;
            Coroutine _turnRoutine;   
            bool _isChallenging;
            bool _lockModeApplied;
            bool _playerLockEventRaised;

            public void Initialize(NPCBehaviourManager context) => _ctx = context;
            public void OnStart() { }

            public void OnEnable()
            {
                _isChallenging = false;
                _lockModeApplied = false;
                _playerLockEventRaised = false;
                if (exclamationPrefab) exclamationPrefab.SetActive(false);
            }

            public void OnDisable()
            {
                if (_challengeRoutine != null)
                {
                    _ctx.StopCoroutineSafe(_challengeRoutine);
                    _challengeRoutine = null;
                }
                if (_turnRoutine != null) { _ctx.StopCoroutineSafe(_turnRoutine); _turnRoutine = null; } // ← NUEVO
                _ctx.Animator.ResetMovement();
                NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                ReleasePlayer();
                if (exclamationPrefab) exclamationPrefab.SetActive(false);
            }

            public void Tick()
            {
                if (!enable || _isChallenging || sightRadius <= 0f) return;

                _ctx.EnsurePlayerReference();
                if (_ctx.Player == null) return;
                if (!_ctx.IsPlayerInFov(sightRadius, fovDegrees)) return;

                // Bloqueo inmediato
                if (lockPlayer && lockOnSight && !_lockModeApplied)
                    ApplyLock();

                // ← NUEVO: programa el giro si procede y aún no se ha lanzado
                if (turnPlayerOnSight && _turnRoutine == null && _lockModeApplied)
                    _turnRoutine = _ctx.RunCoroutine(TurnPlayerAfterDelay());

                if (_challengeRoutine == null)
                    _challengeRoutine = _ctx.RunCoroutine(ChallengeFlow());
            }

            public bool HandleInteraction(GameObject interactor) => false;

            IEnumerator ChallengeFlow()
            {
                _isChallenging = true;
                _ctx.DebugLog("ChallengeFlow iniciado.");

                // Asegura bloqueo incluso si lockOnSight = false
                if (lockPlayer && !_lockModeApplied)
                    ApplyLock();
                
                if (turnPlayerOnSight && _turnRoutine == null && _lockModeApplied)
                    _turnRoutine = _ctx.RunCoroutine(TurnPlayerAfterDelay());

                if (exclamationPrefab) exclamationPrefab.SetActive(true);

                NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                _ctx.Animator.ResetMovement();

                // Animación de “alerta”
                float alertTimer = 0f;
                float alertDuration = Mathf.Max(challengeAlertMinSeconds, 0.05f);

                if (!string.IsNullOrEmpty(challengeAlertState))
                    _ctx.Animator.PlayOneShot(challengeAlertState);

                while (alertTimer < alertDuration)
                {
                    if (_ctx.Player == null || !_ctx.IsPlayerInFov(sightRadius, fovDegrees))
                        break;

                    alertTimer += Time.deltaTime;
                    yield return null;
                }

                if (_ctx.Player == null)
                {
                    CleanupAndRelease("Challenge cancelado: player null tras alerta.");
                    yield break;
                }

                // Aproximación
                float repathTimer = 0f;
                float loseSightTimer = 0f;
                float iconTimer = 0f;

                while (true)
                {
                    if (_ctx.Player == null)
                    {
                        CleanupAndRelease("Challenge cancelado: player perdido durante aproximación.");
                        yield break;
                    }

                    if (exclamationPrefab && exclamationSeconds > 0f)
                    {
                        iconTimer += Time.deltaTime;
                        if (iconTimer >= exclamationSeconds)
                            exclamationPrefab.SetActive(false);
                    }

                    float distance = Vector3.Distance(_ctx.transform.position, _ctx.Player.position);
                    if (distance <= challengeStopDistance)
                        break;

                    if (!_ctx.IsPlayerInFov(sightRadius, fovDegrees))
                    {
                        loseSightTimer += Time.deltaTime;
                        if (loseSightTimer >= loseSightGraceSeconds)
                        {
                            CleanupAndRelease("Challenge cancelado: jugador fuera de visión durante aproximación.");
                            yield break;
                        }
                    }
                    else
                    {
                        loseSightTimer = 0f;
                    }

                    repathTimer -= Time.deltaTime;
                    if (repathTimer <= 0f)
                    {
                        if (_ctx.EnsureAgentOnNavMesh(sightRadius))
                            NavMeshAgentUtility.SetDestination(_ctx.Agent, _ctx.Player.position, challengeStopDistance);
                        repathTimer = approachRepathInterval;
                    }

                    float speed = NavMeshAgentUtility.ComputeSpeedFactor(_ctx.Agent);
                    _ctx.Animator.SetMovementSpeed(speed);
                    yield return null;
                }

                if (exclamationPrefab) exclamationPrefab.SetActive(false);
                _ctx.Animator.ResetMovement();

                if (!string.IsNullOrEmpty(challengeState))
                    _ctx.Animator.PlayOneShot(challengeState);

                // Dispara la interacción o fallback
                if (_ctx.Interactable && _ctx.Player)
                {
                    _ctx.Interactable.Interact(_ctx.Player.gameObject);
                    _ctx.DebugLog("Interactable disparado; esperando cierre de diálogo.");
                }
                else if (!string.IsNullOrWhiteSpace(fallbackDialogue))
                {
                    onDialogueRequest?.Invoke(fallbackDialogue);
                    _ctx.DebugLog("FallbackDialogue disparado.");
                }

                // Espera a que se cierre diálogo
                yield return _ctx.RunCoroutine(_ctx.WaitDialogueToClose());

                onChallengeStarted?.Invoke();
                _ctx.DebugLog("OnChallengeStarted invocado.");

                NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                _ctx.Animator.ResetMovement();

                // Libera al jugador tras el reto/diálogo
                ReleasePlayer();

                _isChallenging = false;
                _challengeRoutine = null;
            }

            void ApplyLock()
            {
                onPlayerLock?.Invoke();
                _playerLockEventRaised = true;

                var pam = _ctx.GetActionManager();
                if (pam != null)
                {
                    pam.PushMode(lockMode);
                    _lockModeApplied = true;
                }
                else
                {
                    _ctx.DebugLog("PlayerActionManager no disponible para aplicar lock.");
                }
            }

            void CleanupAndRelease(string reason)
            {
                if (exclamationPrefab) exclamationPrefab.SetActive(false);
                if (_turnRoutine != null) { _ctx.StopCoroutineSafe(_turnRoutine); _turnRoutine = null; } // ← NUEVO
                ReleasePlayer();
                _isChallenging = false;
                _challengeRoutine = null;
                _ctx.Animator.ResetMovement();
                NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                _ctx.DebugLog(reason);
            }

            void ReleasePlayer()
            {
                if (!lockPlayer) return;

                var pam = _ctx.GetActionManager();
                if (_lockModeApplied && pam != null)
                {
                    pam.PopMode(lockMode);
                    _lockModeApplied = false;
                }
                if (_turnRoutine != null) { _ctx.StopCoroutineSafe(_turnRoutine); _turnRoutine = null; } // ← NUEVO

                if (_playerLockEventRaised)
                {
                    onPlayerUnlock?.Invoke();
                    _playerLockEventRaised = false;
                }
            }
            
            IEnumerator TurnPlayerAfterDelay()
            {
                // Espera el retardo, pero aborta si perdemos el lock o el player
                float t = 0f;
                while (t < turnDelaySeconds)
                {
                    if (!_lockModeApplied || _ctx.Player == null) { _turnRoutine = null; yield break; }
                    t += Time.deltaTime;
                    yield return null;
                }

                // Calcula rotación objetivo mirando al NPC en plano horizontal
                Transform player = _ctx.Player;
                Vector3 toNpc = _ctx.transform.position - player.position;
                toNpc.y = 0f;
                if (toNpc.sqrMagnitude < 0.0001f) { _turnRoutine = null; yield break; }

                Quaternion start = player.rotation;
                Quaternion target = Quaternion.LookRotation(toNpc.normalized, Vector3.up);

                // Slerp suave (ease in-out) durante turnDurationSeconds, solo si seguimos bloqueados
                float dur = Mathf.Max(0.0001f, turnDurationSeconds);
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    if (!_lockModeApplied || _ctx.Player == null) { _turnRoutine = null; yield break; }
                    elapsed += Time.deltaTime;
                    float u = Mathf.Clamp01(elapsed / dur);
                    // ease in-out (smoothstep)
                    u = u * u * (3f - 2f * u);
                    player.rotation = Quaternion.Slerp(start, target, u);
                    yield return null;
                }

                player.rotation = target;
                _turnRoutine = null;
            }
        }

        #endregion

        [Serializable]
        public class StringEvent : UnityEvent<string> { }
    }
}
