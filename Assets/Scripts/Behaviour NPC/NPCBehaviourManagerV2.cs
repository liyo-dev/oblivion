﻿﻿﻿﻿using System;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.States;
using Game.NPC.Modules; // Necesario para CombatLifecycleHandler

namespace Game.NPC
{
    /// <summary>
    /// Gestor de comportamiento de NPC basado en FSM (Finite State Machine).
    /// Versión 2.1 - Integración completa con Sistema de Combate y Persistencia.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    [DisallowMultipleComponent]
    public class NPCBehaviourManagerV2 : MonoBehaviour
    {
        #region ⚙️ Configuration
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
        
        // Runtime Data
        [NonSerialized] public Vector3 lastPosition;
        #endregion

        #region 🔌 Core Components
        private NavMeshAgent _agent;
        private NPCSimpleAnimator _animator;
        private Animator _unityAnimator;
        private Rigidbody _rigidbody;
        private NPCBrain _brain;
        private NPCStateContext _context;
        
        // Player References
        private Transform _player;
        private Transform _playerCamera;
        #endregion

        #region 📢 Public API
        public NPCBrain Brain => _brain;
        public NPCStateContext Context => _context;
        public NPCConfiguration Configuration => configuration;
        public bool IsInCinematic => _context != null && _context.IsInCinematic;
        
        // Accessors
        public NavMeshAgent Agent => _agent;
        public Animator Animator => _unityAnimator;
        public NPCSimpleAnimator SimpleAnimator => _animator;
        public Transform Player => _player;
        #endregion

        void Awake()
        {
            // 1. Validación de Configuración
            if (!configuration.Validate(out string errors))
            {
                Debug.LogError($"[NPCBehaviourV2:{name}] ❌ ERROR DE CONFIG:\n{errors}");
            }
            
            // 2. Obtener Componentes Core
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<NPCSimpleAnimator>();
            _unityAnimator = GetComponent<Animator>();
            _rigidbody = GetComponent<Rigidbody>();
            
            // ✅ FIX: Configurar NavMeshAgent para que NO controle la rotación
            // NPCSimpleAnimator se encarga de la rotación para evitar conflictos
            if (_agent != null)
            {
                _agent.updateRotation = false;
            }
            
            // 3. Configurar Físicas
            if (forceKinematicRigidbody && _rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.linearVelocity = Vector3.zero; // Unity 6 (antes velocity)
                _rigidbody.angularVelocity = Vector3.zero;
            }
            
            // 4. Crear Contexto FSM
            _context = new NPCStateContext(null, transform, _agent, _animator, _unityAnimator, _rigidbody)
            {
                Config = configuration,
                DebugMode = debugMode
            };
            
            // 5. Crear Brain
            _brain = new NPCBrain(_context);
            _context.Brain = _brain; // Cerrar referencia circular
            
            // 6. INYECCIÓN DE DEPENDENCIAS AUTOMÁTICA
            EnsureRequiredComponents();
            
            // 7. Registro Narrativo
            RegisterNarrativeIdentity();
            
            // 8. Eventos Globales
            PlayerService.OnPlayerRegistered += OnPlayerRegistered;
            PlayerService.OnPlayerUnregistered += OnPlayerUnregistered;
            ResolvePlayerReferences();
        }
        
        void Start()
        {
            // Aplicar persistencia si existe
            if (persistLastPosition && lastPosition != Vector3.zero)
            {
                ApplyLastPositionIfNeeded();
            }

            if (startInIdleState)
            {
                _brain.ChangeState(new States.IdleState());
            }
        }
        
        void Update()
        {
            _brain?.Update();
            
            // Actualizar posición para guardado (solo si es necesario para evitar overhead)
            if (persistLastPosition && Time.frameCount % 60 == 0) // Optimización: cada 60 frames
            {
                lastPosition = transform.position;
            }
        }

        void OnDestroy()
        {
            UnregisterNarrativeIdentity();
            PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
            PlayerService.OnPlayerUnregistered -= OnPlayerUnregistered;
        }

        // =================================================================================
        // 🧩 AUTO-COMPONENT MANAGEMENT (MEJORADO)
        // =================================================================================
        
