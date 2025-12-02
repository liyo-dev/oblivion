using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Damageable : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;
    public float Max => maxHealth;
    public float Current { get; private set; }
    public bool  IsAlive => Current > 0f;

    [Header("Muerte")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private GameObject deathVFX;
    [SerializeField] private float deathVFXLifetime = 3f;

    [Header("Invulnerabilidad (opcional)")]
    [Tooltip("Tiempo (seg) tras recibir daño durante el cual se ignoran nuevos daños.")]
    [SerializeField] private float invulnerabilitySeconds = 0f;
    float _invulnerableUntil = -999f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    public event Action<float> OnDamaged; // amount aplicado
    public event Action       OnDied;

    void Awake() => Current = Mathf.Max(1f, maxHealth);

    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;
        if (amount <= 0f) return;

        if (Time.time < _invulnerableUntil) return;

        Current = Mathf.Max(0f, Current - amount);
        if (debugLogs) Debug.Log($"[Damageable:{name}] -{amount:0.##} -> {Current:0.##}/{Max}");

        OnDamaged?.Invoke(amount);

        if (invulnerabilitySeconds > 0f)
            _invulnerableUntil = Time.time + invulnerabilitySeconds;

        if (Current <= 0f)
        {
            Current = 0f;
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        if (amount <= 0f) return;

        Current = Mathf.Min(Max, Current + amount);
        if (debugLogs) Debug.Log($"[Damageable:{name}] +{amount:0.##} -> {Current:0.##}/{Max}");
    }

    /// <summary>Método para que PlayerState pueda establecer máximo y actual simultáneamente</summary>
    public void SetMaxAndCurrent(float newMax, float newCurrent)
    {
        maxHealth = Mathf.Max(1f, newMax);
        Current = Mathf.Clamp(newCurrent, 0f, maxHealth);
        
        if (debugLogs) Debug.Log($"[Damageable:{name}] SetMaxAndCurrent -> {Current:0.##}/{maxHealth}");
    }

    /// <summary>Establece solo la vida máxima, manteniendo el current clampeado</summary>
    public void SetMaxHealth(float newMax)
    {
        maxHealth = Mathf.Max(1f, newMax);
        Current = Mathf.Clamp(Current, 0f, maxHealth);
    }

    public void Kill()
    {
        if (!IsAlive) return;
        Current = 0f;
        Die();
    }

    void Die()
    {
        if (debugLogs) Debug.Log($"[Damageable:{name}] MUERTO");
        OnDied?.Invoke();

        if (deathVFX)
        {
            var vfx = Instantiate(deathVFX, transform.position, transform.rotation);
            Destroy(vfx, Mathf.Max(0.25f, deathVFXLifetime));
        }

        if (destroyOnDeath) Destroy(gameObject);
        else
        {
            // Estado "inerte" sencillo
            var col = GetComponent<Collider>(); if (col) col.enabled = false;
            var rb  = GetComponent<Rigidbody>(); if (rb) { rb.isKinematic = true; rb.detectCollisions = false; }
            enabled = false;
        }
    }

    // === Nuevo: permitir cambiar política de destrucción en runtime ===
    public void SetDestroyOnDeath(bool destroy)
    {
        destroyOnDeath = destroy;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        if (Application.isPlaying)
            Current = Mathf.Clamp(Current, 0f, maxHealth);
    }
#endif
}
