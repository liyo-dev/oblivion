using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado Cinemático - El NPC ejecuta una secuencia programada de acciones
    /// (movimiento, animaciones, diálogos, etc.) controlada externamente.
    /// Este estado es usado por el grafo narrativo para cinemáticas.
    /// </summary>
    public class CinematicState : NPCStateBase
    {
        #region Fields
        
        private CinematicSequence _currentSequence;
        private bool _sequenceCompleted;
        
        #endregion
        
        #region Properties
        
        public override string StateName => "Cinematic";
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Inicia una secuencia cinemática con validación de parámetros
        /// </summary>
        /// <param name="sequence">La secuencia a ejecutar</param>
        /// <exception cref="ArgumentNullException">Si la secuencia es null</exception>
        public void StartSequence(CinematicSequence sequence)
        {
            _currentSequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            _sequenceCompleted = false;
        }
        
        /// <summary>
        /// Cancela la secuencia actual prematuramente
        /// </summary>
        public void CancelSequence()
        {
            _sequenceCompleted = true;
        }
        
        #endregion
        
        #region State Lifecycle
        
        public override void OnEnter(Common.NPCStateContext context)
        {
            base.OnEnter(context);
            
            context.IsInCinematic = true;
            StopMovement(context);
            
            if (_currentSequence == null)
            {
                context.LogWarning($"[{StateName}] No hay secuencia asignada, completando inmediatamente");
                _sequenceCompleted = true;
            }
        }
        
        public override void OnUpdate(Common.NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (_currentSequence != null && !_sequenceCompleted)
            {
                _currentSequence.Update(context);
                
                if (_currentSequence.IsCompleted)
                {
                    context.Log($"[{StateName}] Secuencia completada");
                    _sequenceCompleted = true;
                }
            }
        }
        
        public override void OnExit(Common.NPCStateContext context)
        {
            base.OnExit(context);
            
            context.IsInCinematic = false;
            
            if (_currentSequence != null)
            {
                _currentSequence.Cleanup(context);
                _currentSequence = null;
            }
        }
        
        public override Common.INPCState CheckTransitions(Common.NPCStateContext context)
        {
            // El estado cinemático solo termina cuando la secuencia se completa
            if (_sequenceCompleted || _currentSequence == null)
            {
                context.Log($"[{StateName}] Secuencia finalizada, volviendo a Idle");
                return new IdleState();
            }
            
            return null; // Continuar en cinemática
        }
        
        #endregion
    }
    
    #region Cinematic Sequences
    
    /// <summary>
    /// Clase base abstracta para secuencias cinemáticas.
    /// Define el contrato para todas las secuencias ejecutables en CinematicState.
    /// </summary>
    public abstract class CinematicSequence
    {
        /// <summary>
        /// Indica si la secuencia ha finalizado su ejecución
        /// </summary>
        public bool IsCompleted { get; protected set; }
        
        /// <summary>
        /// Actualiza la lógica de la secuencia. Llamado cada frame mientras esté activa.
        /// </summary>
        /// <param name="context">Contexto del NPC con acceso a componentes y configuración</param>
        public abstract void Update(Common.NPCStateContext context);
        
        /// <summary>
        /// Limpia recursos y resetea el estado cuando la secuencia termina o es cancelada
        /// </summary>
        /// <param name="context">Contexto del NPC</param>
        public virtual void Cleanup(Common.NPCStateContext context) { }
    }
    
    /// <summary>
    /// Secuencia de movimiento cinemática con fade y teletransporte opcional.
    /// El NPC camina hacia un destino y puede teletransportarse con fade después de un tiempo.
    /// </summary>
    public class MoveToPositionSequence : CinematicSequence
    {
        #region Constants
        
        private const float SPAWN_ANCHOR_SEARCH_RADIUS = 2f;
        private const float ARRIVAL_TOLERANCE = 0.1f;
        private const float FADE_DURATION = 0.3f;
        private const float FADE_HALF_DURATION = 0.15f;
        private const float TURN_AROUND_ANGLE = 180f;
        private const float SQR_SPAWN_ANCHOR_SEARCH_RADIUS = SPAWN_ANCHOR_SEARCH_RADIUS * SPAWN_ANCHOR_SEARCH_RADIUS;
        
        #endregion
        
        #region Fields
        
        private readonly Vector3 _targetPosition;
        private readonly float _maxDuration;
        private readonly bool _turnAroundOnArrival;
        private readonly float _walkDisplayDuration;
        private readonly MonoBehaviour _owner;
        private readonly SpawnAnchor _targetAnchor; // ✅ Anchor de destino explícito (opcional)
        private readonly bool _lockPlayer; // ✅ false para movimientos ambientales sin interacción real con el jugador
        
        private float _timer;
        private bool _hasSetDestination;
        private bool _hasStartedFade;
        private bool _hasTeleported;
        private bool _playerLocked;
        private Coroutine _activeCoroutine;
        
        // ✅ NUEVO: Guardar estado original de obstacle avoidance para restaurarlo
        private UnityEngine.AI.ObstacleAvoidanceType _originalObstacleAvoidanceType;
        private bool _hasDisabledObstacleAvoidance;
        
        // Cache para optimización de búsqueda de SpawnAnchors
        private static SpawnAnchor[] _cachedSpawnAnchors;
        private static float _lastCacheTime;
        private const float CACHE_REFRESH_INTERVAL = 5f;
        
        #endregion
        
        #region Constructor

        /// <summary>
        /// Constructor de la secuencia de movimiento con fade
        /// </summary>
        /// <param name="owner">MonoBehaviour dueño para ejecutar corrutinas</param>
        /// <param name="targetPosition">Posición de destino</param>
        /// <param name="maxDuration">Duración máxima antes de timeout (default: 15s)</param>
        /// <param name="turnAroundOnArrival">¿Girar 180° al llegar? (solo si no hay SpawnAnchor)</param>
        /// <param name="walkDisplayDuration">Tiempo caminando antes de hacer fade (default: 999s = sin fade)</param>
        /// <param name="targetAnchor">SpawnAnchor de destino (opcional, si se conoce). Evita búsqueda.</param>
        /// <param name="lockPlayer">¿Bloquear el input del jugador (PlayerLockService) mientras dura el movimiento?
        /// Por defecto true (comportamiento histórico: pensado para cinemáticas de interacción donde el jugador
        /// ya está mirando/congelado). Pasar false para movimientos ambientales de fondo donde el jugador debe
        /// seguir jugando con normalidad mientras el NPC camina (ej: bucle de espera).</param>
        public MoveToPositionSequence(MonoBehaviour owner, Vector3 targetPosition, float maxDuration = 15f,
            bool turnAroundOnArrival = false, float walkDisplayDuration = 999f, SpawnAnchor targetAnchor = null,
            bool lockPlayer = true)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _targetPosition = targetPosition;
            _maxDuration = maxDuration;
            _turnAroundOnArrival = turnAroundOnArrival;
            _walkDisplayDuration = walkDisplayDuration;
            _targetAnchor = targetAnchor;
            _lockPlayer = lockPlayer;
        }
        
        #endregion
        
        #region Update Logic
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            // Inicialización en primer frame
            if (!_hasSetDestination)
            {
                InitializeSequence(context);
                return;
            }
            
            _timer += Time.deltaTime;
            
            // Check 1: Llegada natural antes del fade
            if (!_hasStartedFade && HasReachedDestination(context))
            {
                context.Log("[CinematicSequence] Destino alcanzado naturalmente");
                HandleArrival(context);
                CleanupAndComplete(context);
                return;
            }
            
            // Check 2: Iniciar fade y teletransporte después del tiempo especificado
            if (!_hasStartedFade && _timer >= _walkDisplayDuration)
            {
                _hasStartedFade = true;
                context.Log($"[CinematicSequence] {_walkDisplayDuration}s transcurridos, iniciando fade");
                
                if (_owner != null)
                {
                    _activeCoroutine = _owner.StartCoroutine(FadeAndTeleportCoroutine(context));
                }
                return;
            }
            
            // Check 3: Timeout de seguridad
            if (_timer >= _maxDuration)
            {
                context.LogWarning($"[CinematicSequence] Timeout alcanzado ({_maxDuration}s)");
                HandleArrival(context);
                CleanupAndComplete(context);
                return;
            }
            
            // Actualizar animación de movimiento
            UpdateMovementAnimation(context);
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Inicializa la secuencia: bloquea el player y establece el destino del NavMeshAgent
        /// </summary>
        private void InitializeSequence(Common.NPCStateContext context)
        {
            // Bloquear movimiento del jugador durante la cinemática (solo si este movimiento
            // representa una interacción real; los movimientos ambientales de fondo no deben tocar
            // el input del jugador — ver parámetro lockPlayer del constructor).
            if (_lockPlayer && PlayerLockService.HasInstance)
            {
                PlayerLockService.Instance.Acquire(this);
                _playerLocked = true;
            }
            
            // Validar que el agent esté en el NavMesh
            if (context.Agent == null || !context.Agent.isOnNavMesh)
            {
                context.LogWarning("[CinematicSequence] Agent inválido o fuera del NavMesh");
                CleanupAndComplete(context);
                return;
            }
            
            // Desactivar obstacle avoidance durante movimiento de cinemática para que
            // Player y Party Members no bloqueen al NPC. Los objetos que deben ser obstáculos
            // deben tener NavMeshObstacle con Carve=true (o estar bakeados en el NavMesh),
            // no depender de RVO para ser evitados.
            _originalObstacleAvoidanceType = context.Agent.obstacleAvoidanceType;
            context.Agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
            _hasDisabledObstacleAvoidance = true;
            context.Log($"[CinematicSequence] Obstacle avoidance desactivado temporalmente (era: {_originalObstacleAvoidanceType})");

            // Asegurar que no quede la pose de interacción del diálogo al iniciar movimiento.
            if (context.Animator != null)
            {
                context.Animator.SetTalking(false);
                context.Animator.EndInteraction();
                context.Animator.TransitionToLocomotion();
            }
            
            // Establecer destino
            Common.NavMeshAgentUtility.SetDestination(context.Agent, _targetPosition);
            _hasSetDestination = true;
            context.Log($"[CinematicSequence] Destino establecido: {_targetPosition}");
        }
        
        #endregion
        
        #region Movement & Animation
        
        /// <summary>
        /// Actualiza la animación de movimiento basada en la velocidad del NavMeshAgent
        /// </summary>
        private void UpdateMovementAnimation(Common.NPCStateContext context)
        {
            if (_hasTeleported || context.Agent == null || context.Animator == null)
                return;
            
            float speedFactor = Common.NavMeshAgentUtility.ComputeSpeedFactor(context.Agent);
            context.Animator.SetMovementSpeed(speedFactor);
            
            // ✅ FIX: Rotar hacia la dirección del movimiento para evitar caminar de espaldas
            if (context.Agent.velocity.sqrMagnitude > 0.01f)
            {
                Vector3 moveDirection = context.Agent.velocity.normalized;
                context.Animator.FaceDirection(moveDirection);
            }
        }
        
        /// <summary>
        /// Verifica si el NPC ha llegado al destino
        /// </summary>
        private bool HasReachedDestination(Common.NPCStateContext context)
        {
            var agent = context.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending)
                return false;
            
            float stoppingDist = context.Config?.stoppingDistance ?? 0.5f;
            return agent.remainingDistance <= stoppingDist + ARRIVAL_TOLERANCE;
        }
        
        #endregion
        
        #region Fade & Teleport
        
        /// <summary>
        /// Corrutina que ejecuta el fade a negro, teletransporte y fade de regreso
        /// </summary>
        private System.Collections.IEnumerator FadeAndTeleportCoroutine(Common.NPCStateContext context)
        {
            context.Log("[CinematicSequence] Iniciando fade y teletransporte");
            
            // Fade a negro
            Sendero.Core.Feedback.FeedbackService.ScreenFlash(Color.black, FADE_DURATION);
            yield return new WaitForSeconds(FADE_HALF_DURATION);
            
            // Teletransportar
            if (context.Agent != null)
            {
                Common.NavMeshAgentUtility.HardStop(context.Agent);
                context.Transform.position = _targetPosition;
                _hasTeleported = true;
                context.Log($"[CinematicSequence] NPC teletransportado a {_targetPosition}");
            }
            else
            {
                context.LogWarning("[CinematicSequence] Agent es NULL durante teletransporte");
            }
            
            // Manejar llegada (orientación)
            HandleArrival(context);
            
            // Esperar fin del fade
            yield return new WaitForSeconds(FADE_HALF_DURATION);
            
            // Completar
            CleanupAndComplete(context);
        }
        
        #endregion
        
        #region Arrival Handling
        
        /// <summary>
        /// Maneja la llegada al destino: ajusta orientación según turnAroundOnArrival o SpawnAnchor
        /// </summary>
        private void HandleArrival(Common.NPCStateContext context)
        {
            // Detener el NavMeshAgent ANTES de aplicar orientación para evitar que
            // SyncWithNavMeshAgent sobrescriba la rotación en el siguiente frame
            if (context.Agent != null)
            {
                Common.NavMeshAgentUtility.HardStop(context.Agent);
                context.Log("[CinematicSequence] ✅ NavMeshAgent detenido completamente");
            }

            if (context.Animator != null)
            {
                context.Animator.ResetMovement();
            }

            // turnAroundOnArrival tiene prioridad sobre la orientación del anchor
            if (_turnAroundOnArrival)
            {
                ApplyFallbackOrientation(context);
            }
            else
            {
                SpawnAnchor anchor = _targetAnchor ?? FindNearbySpawnAnchor(_targetPosition);
                if (anchor != null)
                {
                    context.Log($"[CinematicSequence] Usando SpawnAnchor '{anchor.anchorId}' para orientación");
                    ApplySpawnAnchorOrientation(context, anchor);
                }
                else
                {
                    // ✅ Diagnóstico: si esto aparece, el NPC llegó sin encontrar ningún SpawnAnchor
                    // (ni el explícito por nombre ni uno cercano), así que mantiene la rotación que
                    // tenía al terminar de caminar. Revisar: anchorId, si el anchor está activo/registrado
                    // en AnchorRegistry, y si _targetPosition coincide con la posición real del anchor.
                    context.LogWarning($"[CinematicSequence] ⚠️ No se encontró SpawnAnchor en destino {_targetPosition} " +
                        $"(targetAnchor explícito: {(_targetAnchor != null ? _targetAnchor.anchorId : "null")}). " +
                        "El NPC mantiene su rotación de movimiento actual.");
                }
            }
        }
        
        /// <summary>
        /// Aplica la orientación definida por un SpawnAnchor (el NPC mira en la dirección del eje Z del anchor)
        /// </summary>
        private void ApplySpawnAnchorOrientation(Common.NPCStateContext context, SpawnAnchor anchor)
        {
            // ✅ Única fuente de verdad para la rotación de un SpawnAnchor: SpawnAnchor.GetCharacterRotation().
            // Antes este método recalculaba la rotación a mano (Quaternion.LookRotation(anchor.transform.forward, ...)),
            // duplicando la lógica y además ignorando el flag 'faceDoor' del anchor.
            Quaternion targetRotation = anchor.GetCharacterRotation();

            context.Log($"[CinematicSequence] SpawnAnchor '{anchor.anchorId}': forward={anchor.transform.forward}, faceDoor={anchor.faceDoor}");
            context.Log($"[CinematicSequence] Target rotation (euler): {targetRotation.eulerAngles}");
            
            // ✅ NUEVO: Aplicar POSICIÓN exacta del anchor (el NavMesh puede no permitir llegar exactamente)
            Vector3 targetPosition = anchor.transform.position;
            float distanceBefore = Vector3.Distance(context.Transform.position, targetPosition);
            context.Log($"[CinematicSequence] NPC position antes: {context.Transform.position} (distancia al anchor: {distanceBefore:F2}m)");
            
            // Colocar al NPC exactamente en la posición del anchor
            context.Transform.position = targetPosition;
            context.Log($"[CinematicSequence] NPC position después: {context.Transform.position}");
            
            // ✅ Aplicar rotación directamente
            context.Transform.rotation = targetRotation;
            
            // ✅ Sincronizar rotación objetivo del NPCSimpleAnimator
            // Desactivar temporalmente la rotación automática para evitar que LateUpdate la sobrescriba
            if (context.Animator != null)
            {
                context.Animator.DisableAutoRotation();
                
                Vector3 forward = targetRotation * Vector3.forward;
                context.Animator.FaceDirection(forward);
                
                // Reactivar rotación automática después de 1 frame
                if (_owner != null)
                {
                    _owner.StartCoroutine(ReenableAutoRotationNextFrame(context.Animator));
                }
                
                context.Log($"[CinematicSequence] ✅ Rotación objetivo del animator sincronizada");
            }
        }
        
        /// <summary>
        /// Reactiva la rotación automática del animator después de 1 frame
        /// </summary>
        private System.Collections.IEnumerator ReenableAutoRotationNextFrame(NPCSimpleAnimator animator)
        {
            yield return null; // Esperar 1 frame
            if (animator != null)
            {
                animator.EnableAutoRotation();
            }
        }
        
        /// <summary>
        /// Aplica orientación fallback (giro de 180°) cuando no hay SpawnAnchor
        /// </summary>
        private void ApplyFallbackOrientation(Common.NPCStateContext context)
        {
            var newRotation = context.Transform.rotation * Quaternion.Euler(0, TURN_AROUND_ANGLE, 0);
            context.Transform.rotation = newRotation;
            context.Log("[CinematicSequence] Girado 180°");

            if (context.Animator != null)
            {
                // DisableAutoRotation impide que SyncWithNavMeshAgent/ApplySmoothRotation
                // en el LateUpdate del mismo frame sobreescriban la rotación aplicada.
                context.Animator.DisableAutoRotation();

                Vector3 forward = newRotation * Vector3.forward;
                context.Animator.FaceDirection(forward);

                if (_owner != null)
                    _owner.StartCoroutine(ReenableAutoRotationNextFrame(context.Animator));
            }
        }
        
        #endregion
        
        #region SpawnAnchor Search (OPTIMIZED)
        
        /// <summary>
        /// Busca un SpawnAnchor cerca de la posición usando caché y optimizaciones de rendimiento.
        /// OPTIMIZACIÓN: Usa caché temporal + sqrMagnitude en lugar de Distance + NonAlloc para evitar GC
        /// </summary>
        private static SpawnAnchor FindNearbySpawnAnchor(Vector3 position)
        {
            if (_cachedSpawnAnchors == null || Time.time - _lastCacheTime > CACHE_REFRESH_INTERVAL)
            {
                _cachedSpawnAnchors = UnityEngine.Object.FindObjectsByType<SpawnAnchor>();
                _lastCacheTime = Time.time;
            }

            SpawnAnchor closest = null;
            float closestSqrDistance = SQR_SPAWN_ANCHOR_SEARCH_RADIUS;

            foreach (var anchor in _cachedSpawnAnchors)
            {
                if (anchor == null) continue;
                float sqrDistance = (anchor.transform.position - position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = anchor;
                }
            }

            return closest;
        }
        
        #endregion
        
        #region Cleanup
        
        /// <summary>
        /// Completa la secuencia y libera recursos
        /// </summary>
        private void CleanupAndComplete(Common.NPCStateContext context)
        {
            RestoreObstacleAvoidance(context);
            ReleasePlayerLock();
            IsCompleted = true;
        }
        
        /// <summary>
        /// Limpieza forzada cuando la secuencia se cancela o el estado cambia
        /// </summary>
        public override void Cleanup(Common.NPCStateContext context)
        {
            // Detener corrutina activa si existe
            if (_activeCoroutine != null && _owner != null)
            {
                _owner.StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
            
            RestoreObstacleAvoidance(context);
            ReleasePlayerLock();
            
            if (context.Agent != null)
            {
                Common.NavMeshAgentUtility.HardStop(context.Agent);
            }
            
            if (context.Animator != null)
            {
                context.Animator.ResetMovement();
            }
        }
        
        /// <summary>
        /// Restaura el obstacle avoidance a su valor original
        /// </summary>
        private void RestoreObstacleAvoidance(Common.NPCStateContext context)
        {
            if (_hasDisabledObstacleAvoidance && context.Agent != null && context.Agent.isOnNavMesh)
            {
                context.Agent.obstacleAvoidanceType = _originalObstacleAvoidanceType;
                _hasDisabledObstacleAvoidance = false;
                context.Log($"[CinematicSequence] Obstacle avoidance restaurado a: {_originalObstacleAvoidanceType}");
            }
        }
        
        /// <summary>
        /// Libera el bloqueo del jugador de forma segura
        /// </summary>
        private void ReleasePlayerLock()
        {
            if (_playerLocked && PlayerLockService.HasInstance)
            {
                PlayerLockService.Instance.Release(this);
                _playerLocked = false;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Secuencia compuesta de múltiples acciones ejecutadas secuencialmente.
    /// Permite encadenar múltiples CinematicAction para crear cinemáticas complejas.
    /// </summary>
    public class CompositeSequence : CinematicSequence
    {
        #region Fields
        
        private readonly List<CinematicAction> _actions;
        private int _currentActionIndex;
        
        #endregion
        
        #region Constructor
        
        public CompositeSequence()
        {
            _actions = new List<CinematicAction>();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Añade una acción a la secuencia (patrón fluent/builder)
        /// </summary>
        /// <param name="action">Acción a añadir</param>
        /// <returns>Esta instancia para encadenar llamadas</returns>
        public CompositeSequence AddAction(CinematicAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
                
            _actions.Add(action);
            return this;
        }
        
        #endregion
        
        #region Update Logic
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            if (_actions.Count == 0)
            {
                IsCompleted = true;
                return;
            }
            
            // Ejecutar acción actual
            var currentAction = _actions[_currentActionIndex];
            currentAction.Update(context);
            
            // Avanzar a la siguiente acción si se completó
            if (currentAction.IsCompleted)
            {
                context.Log($"[CompositeSequence] Acción {_currentActionIndex + 1}/{_actions.Count} completada");
                _currentActionIndex++;
                
                // Completar si no hay más acciones
                if (_currentActionIndex >= _actions.Count)
                {
                    context.Log("[CompositeSequence] Todas las acciones completadas");
                    IsCompleted = true;
                }
            }
        }
        
        #endregion
        
        #region Cleanup
        
        public override void Cleanup(Common.NPCStateContext context)
        {
            foreach (var action in _actions)
            {
                action?.Cleanup(context);
            }
        }
        
        #endregion
    }
    
    #endregion
    
    #region Cinematic Actions
    
    /// <summary>
    /// Clase base abstracta para acciones individuales dentro de una secuencia cinemática.
    /// Cada acción representa una tarea atómica (mover, esperar, rotar, etc.)
    /// </summary>
    public abstract class CinematicAction
    {
        /// <summary>
        /// Indica si la acción ha finalizado
        /// </summary>
        public bool IsCompleted { get; protected set; }
        
        /// <summary>
        /// Actualiza la lógica de la acción. Llamado cada frame.
        /// </summary>
        /// <param name="context">Contexto del NPC</param>
        public abstract void Update(Common.NPCStateContext context);
        
        /// <summary>
        /// Limpia recursos cuando la acción termina o se cancela
        /// </summary>
        /// <param name="context">Contexto del NPC</param>
        public virtual void Cleanup(Common.NPCStateContext context) { }
    }
    
    /// <summary>
    /// Acción: Mover el NPC a una posición específica usando NavMeshAgent
    /// </summary>
    public class MoveToAction : CinematicAction
    {
        #region Constants
        
        private const float ARRIVAL_TOLERANCE = 0.1f;
        
        #endregion
        
        #region Fields
        
        private readonly Vector3 _targetPosition;
        private readonly float _maxDuration;
        private bool _hasSetDestination;
        private float _timer;
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Constructor de la acción de movimiento
        /// </summary>
        /// <param name="targetPosition">Posición objetivo</param>
        /// <param name="maxDuration">Duración máxima antes de timeout (default: 10s)</param>
        public MoveToAction(Vector3 targetPosition, float maxDuration = 10f)
        {
            _targetPosition = targetPosition;
            _maxDuration = maxDuration;
        }
        
        #endregion
        
        #region Update Logic
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            // Establecer destino en el primer frame
            if (!_hasSetDestination)
            {
                if (context.Agent != null && context.Agent.isOnNavMesh)
                {
                    Common.NavMeshAgentUtility.SetDestination(context.Agent, _targetPosition);
                    _hasSetDestination = true;
                }
                else
                {
                    context.LogWarning("[MoveToAction] Agent inválido, completando inmediatamente");
                    IsCompleted = true;
                    return;
                }
            }
            
            _timer += Time.deltaTime;
            
            // Timeout de seguridad
            if (_timer >= _maxDuration)
            {
                IsCompleted = true;
                return;
            }
            
            // Actualizar animación de movimiento
            if (context.Agent != null && context.Animator != null)
            {
                float speedFactor = Common.NavMeshAgentUtility.ComputeSpeedFactor(context.Agent);
                context.Animator.SetMovementSpeed(speedFactor);
            }
            
            // Verificar llegada al destino
            var agent = context.Agent;
            if (agent != null && !agent.pathPending && agent.isOnNavMesh)
            {
                float stoppingDist = agent.stoppingDistance;
                if (agent.remainingDistance <= stoppingDist + ARRIVAL_TOLERANCE)
                {
                    IsCompleted = true;
                }
            }
        }
        
        #endregion
        
        #region Cleanup
        
        public override void Cleanup(Common.NPCStateContext context)
        {
            if (context.Agent != null)
            {
                Common.NavMeshAgentUtility.HardStop(context.Agent);
            }
            
            if (context.Animator != null)
            {
                context.Animator.ResetMovement();
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Acción: Esperar un tiempo determinado
    /// </summary>
    public class WaitAction : CinematicAction
    {
        #region Fields
        
        private readonly float _duration;
        private float _timer;
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Constructor de la acción de espera
        /// </summary>
        /// <param name="duration">Duración de la espera en segundos</param>
        public WaitAction(float duration)
        {
            _duration = Mathf.Max(0f, duration);
        }
        
        #endregion
        
        #region Update Logic
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            _timer += Time.deltaTime;
            
            if (_timer >= _duration)
            {
                IsCompleted = true;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Acción: Reproducir una animación mediante trigger
    /// </summary>
    public class PlayAnimationAction : CinematicAction
    {
        #region Fields
        
        private readonly string _animationTrigger;
        private readonly float _duration;
        private float _timer;
        private bool _triggerActivated;
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Constructor de la acción de animación
        /// </summary>
        /// <param name="animationTrigger">Nombre del trigger en el Animator</param>
        /// <param name="duration">Duración de la animación en segundos</param>
        public PlayAnimationAction(string animationTrigger, float duration)
        {
            if (string.IsNullOrEmpty(animationTrigger))
                throw new ArgumentException("El trigger de animación no puede estar vacío", nameof(animationTrigger));
                
            _animationTrigger = animationTrigger;
            _duration = Mathf.Max(0f, duration);
        }
        
        #endregion
        
        #region Update Logic
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            // Activar trigger en el primer frame
            if (!_triggerActivated && context.UnityAnimator != null)
            {
                context.UnityAnimator.SetTrigger(_animationTrigger);
                context.Log($"[PlayAnimationAction] Trigger '{_animationTrigger}' activado");
                _triggerActivated = true;
            }
            
            _timer += Time.deltaTime;
            
            if (_timer >= _duration)
            {
                IsCompleted = true;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Acción: Rotar el NPC hacia una orientación específica
    /// </summary>
    public class RotateToAction : CinematicAction
    {
        #region Constants
        
        private const float MIN_DURATION = 0.01f;
        
        #endregion
        
        #region Fields
        
        private readonly Quaternion _targetRotation;
        private readonly float _duration;
        private Quaternion _startRotation;
        private float _timer;
        private bool _initialized;
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Constructor de la acción de rotación
        /// </summary>
        /// <param name="targetRotation">Rotación objetivo</param>
        /// <param name="duration">Duración de la interpolación (default: 0.5s)</param>
        public RotateToAction(Quaternion targetRotation, float duration = 0.5f)
        {
            _targetRotation = targetRotation;
            _duration = Mathf.Max(MIN_DURATION, duration);
        }
        
        #endregion
        
        #region Update Logic
        
        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted)
                return;
            
            // Inicializar en el primer frame
            if (!_initialized)
            {
                _startRotation = context.Transform.rotation;
                _initialized = true;
            }
            
            _timer += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(_timer / _duration);
            
            // Interpolar rotación
            context.Transform.rotation = Quaternion.Slerp(_startRotation, _targetRotation, normalizedTime);
            
            // Completar cuando se alcanza el tiempo total
            if (normalizedTime >= 1f)
            {
                IsCompleted = true;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Secuencia de escolta: el NPC camina hacia un anchor mientras el jugador le sigue.
    /// Si el jugador se aleja, el NPC para y vuelve a buscarle antes de reanudar.
    /// Se ejecuta dentro de CinematicState para que el Brain no interfiera con el agente.
    /// </summary>
    public class LeadPlayerToAnchorSequence : CinematicSequence
    {
        private readonly Vector3 _anchorPos;
        private readonly Transform _player;
        private readonly float _maxDuration;
        private readonly float _escortMaxDist;
        private readonly float _escortResumeDist;

        private float _timer;
        private float _fetchUpdateTimer;
        private float _startupTimer;
        private bool _initialized;
        private bool _startupDone;
        private bool _fetchingPlayer;
        private float _baseSpeed;

        // Segundos que el guardia espera mirando al jugador antes de empezar a caminar.
        private const float STARTUP_WAIT = 1.5f;

        /// <summary>
        /// Verdadero cuando el NPC acaba de recuperar al jugador y está esperando que se reproduzca el diálogo.
        /// El executor debe llamar AcknowledgePlayerRetrieved() para reanudar la escolta.
        /// </summary>
        public bool WaitingForRetrievedDialogue { get; private set; }

        public LeadPlayerToAnchorSequence(Vector3 anchorPos, Transform player,
            float maxDuration, float escortMaxDist, float escortResumeDist)
        {
            _anchorPos      = anchorPos;
            _player         = player;
            _maxDuration    = maxDuration;
            _escortMaxDist  = escortMaxDist;
            _escortResumeDist = escortResumeDist;
        }

        public override void Update(Common.NPCStateContext context)
        {
            if (IsCompleted) return;

            var agent = context.Agent;

            // Validación de requisitos críticos
            if (agent == null || _player == null)
            {
                UnityEngine.Debug.LogError($"[LeadPlayerToAnchorSequence] ❌ Requisito nulo: agent={agent != null}, player={_player != null}. Abortando.");
                IsCompleted = true; return;
            }

            // Si el agente está desactivado (p.ej. por NavMeshAgentForceStop en IdleState anterior),
            // reactivarlo y esperar un frame para que vuelva al NavMesh.
            if (!agent.enabled)
            {
                UnityEngine.Debug.LogWarning($"[LeadPlayerToAnchorSequence] ⚠️ NavMeshAgent desactivado, reactivando...");
                agent.enabled = true;
                return;
            }

            if (!agent.isOnNavMesh)
            {
                UnityEngine.Debug.LogError($"[LeadPlayerToAnchorSequence] ❌ Agente fuera del NavMesh (enabled={agent.enabled}). Abortando.");
                IsCompleted = true; return;
            }

            // Primer frame: preparar escolta y esperar STARTUP_WAIT segundos antes de andar
            if (!_initialized)
            {
                // Salir de la pose de diálogo/idle (sin activar locomoción todavía)
                context.Animator?.SetTalking(false);
                context.Animator?.EndInteraction();

                float startDist = Vector3.Distance(context.Transform.position, _anchorPos);
                context.Log($"[LeadPlayerToAnchorSequence] Iniciando fase de espera ({STARTUP_WAIT}s). distAnchor={startDist:F2}, isOnNavMesh={agent.isOnNavMesh}");
                _baseSpeed = agent.speed;
                _initialized = true;
                agent.isStopped = true;
                return;
            }

            // Fase de espera inicial: el guardia mira al jugador antes de empezar a andar
            if (!_startupDone)
            {
                _startupTimer += Time.deltaTime;
                if (_player != null)
                {
                    Vector3 dir = _player.position - context.Transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        context.Animator?.FaceDirection(dir.normalized);
                }
                if (_startupTimer >= STARTUP_WAIT)
                {
                    _startupDone = true;
                    agent.isStopped = false;
                    if (!agent.SetDestination(_anchorPos))
                        UnityEngine.Debug.LogWarning($"[LeadPlayerToAnchorSequence] ⚠️ SetDestination falló para {_anchorPos}. El anchor puede estar fuera del NavMesh o en una superficie no conectada.");
                    context.Log($"[LeadPlayerToAnchorSequence] Startup completado, iniciando marcha al anchor.");
                    context.Animator?.TransitionToLocomotion();
                }
                return;
            }

            // Segundo frame: verificar si el agente tiene ruta válida
            if (_timer == 0f && !agent.pathPending)
            {
                if (!agent.hasPath)
                    UnityEngine.Debug.LogError($"[LeadPlayerToAnchorSequence] ❌ No hay ruta válida al anchor {_anchorPos}. Comprueba que el anchor esté en el NavMesh y conectado con la posición actual del guardia.");
                else
                    context.Log($"[LeadPlayerToAnchorSequence] Ruta calculada. Distancia NavMesh={agent.remainingDistance:F1}m");
            }

            _timer += Time.deltaTime;
            if (_timer >= _maxDuration)
            {
                UnityEngine.Debug.LogWarning($"[LeadPlayerToAnchorSequence] ⏱️ Tiempo agotado ({_maxDuration}s).");
                IsCompleted = true; return;
            }

            // Si estamos esperando que el executor reproduzca el diálogo, no hacer nada más
            if (WaitingForRetrievedDialogue) return;

            float distToPlayer = Vector3.Distance(context.Transform.position, _player.position);

            if (!_fetchingPlayer)
            {
                // Comprobar llegada al anchor
                bool navArrived  = !agent.pathPending && agent.hasPath && agent.remainingDistance < 0.6f;
                float distToAnchor = Vector3.Distance(context.Transform.position, _anchorPos);
                bool realArrived = distToAnchor < 0.8f;
                if (navArrived && realArrived)
                {
                    context.Log($"[LeadPlayerToAnchorSequence] Llegada al anchor (navDist={agent.remainingDistance:F2}, realDist={distToAnchor:F2})");
                    IsCompleted = true; return;
                }

                // Jugador demasiado lejos → ir a buscarlo
                if (distToPlayer > _escortMaxDist)
                {
                    _fetchingPlayer = true;
                    agent.speed = _baseSpeed;
                    // Limpiar la ruta anterior antes de cambiar destino.
                    // Sin ResetPath el agente navega desde un waypoint intermedio
                    // de la ruta vieja, provocando el snap visual ("teleport").
                    agent.ResetPath();
                    agent.SetDestination(_player.position);
                }
                else
                {
                    // Reducir velocidad progresivamente cuando el jugador se va quedando atrás.
                    // distToPlayer == _escortResumeDist → velocidad máxima
                    // distToPlayer == _escortMaxDist   → velocidad mínima (20 %)
                    float t = Mathf.Clamp01((distToPlayer - _escortResumeDist) / (_escortMaxDist - _escortResumeDist));
                    agent.speed = Mathf.Lerp(_baseSpeed, _baseSpeed * 0.2f, t);
                }
            }
            else
            {
                // Actualizar destino hacia el jugador 4 veces/s
                _fetchUpdateTimer += Time.deltaTime;
                if (_fetchUpdateTimer >= 0.25f)
                {
                    agent.SetDestination(_player.position);
                    _fetchUpdateTimer = 0f;
                }

                // Jugador cerca de nuevo → parar y señalar al executor para el diálogo
                if (distToPlayer <= _escortResumeDist)
                {
                    _fetchingPlayer = false;
                    WaitingForRetrievedDialogue = true;
                    agent.isStopped = true;
                }
            }

            // Animación de movimiento
            if (context.Animator != null && context.Agent != null)
            {
                float speedFactor = Common.NavMeshAgentUtility.ComputeSpeedFactor(context.Agent);
                context.Animator.SetMovementSpeed(speedFactor);
                if (context.Agent.velocity.sqrMagnitude > 0.01f)
                    context.Animator.FaceDirection(context.Agent.velocity.normalized);
            }
        }

        /// <summary>
        /// Llamar desde el executor tras reproducir el diálogo de "jugador recuperado".
        /// Reanuda la marcha hacia el anchor.
        /// </summary>
        public void AcknowledgePlayerRetrieved(Common.NPCStateContext context)
        {
            WaitingForRetrievedDialogue = false;
            var agent = context.Agent;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = _baseSpeed;
                agent.isStopped = false;
                agent.ResetPath(); // Limpiar ruta de fetch antes de volver al anchor
                agent.SetDestination(_anchorPos);
            }
            // Arrancar animación de andar inmediatamente sin esperar a que agent.velocity tenga magnitud
            context.Animator?.TransitionToLocomotion();
            context.Animator?.SetMovementSpeed(1f);
        }

        public override void Cleanup(Common.NPCStateContext context)
        {
            var agent = context.Agent;
            if (agent != null)
                Common.NavMeshAgentUtility.HardStop(agent);
            context.Animator?.ResetMovement();
        }
    }

    #endregion
}

