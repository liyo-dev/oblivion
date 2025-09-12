using UnityEngine;

public interface IDamageable
{
    bool   IsAlive { get; }
    float  Current { get; }
    float  Max     { get; }

    /// <summary>Aplica daño directo. Ignora valores <= 0.</summary>
    void TakeDamage(float amount);

    /// <summary>Cura vida (clamp al máximo). Ignora valores <= 0.</summary>
    void Heal(float amount);

    /// <summary>Mata al objeto inmediatamente.</summary>
    void Kill();
}