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
    [SerializeField] private PlayerShieldController shieldController;
    [SerializeField] private PlayerMovementBlocker movementBlocker;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Cooldowns por slot
    private readonly Dictionary<MagicSlot, float> _slotCooldowns = new();
    private static readonly MagicSlot[] AllSlots = { MagicSlot.Left, MagicSlot.Right, MagicSlot.Special };
    
    // Hechizos actuales por slot
    private MagicSpellSO _leftSpell, _rightSpell, _specialSpell;
    private float _castingUntil;

    // INC-104: bloqueo de giro/movimiento del jugador durante la animación de cast.
    private float _castingLockUntil;
    private Coroutine _castingLockRoutine;
    private bool _castingModePushed;

    public bool IsCasting => Time.time < _castingUntil;

    void Awake()
    {
        // Auto-buscar componentes si no están asignados
        if (!manaPool) manaPool = GetComponentInParent<ManaPool>();
        if (!actionManager) actionManager = GetComponentInParent<PlayerActionManager>();
        if (!spawner) spawner = GetComponentInParent<MagicProjectileSpawner>();
        if (!specialChargeMeter) specialChargeMeter = GetComponentInParent<SpecialChargeMeter>();
        if (!shieldController) shieldController = GetComponentInParent<PlayerShieldController>();

        // INC-104: PlayerMovementBlocker todavía no estaba colocado en ningún GameObject del
        // proyecto (clase ya escrita, pero sin usar) — lo añadimos en el mismo root que
        // PlayerActionManager la primera vez que se necesita.
        if (!movementBlocker) movementBlocker = GetComponentInParent<PlayerMovementBlocker>();
        if (!movementBlocker)
        {
            var root = actionManager ? actionManager.gameObject : transform.root.gameObject;
            movementBlocker = root.AddComponent<PlayerMovementBlocker>();
        }

        // Inicializar cooldowns
        InitializeCooldowns();
    }

    void InitializeCooldowns()
    {
        if (!_slotCooldowns.ContainsKey(MagicSlot.Left))
            _slotCooldowns[MagicSlot.Left] = 0f;
        if (!_slotCooldowns.ContainsKey(MagicSlot.Right))
            _slotCooldowns[MagicSlot.Right] = 0f;
        if (!_slotCooldowns.ContainsKey(MagicSlot.Special))
            _slotCooldowns[MagicSlot.Special] = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < AllSlots.Length; i++)
        {
            var s = AllSlots[i];
            if (_slotCooldowns.TryGetValue(s, out float cd) && cd > 0f)
                _slotCooldowns[s] = Mathf.Max(0f, cd - dt);
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

        // Los hechizos de Levitación se manejan por PlayerLevitationController, no aquí
        if (spell.kind == MagicKind.Levitation)
        {
            if (showDebugLogs) Debug.Log($"[MagicCaster] Hechizo {spell.displayName} es de tipo Levitación, ignorando (manejado por PlayerLevitationController)");
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
        InitializeCooldowns();
        _slotCooldowns[slot] = spell.cooldown;

        float lockDuration = GetCastingLockDuration(spell);
        _castingUntil = Time.time + lockDuration;

        // INC-104: bloquear giro y movimiento del jugador mientras dura la animación de cast
        // (PlayerMovementBlocker.BlockMovementKeepCamera(), cámara libre) y, en paralelo,
        // empujar ActionMode.Casting como gate lógico de Sprint/Roll/Jump/Attack/Magic. Mismo
        // patrón Push/Pop con guard en OnDisable que AerialKnockbackReceiver usa para Stunned.
        if (actionManager != null || movementBlocker != null)
        {
            _castingLockUntil = Mathf.Max(_castingLockUntil, Time.time + lockDuration);
            if (_castingLockRoutine == null)
                _castingLockRoutine = StartCoroutine(Co_CastingLock());
        }

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
        // Asegurar que el diccionario esté inicializado
        InitializeCooldowns();
        
        reason = "";

        // Verificar ActionManager (carrying, stunned, etc.)
        if (actionManager && !actionManager.CanUse(PlayerAbility.Magic))
        {
            reason = "Acción bloqueada";
            return false;
        }

        if (shieldController != null && shieldController.IsDefending)
        {
            reason = "Defendiendo";
            return false;
        }

        // Verificar que hay hechizo
        if (!spell)
        {
            reason = "Sin hechizo asignado";
            return false;
        }

        // Verificar cooldown
        if (_slotCooldowns.TryGetValue(slot, out float cooldown) && cooldown > 0f)
        {
            reason = $"Cooldown activo ({cooldown:F1}s)";
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

    float GetCastingLockDuration(MagicSpellSO spell)
    {
        if (!spell)
            return 0.1f;

        float duration = Mathf.Max(0.1f, spell.castDelaySeconds + spell.chargeTime);
        return duration;
    }

    IEnumerator Co_CastingLock()
    {
        if (actionManager != null)
        {
            actionManager.PushMode(ActionMode.Casting);
            _castingModePushed = true;
        }

        // BlockMovementKeepCamera() resetea InputMagnitude/H/V del Animator al instante (evita
        // el "se queda caminando" de dejar el valor del frame anterior) y deja vThirdPersonInput
        // activo, así que la cámara sigue respondiendo durante todo el cast.
        movementBlocker?.BlockMovementKeepCamera();

        while (Time.time < _castingLockUntil)
            yield return null;

        movementBlocker?.RestoreMovement();

        if (_castingModePushed)
        {
            actionManager.PopMode(ActionMode.Casting);
            _castingModePushed = false;
        }
        _castingLockRoutine = null;
    }

    void OnDisable()
    {
        // Igual que AerialKnockbackReceiver con ActionMode.Stunned: si el objeto se
        // desactiva a mitad del lock (muerte, cambio de escena, etc.) Unity detiene la
        // corrutina sin ejecutar lo que va después del while — hay que soltarlo aquí o
        // ActionMode.Casting quedaría apilado para siempre en PlayerActionManager, y el
        // player quedaría con el movimiento bloqueado (RestoreMovement() es un no-op seguro
        // si BlockMovementKeepCamera() nunca llegó a activarse).
        if (_castingModePushed && actionManager != null)
        {
            actionManager.PopMode(ActionMode.Casting);
            _castingModePushed = false;
        }
        movementBlocker?.RestoreMovement();
        _castingLockRoutine = null;
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

    /// Verifica si el slot tiene un hechizo de tipo Levitación (implementación de IMagicCaster)
    public bool IsLevitationSpell(int slotIndex)
    {
        var slot = slotIndex switch
        {
            0 => MagicSlot.Left,
            1 => MagicSlot.Right,
            2 => MagicSlot.Special,
            _ => MagicSlot.Left
        };
        var spell = GetSpellForSlot(slot);
        return spell != null && spell.kind == MagicKind.Levitation;
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
        if (!shieldController) shieldController = GetComponentInParent<PlayerShieldController>();
        if (!movementBlocker) movementBlocker = GetComponentInParent<PlayerMovementBlocker>();
    }
#endif
}
