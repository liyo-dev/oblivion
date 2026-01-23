using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC.Modules;
using Game.NPC.Common;

namespace Game.NPC
{
    /// <summary>
    /// Sistema de Equipo de Combate para NPCs.
    /// Permite que múltiples NPCs luchen juntos como un equipo coordinado.
    /// 
    /// Uso: Añadir este componente al NPC líder del equipo y arrastrar los compañeros.
    /// Cuando el líder detecte al jugador, todo el equipo entrará en combate.
    /// </summary>
    [DisallowMultipleComponent]
    public class NPCCombatTeam : MonoBehaviour
    {
        #region Configuration
        
        [Header("Equipo de Combate")]
        [Tooltip("NPCs que forman parte de este equipo. El NPC con este componente es el líder.")]
        [SerializeField] private List<NPCBehaviourManagerV2> teamMembers = new List<NPCBehaviourManagerV2>();
    
    [Tooltip("Distancia máxima a la que deben reagruparse los compañeros antes de iniciar combate.")]
    [SerializeField] private float regroupDistance = 3f;
    
    [Tooltip("Distancia alrededor del líder donde se posicionarán los compañeros.")]
    [SerializeField] private float formationRadius = 2.5f;
    
    [Tooltip("Tiempo máximo de espera para reagruparse (segundos).")]
    [SerializeField] private float maxRegroupTime = 5f;
    
    [Tooltip("Si está marcado, espera a que todos lleguen antes de iniciar el diálogo/combate.")]
    [SerializeField] private bool waitForAllMembers = true;
    
    [Header("Resurrección Grupal")]
    [Tooltip("Si está marcado, todos los miembros del equipo se levantan cuando el combate termina (si aplica).")]
    [SerializeField] private bool resurrectAsTeam = true;
    
    [Tooltip("Delay entre la resurrección de cada miembro (para efecto visual).")]
    [SerializeField] private float resurrectDelay = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    #endregion
    
    #region State
    
    private NPCBehaviourManagerV2 _leaderManager;
    private bool _isTeamInCombat;
    private bool _isRegrouping;
    private bool _alertIconsShown; // ✅ FIX: Flag para evitar mostrar iconos múltiples veces
    private int _defeatedCount;
    private List<NPCBehaviourManagerV2> _allMembers = new List<NPCBehaviourManagerV2>(); // Líder + compañeros
    private Transform _currentTarget; // ✅ FIX: Referencia al jugador para calcular formación
    
    // Evento para notificar cuando todo el equipo ha sido derrotado
    public System.Action OnTeamDefeated;
    
    // Evento para notificar cuando el equipo inicia combate
    public System.Action OnTeamCombatStarted;
    
    // Estado de diálogo post-derrota
    public bool IsPostDefeatDialogueFinished { get; private set; }
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// Indica si el equipo está actualmente en combate.
    /// </summary>
    public bool IsTeamInCombat => _isTeamInCombat;
    
    /// <summary>
    /// Indica si el equipo está reagrupándose.
    /// </summary>
    public bool IsRegrouping => _isRegrouping;
    
    /// <summary>
    /// Número de miembros del equipo (incluyendo líder).
    /// </summary>
    public int TeamSize => _allMembers.Count;
    
    /// <summary>
    /// Número de miembros derrotados.
    /// </summary>
    public int DefeatedCount => _defeatedCount;
    
    /// <summary>
    /// Indica si todo el equipo ha sido derrotado.
    /// </summary>
    public bool IsTeamDefeated => _defeatedCount >= _allMembers.Count;
    
    /// <summary>
    /// Lista de todos los miembros del equipo (solo lectura).
    /// </summary>
    public IReadOnlyList<NPCBehaviourManagerV2> AllMembers => _allMembers;
    
