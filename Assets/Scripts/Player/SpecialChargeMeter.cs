using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks charge required to trigger special attacks.
/// Charge values are normalized between 0 and MaxCharge.
/// </summary>
[DisallowMultipleComponent]
public class SpecialChargeMeter : MonoBehaviour
{
    [SerializeField] private float maxCharge = 3f;
    [SerializeField] private float chargeRequiredToUse = 1f;
    [SerializeField] private float currentCharge;

    [Header("Events")]
    [SerializeField] private UnityEvent<float> onChargePercentChanged;
    [SerializeField] private UnityEvent<float> onChargeChanged;

    public float MaxCharge => Mathf.Max(0f, maxCharge);
    public float CurrentCharge => Mathf.Clamp(currentCharge, 0f, MaxCharge);
    public float Normalized => MaxCharge > 0f ? CurrentCharge / MaxCharge : 0f;
    public bool IsReady => CurrentCharge >= Mathf.Max(chargeRequiredToUse, 0f);

    /// <summary>
    /// Adds charge and notifies listeners when value increases.
    /// Returns true when the effective charge changed.
    /// </summary>
    public bool AddCharge(float amount)
    {
        if (amount <= 0f || MaxCharge <= 0f) return false;

        float before = CurrentCharge;
        currentCharge = Mathf.Min(MaxCharge, before + amount);

        if (Mathf.Approximately(before, currentCharge)) return false;

        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Spends charge; when amount is negative it defaults to the configured requirement.
    /// Returns true when enough charge was available and got consumed.
    /// </summary>
    public bool TryConsume(float amount = -1f)
    {
        float spend = amount > 0f ? amount : Mathf.Max(chargeRequiredToUse, 0f);
        if (spend <= 0f) return true;

        if (CurrentCharge < spend) return false;

        currentCharge = Mathf.Max(0f, CurrentCharge - spend);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Sets the current charge, clamped to valid bounds.
    /// </summary>
    public void SetCharge(float amount)
    {
        float clamped = Mathf.Clamp(amount, 0f, MaxCharge);
        if (Mathf.Approximately(CurrentCharge, clamped))
            return;

        currentCharge = clamped;
        NotifyChanged();
    }

    /// <summary>
    /// Resets the charge meter to zero.
    /// </summary>
    public void ResetCharge()
    {
        if (Mathf.Approximately(CurrentCharge, 0f)) return;
        currentCharge = 0f;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        float charge = CurrentCharge;
        onChargeChanged?.Invoke(charge);
        onChargePercentChanged?.Invoke(Normalized);
    }
}
