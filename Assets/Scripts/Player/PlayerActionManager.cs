using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ActionMode { Default, Carrying, Casting, Cinematic, Stunned }
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
}

/// Manager central: prioridades, gates y efectos secundarios por modo
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Animator))]
public class PlayerActionManager : MonoBehaviour
{
    [Header("Animator (no tocamos weights globales)")]
    [SerializeField] private int upperBodyLayer = 1;
    [SerializeField] private string upperIdleState = "UpperIdle";

    [Header("Reglas por modo (inspector)")]
    [Tooltip("Declara aquí todo lo que debe bloquearse/deshabilitarse por modo. Orden no importa.")]
    [SerializeField] private ModeRule[] rules;

    public event Action<ActionMode> OnTopModeChanged;

    // --- Internals ---
    private readonly List<ActionMode> _stack = new() { ActionMode.Default };
    private readonly Dictionary<ActionMode, HashSet<PlayerAbility>> _blockedByMode = new();
    private readonly Dictionary<ActionMode, ModeRule> _ruleByMode = new();
    private Animator _anim;

    void Awake()
    {
        _anim = GetComponent<Animator>();

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
    }

    public ActionMode Top => _stack[^1];

    public void PushMode(ActionMode mode)
    {
        if (Top == mode) return;
        _stack.Add(mode);
        ApplyTopMode();
    }

    public void PopMode(ActionMode mode)
    {
        for (int i = _stack.Count - 1; i >= 0; --i)
        {
            if (_stack[i] == mode) { _stack.RemoveAt(i); break; }
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
                return false;
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
                foreach (var c in rule.disableComponents) if (c) c.enabled = false;

            // Deshabilitar acciones Input System
            if (rule.disableInputActions != null)
                foreach (var a in rule.disableInputActions)
                    if (a && a.action != null && a.action.enabled) a.action.Disable();

            // Anim: UpperIdle limpio (sin tocar weights)
            if (rule.forceUpperIdle && upperBodyLayer > 0 && !string.IsNullOrEmpty(upperIdleState))
                _anim.CrossFade(upperIdleState, 0.1f, upperBodyLayer);
        }

        OnTopModeChanged?.Invoke(top);
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
}
