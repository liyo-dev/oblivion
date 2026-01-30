using System;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC.Movement
{
    /// <summary>
    /// Sistema centralizado de movimiento para NPCs.
    /// TODO el movimiento de NPCs (Combat, Party, States) pasa por aquí.
    /// CERO delays, CERO "yield return null", sistema profesional y robusto.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCMovementController : MonoBehaviour
    {
        #region Events
        /// <summary>
        /// Se dispara cuando el NPC llega a su destino
        /// </summary>
        public event Action OnDestinationReached;
        
        /// <summary>
        /// Se dispara cuando el movimiento es bloqueado (path inválido, etc)
        /// </summary>
        public event Action<string> OnMovementBlocked;
        
        /// <summary>
        /// Se dispara cuando el NPC empieza a moverse
        /// </summary>
        public event Action OnMovementStarted;
        
        /// <summary>
        /// Se dispara cuando el NPC se detiene
        /// </summary>
        public event Action OnMovementStopped;
        #endregion

        #region Configuration
        [Header("Movement Settings")]
        [SerializeField] private float defaultWalkSpeed = 3.5f;
        [SerializeField] private float defaultRunSpeed = 7.5f;
        [SerializeField] private float stoppingDistance = 0.5f;
        [SerializeField] private float pathUpdateInterval = 0.2f;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region State
        private NavMeshAgent _agent;
        private NPCSimpleAnimator _animator;
        
        private Vector3 _currentDestination;
        private bool _isMoving;
        private bool _hasActiveDestination;
        private float _pathUpdateTimer;
        private MovementMode _currentMode;
        private float _currentSpeed;
        
        private bool _isInitialized;
        #endregion

        #region Properties
        public bool IsMoving => _isMoving;
        public bool HasActiveDestination => _hasActiveDestination;
        public Vector3 CurrentDestination => _currentDestination;
        public MovementMode CurrentMode => _currentMode;
        public float CurrentSpeed => _currentSpeed;
        public bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// Distancia restante al destino
        /// </summary>
        public float RemainingDistance
        {
            get
            {
                if (!_isInitialized || !_agent.isOnNavMesh || !_hasActiveDestination)
                    return float.MaxValue;
                return _agent.remainingDistance;
            }
        }
        
        /// <summary>
        /// ¿Ha llegado al destino?
        /// </summary>
        public bool HasReachedDestination
        {
            get
            {
                if (!_hasActiveDestination) return false;
                return RemainingDistance <= stoppingDistance && !_agent.pathPending;
            }
        }
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<NPCSimpleAnimator>();
            
            if (_agent == null)
            {
                Debug.LogError($"[MovementController:{name}] ❌ NavMeshAgent no encontrado!");
                return;
            }
            
            // Configurar agent
            _agent.updateRotation = false; // NPCSimpleAnimator controla la rotación
            _agent.stoppingDistance = stoppingDistance;
            _agent.speed = defaultWalkSpeed;
            
            _isInitialized = true;
            
            if (debugMode)
                Debug.Log($"[MovementController:{name}] ✅ Inicializado");
        }

        void Update()
        {
            if (!_isInitialized || !_agent.isOnNavMesh) return;
            
            UpdateMovementState();
            
            // Actualizar path periódicamente si está en movimiento
            if (_hasActiveDestination && _isMoving)
            {
                _pathUpdateTimer += Time.deltaTime;
                if (_pathUpdateTimer >= pathUpdateInterval)
                {
                    _pathUpdateTimer = 0f;
                    UpdatePathIfNeeded();
                }
            }
        }
        #endregion

        #region Public API - Movement Commands
        /// <summary>
        /// Mueve el NPC a una posición específica.
        /// INMEDIATO, sin delays ni coroutines.
        /// </summary>
        public bool MoveTo(Vector3 destination, MovementMode mode = MovementMode.Walk, float? customSpeed = null)
        {
            if (!_isInitialized)
            {
                LogError("MoveTo llamado antes de que el controller esté inicializado");
                return false;
            }
            
            if (!_agent.isOnNavMesh)
            {
                LogError("Agent no está en NavMesh");
                OnMovementBlocked?.Invoke("Agent not on NavMesh");
                return false;
            }
            
            // Validar que el destino está en NavMesh
            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                LogError($"Destino {destination} no está en NavMesh");
                OnMovementBlocked?.Invoke("Destination not on NavMesh");
                return false;
            }
            
            Vector3 validDestination = hit.position;
            
            // Verificar que hay un path válido
            NavMeshPath path = new NavMeshPath();
            if (!_agent.CalculatePath(validDestination, path))
            {
                LogError($"No se puede calcular path a {validDestination}");
                OnMovementBlocked?.Invoke("Cannot calculate path");
                return false;
            }
            
            if (path.status == NavMeshPathStatus.PathInvalid)
            {
                LogError($"Path inválido a {validDestination}");
                OnMovementBlocked?.Invoke("Invalid path");
                return false;
            }
            
            // TODO OK - Configurar movimiento
            _currentDestination = validDestination;
            _currentMode = mode;
            _currentSpeed = customSpeed ?? GetSpeedForMode(mode);
            _hasActiveDestination = true;
            
            _agent.isStopped = false;
            _agent.speed = _currentSpeed;
            _agent.SetDestination(validDestination);
            
            _isMoving = true;
            _pathUpdateTimer = 0f;
            
            // Actualizar animación
            if (_animator != null)
            {
                float normalizedSpeed = _currentSpeed / defaultRunSpeed;
                _animator.SetMovementSpeed(normalizedSpeed);
            }
            
            OnMovementStarted?.Invoke();
            
            if (debugMode)
                Debug.Log($"[MovementController:{name}] ➡️ Moviendo a {validDestination} (modo: {mode}, speed: {_currentSpeed:F1})");
            
            return true;
        }
        
        /// <summary>
        /// Detiene el movimiento INMEDIATAMENTE.
        /// </summary>
        public void Stop()
        {
            if (!_isInitialized) return;
            
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
            
            _isMoving = false;
            _hasActiveDestination = false;
            
            // Actualizar animación
            if (_animator != null)
            {
                _animator.SetMovementSpeed(0f);
            }
            
            OnMovementStopped?.Invoke();
            
            if (debugMode)
                Debug.Log($"[MovementController:{name}] ⏹️ Movimiento detenido");
        }
        
        /// <summary>
        /// Pausa el movimiento (puede reanudarse).
        /// </summary>
        public void Pause()
        {
            if (!_isInitialized || !_hasActiveDestination) return;
            
            _agent.isStopped = true;
            _isMoving = false;
            
            if (_animator != null)
            {
                _animator.SetMovementSpeed(0f);
            }
            
            if (debugMode)
                Debug.Log($"[MovementController:{name}] ⏸️ Movimiento pausado");
        }
        
        /// <summary>
        /// Reanuda el movimiento pausado.
        /// </summary>
        public void Resume()
        {
            if (!_isInitialized || !_hasActiveDestination) return;
            
            _agent.isStopped = false;
            _isMoving = true;
            
            if (_animator != null)
            {
                float normalizedSpeed = _currentSpeed / defaultRunSpeed;
                _animator.SetMovementSpeed(normalizedSpeed);
            }
            
            if (debugMode)
                Debug.Log($"[MovementController:{name}] ▶️ Movimiento reanudado");
        }
        
        /// <summary>
        /// Cambia la velocidad durante el movimiento.
        /// </summary>
        public void SetSpeed(float speed)
        {
            if (!_isInitialized) return;
            
            _currentSpeed = speed;
            _agent.speed = speed;
            
            if (_animator != null && _isMoving)
            {
                float normalizedSpeed = speed / defaultRunSpeed;
                _animator.SetMovementSpeed(normalizedSpeed);
            }
        }
        
        /// <summary>
        /// Cambia el modo de movimiento (caminar/correr).
        /// </summary>
        public void SetMode(MovementMode mode)
        {
            _currentMode = mode;
            SetSpeed(GetSpeedForMode(mode));
        }
        
        /// <summary>
        /// Teletransporta el NPC a una posición (sin animación de movimiento).
        /// </summary>
        public bool Warp(Vector3 position)
        {
            if (!_isInitialized)
            {
                LogError("Warp llamado antes de inicializar");
                return false;
            }
            
            // Validar posición en NavMesh
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                LogError($"Warp: Posición {position} no está en NavMesh");
                return false;
            }
            
            // Detener movimiento actual
            Stop();
            
            // Warp
            if (_agent.isOnNavMesh)
            {
                _agent.Warp(hit.position);
            }
            else
            {
                transform.position = hit.position;
            }
            
            if (debugMode)
                Debug.Log($"[MovementController:{name}] ⚡ Warped a {hit.position}");
            
            return true;
        }
        #endregion

        #region Internal Update Logic
        private void UpdateMovementState()
        {
            if (!_hasActiveDestination) return;
            
            // Verificar si llegó al destino
            if (HasReachedDestination)
            {
                _isMoving = false;
                _hasActiveDestination = false;
                
                // Detener agent
                _agent.isStopped = true;
                
                // Actualizar animación
                if (_animator != null)
                {
                    _animator.SetMovementSpeed(0f);
                }
                
                if (debugMode)
                    Debug.Log($"[MovementController:{name}] ✅ Destino alcanzado");
                
                OnDestinationReached?.Invoke();
            }
        }
        
        private void UpdatePathIfNeeded()
        {
            if (!_hasActiveDestination || !_isMoving) return;
            
            // Si el path es inválido, intentar recalcular
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                LogWarning("Path inválido detectado, recalculando...");
                _agent.SetDestination(_currentDestination);
            }
        }
        
        private float GetSpeedForMode(MovementMode mode)
        {
            switch (mode)
            {
                case MovementMode.Walk:
                    return defaultWalkSpeed;
                case MovementMode.Run:
                    return defaultRunSpeed;
                case MovementMode.Custom:
                    return _currentSpeed;
                default:
                    return defaultWalkSpeed;
            }
        }
        #endregion

        #region Debug
        private void Log(string message)
        {
            if (debugMode)
                Debug.Log($"[MovementController:{name}] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (debugMode)
                Debug.LogWarning($"[MovementController:{name}] ⚠️ {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[MovementController:{name}] ❌ {message}");
        }
        
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !_hasActiveDestination) return;
            
            // Dibujar destino
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_currentDestination, 0.5f);
            
            // Dibujar línea al destino
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentDestination);
            
            // Dibujar path si existe
            if (_agent != null && _agent.hasPath)
            {
                Gizmos.color = Color.cyan;
                Vector3[] corners = _agent.path.corners;
                for (int i = 0; i < corners.Length - 1; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[i + 1]);
                }
            }
        }
        #endregion
    }
    
    /// <summary>
    /// Modo de movimiento del NPC
    /// </summary>
    public enum MovementMode
    {
        Walk,
        Run,
        Custom
    }
}
