using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ActionMode { Default, Carrying, Casting, Cinematic, Stunned, Swimming }
public enum PlayerAbility { Move, Jump, Sprint, Roll, Attack, Magic, Interact, Carry, Aim }

/// Reglas declarativas que se aplican cuando un modo es el tope de la pila
[Serializable]
public sealed class ModeRule
{
    public ActionMode mode = ActionMode.Default;

    [Header("Bloqueos de habilidades (lógica)")]
    public PlayerAbility[] blockedAbilities;

    [Header("Desactivar componentes mientras este modo esté arriba")]
    public Behaviour[] disableComponents;

    [Header("Deshabilitar acciones del Input System")]
    public InputActionReference[] disableInputActions;

    [Header("Animación")]
    public bool forceUpperIdle = false;                 // Forzar clip idle de UpperBody
    [Range(0f, 1f)] public float upperBodyWeight = -1f; // -1 = no tocar, 0-1 = forzar peso
}

/// Manager central: prioridades, gates y efectos secundarios por modo
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Animator))]
public class PlayerActionManager : MonoBehaviour, IActionValidator
{
    [Header("Animator")]
    [SerializeField] private int upperBodyLayer = 1;
    [SerializeField] private string upperIdleState = "UpperIdle";

    [Header("Reglas por modo (inspector)")]
    [Tooltip("Declara aquí todo lo que debe bloquearse/deshabilitarse por modo. Orden no importa.")]
    [SerializeField] private ModeRule[] rules;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public event Action<ActionMode> OnTopModeChanged;

    // --- Internals ---
    private readonly List<ActionMode> _stack = new() { ActionMode.Default };
    private readonly Dictionary<ActionMode, HashSet<PlayerAbility>> _blockedByMode = new();
    private readonly Dictionary<ActionMode, ModeRule> _ruleByMode = new();
    private Animator _anim;
    private float _originalUpperWeight = 0f;