        private void EnsureRequiredComponents()
        {
            if (debugMode) Debug.Log($"[NPCManager] 🔧 Verificando módulos...");
            
            // 1. QUEST MODULE
            if (configuration.HasBehaviour(NPCBehaviourType.Quest) && configuration.questConfig != null)
            {
                if (!GetComponent<NPCQuestActionExecutor>()) 
                    gameObject.AddComponent<NPCQuestActionExecutor>();
                
                // Añadir gestor de iconos de quest
                if (!GetComponent<NPCQuestIconManager>())
                {
                    gameObject.AddComponent<NPCQuestIconManager>();
                    if (debugMode) Debug.Log($"[NPCManager] 🎯 NPCQuestIconManager añadido para {name}");
                }
            }
            
            // 2. INTERACTIVE NARRATIVE
            if (configuration.HasBehaviour(NPCBehaviourType.InteractiveNarrative) && configuration.interactiveNarrativeConfig != null)
            {
                if (!GetComponent<NPCInteractiveNarrativeExecutor>()) 
                    gameObject.AddComponent<NPCInteractiveNarrativeExecutor>();
            }

            // 3. COMBAT MODULE - 🔥 MEJORA CRÍTICA 🔥
            // Inicializamos los componentes "físicos" (Salud, Targetable) AHORA.
            // La "IA" (CombatBrain) se añade luego en CombatState.
            if (configuration.HasBehaviour(NPCBehaviourType.Combat) && configuration.combatConfig != null)
            {
                // A. Damageable (Vida) - Para que pueda morir por emboscada
                if (!GetComponent<Damageable>())
                {
                    var dmg = gameObject.AddComponent<Damageable>();
                    dmg.SetMaxAndCurrent(configuration.combatConfig.health, configuration.combatConfig.health);
                    
                    // ✅ CRÍTICO: Establecer destroyOnDeath=false INMEDIATAMENTE
                    // El LifecycleHandler controlará la muerte manualmente
                    dmg.SetDestroyOnDeath(false);
                    
                    if (debugMode) Debug.Log($"[NPCManager] 🛡️ Damageable añadido (Pre-Combate) - destroyOnDeath=false");
                }

                // B. CombatLifecycleHandler (Gestión de muerte/stun)
                if (!GetComponent<NPCCombatLifecycleHandler>())
                {
                    gameObject.AddComponent<NPCCombatLifecycleHandler>();
                    if (debugMode) Debug.Log($"[NPCManager] ☠️ NPCCombatLifecycleHandler añadido (Pre-Combate) para {name}");
                }
                else
                {
                    if (debugMode) Debug.Log($"[NPCManager] ℹ️ NPCCombatLifecycleHandler ya existe en {name}");
                }

                // C. Targetable (Para que el jugador pueda apuntarle antes de pelear)
                if (!GetComponent<Targetable>())
                {
                    gameObject.AddComponent<Targetable>();
                    if (debugMode) Debug.Log($"[NPCManager] 🎯 Targetable añadido (Pre-Combate)");
                }
                
                // D. NPCHealthBarSpawner (Barra de vida)
                if (!GetComponent<NPCHealthBarSpawner>())
                {
                    var spawner = gameObject.AddComponent<NPCHealthBarSpawner>();
                    if (configuration.combatConfig.healthBarPrefab != null)
                        spawner.SetHealthBarPrefab(configuration.combatConfig.healthBarPrefab);
                }
            }
        }

        // =================================================================================
        // 🎮 STATE CONTROL API
        // =================================================================================

        public void EnterCombat()
        {
            // Evitar entrar en combate si ya estamos muertos
            var lifecycle = GetComponent<NPCCombatLifecycleHandler>();
            if (lifecycle != null && lifecycle.IsDefeatedAndInactive) return;

            _context.IsInCombat = true;
            if (!(_brain.CurrentState is States.CombatState))
            {
                _brain.ChangeState(new States.CombatState());
            }
        }
        
        public void ExitCombat()
        {
            _context.IsInCombat = false;
            // El CombatState detectará el flag en su Update y saldrá solo
        }

        public void ForceIdle()
        {
            _brain.ForceState(new States.IdleState());
        }

        // =================================================================================
        // 🎬 CINEMATICS & MOVEMENT API
        // =================================================================================