    /// <summary>
    /// El líder del equipo (el NPC que tiene este componente).
    /// </summary>
    public NPCBehaviourManagerV2 Leader => _leaderManager;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Awake()
    {
        _leaderManager = GetComponent<NPCBehaviourManagerV2>();
        
        if (_leaderManager == null)
        {
            Debug.LogError($"[NPCCombatTeam] {name}: No se encontró NPCBehaviourManagerV2 en este GameObject!");
            enabled = false;
            return;
        }
        
        // Construir lista completa (líder + compañeros)
        _allMembers.Clear();
        _allMembers.Add(_leaderManager);
        
        foreach (var member in teamMembers)
        {
            if (member != null && member != _leaderManager && !_allMembers.Contains(member))
            {
                _allMembers.Add(member);
            }
        }
        
        if (showDebugLogs)
        {
            // Debug.Log($"[NPCCombatTeam] {name}: Equipo inicializado con {_allMembers.Count} miembros");
        }
    }
    
    void Start()
    {
        // Suscribirse a eventos de detección del líder
        if (_leaderManager != null)
        {
            // Registrar este equipo en cada miembro para que sepan que pertenecen a un equipo
            foreach (var member in _allMembers)
            {
                var teamLink = member.gameObject.GetComponent<NPCTeamMember>();
                if (teamLink == null)
                {
                    teamLink = member.gameObject.AddComponent<NPCTeamMember>();
                }
                teamLink.SetTeam(this, member == _leaderManager);
            }
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Llamado cuando el líder o cualquier miembro detecta al jugador.
    /// Inicia el proceso de reagrupación y combate grupal.
    /// </summary>
    public void OnPlayerDetected(Transform player)
    {
        // ✅ FIX: Usar _alertIconsShown como guard adicional para evitar bucles
        // _isRegrouping se pone false muy rápido, necesitamos un flag persistente
        if (_isTeamInCombat || _isRegrouping || _alertIconsShown) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: ¡Jugador detectado! Iniciando reagrupación del equipo...");
        }
        
        // ✅ Mostrar iconos de alerta en TODOS los miembros
        ShowTeamAlertIcons();
        
        StartCoroutine(Co_RegroupAndStartCombat(player));
    }
    
    /// <summary>
    /// Muestra el icono de alerta en todos los miembros del equipo.
    /// </summary>
    public void ShowTeamAlertIcons()
    {
        // ✅ FIX: Evitar mostrar iconos múltiples veces
        if (_alertIconsShown) return;
        _alertIconsShown = true;
        
        foreach (var member in _allMembers)
        {
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            
            var config = member.Configuration?.combatConfig;
            if (config != null && config.alertIconPrefab != null)
            {
                var iconController = member.GetComponent<NPCAlertIconController>();
                if (iconController == null) iconController = member.gameObject.AddComponent<NPCAlertIconController>();
                
                // Configurar altura del icono (desde los pies del NPC)
                if (config.alertIconHeight > 0)
                {
                    iconController.SetIconHeight(config.alertIconHeight);
                }
                
                iconController.ShowAlertIcon(config.alertIconPrefab, config.alertIconDuration);
            }
        }
    }
    
    /// <summary>
    /// Notifica que el diálogo post-derrota ha terminado.
    /// Llamado por el líder desde NPCCombatLifecycleHandler.
    /// </summary>
    public void NotifyPostDefeatDialogueFinished()
    {
        if (IsPostDefeatDialogueFinished)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[NPCCombatTeam] {name}: NotifyPostDefeatDialogueFinished llamado múltiples veces - ignorando");
            }
            return;
        }
        
