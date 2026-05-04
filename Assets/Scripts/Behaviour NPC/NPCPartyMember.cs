using System;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// Componente que permite a un NPC unirse al equipo (Party) del jugador.
    /// Gestiona la lógica de seguimiento y comportamiento como compañero.
    /// </summary>
    [DisallowMultipleComponent]
    public class NPCPartyMember : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Party Configuration")]
        [SerializeField] private NPCPartyConfig partyConfig;
        
        [Header("Estado Inicial")]
        [Tooltip("Si está activado, este NPC se unirá al equipo automáticamente al iniciar")]
        [SerializeField] private bool autoJoinOnStart = false;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region State
        private PlayerParty _party;
        private NPCBehaviourManagerV2 _npcManager;
        private NavMeshAgent _agent;
        private Interactable _interactable;
        private bool _isInParty;
        private bool _wasInPartyBeforeCombat;
        private INPCState _stateBeforeJoining;
        private bool _isJoining; // Flag para evitar joins simultáneos
        private float _nextIdleCheck;
        #endregion

        #region Events
        /// <summary>
        /// Se dispara cuando este NPC se une al equipo
        /// </summary>
        public event Action OnJoined;
        
        /// <summary>
        /// Se dispara cuando este NPC abandona el equipo
        /// </summary>
        public event Action OnLeft;
        #endregion

        #region Runtime spell overrides
        private MagicSpellSO[] _runtimeSpells; // null = usar PartyConfig

        /// <summary>
        /// Sobreescribe los hechizos en runtime sin modificar el ScriptableObject.
        /// Usado por ActiveCharacterSwapper para sincronizar los hechizos actuales de Will.
        /// </summary>
        public void SetRuntimeSpells(MagicSpellSO left, MagicSpellSO right, MagicSpellSO special)
        {
            _runtimeSpells = new[] { left, right, special };
        }

        /// <summary>
        /// Devuelve el hechizo efectivo: override runtime si existe, si no el del PartyConfig.
        /// </summary>
        public MagicSpellSO GetEffectiveSpell(int index)
        {
            if (_runtimeSpells != null && index >= 0 && index < _runtimeSpells.Length)
                return _runtimeSpells[index];
            return partyConfig?.GetSpell(index);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Configuración de comportamiento en el equipo
        /// </summary>
        public NPCPartyConfig PartyConfig => partyConfig;
        
        /// <summary>
        /// Referencia al manager principal del NPC
        /// </summary>
        public NPCBehaviourManagerV2 NPCManager => _npcManager;
        
        /// <summary>
        /// ¿Está actualmente en el equipo?
        /// </summary>
        public bool IsInParty => _isInParty;
        
        /// <summary>
        /// ¿Está activo en el equipo? (en party y no en combate/cinemática)
        /// </summary>
        public bool IsActiveInParty => _isInParty && 
            _npcManager != null && 
            !_npcManager.Context.IsInCombat && 
            !_npcManager.Context.IsInCinematic;
        
        /// <summary>
        /// Nombre para mostrar en UI
        /// </summary>
        public string DisplayName => partyConfig?.displayName ??
            (_npcManager?.Configuration?.interactiveNarrativeConfig?.persistenceId ?? gameObject.name);
        
        /// <summary>
        /// Índice en el equipo (para formación)
        /// </summary>
        public int PartyIndex
        {
            get
            {
                if (_party == null) return -1;
                var members = _party.Members;
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i] == this) return i;
                }
                return -1;
            }
        }
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            _agent = GetComponent<NavMeshAgent>();
            _interactable = GetComponent<Interactable>() ?? GetComponentInChildren<Interactable>(true);
            
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCPartyMember] {name} requiere NPCBehaviourManagerV2");
            }
        }

        void Start()
        {
            if (autoJoinOnStart)
            {
                // Sistema robusto sin delays: intentar join inmediato
                TryAutoJoin();
            }
        }
        
        /// <summary>
        /// Intenta auto-join de forma inmediata y robusta.
        /// Si no está listo, se suscribe a eventos para intentar después.
        /// CERO delays, CERO "yield return null".
        /// </summary>
        private void TryAutoJoin()
        {
            // Verificación inmediata de estado
            if (!NPCInitializer.IsNPCReady(_npcManager, out string reason))
            {
                if (debugMode)
                    Debug.Log($"[NPCPartyMember:{name}] No listo para auto-join: {reason}. Reintentando en Update.");
                
                // Si no está listo AHORA, lo intentaremos en Update hasta que lo esté
                return;
            }
            
            // ¡Está listo! Unirse inmediatamente
            JoinParty();
        }
        
        void Update()
        {
            // Si está esperando auto-join, verificar en cada frame hasta que esté listo
            if (autoJoinOnStart && !_isInParty && !_isJoining)
            {
                if (NPCInitializer.IsNPCReady(_npcManager, out _))
                {
                    JoinParty();
                }
            }
            
            // Si está en el party pero no está siguiendo (Brain no estaba listo), verificar cada 0.5s
            if (_isInParty && _npcManager != null && _npcManager.Brain != null && Time.time >= _nextIdleCheck)
            {
                _nextIdleCheck = Time.time + 0.5f;
                var currentState = _npcManager.Brain.CurrentState;
                if (currentState != null && currentState.StateName == "Idle"
                    && !_npcManager.Context.IsInCombat && !_npcManager.Context.IsInCinematic
                    && (PartyControlManager.Instance?.IsPartyFollowing ?? true))
                {
                    StartFollowing();
                }
            }
        }

        void OnDestroy()
        {
            if (_isInParty)
            {
                LeaveParty();
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Configura el PartyConfig programáticamente.
        /// Útil cuando el componente se añade en runtime.
        /// </summary>
        public void SetConfig(NPCPartyConfig config)
        {
            partyConfig = config;
        }
        
        /// <summary>
        /// Une este NPC al equipo del jugador.
        /// </summary>
        public bool JoinParty()
        {
            if (_isInParty)
            {
                Log("Ya está en el equipo");
                return false;
            }
            
            // ✅ FIX: Evitar joins simultáneos durante la carga de partida
            if (_isJoining)
            {
                LogWarning("Ya hay un proceso de unión en curso, ignorando llamada duplicada");
                return false;
            }
            
            // ✅ FIX: Verificar que el NPC esté completamente inicializado
            if (_npcManager == null || _agent == null)
            {
                LogWarning("NPC no está completamente inicializado, esperando...");
                return false;
            }
            
            // ✅ FIX: Verificar que el NavMeshAgent esté en un NavMesh
            if (!_agent.isOnNavMesh)
            {
                LogWarning("NavMeshAgent no está en NavMesh, no se puede unir al party ahora");
                return false;
            }
            
            _isJoining = true;
            
            _party = PlayerParty.Instance;
            if (_party == null)
            {
                LogWarning("No se encontró PlayerParty en la escena");
                _isJoining = false;
                return false;
            }
            
            if (_party.IsFull)
            {
                LogWarning("El equipo está lleno");
                _isJoining = false;
                return false;
            }
            
            // Guardar estado actual para restaurarlo si sale del equipo
            if (_npcManager?.Brain != null)
            {
                _stateBeforeJoining = _npcManager.Brain.CurrentState;
            }
            
            bool success = _party.AddMember(this);
            
            if (!success)
            {
                _isJoining = false;
            }
            
            return success;
        }

        /// <summary>
        /// Remueve este NPC del equipo del jugador.
        /// </summary>
        public bool LeaveParty()
        {
            if (!_isInParty || _party == null)
            {
                Log("No está en el equipo");
                return false;
            }
            
            return _party.RemoveMember(this);
        }

        /// <summary>
        /// Cambia al estado de seguir al jugador.
        /// </summary>
        public void StartFollowing()
        {
            if (_npcManager?.Brain == null) return;

            // Solo cambiar a FollowState si no estamos en combate/cinemática
            if (!_npcManager.Context.IsInCombat && !_npcManager.Context.IsInCinematic)
            {
                _npcManager.Brain.ChangeState(new FollowPlayerState(this));
            }
        }

        /// <summary>
        /// Igual que StartFollowing pero omite la verificación de pertenencia al party.
        /// Útil para NPCs temporales como el Will NPC instanciado al cambiar de personaje.
        /// </summary>
        public void StartFollowingIgnorePartyCheck()
        {
            if (_npcManager?.Brain == null) return;
            if (!_npcManager.Context.IsInCombat && !_npcManager.Context.IsInCinematic)
                _npcManager.Brain.ChangeState(new FollowPlayerState(this, skipPartyCheck: true));
        }

        /// <summary>
        /// Detiene el seguimiento y vuelve al estado anterior o Idle.
        /// </summary>
        public void StopFollowing()
        {
            if (_npcManager?.Brain == null) return;
            
            if (_stateBeforeJoining != null && !(_stateBeforeJoining is FollowPlayerState))
            {
                _npcManager.Brain.ChangeState(_stateBeforeJoining);
            }
            else
            {
                _npcManager.ForceIdle();
            }
        }
        
        /// <summary>
        /// Mueve al NPC a una posición específica para un diálogo.
        /// El NPC cambia temporalmente a un estado de movimiento hacia esa posición.
        /// </summary>
        /// <param name="targetPosition">Posición objetivo al lado del player</param>
        /// <param name="maxTime">Tiempo máximo antes de teletransportar</param>
        /// <param name="npcTarget">El NPC con quien se está hablando (para mirar hacia él)</param>
        public void MoveToDialoguePosition(Vector3 targetPosition, float maxTime, Transform npcTarget = null)
        {
            if (_npcManager?.Brain == null || _agent == null)
            {
                LogWarning("No se puede mover a posición de diálogo: NPC no inicializado");
                return;
            }
            
            // Cambiar a un estado especial de diálogo con el NPC target
            _npcManager.Brain.ChangeState(new Game.NPC.States.DialoguePositionState(this, targetPosition, maxTime, npcTarget));
            
            Log($"📍 Moviéndose a posición de diálogo: {targetPosition}");
        }
        
        /// <summary>
        /// Libera al NPC del posicionamiento de diálogo y vuelve a seguir al player.
        /// </summary>
        public void ReleaseDialoguePosition()
        {
            if (!_isInParty || _npcManager?.Brain == null)
                return;
            
            // Volver al estado de seguir al player
            var currentState = _npcManager.Brain.CurrentState;
            if (currentState is Game.NPC.States.DialoguePositionState)
            {
                _npcManager.Brain.ChangeState(new Game.NPC.States.FollowPlayerState(this));
                Log("🔓 Liberado de posición de diálogo, volviendo a seguir");
            }
        }
        
        #endregion

        #region Internal Callbacks (llamados por PlayerParty)
        /// <summary>
        /// Llamado cuando se ha añadido exitosamente al equipo.
        /// </summary>
        internal void OnJoinedParty(PlayerParty party)
        {
            _party = party;
            _isInParty = true;
            _isJoining = false; // ✅ FIX: Limpiar flag de joining
            if (_interactable == null)
                _interactable = GetComponent<Interactable>() ?? GetComponentInChildren<Interactable>(true);
            _interactable?.SetHintVisible(false);
            
            Log($"✨ Unido al equipo (índice {PartyIndex})");
            
            // Sistema robusto: verificar estado inmediatamente
            if (_npcManager?.Brain != null)
            {
                // Iniciar seguimiento INMEDIATAMENTE
                StartFollowing();
            }
            else
            {
                LogWarning("Brain no inicializado. El Update verificará cuando esté listo.");
                // Update verificará y llamará StartFollowing cuando esté listo
            }
            
            OnJoined?.Invoke();
        }
        

        /// <summary>
        /// Llamado cuando se ha removido del equipo.
        /// </summary>
        internal void OnLeftParty()
        {
            _isInParty = false;
            _isJoining = false; // ✅ FIX: Limpiar flag por si acaso
            
            Log("👋 Abandonó el equipo");
            
            // Detener seguimiento
            StopFollowing();
            
            _party = null;
            OnLeft?.Invoke();
        }

        /// <summary>
        /// Llamado cuando el jugador entra en combate.
        /// SIMPLIFICADO: Entra en combate sin verificar rango.
        /// </summary>
        internal void OnPlayerEnteredCombat(Transform enemy)
        {
            Debug.Log($"[NPCPartyMember:{name}] 🔔 OnPlayerEnteredCombat - Enemigo: {enemy?.name}");

            if (_npcManager?.Brain == null)
            {
                Debug.LogError($"[NPCPartyMember:{name}] ⚠️ _npcManager.Brain es NULL!");
                return;
            }

            // No interrumpir si el jugador está controlando directamente este NPC
            if (ActiveCharacterSwapper.Instance != null && ActiveCharacterSwapper.Instance.HiddenNpc == this)
            {
                Debug.Log($"[NPCPartyMember:{name}] ℹ️ Ignorando combate: personaje bajo control del jugador");
                return;
            }
            
            _wasInPartyBeforeCombat = _isInParty;
            
            // Entrar en combate directamente
            _npcManager.Context.IsInCombat = true;
            
            // Cambiar a AllyCombatState CON EL TARGET
            if (!(_npcManager.Brain.CurrentState is States.AllyCombatState))
            {
                // Pasar el enemigo al constructor para que lo ataque directamente
                _npcManager.Brain.ChangeState(new States.AllyCombatState(enemy));
                Debug.Log($"[NPCPartyMember:{name}] ⚔️ CAMBIADO A AllyCombatState con target: {enemy?.name}");
            }
        }

        /// <summary>
        /// Llamado cuando el jugador sale del combate.
        /// </summary>
        internal void OnPlayerExitedCombat()
        {
            if (_npcManager == null) return;
            
            // Si estaba en party antes del combate, volver a seguir
            if (_wasInPartyBeforeCombat && _isInParty)
            {
                Log("🏳️ Combate terminado, volviendo a seguir al jugador");
                _npcManager.ExitCombat();
                if (PartyControlManager.Instance?.IsPartyFollowing ?? true)
                    StartFollowing();
                else
                    _npcManager.ForceIdle();
            }
        }

        /// <summary>
        /// Llamado cuando ha sido teletransportado cerca del jugador.
        /// </summary>
        internal void OnTeleportedToPlayer()
        {
            Log("⚡ Teletransportado cerca del jugador");
            
            // Asegurar que sigue en estado de seguimiento
            if (_isInParty && _npcManager?.Brain?.CurrentState is not FollowPlayerState)
            {
                StartFollowing();
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Obtiene la distancia actual al jugador.
        /// </summary>
        public float GetDistanceToPlayer()
        {
            if (_party?.PlayerTransform == null) return float.MaxValue;
            return Vector3.Distance(transform.position, _party.PlayerTransform.position);
        }

        /// <summary>
        /// Obtiene la posición de formación asignada.
        /// </summary>
        public Vector3 GetFormationPosition()
        {
            return _party?.GetFormationPosition(PartyIndex) ?? transform.position;
        }

        private void Log(string message)
        {
            if (debugMode) Debug.Log($"[NPCPartyMember:{name}] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[NPCPartyMember:{name}] ⚠️ {message}");
        }
        #endregion

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !_isInParty) return;
            
            // Mostrar posición de formación
            if (_party?.PlayerTransform != null)
            {
                Vector3 formationPos = GetFormationPosition();
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(formationPos, 0.3f);
                Gizmos.DrawLine(transform.position, formationPos);
            }
            
            // Mostrar rango de teleport
            if (partyConfig != null)
            {
                Gizmos.color = new Color(1, 0, 1, 0.2f);
                Gizmos.DrawWireSphere(transform.position, partyConfig.teleportDistance);
            }
        }
#endif
    }
}

