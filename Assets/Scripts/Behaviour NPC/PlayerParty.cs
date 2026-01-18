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
        [SerializeField] private bool debugMode = false;
        
        [Header("Teleport Settings")]
        [Tooltip("Radio alrededor del jugador donde reaparecerán los compañeros")]
        [SerializeField] private float teleportRadius = 3f;
        
        [Tooltip("Distancia mínima del jugador para el teleport")]
        [SerializeField] private float minTeleportDistance = 2f;
        #endregion

        #region State
        private readonly List<NPCPartyMember> _members = new();
        private Transform _playerTransform;
        private bool _isInitialized;
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
            if (_instance != null && _instance != this)
            {
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
            
            // Subscribirse a OnProfileReady para restaurar el party al cargar partida
            GameBootService.OnProfileReady += OnProfileReady;
            
            // Intentar obtener referencia inicial
            ResolvePlayerReference();
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
                PlayerService.OnPlayerUnregistered -= OnPlayerUnregistered;
                ActiveCombatRegistry.OnNPCEnteredCombat -= OnEnemyEnteredCombat;
                ActiveCombatRegistry.OnNPCExitedCombat -= OnEnemyExitedCombat;
                GameBootService.OnProfileReady -= OnProfileReady;
                _instance = null;
            }
        }

        void Update()
        {
            if (!_isInitialized || _playerTransform == null) return;
            
            // Verificar distancias y teleportar si es necesario
            CheckMemberDistances();
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
            
            Log($"✨ {member.DisplayName} se unió al equipo [{MemberCount}/{maxPartySize}]");
            
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
                m.NPCManager?.Configuration?.narrativeConfig?.narrativeID == narrativeId);
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
            
            // Calcular posición en formación (semicírculo detrás del jugador)
            float angle = CalculateFormationAngle(memberIndex);
            float distance = CalculateFormationDistance(memberIndex);
            
            Vector3 offset = Quaternion.Euler(0, angle, 0) * (-_playerTransform.forward * distance);
            Vector3 targetPos = _playerTransform.position + offset;
            
            // Validar en NavMesh
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                return hit.position;
            }
            
            return _playerTransform.position + (-_playerTransform.forward * distance);
        }

        /// <summary>
        /// Fuerza a todos los miembros a teletransportarse cerca del jugador.
        /// </summary>
        public void TeleportAllMembersToPlayer()
        {
            if (_playerTransform == null) return;
            
            for (int i = 0; i < _members.Count; i++)
            {
                TeleportMemberToPlayer(_members[i], i);
            }
        }

        /// <summary>
        /// Notifica a todos los compañeros que el jugador entró en combate.
        /// </summary>
        public void NotifyPlayerEnteredCombat(Transform enemy)
        {
            foreach (var member in _members)
            {
                if (member.PartyConfig != null && member.PartyConfig.autoJoinPlayerCombat)
                {
                    member.OnPlayerEnteredCombat(enemy);
                }
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
            if (profile == null) return;
            
            var preset = profile.GetActivePresetResolved();
            if (preset == null || preset.partyMemberIds == null || preset.partyMemberIds.Count == 0)
            {
                Log("No hay miembros de party para restaurar");
                return;
            }
            
            Log($"🔄 Restaurando {preset.partyMemberIds.Count} miembros del party...");
            
            // Usar coroutine para esperar a que los NPCs se registren en la escena
            StartCoroutine(RestorePartyDelayed(preset.partyMemberIds));
        }

        private System.Collections.IEnumerator RestorePartyDelayed(List<string> memberIds)
        {
            // Esperar unos frames para que los NPCs se inicialicen y registren
            yield return null;
            yield return null;
            yield return new UnityEngine.WaitForSeconds(0.5f);
            
            RestoreMembersFromIds(memberIds);
        }

        /// <summary>
        /// Llamado cuando un NPC enemigo entra en combate.
        /// Notifica a los compañeros para que ayuden al jugador.
        /// </summary>
        private void OnEnemyEnteredCombat(GameObject enemy)
        {
            if (enemy == null || _playerTransform == null || IsEmpty) return;
            
            // Verificar que el enemigo NO sea uno de nuestros compañeros
            var enemyPartyMember = enemy.GetComponent<NPCPartyMember>();
            if (enemyPartyMember != null && HasMember(enemyPartyMember)) return;
            
            // Verificar que el enemigo esté cerca del jugador (para asegurar que es combate relevante)
            float distanceToPlayer = Vector3.Distance(enemy.transform.position, _playerTransform.position);
            if (distanceToPlayer > 20f) return; // Ignorar combates lejanos
            
            Log($"⚔️ Enemigo '{enemy.name}' entró en combate cerca del jugador");
            NotifyPlayerEnteredCombat(enemy.transform);
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

        private void CheckMemberDistances()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                if (member == null || !member.IsActiveInParty) continue;
                
                float distance = Vector3.Distance(member.transform.position, _playerTransform.position);
                float teleportThreshold = member.PartyConfig?.teleportDistance ?? 25f;
                
                if (distance > teleportThreshold)
                {
                    Log($"⚡ {member.DisplayName} demasiado lejos ({distance:F1}m), teletransportando...");
                    TeleportMemberToPlayer(member, i);
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
            // Variar ligeramente la distancia para que no estén en línea
            float baseDistance = teleportRadius;
            return baseDistance + (index % 2 == 0 ? 0 : 0.5f);
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
        /// Obtiene los IDs de los miembros actuales para guardar.
        /// </summary>
        public List<string> GetMemberIdsForSave()
        {
            return _members
                .Where(m => m?.NPCManager?.Configuration?.narrativeConfig != null)
                .Select(m => m.NPCManager.Configuration.narrativeConfig.narrativeID)
                .ToList();
        }

        /// <summary>
        /// Restaura miembros por sus IDs (llamar después de cargar escena).
        /// </summary>
        public void RestoreMembersFromIds(List<string> memberIds)
        {
            if (memberIds == null) return;
            
            Log($"Restaurando {memberIds.Count} miembros del equipo...");
            
            foreach (var id in memberIds)
            {
                // Buscar NPC en el registro
                var npcManager = NPCRegistry.Instance?.GetNPCByID(id);
                if (npcManager != null)
                {
                    var partyMember = npcManager.GetComponent<NPCPartyMember>();
                    if (partyMember != null && !HasMember(partyMember))
                    {
                        partyMember.JoinParty(); // Esto llamará a AddMember internamente
                    }
                }
            }
        }
        #endregion
    }
}

