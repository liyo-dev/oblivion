using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
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
        [Tooltip("Desactiva el wander para que el NPC se quede estático en su posición inicial.")]
        [SerializeField] private bool disableWander = false;
        
        [Header("Initial State")]
        [SerializeField] private bool startInIdleState = true;
        
        [Header("Physics")]
        [SerializeField] private bool forceKinematicRigidbody = true;
        
        [Header("Save System")]
        [Tooltip("Si está activado, el sistema de guardado recordará la última posición del NPC")]
        [SerializeField] public bool persistLastPosition = false;
        
        // Runtime Data
        [NonSerialized] public Vector3 lastPosition;

        [Header("Identidad NPC")]
        [Tooltip("ID único para persistencia y registro (ej: 'NPC_InteractiveNarrative_Config_Eldran_cd6ce7a3')")]
        [SerializeField] private string persistenceId;

        [Tooltip("ID del personaje para diálogos (ej: 'CHAR_ELDRAN'). Se usa para identificar al speaker en diálogos con múltiples participantes.")]
        [SerializeField] private string dialogueCharacterId;

        [Header("Interacción")]
        [Tooltip("¿El NPC gira hacia el jugador al interactuar?")]
        [SerializeField] private bool rotateToPlayerOnInteract = true;

        [Min(0f)]
        [Tooltip("Duración de la rotación hacia el jugador")]
        [SerializeField] private float rotationDuration = 0.3f;

        [Header("Layer Management")]
        [Tooltip("Capa inicial del NPC. Cambiará a 'Enemy' al iniciar combate si switchToEnemyLayerOnCombat está activo.")]
        [SerializeField] private Modules.LayerMode initialLayer = Modules.LayerMode.Interactable;

        [Tooltip("¿Cambiar automáticamente a la capa 'Enemy' cuando se inicie un combate (acción StartCombat)?")]
        [SerializeField] private bool switchToEnemyLayerOnCombat = true;

        [Header("Detección Narrativa")]
        [Tooltip("Rango de detección del jugador para narrativas con autoStartOnDetection=true")]
        [Min(1f)]
        [SerializeField] private float narrativeDetectionRange = 10f;

        [Tooltip("¿El NPC camina hacia el jugador durante la alerta narrativa?")]
        [SerializeField] private bool walkTowardsPlayerOnAlert = true;

        [Tooltip("Distancia mínima para detenerse al acercarse al jugador")]
        [Min(0.5f)]
        [SerializeField] private float stopDistanceFromPlayer = 2f;

        [Tooltip("Icono que aparece sobre el NPC al detectar al jugador (para narrativas sin combate). " +
                 "Se usa cuando el chain entry tiene 'Show Alert Icon' activo pero no tiene su propio prefab asignado.")]
        [SerializeField] private GameObject narrativeAlertIconPrefab;
        #endregion

        #region 🔌 Core Components
        private NavMeshAgent _agent;
        private NPCSimpleAnimator _animator;
        private Animator _unityAnimator;
        private Rigidbody _rigidbody;
        private NPCBrain _brain;
        private NPCStateContext _context;
        private int _externalMovementOverrideRequests;
        
        // Player References
        private Transform _player;
        private Transform _playerCamera;

        // Cached components (evita GetComponent repetido)
        private NPCPartyMember _cachedPartyMember;
        private NPCCombatLifecycleHandler _cachedLifecycle;
        #endregion

        #region 📢 Public API
        public NPCBrain Brain => _brain;
        public NPCStateContext Context => _context;
        public NPCConfiguration Configuration => configuration;
        public bool IsInCinematic => _context != null && _context.IsInCinematic;
        public bool IsExternalMovementOverrideActive => _externalMovementOverrideRequests > 0;
        
        // Accessors
        public NavMeshAgent Agent => _agent;
        public Animator Animator => _unityAnimator;
        public NPCSimpleAnimator SimpleAnimator => _animator;
        public Transform Player => _player;
        
        /// <summary>
        /// Indica si este NPC es un aliado del jugador (miembro del party).
        /// </summary>
        public bool IsAlly => _cachedPartyMember != null && _cachedPartyMember.IsInParty;

        // Identidad NPC (migrado desde NPCInteractiveNarrativeConfig)
        public string PersistenceId => persistenceId;
        public string DialogueCharacterId => dialogueCharacterId;

        // Interacción
        public bool RotateToPlayerOnInteract => rotateToPlayerOnInteract;
        public float RotationDuration => rotationDuration;

        // Layer Management
        public Modules.LayerMode InitialLayer => initialLayer;
        public bool SwitchToEnemyLayerOnCombat => switchToEnemyLayerOnCombat;

        // Detección Narrativa
        public float NarrativeDetectionRange => narrativeDetectionRange;
        public bool WalkTowardsPlayerOnAlert => walkTowardsPlayerOnAlert;
        public float StopDistanceFromPlayer => stopDistanceFromPlayer;
        public GameObject NarrativeAlertIconPrefab => narrativeAlertIconPrefab;
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
                // IMPORTANTE: Resetear velocidades ANTES de hacer el rigidbody kinematic
                // Unity no permite modificar velocidades de rigidbodies kinematic
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
            }
            
            // 4. Aplicar overrides de configuración
            if (disableWander)
                configuration.DisableWander();

            // Los NPCs narrativos (con persistenceId) no deben desaparecer refugiándose de la
            // lluvia: podrían ser necesarios para una interacción o quest en curso. Ver
            // Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § A.4.
            if (!string.IsNullOrEmpty(persistenceId))
                configuration.DisableShelterSeeking();

            // 5. Crear Contexto FSM
            _context = new NPCStateContext(null, transform, _agent, _animator, _unityAnimator, _rigidbody)
            {
                Config = configuration,
                DebugMode = debugMode,
                RelationshipId = ResolveRelationshipId()
            };
            
            // 6. Crear Brain
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

            // 9. Refugio de lluvia: escuchar cambios de clima y aplicar el estado actual por si
            // ya está lloviendo al activarse este NPC (recarga de escena a mitad de tormenta, etc.)
            NPCWeatherAwareness.RainStarted += HandleRainStarted;
            NPCWeatherAwareness.RainStopped += HandleRainStopped;
            _context.ShouldSeekShelter = NPCWeatherAwareness.IsRaining;
        }

        private void HandleRainStarted()
        {
            if (_context != null) _context.ShouldSeekShelter = true;
        }

        private void HandleRainStopped()
        {
            if (_context == null) return;

            // El propio SeekShelterState (todavía activo y recibiendo Update normalmente, el NPC
            // nunca se desactiva) detecta este flag en su CheckTransitions y hace que el NPC
            // camine de vuelta a ShelterOriginPosition vía ReturnFromShelterState.
            _context.ShouldSeekShelter = false;
        }
        
        void OnEnable()
        {
            // _context ya existe siempre aquí: Unity llama Awake() antes que OnEnable(),
            // incluida la primera activación. Ver NPCAmbientRegistry para el uso (radar de amistad).
            if (_context != null)
                NPCAmbientRegistry.Register(_context.RelationshipId, this);
        }

        void OnDisable()
        {
            if (_context != null)
                NPCAmbientRegistry.Unregister(_context.RelationshipId, this);
        }

        void Start()
        {
            // Aplicar persistencia si existe
            if (persistLastPosition && lastPosition != Vector3.zero)
            {
                ApplyLastPositionIfNeeded();
            }

            // Solo inicializar a Idle si ningún sistema externo ya asignó un estado
            // (ej: ActiveCharacterSwapper puede poner al Will NPC en AllyCombatState antes de que Start() corra)
            if (startInIdleState && _brain.CurrentState == null)
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
        
        void LateUpdate()
        {
            if (IsExternalMovementOverrideActive)
                return;

            // ✅ SAFETY CHECK: Si está en IdleState pero el agente no está detenido, forzar detención
            // Esto captura casos donde algo externo está activando el agente
            // FIX M12 (auditoría 2026-08-07, parcial): comparación por tipo en vez de por string
            // (evita comparar StateName == "Idle" cada NPC/frame). No se ha unificado con la
            // vigilancia equivalente de IdleState.OnUpdate — ambas llevan el comentario "FIX
            // CRÍTICO" propio, señal de que cada una se añadió para tapar un caso real distinto;
            // fusionarlas sin poder reproducir esos casos en el editor es más riesgo del que
            // conviene asumir ahora mismo.
            if (_brain != null && _brain.CurrentState is IdleState &&
                _agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                if (!_agent.isStopped || _agent.velocity.sqrMagnitude > 0.01f)
                {
                    if (debugMode)
                        Debug.LogWarning($"[NPCManager:{name}] ⚠️ LateUpdate Safety: Agent no detenido en IdleState (isStopped={_agent.isStopped}, vel={_agent.velocity.magnitude:F1})");
                    
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                    if (_agent.hasPath)
                        _agent.ResetPath();
                }
            }
        }

        void OnDestroy()
        {
            // Limpiar registro de combate
            ActiveCombatRegistry.UnregisterNPC(gameObject);
            
            UnregisterNarrativeIdentity();
            PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
            PlayerService.OnPlayerUnregistered -= OnPlayerUnregistered;
            NPCWeatherAwareness.RainStarted -= HandleRainStarted;
            NPCWeatherAwareness.RainStopped -= HandleRainStopped;
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
                    //if (debugMode) Debug.Log($"[NPCManager] ☠️ NPCCombatLifecycleHandler añadido (Pre-Combate) para {name}");
                }
                else
                {
                    //if (debugMode) Debug.Log($"[NPCManager] ℹ️ NPCCombatLifecycleHandler ya existe en {name}");
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
            
            // 4. COMPANION MODULE (Party System)
            // Permite que el NPC se una al equipo del jugador
            if (configuration.HasBehaviour(NPCBehaviourType.Companion) && configuration.partyConfig != null)
            {
                if (!GetComponent<NPCPartyMember>())
                {
                    var partyMember = gameObject.AddComponent<NPCPartyMember>();
                    partyMember.SetConfig(configuration.partyConfig);
                    if (debugMode) Debug.Log($"[NPCManager] 🤝 NPCPartyMember añadido para {name}");
                }

                // Prompt "Sígueme": se muestra cuando el jugador se acerca y el equipo está disuelto
                if (!GetComponent<CompanionFollowPrompt>() &&
                    configuration.partyConfig.followPromptIconPrefab != null)
                {
                    var prompt = gameObject.AddComponent<CompanionFollowPrompt>();
                    prompt.SetFollowIcon(configuration.partyConfig.followPromptIconPrefab);
                    prompt.SetStopFollowIcon(configuration.partyConfig.stopFollowIconPrefab);
                }
            }

            // Cachear componentes frecuentemente usados
            _cachedPartyMember = GetComponent<NPCPartyMember>();
            _cachedLifecycle = GetComponent<NPCCombatLifecycleHandler>();
        }

        // =================================================================================
        // 🎮 STATE CONTROL API
        // =================================================================================

        public void EnterCombat()
        {
            // Evitar entrar en combate si ya estamos muertos
            if (_cachedLifecycle != null && _cachedLifecycle.IsDefeatedAndInactive) return;

            _context.IsInCombat = true;

            // ✅ Verificar si es un aliado (miembro del party)
            bool isAlly = _cachedPartyMember != null && _cachedPartyMember.IsInParty;
            
            if (isAlly)
            {
                // 🤝 ALIADO: Usar AllyCombatState (ataca enemigos, no al jugador)
                // NO registrar en ActiveCombatRegistry (eso es para enemigos)
                if (!(_brain.CurrentState is States.AllyCombatState))
                {
                    if (debugMode) Debug.Log($"[NPCManager] 🤝 {name} entrando en AllyCombatState (es aliado)");
                    _brain.ChangeState(new States.AllyCombatState());
                }
                return; // No continuar con lógica de enemigos
            }
            
            // ⚔️ ENEMIGO: Lógica normal
            // Registrar NPC en el registro de combate activo
            ActiveCombatRegistry.RegisterNPC(gameObject);
            
            if (!(_brain.CurrentState is States.CombatState))
            {
                _brain.ChangeState(new States.CombatState());
            }
            
            // Si es líder de un equipo, hacer que los compañeros también entren en combate
            var combatTeam = GetComponent<NPCCombatTeam>();
            if (combatTeam != null)
            {
                // Obtener el jugador como objetivo
                Transform target = _context.Player;
                if (target == null && PlayerService.TryGetPlayer(out var player))
                {
                    target = player.transform;
                }
                
                if (target != null)
                {
                    // ForceTeamCombat hará que todos los miembros entren en combate
                    combatTeam.ForceTeamCombat(target);
                }
            }
        }
        
        public void ExitCombat()
        {
            _context.IsInCombat = false;
            
            // ✅ Desregistrar NPC del registro de combate activo
            ActiveCombatRegistry.UnregisterNPC(gameObject);
            
            // El CombatState detectará el flag en su Update y saldrá solo
        }
        
        /// <summary>
        /// Fuerza al NPC a entrar en combate inmediatamente contra un objetivo específico.
        /// Usado por el sistema de equipos de combate (NPCCombatTeam).
        /// </summary>
        public void ForceEnterCombat(Transform target)
        {
            // Evitar entrar en combate si ya estamos muertos
            if (_cachedLifecycle != null && _cachedLifecycle.IsDefeatedAndInactive) return;

            // FIX C5 (auditoría 2026-08-07): NPCCombatLifecycleHandler.OnDamaged llama a este
            // método sin comprobar si el NPC está en mitad de una cinemática. Golpear a un NPC
            // durante una cinemática forzaba la salida de CinematicState (vía ForceState del
            // brain, más abajo) a mitad de secuencia, dejando colgados los secuenciadores que
            // encadenan pasos por onComplete (MountainSequencer, ReinoExitBanterSequencer). El
            // combate se resolverá igualmente en cuanto la cinemática termine — el atacante no
            // pierde el registro, solo se evita interrumpir la secuencia en curso.
            if (_context != null && _context.IsInCinematic) return;

            // Asignar el objetivo
            _context.Player = target;
            _context.IsInCombat = true;
            
            // Registrar en el registro de combate activo
            ActiveCombatRegistry.RegisterNPC(gameObject);
            
            // Forzar cambio a estado de combate
            if (!(_brain.CurrentState is States.CombatState))
            {
                if (debugMode) Debug.Log($"[NPCManager] ⚔️ {name} forzado a entrar en combate contra {target.name}");
                _brain.ForceState(new States.CombatState());
            }
        }

        public void ForceIdle()
        {
            _brain.ForceState(new States.IdleState());
        }

        public void PushExternalMovementOverride()
        {
            _externalMovementOverrideRequests++;
        }

        public void PopExternalMovementOverride()
        {
            if (_externalMovementOverrideRequests > 0)
                _externalMovementOverrideRequests--;
        }

        // =================================================================================
        // 🤝 PARTY SYSTEM API
        // =================================================================================
        
        #region Party Events
        
        [Header("Party Events")]
        [Tooltip("Evento que se invoca cuando el NPC se une al party")]
        [SerializeField] private UnityEvent onJoinedParty;
        
        [Tooltip("Evento que se invoca cuando el NPC abandona el party")]
        [SerializeField] private UnityEvent onLeftParty;
        
        /// <summary>
        /// Evento C# que se dispara cuando el NPC se une al party
        /// </summary>
        public event Action OnJoinedParty;
        
        /// <summary>
        /// Evento C# que se dispara cuando el NPC abandona el party
        /// </summary>
        public event Action OnLeftParty;
        
        #endregion
        
        /// <summary>
        /// Une este NPC al equipo del jugador.
        /// Requiere que el NPC tenga el comportamiento Companion configurado.
        /// </summary>
        public bool JoinPlayerParty()
        {
            if (_cachedPartyMember == null)
            {
                if (debugMode) Debug.LogWarning($"[NPCManager] {name} no tiene NPCPartyMember. Asegúrate de configurar Companion behaviour.");
                return false;
            }

            bool success = _cachedPartyMember.JoinParty();
            
            if (success)
            {
                // Disparar eventos
                onJoinedParty?.Invoke();
                OnJoinedParty?.Invoke();
                
                if (debugMode) Debug.Log($"[NPCManager:{name}] ✅ Se unió al party - Eventos disparados");
            }
            
            return success;
        }
        
        /// <summary>
        /// Remueve este NPC del equipo del jugador.
        /// </summary>
        public bool LeavePlayerParty()
        {
            if (_cachedPartyMember == null) return false;

            bool success = _cachedPartyMember.LeaveParty();
            
            if (success)
            {
                // Disparar eventos
                onLeftParty?.Invoke();
                OnLeftParty?.Invoke();
                
                if (debugMode) Debug.Log($"[NPCManager:{name}] 👋 Abandonó el party - Eventos disparados");
            }
            
            return success;
        }
        
        /// <summary>
        /// Comprueba si este NPC está actualmente en el equipo del jugador.
        /// </summary>
        public bool IsInPlayerParty() => _cachedPartyMember != null && _cachedPartyMember.IsInParty;
        
        /// <summary>
        /// Une este NPC al party del jugador.
        /// Versión void para poder usarse desde Unity Events (OnMinigameWon, etc.)
        /// </summary>
        [ContextMenu("Añadir al Party")]
        public void AddToParty()
        {
            bool success = JoinPlayerParty();
            if (success)
            {
                Debug.Log($"[NPCManager:{name}] ✅ AddToParty exitoso");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[NPCManager:{name}] ⚠️ AddToParty falló - ¿Tiene Companion behaviour configurado?");
            }
        }
        
        /// <summary>
        /// Remueve este NPC del party del jugador.
        /// Versión void para poder usarse desde Unity Events.
        /// </summary>
        public void RemoveFromParty()
        {
            bool success = LeavePlayerParty();
            if (!success && debugMode)
            {
                Debug.LogWarning($"[NPCManager:{name}] ⚠️ RemoveFromParty falló");
            }
        }

        // =================================================================================
        // 🗣️ SOCIAL ENCOUNTER API
        // =================================================================================

        /// <summary>
        /// Llamado por otro NPC que quiere iniciar un encuentro social con este.
        /// Devuelve true si se acepta; en ese caso el NPC entra directamente en NPCSocialEncounterState.
        /// </summary>
        public bool TryAcceptSocialEncounter(Transform initiator, NPCRelationType relation)
        {
            if (_context == null) return false;
            if (_context.IsInCombat || _context.IsInCinematic || _context.IsInteracting) return false;
            if (_context.WasDefeatedInCombat) return false;
            if (_brain.CurrentState is States.NPCSocialEncounterState) return false;

            float cooldown = configuration.socialConfig?.socialCooldown ?? 30f;
            if (Time.time - _context.LastSocialEncounterTime < cooldown) return false;

            _context.PendingSocialPartner  = initiator;
            _context.PendingSocialRelation = relation;
            _brain.ChangeState(new States.NPCSocialEncounterState());
            return true;
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

            // FIX INC-070: si el NavMesh se ha vuelto a bakear (o el terreno cambió) desde que se
            // guardó lastPosition, esa altura guardada puede ya no coincidir con la superficie
            // actual. Antes, si _agent.isOnNavMesh daba false en este punto (p.ej. porque la
            // posición de editor del NPC quedó fuera del NavMesh rebakeado), se hacía un
            // "transform.position = lastPosition" en crudo, sin comprobar el NavMesh actual — el
            // NPC quedaba flotando/enterrado ahí para siempre, ya que es cinemático
            // (forceKinematicRigidbody) y sin gravedad que lo corrija. Ahora buscamos primero el
            // punto válido más cercano en el NavMesh vigente antes de colocar al NPC.
            if (NavMesh.SamplePosition(lastPosition, out var hit, 5f, NavMesh.AllAreas))
            {
                if (_agent != null)
                {
                    bool wasEnabled = _agent.enabled;
                    if (!wasEnabled)
                    {
                        // FIX: Unity valida transform.position en el instante de poner
                        // enabled = true (antes de que el Warp de abajo pueda corregirlo). Si el
                        // transform seguía en su posición vieja (fuera del NavMesh rebakeado),
                        // ese "enabled = true" a secas loggeaba "Failed to create agent because
                        // there is no valid NavMesh" aunque el Warp siguiente ya llevara al NPC al
                        // punto correcto. SafeEnable recoloca el transform en hit.position ANTES
                        // de habilitar, evitando el falso error.
                        NavMeshAgentUtility.SafeEnable(_agent, transform, hit.position);
                    }
                    _agent.Warp(hit.position);
                    if (!wasEnabled) _agent.enabled = false;
                }
                else
                {
                    transform.position = hit.position;
                }
            }
            else if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.Warp(lastPosition);
            }
            else
            {
                transform.position = lastPosition;
            }

            if(debugMode) Debug.Log($"[NPCManager] 📍 Posición restaurada: {lastPosition}");
        }
        
        public void HandleInteraction(GameObject interactor)
        {
            // No interactuar si estamos muertos o en combate
            if (_context.IsInCombat) return;
            if (_cachedLifecycle != null && _cachedLifecycle.IsDefeatedAndInactive)
            {
                // TODO: Implementar HandlePostDefeatInteraction en NPCCombatLifecycleHandler
                // Por ahora, delegar al Brain para que maneje la interacción normalmente
                // lifecycle.HandlePostDefeatInteraction(interactor);
                _brain?.HandleInteraction(interactor);
                return;
            }

            _brain?.HandleInteraction(interactor);
        }

        /// <summary>
        /// Id estable para el registro de relaciones dinámicas (NPCRelationshipRegistry).
        /// Los NPCs con historia propia ya tienen un npcId único en su NPCSocialConfig individual.
        /// Los NPCs de relleno comparten un NPCSocialConfig de arquetipo con npcId vacío
        /// (ej. NPC_Social_Archetype_Friendly.asset) — para esos, generamos un id único por
        /// instancia aquí. Nunca se escribe de vuelta al ScriptableObject compartido.
        /// Ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § B.2.
        /// </summary>
        private string ResolveRelationshipId()
        {
            string authoredId = configuration?.socialConfig?.npcId;
            if (!string.IsNullOrEmpty(authoredId)) return authoredId;

            if (!string.IsNullOrEmpty(persistenceId)) return persistenceId;

            return $"{gameObject.name}_{GetEntityId()}";
        }

        private void RegisterNarrativeIdentity()
        {
            string registryId = !string.IsNullOrEmpty(persistenceId) ? persistenceId : null;

            if (registryId == null)
            {
                if (configuration.HasBehaviour(NPCBehaviourType.InteractiveNarrative) ||
                    configuration.HasBehaviour(NPCBehaviourType.Companion) ||
                    GetComponent<NPCPartyMember>() != null)
                {
                    registryId = gameObject.name;
                }
            }

            if (!string.IsNullOrEmpty(registryId))
            {
                NPCRegistry.Instance.RegisterNPC(registryId, null, this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugMode) Debug.Log($"[NPCBehaviourManagerV2] NPC '{registryId}' registrado en NPCRegistry");
#endif
            }
        }
        
        private void UnregisterNarrativeIdentity()
        {
            string registryId = !string.IsNullOrEmpty(persistenceId) ? persistenceId : null;

            if (registryId == null && configuration != null)
            {
                if (configuration.HasBehaviour(NPCBehaviourType.InteractiveNarrative) ||
                    configuration.HasBehaviour(NPCBehaviourType.Companion) ||
                    GetComponent<NPCPartyMember>() != null)
                {
                    registryId = gameObject.name;
                }
            }

            if (!string.IsNullOrEmpty(registryId) && Game.NPC.NPCRegistry.HasInstance)
            {
                NPCRegistry.Instance.UnregisterNPC(registryId, null);
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
                // ✅ OPTIMIZACIÓN: Cachear Camera.main solo si no la tenemos ya
                if (_playerCamera == null)
                {
                    _playerCamera = Camera.main?.transform;
                }
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
