using System;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// Gestor de comportamiento de NPC basado en FSM (Finite State Machine).
    /// Versión 2 - Arquitectura modular y profesional.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    [DisallowMultipleComponent]
    public class NPCBehaviourManagerV2 : MonoBehaviour
    {
        [Header("FSM Configuration")]
        [SerializeField] private NPCConfiguration configuration = new NPCConfiguration();
        [SerializeField] private bool debugMode = false;
        
        [Header("Initial State")]
        [SerializeField] private bool startInIdleState = true;
        
        [Header("Physics")]
        [SerializeField] private bool forceKinematicRigidbody = true;
        
        [Header("Save System")]
        [Tooltip("Si está activado, el sistema de guardado recordará la última posición del NPC")]
        [SerializeField] public bool persistLastPosition = false;
        
        /// <summary>
        /// Última posición guardada del NPC. Usado por el sistema de guardado.
        /// Se actualiza automáticamente cuando el NPC se mueve.
        /// </summary>
        [NonSerialized] public Vector3 lastPosition;
        
        // Core components
        private NavMeshAgent _agent;
        private NPCSimpleAnimator _animator;
        private Animator _unityAnimator;
        private Rigidbody _rigidbody;
        
        // FSM components
        private NPCBrain _brain;
        private NPCStateContext _context;
        
        // Player references
        private Transform _player;
        private Transform _playerCamera;
        
        // Public API
        public NPCBrain Brain => _brain;
        public NPCStateContext Context => _context;
        public NPCConfiguration Configuration => configuration;
        public bool IsInCinematic => _context != null && _context.IsInCinematic;
        
        // Component accessors
        public NavMeshAgent Agent => _agent;
        public Animator Animator => _unityAnimator;
        public Transform Player => _player;
        
        void Awake()
        {
            // Validar configuración
            if (!configuration.Validate(out string errors))
            {
                Debug.LogError($"[NPCBehaviourV2:{name}] Configuración inválida:\n{errors}");
            }
            
            // Get components
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<NPCSimpleAnimator>();
            _unityAnimator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            
            // Setup physics
            if (forceKinematicRigidbody && _rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.constraints = configuration.rigidbodyConstraints;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            
            // Create FSM context
            _context = new NPCStateContext(_brain, transform, _agent, _animator, _unityAnimator, _rigidbody)
            {
                Config = configuration,
                DebugMode = debugMode
            };
            
            // Create brain
            _brain = new NPCBrain(_context);
            _context.Brain = _brain; // Set circular reference after creation
            
            // Registrar en NPCRegistry si tiene configuración narrativa
            if (configuration.HasBehaviour(NPCBehaviourType.Narrative) && configuration.narrativeConfig != null)
            {
                NPCRegistry.Instance.RegisterNPC(
                    configuration.narrativeConfig.narrativeID,
                    configuration.narrativeConfig.narrativeTag,
                    this
                );
            }
            
            // Subscribe to player events
            PlayerService.OnPlayerRegistered += OnPlayerRegistered;
            PlayerService.OnPlayerUnregistered += OnPlayerUnregistered;
            
            ResolvePlayerReferences();
            
            if (debugMode)
                Debug.Log($"[NPCBehaviourV2:{name}] Initialized");
        }
        
        void Start()
        {
            // Set initial state
            if (startInIdleState)
            {
                _brain.ChangeState(new States.IdleState());
            }
        }
        
        void OnEnable()
        {
            // Resume FSM if it was active
            if (_brain != null && _brain.CurrentState == null && startInIdleState)
            {
                _brain.ChangeState(new States.IdleState());
            }
        }
        
        void OnDisable()
        {
            // Stop movement
            if (_animator != null)
                _animator.ResetMovement();
                
            if (_agent != null)
                NavMeshAgentUtility.HardStop(_agent);
        }
        
        void Update()
        {
            // Update FSM
            _brain?.Update();
            
            // Update last position for save system
            if (persistLastPosition)
            {
                lastPosition = transform.position;
            }
        }
        
        void OnDestroy()
        {
            // Des-registrar del NPCRegistry
            if (configuration.HasBehaviour(NPCBehaviourType.Narrative) && configuration.narrativeConfig != null)
            {
                NPCRegistry.Instance.UnregisterNPC(
                    configuration.narrativeConfig.narrativeID,
                    configuration.narrativeConfig.narrativeTag
                );
            }
            
            PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
            PlayerService.OnPlayerUnregistered -= OnPlayerUnregistered;
        }
        
        #region Player References
        
        private void OnPlayerRegistered(GameObject player)
        {
            ResolvePlayerReferences();
        }
        
        private void OnPlayerUnregistered()
        {
            if (_context != null)
            {
                _context.Player = null;
                _context.PlayerCamera = null;
            }
            _player = null;
            _playerCamera = null;
        }
        
        private void ResolvePlayerReferences()
        {
            if (!ServiceLocator.TryGet(out PlayerService ps))
                return;
            
            var player = PlayerService.Player;
            if (player != null)
            {
                _player = player.transform;
                
                // Buscar cámara del player
                _playerCamera = Camera.main?.transform;
                
                if (_context != null)
                {
                    _context.Player = _player;
                    _context.PlayerCamera = _playerCamera;
                }
            }
        }
        
        #endregion
        
        #region Public API for Narrative/Cinematic
        
        /// <summary>
        /// Inicia una secuencia cinemática
        /// </summary>
        public void StartCinematicSequence(States.CinematicSequence sequence)
        {
            if (sequence == null)
            {
                Debug.LogError($"[NPCBehaviourV2:{name}] Sequence is null");
                return;
            }
            
            var cinematicState = new States.CinematicState();
            cinematicState.StartSequence(sequence);
            _brain.ForceState(cinematicState);
            
            if (debugMode)
                Debug.Log($"[NPCBehaviourV2:{name}] Cinematic sequence started");
        }
        
        /// <summary>
        /// Sale del estado cinemático y vuelve a Idle
        /// </summary>
        public void ExitCinematic()
        {
            if (_brain.CurrentState is States.CinematicState)
            {
                _brain.ChangeState(new States.IdleState());
                
                if (debugMode)
                    Debug.Log($"[NPCBehaviourV2:{name}] Exited cinematic");
            }
        }
        
        /// <summary>
        /// Fuerza un cambio a estado Idle
        /// </summary>
        public void ForceIdle()
        {
            _brain.ForceState(new States.IdleState());
        }
        
        /// <summary>
        /// Activa el modo combate
        /// </summary>
        public void EnterCombat()
        {
            _context.IsInCombat = true;
            if (!(_brain.CurrentState is States.CombatState))
            {
                _brain.ChangeState(new States.CombatState());
            }
        }
        
        /// <summary>
        /// Desactiva el modo combate
        /// </summary>
        public void ExitCombat()
        {
            _context.IsInCombat = false;
            // El estado de combate detectará el flag y hará transición automáticamente
        }
        
        /// <summary>
        /// Aplica la última posición guardada al NPC (para sistema de guardado)
        /// </summary>
        public void ApplyLastPositionIfNeeded()
        {
            if (!persistLastPosition || lastPosition == Vector3.zero)
                return;
                
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.Warp(lastPosition);
                Debug.Log($"[NPCBehaviourV2:{name}] Posición restaurada: {lastPosition}");
            }
            else
            {
                transform.position = lastPosition;
                Debug.Log($"[NPCBehaviourV2:{name}] Posición restaurada (sin NavMesh): {lastPosition}");
            }
        }
        
        /// <summary>
        /// Mueve el NPC a una posición específica (crea una secuencia simple)
        /// </summary>
        public void MoveToPosition(Vector3 targetPosition, float maxDuration = 15f, bool turnAroundOnArrival = false, Action onComplete = null)
        {
            var sequence = new States.MoveToPoscionSequence(targetPosition, maxDuration, turnAroundOnArrival);
            
            // Si se proporciona callback, monitorearlo
            if (onComplete != null)
            {
                StartCoroutine(WaitForSequenceComplete(sequence, onComplete));
            }
            
            StartCinematicSequence(sequence);
        }
        
        private System.Collections.IEnumerator WaitForSequenceComplete(States.CinematicSequence sequence, Action onComplete)
        {
            while (!sequence.IsCompleted)
            {
                yield return null;
            }
            
            onComplete?.Invoke();
        }
        
        #endregion
        
        #region Gizmos
        
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || _context == null)
                return;
            
            // Dibujar destino actual
            if (_context.TargetDestination != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_context.TargetDestination, 0.5f);
                Gizmos.DrawLine(transform.position, _context.TargetDestination);
            }
            
            // Dibujar estado actual
            if (_brain != null && _brain.CurrentState != null)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"State: {_brain.CurrentState.StateName}",
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.white },
                        fontSize = 12,
                        fontStyle = FontStyle.Bold
                    }
                );
            }
        }
#endif
        
        #endregion
    }
}