        public void MoveToPosition(Vector3 targetPosition, float walkDuration = 2f, float maxDuration = 15f, bool turn = false, Action onComplete = null)
        {
            var sequence = new States.MoveToPositionSequence(this, targetPosition, maxDuration, turn, walkDuration);
            if (onComplete != null) StartCoroutine(WaitForSequence(sequence, onComplete));
            StartCinematicSequence(sequence);
        }

        public void StartCinematicSequence(States.CinematicSequence sequence)
        {
            if (sequence == null) return;
            var state = new States.CinematicState();
            state.StartSequence(sequence);
            _brain.ForceState(state);
        }

        private System.Collections.IEnumerator WaitForSequence(States.CinematicSequence seq, Action callback)
        {
            while (!seq.IsCompleted) yield return null;
            callback?.Invoke();
        }

        // =================================================================================
        // 💾 PERSISTENCE & UTILS
        // =================================================================================

        public void ApplyLastPositionIfNeeded()
        {
            if (!persistLastPosition || lastPosition == Vector3.zero) return;
            
            if (_agent != null && _agent.isOnNavMesh) _agent.Warp(lastPosition);
            else transform.position = lastPosition;
            
            if(debugMode) Debug.Log($"[NPCManager] 📍 Posición restaurada: {lastPosition}");
        }
        
        public void HandleInteraction(GameObject interactor)
        {
            // No interactuar si estamos muertos o en combate
            if (_context.IsInCombat) return;
            var lifecycle = GetComponent<NPCCombatLifecycleHandler>();
            // Si está derrotado, el LifecycleHandler gestiona la interacción especial (diálogo post-derrota)
            // pero si está vivo y bien, el Brain gestiona la interacción normal.
            if (lifecycle != null && lifecycle.IsDefeatedAndInactive)
            {
                // TODO: Implementar HandlePostDefeatInteraction en NPCCombatLifecycleHandler
                // Por ahora, delegar al Brain para que maneje la interacción normalmente
                // lifecycle.HandlePostDefeatInteraction(interactor);
                _brain?.HandleInteraction(interactor);
                return;
            }

            _brain?.HandleInteraction(interactor);
        }

        private void RegisterNarrativeIdentity()
        {
            if (configuration.HasBehaviour(NPCBehaviourType.InteractiveNarrative) && configuration.narrativeConfig != null)
            {
                NPCRegistry.Instance.RegisterNPC(
                    configuration.narrativeConfig.narrativeID, 
                    configuration.narrativeConfig.narrativeTag, 
                    this
                );
            }
        }
        
        private void UnregisterNarrativeIdentity()
        {
            if (configuration.HasBehaviour(NPCBehaviourType.InteractiveNarrative) && configuration.narrativeConfig != null)
            {
                NPCRegistry.Instance.UnregisterNPC(
                    configuration.narrativeConfig.narrativeID, 
                    configuration.narrativeConfig.narrativeTag
                );
            }
        }

        #region Player References
        private void OnPlayerRegistered(GameObject player) => ResolvePlayerReferences();
        private void OnPlayerUnregistered() { 
            _player = null; _playerCamera = null; 
            if(_context != null) { _context.Player = null; _context.PlayerCamera = null; }
        }
        private void ResolvePlayerReferences() {
            if (ServiceLocator.TryGet(out PlayerService ps) && PlayerService.Player != null) {
                _player = PlayerService.Player.transform;
                _playerCamera = Camera.main?.transform;
                if (_context != null) { _context.Player = _player; _context.PlayerCamera = _playerCamera; }
            }
        }
        #endregion

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || _context == null) return;
            
            // Visualizar Destino
            if (_context.TargetDestination != Vector3.zero) {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_context.TargetDestination, 0.5f);
                Gizmos.DrawLine(transform.position, _context.TargetDestination);
            }

            // Visualizar rango de combate si existe
            if (configuration.combatConfig != null) {
                Gizmos.color = new Color(1, 0, 0, 0.3f);
                Gizmos.DrawWireSphere(transform.position, configuration.combatConfig.minAttackDistance);
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Gizmos.DrawWireSphere(transform.position, configuration.combatConfig.maxAttackDistance);
            }
        }
#endif
    }
}