        IsPostDefeatDialogueFinished = true;
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Diálogo post-derrota finalizado. Notificando al equipo.");
        }
    }
    
    /// <summary>
    /// Fuerza la finalización del diálogo si el sistema se atasca.
    /// Útil para debugging o situaciones de emergencia.
    /// </summary>
    public void ForceFinishPostDefeatDialogue()
    {
        if (!IsPostDefeatDialogueFinished)
        {
            Debug.LogWarning($"[NPCCombatTeam] {name}: ⚠️ FORZANDO finalización de diálogo post-derrota");
            IsPostDefeatDialogueFinished = true;
            
            // Cancelar dizzy en todos los miembros para que procedan
            foreach (var member in _allMembers)
            {
                if (member == null) continue;
                var lifecycle = member.GetComponent<NPCCombatLifecycleHandler>();
                if (lifecycle != null)
                {
                    lifecycle.CancelDizzySequence();
                }
            }
        }
    }
    
    /// <summary>
    /// Resetea el estado del equipo (útil para resurrección).
    /// </summary>
    public void ResetTeamState()
    {
        IsPostDefeatDialogueFinished = false;
        _defeatedCount = 0;
        _isTeamInCombat = false;
        _isRegrouping = false;
        _alertIconsShown = false; // ✅ FIX: Resetear flag de iconos
        
        // ✅ FIX: Resetear el flag de notificación en todos los miembros
        foreach (var member in _allMembers)
        {
            if (member == null) continue;
            var teamMember = member.GetComponent<NPCTeamMember>();
            if (teamMember != null)
            {
                teamMember.ResetNotificationFlag();
            }
        }
    }
    
    /// <summary>
    /// Notifica que un miembro del equipo ha sido derrotado.
    /// </summary>
    public void OnMemberDefeated(NPCBehaviourManagerV2 member)
    {
        if (!_allMembers.Contains(member)) return;
        
        _defeatedCount++;
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Miembro {member.name} derrotado ({_defeatedCount}/{_allMembers.Count})");
        }
        
        // Verificar si todo el equipo ha sido derrotado
        if (IsTeamDefeated)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[NPCCombatTeam] {name}: ¡Todo el equipo ha sido derrotado!");
            }
            
            _isTeamInCombat = false;
            OnTeamDefeated?.Invoke();
        }
    }
    
    /// <summary>
    /// Resucita a todos los miembros del equipo (si está configurado).
    /// </summary>
    public void ResurrectTeam()
    {
        if (!resurrectAsTeam) return;
        
        StartCoroutine(Co_ResurrectTeam());
    }
    
    /// <summary>
    /// Fuerza a todos los miembros a entrar en combate inmediatamente (sin reagrupación).
    /// </summary>
    public void ForceTeamCombat(Transform player)
    {
        if (_isTeamInCombat) return;
        
        _isTeamInCombat = true;
        _defeatedCount = 0;
        IsPostDefeatDialogueFinished = false; // Resetear flag
        
        foreach (var member in _allMembers)
        {
            if (member != null && member.gameObject.activeInHierarchy)
            {
                // Forzar entrada en combate
                member.ForceEnterCombat(player);
            }
        }
        
        OnTeamCombatStarted?.Invoke();
    }
    
    /// <summary>
    /// Obtiene las posiciones de formación frente al jugador.
    /// Los NPCs se colocan en semicírculo frente al jugador para la escena de confrontación.
    /// </summary>
    public Vector3 GetFormationPosition(int memberIndex)
    {
        if (memberIndex < 0 || _allMembers.Count <= 0) return transform.position;
        if (_currentTarget == null) return transform.position;
        
        // ✅ FIX: Calcular posición relativa al JUGADOR, no al líder
        // Esto asegura que todos los NPCs se agrupen frente al jugador para el diálogo
        
        Vector3 playerPos = _currentTarget.position;
        Vector3 dirFromPlayer = (transform.position - playerPos).normalized;
        dirFromPlayer.y = 0;
        
        // Si no hay dirección válida, usar forward del jugador
        if (dirFromPlayer.sqrMagnitude < 0.01f)
        {
            dirFromPlayer = _currentTarget.forward;
        }
        
        // Posición base: frente al jugador a distancia de formación
        float baseDistance = formationRadius + 2f; // Distancia base del jugador
        Vector3 formationCenter = playerPos + dirFromPlayer * baseDistance;
        
        // Si solo hay un miembro (el líder), ponerlo en el centro
        if (_allMembers.Count == 1 || memberIndex == 0)
        {
            return formationCenter;
        }
        
        // Distribuir en arco frente al jugador
        // El líder (index 0) va al centro, los demás a los lados
        float totalMembers = _allMembers.Count;
        float spreadAngle = 60f; // Ángulo total del arco (grados)
        
        // Calcular ángulo para este miembro
        // Index 0 = centro, index 1 = izquierda, index 2 = derecha, etc.
        float angleOffset;
        if (memberIndex == 0)
        {
            angleOffset = 0f;
        }
        else
        {
            // Alternar izquierda/derecha
            int sideIndex = (memberIndex + 1) / 2;
            bool isLeft = (memberIndex % 2) == 1;
            angleOffset = sideIndex * (spreadAngle / (totalMembers - 1)) * (isLeft ? -1f : 1f);
        }
        
        // Rotar la dirección por el ángulo calculado
        Vector3 memberDir = Quaternion.Euler(0, angleOffset, 0) * dirFromPlayer;
        Vector3 memberPos = playerPos + memberDir * baseDistance;
        
        return memberPos;
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Corrutina que reagrupa al equipo y luego inicia el combate.
    /// </summary>
    private IEnumerator Co_RegroupAndStartCombat(Transform player)
    {
        _isRegrouping = true;
        _currentTarget = player; // ✅ FIX: Guardar referencia para GetFormationPosition
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Reagrupando equipo...");
        }
        
        // ✅ FIX: Mover TODOS los miembros (incluyendo el líder) a posiciones de formación
        // Esto asegura que todos estén cerca del jugador para el diálogo cinematográfico
        List<Coroutine> moveCoroutines = new List<Coroutine>();
        
        for (int i = 0; i < _allMembers.Count; i++)
        {
            var member = _allMembers[i];
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            
            Vector3 targetPos = GetFormationPosition(i);
            moveCoroutines.Add(StartCoroutine(Co_MoveMemberToPosition(member, targetPos)));
        }
        
        // Esperar a que todos lleguen (o timeout)
        if (waitForAllMembers && moveCoroutines.Count > 0)
        {
            float startTime = Time.time;
            bool allArrived = false;
            
            while (!allArrived && (Time.time - startTime) < maxRegroupTime)
            {
                allArrived = true;
                
                // ✅ FIX: Verificar TODOS los miembros (desde index 0)
                for (int i = 0; i < _allMembers.Count; i++)
                {
                    var member = _allMembers[i];
                    if (member == null || !member.gameObject.activeInHierarchy) continue;
                    
                    Vector3 targetPos = GetFormationPosition(i);
                    float dist = Vector3.Distance(member.transform.position, targetPos);
                    
                    if (dist > regroupDistance)
                    {
                        allArrived = false;
                        break;
                    }
                }
                
                yield return null;
            }
            
            if (showDebugLogs)
            {
                if (allArrived)
                    Debug.Log($"[NPCCombatTeam] {name}: ¡Equipo reagrupado!");
                else
                    Debug.Log($"[NPCCombatTeam] {name}: Timeout de reagrupación - iniciando combate de todas formas");
            }
        }
        
        // Pequeña pausa dramática
        yield return new WaitForSeconds(0.3f);
        
        // Hacer que todos miren al jugador
        foreach (var member in _allMembers)
        {
            if (member == null || !member.gameObject.activeInHierarchy) continue;
            
            Vector3 dir = (player.position - member.transform.position);
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                member.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
        
        _isRegrouping = false;
        
        // NO iniciamos combate aquí - dejamos que el sistema narrativo del líder maneje el flujo
        // El diálogo se ejecutará después de la alerta, y al terminar el diálogo,
        // la cadena narrativa activará el combate con EnterCombatAfterDialogue
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Equipo reagrupado y listo - El sistema narrativo del líder manejará el diálogo y combate");
        }
        
        // Notificar que el equipo está listo (el sistema narrativo continuará el flujo)
        OnTeamCombatStarted?.Invoke();
    }
    
    /// <summary>
    /// Mueve un miembro a su posición de formación.
    /// </summary>
    private IEnumerator Co_MoveMemberToPosition(NPCBehaviourManagerV2 member, Vector3 targetPos)
    {
        var agent = member.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) yield break;
        
        // Verificar que la posición está en NavMesh
        if (!UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            yield break;
        }
        
        // ✅ CRÍTICO: Asegurar que el agent mueva y rote el transform
        // Esto puede estar deshabilitado si el NPC tiene NPCCombatBrain previamente inicializado
        agent.updatePosition = true;
        
        // ✅ FIX: Desactivar updateRotation para evitar conflicto con NPCSimpleAnimator
        // NPCSimpleAnimator se encargará de rotar el NPC hacia la dirección de movimiento
        agent.updateRotation = false;

        agent.isStopped = false;
        
        // ✅ Verificar si hay un NPCCombatBrain activo que pueda interferir
        var combatBrain = member.GetComponent<NPCCombatBrain>();
        if (combatBrain != null && combatBrain.enabled)
        {
            // Desactivar temporalmente el combat brain durante el reagrupamiento
            combatBrain.enabled = false;
            if (showDebugLogs)
            {
                Debug.Log($"[NPCCombatTeam] ⚠️ {member.name} tiene NPCCombatBrain activo - desactivando temporalmente para reagrupamiento");
            }
        }
        
        agent.SetDestination(navHit.position);
        
        // Obtener el animador - usar SimpleAnimator del manager que es más fiable
        var animator = member.SimpleAnimator;
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] 🚶 {member.name} moviéndose a formación desde {member.transform.position} hacia {navHit.position}, Agent speed: {agent.speed}");
        }
        
        // Esperar a que llegue
        float timeout = maxRegroupTime;
        while (timeout > 0 && agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.pathPending && agent.remainingDistance <= regroupDistance)
            {
                break;
            }
            
            // ✅ Actualizar animación según velocidad real cada frame
            if (animator != null)
            {
                float currentSpeed = agent.velocity.magnitude;
                if (currentSpeed > 0.1f)
                {
                    // Normalizar la velocidad: dividir por la velocidad máxima del agent
                    float normalizedSpeed = currentSpeed / Mathf.Max(agent.speed, 1f);
                    animator.SetMovementSpeed(normalizedSpeed, 0.1f);
                }
                else
                {
                    // Si está parado, establecer velocidad a 0
                    animator.SetMovementSpeed(0f, 0.1f);
                }
            }
            
            timeout -= Time.deltaTime;
            yield return null;
        }
        
        // Detener
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        
        if (animator != null)
        {
            animator.SetMovementSpeed(0, 0.1f);
        }
        
        // ✅ Reactivar combat brain si fue desactivado
        if (combatBrain != null && !combatBrain.enabled)
        {
            combatBrain.enabled = true;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] ✅ {member.name} llegó a formación");
        }
    }
    
    /// <summary>
    /// Resucita a todos los miembros del equipo con delay.
    /// </summary>
    private IEnumerator Co_ResurrectTeam()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Resucitando equipo...");
        }
        
        ResetTeamState();
        
        foreach (var member in _allMembers)
        {
            if (member == null) continue;
            
            // Llamar al método de resurrección del NPC
            var lifecycleHandler = member.GetComponent<NPCCombatLifecycleHandler>();
            if (lifecycleHandler != null)
            {
                lifecycleHandler.Resurrect();
            }
            
            yield return new WaitForSeconds(resurrectDelay);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: ¡Equipo resucitado!");
        }
    }
    
    #endregion
    
    #region Editor
    
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Dibujar radio de formación
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, formationRadius);
        
        // Dibujar posiciones de formación
        if (_allMembers == null || _allMembers.Count == 0)
        {
            // En editor sin play, simular posiciones
            int simulatedCount = teamMembers.Count + 1;
            for (int i = 1; i < simulatedCount; i++)
            {
                float angle = (360f / (simulatedCount - 1)) * (i - 1) + 180f;
                Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * formationRadius;
                Vector3 pos = transform.position + offset;
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pos, 0.3f);
                Gizmos.DrawLine(transform.position, pos);
            }
        }
        else
        {
            for (int i = 1; i < _allMembers.Count; i++)
            {
                Vector3 pos = GetFormationPosition(i);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pos, 0.3f);
                Gizmos.DrawLine(transform.position, pos);
            }
        }
        
        // Líneas a los compañeros
        Gizmos.color = Color.green;
        foreach (var member in teamMembers)
        {
            if (member != null)
            {
                Gizmos.DrawLine(transform.position + Vector3.up, member.transform.position + Vector3.up);
            }
        }
    }
#endif
    
    #endregion
}
}
