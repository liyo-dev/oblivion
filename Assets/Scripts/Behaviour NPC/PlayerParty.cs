using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC
{
    /// <summary>
    /// Sistema central de gestión del equipo (Party) del jugador.
    /// Singleton que mantiene registro de todos los NPCs compañeros activos.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class PlayerParty : MonoBehaviour
    {
        #region Bootstrap
        /// <summary>
        /// Auto-crea el PlayerParty al inicio del juego si no existe.
        /// Esto garantiza que esté listo para suscribirse a OnProfileReady.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            
            var existing = FindFirstObjectByType<PlayerParty>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }
            
            var go = new GameObject("PlayerParty");
            _instance = go.AddComponent<PlayerParty>();
            DontDestroyOnLoad(go);
            Debug.Log("[PlayerParty] 🚀 Bootstrap: Instancia creada automáticamente");
        }
        #endregion
        
        #region Singleton
        private static PlayerParty _instance;
        public static PlayerParty Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PlayerParty>();
                    if (_instance == null)
                    {
                        var go = new GameObject("PlayerParty");
                        _instance = go.AddComponent<PlayerParty>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        public static bool HasInstance => _instance != null;
        #endregion

        #region Configuration
        [Header("Configuración del Equipo")]
        [SerializeField] private int maxPartySize = 4;
        [SerializeField] private bool debugMode = true;
        
        [Header("Teleport Settings")]
        [Tooltip("Radio alrededor del jugador donde reaparecerán los compañeros")]
        [SerializeField] private float teleportRadius = 2f; // CAMBIADO: Más cerca (era 3f)
        
        [Tooltip("Distancia mínima del jugador para el teleport")]
        [SerializeField] private float minTeleportDistance = 1.5f; // CAMBIADO: Más cerca (era 2f)
        #endregion

        #region State
        private readonly List<NPCPartyMember> _members = new();
        private Transform _playerTransform;
        private bool _isInitialized;
        
        // ✅ OPTIMIZACIÓN FASE 1: Timer para throttling de verificación de distancias
        private float _distanceCheckTimer;
        
        // ✅ Sistema robusto: Timer para retry de miembros pendientes
        private float _retryPendingTimer;
        
        // ✅ OPTIMIZACIÓN FASE 1: Buffer reutilizable para Physics queries
        private Collider[] _enemySearchBuffer = new Collider[32];
        
        // IDs de miembros pendientes de restaurar (si no se encontraron en la escena actual)
        private List<string> _pendingMemberIds = new();
        #endregion

        #region Events
        /// <summary>
        /// Se dispara cuando un NPC se une al equipo
        /// </summary>
        public static event Action<NPCPartyMember> OnMemberJoined;
        
        /// <summary>
        /// Se dispara cuando un NPC abandona el equipo
        /// </summary>
        public static event Action<NPCPartyMember> OnMemberLeft;
        
        /// <summary>
        /// Se dispara cuando cambia la composición del equipo
        /// </summary>
        public static event Action<IReadOnlyList<NPCPartyMember>> OnPartyChanged;
        
        #if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OnMemberJoined = null;
            OnMemberLeft = null;
            OnPartyChanged = null;
        }
        #endif
        #endregion

        #region Properties
        /// <summary>
        /// Lista de miembros actuales del equipo (solo lectura)
        /// </summary>
        public IReadOnlyList<NPCPartyMember> Members => _members;
        
        /// <summary>
        /// Número de miembros actuales
        /// </summary>
        public int MemberCount => _members.Count;
        
        /// <summary>
        /// ¿El equipo está lleno?
        /// </summary>
        public bool IsFull => _members.Count >= maxPartySize;
        
        /// <summary>
        /// ¿El equipo está vacío?
        /// </summary>
        public bool IsEmpty => _members.Count == 0;
        
        /// <summary>
        /// Tamaño máximo del equipo
        /// </summary>
        public int MaxSize => maxPartySize;
        
        /// <summary>
        /// Transform del jugador (cacheado)
        /// </summary>
        public Transform PlayerTransform => _playerTransform;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            Debug.Log("[PlayerParty] 🚀 Awake iniciado");
            
            if (_instance != null && _instance != this)
            {
                Debug.Log("[PlayerParty] ⚠️ Instancia duplicada detectada, destruyendo...");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Subscribirse a eventos del jugador
            PlayerService.OnPlayerRegistered += OnPlayerRegistered;
            PlayerService.OnPlayerUnregistered += OnPlayerUnregistered;
            
            // Subscribirse a eventos de combate para notificar a compañeros
            ActiveCombatRegistry.OnNPCEnteredCombat += OnEnemyEnteredCombat;
            ActiveCombatRegistry.OnNPCExitedCombat += OnEnemyExitedCombat;
            
            // 🔔 Subscribirse cuando el jugador ataca para alertar compañeros
            MagicProjectileSpawner.OnPlayerAttacked += OnPlayerAttacked;
            
            // Subscribirse a OnProfileReady para restaurar el party al cargar partida
            GameBootService.OnProfileReady += OnProfileReady;
            ProfileReadyDiagnostics.RegisterSubscriber(nameof(PlayerParty));
            
            // Subscribirse a cambios de escena para reintentar miembros pendientes
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedHandler;
            
            // Intentar obtener referencia inicial
            ResolvePlayerReference();
            
            // ✅ Si GameBootService ya disparó el evento antes de que nos suscribiéramos,
            // llamamos manualmente a OnProfileReady para leer el runtimePreset
            if (GameBootService.Profile != null)
            {
                Debug.Log("[PlayerParty] ℹ️ GameBootService ya inicializado, leyendo runtimePreset...");
                OnProfileReady();
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
                PlayerService.OnPlayerUnregistered -= OnPlayerUnregistered;
                ActiveCombatRegistry.OnNPCEnteredCombat -= OnEnemyEnteredCombat;
                ActiveCombatRegistry.OnNPCExitedCombat -= OnEnemyExitedCombat;
                MagicProjectileSpawner.OnPlayerAttacked -= OnPlayerAttacked;
                GameBootService.OnProfileReady -= OnProfileReady;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedHandler;
                _instance = null;
            }
        }

        void Update()
        {
            if (!_isInitialized || _playerTransform == null) return;
            
            // ✅ OPTIMIZACIÓN MEJORADA: Verificar distancias cada 0.3 segundos (era 0.5)
            // Más frecuente para respuesta más rápida sin ser demasiado intensivo
            _distanceCheckTimer += Time.deltaTime;
            if (_distanceCheckTimer >= 0.3f)
            {
                _distanceCheckTimer = 0;
                CheckMemberDistances();
            }
            
            // ✅ NUEVO: Retry agresivo de miembros pendientes cada 2 segundos
            if (_pendingMemberIds.Count > 0)
            {
                _retryPendingTimer += Time.deltaTime;
                if (_retryPendingTimer >= 2f)
                {
                    _retryPendingTimer = 0;
                    RetryPendingMembers();
                }
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Añade un NPC al equipo del jugador.
        /// </summary>
        /// <returns>True si se añadió exitosamente</returns>
        public bool AddMember(NPCPartyMember member)
        {
            if (member == null)
            {
                LogWarning("Intento de añadir miembro nulo");
                return false;
            }
            
            if (IsFull)
            {
                LogWarning($"Equipo lleno ({MemberCount}/{maxPartySize}). No se puede añadir {member.name}");
                return false;
            }
            
            if (_members.Contains(member))
            {
                LogWarning($"{member.name} ya está en el equipo");
                return false;
            }
            
            _members.Add(member);
            member.OnJoinedParty(this);
            
            Debug.Log($"[PlayerParty] ✨✨✨ {member.DisplayName} se unió al equipo [{MemberCount}/{maxPartySize}] - PartyConfig: {(member.PartyConfig != null ? "✅" : "❌")}, autoJoinCombat: {member.PartyConfig?.autoJoinPlayerCombat}");
            
            // Sincronizar con el preset para persistencia
            SyncPartyToPreset();
            
            OnMemberJoined?.Invoke(member);
            OnPartyChanged?.Invoke(_members);
            
            return true;
        }

        /// <summary>
        /// Remueve un NPC del equipo del jugador.
        /// </summary>
        /// <returns>True si se removió exitosamente</returns>
        public bool RemoveMember(NPCPartyMember member)
        {
            if (member == null) return false;
            
            if (!_members.Contains(member))
            {
                LogWarning($"{member.name} no está en el equipo");
                return false;
            }
            
            _members.Remove(member);
            member.OnLeftParty();
            
            Log($"👋 {member.DisplayName} abandonó el equipo [{MemberCount}/{maxPartySize}]");
            
            // Sincronizar con el preset para persistencia
            SyncPartyToPreset();
            
            OnMemberLeft?.Invoke(member);
            OnPartyChanged?.Invoke(_members);
            
            return true;
        }

        /// <summary>
        /// Comprueba si un NPC específico está en el equipo.
        /// </summary>
        public bool HasMember(NPCPartyMember member)
        {
            return member != null && _members.Contains(member);
        }
        
        /// <summary>
        /// Busca un miembro por su ID narrativo.
        /// </summary>
        public NPCPartyMember GetMemberByNarrativeId(string narrativeId)
        {
            return _members.FirstOrDefault(m =>
                m.NPCManager?.PersistenceId == narrativeId);
        }
        
        /// <summary>
        /// Busca un miembro por nombre.
        /// </summary>
        public NPCPartyMember GetMemberByName(string displayName)
        {
            return _members.FirstOrDefault(m => 
                m.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Obtiene una posición válida cerca del jugador para un compañero.
        /// </summary>
        public Vector3 GetFormationPosition(int memberIndex)
        {
            if (_playerTransform == null) return Vector3.zero;
            
            // Calcular posición en formación (semicírculo detrás del jugador, MÁS CERCA)
            float angle = CalculateFormationAngle(memberIndex);
            float distance = CalculateFormationDistance(memberIndex);
            
            Vector3 offset = Quaternion.Euler(0, angle, 0) * (-_playerTransform.forward * distance);
            Vector3 targetPos = _playerTransform.position + offset;
            
            // Validar en NavMesh con mayor radio de búsqueda
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                return hit.position;
            }
            
            // Fallback más agresivo: posición detrás del jugador
            Vector3 fallbackPos = _playerTransform.position - _playerTransform.forward * (distance * 0.8f);
            if (NavMesh.SamplePosition(fallbackPos, out NavMeshHit fallbackHit, 5f, NavMesh.AllAreas))
            {
                return fallbackHit.position;
            }
            
            return _playerTransform.position + (-_playerTransform.forward * distance);
        }

        /// <summary>
        /// Fuerza a todos los miembros a teletransportarse cerca del jugador.
        /// </summary>
        public void TeleportAllMembersToPlayer()
        {
            if (_playerTransform == null) return;

            var hiddenNpc = ActiveCharacterSwapper.Instance?.HiddenNpc;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i] == hiddenNpc) continue;
                TeleportMemberToPlayer(_members[i], i);
            }
        }
        
        /// <summary>
        /// Posiciona a los party members al lado del player para un diálogo con un NPC.
        /// Los miembros se posicionan según su configuración (izquierda/derecha).
        /// </summary>
        /// <param name="npcTarget">El NPC con quien el player está hablando (opcional, para orientación)</param>
        public void PositionMembersForDialogue(Transform npcTarget = null)
        {
            if (_playerTransform == null || _members.Count == 0)
                return;
            
            Log($"📍 Posicionando {_members.Count} party members para diálogo");
            
            // ✅ Rotar al player hacia el NPC
            if (npcTarget != null)
            {
                Vector3 toNpc = (npcTarget.position - _playerTransform.position);
                toNpc.y = 0;
                if (toNpc.sqrMagnitude > 0.01f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(toNpc);
                    _playerTransform.rotation = lookRotation;
                    Log($"👁️ Player girado hacia {npcTarget.name}");
                }
            }
            
            // Determinar la dirección frontal del player (hacia el NPC)
            Vector3 playerForward = _playerTransform.forward;
            if (npcTarget != null)
            {
                Vector3 toNpc = (npcTarget.position - _playerTransform.position).normalized;
                toNpc.y = 0;
                if (toNpc.sqrMagnitude > 0.01f)
                {
                    playerForward = toNpc;
                }
            }
            
            // ✅ Calcular la derecha del player MIRANDO al NPC
            // Right = Vector3.Cross(up, forward) → la derecha cuando miras hacia adelante
            Vector3 playerRight = Vector3.Cross(Vector3.up, playerForward).normalized;
            
            // Contadores para distribuir múltiples NPCs en el mismo lado
            int leftCount = 0;
            int rightCount = 0;
            
            var hiddenNpc = ActiveCharacterSwapper.Instance?.HiddenNpc;
            foreach (var member in _members)
            {
                if (member == null || !member.IsActiveInParty || member.PartyConfig == null)
                    continue;

                // No posicionar al NPC oculto (controlado por el jugador vía character swap)
                if (member == hiddenNpc) continue;
                
                // Verificar si debe posicionarse durante diálogos
                if (!member.PartyConfig.posicionarseDuranteDialogos)
                {
                    Log($"  ↳ {member.DisplayName}: posicionamiento desactivado");
                    continue;
                }
                
                // Determinar el lado y calcular posición
                bool isLeftSide = member.PartyConfig.ladoPreferidoDialogo == Modules.DialoguePositionSide.Left;
                int sideCount = isLeftSide ? leftCount++ : rightCount++;
                
                // Calcular offset escalonado si hay múltiples NPCs en el mismo lado
                float lateralDistance = member.PartyConfig.distanciaLateralDialogo + (sideCount * 0.5f);
                float forwardOffset = member.PartyConfig.offsetDelanteDialogo - (sideCount * 0.3f);
                
                // Calcular posición objetivo
                Vector3 lateralOffset = playerRight * lateralDistance * (isLeftSide ? -1f : 1f);
                Vector3 forwardOffsetVec = playerForward * forwardOffset;
                Vector3 targetPosition = _playerTransform.position + lateralOffset + forwardOffsetVec;
                
                // Validar en NavMesh
                if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    targetPosition = hit.position;
                }
                
                // Si el miembro está lejos, teleportarlo directamente para que estén todos en el diálogo
                const float teleportThreshold = 3f;
                float distToTarget = Vector3.Distance(member.transform.position, targetPosition);
                if (distToTarget > teleportThreshold)
                {
                    var navAgent = member.NPCManager?.Agent;
                    if (navAgent != null && navAgent.isOnNavMesh)
                        navAgent.Warp(targetPosition);
                    else
                        member.transform.position = targetPosition;
                    Log($"  ↳ {member.DisplayName} teleportado a posición de diálogo (distancia era {distToTarget:F1}m > {teleportThreshold}m)");
                }

                // Enviar al miembro a esa posición
                member.MoveToDialoguePosition(targetPosition, member.PartyConfig.tiempoMaximoMovimientoDialogo, npcTarget);
                
                string sideName = isLeftSide ? "IZQUIERDA" : "DERECHA";
                Log($"  ↳ {member.DisplayName} → {sideName} (distancia: {lateralDistance:F1}m, offset: {forwardOffset:F1}m)");
            }
        }
        
        /// <summary>
        /// Libera a los party members del posicionamiento de diálogo y vuelven a seguir normalmente.
        /// </summary>
        public void ReleaseDialoguePositioning()
        {
            if (_members.Count == 0) return;
            
            Log($"🔓 Liberando posicionamiento de diálogo para {_members.Count} members");
            
            foreach (var member in _members)
            {
                if (member == null || !member.IsActiveInParty)
                    continue;
                
                member.ReleaseDialoguePosition();
            }
        }

        /// <summary>
        /// Notifica a todos los compañeros que el jugador entró en combate.
        /// </summary>
        public void NotifyPlayerEnteredCombat(Transform enemy)
        {
            Debug.Log($"[PlayerParty] 🔔 NotifyPlayerEnteredCombat - Enemigo: {enemy.name}, Compañeros en party: {_members.Count}");
            
            int notifiedCount = 0;
            foreach (var member in _members)
            {
                if (member == null)
                {
                    Debug.LogWarning($"[PlayerParty] ⚠️ Miembro null en party!");
                    continue;
                }
                
                if (member.PartyConfig == null)
                {
                    Debug.LogWarning($"[PlayerParty] ⚠️ {member.name} no tiene PartyConfig asignado!");
                    continue;
                }
                
                if (!member.PartyConfig.autoJoinPlayerCombat)
                {
                    Debug.Log($"[PlayerParty] ⚠️ {member.DisplayName} tiene autoJoinPlayerCombat=FALSE, no se notificará");
                    continue;
                }
                
                Debug.Log($"[PlayerParty] ✅ Notificando a {member.DisplayName} sobre combate con {enemy.name}");
                member.OnPlayerEnteredCombat(enemy);
                notifiedCount++;
            }
            
            Debug.Log($"[PlayerParty] 📊 Total notificados: {notifiedCount}/{_members.Count}");
            
            // Notificar al Will NPC instanciado (no está en _members pero debe asistir en combate)
            var willNpc = ActiveCharacterSwapper.Instance?.WillNpcInstance;
            if (willNpc != null && willNpc.PartyConfig != null && willNpc.PartyConfig.autoJoinPlayerCombat)
            {
                Debug.Log($"[PlayerParty] ✅ Notificando al Will NPC instanciado sobre combate con {enemy.name}");
                willNpc.OnPlayerEnteredCombat(enemy);
                notifiedCount++;
            }
        }

        /// <summary>
        /// Notifica a todos los compañeros que el jugador salió del combate.
        /// </summary>
        public void NotifyPlayerExitedCombat()
        {
            foreach (var member in _members)
            {
                member.OnPlayerExitedCombat();
            }
            
            // Notificar al Will NPC instanciado
            var willNpc = ActiveCharacterSwapper.Instance?.WillNpcInstance;
            if (willNpc != null)
            {
                willNpc.OnPlayerExitedCombat();
            }
        }

        /// <summary>
        /// Disuelve el equipo, removiendo a todos los miembros.
        /// </summary>
        public void DisbandParty()
        {
            Log("🔥 Disolviendo equipo...");
            
            // Crear copia para evitar modificar durante iteración
            var membersToRemove = new List<NPCPartyMember>(_members);
            foreach (var member in membersToRemove)
            {
                RemoveMember(member);
            }
        }

        /// <summary>
        /// Limpia completamente el party para flujo de Nueva Partida.
        /// </summary>
        public void ResetForNewGame()
        {
            Log("🧼 ResetForNewGame: limpiando miembros y pendientes del party");

            _pendingMemberIds.Clear();
            DisbandParty();

            // Seguridad extra: en caso de referencias residuales/nulls.
            _members.RemoveAll(m => m == null);
            if (_members.Count > 0)
                _members.Clear();

            SyncPartyToPreset();
            OnPartyChanged?.Invoke(_members);
        }
        #endregion

        #region Internal
        private void OnPlayerRegistered(GameObject player)
        {
            ResolvePlayerReference();
        }

        private void OnPlayerUnregistered()
        {
            _playerTransform = null;
            _isInitialized = false;
        }

        private void ResolvePlayerReference()
        {
            if (PlayerService.TryGetPlayer(out var player))
            {
                _playerTransform = player.transform;
                _isInitialized = true;
                Log("Referencia al jugador establecida");
            }
        }

        /// <summary>
        /// Llamado cuando el GameBootProfile está listo (al cargar partida).
        /// Restaura los miembros del party desde el save.
        /// </summary>
        private void OnProfileReady()
        {
            var profile = GameBootService.Profile;
            if (profile == null)
            {
                Debug.LogWarning("[PlayerParty] ⚠️ OnProfileReady llamado pero GameBootService.Profile es null");
                return;
            }
            
            var preset = profile.GetActivePresetResolved();
            if (preset == null)
            {
                Debug.LogWarning("[PlayerParty] ⚠️ OnProfileReady: GetActivePresetResolved() devolvió null");
                return;
            }
            
            if (preset.partyMemberIds == null || preset.partyMemberIds.Count == 0)
            {
                if (_members.Count > 0)
                {
                    Debug.Log($"[PlayerParty] 🔄 Preset sin partyMemberIds; limpiando {_members.Count} miembros residuales");
                    ResetForNewGame();
                }
                else
                {
                    // Limpiar pendientes aunque _members esté vacío: si había IDs pendientes de una sesión anterior
                    // (cargada pero no consumida), el Update los reintentaría en la nueva escena y añadiría NPCs fantasma.
                    if (_pendingMemberIds.Count > 0)
                    {
                        Debug.Log($"[PlayerParty] 🧹 Limpiando {_pendingMemberIds.Count} IDs pendientes residuales (preset vacío)");
                        _pendingMemberIds.Clear();
                    }
                    Debug.Log("[PlayerParty] ℹ️ No hay miembros de party en el preset para restaurar");
                }
                // Ocultar NPCs con hideWhenNotInParty aunque no haya party (ej: nueva partida)
                HideNonPartyNPCs();
                return;
            }
            
            Debug.Log($"[PlayerParty] 🔄 Restaurando {preset.partyMemberIds.Count} miembros del party: [{string.Join(", ", preset.partyMemberIds)}]");

            // Sistema robusto sin delays: intentar restaurar AHORA
            RestoreMembersFromIds(preset.partyMemberIds);

            // Ocultar NPCs con hideWhenNotInParty que no forman parte del party activo
            HideNonPartyNPCs();

            // Si quedaron pendientes, el Update los manejará automáticamente
            if (_pendingMemberIds.Count > 0)
            {
                Log($"⏳ {_pendingMemberIds.Count} miembros pendientes. Update los reintentará cuando estén disponibles.");
            }
        }
        
        /// <summary>
        /// Oculta los renderers de todos los NPCPartyMember que no están en el party activo.
        /// Usa renderer toggling (no SetActive) para que el NPCRegistry los siga encontrando.
        /// Se llama al cargar partida para esconder personajes aún no desbloqueados.
        /// </summary>
        private void HideNonPartyNPCs()
        {
            var allPartyMembers = UnityEngine.Object.FindObjectsByType<NPCPartyMember>(
                UnityEngine.FindObjectsInactive.Exclude,
                UnityEngine.FindObjectsSortMode.None);

            foreach (var member in allPartyMembers)
            {
                if (member == null) continue;
                if (!HasMember(member))
                {
                    foreach (var r in member.GetComponentsInChildren<Renderer>(true))
                        r.enabled = false;
                    Log($"🙈 Ocultando renderers de {member.DisplayName}: no está en el party activo");
                }
            }
        }

        /// <summary>
        /// Reintenta restaurar los miembros pendientes.
        /// </summary>
        private void RetryPendingMembers()
        {
            if (_pendingMemberIds.Count == 0) return;
            
            Log($"🔄 === RETRY PENDIENTES ===  {_pendingMemberIds.Count} miembros: [{string.Join(", ", _pendingMemberIds)}]");
            
            // Log de NPCs registrados actualmente
            string[] registeredIds = System.Array.Empty<string>();
            if (NPCRegistry.Instance != null)
            {
                registeredIds = NPCRegistry.Instance.GetAllRegisteredIDs();
                Log($"📋 NPCs registrados ({registeredIds.Length}): [{string.Join(", ", registeredIds)}]");
            }
            else
            {
                LogWarning("⚠️ NPCRegistry.Instance es NULL - no se pueden buscar NPCs");
                return;
            }
            
            var stillPending = new List<string>();
            foreach (var id in _pendingMemberIds)
            {
                Log($"🔍 Buscando: '{id}'");
                NPCBehaviourManagerV2 npcManager = null;
                
                // 1. Buscar por ID exacto
                npcManager = NPCRegistry.Instance?.GetNPCByID(id);
                if (npcManager != null)
                {
                    Log($"  ✅ Encontrado por ID exacto");
                }
                
                // 2. Intentar sin guion bajo inicial
                if (npcManager == null && id.StartsWith("_"))
                {
                    var idSinGuion = id.Substring(1);
                    Log($"  🔍 Intentando sin guion: '{idSinGuion}'");
                    npcManager = NPCRegistry.Instance?.GetNPCByID(idSinGuion);
                    if (npcManager != null) Log($"  ✅ Encontrado sin guion");
                }
                
                // 3. Intentar añadiendo guion bajo inicial
                if (npcManager == null && !id.StartsWith("_"))
                {
                    var idConGuion = "_" + id;
                    Log($"  🔍 Intentando con guion: '{idConGuion}'");
                    npcManager = NPCRegistry.Instance?.GetNPCByID(idConGuion);
                    if (npcManager != null) Log($"  ✅ Encontrado con guion");
                }
                
                // 4. Buscar por coincidencia parcial en registrados
                if (npcManager == null && registeredIds.Length > 0)
                {
                    var idLower = id.ToLowerInvariant().Replace("_", "").Replace(" ", "");
                    Log($"  🔍 Buscando coincidencia parcial con: '{idLower}'");
                    foreach (var regId in registeredIds)
                    {
                        var regIdClean = regId.ToLowerInvariant().Replace("_", "").Replace(" ", "");
                        if (regIdClean.Contains(idLower) || idLower.Contains(regIdClean))
                        {
                            Log($"  🎯 Coincidencia parcial con: '{regId}'");
                            npcManager = NPCRegistry.Instance?.GetNPCByID(regId);
                            if (npcManager != null)
                            {
                                Log($"  ✅ Encontrado por coincidencia parcial");
                                break;
                            }
                        }
                    }
                }
                
                // 5. ÚLTIMO RECURSO: Buscar NPCPartyMember directamente en la escena
                if (npcManager == null)
                {
                    Log($"  🔍 ÚLTIMO RECURSO: Buscando en escena...");
                    var allPartyMembers = UnityEngine.Object.FindObjectsByType<NPCPartyMember>(UnityEngine.FindObjectsSortMode.None);
                    Log($"  📋 {allPartyMembers.Length} NPCPartyMember encontrados en escena");
                    
                    var idLower = id.ToLowerInvariant().Replace("_", "").Replace(" ", "");
                    foreach (var pm in allPartyMembers)
                    {
                        var goNameClean = pm.gameObject.name.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("(clone)", "");
                        var displayNameClean = pm.DisplayName != null ? pm.DisplayName.ToLowerInvariant().Replace("_", "").Replace(" ", "") : "";
                        
                        Log($"    Comparando con: GO='{pm.gameObject.name}' ({goNameClean}), DisplayName='{pm.DisplayName}' ({displayNameClean})");
                        
                        if (goNameClean.Contains(idLower) || idLower.Contains(goNameClean) ||
                            (pm.DisplayName != null && (displayNameClean.Contains(idLower) || idLower.Contains(displayNameClean))))
                        {
                            Log($"  ✅ Encontrado en escena: '{pm.gameObject.name}'");
                            if (!HasMember(pm))
                            {
                                pm.JoinParty();
                            }
                            npcManager = pm.NPCManager; // Marca como encontrado
                            break;
                        }
                    }
                }
                
                if (npcManager != null)
                {
                    var partyMember = npcManager.GetComponent<NPCPartyMember>();
                    if (partyMember != null && !HasMember(partyMember))
                    {
                        Log($"✅ Reintento exitoso: {partyMember.DisplayName} encontrado y unido al party");
                        partyMember.JoinParty();
                    }
                    else if (partyMember == null)
                    {
                        LogWarning($"  ⚠️ NPC encontrado pero sin NPCPartyMember component");
                        stillPending.Add(id);
                    }
                    else
                    {
                        Log($"  ℹ️ Ya está en el party");
                    }
                }
                else
                {
                    LogWarning($"  ❌ No encontrado - sigue pendiente");
                    stillPending.Add(id);
                }
            }
            _pendingMemberIds = stillPending;
            
            if (stillPending.Count > 0)
            {
                LogWarning($"⏳ Aún pendientes tras reintento: [{string.Join(", ", stillPending)}]");
            }
            else
            {
                Log($"✅ Todos los miembros pendientes restaurados exitosamente!");
            }
        }
        
        /// <summary>
        /// Handler para el evento de Unity SceneManager.sceneLoaded
        /// </summary>
        private void OnSceneLoadedHandler(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            OnSceneLoaded();
        }
        
        /// <summary>
        /// Llamado cuando se carga una nueva escena. Reintenta restaurar miembros pendientes.
        /// </summary>
        public void OnSceneLoaded()
        {
            // ✅ CRÍTICO: Limpiar miembros null (destruidos al cambiar de escena)
            // Esto ocurre cuando sales al menú principal y vuelves a cargar
            var nullCount = _members.RemoveAll(m => m == null);
            if (nullCount > 0)
            {
                Log($"🧹 Limpiados {nullCount} miembros null tras cambio de escena");
            }
            
            if (_pendingMemberIds.Count > 0)
            {
                Log($"🔄 Nueva escena cargada, reintentando restaurar {_pendingMemberIds.Count} miembros pendientes...");
                // Los reintentos se manejan automáticamente en Update()
            }
        }

        /// <summary>
        /// Llamado cuando un NPC enemigo entra en combate.
        /// Notifica a los compañeros para que ayuden al jugador.
        /// </summary>
        private void OnEnemyEnteredCombat(GameObject enemy)
        {
            if (enemy == null || _playerTransform == null)
            {
                Debug.Log($"[PlayerParty] ⚠️ OnEnemyEnteredCombat - enemy={enemy}, player={_playerTransform}, members={_members.Count}");
                return;
            }
            
            if (IsEmpty)
            {
                Debug.Log($"[PlayerParty] ⚠️ OnEnemyEnteredCombat({enemy.name}) - El party está VACÍO, no hay compañeros para notificar");
                return;
            }
            
            // Verificar que el enemigo NO sea uno de nuestros compañeros
            var enemyPartyMember = enemy.GetComponent<NPCPartyMember>();
            if (enemyPartyMember != null && HasMember(enemyPartyMember))
            {
                Debug.Log($"[PlayerParty] ⚠️ OnEnemyEnteredCombat - {enemy.name} es un compañero, ignorando");
                return;
            }
            
            // Verificar que el enemigo esté cerca del jugador (para asegurar que es combate relevante)
            float distanceToPlayer = Vector3.Distance(enemy.transform.position, _playerTransform.position);
            if (distanceToPlayer > 50f) // ✅ Aumentado a 50m para bosses grandes como Golem
            {
                Debug.Log($"[PlayerParty] ⚠️ OnEnemyEnteredCombat - {enemy.name} está demasiado lejos del jugador ({distanceToPlayer:F1}m > 50m)");
                return;
            }
            
            // ✅ ANTES de notificar combate, teletransportar compañeros lejanos cerca del jugador
            TeleportFarMembersForCombat();
            
            Debug.Log($"[PlayerParty] ⚔️⚔️⚔️ Enemigo '{enemy.name}' entró en combate cerca del jugador ({distanceToPlayer:F1}m) - Notificando a {_members.Count} compañeros");
            NotifyPlayerEnteredCombat(enemy.transform);
        }
        
        /// <summary>
        /// Teletransporta a los compañeros que estén lejos del jugador antes de un combate.
        /// Esto asegura que puedan asistir sin importar su combatAssistRange.
        /// </summary>
        private void TeleportFarMembersForCombat()
        {
            const float combatTeleportThreshold = 15f; // Si está más lejos de 15m, teletransportar
            var hiddenNpc = ActiveCharacterSwapper.Instance?.HiddenNpc;

            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (member == null) continue;

                // No operar sobre el NPC oculto (controlado por el jugador vía character swap)
                if (member == hiddenNpc) continue;

                // No teletransportar miembros en cinemática (escoltas, secuencias, etc.)
                if (member.NPCManager != null && member.NPCManager.IsInCinematic) continue;

                // Verificar si tiene autoJoinCombat activado
                if (member.PartyConfig != null && !member.PartyConfig.autoJoinPlayerCombat)
                    continue;

                float distance = Vector3.Distance(member.transform.position, _playerTransform.position);

                if (distance > combatTeleportThreshold)
                {
                    Debug.Log($"[PlayerParty] ⚡ Teletransportando a {member.DisplayName} para combate (estaba a {distance:F1}m)");
                    TeleportMemberToPlayer(member, i);
                }
            }
        }

        /// <summary>
        /// Llamado cuando un NPC enemigo sale del combate.
        /// </summary>
        private void OnEnemyExitedCombat(GameObject enemy)
        {
            if (enemy == null || IsEmpty) return;
            
            // Verificar si ya no hay más enemigos en combate
            if (ActiveCombatRegistry.Count == 0)
            {
                Log("🏳️ No quedan enemigos en combate");
                NotifyPlayerExitedCombat();
            }
        }
        
        /// <summary>
        /// Llamado cuando el jugador usa magia.
        /// Busca enemigos cercanos y pone a los compañeros en combate.
        /// </summary>
        private void OnPlayerAttacked()
        {
            if (IsEmpty || _playerTransform == null) return;
            
            // ✅ OPTIMIZACIÓN FASE 1: Usar ActiveCombatRegistry primero (más eficiente)
            Transform nearestEnemy = null;
            float nearestDistance = 25f;
            
            // MÉTODO 1: Buscar en ActiveCombatRegistry
            var combatEnemies = ActiveCombatRegistry.GetAllInCombat();
            if (combatEnemies != null && combatEnemies.Count > 0)
            {
                foreach (var enemy in combatEnemies)
                {
                    if (enemy == null) continue;
                    
                    var damageable = enemy.GetComponent<Damageable>();
                    if (damageable != null && damageable.Current <= 0) continue;
                    
                    float dist = Vector3.Distance(_playerTransform.position, enemy.transform.position);
                    if (dist < nearestDistance)
                    {
                        nearestDistance = dist;
                        nearestEnemy = enemy.transform;
                    }
                }
            }
            
            // MÉTODO 2: Fallback por Layer (solo si no encontró en Registry)
            if (nearestEnemy == null)
            {
                int enemyLayer = LayerMask.GetMask("Enemy", "Boss");
                int hitCount = Physics.OverlapSphereNonAlloc(_playerTransform.position, 25f, _enemySearchBuffer, enemyLayer); // ✅ OPTIMIZACIÓN: NonAlloc
                
                for (int i = 0; i < hitCount; i++)
                {
                    var col = _enemySearchBuffer[i];
                    if (col == null) continue;
                    
                    var damageable = col.GetComponent<Damageable>();
                    if (damageable != null && damageable.Current <= 0) continue;
                    
                    float dist = Vector3.Distance(_playerTransform.position, col.transform.position);
                    if (dist < nearestDistance)
                    {
                        nearestDistance = dist;
                        nearestEnemy = col.transform;
                    }
                }
            }
            
            if (nearestEnemy != null)
            {
                Debug.Log($"[PlayerParty] 🔔 Jugador atacó! Enemigo cercano: {nearestEnemy.name} a {nearestDistance:F1}m");
                
                // Teletransportar compañeros lejanos primero
                TeleportFarMembersForCombat();
                
                // Alertar a todos los compañeros
                NotifyPlayerEnteredCombat(nearestEnemy);
            }
        }

        private void CheckMemberDistances()
        {
            var hiddenNpc = ActiveCharacterSwapper.Instance?.HiddenNpc;
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (member == null || !member.IsActiveInParty) continue;

                // No operar sobre el NPC oculto (controlado por el jugador vía character swap)
                if (member == hiddenNpc) continue;

                // No teletransportar miembros en cinemática (escoltas, secuencias, etc.)
                if (member.NPCManager != null && member.NPCManager.IsInCinematic) continue;

                float distance = Vector3.Distance(member.transform.position, _playerTransform.position);

                // Usar distancia del config del miembro (o 15f por defecto, más agresivo que antes)
                float teleportThreshold = member.PartyConfig?.distanciaParaTeletransporte ?? 15f;

                if (distance > teleportThreshold)
                {
                    Log($"⚡ {member.DisplayName} demasiado lejos ({distance:F1}m > {teleportThreshold:F1}m), teletransportando...");
                    TeleportMemberToPlayer(member, i);
                }
            }

            // Teleportar el Will NPC si no está en _members (party lleno) pero existe en el mundo
            var willNpcSwapper = ActiveCharacterSwapper.Instance;
            var willNpc = willNpcSwapper?.WillNpcInstance;
            if (willNpc != null && !(willNpc.NPCManager?.IsInCinematic ?? false))
            {
                float willDist = Vector3.Distance(willNpc.transform.position, _playerTransform.position);
                float willThreshold = willNpc.PartyConfig?.distanciaParaTeletransporte ?? 15f;
                if (willDist > willThreshold)
                {
                    Log($"⚡ Will NPC demasiado lejos ({willDist:F1}m > {willThreshold:F1}m), teletransportando...");
                    willNpcSwapper.TeleportWillNpcToPlayer();
                }
            }
        }

        private void TeleportMemberToPlayer(NPCPartyMember member, int index)
        {
            if (_playerTransform == null || member == null) return;
            
            Vector3 targetPos = GetFormationPosition(index);
            
            // Usar Warp del NavMeshAgent si existe
            var agent = member.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.Warp(targetPos);
            }
            else
            {
                member.transform.position = targetPos;
            }
            
            member.OnTeleportedToPlayer();
        }

        private float CalculateFormationAngle(int index)
        {
            // Distribuir en abanico detrás del jugador: -60°, 0°, 60°, etc.
            int totalSlots = maxPartySize;
            float angleSpread = 120f; // Ángulo total del abanico
            float baseAngle = 180f; // Detrás del jugador
            
            if (totalSlots <= 1) return baseAngle;
            
            float step = angleSpread / (totalSlots - 1);
            return baseAngle - (angleSpread / 2f) + (step * index);
        }

        private float CalculateFormationDistance(int index)
        {
            // Más cerca que antes (era teleportRadius que podía ser 3f)
            float baseDistance = Mathf.Min(teleportRadius, 2f); // Máximo 2 metros
            return baseDistance + (index % 2 == 0 ? 0 : 0.3f); // Menor variación
        }

        private void Log(string message)
        {
            if (debugMode) Debug.Log($"[PlayerParty] {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"[PlayerParty] ⚠️ {message}");
        }
        #endregion

        #region Save/Load Support
        /// <summary>
        /// Sincroniza los IDs del party actual con el preset para garantizar persistencia.
        /// </summary>
        private void SyncPartyToPreset()
        {
            try
            {
                Debug.Log($"[PlayerParty] 🔄 SyncPartyToPreset() llamado - _members.Count = {_members.Count}");
                
                var profile = GameBootService.Profile;
                if (profile == null)
                {
                    Debug.LogWarning("[PlayerParty] ⚠️ GameBootService.Profile es null, no se puede sincronizar party");
                    return;
                }

                var preset = profile.GetActivePresetResolved();
                if (preset == null)
                {
                    Debug.LogWarning("[PlayerParty] ⚠️ No hay preset activo, no se puede sincronizar party");
                    return;
                }

                Debug.Log($"[PlayerParty] 📋 Preset encontrado: '{preset.name}', llamando a GetMemberIdsForSave()...");
                var memberIds = GetMemberIdsForSave(allowPresetFallbackWhenEmpty: false);
                Debug.Log($"[PlayerParty] 📋 GetMemberIdsForSave() retornó {memberIds.Count} IDs: [{string.Join(", ", memberIds)}]");

                preset.partyMemberIds = memberIds;

                // NOTA: activeCharacterSlot NO se actualiza aquí para no sobreescribir
                // el slot guardado durante el OnProfileReady, antes de que PartyControlManager
                // lo lea. Se sincroniza en UpdateRuntimePresetFromCurrentState() antes de guardar.

                Debug.Log($"[PlayerParty] ✅ Party sincronizado con preset '{preset.name}': {preset.partyMemberIds.Count} miembros [{string.Join(", ", memberIds)}]");
            }
            catch (System.Exception ex)
            {
                LogWarning($"Error sincronizando party con preset: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Obtiene los IDs de los miembros actuales para guardar.
        /// </summary>
        public List<string> GetMemberIdsForSave(bool allowPresetFallbackWhenEmpty = true)
        {
            Debug.Log($"[PlayerParty] 📊 GetMemberIdsForSave() - _members.Count = {_members.Count}");
            
            // ✅ CRÍTICO: Si _members está vacío o solo tiene nulls, leer desde el preset actual
            // Esto ocurre cuando se guarda justo después de un cambio de escena (ej: al salir al menú)
            int validMembersCount = _members.Count(m => m != null);
            Debug.Log($"[PlayerParty] 📊 Valid members (non-null): {validMembersCount}");
            
            if (validMembersCount == 0)
            {
                if (!allowPresetFallbackWhenEmpty)
                {
                    Debug.Log("[PlayerParty] ℹ️ GetMemberIdsForSave sin fallback: retornando lista vacía");
                    return new List<string>();
                }

                Debug.LogWarning("[PlayerParty] ⚠️ _members está vacío o solo tiene nulls - Leyendo desde preset actual");
                
                var profile = GameBootService.Profile;
                if (profile != null)
                {
                    var preset = profile.GetActivePresetResolved();
                    if (preset != null && preset.partyMemberIds != null && preset.partyMemberIds.Count > 0)
                    {
                        Debug.Log($"[PlayerParty] ✅ Usando {preset.partyMemberIds.Count} IDs desde preset: [{string.Join(", ", preset.partyMemberIds)}]");
                        return new List<string>(preset.partyMemberIds);
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerParty] ⚠️ Preset no tiene IDs (preset={preset != null}, partyMemberIds={(preset?.partyMemberIds != null ? preset.partyMemberIds.Count.ToString() : "null")})");
                    }
                }
                else
                {
                    Debug.LogWarning("[PlayerParty] ⚠️ GameBootService.Profile es null");
                }
                
                Debug.LogWarning("[PlayerParty] ⚠️ No se encontraron IDs en el preset - Retornando lista vacía");
                return new List<string>();
            }
            
            Debug.Log($"[PlayerParty] ✅ Extrayendo IDs de {validMembersCount} miembros válidos...");
            
            var result = new List<string>();
            
            foreach (var member in _members)
            {
                if (member == null)
                {
                    Debug.LogWarning("[PlayerParty] ⚠️ Miembro null en la lista - omitido");
                    continue;
                }
                
                // PRIORIZAR EL NOMBRE COMO ID: En el sistema de party, el ID narrativo genera duplicados (Estela_Config_XXX).
                // Es preferible usar el nombre del GameObject (Estela, Oliver, etc) que se enlaza más fácil en el Load.
                string persistenceId = member.gameObject.name.Replace("(Clone)", "").Trim();
                
                // Validar si el NPCRegistry tiene su ID bajo un nombre explícito distinto
                var npcManager = member.NPCManager;
                if (npcManager != null && NPCRegistry.HasInstance)
                {
                   // Try to find if there is a direct registration for this NPC to use its correct ID
                   var allIds = NPCRegistry.Instance.GetAllRegisteredIDs();
                   foreach(var id in allIds) 
                   {
                       var registeredNpc = NPCRegistry.Instance.GetNPCByID(id);
                       if (registeredNpc != null && registeredNpc == npcManager) 
                       {
                           // Only use it if it's not a narrative config ID (which causes duplicates)
                           if (!id.StartsWith("NPC_InteractiveNarrative_Config_")) 
                           {
                               persistenceId = id;
                               break;
                           }
                       }
                   }
                }
                
                result.Add(persistenceId);
                Debug.Log($"[PlayerParty] ✅ Miembro '{member.name}' guardado con ID '{persistenceId}'");
            }
            
            return result;
        }

        /// <summary>
        /// Restaura miembros por sus IDs (llamar después de cargar escena).
        /// </summary>
        public void RestoreMembersFromIds(List<string> memberIds)
        {
            if (memberIds == null) return;
            
            Log($"Restaurando {memberIds.Count} miembros del equipo...");
            
            // Limpiar pendientes antes de procesar (se re-añadirán si fallan)
            _pendingMemberIds.Clear();
            
            // Log de todos los NPCs registrados actualmente
            string[] registeredIds = System.Array.Empty<string>();
            if (NPCRegistry.Instance != null)
            {
                registeredIds = NPCRegistry.Instance.GetAllRegisteredIDs();
                Log($"NPCs registrados en la escena ({registeredIds.Length}): [{string.Join(", ", registeredIds)}]");
            }
            else
            {
                LogWarning("NPCRegistry.Instance es null!");
                // Si el registro no existe, todos los IDs quedan pendientes
                _pendingMemberIds.AddRange(memberIds);
                return;
            }
            
            foreach (var id in memberIds)
            {
                // Ignorar explícitamente IDs narrativos antiguos o corruptos que se guardaron antes de este parche
                if (id.StartsWith("NPC_InteractiveNarrative_Config_"))
                {
                    Log($"⚠️ Ignorando ID narrativo del party '{id}'. Tratando de extraer el nombre real...");
                    string possibleName = "Estela";
                    if (id.ToLower().Contains("oliver")) possibleName = "Oliver";
                    else if (id.ToLower().Contains("victoria")) possibleName = "Victoria";
                    else if (id.ToLower().Contains("liam")) possibleName = "Liam";
                    
                    Log($"⚠️ Reemplazando ID '{id}' -> '{possibleName}'");
                    var newId = possibleName;
                    
                    // Solo intentar restaurar si no está ya restaurado
                    bool alreadyFound = _members.Any(m => m != null && m.DisplayName != null && m.DisplayName.Equals(possibleName, StringComparison.OrdinalIgnoreCase));
                    if (alreadyFound) continue;
                    
                    Log($"Buscando NPC (parcheado) con ID: '{newId}'");
                    FindAndRestore(newId, registeredIds);
                }
                else 
                {
                    Log($"Buscando NPC con ID: '{id}'");
                    FindAndRestore(id, registeredIds);
                }
            }
            
            Log($"Restauración completada. Miembros activos: {_members.Count}, Pendientes: {_pendingMemberIds.Count}");
        }

        private void FindAndRestore(string id, string[] registeredIds)
        {
            // 1. Buscar NPC en el registro por ID exacto
            var npcManager = NPCRegistry.Instance?.GetNPCByID(id);

            // 2. FALLBACK: Si no se encontró, intentar sin guion bajo inicial (por si fue guardado con nombre de GO)
            if (npcManager == null && id.StartsWith("_"))
            {
                var idSinGuion = id.Substring(1);
                Log($"  → Intentando sin guion bajo: '{idSinGuion}'");
                npcManager = NPCRegistry.Instance?.GetNPCByID(idSinGuion);
            }

            // 3. FALLBACK: Buscar por nombre similar en los registrados
            if (npcManager == null)
            {
                var idLower = id.ToLowerInvariant().TrimStart('_');
                foreach (var regId in registeredIds)
                {
                    if (regId.ToLowerInvariant().Contains(idLower) || idLower.Contains(regId.ToLowerInvariant()))
                    {
                        Log($"  → Encontrado por coincidencia parcial: '{regId}'");
                        npcManager = NPCRegistry.Instance?.GetNPCByID(regId);
                        break;
                    }
                }
            }

            if (npcManager != null)
            {
                Log($"✅ NPC encontrado: {npcManager.name}");
                var partyMember = npcManager.GetComponent<NPCPartyMember>();
                if (partyMember != null && !HasMember(partyMember))
                {
                    Log($"Uniendo {partyMember.DisplayName} al party...");
                    partyMember.JoinParty(); // Esto llamará a AddMember internamente (y activará el GO si está inactivo)
                }
                else if (partyMember == null)
                {
                    LogWarning($"NPC {npcManager.name} no tiene componente NPCPartyMember");
                    _pendingMemberIds.Add(id);
                }
                else
                {
                    Log($"NPC {partyMember.DisplayName} ya está en el party");
                }
            }
            else
            {
                LogWarning($"❌ No se encontró NPC con ID: '{id}' en el registro - marcado como pendiente");
                _pendingMemberIds.Add(id);
            }
        }

        #endregion
    }
}