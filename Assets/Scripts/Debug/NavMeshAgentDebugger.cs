using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Script de debugging para diagnosticar problemas con NavMeshAgent.
/// Añadir temporalmente a un NPC para ver información detallada en tiempo real.
/// 
/// USO:
/// 1. Añade este componente al NPC que tiene problemas
/// 2. Activa "Show Debug Gizmos" en el Inspector
/// 3. Reproduce el juego y observa la Scene View y Console
/// 4. Verás líneas de colores indicando el path y estado del agent
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshAgentDebugger : MonoBehaviour
{
    [Header("Debug Options")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private float logInterval = 1f; // Segundos entre logs
    
    [Header("Gizmo Colors")]
    [SerializeField] private Color pathColor = Color.green;
    [SerializeField] private Color velocityColor = Color.blue;
    [SerializeField] private Color destinationColor = Color.red;
    
    private NavMeshAgent _agent;
    private float _lastLogTime;
    
    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }
    
    void Update()
    {
        if (!logEveryFrame && Time.time - _lastLogTime >= logInterval)
        {
            LogAgentState();
            _lastLogTime = Time.time;
        }
        else if (logEveryFrame)
        {
            LogAgentState();
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();
        
        if (_agent == null || !_agent.isOnNavMesh) return;
        
        // Dibujar destino
        Gizmos.color = destinationColor;
        Gizmos.DrawWireSphere(_agent.destination, 0.5f);
        Gizmos.DrawLine(transform.position, _agent.destination);
        
        // Dibujar path
        if (_agent.path != null && _agent.path.corners.Length > 1)
        {
            Gizmos.color = pathColor;
            for (int i = 0; i < _agent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(_agent.path.corners[i], _agent.path.corners[i + 1]);
                Gizmos.DrawWireSphere(_agent.path.corners[i], 0.2f);
            }
            Gizmos.DrawWireSphere(_agent.path.corners[_agent.path.corners.Length - 1], 0.2f);
        }
        
        // Dibujar velocidad
        if (_agent.velocity.magnitude > 0.01f)
        {
            Gizmos.color = velocityColor;
            Gizmos.DrawRay(transform.position, _agent.velocity);
        }
        
        // Dibujar círculo en el NPC
        Gizmos.color = _agent.velocity.magnitude > 0.01f ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
    
    private void LogAgentState()
    {
        if (_agent == null) return;
        
        string status = GetAgentStatusEmoji();
        
        Debug.Log($"{status} NavMeshAgent Debug [{name}]:\n" +
                 $"  Enabled: {_agent.enabled}\n" +
                 $"  Is On NavMesh: {_agent.isOnNavMesh}\n" +
                 $"  Is Stopped: {_agent.isStopped}\n" +
                 $"  Speed: {_agent.speed:F2}\n" +
                 $"  Velocity: {_agent.velocity.magnitude:F2} (x:{_agent.velocity.x:F2}, y:{_agent.velocity.y:F2}, z:{_agent.velocity.z:F2})\n" +
                 $"  Destination: {_agent.destination}\n" +
                 $"  Remaining Distance: {_agent.remainingDistance:F2}\n" +
                 $"  Stopping Distance: {_agent.stoppingDistance:F2}\n" +
                 $"  Path Pending: {_agent.pathPending}\n" +
                 $"  Path Status: {(_agent.path != null ? _agent.path.status.ToString() : "NULL")}\n" +
                 $"  Update Rotation: {_agent.updateRotation}\n" +
                 $"  Angular Speed: {_agent.angularSpeed:F1}°/s\n" +
                 $"  Has Path: {_agent.hasPath}\n" +
                 $"  Position: {transform.position}");
    }
    
    private string GetAgentStatusEmoji()
    {
        if (!_agent.isOnNavMesh) return "❌";
        if (_agent.isStopped) return "🛑";
        if (_agent.pathPending) return "⏳";
        if (_agent.velocity.magnitude < 0.01f && _agent.remainingDistance > _agent.stoppingDistance + 0.5f) return "⚠️";
        if (_agent.velocity.magnitude > 0.01f) return "✅";
        return "🔵";
    }
    
    /// <summary>
    /// Llama a este método desde la consola o desde otro script para hacer un diagnóstico completo
    /// </summary>
    public void FullDiagnostic()
    {
        Debug.Log($"=== DIAGNÓSTICO COMPLETO: {name} ===\n");
        
        // 1. NavMeshAgent
        if (_agent == null)
        {
            Debug.LogError("❌ No se encontró NavMeshAgent!");
            return;
        }
        
        Debug.Log($"✅ NavMeshAgent encontrado");
        
        // 2. NavMesh
        if (!_agent.isOnNavMesh)
        {
            Debug.LogError($"❌ El NPC NO está sobre NavMesh! Posición: {transform.position}");
            
            // Intentar encontrar punto más cercano en NavMesh
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                Debug.Log($"💡 Punto más cercano en NavMesh: {hit.position} (distancia: {Vector3.Distance(transform.position, hit.position):F2}m)");
            }
            else
            {
                Debug.LogError("❌ No hay NavMesh en 5m alrededor!");
            }
        }
        else
        {
            Debug.Log($"✅ NPC está sobre NavMesh");
        }
        
        // 3. Path
        if (_agent.hasPath)
        {
            Debug.Log($"✅ Tiene path - Status: {_agent.path.status} - Corners: {_agent.path.corners.Length}");
        }
        else
        {
            Debug.LogWarning($"⚠️ No tiene path activo");
        }
        
        // 4. Velocidad
        if (_agent.velocity.magnitude > 0.01f)
        {
            Debug.Log($"✅ Moviéndose - Velocity: {_agent.velocity.magnitude:F2}");
        }
        else
        {
            if (!_agent.isStopped && _agent.remainingDistance > _agent.stoppingDistance + 0.5f)
            {
                Debug.LogError($"❌ PROBLEMA: Velocity = 0 pero debería moverse (remainingDistance: {_agent.remainingDistance:F2})");
            }
            else
            {
                Debug.Log($"✅ Parado correctamente (isStopped: {_agent.isStopped}, remainingDistance: {_agent.remainingDistance:F2})");
            }
        }
        
        // 5. Animator
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            Debug.Log($"✅ Animator encontrado - Speed: {animator.speed} - InputMagnitude: {animator.GetFloat("InputMagnitude"):F2}");
        }
        else
        {
            Debug.LogWarning($"⚠️ No se encontró Animator");
        }
        
        Debug.Log($"=== FIN DIAGNÓSTICO ===\n");
    }
}
