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

    public event Action<float>            OnDamaged;   // amount aplicado
    public event Action<float, GameObject> OnDamagedBy; // amount + instigador (puede ser null)
    public event Action                    OnDied;

    void Awake() => Current = Mathf.Max(1f, maxHealth);

    public void TakeDamage(float amount) => TakeDamage(amount, null);

    public void TakeDamage(float amount, GameObject instigator)
    {
        if (!IsAlive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Damageable:{name}] ⚠️ Ignorando daño - ya está muerto (Current: {Current})");
#endif
            return;
        }
        if (amount <= 0f) return;

        if (Time.time < _invulnerableUntil)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Damageable:{name}] 🛡️ Ignorando daño - invulnerable hasta {_invulnerableUntil - Time.time:F2}s");
#endif
            return;
        }

        float oldHealth = Current;
        Current = Mathf.Max(0f, Current - amount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLogs) Debug.Log($"[Damageable:{name}] -{amount:0.##} -> {Current:0.##}/{Max}");
#endif

        OnDamaged?.Invoke(amount);
        OnDamagedBy?.Invoke(amount, instigator);

        if (invulnerabilitySeconds > 0f)
            _invulnerableUntil = Time.time + invulnerabilitySeconds;

        if (Current <= 0f)
        {
            Current = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Damageable:{name}] 💀 VIDA AGOTADA - Llamando a Die() (vida anterior: {oldHealth:F1})");
#endif
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;
        if (amount <= 0f) return;

        Current = Mathf.Min(Max, Current + amount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLogs) Debug.Log($"[Damageable:{name}] +{amount:0.##} -> {Current:0.##}/{Max}");
#endif
    }

    /// <summary>Método para que PlayerState pueda establecer máximo y actual simultáneamente</summary>
    public void SetMaxAndCurrent(float newMax, float newCurrent)
    {
        maxHealth = Mathf.Max(1f, newMax);
        Current = Mathf.Clamp(newCurrent, 0f, maxHealth);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLogs) Debug.Log($"[Damageable:{name}] SetMaxAndCurrent -> {Current:0.##}/{maxHealth}");
#endif
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

    /// <summary>Revive este objeto con la vida indicada. Reactiva colisionador y Rigidbody si fueron desactivados por Die().</summary>
    public void Revive(float hp)
    {
        enabled = true;
        _invulnerableUntil = -999f;
        var col = GetComponent<Collider>();
        if (col) col.enabled = true;
        var rb = GetComponent<Rigidbody>();
        if (rb) { rb.isKinematic = false; rb.detectCollisions = true; }
        Current = Mathf.Clamp(Mathf.Max(0.1f, hp), 0f, maxHealth);
    }

    void Die()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Damageable:{name}] 💀💀💀 Die() llamado - Invocando OnDied (suscriptores: {OnDied?.GetInvocationList().Length ?? 0})");
#endif
        OnDied?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Damageable:{name}] OnDied invocado - destroyOnDeath: {destroyOnDeath}");
#endif

        if (deathVFX)
        {
            // ✅ FIX #3 (auditoría combate, 15 ago 2026): antes Instantiate+Destroy directo, el
            // mismo patrón que ya causó un bug real y arreglado en NPCCombatLifecycleHandler.cs
            // ("el prefab no se autodestruía y quedaba flotando en el suelo indefinidamente").
            // Damageable es más genérico que ese handler (lo usa más que solo NPCs de combate),
            // así que nunca había recibido el mismo fix. Regla del proyecto: VFX de un solo uso
            // siempre vía VfxPoolService.
            VfxPoolService.Instance.Play(deathVFX, transform.position, transform.rotation, Mathf.Max(0.25f, deathVFXLifetime));
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