    void Awake()
    {
        _anim = GetComponent<Animator>();

        // Guardar peso original del UpperBody
        if (upperBodyLayer > 0 && _anim.layerCount > upperBodyLayer)
            _originalUpperWeight = _anim.GetLayerWeight(upperBodyLayer);

        // Preconstruir diccionarios para O(1) en runtime
        if (rules != null)
        {
            foreach (var r in rules)
            {
                if (r == null) continue;
                _ruleByMode[r.mode] = r;

                var set = new HashSet<PlayerAbility>();
                if (r.blockedAbilities != null)
                    foreach (var a in r.blockedAbilities) set.Add(a);
                _blockedByMode[r.mode] = set;
            }
        }

        // Garantiza entrada Default
        if (!_blockedByMode.ContainsKey(ActionMode.Default))
            _blockedByMode[ActionMode.Default] = new HashSet<PlayerAbility>();

        // GARANTIZAR bloqueos críticos para modo Carrying (independiente del Inspector)
        if (!_blockedByMode.ContainsKey(ActionMode.Carrying))
            _blockedByMode[ActionMode.Carrying] = new HashSet<PlayerAbility>();
        
        // Asegurar que Carrying SIEMPRE bloquee estas habilidades
        _blockedByMode[ActionMode.Carrying].Add(PlayerAbility.Magic);
        _blockedByMode[ActionMode.Carrying].Add(PlayerAbility.Attack);
        _blockedByMode[ActionMode.Carrying].Add(PlayerAbility.Roll);
        _blockedByMode[ActionMode.Carrying].Add(PlayerAbility.Jump);

        // GARANTIZAR bloqueos críticos para modo Stunned (independiente del Inspector)
        if (!_blockedByMode.ContainsKey(ActionMode.Stunned))
            _blockedByMode[ActionMode.Stunned] = new HashSet<PlayerAbility>();
        
        // Asegurar que Stunned SIEMPRE bloquee TODAS las habilidades (usado para limpiar inputs)
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Move);
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Jump);
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Sprint);
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Roll);
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Attack);
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Magic);
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Interact);
        _blockedByMode[ActionMode.Stunned].Add(PlayerAbility.Carry);

        // GARANTIZAR bloqueos críticos para modo Swimming (independiente del Inspector)
        if (!_blockedByMode.ContainsKey(ActionMode.Swimming))
            _blockedByMode[ActionMode.Swimming] = new HashSet<PlayerAbility>();
        
        // Asegurar que Swimming SIEMPRE bloquee estas habilidades
        _blockedByMode[ActionMode.Swimming].Add(PlayerAbility.Jump);
        _blockedByMode[ActionMode.Swimming].Add(PlayerAbility.Sprint);
        _blockedByMode[ActionMode.Swimming].Add(PlayerAbility.Roll);
        _blockedByMode[ActionMode.Swimming].Add(PlayerAbility.Attack);
        _blockedByMode[ActionMode.Swimming].Add(PlayerAbility.Magic);
        _blockedByMode[ActionMode.Swimming].Add(PlayerAbility.Carry);
        _blockedByMode[ActionMode.Swimming].Add(PlayerAbility.Interact);

        if (debugLogs) Debug.Log($"[PlayerActionManager] Inicializado con {rules?.Length ?? 0} reglas");
    }

    public ActionMode Top => _stack[^1];

    public void PushMode(ActionMode mode)
    {
        if (Top == mode) return;
        _stack.Add(mode);
        if (debugLogs) Debug.Log($"[PlayerActionManager] Push: {mode} (stack size: {_stack.Count})");
        ApplyTopMode();
    }

    public void PopMode(ActionMode mode)
    {
        for (int i = _stack.Count - 1; i >= 0; --i)
        {
            if (_stack[i] == mode) 
            { 
                _stack.RemoveAt(i); 
                if (debugLogs) Debug.Log($"[PlayerActionManager] Pop: {mode} (stack size: {_stack.Count})");
                break; 
            }
        }
        if (_stack.Count == 0) _stack.Add(ActionMode.Default);
        ApplyTopMode();
    }

    public bool CanUse(PlayerAbility ability)
    {
        // Recorre de arriba a abajo (prioridad)
        for (int i = _stack.Count - 1; i >= 0; --i)
        {
            var m = _stack[i];
            if (_blockedByMode.TryGetValue(m, out var set) && set.Contains(ability))
            {
                if (debugLogs) Debug.Log($"[PlayerActionManager] ❌ {ability} bloqueado por modo {m}");
                return false;
            }
        }
        return true;
    }

    // --- Core: aplicar efectos secundarios del modo superior ---
    private void ApplyTopMode()
    {
        // Primero, re-habilitar todo (seguridad) y luego aplicar deshabilitaciones del top.
        ReenableAll();

        var top = Top;
        if (_ruleByMode.TryGetValue(top, out var rule))
        {
            // Desactivar componentes
            if (rule.disableComponents != null)
                foreach (var c in rule.disableComponents) 
                    if (c) 
                    {
                        c.enabled = false;
                        if (debugLogs) Debug.Log($"[PlayerActionManager] Deshabilitando componente: {c.GetType().Name}");
                    }

            // Deshabilitar acciones Input System
            if (rule.disableInputActions != null)
                foreach (var a in rule.disableInputActions)
                    if (a && a.action != null && a.action.enabled) 
                    {
                        a.action.Disable();
                        if (debugLogs) Debug.Log($"[PlayerActionManager] Deshabilitando input: {a.action.name}");
                    }

            // Control de peso de UpperBody
            if (rule.upperBodyWeight >= 0f && upperBodyLayer > 0 && _anim.layerCount > upperBodyLayer)
            {
                _anim.SetLayerWeight(upperBodyLayer, rule.upperBodyWeight);
                if (debugLogs) Debug.Log($"[PlayerActionManager] UpperBody weight: {rule.upperBodyWeight}");
            }

            // Anim: UpperIdle limpio
            if (rule.forceUpperIdle && upperBodyLayer > 0 && !string.IsNullOrEmpty(upperIdleState))
                _anim.CrossFade(upperIdleState, 0.1f, upperBodyLayer);
        }
        else
        {
            // Modo Default: restaurar peso original
            if (top == ActionMode.Default && upperBodyLayer > 0 && _anim.layerCount > upperBodyLayer)
                _anim.SetLayerWeight(upperBodyLayer, _originalUpperWeight);
        }

        OnTopModeChanged?.Invoke(top);
        if (debugLogs) Debug.Log($"[PlayerActionManager] ✅ Modo activo: {top}");
    }

    private void ReenableAll()
    {
        // Rehabilita TODO lo que cualquier regla pudiera haber tocado.
        // Esto se ejecuta solo cuando cambia el modo → coste despreciable.
        if (rules == null) return;

        foreach (var r in rules)
        {
            if (r == null) continue;

            if (r.disableComponents != null)
                foreach (var c in r.disableComponents) if (c) c.enabled = true;

            if (r.disableInputActions != null)
                foreach (var a in r.disableInputActions)
                    if (a && a.action != null && !a.action.enabled) a.action.Enable();
        }
    }

    // Por si el GameObject se desactiva en mitad de un modo
    void OnDisable()
    {
        ReenableAll();
        _stack.Clear();
        _stack.Add(ActionMode.Default);
    }

    // API pública para consultar estado
    public bool IsInMode(ActionMode mode) => _stack.Contains(mode);
    public int StackDepth => _stack.Count;

    // Implementación de IActionValidator para compatibilidad con otros namespaces
    public bool CanJump() => CanUse(PlayerAbility.Jump);
    public bool CanSprint() => CanUse(PlayerAbility.Sprint);
    public bool CanAttack() => CanUse(PlayerAbility.Attack);
    public bool CanCastMagic() => CanUse(PlayerAbility.Magic);
    public bool CanInteract() => CanUse(PlayerAbility.Interact);
}
