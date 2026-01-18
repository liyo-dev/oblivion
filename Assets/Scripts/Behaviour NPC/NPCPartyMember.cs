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
        private bool _isInParty;
        private bool _wasInPartyBeforeCombat;
        private INPCState _stateBeforeJoining;
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
            (_npcManager?.Configuration?.narrativeConfig?.narrativeID ?? gameObject.name);
        
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
            
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCPartyMember] {name} requiere NPCBehaviourManagerV2");
            }
        }

        void Start()
        {
            if (autoJoinOnStart)
            {
                // Delay para asegurar que todo esté inicializado
                Invoke(nameof(JoinParty), 0.1f);
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
            
            _party = PlayerParty.Instance;
            if (_party == null)
            {
                LogWarning("No se encontró PlayerParty en la escena");
                return false;
            }
            
            if (_party.IsFull)
            {
                LogWarning("El equipo está lleno");
                return false;
            }
            
            // Guardar estado actual para restaurarlo si sale del equipo
            if (_npcManager?.Brain != null)
            {
                _stateBeforeJoining = _npcManager.Brain.CurrentState;
            }
            
            bool success = _party.AddMember(this);
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
        #endregion

        #region Internal Callbacks (llamados por PlayerParty)
        /// <summary>
        /// Llamado cuando se ha añadido exitosamente al equipo.
        /// </summary>
        internal void OnJoinedParty(PlayerParty party)
        {
            _party = party;
            _isInParty = true;
            
            Log($"✨ Unido al equipo (índice {PartyIndex})");
            
            // Iniciar seguimiento
            StartFollowing();
            
            OnJoined?.Invoke();
        }

        /// <summary>
        /// Llamado cuando se ha removido del equipo.
        /// </summary>
        internal void OnLeftParty()
        {
            _isInParty = false;
            
            Log("👋 Abandonó el equipo");
            
            // Detener seguimiento
            StopFollowing();
            
            _party = null;
            OnLeft?.Invoke();
        }

        /// <summary>
        /// Llamado cuando el jugador entra en combate.
        /// </summary>
        internal void OnPlayerEnteredCombat(Transform enemy)
        {
            if (_npcManager == null) return;
            
            // Verificar distancia al enemigo
            float distance = Vector3.Distance(transform.position, enemy.position);
            float assistRange = partyConfig?.combatAssistRange ?? 12f;
            
            if (distance <= assistRange)
            {
                _wasInPartyBeforeCombat = _isInParty;
                Log($"⚔️ Asistiendo al jugador en combate contra {enemy.name}");
                
                // Entrar en combate ALIADO (no como enemigo)
                EnterAllyCombat(enemy);
            }
        }
        
        /// <summary>
        /// Entra en modo de combate aliado contra un enemigo específico.
        /// </summary>
        private void EnterAllyCombat(Transform enemy)
        {
            if (_npcManager?.Brain == null) return;
            
            // Configurar el enemigo como objetivo (en context.Player)
            _npcManager.Context.Player = enemy;
            _npcManager.Context.IsInCombat = true;
            
            // Usar AllyCombatState en lugar de CombatState
            if (!(_npcManager.Brain.CurrentState is States.AllyCombatState))
            {
                _npcManager.Brain.ChangeState(new States.AllyCombatState());
                Log($"⚔️ Entrando en AllyCombatState contra {enemy.name}");
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
                StartFollowing();
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

