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
                        }
                        catch (Exception)
                        {
                            // Swallow start errors silently as requested (no debug)
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

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // Gizmos para los orígenes de disparo por slot
            if (combatModule == null) return;

            DrawOriginGizmo(combatModule.leftProjectileOrigin ? combatModule.leftProjectileOrigin : (combatModule.projectileOrigin ? combatModule.projectileOrigin : transform),
                new Color(0f, 1f, 1f, 0.9f)); // cian para izquierda
            DrawOriginGizmo(combatModule.rightProjectileOrigin ? combatModule.rightProjectileOrigin : (combatModule.projectileOrigin ? combatModule.projectileOrigin : transform),
                new Color(1f, 0f, 1f, 0.9f)); // magenta para derecha
            DrawOriginGizmo(combatModule.specialProjectileOrigin ? combatModule.specialProjectileOrigin : (combatModule.projectileOrigin ? combatModule.projectileOrigin : transform),
                new Color(1f, 0.9f, 0.2f, 0.9f)); // amarillo para especial
        }

        void DrawOriginGizmo(Transform origin, Color color)
        {
            if (origin == null) return;
            var prev = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawSphere(origin.position, 0.08f);
            Gizmos.DrawRay(origin.position, origin.forward * 0.6f);
            Gizmos.color = prev;
        }
