using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;
using Game.NPC.Common;

namespace Game.NPC
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
        // Manejador simple para poder devolver un token desde RunRoutine que represente
        // una coroutine incluso si no se ha arrancado todavía (por ejemplo cuando el GO está inactivo).
        internal sealed class RoutineHandle
        {
            public IEnumerator Enumerator { get; }
            public Coroutine RunningCoroutine { get; set; }
            public bool IsStarted => RunningCoroutine != null;
            public RoutineHandle(IEnumerator enumerator, Coroutine running = null)
            {
                Enumerator = enumerator;
                RunningCoroutine = running;
            }
        }

        // Lista de rutinas pendientes que se encolan mientras este component está inactivo
        readonly List<RoutineHandle> _pendingRoutines = new();

        [Header("Ambientación")]
        [SerializeField] AmbientModule ambientModule = new();

        [Header("Misiones")]
        [SerializeField] QuestModule questModule = new();

        [Header("Reto / Combate")]
        [SerializeField] CombatModule combatModule = new();

        [Header("Debug")]
        [SerializeField] bool logDebug = false;

        [Header("Física")]
        [Tooltip("Si hay un Rigidbody en el NPC lo dejará en modo cinemático para evitar empujones físicos.")]
        [SerializeField] bool forceKinematicRigidbody = true;
        [SerializeField] RigidbodyConstraints rigidbodyConstraints = RigidbodyConstraints.FreezeRotation;

        INPCBehaviourModule[] _modules;

        NavMeshAgent _agent;
        NPCSimpleAnimator _animator;
        Interactable _interactable;
        Rigidbody _rigidbody;

        Transform _player;
        Transform _playerCamera;
        Animator _playerAnimator;

        PlayerActionManager _actionManager; // agregado: cache para PlayerActionManager

        static readonly int PlayerInputMagnitudeHash = UnityEngine.Animator.StringToHash("InputMagnitude");

        [Header("Persistencia (opcional)")]
        [Tooltip("Si está activado, este NPC guardará/restaurará su última posición mediante el sistema de guardado.")]
        public bool persistLastPosition = false;
        [Tooltip("Última posición conocida a persistir/restaurar. Se actualiza cuando el NPC se recoloca por misión o lógica explícita.")]
        public Vector3 lastPosition;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<NPCSimpleAnimator>();
            _interactable = GetComponent<Interactable>();

            if (forceKinematicRigidbody && TryGetComponent(out _rigidbody))
            {
                _rigidbody.isKinematic = true;
                _rigidbody.constraints = rigidbodyConstraints;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

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

            // Restauración inicial si procede
            ApplyLastPositionIfNeeded();
        }

        void OnEnable()
        {
            foreach (var module in _modules)
                module?.OnEnable();

            // Arrancar cualquier rutina que se encoló mientras estábamos inactivos
            if (_pendingRoutines.Count > 0)
            {
                for (int i = _pendingRoutines.Count - 1; i >= 0; i--)
                {
                    var h = _pendingRoutines[i];
                    if (h != null && h.RunningCoroutine == null && h.Enumerator != null)
                    {
                        try
                        {
                            h.RunningCoroutine = StartCoroutine(h.Enumerator);
                            Debug.Log($"[NPCBehaviourManager] Rutina arrancada desde OnEnable: {h.Enumerator}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[NPCBehaviourManager] No se pudo arrancar rutina en OnEnable: {ex.Message}");
                        }
                    }
                    _pendingRoutines.RemoveAt(i);
                }
            }
        }

        void OnDisable()
        {
            foreach (var module in _modules)
                module?.OnDisable();

            _animator.ResetMovement();
            NavMeshAgentUtility.HardStop(_agent);
        }

        // API pública para actualizar la última posición cuando la lógica externa
        // mueva al NPC (por ejemplo, al completar misión y cambiar de punto del mapa)
        public void SetLastPosition(Vector3 worldPosition)
        {
            lastPosition = worldPosition;
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
        [Header("Interacción (opcional)")]
        [Tooltip("Si está activo, el NPC girará suavemente hacia el jugador al iniciar la interacción.")]
        [SerializeField] bool rotateOnInteract = false;
        [SerializeField, Min(0f)] float rotateOnInteractSeconds = 0.3f;

        public bool HandleInteraction(GameObject interactor)
        {
            // Giro opcional y no intrusivo
            if (rotateOnInteract)
                StartSmoothFaceTowardsPlayer(Mathf.Max(0.05f, rotateOnInteractSeconds));
            foreach (var module in _modules)
            {
                if (module != null && module.HandleInteraction(interactor))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Lanza el proyectil configurado para la fase de combate de NPCs.
        /// Útil para ser invocado desde eventos de animación.
        /// </summary>
        public void FireCombatProjectile() => combatModule?.FireProjectile();

        #region Helpers accesibles desde los módulos

        internal NavMeshAgent Agent => _agent;
        internal NPCSimpleAnimator Animator => _animator;
        internal Interactable Interactable => _interactable;
        internal Transform Player => _player;
        internal Transform PlayerCamera => _playerCamera;

        // Inicia una rutina; si el GO está inactivo, se encola y se devolverá un RoutineHandle válido
        // que podrá utilizarse para detenerla más tarde.
        internal RoutineHandle RunCoroutine(IEnumerator routine)
        {
            if (isActiveAndEnabled)
            {
                var c = StartCoroutine(routine);
                return new RoutineHandle(routine, c);
            }
            else
            {
                var h = new RoutineHandle(routine, null);
                _pendingRoutines.Add(h);
                Debug.Log($"[NPCBehaviourManager] Rutina encolada: {routine}");
                return h;
            }
        }

        internal void StopCoroutineSafe(RoutineHandle handle)
        {
            if (handle == null) return;
            // Si ya está en ejecución, detener la coroutine asociada
            if (handle.RunningCoroutine != null)
            {
                try { StopCoroutine(handle.RunningCoroutine); } catch { }
                handle.RunningCoroutine = null;
                return;
            }
            // Si estaba pendiente, removerla de la cola
            if (_pendingRoutines.Contains(handle))
                _pendingRoutines.Remove(handle);
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

        // Permite aplicar la última posición después de una carga cuando el valor se reinyecta tras Start().
        public void ApplyLastPositionIfNeeded()
        {
            if (!persistLastPosition) return;
            if (lastPosition == default) return;

            transform.position = lastPosition;
            EnsureAgentOnNavMesh(2f);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                _agent.Warp(transform.position);
        }

        Coroutine _faceRoutine;

        // Inicia un giro suave para mirar al jugador sin tocar NavMeshAgent ni Animator
        public void StartSmoothFaceTowardsPlayer(float duration = 0.3f)
        {
            EnsurePlayerReference();
            if (_player == null) return;
            if (_faceRoutine != null) { StopCoroutine(_faceRoutine); _faceRoutine = null; }
            _faceRoutine = StartCoroutine(FaceTowardsRoutine(duration));
        }

        IEnumerator FaceTowardsRoutine(float duration)
        {
            if (_player == null) yield break;
            Vector3 dir = _player.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) yield break;

            Quaternion start = transform.rotation;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            float t = 0f; duration = Mathf.Max(0.05f, duration);
            while (t < duration)
            {
                // Unscaled para no quedar bloqueado si el diálogo pausa el juego
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // easing suave (ease in-out)
                u = u * u * (3f - 2f * u);
                transform.rotation = Quaternion.Slerp(start, target, u);
                yield return null;
            }
            transform.rotation = target;
            _faceRoutine = null;
        }

        internal PlayerActionManager GetActionManager()
        {
            if (_actionManager == null)
                PlayerService.TryGetComponent(out _actionManager);
            return _actionManager;
        }

        internal void ForcePlayerIdle()
        {
            var pam = GetActionManager();
            if (pam == null)
                return;

            if (_playerAnimator == null || _playerAnimator.gameObject != pam.gameObject)
                _playerAnimator = pam.GetComponent<Animator>();

            _playerAnimator?.SetFloat(PlayerInputMagnitudeHash, 0f);
            ResetPlayerMotion();
        }

        void ResetPlayerMotion()
        {
            if (_player == null)
                return;

            // Intenta limpiar input residual del controlador para evitar que el
            // personaje salga corriendo al devolver el control tras un reto
            var inputType = Type.GetType("Invector.vCharacterController.vThirdPersonInput, Assembly-CSharp-firstpass", false)
                             ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Assembly-CSharp", false)
                             ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Invector-3rdPersonController_LITE", false)
                             ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Invector-3rdPersonController", false)
                             ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput", false);

            if (inputType != null)
            {
                var input = _player.GetComponent(inputType) ?? _player.GetComponentInChildren(inputType, true);
                if (input != null)
                {
                    TrySetField(inputType, input, "moveInput", Vector2.zero);
                    TrySetField(inputType, input, "cameraInput", Vector2.zero);
                    TrySetField(inputType, input, "sprintHeld", false);
                    TrySetField(inputType, input, "jumpPressed", false);
                    TrySetField(inputType, input, "strafePressed", false);
                }
            }

            // Reflejo defensivo para no depender directamente de Invector en tiempo de compilación
            var controllerType = Type.GetType("Invector.vCharacterController.vThirdPersonController, Invector-3rdPersonController_LITE", false)
                                ?? Type.GetType("Invector.vCharacterController.vThirdPersonController, Invector-3rdPersonController", false)
                                ?? Type.GetType("Invector.vCharacterController.vThirdPersonController", false);

            if (controllerType != null)
            {
                var controller = _player.GetComponent(controllerType) ?? _player.GetComponentInChildren(controllerType, true);
                if (controller != null)
                {
                    var moveDir = controllerType.GetField("moveDirection", BindingFlags.NonPublic | BindingFlags.Instance);
                    moveDir?.SetValue(controller, Vector3.zero);

                    var extraImpulse = controllerType.GetField("extraImpulse", BindingFlags.NonPublic | BindingFlags.Instance);
                    extraImpulse?.SetValue(controller, Vector3.zero);
                }
            }

            if (_player.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        static void TrySetField<T>(Type type, object instance, string fieldName, T value)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(instance, value);
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
                waitForOpen += Time.unscaledDeltaTime;
                yield return null;
            }

            float waitForClose = 0f;
            while (dm.IsOpen && waitForClose < timeout)
            {
                waitForClose += Time.unscaledDeltaTime;
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

            // Iniciar animación de interactuar igual que hace el Interactable (OnStarted/OnFinished)
            if (_animator != null && _animator.isActiveAndEnabled && gameObject.activeInHierarchy)
                _animator.BeginInteraction();
            dm.StartDialogue(asset, transform, () =>
            {
                try { onComplete?.Invoke(); }
                finally
                {
                    if (_animator != null && _animator.isActiveAndEnabled && gameObject.activeInHierarchy)
                        _animator.EndInteraction();
                }
            });
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
            _playerAnimator = null;
            _actionManager = null;
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
            RoutineHandle _wanderRoutine;
             
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

                if (_wanderRoutine == null)
                    _wanderRoutine = _ctx.RunCoroutine(WanderLoop());
             }

             public void OnDisable()
             {
                 if (_wanderRoutine != null)
                 {
                    _ctx.StopCoroutineSafe(_wanderRoutine);
                    _wanderRoutine = null;
                 }
                 _ctx.Animator.ResetMovement();
                 NavMeshAgentUtility.HardStop(_ctx.Agent);
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

                    float stuckTimer = 0f;
                    Vector3 lastPosition = _ctx.transform.position;

                    while (ShouldContinueWalking())
                    {
                        if (IsPathBlocked())
                            break;

                        float speed = NavMeshAgentUtility.ComputeSpeedFactor(_ctx.Agent);
                        _ctx.Animator.SetMovementSpeed(speed);

                        if (!pickWhileMoving && _ctx.Agent.remainingDistance <= _ctx.Agent.stoppingDistance + 0.1f)
                            break;

                        if (HasStalled(ref lastPosition, ref stuckTimer))
                            break;

                        yield return null;
                    }

                    NavMeshAgentUtility.HardStop(_ctx.Agent);
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

            bool IsPathBlocked()
            {
                var agent = _ctx.Agent;
                if (agent == null)
                    return true;

                return agent.pathStatus == NavMeshPathStatus.PathPartial ||
                       agent.pathStatus == NavMeshPathStatus.PathInvalid;
            }

            bool HasStalled(ref Vector3 lastPosition, ref float stuckTimer)
            {
                var pos = _ctx.transform.position;
                if ((pos - lastPosition).sqrMagnitude <= 0.0004f)
                {
                    stuckTimer += Time.deltaTime;
                    return stuckTimer > 1.5f;
                }

                lastPosition = pos;
                stuckTimer = 0f;
                return false;
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
            RoutineHandle _scanRoutine;
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
                {
                    // Chequeo de configuración útil en edición/runtime para detectar entradas mal configuradas
                    for (int i = 0; i < chain.Length; i++)
                    {
                        var entry = chain[i];
                        if (entry == null) continue;
                        if (!entry.autoDetectItemDelivery && (!string.IsNullOrEmpty(entry.itemTag) || entry.itemDeliveryStepIndex != 1))
                        {
                            _ctx.DebugLog($"QuestModule: chain[{i}] '{entry.questData?.questId}' tiene itemTag='{entry.itemTag}' o itemDeliveryStepIndex={entry.itemDeliveryStepIndex} pero autoDetectItemDelivery está DESACTIVADO. Si esperas detección automática, habilítalo en el inspector.");
                        }
                    }

                    if (_scanRoutine == null)
                        _scanRoutine = _ctx.RunCoroutine(ScanRoutine());
                }
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
                        // Verificar inventario antes de autocompletar
                        if (CheckInventoryRequirement(entry, out var inventoryAuto))
                        {
                            CompleteAllSteps(entry, qm, questId, index, inventoryAuto);
                        }
                        else
                        {
                            _ctx.PlayDialogue(entry.dlgInProgress);
                        }
                        break;
                    case QuestCompletionMode.CompleteOnTalkIfStepsReady:
                        // Verificar inventario primero
                        if (!CheckInventoryRequirement(entry, out var inventorySteps))
                        {
                            _ctx.PlayDialogue(entry.dlgInProgress);
                            break;
                        }
                        // Si tiene el ítem, verificar steps
                        if (qm.AreAllStepsCompleted(questId))
                        {
                            FinishQuest(entry, qm, questId, index, inventorySteps);
                        }
                        else
                        {
                            _ctx.PlayDialogue(entry.dlgInProgress);
                        }
                        break;
                    default:
                        // Verificar inventario primero
                        if (!CheckInventoryRequirement(entry, out var inventoryManual))
                        {
                            _ctx.DebugLog($"HandleActive(Manual): NO tiene el ítem requerido → dlgInProgress");
                            _ctx.PlayDialogue(entry.dlgInProgress);
                            break;
                        }
                        
                        _ctx.DebugLog($"HandleActive(Manual): SÍ tiene el ítem en inventario");
                        
                        // Si tiene verificación de inventario activada, el ítem es suficiente para completar
                        // (los steps son opcionales para mostrar objetivos, pero no bloquean la entrega)
                        if (entry.requireItemInInventory)
                        {
                            _ctx.DebugLog($"HandleActive(Manual): Tiene el ítem → completar todos los steps automáticamente");
                            CompleteAllSteps(entry, qm, questId, index, inventoryManual);
                        }
                        else
                        {
                            // Sin verificación de inventario, sí necesita completar steps manualmente
                            _ctx.DebugLog($"HandleActive(Manual): Sin verificación de inventario → verificar steps");
                            bool allStepsCompleted = qm.AreAllStepsCompleted(questId);
                            _ctx.DebugLog($"HandleActive(Manual): AreAllStepsCompleted('{questId}') = {allStepsCompleted}");
                            if (allStepsCompleted)
                            {
                                _ctx.DebugLog($"HandleActive(Manual): FinishQuest!");
                                FinishQuest(entry, qm, questId, index, inventoryManual);
                            }
                            else
                            {
                                _ctx.DebugLog($"HandleActive(Manual): Steps incompletos → dlgInProgress");
                                _ctx.PlayDialogue(entry.dlgInProgress);
                            }
                        }
                        break;
                }
            }

            void CompleteAllSteps(QuestChainEntry entry, QuestManager qm, string questId, int index, Inventory playerInventory = null)
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

            FinishQuest(entry, qm, questId, index, playerInventory);
        }

            void FinishQuest(QuestChainEntry entry, QuestManager qm, string questId, int index, Inventory playerInventory = null)
            {
                // Consumir el ítem del inventario si es necesario
                if (playerInventory != null)
                {
                    ConsumeInventoryItem(entry, playerInventory);
                }

                _ctx.DebugLog($"FinishQuest → {questId}");
                qm.CompleteQuest(questId);
                entry.onQuestCompleted?.Invoke();

                // Si no hay diálogo de entrega, avanzar inmediatamente en la cadena (evita panel vacío)
                if (entry.dlgTurnIn)
                    _ctx.PlayDialogue(entry.dlgTurnIn, () => _ctx.RunCoroutine(StartNextQuestAfterDialogue(qm, index)));
                else
                    _ctx.RunCoroutine(StartNextQuestAfterDialogue(qm, index));
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
                {
                    _ctx.DebugLog("Detección: QuestManager null");
                    return;
                }

                if (!TryGetCurrentEntry(qm, out var entry, out int index))
                {
                    _ctx.DebugLog("Detección: no hay entrada activa en la cadena");
                    return;
                }

                if (!entry.autoDetectItemDelivery || entry.questData == null)
                {
                    _ctx.DebugLog("Detección: autoDetect desactivado o questData null");
                    return;
                }

                if (qm.GetState(entry.questData.questId) != QuestState.Active)
                {
                    _ctx.DebugLog($"Detección: quest '{entry.questData.questId}' no está Active");
                    return;
                }

                float useRadius = (entry.overrideDetectionRadius > 0f) ? entry.overrideDetectionRadius : detectionRadius;
                int hits = Physics.OverlapSphereNonAlloc(_ctx.transform.position, useRadius,
                    _overlapBuffer, detectionLayer, QueryTriggerInteraction.Collide);

                if (hits <= 0) { _ctx.DebugLog("Detección: no hay colliders en radio"); return; }

                Vector3 origin = _ctx.transform.position;
                Vector3 forward = _ctx.transform.forward;
                float halfAngle = detectionAngle * 0.5f;
                float radiusSqr = useRadius * useRadius;

                for (int i = 0; i < hits; i++)
                {
                    var collider = _overlapBuffer[i];
                    if (!collider) continue;

                    var go = collider.attachedRigidbody ? collider.attachedRigidbody.gameObject : collider.gameObject;
                    if (!go || _consumed.Contains(go))
                        continue;

                    if (!string.IsNullOrEmpty(entry.itemTag) && !go.CompareTag(entry.itemTag))
                    { _ctx.DebugLog($"Detección: descarta '{go.name}' por tag != '{entry.itemTag}'"); continue; }

                    Vector3 dir = go.transform.position - origin;
                    if (dir.sqrMagnitude > radiusSqr)
                    { _ctx.DebugLog($"Detección: '{go.name}' fuera de radio"); continue; }

                    if (!entry.ignoreFovForItem && Vector3.Angle(forward, dir) > halfAngle)
                    { _ctx.DebugLog($"Detección: '{go.name}' fuera de FOV"); continue; }

                    if (IsHeldByPlayer(go))
                    { _ctx.DebugLog($"Detección: '{go.name}' ignorado (lo lleva el jugador)"); continue; }

                    _ctx.DebugLog($"Detección: item '{go.name}' válido → entregar");
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

            bool CheckInventoryRequirement(QuestChainEntry entry, out Inventory playerInventory)
            {
                playerInventory = null;

                if (!entry.requireItemInInventory)
                {
                    _ctx.DebugLog("CheckInventoryRequirement: requireItemInInventory está DESACTIVADO → devuelve TRUE");
                    return true;
                }

                // Obtener el inventario del jugador usando PlayerService
                if (!PlayerService.TryGetComponent<Inventory>(out playerInventory, includeInactive: false, allowSceneLookup: true))
                {
                    _ctx.DebugLog("CheckInventoryRequirement: No se pudo obtener el Inventory del jugador.");
                    return false;
                }

                // Determinar qué ítem buscar
                string itemId = null;
                if (entry.requiredItem != null)
                {
                    itemId = entry.requiredItem.itemId;
                    _ctx.DebugLog($"CheckInventoryRequirement: Usando requiredItem.itemId = '{itemId}'");
                }

                if (string.IsNullOrEmpty(itemId))
                {
                    _ctx.DebugLog("CheckInventoryRequirement: requireItemInInventory está activado pero requiredItem es null o no tiene itemId.");
                    return false;
                }

                // Log del inventario actual
                var snapshot = playerInventory.GetSaveSnapshot();
                if (snapshot != null && snapshot.Count > 0)
                {
                    _ctx.DebugLog($"CheckInventoryRequirement: Inventario tiene {snapshot.Count} ítems:");
                    foreach (var item in snapshot)
                    {
                        _ctx.DebugLog($"  - ItemId='{item.itemId}' Count={item.count}");
                    }
                }
                else
                {
                    _ctx.DebugLog("CheckInventoryRequirement: El inventario está VACÍO.");
                }

                // Verificar si el jugador tiene el ítem en la cantidad requerida
                bool hasItem = playerInventory.HasItem(itemId, entry.requiredAmount);
                _ctx.DebugLog($"CheckInventoryRequirement: Buscando '{itemId}' x{entry.requiredAmount} → {(hasItem ? "✅ SÍ LO TIENE" : "❌ NO LO TIENE")}");
                return hasItem;
            }

            void ConsumeInventoryItem(QuestChainEntry entry, Inventory playerInventory)
            {
                if (!entry.requireItemInInventory || !entry.consumeItemOnComplete || playerInventory == null)
                    return;

                if (entry.requiredItem == null)
                {
                    _ctx.DebugLog("ConsumeInventoryItem: requiredItem es null, no se puede consumir.");
                    return;
                }

                string itemId = entry.requiredItem.itemId;
                if (string.IsNullOrEmpty(itemId))
                {
                    _ctx.DebugLog("ConsumeInventoryItem: requiredItem.itemId está vacío.");
                    return;
                }

                // Consumir el ítem
                bool consumed = playerInventory.TryConsume(itemId, entry.requiredAmount, notifyChanges: true, fallbackDefinition: entry.requiredItem);
                _ctx.DebugLog($"ConsumeInventoryItem: Intentando consumir '{itemId}' x{entry.requiredAmount} → {(consumed ? "OK" : "FALLO")}");
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
                [Tooltip("Ignorar FOV; usa solo radio alrededor del NPC para detección")] public bool ignoreFovForItem = false;
                [Tooltip("Si >0, usa este radio en lugar del del módulo para detectar el item")] public float overrideDetectionRadius = 0f;

                [Header("Verificación de Inventario")]
                [Tooltip("Si está activado, verifica que el jugador tenga cierto ítem en el inventario para completar la quest.")]
                public bool requireItemInInventory = false;
                [Tooltip("El ítem requerido en el inventario.")]
                public ItemData requiredItem;
                [Tooltip("Cantidad del ítem requerida.")]
                [Min(1)] public int requiredAmount = 1;
                [Tooltip("Si está activado, consume el ítem del inventario al completar la quest.")]
                public bool consumeItemOnComplete = true;

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

            [Header("Música y eventos")]
            [Tooltip("Evento custom para la fase de alerta/persecución. Se emite al detectar al jugador.")]
            public string alertMusicEvent = "";
            [Tooltip("ID de batalla para AudioGraphProfile (se usa en BATTLE_START:{id} y BattleWon)")]
            public string battleMusicId = "";
            [Tooltip("Evento custom opcional para restaurar/ajustar la música cuando acaba la batalla.")]
            public string endMusicEvent = "";

            [Header("Battle")]
            public bool startBattleOnChallengeEnd = true;
            [Min(1f)] public float battleHealth = 120f;
            public Vector3 healthBarOffset = new(0f, 2.4f, 0f);
            public GameObject healthBarPrefab;
            public Canvas healthBarCanvasOverride;
            [Tooltip("Colores de la barra de vida (saludable / aviso / crítico).")]
            public Color healthColor = Color.green;
            public Color warningColor = Color.yellow;
            public Color criticalColor = Color.red;
            [Range(0f, 1f)] public float warningThreshold = 0.5f;
            [Range(0f, 1f)] public float criticalThreshold = 0.25f;
            [Tooltip("Si está activo, la barra se oculta cuando la vida está llena.")]
            public bool hideHealthBarWhenFull = true;

            [Header("Diálogos")]
            public DialogueAsset dialogueOnChallenge;
            public DialogueAsset dialogueOnDefeat;
            public DialogueAsset dialogueAfterBattle;

            [Header("Recompensas")]
            public RewardEntry[] rewards = Array.Empty<RewardEntry>();

            [Header("Ataques (referencias de animación)")]
            public string lightAttackStateLeft = "LeftAttack";
            public string lightAttackStateRight = "RightAttack";
            public string specialAttackState = "SpecialAttack";

            [Header("Ataques (prefab simple)")]
            [Tooltip("Prefab de proyectil a instanciar durante el ataque.")]
            public GameObject projectilePrefab;
            [Tooltip("Punto opcional desde el que se lanzará el proyectil (usa el transform del NPC si está vacío).")]
            public Transform projectileOrigin;
            [Min(0f)] public float projectileDamage = 10f;
            [Min(0f)] public float projectileSpeed = 12f;
            
            [Header("IA de Combate")]
            [Tooltip("Distancia mínima al jugador durante el combate")]
            [Min(0f)] public float combatMinDistance = 3f;
            [Tooltip("Distancia máxima para atacar al jugador")]
            [Min(0f)] public float combatMaxDistance = 10f;
            [Tooltip("Tiempo entre ataques (segundos)")]
            [Min(0.1f)] public float attackCooldown = 2f;
            [Tooltip("Probabilidad de usar ataque especial (0-1)")]
            [Range(0f, 1f)] public float specialAttackChance = 0.3f;
            [Tooltip("Intervalo de recálculo de ruta durante el combate")]
            [Min(0.05f)] public float combatRepathInterval = 0.2f;
            [Tooltip("Distancia que intenta tomar al retroceder")]
            [Min(0.25f)] public float combatRetreatDistance = 2f;
            [Tooltip("Velocidad de giro para encarar al jugador durante el combate")]
            [Min(0.1f)] public float combatTurnSpeed = 6f;

            // Bloqueo de jugador: se fuerza internamente igual que el sistema de diálogo
            const ActionMode PlayerLockMode = ActionMode.Stunned;
            [SerializeField, HideInInspector] bool lockPlayer = true;
            [SerializeField, HideInInspector] bool lockOnSight = true;

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

            [Header("Battle Events")]
            public UnityEvent onBattleStarted;
            public UnityEvent onBattleFinished;

            NPCBehaviourManager _ctx;
            NPCCombatBrain _combatBrain;
            RoutineHandle _challengeRoutine;
            RoutineHandle _turnRoutine;
            bool _isChallenging;
            bool _lockModeApplied;
            bool _playerLockEventRaised;
            Behaviour _vThirdPersonInput;

            Damageable _resolvedHealth;
            bool _battleStarted;
            bool _battleFinished;
            bool _forceHealthVisibleUntilDamage;
            GameObject _healthBarInstance;
            RectTransform _healthBarRect;
            Image _healthBarFill;
            CanvasGroup _healthBarCanvasGroup;
            Camera _camera;
            bool _ownsHealthComponent;
            Vector3 _homePosition;
            Quaternion _homeRotation;
            bool _alertMusicRaised;
            static Sprite _fallbackSprite;

             public void Initialize(NPCBehaviourManager context)
             {
                 _ctx = context;
                 _combatBrain = context.GetComponent<NPCCombatBrain>() ?? context.gameObject.AddComponent<NPCCombatBrain>();
                 _combatBrain.Initialize(context);
             }
             public void OnStart() { }

             public void OnEnable()
             {
                 _isChallenging = false;
                 _lockModeApplied = false;
                 _playerLockEventRaised = false;
                 if (exclamationPrefab) exclamationPrefab.SetActive(false);

                 _homePosition = _ctx.transform.position;
                 _homeRotation = _ctx.transform.rotation;

                 _ownsHealthComponent = false;
                _camera = null;
                _resolvedHealth = ResolveHealth();
                _battleStarted = false;
                _battleFinished = false;
                _forceHealthVisibleUntilDamage = false;
                _alertMusicRaised = false;
                HideHealthBar();
            }

             public void OnDisable()
             {
                if (_challengeRoutine != null)
                {
                    _ctx.StopCoroutineSafe(_challengeRoutine);
                    _challengeRoutine = null;
                }
                if (_turnRoutine != null) { _ctx.StopCoroutineSafe(_turnRoutine); _turnRoutine = null; } // ← NUEVO
                _combatBrain?.StopCombat();
                _ctx.Animator.ResetMovement();
                NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                ReleasePlayer();
                if (exclamationPrefab) exclamationPrefab.SetActive(false);
                HideHealthBar();
             }

            public void Tick()
            {
                UpdateHealthBarVisual();

                if (!enable || _isChallenging || sightRadius <= 0f) return;
                if (_battleFinished) return;
                if (_battleStarted) return; // Ya estamos en combate, no relanzar el reto

                _ctx.EnsurePlayerReference();
                if (_ctx.Player == null) return;
                if (!_ctx.IsPlayerInFov(sightRadius, fovDegrees)) return;

                // Bloqueo inmediato
                if (lockPlayer && lockOnSight && !_lockModeApplied)
                    ApplyLock();

                if (lockPlayer && _lockModeApplied)
                    _ctx.ForcePlayerIdle();

                // ← NUEVO: programa el giro si procede y aún no se ha lanzado
                if (turnPlayerOnSight && _turnRoutine == null && _lockModeApplied)
                    _turnRoutine = _ctx.RunCoroutine(TurnPlayerAfterDelay());

                if (!_battleStarted)
                    TriggerAlertMusic();

                if (_challengeRoutine == null)
                    _challengeRoutine = _ctx.RunCoroutine(ChallengeFlow());
             }

             public bool HandleInteraction(GameObject interactor)
             {
                if (!enable)
                    return false;

                if (_battleFinished && dialogueAfterBattle)
                {
                    _ctx.PlayDialogue(dialogueAfterBattle);
                    return true;
                }

                return false;
             }

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
                if (lockPlayer && _lockModeApplied)
                    _ctx.ForcePlayerIdle();

                // Animación de “alerta”
                float alertTimer = 0f;
                float alertDuration = Mathf.Max(challengeAlertMinSeconds, 0.05f);

                if (!string.IsNullOrEmpty(challengeAlertState))
                    _ctx.Animator.PlayOneShot(challengeAlertState);

                while (alertTimer < alertDuration)
                {
                    if (_ctx.Player == null || !_ctx.IsPlayerInFov(sightRadius, fovDegrees))
                        break;

                    if (lockPlayer && _lockModeApplied)
                        _ctx.ForcePlayerIdle();

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

                    if (lockPlayer && _lockModeApplied)
                        _ctx.ForcePlayerIdle();

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
                if (lockPlayer && _lockModeApplied)
                    _ctx.ForcePlayerIdle();

                if (!string.IsNullOrEmpty(challengeState))
                {
                    _ctx.Animator.SetInteractOverride(challengeState, true);
                    _ctx.Animator.PlayOneShot(challengeState);
                }
                else
                {
                    _ctx.Animator.ClearInteractOverride();
                }

                // Dispara la interacción o fallback
                TriggerChallengeDialogue();

                // Espera a que se cierre diálogo (directamente yield al IEnumerator)
                yield return _ctx.WaitDialogueToClose();

                onChallengeStarted?.Invoke();
                _ctx.DebugLog("OnChallengeStarted invocado.");

                NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                _ctx.Animator.ResetMovement();

                // Libera al jugador tras el reto/diálogo
                ReleasePlayer();

                Debug.Log($"[CombatModule] Después de ReleasePlayer - startBattleOnChallengeEnd: {startBattleOnChallengeEnd}, _battleFinished: {_battleFinished}");

                if (startBattleOnChallengeEnd && !_battleFinished)
                {
                    // Asegura que el NPC abandona el saludo y entra en combate
                    _battleFinished = false;
                    _battleStarted = false;
                    Debug.Log("[CombatModule] Llamando a StartBattle()");
                    StartBattle();
                }
                else
                {
                    Debug.LogWarning($"[CombatModule] ⚠️ NO se llama a StartBattle - startBattleOnChallengeEnd: {startBattleOnChallengeEnd}, _battleFinished: {_battleFinished}");
                }

                _isChallenging = false;
                _challengeRoutine = null;
             }

            void ApplyLock()
            {
                onPlayerLock?.Invoke();
                _playerLockEventRaised = true;

                // Buscar y deshabilitar vThirdPersonInput
                if (_vThirdPersonInput == null)
                {
                    GameObject playerGo = _ctx.Player ? _ctx.Player.gameObject : null;
                    if (playerGo == null && PlayerService.TryGetPlayer(out var resolved, allowSceneLookup: true))
                        playerGo = resolved;

                    if (playerGo != null)
                    {
                        Debug.Log($"[CombatModule] Buscando vThirdPersonInput en jugador: {playerGo.name}");
                        
                        // Buscar por nombre de tipo usando diferentes variantes
                        var inputType = Type.GetType("Invector.vCharacterController.vThirdPersonInput, Assembly-CSharp-firstpass", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Assembly-CSharp", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Invector-3rdPersonController_LITE", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Invector-3rdPersonController", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput", false);
                        
                        Debug.Log($"[CombatModule] Tipo vThirdPersonInput resuelto: {inputType?.FullName ?? "NULL"}");
                        
                        if (inputType != null)
                        {
                            // Buscar en el objeto y en sus hijos
                            _vThirdPersonInput = playerGo.GetComponent(inputType) as Behaviour;
                            if (_vThirdPersonInput == null)
                            {
                                _vThirdPersonInput = playerGo.GetComponentInChildren(inputType, true) as Behaviour;
                                Debug.Log($"[CombatModule] Buscado en hijos, encontrado: {_vThirdPersonInput != null}");
                            }
                            else
                            {
                                Debug.Log($"[CombatModule] Encontrado en objeto raíz");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[CombatModule] ⚠️ No se pudo resolver el tipo vThirdPersonInput");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[CombatModule] ⚠️ No se encontró el GameObject del jugador");
                    }
                }

                if (_vThirdPersonInput != null)
                {
                    Debug.Log($"[CombatModule] Deshabilitando vThirdPersonInput (enabled antes: {_vThirdPersonInput.enabled})");
                    _vThirdPersonInput.enabled = false;
                    _lockModeApplied = true;
                    Debug.Log($"[CombatModule] ✅ vThirdPersonInput.enabled = {_vThirdPersonInput.enabled}");
                }
                else
                {
                    Debug.LogError("[CombatModule] ❌ No se pudo encontrar vThirdPersonInput para deshabilitar");
                }

                if (_lockModeApplied)
                    _ctx.ForcePlayerIdle();
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
                _ctx.Animator.ClearInteractOverride();

                if (!lockPlayer) return;

                // Habilitar vThirdPersonInput
                if (_lockModeApplied && _vThirdPersonInput != null)
                {
                    Debug.Log($"[CombatModule] Habilitando vThirdPersonInput (enabled antes: {_vThirdPersonInput.enabled})");
                    
                    // Resetear el movimiento del jugador antes de rehabilitar
                    _ctx.ResetPlayerMotion();
                    
                    _vThirdPersonInput.enabled = true;
                    Debug.Log($"[CombatModule] ✅ vThirdPersonInput.enabled = {_vThirdPersonInput.enabled}");
                    _lockModeApplied = false;
                }
                else if (_lockModeApplied)
                {
                    Debug.LogWarning("[CombatModule] ⚠️ _lockModeApplied=true pero _vThirdPersonInput es null");
                    _lockModeApplied = false;
                }
                
                if (_turnRoutine != null) { _ctx.StopCoroutineSafe(_turnRoutine); _turnRoutine = null; } // ← NUEVO

                if (_playerLockEventRaised)
                {
                    onPlayerUnlock?.Invoke();
                    _playerLockEventRaised = false;
                }
            }

            void TriggerAlertMusic()
            {
                if (_alertMusicRaised)
                    return;

                if (string.IsNullOrWhiteSpace(alertMusicEvent))
                    return;

                DefaultNarrativeSignals.Instance?.RaiseCustom(alertMusicEvent);
                _alertMusicRaised = true;
            }

            void TriggerBattleMusic()
            {
                if (!string.IsNullOrWhiteSpace(battleMusicId))
                {
                    DefaultNarrativeSignals.Instance?.RaiseCustom($"BATTLE_START:{battleMusicId}");
                    AudioService.Instance?.BeginBattleById(battleMusicId);
                }
            }

            void RestoreBattleMusic()
            {
                if (!string.IsNullOrWhiteSpace(endMusicEvent))
                    DefaultNarrativeSignals.Instance?.RaiseCustom(endMusicEvent);

                if (!string.IsNullOrWhiteSpace(battleMusicId))
                {
                    DefaultNarrativeSignals.Instance?.RaiseBattleWon(battleMusicId);
                    AudioService.Instance?.EndBattleById(battleMusicId);
                }
            }

            void TriggerChallengeDialogue()
            {
                if (dialogueOnChallenge)
                {
                    _ctx.PlayDialogue(dialogueOnChallenge);
                    return;
                }

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
            }

            public void FireProjectile()
            {
                if (!projectilePrefab)
                    return;

                var origin = projectileOrigin ? projectileOrigin : _ctx.transform;
                var target = _ctx.Player ? _ctx.Player.position : (origin.position + origin.forward);
                Vector3 dir = (target - origin.position);
                if (dir.sqrMagnitude < 0.0001f)
                    dir = origin.forward;

                dir = dir.normalized;
                Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir, Vector3.up) : origin.rotation;

                var instance = GameObject.Instantiate(projectilePrefab, origin.position, rot);

                if (instance.TryGetComponent<EnemyProjectile>(out var enemyProj))
                {
                    enemyProj.Initialize(dir, projectileDamage);
                    return;
                }

                if (instance.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = dir * projectileSpeed;
                }
            }

            void StartBattle()
            {
                if (_battleStarted) return;

                _battleStarted = true;
                _battleFinished = false;

                _resolvedHealth = ResolveHealth();
                if (_resolvedHealth != null)
                {
                    if (_ownsHealthComponent)
                        _resolvedHealth.SetMaxAndCurrent(battleHealth, battleHealth);

                    _resolvedHealth.OnDamaged -= HandleNpcDamaged;
                    _resolvedHealth.OnDamaged += HandleNpcDamaged;
                    _resolvedHealth.OnDied -= HandleNpcDied;
                    _resolvedHealth.OnDied += HandleNpcDied;
                }

                _forceHealthVisibleUntilDamage = true;
                ShowHealthBar();

                TriggerBattleMusic();
                onBattleStarted?.Invoke();

                _ctx.DebugLog("Batalla iniciada.");

                // Iniciar IA de combate delegada
                _combatBrain?.BeginCombat(BuildCombatSettings());
            }

            NPCCombatBrain.Settings BuildCombatSettings()
            {
                return new NPCCombatBrain.Settings
                {
                    sightRadius = sightRadius,
                    minDistance = combatMinDistance,
                    maxDistance = combatMaxDistance,
                    attackCooldown = attackCooldown,
                    specialAttackChance = specialAttackChance,
                    repathInterval = combatRepathInterval,
                    retreatDistance = combatRetreatDistance,
                    turnSpeed = combatTurnSpeed,
                    lightAttackStateLeft = lightAttackStateLeft,
                    lightAttackStateRight = lightAttackStateRight,
                    specialAttackState = specialAttackState,
                };
            }

            void HandleNpcDamaged(float amount)
            {
                if (_resolvedHealth == null)
                    return;

                _forceHealthVisibleUntilDamage = false;
                RefreshHealthBarImmediate();

                if (_resolvedHealth.Current <= 0f && !_battleFinished)
                    HandleNpcDied();
            }

            void HandleNpcDied()
            {
                if (_battleFinished)
                    return;

                _battleFinished = true;
                _battleStarted = false;
                _combatBrain?.StopCombat();
                RestoreBattleMusic();

                if (dialogueOnDefeat)
                    _ctx.PlayDialogue(dialogueOnDefeat);

                GrantRewards();
                ReturnToHome();

                onBattleFinished?.Invoke();
                _ctx.DebugLog("Batalla finalizada.");

                RefreshHealthBarImmediate();
                HideHealthBar();
                enable = false; // evita repetir combate
            }

            Damageable ResolveHealth()
            {
                if (_ctx.TryGetComponent(out Damageable existing))
                    return existing;

                _ownsHealthComponent = true;
                return _ctx.gameObject.AddComponent<Damageable>();
            }

            void ShowHealthBar()
            {
                if (!_resolvedHealth)
                    return;

                HideHealthBar();

                Transform parent = healthBarCanvasOverride ? healthBarCanvasOverride.transform : FindCanvas();
                if (!parent)
                    parent = BuildRuntimeCanvas();
                if (!parent)
                    return;

                _healthBarInstance = healthBarPrefab
                    ? GameObject.Instantiate(healthBarPrefab, parent, false)
                    : BuildRuntimeHealthBar(parent);

                _healthBarRect = _healthBarInstance ? _healthBarInstance.GetComponent<RectTransform>() : null;
                _healthBarFill = _healthBarInstance ? FindFillImage(_healthBarInstance) : null;
                _healthBarCanvasGroup = _healthBarInstance
                    ? _healthBarInstance.GetComponent<CanvasGroup>() ?? _healthBarInstance.AddComponent<CanvasGroup>()
                    : null;

                RefreshHealthBarImmediate();
            }

            void HideHealthBar()
            {
                if (_healthBarInstance)
                {
                    GameObject.Destroy(_healthBarInstance);
                    _healthBarInstance = null;
                }

                _healthBarRect = null;
                _healthBarFill = null;
                _healthBarCanvasGroup = null;
            }

            void RefreshHealthBarImmediate()
            {
                if (_resolvedHealth == null || _healthBarFill == null)
                    return;

                float ratio = Mathf.Clamp01(_resolvedHealth.Current / Mathf.Max(1f, _resolvedHealth.Max));
                _healthBarFill.fillAmount = ratio;
                _healthBarFill.color = GetColorForRatio(ratio);

                if (_healthBarCanvasGroup)
                {
                    if (_forceHealthVisibleUntilDamage)
                        _healthBarCanvasGroup.alpha = 1f;
                    else if (hideHealthBarWhenFull)
                        _healthBarCanvasGroup.alpha = ratio >= 0.999f ? 0f : 1f;
                }
            }

            void UpdateHealthBarVisual()
            {
                if (_healthBarRect == null)
                    return;

                _camera ??= _ctx.PlayerCamera ? _ctx.PlayerCamera.GetComponent<Camera>() : null;
                _camera ??= Camera.main;
                if (_camera == null)
                    return;

                Vector3 targetPos = _ctx.transform.position + healthBarOffset;
                Vector3 screenPos = _camera.WorldToScreenPoint(targetPos);
                if (screenPos.z < 0f)
                    return;

                _healthBarRect.position = Vector3.Lerp(_healthBarRect.position, screenPos, Time.unscaledDeltaTime * 12f);
            }

            GameObject BuildRuntimeHealthBar(Transform parent)
            {
                var root = new GameObject("NPC Health Bar", typeof(RectTransform), typeof(CanvasGroup));
                root.transform.SetParent(parent, false);

                var rect = root.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(120f, 18f);

                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(root.transform, false);
                var bgRect = bg.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;

                var bgImage = bg.GetComponent<Image>();
                bgImage.sprite = GetFallbackSprite();
                bgImage.type = Image.Type.Simple;
                bgImage.color = new Color(0f, 0f, 0f, 0.65f);

                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(bg.transform, false);
                var fillRect = fill.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = new Vector2(1f, 1f);
                fillRect.offsetMax = new Vector2(-1f, -1f);

                var fillImage = fill.GetComponent<Image>();
                fillImage.sprite = GetFallbackSprite();
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.color = healthColor;

                return root;
            }

            static Sprite GetFallbackSprite()
            {
                if (_fallbackSprite != null)
                    return _fallbackSprite;

                var tex = Texture2D.whiteTexture;
                _fallbackSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _fallbackSprite.name = "NPCHealth_FallbackSprite";
                return _fallbackSprite;
            }

            Image FindFillImage(GameObject root)
            {
                var images = root.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.gameObject != root && (img.type == Image.Type.Filled || img.fillMethod == Image.FillMethod.Horizontal))
                        return img;
                }

                return root.GetComponentInChildren<Image>(true);
            }

            Color GetColorForRatio(float ratio)
            {
                if (ratio <= criticalThreshold)
                    return criticalColor;
                if (ratio <= warningThreshold)
                    return warningColor;
                return healthColor;
            }

            Transform FindCanvas()
            {
                if (healthBarCanvasOverride)
                    return healthBarCanvasOverride.transform;

#if UNITY_2022_3_OR_NEWER
                var uiCanvas = GameObject.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
                var uiCanvas = GameObject.FindObjectOfType<Canvas>();
#pragma warning restore 618
#endif
                return uiCanvas ? uiCanvas.transform : null;
            }

            Transform BuildRuntimeCanvas()
            {
                var go = new GameObject("NPC Health Runtime Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 250;

                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                return go.transform;
            }

            void GrantRewards()
            {
                if (rewards == null || rewards.Length == 0)
                    return;

                if (!PlayerService.TryGetInventory(out var inventory))
                {
                    Debug.LogWarning("[NPC Combat] No se encontró el inventario del jugador para otorgar recompensas.");
                    return;
                }

                foreach (var r in rewards)
                {
                    if (!r.item || r.amount <= 0) continue;
                    inventory.Add(r.item, r.amount);
                }
            }

            void ReturnToHome()
            {
                _ctx.transform.SetPositionAndRotation(_homePosition, _homeRotation);
                _ctx.SetLastPosition(_homePosition);
                if (_ctx.Agent && _ctx.Agent.enabled)
                {
                    NavMeshAgentUtility.HardStop(_ctx.Agent);
                    _ctx.Agent.Warp(_homePosition);
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

        [Serializable]
        public struct RewardEntry
        {
            public ItemData item;
            [Min(1)] public int amount;
        }

        #endregion

        [Serializable]
        public class StringEvent : UnityEvent<string> { }
    }
}
