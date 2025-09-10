using UnityEngine;

[DisallowMultipleComponent]
public class ManaPool : MonoBehaviour
{
    [SerializeField] float max = 50f;
    [SerializeField] float current = 50f;

    public float Max => max;
    public float Current => current;

    // Llamado por PlayerState al cargar preset/partida
    public void Init(float maxMP, float currentMP)
    {
        max = Mathf.Max(0f, maxMP);
        current = Mathf.Clamp(currentMP, 0f, max);
    }

    // Úsalo desde tu caster de hechizos
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (current < amount) return false;
        current -= amount;
        return true;
    }

    public void Refill(float amount)
    {
        current = Mathf.Clamp(current + Mathf.Max(0f, amount), 0f, max);
    }
}