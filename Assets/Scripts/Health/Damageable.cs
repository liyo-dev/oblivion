using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Damageable : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;
    public float Current { get; private set; }

    [Header("Muerte")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private GameObject deathVFX; // opcional
    [SerializeField] private float deathVFXLifetime = 3f; // fallback si el VFX no se autodestruye

    [Header("Ajustes de daño (multiplicadores)")]
    [Tooltip("Multiplicadores por tipo: 1 = normal, 0.5 = resistencia, 2 = vulnerable")]
    [SerializeField] private float physicalMultiplier = 1f;
    [SerializeField] private float magicMultiplier    = 1f;
    [SerializeField] private float specialMultiplier  = 1f;

    [Header("Invulnerabilidad (opcional)")]
    [SerializeField] private float invulnerabilitySeconds = 0f; // 0 = sin iFrames
    private float _invulnerableUntil = -999f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;     // ← activa/desactiva logs
    [SerializeField] private bool drawHitPoint = true;  // dibuja rayito en el impacto

    public event Action<DamageInfo> OnDamaged;
    public event Action OnDied;

    void Awake()
    {
        Current = maxHealth;
    }

    public void ApplyDamage(in DamageInfo dmg)
    {
        if (Current <= 0f) return;
        if (Time.time < _invulnerableUntil)
        {
            if (debugLogs)
                Debug.Log($"[Damageable:{name}] Ignorado (iFrames activos).");
            return;
        }

        float mult = 1f;
        switch (dmg.kind)
        {
            case DamageKind.Physical: mult = physicalMultiplier; break;
            case DamageKind.Magic:    mult = magicMultiplier;    break;
            case DamageKind.Special:  mult = specialMultiplier;  break;
        }

        float final = Mathf.Max(0f, dmg.amount * mult);
        if (final <= 0f)
        {
            if (debugLogs)
                Debug.Log($"[Damageable:{name}] Daño 0 (mult={mult:0.00}).");
            return;
        }

        Current -= final;

        if (debugLogs)
        {
            string src = dmg.source ? dmg.source.name : "null";
            string inst = dmg.instigator ? dmg.instigator.name : "null";
            Debug.Log($"[Damageable:{name}] -{final:0.##} ({dmg.kind})  from Source:{src}  Instigator:{inst}  -> HP:{Mathf.Max(0, Current):0.##}/{maxHealth}");
            if (drawHitPoint)
            {
                Debug.DrawRay(dmg.point, dmg.normal.normalized * 0.6f, Color.red, 0.85f);
            }
        }

        OnDamaged?.Invoke(dmg);

        if (invulnerabilitySeconds > 0f)
            _invulnerableUntil = Time.time + invulnerabilitySeconds;

        if (Current <= 0f)
        {
            Current = 0f;
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        if (debugLogs)
            Debug.Log($"[Damageable:{name}] MUERTO");

        OnDied?.Invoke();

        if (deathVFX)
        {
            var vfx = Instantiate(deathVFX, transform.position, transform.rotation);
            Destroy(vfx, Mathf.Max(0.5f, deathVFXLifetime));
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            var col = GetComponent<Collider>(); if (col) col.enabled = false;
            var rb  = GetComponent<Rigidbody>(); if (rb) { rb.isKinematic = true; rb.detectCollisions = false; }
            enabled = false;
        }
    }

    // Helpers
    public void Heal(float amount)
    {
        if (amount <= 0f || Current <= 0f) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        if (debugLogs)
            Debug.Log($"[Damageable:{name}] +{amount:0.##} Heal -> HP:{Current:0.##}/{maxHealth}");
    }

    public void Kill()
    {
        if (Current <= 0f) return;
        Current = 0f;
        HandleDeath();
    }
}
