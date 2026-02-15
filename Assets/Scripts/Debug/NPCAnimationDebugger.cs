using UnityEngine;
using Game.NPC;

/// <summary>
/// Componente de diagnóstico para depurar problemas de animación en NPCs.
/// Añade este componente al NPC que tiene problemas (ej: Liam) y observa los logs.
/// </summary>
public class NPCAnimationDebugger : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private bool enableLogging = true;
    [SerializeField] private float logInterval = 2f;
    
    private Animator _animator;
    private NPCSimpleAnimator _simpleAnimator;
    private NPCBehaviourManagerV2 _behaviourManager;
    
    private float _lastLogTime;
    private string _lastStateName;
    
    void Awake()
    {
        _animator = GetComponent<Animator>();
        _simpleAnimator = GetComponent<NPCSimpleAnimator>();
        _behaviourManager = GetComponent<NPCBehaviourManagerV2>();
    }
    
    void Update()
    {
        if (!enableLogging || _animator == null) return;
        
        if (Time.time - _lastLogTime < logInterval) return;
        _lastLogTime = Time.time;
        
        // Obtener estado actual del Animator
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        string currentStateName = GetStateName(stateInfo);
        
        // Solo loggear si cambió el estado
        if (currentStateName != _lastStateName)
        {
            string fsmState = "N/A";
            if (_behaviourManager?.Brain?.CurrentState != null)
            {
                fsmState = _behaviourManager.Brain.CurrentState.StateName;
            }
            
            Debug.Log($"[NPCAnimDebug:{name}] 🎭 Animator: '{currentStateName}' | FSM: '{fsmState}' | IsInParty: {IsInParty()}");
            _lastStateName = currentStateName;
        }
    }
    
    private string GetStateName(AnimatorStateInfo stateInfo)
    {
        // Intentar identificar el estado por su hash (no es perfecto pero ayuda)
        // Los hashes son específicos del Animator Controller
        
        // Estados comunes que conocemos
        if (_animator.GetCurrentAnimatorClipInfo(0).Length > 0)
        {
            var clipInfo = _animator.GetCurrentAnimatorClipInfo(0)[0];
            return clipInfo.clip != null ? clipInfo.clip.name : $"Hash:{stateInfo.shortNameHash}";
        }
        
        return $"Hash:{stateInfo.shortNameHash}";
    }
    
    private bool IsInParty()
    {
        var partyMember = GetComponent<Game.NPC.NPCPartyMember>();
        return partyMember != null && partyMember.IsInParty;
    }
    
    [ContextMenu("Log Current State Now")]
    public void LogCurrentStateNow()
    {
        if (_animator == null)
        {
            Debug.LogWarning($"[NPCAnimDebug:{name}] No hay Animator");
            return;
        }
        
        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        string currentStateName = GetStateName(stateInfo);
        
        string fsmState = "N/A";
        if (_behaviourManager?.Brain?.CurrentState != null)
        {
            fsmState = _behaviourManager.Brain.CurrentState.StateName;
        }
        
        Debug.Log($"[NPCAnimDebug:{name}] 📊 ESTADO ACTUAL:");
        Debug.Log($"  - Animator State: '{currentStateName}'");
        Debug.Log($"  - FSM State: '{fsmState}'");
        Debug.Log($"  - IsInParty: {IsInParty()}");
        Debug.Log($"  - Animator Speed: {_animator.speed}");
        Debug.Log($"  - Normalized Time: {stateInfo.normalizedTime}");
        Debug.Log($"  - Is Looping: {stateInfo.loop}");
    }
}