#endif

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
            // Debug logs disabled on request
            return;
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

        #region Attack / Projectile

        // Compatibilidad con llamadas antiguas sin índice (usa el slot izquierdo por defecto)
        public void OnAttackTriggered()
        {
            combatModule?.FireProjectile(0);
        }

        // Nuevo: disparo de proyectil según slot: 0=izquierdo, 1=derecho, 2=especial
        public void OnAttackTriggered(int slotIndex)
        {
            combatModule?.FireProjectile(slotIndex);
        }

        // También mantenemos el alias existente usado por animaciones
        public void FireCombatProjectile() => combatModule?.FireProjectile(0);

        // Métodos auxiliares pensados para Animation Events sin parámetros
        public void AE_FireLeft() => combatModule?.FireProjectile(0);
        public void AE_FireRight() => combatModule?.FireProjectile(1);
        public void AE_FireSpecial() => combatModule?.FireProjectile(2);

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
                            entry.onOfferDialogueStarted?.Invoke();
                            _ctx.PlayDialogue(entry.dlgBefore, () => entry.onOfferDialogueFinished?.Invoke());
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
                        first.onOfferDialogueStarted?.Invoke();
                        _ctx.PlayDialogue(first.dlgBefore, () => first.onOfferDialogueFinished?.Invoke());
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
                        if (entry.dlgBefore)
                        {
                            entry.onOfferDialogueStarted?.Invoke();
                            _ctx.PlayDialogue(entry.dlgBefore, () => entry.onOfferDialogueFinished?.Invoke());
                        }
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
                [Tooltip("Se dispara cuando inicia el diálogo de oferta (dlgBefore) para esta etapa de la cadena.")]
                public UnityEvent onOfferDialogueStarted;
                [Tooltip("Se dispara cuando termina el diálogo de oferta (dlgBefore) para esta etapa de la cadena.")]
                public UnityEvent onOfferDialogueFinished;
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
            [Header("Reacciones de Daño")]
            [HideInInspector] public string damageStatePrimary = "TakeDamage";
            [HideInInspector] public string damageStateAlternate = "GetHit02_NoWeapon";
            [Min(0f)] public float damageReactionMinInterval = 0.35f;

            [Header("UI / Feedback")]
            public GameObject exclamationPrefab;
            public Vector3 exclamationOffset = new Vector3(0f, 2f, 0f);
            public float exclamationSeconds = 2f;

            [Header("Música y eventos")]
            [Tooltip("Evento custom para la fase de alerta/persecución. Se emite al detectar al jugador.")]
            public string alertMusicEvent = "Npc_Battle_Alert";
            [Tooltip("ID de batalla para AudioGraphProfile (se usa en BATTLE_START:{id} y BattleWon)")]
            public string battleMusicId = "Npc_Battle";
            [Tooltip("Evento custom opcional para restaurar/ajustar la música cuando acaba la batalla.")]
            public string endMusicEvent = "";

            [Header("Battle")]
            public bool startBattleOnChallengeEnd = true;
            [Min(1f)] public float battleHealth = 120f;
            public Vector3 healthBarOffset = new(0f, 2.4f, 0f);
            public GameObject healthBarPrefab; // GO raíz (Canvas + barra)
            public Image healthBarFillImage;   // Referencia directa a la Image (Fill) hija
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
            [Tooltip("Estado de idle durante batalla (ej: Idle_Battle_NoWeapon)")]
            public string battleIdleState = "Idle_Battle_NoWeapon";
            [Tooltip("Índice de la capa del animator para animaciones del torso superior (ataques)")]
            public int upperBodyLayer = 1;
            [HideInInspector] public string lightAttackStateLeft = "MagicLeft";
            [HideInInspector] public string lightAttackStateRight = "MagicRight";
            [HideInInspector] public string specialAttackState = "MagicSpecial";

            [Header("Ataques - Sistema de 3 Slots")]
            [Tooltip("Slot izquierdo - Ataque rápido")]
            public GameObject leftProjectilePrefab;
            [Min(0f)] public float leftProjectileDamage = 10f;
            [Min(0f)] public float leftProjectileSpeed = 12f;
            [Min(0.1f)] public float leftAttackCooldown = 1.5f;
            
            [Tooltip("Slot derecho - Ataque medio")]
            public GameObject rightProjectilePrefab;
            [Min(0f)] public float rightProjectileDamage = 15f;
            [Min(0f)] public float rightProjectileSpeed = 10f;
            [Min(0.1f)] public float rightAttackCooldown = 2f;
            
            [Tooltip("Slot especial - Ataque poderoso")]
            public GameObject specialProjectilePrefab;
            [Min(0f)] public float specialProjectileDamage = 25f;
            [Min(0f)] public float specialProjectileSpeed = 8f;
            [Min(0.1f)] public float specialAttackCooldown = 4f;
            
            [Header("Orígenes de Disparo (opcionales)")]
            [Tooltip("Punto genérico de disparo (fallback si el específico está vacío)")]
            public Transform projectileOrigin;
            [Tooltip("Origen de disparo para el slot izquierdo")]
            public Transform leftProjectileOrigin;
            [Tooltip("Origen de disparo para el slot derecho")]
            public Transform rightProjectileOrigin;
            [Tooltip("Origen de disparo para el slot especial")]
            public Transform specialProjectileOrigin;
            
            [Header("Sincronización de Disparo")]
            [Tooltip("Si está activo, el proyectil se instanciará desde un Animation Event y no al iniciar la animación.")]
            [HideInInspector]
            public bool spawnProjectileViaAnimationEvent = true;
            
            [Header("IA de Combate")]
            [Tooltip("Distancia mínima al jugador durante el combate")]
            [Min(0f)] public float combatMinDistance = 4f;
            [Tooltip("Distancia máxima para atacar al jugador")]
            [Min(0f)] public float combatMaxDistance = 12f;
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
            public UnityEvent onBattleWon;

            NPCBehaviourManager _ctx;
            NPCCombatBrain _combatBrain;
            RoutineHandle _challengeRoutine;
            RoutineHandle _turnRoutine;
            bool _isChallenging;
            bool _lockModeApplied;
            bool _playerLockEventRaised;
            Behaviour _vThirdPersonInput;
            Vector3 _lockedPlayerPosition;
            Quaternion _lockedPlayerRotation;
            bool _hasLockSnapshot;

            Damageable _resolvedHealth;
            bool _battleStarted;
            bool _battleFinished;
            bool _forceHealthVisibleUntilDamage;
            RectTransform _healthBarRect;
            Image _healthBarFill;
            CanvasGroup _healthBarCanvasGroup;
            Camera _camera;
            RoutineHandle _healthAnimRoutine;
            bool _ownsHealthComponent;
            Vector3 _homePosition;
            Quaternion _homeRotation;
            bool _alertMusicRaised;
            // Eliminado fallback sprite: ya no se genera barra runtime
            float _damageReactTimer;
            Vector3 _lastPlayerPos;
            [HideInInspector] public bool usePredictiveAim = true;
            [HideInInspector] public bool requireLineOfSight = true;

            [Header("Dificultad")]
            [Tooltip("Multiplicador de frecuencia de ataque (reduce cooldowns)")]
            [Range(0.2f, 3f)] public float attackFrequencyMultiplier = 1f;
            [Tooltip("Sesgo de agresividad de decisiones de ataque")]
            [Range(0f, 1f)] public float aggressionBias = 0.5f;
            [Tooltip("Probabilidad de solicitar esquiva al recibir daño")]
            [Range(0f, 1f)] public float dodgeChance = 0.2f;

            [Header("Derrota / Secuencia")]
            [Tooltip("Si está activo, el NPC permanece tras la derrota y no se destruye.")]
            public bool persistAfterDefeat = true;
            [Tooltip("Si está activo y persistAfterDefeat es false: instancia FX de explosión y oculta NPC.")]
            public bool explodeOnDefeat = false;
            [Tooltip("Prefab FX para explosión (opcional)")]
            public GameObject defeatExplosionPrefab;
            [Tooltip("Prefab FX para victoria del jugador (confeti, etc.)")]
            public GameObject victoryFXPrefab;
            [Min(0f)] public float victoryFXDelay = 0.2f;
            [Min(0.2f)] public float victoryFocusSeconds = 1.0f;
            [Min(0f)] public float victoryExtraSeconds = 0.25f;
            [Tooltip("Animación de muerte del NPC")]
            public string npcDeathAnimation = "Die02_NoWeapon";
            [Tooltip("Tiempo que mantiene la pose tras morir antes de continuar la secuencia.")]
            [Min(0f)] public float deathPoseHoldSeconds = 0.35f;
            [Tooltip("Animación de victoria del jugador")]
            public string playerVictoryAnimation = "Dance_NoWeapon";
            [Tooltip("Layer Unity para combate (Enemy)")]
            public int enemyUnityLayer = 7;
            [Tooltip("Layer Unity post-derrota (Interactable)")]
            public int interactableUnityLayer = 8;
            [Tooltip("Si está activo, reproducirá el diálogo de derrota tras la batalla si existe.")]
            public bool useDialogueAfterBattle = true;
            [Tooltip("Diálogo repetible tras derrota")]
            // Usamos dialogueAfterBattle ya declarado en la sección de Diálogos

            bool hasBeenDefeated;
            bool _defeatSequenceRunning;

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

                 // Defaults por código para mantener el inspector limpio
                 if (string.IsNullOrEmpty(lightAttackStateLeft)) lightAttackStateLeft = "MagicLeft";
                 if (string.IsNullOrEmpty(lightAttackStateRight)) lightAttackStateRight = "MagicRight";
                 if (string.IsNullOrEmpty(specialAttackState)) specialAttackState = "MagicSpecial";
                 if (string.IsNullOrEmpty(challengeAlertState)) challengeAlertState = "SenseSomethingStart_NoWeapon";
                 if (string.IsNullOrEmpty(challengeState)) challengeState = "Challenging_NoWeapon";
                 if (string.IsNullOrEmpty(battleIdleState)) battleIdleState = "Idle_Battle_NoWeapon";
                 // Defaults de música para facilitar creación rápida de NPCs de batalla
                 if (string.IsNullOrWhiteSpace(alertMusicEvent)) alertMusicEvent = "Npc_Battle_Alert";
                 if (string.IsNullOrWhiteSpace(battleMusicId)) battleMusicId = "Npc_Battle";
                 if (string.IsNullOrWhiteSpace(endMusicEvent)) endMusicEvent = "Npc_Battle_Victory";
                 // Por defecto NO requerimos Animation Events para spawnear (más robusto)
                 // Si tus clips tienen eventos AE_FireX, puedes activarlo manualmente desde código.
                 spawnProjectileViaAnimationEvent = false;

                 // Auto-asignación de orígenes de disparo por convención (weapon_l / weapon_r)
                 AutoAssignProjectileOrigins();

                 // Validación y avisos de configuración
                 LogSlotConfigWarnings();

                 // Inicializar última posición del jugador para apuntado predictivo
                 _ctx.EnsurePlayerReference();
                 _lastPlayerPos = _ctx.Player ? _ctx.Player.position : _ctx.transform.position + _ctx.transform.forward;

                 _homePosition = _ctx.transform.position;
                 _homeRotation = _ctx.transform.rotation;

                 _ownsHealthComponent = false;
                _camera = null;
                _resolvedHealth = ResolveHealth();
                _battleStarted = false;
                _battleFinished = false;
                _forceHealthVisibleUntilDamage = false;
                _alertMusicRaised = false;
                _hasLockSnapshot = false;
                _defeatSequenceRunning = false;
                if (hasBeenDefeated)
                {
                    _ctx.gameObject.layer = interactableUnityLayer;
                    enable = false; // impedir nuevo combate
                }
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
             void LogSlotConfigWarnings()
             {
             }            // Busca automáticamente los orígenes por nombre de hueso/nodo estándar
            void AutoAssignProjectileOrigins()
            {
                if (_ctx == null) return;

                // Fallback general al propio transform del NPC si no está asignado
                if (projectileOrigin == null)
                    projectileOrigin = _ctx.transform;

                // Resolver izquierda/derecha solo si no están ya asignados desde fuera
                if (leftProjectileOrigin == null)
                    leftProjectileOrigin = FindChildByNames(_ctx.transform, new[] { "weapon_l", "weapon-left", "Weapon_L", "WeaponLeft" });

                if (rightProjectileOrigin == null)
                    rightProjectileOrigin = FindChildByNames(_ctx.transform, new[] { "weapon_r", "weapon-right", "Weapon_R", "WeaponRight" });

                // Si el especial no está asignado, usar el derecho por convención
                if (specialProjectileOrigin == null)
                    specialProjectileOrigin = rightProjectileOrigin ? rightProjectileOrigin : leftProjectileOrigin;
            }

            static Transform FindChildByNames(Transform root, string[] candidates)
            {
                if (root == null || candidates == null || candidates.Length == 0) return null;
                for (int i = 0; i < candidates.Length; i++)
                    candidates[i] = candidates[i]?.ToLowerInvariant();

                // Búsqueda en profundidad (DFS) por nombre exacto o que contenga el candidato
                var stack = new System.Collections.Generic.Stack<Transform>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var t = stack.Pop();
                    string name = t.name.ToLowerInvariant();
                    foreach (var c in candidates)
                    {
                        if (string.IsNullOrEmpty(c)) continue;
                        if (name == c || name.Contains(c))
                            return t;
                    }
                    for (int i = 0; i < t.childCount; i++)
                        stack.Push(t.GetChild(i));
                }
                return null;
            }

            static Transform FindChildByName(Transform root, string name)
            {
                if (root == null || string.IsNullOrWhiteSpace(name))
                    return null;

                string target = name.ToLowerInvariant();
                var stack = new System.Collections.Generic.Stack<Transform>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    var t = stack.Pop();
                    string n = t.name.ToLowerInvariant();
                    if (n == target || n.Contains(target))
                        return t;

                    for (int i = 0; i < t.childCount; i++)
                        stack.Push(t.GetChild(i));
                }

                return null;
            }

            public void Tick()
            {
                UpdateHealthBarVisual();
                if (_damageReactTimer > 0f) _damageReactTimer -= Time.deltaTime;

                // Capturar trayectoria del jugador para apuntado predictivo
                if (_ctx.Player)
                    _lastPlayerPos = _ctx.Player.position;

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

                // IMPORTANTE: Limpiar el override del challengeState ANTES de iniciar batalla
                _ctx.Animator.ClearInteractOverride();

                // CORREGIDO: NO detener el agente ni resetear animación aquí si vamos a iniciar combate
                // NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, true);
                // _ctx.Animator.ResetMovement();

                // Libera al jugador tras el reto/diálogo
                ReleasePlayer();

                if (startBattleOnChallengeEnd && !_battleFinished)
                {
                    // Asegura que el NPC abandona el saludo y entra en combate
                    _battleFinished = false;
                    // CORREGIDO: NO resetear _battleStarted aquí para evitar conflictos
                    StartBattle();
                }
                else
                {
                    // no-op
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
                        // debug removed
                        
                        // Buscar por nombre de tipo usando diferentes variantes
                        var inputType = Type.GetType("Invector.vCharacterController.vThirdPersonInput, Assembly-CSharp-firstpass", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Assembly-CSharp", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Invector-3rdPersonController_LITE", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput, Invector-3rdPersonController", false)
                                       ?? Type.GetType("Invector.vCharacterController.vThirdPersonInput", false);
                        
                        // debug removed
                        
                        if (inputType != null)
                        {
                            // Buscar en el objeto y en sus hijos
                            _vThirdPersonInput = playerGo.GetComponent(inputType) as Behaviour;
                            if (_vThirdPersonInput == null)
                            {
                                _vThirdPersonInput = playerGo.GetComponentInChildren(inputType, true) as Behaviour;
                                // debug removed
                            }
                            else
                            {
                                // debug removed
                            }
                        }
                        else
                        {
                            // debug removed
                        }
                    }
                    else
                    {
                        // debug removed
                    }
                }

                if (_vThirdPersonInput != null)
                {
                    // debug removed
                    if (_ctx.Player != null)
                    {
                        _lockedPlayerPosition = _ctx.Player.position;
                        _lockedPlayerRotation = _ctx.Player.rotation;
                        _hasLockSnapshot = true;
                    }
                    _vThirdPersonInput.enabled = false;
                    _lockModeApplied = true;
                    // debug removed
                }
                else
                {
                    // debug removed
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
                    // debug removed

                    // Resetear el movimiento del jugador antes de rehabilitar
                    _ctx.ResetPlayerMotion();

                    // Rehabilitar con un pequeño delay para evitar que inputs residuales causen saltos
                    _ctx.RunCoroutine(ReenableInputAfterDelay(_vThirdPersonInput, 0.1f));
                    _lockModeApplied = false;

                    // ELIMINADO: No restaurar posición/rotación para evitar teletransporte
                    // if (_hasLockSnapshot && _ctx.Player != null)
                    // {
                    //     _ctx.Player.SetPositionAndRotation(_lockedPlayerPosition, _lockedPlayerRotation);
                    // }
                    _hasLockSnapshot = false;

                    // ELIMINADO: No aplicar TemporaryStun aquí, el DialogueManager ya maneja el inputRestoreDelay
                    // var pam = _ctx.GetActionManager();
                    // if (pam != null)
                    //     _ctx.RunCoroutine(TemporaryStun(pam, 0.12f));
                }
                else if (_lockModeApplied)
                {
                    // debug removed
                    _lockModeApplied = false;
                    _hasLockSnapshot = false;
                }
                
                if (_turnRoutine != null) { _ctx.StopCoroutineSafe(_turnRoutine); _turnRoutine = null; } // ← NUEVO

                if (_playerLockEventRaised)
                {
                    onPlayerUnlock?.Invoke();
                    _playerLockEventRaised = false;
                }
            }

            IEnumerator TemporaryStun(PlayerActionManager pam, float seconds)
            {
                pam.PushMode(ActionMode.Stunned);
                yield return new WaitForSecondsRealtime(seconds);
                pam.PopMode(ActionMode.Stunned);
            }

            IEnumerator ReenableInputAfterDelay(UnityEngine.Behaviour inputComponent, float delay)
            {
                yield return new WaitForSecondsRealtime(delay);
                if (inputComponent != null)
                {
                    inputComponent.enabled = true;
                    // debug removed
                }
            }

            void TriggerAlertMusic()
            {
                if (_alertMusicRaised)
                    return;

                if (string.IsNullOrWhiteSpace(alertMusicEvent))
                    return;

                // Emitir evento custom (para otros sistemas) y pedir música de alerta al AudioService
                DefaultNarrativeSignals.Instance?.RaiseCustom(alertMusicEvent);
                AudioService.Instance?.BeginAlertById(alertMusicEvent);
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
                // Si hay evento de victoria, reproducir primero esa música y restaurar después
                if (!string.IsNullOrWhiteSpace(endMusicEvent) && !string.IsNullOrWhiteSpace(battleMusicId))
                {
                    DefaultNarrativeSignals.Instance?.RaiseCustom(endMusicEvent);
                    // Mantener la victoria el tiempo suficiente para el focus + extra + anim de victoria (~2s)
                    float hold = Mathf.Max(1.5f, victoryFocusSeconds + victoryExtraSeconds + 2.0f);
                    AudioService.Instance?.PlayVictoryForBattle(battleMusicId, endMusicEvent, hold);
                    return;
                }

                // Fallback: si no hay música de victoria configurada, restaurar inmediatamente
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

            public void FireProjectile(int slotIndex)
            {
                GameObject prefab = null;
                float damage = 0f;
                float speed = 0f;
                Transform originOverride = null;

                switch (slotIndex)
                {
                    case 0:
                        prefab = leftProjectilePrefab;
                        damage = leftProjectileDamage;
                        speed = leftProjectileSpeed;
                        originOverride = leftProjectileOrigin;
                        break;
                    case 1:
                        prefab = rightProjectilePrefab;
                        damage = rightProjectileDamage;
                        speed = rightProjectileSpeed;
                        originOverride = rightProjectileOrigin;
                        break;
                    case 2:
                        prefab = specialProjectilePrefab;
                        damage = specialProjectileDamage;
                        speed = specialProjectileSpeed;
                        originOverride = specialProjectileOrigin;
                        break;
                    default:
                        prefab = leftProjectilePrefab;
                        damage = leftProjectileDamage;
                        speed = leftProjectileSpeed;
                        originOverride = leftProjectileOrigin;
                        break;
                }

                if (!prefab)
                {
                    return;
                }

                var origin = originOverride ? originOverride : (projectileOrigin ? projectileOrigin : _ctx.transform);

                // Objetivo con apuntado predictivo opcional
                Vector3 playerPos = _ctx.Player ? _ctx.Player.position : (origin.position + origin.forward * 5f);
                Vector3 dirToPlayer = (playerPos - origin.position);
                Vector3 target = playerPos;

                if (usePredictiveAim && _ctx.Player)
                {
                    Vector3 playerVel = Vector3.zero;
                    if (_ctx.Player.TryGetComponent<Rigidbody>(out var prb))
                        playerVel = prb.linearVelocity;
                    else
                    {
                        // Aproximación por diferencia de posición
                        float dt = Mathf.Max(0.016f, Time.deltaTime);
                        playerVel = (_ctx.Player.position - _lastPlayerPos) / dt;
                    }

                    float projSpeed = Mathf.Max(0.1f, speed);
                    Vector3 toTarget = playerPos - origin.position;
                    float t = toTarget.magnitude / projSpeed;
                    target = playerPos + playerVel * t;
                }

                Vector3 dir = (target - origin.position);
                if (dir.sqrMagnitude < 0.0001f)
                    dir = origin.forward;

                dir = dir.normalized;
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                var instance = GameObject.Instantiate(prefab, origin.position, rot);

                // Intentar inicialización genérica si existe un componente compatible
                var enemyProjType = instance.GetComponent("EnemyProjectile");
                if (enemyProjType != null)
                {
                    // Llamada reflectiva sencilla: Initialize(Vector3 dir, float damage)
                    var m = enemyProjType.GetType().GetMethod("Initialize", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (m != null)
                    {
                        try { m.Invoke(enemyProjType, new object[] { dir, damage }); return; } catch { /* fallback a Rigidbody */ }
                    }
                }

                if (instance.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = dir * speed;
                }
            }

            void StartBattle()
            {
                
                
                if (_battleStarted)
                {
                    
                    return;
                }

                _ctx.EnsurePlayerReference();
                if (_ctx.Player == null)
                {
                    
                    return;
                }

                

                if (_ctx.Agent != null)
                {
                    if (!_ctx.Agent.enabled)
                        _ctx.Agent.enabled = true;
                    
                    // CORREGIDO: Asegurar que el agente NO esté detenido para que pueda moverse
                    NavMeshAgentUtility.SafeSetStopped(_ctx.Agent, false);
                    
                }
                else
                {
                    
                }

                _ctx.EnsureAgentOnNavMesh(sightRadius);
                

                _battleStarted = true;
                _battleFinished = false;

                // Cambiar capa a Enemy al iniciar combate
                _ctx.gameObject.layer = enemyUnityLayer;

                

                // Asegura que la IA de combate esté activa ANTES de cualquier verificación
                if (_combatBrain != null)
                {
                    if (!_combatBrain.enabled)
                        _combatBrain.enabled = true;
                    
                    
                }
                else
                {
                    
                }

                _resolvedHealth = ResolveHealth();
                if (_resolvedHealth != null)
                {
                    if (_ownsHealthComponent)
                        _resolvedHealth.SetMaxAndCurrent(battleHealth, battleHealth);

                    _resolvedHealth.OnDamaged -= HandleNpcDamaged;
                    _resolvedHealth.OnDamaged += HandleNpcDamaged;
                    _resolvedHealth.OnDied -= HandleNpcDied;
                    _resolvedHealth.OnDied += HandleNpcDied;

                    TryDisableDestroyOnDeath(_resolvedHealth);
                }

                _forceHealthVisibleUntilDamage = true;
                ShowHealthBar();

                // CORREGIDO: Limpiar cualquier override de animaci\u00f3n que pueda estar activo (ej. challengeState)
                _ctx.Animator.ClearInteractOverride();

                TriggerBattleMusic();
                onBattleStarted?.Invoke();

                _ctx.DebugLog("Batalla iniciada.");

                // Iniciar IA de combate delegada
                if (_combatBrain != null)
                {
                    // Activar modo batalla en el animador para habilitar idle/upper body
                    _ctx.Animator.SetBattleMode(true);
                    _combatBrain.BeginCombat(BuildCombatSettings());
                    
                }
                else
                {
                    
                }
            }

            NPCCombatBrain.Settings BuildCombatSettings()
            {
                return new NPCCombatBrain.Settings
                {
                    sightRadius = sightRadius,
                    minDistance = combatMinDistance,
                    maxDistance = combatMaxDistance,
                    repathInterval = combatRepathInterval,
                    retreatDistance = combatRetreatDistance,
                    turnSpeed = combatTurnSpeed,
                    upperBodyLayer = upperBodyLayer,
                    battleIdleState = battleIdleState,
                    leftAttack = new NPCCombatBrain.AttackSlot { animationState = lightAttackStateLeft, cooldown = leftAttackCooldown, slotIndex = 0 },
                    rightAttack = new NPCCombatBrain.AttackSlot { animationState = lightAttackStateRight, cooldown = rightAttackCooldown, slotIndex = 1 },
                    specialAttack = new NPCCombatBrain.AttackSlot { animationState = specialAttackState, cooldown = specialAttackCooldown, slotIndex = 2 },
                    aggressiveDistance = Mathf.Max(0f, combatMinDistance + 0.5f),
                    retreatHealthPercent = 0.25f,
                    circleDistance = Mathf.Max(0f, (combatMinDistance + combatMaxDistance) * 0.5f),
                    circleSpeed = 90f,
                    spawnProjectileViaAnimationEvent = spawnProjectileViaAnimationEvent,
                    // Retardo para que se vea el gesto de disparo antes de generar el proyectil
                    fireDelaySeconds = 0.5f,
                    requireLineOfSight = requireLineOfSight,
                    losMask = Physics.DefaultRaycastLayers,
                    windupMin = 0.06f,
                    windupMax = 0.18f,
                    strafeFlipMin = 1.2f,
                    strafeFlipMax = 2.4f,
                    dodgeDistance = 1.2f,
                    dodgeCooldown = 1.6f,
                    // Micro-pausas: tamaño y cadencia
                    microPauseDurationMin = 0.08f,
                    microPauseDurationMax = 0.20f,
                    microPauseIntervalMin = 1.1f,
                    microPauseIntervalMax = 2.0f,
                    // Burst & reposition tras 1–2 ataques
                    burstRepositionDistance = 2.6f,
                    burstRepositionCooldown = 2.4f,
                    burstAttacksMin = 1,
                    burstAttacksMax = 2,
                    // Ventanas de quieto (mantener posición)
                    holdDurationMin = 0.6f,
                    holdDurationMax = 1.4f,
                    holdIntervalMin = 1.2f,
                    holdIntervalMax = 2.4f,
                    attackHoldSeconds = 0.28f,
                    // Dificultad (ajustable por inspector)
                    attackFrequencyMultiplier = attackFrequencyMultiplier,
                    aggressionBias = aggressionBias,
                    dodgeChance = dodgeChance,
                };
            }

            void HandleNpcDamaged(float amount)
            {
                if (_resolvedHealth == null)
                    return;

                _forceHealthVisibleUntilDamage = false;
                // Animar el descenso de vida para que se perciba claramente
                AnimateHealthBarToCurrent(0.25f);

                TryPlayDamageReaction();

                // Solicitar una pequeña esquiva lateral al cerebro de combate (si disponible)
                _combatBrain?.RequestDodge();

                if (_resolvedHealth.Current <= 0f && !_battleFinished)
                    HandleNpcDied();
            }

            void TryPlayDamageReaction()
            {
                if (_ctx == null) return;
                if (damageReactionMinInterval > 0f && _damageReactTimer > 0f) return;

                string state = !string.IsNullOrEmpty(damageStatePrimary) ? damageStatePrimary : damageStateAlternate;
                if (string.IsNullOrEmpty(state)) return;

                _ctx.Animator?.PlayOneShot(state);
                _damageReactTimer = Mathf.Max(0f, damageReactionMinInterval);
            }

            void HandleNpcDied()
            {
                if (_battleFinished)
                    return;
                CameraImpactOnKillMain();
                StartDefeatSequence();
            }

            void StartDefeatSequence()
            {
                if (_defeatSequenceRunning) return;
                _defeatSequenceRunning = true;
                _battleFinished = true;
                _battleStarted = false;
                _combatBrain?.StopCombat();
                _ctx.Animator.SetBattleMode(false);
                
                _ctx.RunCoroutine(DefeatFlow());
            }

            IEnumerator DefeatFlow()
            {
                // Reproducir animación de muerte o explosión y aplicar feedback
                if (!explodeOnDefeat && persistAfterDefeat && !string.IsNullOrEmpty(npcDeathAnimation))
                {
                    // Detener movimiento y preparar animator para muerte
                    NavMeshAgentUtility.HardStop(_ctx.Agent);
                    _ctx.Animator.ResetMovement();
                    _ctx.Animator.SetBattleMode(false);

                    // Desactivar temporalmente el NPCSimpleAnimator para evitar que vuelva a locomotion
                    var simpleAnim = _ctx.GetComponent<NPCSimpleAnimator>();
                    bool reenableSimple = false;
                    if (simpleAnim && simpleAnim.enabled) { simpleAnim.enabled = false; reenableSimple = true; }

                    // Limpiar overrides y forzar muerte con Animator.Play (hash) y fallback CrossFade
                    _ctx.Animator.ClearInteractOverride();
                    var unityAnim = _ctx.GetComponent<UnityEngine.Animator>();
                    if (unityAnim != null)
                    {
                        string shortName = npcDeathAnimation;
                        string fullPath = "Base Layer." + npcDeathAnimation;
                        int hashShort = UnityEngine.Animator.StringToHash(shortName);
                        int hashFull = UnityEngine.Animator.StringToHash(fullPath);
                        try { int layers = unityAnim.layerCount; for (int i = 1; i < layers; i++) unityAnim.SetLayerWeight(i, 0f); } catch { }
                        bool played = false;
                        try { if (unityAnim.HasState(0, hashFull)) { unityAnim.Play(hashFull, 0, 0f); played = true; } } catch { }
                        if (!played) { try { if (unityAnim.HasState(0, hashShort)) { unityAnim.Play(hashShort, 0, 0f); played = true; } } catch { } }
                        if (!played) { try { if (unityAnim.HasState(0, hashFull)) { unityAnim.CrossFadeInFixedTime(hashFull, 0.1f, 0, 0f); played = true; } } catch { } }
                        if (!played) { try { if (unityAnim.HasState(0, hashShort)) { unityAnim.CrossFadeInFixedTime(hashShort, 0.1f, 0, 0f); played = true; } } catch { } }
                        if (!played)
                        {
                            try { unityAnim.Play(fullPath, 0, 0f); played = true; } catch { }
                            if (!played) { try { unityAnim.Play(shortName, 0, 0f); played = true; } catch { } }
                            if (!played) { try { unityAnim.CrossFadeInFixedTime(fullPath, 0.1f, 0, 0f); played = true; } catch { } }
                            if (!played) { try { unityAnim.CrossFadeInFixedTime(shortName, 0.1f, 0, 0f); played = true; } catch { } }
                        }
                        try { unityAnim.Update(0f); } catch { }
                    }

                    // Slow-mo sincronizado con la animación de muerte y mantener pose
                    yield return DeathSlowmoRoutine(unityAnim, npcDeathAnimation);
                    yield return new WaitForSeconds(deathPoseHoldSeconds);

                    if (reenableSimple && simpleAnim) simpleAnim.enabled = true;
                }
                else if (explodeOnDefeat && defeatExplosionPrefab)
                {
                    GameObject.Instantiate(defeatExplosionPrefab, _ctx.transform.position, Quaternion.identity);
                }

                // Recompensas y música
                GrantRewards();
                RestoreBattleMusic();
                onBattleWon?.Invoke();
                HideHealthBar();

                // Animación de victoria del jugador y FX (sin cambio de cámara)
                if (_ctx.Player && !string.IsNullOrEmpty(playerVictoryAnimation))
                {
                    var playerAnim = _ctx.Player.GetComponent<UnityEngine.Animator>();
                    if (playerAnim)
                    {
                        Vector3 toNpc = (_ctx.transform.position - _ctx.Player.position); toNpc.y = 0f;
                        if (toNpc.sqrMagnitude > 0.001f)
                            _ctx.Player.rotation = Quaternion.LookRotation(toNpc.normalized, Vector3.up);
                        // Reproducir victoria y mantener 2s, luego volver a idle
                        playerAnim.CrossFadeInFixedTime(playerVictoryAnimation, 0.12f, 0, 0f);
                        if (victoryFXPrefab)
                        {
                            yield return new WaitForSeconds(victoryFXDelay);
                            GameObject.Instantiate(victoryFXPrefab, _ctx.Player.position + Vector3.up * 1f, Quaternion.identity);
                        }
                        yield return new WaitForSeconds(2.0f);
                        // Volver a locomotion (Free Locomotion)
                        var locomotionState = "Free Locomotion";
                        try
                        {
                            int locoHash = UnityEngine.Animator.StringToHash(locomotionState);
                            if (playerAnim.HasState(0, locoHash))
                                playerAnim.CrossFadeInFixedTime(locoHash, 0.12f, 0, 0f);
                            else
                                playerAnim.CrossFadeInFixedTime(locomotionState, 0.12f, 0, 0f);
                        }
                        catch { }

                        // Rehabilitar control del jugador inmediatamente tras la victoria
                        ReleasePlayer();
                    }
                }

                if (useDialogueAfterBattle && dialogueOnDefeat)
                    _ctx.PlayDialogue(dialogueOnDefeat);

                hasBeenDefeated = true;
                _ctx.gameObject.layer = interactableUnityLayer;
                enable = false;
                if (useDialogueAfterBattle && dialogueOnDefeat)
                    yield return _ctx.WaitDialogueToClose();
                ReleasePlayer();

                // Caminar de vuelta a casa
                _ctx.Animator.SetBattleMode(false);
                if (_ctx.Agent && _ctx.Agent.enabled)
                {
                    if (_ctx.EnsureAgentOnNavMesh(2f))
                    {
                        NavMeshAgentUtility.SetDestination(_ctx.Agent, _homePosition, 0.1f);
                        while (_ctx.Agent.pathPending || _ctx.Agent.remainingDistance > _ctx.Agent.stoppingDistance + 0.05f)
                        {
                            float speed = NavMeshAgentUtility.ComputeSpeedFactor(_ctx.Agent);
                            _ctx.Animator.SetMovementSpeed(speed);
                            yield return null;
                        }
                        NavMeshAgentUtility.HardStop(_ctx.Agent);
                        _ctx.Animator.ResetMovement();
                        _ctx.transform.rotation = _homeRotation; // no teletransporte
                    }
                }
                onBattleFinished?.Invoke();
                _ctx.DebugLog("Batalla finalizada.");
                _defeatSequenceRunning = false;
            }

            IEnumerator DeathSlowmoRoutine(UnityEngine.Animator unityAnim, string deathAnimation)
            {
                // Slow-mo más marcado al derrotar al NPC
                float wait = 0.75f;
                yield return new WaitForSecondsRealtime(wait);
            }

            // Eliminado: efectos de cámara sobre el jugador

            void TryDisableDestroyOnDeath(object health)
            {
                if (health == null) return;
                var t = health.GetType();
                try
                {
                    var m = t.GetMethod("SetDestroyOnDeath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null)
                    {
                        m.Invoke(health, new object[] { false });
                        return;
                    }
                    var f = t.GetField("destroyOnDeath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.FieldType == typeof(bool))
                    {
                        f.SetValue(health, false);
                        return;
                    }
                    var p = t.GetProperty("DestroyOnDeath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
                    {
                        p.SetValue(health, false, null);
                    }
                }
                catch { }
            }

            void CameraImpactOnKillMain()
            {
                _ctx.RunCoroutine(KillImpactRoutineMain());
            }

            IEnumerator KillImpactRoutineMain()
            {
                float originalTimeScale = Time.timeScale;
                float originalFixedDelta = Time.fixedDeltaTime;
                Time.timeScale = 0.2f;
                Time.fixedDeltaTime = originalFixedDelta * Time.timeScale;

                var mainCam = Camera.main ? Camera.main.transform : null;
                Vector3 basePos = mainCam ? mainCam.localPosition : Vector3.zero;
                float duration = 0.6f;
                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    if (mainCam)
                    {
                        float strength = Mathf.Lerp(0.25f, 0f, t / duration);
                        mainCam.localPosition = basePos + new Vector3(
                            UnityEngine.Random.Range(-strength, strength),
                            UnityEngine.Random.Range(-strength, strength),
                            0f);
                    }
                    yield return null;
                }
                if (mainCam) mainCam.localPosition = basePos;
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDelta;
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
                if (!_resolvedHealth || !healthBarPrefab || !healthBarFillImage)
                    return;

                _forceHealthVisibleUntilDamage = true;

                _healthBarRect = healthBarPrefab.GetComponent<RectTransform>();
                _healthBarFill = healthBarFillImage; // Asignación directa desde el inspector
                _healthBarCanvasGroup = healthBarPrefab.GetComponent<CanvasGroup>();
                if (!_healthBarCanvasGroup)
                    _healthBarCanvasGroup = healthBarPrefab.AddComponent<CanvasGroup>();
                if (healthBarPrefab && _battleStarted && !_battleFinished && !healthBarPrefab.activeSelf)
                    healthBarPrefab.SetActive(true);

                healthBarPrefab.SetActive(true);

                if (_healthBarFill)
                    _healthBarFill.fillAmount = 0f;
                if (_healthBarCanvasGroup)
                    _healthBarCanvasGroup.alpha = 1f;

                AnimateHealthBarToCurrent(0.35f);
            }

            void HideHealthBar()
            {
                if (_healthAnimRoutine != null)
                {
                    _ctx.StopCoroutineSafe(_healthAnimRoutine);
                    _healthAnimRoutine = null;
                }
                if (healthBarPrefab)
                    healthBarPrefab.SetActive(false);
                _healthBarRect = null;
                _healthBarFill = null;
                _healthBarCanvasGroup = null;
            }

            void AnimateHealthBarToCurrent(float seconds)
            {
                if (_resolvedHealth == null || _healthBarFill == null)
                    return;
                float target = Mathf.Clamp01(_resolvedHealth.Current / Mathf.Max(1f, _resolvedHealth.Max));
                if (_healthAnimRoutine != null)
                {
                    _ctx.StopCoroutineSafe(_healthAnimRoutine);
                    _healthAnimRoutine = null;
                }
                _healthAnimRoutine = _ctx.RunCoroutine(AnimateHealthBarFill(_healthBarFill.fillAmount, target, Mathf.Max(0.05f, seconds)));
            }

            IEnumerator AnimateHealthBarFill(float from, float to, float seconds)
            {
                if (_healthBarFill == null) yield break;
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / seconds);
                    float v = Mathf.Lerp(from, to, k);
                    if (_healthBarFill == null) yield break;
                    _healthBarFill.fillAmount = v;
                    _healthBarFill.color = GetColorForRatio(v);
                    yield return null;
                }
                if (_healthBarFill == null) yield break;
                _healthBarFill.fillAmount = to;
                _healthBarFill.color = GetColorForRatio(to);
                // Ocultar si está llena y la opción lo indica
                if (_healthBarCanvasGroup)
                {
                    if (_forceHealthVisibleUntilDamage)
                        _healthBarCanvasGroup.alpha = 1f;
                    else if (hideHealthBarWhenFull)
                        _healthBarCanvasGroup.alpha = to >= 0.999f ? 0f : 1f;
                }
                _healthAnimRoutine = null;
            }

            void UpdateHealthBarVisual()
            {
                // No re-posicionar en runtime: el prefab ya está colocado donde corresponde
                return;
            }

            // Eliminado: FindFillImage ya no es necesario (se asigna por inspector)

            Color GetColorForRatio(float ratio)
            {
                if (ratio <= criticalThreshold)
                    return criticalColor;
                if (ratio <= warningThreshold)
                    return warningColor;
                return healthColor;
            }

            // Eliminados métodos FindCanvas / BuildRuntimeCanvas (no usados con referencia directa de prefab)

            void GrantRewards()
            {
                if (rewards == null || rewards.Length == 0)
                    return;

                if (!PlayerService.TryGetInventory(out var inventory))
                {
                    
                    return;
                }

                foreach (var r in rewards)
                {
                    if (!r.item || r.amount <= 0) continue;
                    inventory.Add(r.item, r.amount);
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
    }

    /// <summary>
    /// Entrada de recompensa para otorgar ítems al jugador tras derrotar un NPC.
    /// </summary>
    [Serializable]
    public class RewardEntry
    {
        public ItemData item;
        [Min(1)] public int amount = 1;
    }

    /// <summary>
    /// Evento de Unity que recibe un string como parámetro.
    /// </summary>
    [Serializable]
    public class StringEvent : UnityEvent<string> { }
}

