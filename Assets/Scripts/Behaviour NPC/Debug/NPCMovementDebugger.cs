using UnityEngine;
using UnityEngine.AI;
using Game.NPC;

/// <summary>
/// Script de diagnóstico temporal para identificar por qué un NPC se está moviendo.
/// USAR SOLO PARA DEBUG - Quitar después de identificar el problema.
/// </summary>
public class NPCMovementDebugger : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private bool logEveryFrame = false;
    [SerializeField] private float logInterval = 1f;
    
    private NavMeshAgent _agent;
    private NPCBehaviourManagerV2 _manager;
    private NPCSimpleAnimator _animator;
    private NPCPartyMember _partyMember;
    private float _lastLogTime;
    private string _lastStateName = "";
    private int _stateChangeCount = 0;
    
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _manager = GetComponent<NPCBehaviourManagerV2>();
        _animator = GetComponent<NPCSimpleAnimator>();
        
        if (_agent == null)
        {
            Debug.LogError($"[MovementDebugger:{name}] ❌ No tiene NavMeshAgent!");
            enabled = false;
            return;
        }
        
        Debug.Log($"[MovementDebugger:{name}] ✅ Iniciado - Monitoreando movimiento");
    }
    
    void Update()
    {
        if (logEveryFrame)
        {
            LogStatus();
        }
        else if (Time.time - _lastLogTime >= logInterval)
        {
            _lastLogTime = Time.time;
            LogStatus();
        }
        
        // Detección de movimiento no deseado
        if (_manager != null && _manager.Brain != null)
        {
            var currentState = _manager.Brain.CurrentState;
            
            // Si está en Idle pero se está moviendo, ALERTA
            if (currentState != null && currentState.StateName == "Idle")
            {
                if (_agent.velocity.sqrMagnitude > 0.01f || _agent.hasPath)
                {
                    Debug.LogWarning($"[MovementDebugger:{name}] ⚠️ ¡MOVIMIENTO NO DESEADO EN IDLE!\n" +
                                   $"Velocity: {_agent.velocity}\n" +
                                   $"HasPath: {_agent.hasPath}\n" +
                                   $"IsStopped: {_agent.isStopped}\n" +
                                   $"RemainingDistance: {_agent.remainingDistance}");
                }
            }
        }
    }
    
    void LogStatus()
    {
        string stateInfo = "Unknown";
        string stateType = "Unknown";
        if (_manager != null && _manager.Brain != null && _manager.Brain.CurrentState != null)
        {
            var currentState = _manager.Brain.CurrentState;
            stateInfo = currentState.StateName;
            stateType = currentState.GetType().Name;
            
            // Detectar cambio de estado
            if (stateInfo != _lastStateName)
            {
                _stateChangeCount++;
                Debug.LogWarning($"[MovementDebugger:{name}] 🔄 CAMBIO DE ESTADO #{_stateChangeCount}: '{_lastStateName}' → '{stateInfo}' (tipo: {stateType})");
                _lastStateName = stateInfo;
            }
        }
        
        string partyInfo = "No está en party";
        if (_partyMember != null)
        {
            partyInfo = _partyMember.IsInParty ? $"✅ En party (índice {_partyMember.PartyIndex})" : "❌ NO en party";
        }
        
        string combatBrainInfo = "N/A";
        var combatBrain = GetComponent<NPCCombatBrain>();
        if (combatBrain != null)
        {
            // Usa reflection para acceder al campo privado _isActive
            var field = typeof(NPCCombatBrain).GetField("_isActive", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                bool isActive = (bool)field.GetValue(combatBrain);
                combatBrainInfo = isActive ? "ACTIVO" : "Inactivo";
            }
        }
        
        float animSpeed = 0f;
        if (_animator != null)
        {
            // Obtener InputMagnitude del animator
            var unityAnimator = GetComponent<Animator>();
            if (unityAnimator != null)
            {
                animSpeed = unityAnimator.GetFloat("InputMagnitude");
            }
        }
        
        Debug.Log($"[MovementDebugger:{name}] 📊 Estado:\n" +
                 $"  FSM State: {stateInfo} (tipo: {stateType})\n" +
                 $"  Party Status: {partyInfo}\n" +
                 $"  CombatBrain: {combatBrainInfo}\n" +
                 $"  Agent.velocity: {_agent.velocity} (mag: {_agent.velocity.magnitude:F3})\n" +
                 $"  Agent.isStopped: {_agent.isStopped}\n" +
                 $"  Agent.hasPath: {_agent.hasPath}\n" +
                 $"  Agent.pathPending: {_agent.pathPending}\n" +
                 $"  Agent.remainingDistance: {_agent.remainingDistance:F2}\n" +
                 $"  Animator InputMagnitude: {animSpeed:F3}\n" +
                 $"  Position: {transform.position}\n" +
                 $"  State Changes: {_stateChangeCount}");
    }
    
    void OnDrawGizmos()
    {
        if (_agent == null || !Application.isPlaying) return;
        
        // Dibujar velocidad
        if (_agent.velocity.magnitude > 0.01f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up, _agent.velocity);
        }
        
        // Dibujar path
        if (_agent.hasPath && _agent.path.corners.Length > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _agent.path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(_agent.path.corners[i], _agent.path.corners[i + 1]);
            }
        }
    }
}
