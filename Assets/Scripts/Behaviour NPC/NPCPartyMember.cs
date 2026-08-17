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

        [Tooltip("Si está activado, este NPC se ocultará (SetActive false) cuando no esté en el party y se activará al unirse")]
        [SerializeField] private bool hideWhenNotInParty = false;
        
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
        private Damageable _damageable;
        // --- Preparación para escalada ---
        private bool _waitingForClimb = false;
        private Vector3 _climbBasePosition = Vector3.zero;
        // Slot activo cuando este NPC se unió al party; null = unión mientras Will era activo (miembro compartido)
        internal PartyControlManager.CharacterSlot? _joinedForSlot;
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
        /// Si true, el NPC se oculta cuando no está en el party y se activa al unirse.
        /// </summary>
        public bool HideWhenNotInParty => hideWhenNotInParty;

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
            !_npcManager.Context.IsInCinematic &&
            !CinematicSequencerBase.AnySequenceActive;
        
        /// <summary>
        /// Nombre para mostrar en UI
        /// </summary>
        public string DisplayName => partyConfig?.displayName ??
            (_npcManager?.PersistenceId ?? gameObject.name);

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

        /// <summary>
        /// Indica si este miembro está en modo "preparado para trepar" (esperando espacio en la base).
        /// </summary>
        public bool IsWaitingForClimb => _waitingForClimb;

        /// <summary>
        /// Posición base donde se espera que el miembro comience la escalada.
        /// </summary>
        public Vector3 ClimbBasePosition => _climbBasePosition;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            _agent = GetComponent<NavMeshAgent>();
            _interactable = GetComponent<Interactable>() ?? GetComponentInChildren<Interactable>(true);
            _damageable = GetComponent<Damageable>();
            
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
                var party = PlayerParty.Instance;
                if (party != null && !party.IsFull && NPCInitializer.IsNPCReady(_npcManager, out _))
                {
                    JoinParty();
                }
            }
            
            // Si está en el party pero no está siguiendo (Brain no estaba listo), verificar cada 0.5s
            // No actuar si este NPC está oculto (controlado por el jugador vía character swap)
            if (_isInParty && _npcManager != null && _npcManager.Brain != null && Time.time >= _nextIdleCheck)
            {
                _nextIdleCheck = Time.time + 0.5f;

                if (ActiveCharacterSwapper.Instance != null && ActiveCharacterSwapper.Instance.HiddenNpc == this)
                    return;

                var currentState = _npcManager.Brain.CurrentState;
                // ✅ FIX: comprobar también CinematicSequencerBase.AnySequenceActive (mismo patrón
                // que CompanionFollowPrompt, comentario "FIX INC-059"). Context.IsInCinematic solo se
                // activa vía CinematicState de la FSM; TabernaSequencer sienta a los NPCs pausando
                // NPCBehaviourManagerV2 directamente (ForceIdle() + enabled=false) sin pasar por esa
                // FSM, así que Context.IsInCinematic nunca llega a true durante la secuencia. Como
                // ForceIdle() deja al Brain en el estado "Idle", este chequeo (que corre en
                // NPCPartyMember.Update(), un componente aparte que sigue activo aunque
                // NPCBehaviourManagerV2 esté deshabilitado) veía "Idle" + "no cinemática" y llamaba a
                // StartFollowing() a mitad de la secuencia de la taberna — deshaciendo el sentado de
                // Estela de forma intermitente (según el instante en que caía este chequeo de 0.5s
                // respecto al SeatNPC). Eldran no sufría esto porque en esta partida no figura como
                // miembro activo del party (ver PlayerParty), así que este bucle nunca se ejecutaba
                // para él.
                if (currentState != null && currentState.StateName == "Idle"
                    && !_npcManager.Context.IsInCombat && !_npcManager.Context.IsInCinematic
                    && !CinematicSequencerBase.AnySequenceActive
                    && (PartyControlManager.Instance?.IsPartyFollowing ?? true)
                    && GetDistanceToPlayer() <= 40f)
                {
                    StartFollowing();
                }
            }
        }

        void OnDestroy()
        {
            if (_isInParty)
            {
                // No llamar LeaveParty() al destruirse por cambio de escena:
                // LeaveParty() → SyncPartyToPreset() vaciaría partyMemberIds uno a uno
                // mientras Unity destruye todos los GOs, corrompiendo el preset para el restore.
                // Usamos una ruta silenciosa que preserva el preset intacto.
                _isInParty = false;
                var partyRef = _party;
                _party = null;
                partyRef?.RemoveMemberFromDestroy(this);
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
        /// <param name="isRestore">
        /// True cuando la unión viene de restaurar una partida/preset guardado (el miembro ya
        /// formaba parte del equipo antes del guardado). En ese caso NO debe disparar los efectos
        /// secundarios de una unión "en vivo" (p.ej. auto-completar pasos de misión ligados a
        /// requiredPartyMembers), porque esos pasos ya reflejan el estado real guardado y no deben
        /// re-evaluarse solo por reconstruir el equipo al cargar.
        /// </param>
        public bool JoinParty(bool isRestore = false)
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
                // FIX (17 ago 2026) — CAUSA RAÍZ REAL confirmada por consola + inspector del bug
                // crítico "Estela/Liam no vuelven al party tras cerrar y reabrir el juego":
                // NPCBehaviourManagerV2.UpdateDistanceLOD() "duerme" a cualquier NPC lejos del
                // jugador (EnterFarState() → agent.enabled = false, optimización de rendimiento
                // para NPCs ambientales dispersos por el mundo). En un arranque en frío, un
                // compañero de equipo aparece en su posición de diseño de escena — que puede
                // estar a cientos de metros del punto de guardado del jugador (Estela: 578m) —
                // y ese chequeo de distancia se dispara ANTES de que PlayerParty consiga
                // restaurarlo. HasActiveAiExemption() solo exime de dormir a NPCs con
                // IsAlly=true (ya unidos al party) — un compañero todavía PENDIENTE de unirse no
                // cuenta, así que nunca se despertaba: dormido porque no está en el party,
                // incapaz de unirse al party porque está dormido (agent.enabled=false, por lo que
                // ni SamplePosition+Warp sirven de nada — un NavMeshAgent deshabilitado no puede
                // estar "on mesh" pase lo que pase). Este método es el único sitio por el que
                // pasa CUALQUIER intento de unirse al equipo (incluido cada reintento cada 2s
                // desde PlayerParty.RetryPendingMembers), así que es el punto correcto para
                // reactivarlo: en cuanto el join tenga éxito más abajo, IsAlly pasará a true y el
                // propio Update() de NPCBehaviourManagerV2 llamará a ExitFarState() de forma
                // normal en su siguiente ciclo, dejando todo consistente (Brain, animaciones, etc).
                if (_agent.enabled == false)
                {
                    _agent.enabled = true;
                    LogWarning("NavMeshAgent estaba deshabilitado (NPC 'dormido' por estar lejos del jugador — UpdateDistanceLOD/EnterFarState) — reactivado para poder unirse al party");
                }

                if (NavMesh.SamplePosition(transform.position, out var navHit, 10f, NavMesh.AllAreas))
                {
                    _agent.Warp(navHit.position);
                    LogWarning($"NavMeshAgent no estaba en NavMesh — auto-recuperado con Warp a {navHit.position} (pos. previa: {transform.position})");
                }

                if (!_agent.isOnNavMesh)
                {
                    LogWarning("NavMeshAgent no está en NavMesh, no se puede unir al party ahora (auto-recuperación falló: no se encontró NavMesh cerca de la posición actual)");
                    return false;
                }
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
            
            bool success = _party.AddMember(this, isRestore);
            
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

            // Solo cambiar a FollowState si no estamos en combate/cinemática.
            // ✅ FIX: añadido AnySequenceActive — ver comentario detallado en Update().
            if (!_npcManager.Context.IsInCombat && !_npcManager.Context.IsInCinematic
                && !CinematicSequencerBase.AnySequenceActive)
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
            // ✅ FIX: añadido AnySequenceActive — ver comentario detallado en Update().
            if (!_npcManager.Context.IsInCombat && !_npcManager.Context.IsInCinematic
                && !CinematicSequencerBase.AnySequenceActive)
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
        /// En diálogos grupales: cambia hacia quién mira este compañero mientras mantiene su
        /// posición de diálogo. La usa DialogueCinematicController en cada línea para que los
        /// personajes se miren entre ellos cuando se contestan. Pasar null para volver al
        /// objetivo por defecto (el NPC del diálogo). No hace nada si el compañero no está
        /// en DialoguePositionState (p.ej. anclado en modo Libre o en combate).
        /// </summary>
        public void SetDialogueLookTarget(Transform target)
        {
            if (_npcManager?.Brain?.CurrentState is Game.NPC.States.DialoguePositionState dialogueState)
            {
                dialogueState.SetLookTargetOverride(target);
            }
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
        /// <param name="isRestore">
        /// True cuando el join viene de restaurar una partida guardada (ver JoinParty). En ese caso
        /// NUNCA se debe leer PartyControlManager.ActiveSlot para fijar _joinedForSlot: PlayerParty
        /// restaura los miembros desde OnProfileReady, suscrito en su Awake (execution order -500),
        /// mientras que PartyControlManager.HandleProfileReady —que resetea _activeIndex a Will,
        /// "al cargar partida siempre se arranca como Will"— se suscribe en su Start (-200) y por
        /// tanto se ejecuta DESPUÉS en la invocación del mismo evento. Sin este parámetro, un
        /// miembro restaurado podía heredar el _activeIndex TODAVÍA SIN RESETEAR de la sesión
        /// anterior (p.ej. si el jugador murió controlando a Estela, _activeIndex seguía en Estela
        /// en el momento del restore) y quedar mal etiquetado con _joinedForSlot=Estela aunque la
        /// partida recién cargada arranca en Will. Ese etiquetado erróneo podía provocar que
        /// ActiveCharacterSwapper.SwitchCharacter desvinculara compañeros del equipo al cambiar de
        /// personaje sin motivo (ver comentario "5b. Desvincular compañeros..." en ese archivo).
        /// </param>
        internal void OnJoinedParty(PlayerParty party, bool isRestore = false)
        {
            // Registrar el slot activo en el momento del join.
            // Will (slot 1) → null (miembro compartido). Liam/Estela → ese slot específico.
            // Al restaurar una partida no existe todavía un "personaje activo en vivo": siempre null.
            var activeSlot = isRestore ? null : PartyControlManager.Instance?.ActiveSlot;
            _joinedForSlot = (activeSlot.HasValue && activeSlot.Value != PartyControlManager.CharacterSlot.Will)
                ? activeSlot
                : null;

            // Limpiar anclaje al rejoinearse al party
            if (_npcManager?.Context != null)
                _npcManager.Context.IsPinnedByParty = false;

            // Asegurar que los renderers están visibles (pueden haber sido ocultados por HideNonPartyNPCs)
            // EXCEPTO si este NPC es el oculto (controlado por el jugador vía character swap)
            bool isHiddenBySwapper = ActiveCharacterSwapper.Instance != null
                                     && ActiveCharacterSwapper.Instance.HiddenNpc == this;
            if (!isHiddenBySwapper)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    r.enabled = true;
            }

            _party = party;
            _isInParty = true;
            _isJoining = false;
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
            _isJoining = false;
            _joinedForSlot = null;

            Log("👋 Abandonó el equipo");

            // Anclar al NPC: queda fijo donde está sin vagar hasta que rejoinee el party
            if (_npcManager?.Context != null)
                _npcManager.Context.IsPinnedByParty = true;

            // Forzar Idle directamente (no restaurar estado anterior que podría ser WanderState)
            _npcManager?.ForceIdle();

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

            if (_isInParty && _npcManager?.Brain?.CurrentState is not FollowPlayerState
                && (PartyControlManager.Instance?.IsPartyFollowing ?? true))
            {
                StartFollowing();
            }
        }

        /// <summary>
        /// Marca a este NPC como "preparado para trepar". Se llama desde PlayerParty
        /// cuando el player inicia una escalada. El NPC se moverá a la posición base
        /// y esperará hasta que haya espacio para empezar a subir.
        /// </summary>
        public void PrepareForClimb(Vector3 basePosition)
        {
            _climbBasePosition = basePosition;
            _waitingForClimb = true;
            Log($"Preparado para trepar (base: {_climbBasePosition})");
            // Si estamos ya siguiendo, cambiamos a FollowPlayerState para que el state
            // especial de Follow maneje el movimiento 3D hacia la formación en la base.
            if (_npcManager?.Brain != null)
            {
                _npcManager.Brain.ChangeState(new Game.NPC.States.FollowPlayerState(this));
            }
        }

        /// <summary>
        /// Cancela la preparación para la escalada (p.ej. player abandonó la escalada).
        /// </summary>
        public void CancelClimbPreparation()
        {
            if (!_waitingForClimb) return;
            _waitingForClimb = false;
            _climbBasePosition = Vector3.zero;
            Log("Cancelada preparación de escalada");
            // Volver a seguir normalmente si corresponde
            if (_isInParty && _npcManager?.Brain != null)
            {
                if (PartyControlManager.Instance?.IsPartyFollowing ?? true)
                    StartFollowing();
                else
                    _npcManager.ForceIdle();
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

