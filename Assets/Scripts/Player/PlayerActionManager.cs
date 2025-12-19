using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    // --- Runtime: permisos globales aplicables desde presets u otros sistemas ---
    //private bool _allowPhysical = true; // ataques físicos
    private bool _allowSwim = false;     // nadar
    private bool _allowJump = false;     // saltar
    private bool _allowClimb = false;    // trepar
    private bool _allowMagic = false;   // lanzar magia
    private bool _allowFly = false;      // volar

    // API pública para que otros sistemas (p.ej. PlayerPresetService) apliquen permisos
    public void ApplyAbilities(PlayerAbilities abilities)
    {
        if (abilities == null)
        {
            Debug.LogWarning("[PlayerActionManager] ApplyAbilities called with null abilities!");
            return;
        }
        // _allowPhysical = abilities.physical;
        _allowSwim = abilities.swim;
        _allowJump = abilities.jump;
        _allowClimb = abilities.climb;
        _allowFly = abilities.fly;
        // 'magic' read with reflection as fallback (some analyzers/assemblies may not resolve the member)
        try
        {
            var t = abilities.GetType();
            var f = t.GetField("magic");
            if (f != null && f.FieldType == typeof(bool))
            {
                _allowMagic = (bool)f.GetValue(abilities);
            }
            else
            {
                // por compatibilidad, mantener true si no existe
                _allowMagic = true;
            }
        }
        catch
        {
            _allowMagic = true;
        }

        if (debugLogs) Debug.Log($"[PlayerActionManager] Abilities applied: Swim={_allowSwim} Jump={_allowJump} Climb={_allowClimb} Fly={_allowFly} Magic={_allowMagic}");
        else Debug.Log($"[PlayerActionManager] Abilities applied: Fly={_allowFly}");
    }

    // Opcional: getters públicos si otros sistemas necesitan consultarlos
    //public bool AllowPhysical => _allowPhysical;
    public bool AllowSwim => _allowSwim;
    public bool AllowJump => _allowJump;
    public bool AllowClimb => _allowClimb;
    public bool AllowMagic => _allowMagic;
    public bool AllowFly => _allowFly;

    public event Action<ActionMode> OnTopModeChanged;

    // --- Internals ---
    private readonly List<ActionMode> _stack = new() { ActionMode.Default };
    private readonly Dictionary<ActionMode, HashSet<PlayerAbility>> _blockedByMode = new();
    private readonly Dictionary<ActionMode, ModeRule> _ruleByMode = new();
    private Animator _anim;
    private float _originalUpperWeight = 0f;
    private readonly object _lockOwner = new();
    
    // Cooldown para Interact - evita que el botón A se procese como salto justo después de interactuar
    private float _interactCooldownUntil = 0f;
    private const float INTERACT_COOLDOWN_DURATION = 0.3f;

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

        // GARANTIZAR bloqueos críticos para modo Inventory (independiente del Inspector)
        if (!_blockedByMode.ContainsKey(ActionMode.Inventory))
            _blockedByMode[ActionMode.Inventory] = new HashSet<PlayerAbility>();

        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Move);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Jump);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Sprint);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Roll);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Attack);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Magic);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Interact);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Carry);
        _blockedByMode[ActionMode.Inventory].Add(PlayerAbility.Aim);

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

        // GARANTIZAR bloqueos básicos para Flying (independiente del Inspector)
        if (!_blockedByMode.ContainsKey(ActionMode.Flying))
            _blockedByMode[ActionMode.Flying] = new HashSet<PlayerAbility>();

        _blockedByMode[ActionMode.Flying].Add(PlayerAbility.Jump);
        _blockedByMode[ActionMode.Flying].Add(PlayerAbility.Roll);

        // GARANTIZAR bloqueos básicos para Climbing (independiente del Inspector)
        if (!_blockedByMode.ContainsKey(ActionMode.Climbing))
            _blockedByMode[ActionMode.Climbing] = new HashSet<PlayerAbility>();

        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Jump);
        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Sprint);
        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Roll);
        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Attack);
        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Magic);
        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Carry);
        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Interact);
        _blockedByMode[ActionMode.Climbing].Add(PlayerAbility.Fly);

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
        // Comprueba permisos globales aplicados desde preset u otros sistemas
        switch (ability)
        {
            case PlayerAbility.Jump:
                if (!_allowJump)
                {
                    if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Jump deshabilitado por preset");
                    return false;
                }
                break;
            case PlayerAbility.Fly:
                if (!_allowFly)
                {
                    if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Fly deshabilitado por preset");
                    return false;
                }
                break;
            case PlayerAbility.Climb:
                if (!_allowClimb)
                {
                    if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Climb deshabilitado por preset");
                    return false;
                }
                break;
            case PlayerAbility.Magic:
                if (!_allowMagic)
                {
                    if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Magic deshabilitado por preset");
                    return false;
                }
                break;
            //case PlayerAbility.Attack:
            //    if (!_allowPhysical)
            //    {
            //        if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Attack deshabilitado por preset");
            //        return false;
            //    }
            //    break;
            // Otros mapeos pueden añadirse si se utiliza swim/climb en otras partes
        }

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

        // Special-case: when flying, ensure base locomotion animator doesn't transition to falling.
        // Some animator controllers check IsGrounded/InputMagnitude to play fall states — force safe values while Flying.
        if (top == ActionMode.Flying && _anim != null)
        {
            try
            {
                _anim.SetBool(Invector.vCharacterController.vAnimatorParameters.IsGrounded, true);
                _anim.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, 0f);
                if (debugLogs) Debug.Log("[PlayerActionManager] Flight mode active - forcing IsGrounded=true and InputMagnitude=0 on animator to avoid fall animations.");
            }
            catch { }
        }

        // NUEVO: Resetear visuales de daño al entrar en modo Cinematic
        if (top == ActionMode.Cinematic)
        {
            var healthSystem = GetComponent<PlayerHealthSystem>();
            if (healthSystem != null)
            {
                healthSystem.ResetDamageVisuals();
                if (debugLogs) Debug.Log("[PlayerActionManager] Visuales de daño reseteados (modo Cinematic)");
            }
        }

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

        UpdatePlayerLock(top);
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
        UpdatePlayerLock(ActionMode.Default);
    }

    // API pública para consultar estado
    public bool IsInMode(ActionMode mode) => _stack.Contains(mode);
    public int StackDepth => _stack.Count;

    // Implementación de IActionValidator para compatibilidad con otros namespaces
    public bool CanJump()
    {
        // Si hay menús abiertos (Shop, Equipment, etc.), NO permitir saltar
        if (MenuManager.HasOpenMenus)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Jump bloqueado - hay menús abiertos");
            return false;
        }
        
        return CanUse(PlayerAbility.Jump);
    }
    
    public bool CanSprint()
    {
        // Si hay menús abiertos, NO permitir sprint
        if (MenuManager.HasOpenMenus)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Sprint bloqueado - hay menús abiertos");
            return false;
        }
        
        return CanUse(PlayerAbility.Sprint);
    }
    
    public bool CanAttack()
    {
        // Si hay menús abiertos, NO permitir atacar
        if (MenuManager.HasOpenMenus)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Attack bloqueado - hay menús abiertos");
            return false;
        }
        
        return CanUse(PlayerAbility.Attack);
    }
    
    public bool CanCastMagic()
    {
        // Si hay menús abiertos, NO permitir magia
        if (MenuManager.HasOpenMenus)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Magic bloqueado - hay menús abiertos");
            return false;
        }
        
        return CanUse(PlayerAbility.Magic);
    }
    
    public bool CanInteract()
    {
        // IMPORTANTE: Cooldown después de interactuar para evitar que el botón A se procese como salto
        if (Time.unscaledTime < _interactCooldownUntil)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Interact en cooldown");
            return false;
        }
        
        // Si hay menús abiertos, el botón A se usa para submit en UI, no para interactuar
        if (MenuManager.HasOpenMenus)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Interact bloqueado - hay menús abiertos");
            return false;
        }
        
        return CanUse(PlayerAbility.Interact);
    }
    
    /// <summary>
    /// Llamar este método después de que el player interactúe con algo
    /// para establecer un cooldown que evite procesamiento múltiple del botón A
    /// </summary>
    public void SetInteractCooldown()
    {
        _interactCooldownUntil = Time.unscaledTime + INTERACT_COOLDOWN_DURATION;
        if (debugLogs) Debug.Log($"[PlayerActionManager] Interact cooldown activado por {INTERACT_COOLDOWN_DURATION}s");
    }
    
    public bool CanSwim() => _allowSwim;
    
    public bool CanFly()
    {
        // Si hay menús abiertos, NO permitir volar
        if (MenuManager.HasOpenMenus)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Fly bloqueado - hay menús abiertos");
            return false;
        }
        
        return CanUse(PlayerAbility.Fly);
    }
    
    public bool CanClimb()
    {
        // Si hay menús abiertos, NO permitir trepar
        if (MenuManager.HasOpenMenus)
        {
            if (debugLogs) Debug.Log("[PlayerActionManager] ❌ Climb bloqueado - hay menús abiertos");
            return false;
        }
        
        return CanUse(PlayerAbility.Climb);
    }

    void UpdatePlayerLock(ActionMode top)
    {
        bool shouldLock = ShouldLockMovement(top);

        if (shouldLock)
        {
            PlayerLockService.Instance.Acquire(_lockOwner);
        }
        else if (PlayerLockService.HasInstance)
        {
            PlayerLockService.Instance.Release(_lockOwner);
        }
    }

    bool ShouldLockMovement(ActionMode mode)
    {
        if (_blockedByMode.TryGetValue(mode, out var blocked) && blocked.Contains(PlayerAbility.Move))
            return true;

        return mode == ActionMode.Cinematic;
    }
}
