using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Sistema completo de gestión de magia: cooldowns, maná, validaciones
[DisallowMultipleComponent]
public class MagicCaster : MonoBehaviour, IMagicCaster
{
    [Header("Referencias")]
    [SerializeField] private ManaPool manaPool;
    [SerializeField] private PlayerActionManager actionManager;
    [SerializeField] private MagicProjectileSpawner spawner;
    [SerializeField] private SpecialChargeMeter specialChargeMeter;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Cooldowns por slot
    private readonly Dictionary<MagicSlot, float> _slotCooldowns = new();
    
    // Hechizos actuales por slot
    private MagicSpellSO _leftSpell, _rightSpell, _specialSpell;

    void Awake()
    {
        // Auto-buscar componentes si no están asignados
        if (!manaPool) manaPool = GetComponentInParent<ManaPool>();
        if (!actionManager) actionManager = GetComponentInParent<PlayerActionManager>();
        if (!spawner) spawner = GetComponentInParent<MagicProjectileSpawner>();
        if (!specialChargeMeter) specialChargeMeter = GetComponentInParent<SpecialChargeMeter>();

        // Inicializar cooldowns
        _slotCooldowns[MagicSlot.Left] = 0f;
        _slotCooldowns[MagicSlot.Right] = 0f;
        _slotCooldowns[MagicSlot.Special] = 0f;
    }

    void Update()
    {
        // Reducir cooldowns
        float deltaTime = Time.deltaTime;
        var keys = new List<MagicSlot>(_slotCooldowns.Keys);
        foreach (var slot in keys)
        {
            if (_slotCooldowns[slot] > 0f)
                _slotCooldowns[slot] = Mathf.Max(0f, _slotCooldowns[slot] - deltaTime);
        }
    }

    /// Intenta lanzar magia del slot especificado
    public bool TryCastSpell(MagicSlot slot)
    {
        var spell = GetSpellForSlot(slot);
        if (!CanCastSpell(slot, spell, out string reason))
        {
            if (showDebugLogs) Debug.Log($"[MagicCaster] No se puede lanzar {slot}: {reason}");
            return false;
        }

        // Consumir maná
        if (!manaPool.TrySpend(spell.manaCost))
        {
            if (showDebugLogs) Debug.Log($"[MagicCaster] Sin maná suficiente para {spell.displayName} (costo: {spell.manaCost})");
            return false;
        }

        if (slot == MagicSlot.Special && specialChargeMeter)
        {
            if (!specialChargeMeter.TryConsume())
            {
                if (showDebugLogs) Debug.LogWarning("[MagicCaster] Fallo el consumo de carga especial.");
                if (manaPool) manaPool.Refill(spell.manaCost);
                return false;
            }
        }

        // Activar cooldown
        _slotCooldowns[slot] = spell.cooldown;

        // Lanzar el hechizo usando el spawner existente
        spawner.Spawn(slot);

        if (showDebugLogs) 
            Debug.Log($"[MagicCaster] Lanzado {spell.displayName} - Maná restante: {manaPool.Current:F1}");

        return true;
    }

    /// Intenta lanzar magia por índice de slot (0=Left, 1=Right, 2=Special)
    public bool TryCastSpell(int slotIndex)
    {
        var slot = slotIndex switch
        {
            0 => MagicSlot.Left,
            1 => MagicSlot.Right,
            2 => MagicSlot.Special,
            _ => MagicSlot.Left
        };
        return TryCastSpell(slot);
    }

    /// Verifica si se puede lanzar un hechizo
    public bool CanCastSpell(MagicSlot slot, MagicSpellSO spell, out string reason)
    {
        reason = "";

        // Verificar ActionManager (carrying, stunned, etc.)
        if (actionManager && !actionManager.CanUse(PlayerAbility.Magic))
        {
            reason = "Acción bloqueada";
            return false;
        }

        // Verificar que hay hechizo
        if (!spell)
        {
            reason = "Sin hechizo asignado";
            return false;
        }

        // Verificar cooldown
        if (_slotCooldowns[slot] > 0f)
        {
            reason = $"Cooldown activo ({_slotCooldowns[slot]:F1}s)";
            return false;
        }

        // Verificar maná
        if (manaPool && manaPool.Current < spell.manaCost)
        {
            reason = $"Maná insuficiente ({spell.manaCost:F1} requerido, {manaPool.Current:F1} disponible)";
            return false;
        }

        if (slot == MagicSlot.Special && specialChargeMeter && !specialChargeMeter.IsReady)
        {
            reason = "Sin carga especial disponible";
            return false;
        }

        return true;
    }

    /// Versión sin reason para uso simple
    public bool CanCastSpell(MagicSlot slot)
    {
        var spell = GetSpellForSlot(slot);
        return CanCastSpell(slot, spell, out _);
    }

    /// Obtiene el tiempo de cooldown restante para un slot
    public float GetCooldownTime(MagicSlot slot)
    {
        return _slotCooldowns.TryGetValue(slot, out float time) ? time : 0f;
    }

    /// Verifica si un slot está en cooldown
    public bool IsOnCooldown(MagicSlot slot)
    {
        return GetCooldownTime(slot) > 0f;
    }

    /// Establece los hechizos para cada slot
    public void SetSpells(MagicSpellSO left, MagicSpellSO right, MagicSpellSO special)
    {
        _leftSpell = left;
        _rightSpell = right;
        _specialSpell = special;

        // También actualizarlo en el spawner
        if (spawner) spawner.SetSpells(left, right, special);
    }

    /// Obtiene el hechizo para un slot específico
    public MagicSpellSO GetSpellForSlot(MagicSlot slot)
    {
        return slot switch
        {
            MagicSlot.Left => _leftSpell,
            MagicSlot.Right => _rightSpell,
            MagicSlot.Special => _specialSpell,
            _ => null
        };
    }

    /// Resetea todos los cooldowns (útil para debug o power-ups)
    public void ResetAllCooldowns()
    {
        _slotCooldowns[MagicSlot.Left] = 0f;
        _slotCooldowns[MagicSlot.Right] = 0f;
        _slotCooldowns[MagicSlot.Special] = 0f;
    }

    /// Propiedades públicas para debugging/UI
    public float LeftCooldown => GetCooldownTime(MagicSlot.Left);
    public float RightCooldown => GetCooldownTime(MagicSlot.Right);
    public float SpecialCooldown => GetCooldownTime(MagicSlot.Special);
    public SpecialChargeMeter SpecialChargeMeter => specialChargeMeter;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!manaPool) manaPool = GetComponentInParent<ManaPool>();
        if (!actionManager) actionManager = GetComponentInParent<PlayerActionManager>();
        if (!spawner) spawner = GetComponentInParent<MagicProjectileSpawner>();
        if (!specialChargeMeter) specialChargeMeter = GetComponentInParent<SpecialChargeMeter>();
    }
#endif
}
