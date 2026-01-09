using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC.Modules;

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
    private int _defeatedCount;
    private List<NPCBehaviourManagerV2> _allMembers = new List<NPCBehaviourManagerV2>(); // Líder + compañeros
    
    // Evento para notificar cuando todo el equipo ha sido derrotado
    public System.Action OnTeamDefeated;
    
    // Evento para notificar cuando el equipo inicia combate
    public System.Action OnTeamCombatStarted;
    
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
        if (_isTeamInCombat || _isRegrouping) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: ¡Jugador detectado! Iniciando reagrupación del equipo...");
        }
        
        StartCoroutine(Co_RegroupAndStartCombat(player));
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
    /// Obtiene las posiciones de formación alrededor del líder.
    /// </summary>
    public Vector3 GetFormationPosition(int memberIndex)
    {
        if (memberIndex <= 0 || _allMembers.Count <= 1) return transform.position;
        
        // Distribuir en círculo alrededor del líder
        float angle = (360f / (_allMembers.Count - 1)) * (memberIndex - 1);
        angle += 180f; // Empezar detrás del líder
        
        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * formationRadius;
        return transform.position + offset;
    }
    
    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Corrutina que reagrupa al equipo y luego inicia el combate.
    /// </summary>
    private IEnumerator Co_RegroupAndStartCombat(Transform player)
    {
        _isRegrouping = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"[NPCCombatTeam] {name}: Reagrupando equipo...");
        }
        
        // Detener al líder y hacer que mire al jugador
        var leaderAgent = _leaderManager.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (leaderAgent != null && leaderAgent.enabled && leaderAgent.isOnNavMesh)
        {
            leaderAgent.isStopped = true;
        }
        
        // Hacer que el líder mire al jugador
        Vector3 lookDir = (player.position - transform.position);
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }
        
        // Ordenar a los compañeros que se acerquen
        List<Coroutine> moveCoroutines = new List<Coroutine>();
        
        for (int i = 1; i < _allMembers.Count; i++)
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
                
                for (int i = 1; i < _allMembers.Count; i++)
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
        agent.updateRotation = true;
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
        
        _defeatedCount = 0;
        _isTeamInCombat = false;
        
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

