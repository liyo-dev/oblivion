using UnityEngine;

public enum PickupEffectType
{
    Currency,
    ManaRestore,
    HealthRestore,
    SpecialCharge
}

/// <summary>
/// Describes how a pickup should affect the player.
/// </summary>
[System.Serializable]
public struct PickupEffect
{
    public PickupEffectType effectType;
    public ItemData item;
    [Min(1)] public int quantity;
    [Tooltip("Optional override for continuous values (HP, MP, charge). When zero the quantity will be used.")]
    public float amount;
    [Tooltip("Consume the pickup even if the effect did not change stats (e.g. player already at max health).")]
    public bool consumeEvenIfNoChange;

    public int GetQuantityOrDefault()
    {
        return quantity <= 0 ? 1 : quantity;
    }

    public float GetAmountOrFallback()
    {
        return amount > 0f ? amount : Mathf.Max(GetQuantityOrDefault(), 0);
    }
}
