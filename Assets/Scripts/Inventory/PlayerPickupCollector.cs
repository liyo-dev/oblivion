using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Inventory))]
public class PlayerPickupCollector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerHealthSystem healthSystem;
    [SerializeField] private ManaPool manaPool;
    [SerializeField] private SpecialChargeMeter specialChargeMeter;

    [Header("Settings")]
    [SerializeField] private bool logWarnings;

    [Header("Events")]
    [SerializeField] private UnityEvent<PickupEffectType> onEffectApplied;
    [SerializeField] private UnityEvent onAnyPickup;

    void Awake()
    {
        if (!inventory) inventory = GetComponent<Inventory>();
        if (!healthSystem) healthSystem = GetComponentInChildren<PlayerHealthSystem>(true);
        if (!manaPool) manaPool = GetComponentInChildren<ManaPool>(true);
        if (!specialChargeMeter) specialChargeMeter = GetComponentInChildren<SpecialChargeMeter>(true);

        PlayerService.RegisterComponent(this);
    }

    /// <summary>
    /// Applies a pickup effect. Returns true when the effect modified the player state.
    /// The caller can decide whether the pickup should be consumed when false is returned.
    /// </summary>
    public bool TryCollect(PickupEffect effect, out bool shouldConsume)
    {
        bool changed = effect.effectType switch
        {
            PickupEffectType.Currency => ApplyCurrency(effect),
            PickupEffectType.ManaRestore => ApplyMana(effect),
            PickupEffectType.HealthRestore => ApplyHealth(effect),
            PickupEffectType.SpecialCharge => ApplySpecialCharge(effect),
            _ => false
        };

        shouldConsume = changed || effect.consumeEvenIfNoChange;

        if (changed)
        {
            onEffectApplied?.Invoke(effect.effectType);
            onAnyPickup?.Invoke();
        }

        return changed;
    }

    private bool ApplyCurrency(PickupEffect effect)
    {
        if (!inventory)
        {
            LogMissingComponent(nameof(Inventory));
            return false;
        }

        if (!effect.item)
        {
            if (logWarnings) Debug.LogWarning("[PlayerPickupCollector] Currency pickup has no ItemData assigned.");
            return false;
        }

        int quantity = effect.GetQuantityOrDefault();
        if (quantity <= 0) return false;

        inventory.Add(effect.item, quantity);
        return true;
    }

    private bool ApplyMana(PickupEffect effect)
    {
        if (!manaPool)
        {
            LogMissingComponent(nameof(ManaPool));
            return false;
        }

        float amount = effect.GetAmountOrFallback();
        if (amount <= 0f) return false;

        float before = manaPool.Current;
        manaPool.Refill(amount);
        return manaPool.Current > before + Mathf.Epsilon;
    }

    private bool ApplyHealth(PickupEffect effect)
    {
        if (!healthSystem)
        {
            LogMissingComponent(nameof(PlayerHealthSystem));
            return false;
        }

        float amount = effect.GetAmountOrFallback();
        if (amount <= 0f) return false;

        return healthSystem.Heal(amount);
    }

    private bool ApplySpecialCharge(PickupEffect effect)
    {
        if (!specialChargeMeter)
        {
            LogMissingComponent(nameof(SpecialChargeMeter));
            return false;
        }

        float amount = effect.GetAmountOrFallback();
        if (amount <= 0f) return false;

        return specialChargeMeter.AddCharge(amount);
    }

    private void LogMissingComponent(string componentName)
    {
        if (!logWarnings) return;
        Debug.LogWarning($"[PlayerPickupCollector] Missing required component '{componentName}' on '{name}'.");
    }
}
