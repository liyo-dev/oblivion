using UnityEngine;
using UnityEngine.AI;
using Game.NPC;

/// <summary>
/// Script de emergencia para forzar la detención del NavMeshAgent en IdleState.
/// USAR SOLO SI EL PROBLEMA PERSISTE después de los otros fixes.
/// </summary>
public class NavMeshAgentForceStop : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private bool disableAgentInIdle = true;
    [SerializeField] private bool debugMode = true;
    
    private NavMeshAgent _agent;
    private NPCBehaviourManagerV2 _manager;
    private bool _lastWasIdle;
    
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _manager = GetComponent<NPCBehaviourManagerV2>();
        
        if (_agent == null)
        {
            Debug.LogError($"[ForceStop:{name}] ❌ No tiene NavMeshAgent!");
            enabled = false;
            return;
        }
        
        if (_manager == null)
        {
            Debug.LogError($"[ForceStop:{name}] ❌ No tiene NPCBehaviourManagerV2!");
            enabled = false;
            return;
        }
        
        Debug.Log($"[ForceStop:{name}] ✅ Iniciado - Modo: {(disableAgentInIdle ? "DESACTIVAR" : "FORZAR STOP")}");
    }
    
    void LateUpdate()
    {
        if (_agent == null || _manager == null) return;
        if (_manager.Brain == null || _manager.Brain.CurrentState == null) return;
        
        bool isIdle = _manager.Brain.CurrentState.StateName == "Idle";
        
        if (isIdle)
        {
            // Detectar transición a Idle
            if (!_lastWasIdle && debugMode)
            {
                Debug.Log($"[ForceStop:{name}] 🔄 Entró en IdleState");
            }
            
            if (disableAgentInIdle)
            {
                // MODO EXTREMO: Desactivar el NavMeshAgent completamente
                if (_agent.enabled)
                {
                    if (debugMode)
                        Debug.LogWarning($"[ForceStop:{name}] ⚠️ DESACTIVANDO NavMeshAgent en IdleState");
                    
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                    _agent.ResetPath();
                    _agent.enabled = false;
                }
            }
            else
            {
                // MODO MODERADO: Solo forzar detención
                if (_agent.enabled && _agent.isOnNavMesh)
                {
                    bool needsFix = false;
                    
                    if (!_agent.isStopped)
                    {
                        if (debugMode)
                            Debug.LogWarning($"[ForceStop:{name}] ⚠️ isStopped era FALSE, corrigiendo");
                        _agent.isStopped = true;
                        needsFix = true;
                    }
                    
                    if (_agent.velocity.sqrMagnitude > 0.01f)
                    {
                        if (debugMode)
                            Debug.LogWarning($"[ForceStop:{name}] ⚠️ Velocidad residual: {_agent.velocity.magnitude:F1}, limpiando");
                        _agent.velocity = Vector3.zero;
                        needsFix = true;
                    }
                    
                    if (_agent.hasPath)
                    {
                        if (debugMode)
                            Debug.LogWarning($"[ForceStop:{name}] ⚠️ Tiene path activo, reseteando");
                        _agent.ResetPath();
                        needsFix = true;
                    }
                    
                    if (needsFix && debugMode)
                    {
                        Debug.LogError($"[ForceStop:{name}] 🔥 ALGUIEN ESTÁ REACTIVANDO EL AGENTE! Verificar otros scripts.");
                    }
                }
            }
        }
        else
        {
            // Detectar transición fuera de Idle
            if (_lastWasIdle && debugMode)
            {
                Debug.Log($"[ForceStop:{name}] 🔄 Salió de IdleState → {_manager.Brain.CurrentState.StateName}");
            }
            
            // Reactivar el agente si estaba desactivado
            if (disableAgentInIdle && !_agent.enabled)
            {
                if (debugMode)
                    Debug.Log($"[ForceStop:{name}] ✅ Reactivando NavMeshAgent para {_manager.Brain.CurrentState.StateName}");
                _agent.enabled = true;
            }
        }
        
        _lastWasIdle = isIdle;
    }
    
    void OnDrawGizmosSelected()
    {
        if (_agent == null || !Application.isPlaying) return;
        
        // Dibujar estado
        if (_manager != null && _manager.Brain != null && _manager.Brain.CurrentState != null)
        {
            bool isIdle = _manager.Brain.CurrentState.StateName == "Idle";
            Gizmos.color = isIdle ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);
        }
        
        // Dibujar velocidad
        if (_agent.velocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up, _agent.velocity.normalized * 2f);
        }
    }
}
