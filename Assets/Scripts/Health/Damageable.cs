using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class Damageable : MonoBehaviour, IDamageable
{
    // ================== VIDA / DAÑO ==================
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

    // ================== KNOCKBACK (OPCIONAL) ==================
    [Header("Knockback (Opcional)")]
    [SerializeField] private bool enableKnockback = true;
    [Tooltip("Daño mínimo aplicado (tras multiplicadores) para activar el knockback.")]
    [SerializeField] private float minDamageForKnockback = 0.01f;
    [SerializeField] private float knockStrength = 6.0f;    // “metros/seg” equivalentes
    [SerializeField] private float knockDuration = 0.15f;   // tiempo del empujón
    [SerializeField] private AnimationCurve knockCurve = null; // 1 → 0
    [Tooltip("Si hay Rigidbody no cinemático, usar AddForce en vez de desplazamiento manual.")]
    [SerializeField] private bool preferRigidbody = false;
    [Tooltip("Desactivar estos behaviours durante el knockback.")]
    [SerializeField] private MonoBehaviour[] behavioursToDisable;

    // ================== DEBUG ==================
    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;     // ← activa/desactiva logs
    [SerializeField] private bool drawHitPoint = true;  // dibuja rayito en el impacto

    // ================== EVENTOS ==================
    public event Action<DamageInfo> OnDamaged;
    public event Action OnDied;

    // ================== CACHÉS ==================
    Rigidbody _rb;
    CharacterController _cc;
    NavMeshAgent _agent;
    Animator _anim;
    Coroutine _knockCo;

    void Awake()
    {
        Current = maxHealth;
        _rb    = GetComponent<Rigidbody>();
        _cc    = GetComponent<CharacterController>();
        _agent = GetComponent<NavMeshAgent>();
        _anim  = GetComponent<Animator>();
        if (knockCurve == null) knockCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    }

    // ------------------ APLICAR DAÑO ------------------
    public void ApplyDamage(in DamageInfo dmg)
    {
        if (Current <= 0f) return;

        if (Time.time < _invulnerableUntil)
        {
            if (debugLogs) Debug.Log($"[Damageable:{name}] Ignorado (iFrames activos).");
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
            if (debugLogs) Debug.Log($"[Damageable:{name}] Daño 0 (mult={mult:0.00}).");
            return;
        }

        Current -= final;

        if (debugLogs)
        {
            string src = dmg.source ? dmg.source.name : "null";
            string inst = dmg.instigator ? dmg.instigator.name : "null";
            Debug.Log($"[Damageable:{name}] -{final:0.##} ({dmg.kind})  from Source:{src}  Instigator:{inst}  -> HP:{Mathf.Max(0, Current):0.##}/{maxHealth}");
            if (drawHitPoint) Debug.DrawRay(dmg.point, dmg.normal.normalized * 0.6f, Color.red, 0.85f);
        }

        OnDamaged?.Invoke(dmg);

        if (invulnerabilitySeconds > 0f)
            _invulnerableUntil = Time.time + invulnerabilitySeconds;

        // Knockback opcional
        if (enableKnockback && final >= minDamageForKnockback)
            DoKnockback(dmg);

        if (Current <= 0f)
        {
            Current = 0f;
            HandleDeath();
        }
    }

    // ------------------ KNOCKBACK ------------------
    void DoKnockback(in DamageInfo info)
    {
        // Dirección: preferimos la normal de impacto; si no, desde la fuente
        Vector3 dir;
        if (info.normal.sqrMagnitude > 0.001f) 
        {
            dir = info.normal;
            if (debugLogs) Debug.Log($"[Knockback:{name}] Usando normal: {dir}");
        }
        else
        {
            Vector3 from = info.source ? info.source.transform.position : info.point;
            dir = (transform.position - from);
            if (debugLogs) Debug.Log($"[Knockback:{name}] Calculando desde fuente/punto: from={from}, to={transform.position}");
        }
        
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) 
        {
            if (debugLogs) Debug.Log($"[Knockback:{name}] Dirección muy pequeña, cancelando knockback");
            return;
        }
        dir.Normalize();

        if (debugLogs) Debug.Log($"[Knockback:{name}] dir={dir} strength={knockStrength} dur={knockDuration}");
        if (debugLogs) Debug.Log($"[Knockback:{name}] Components: Agent={_agent!=null} CC={_cc!=null} RB={_rb!=null} PreferRB={preferRigidbody}");

        // Si hay Rigidbody dinámico y se prefiere, impulsamos y salimos
        if (preferRigidbody && _rb && !_rb.isKinematic)
        {
            _rb.AddForce(dir * knockStrength, ForceMode.VelocityChange);
            if (debugLogs) Debug.Log($"[Knockback:{name}] Aplicando fuerza a Rigidbody: {dir * knockStrength}");
            return;
        }

        if (_knockCo != null) StopCoroutine(_knockCo);
        _knockCo = StartCoroutine(Co_Knock(dir));
    }

    IEnumerator Co_Knock(Vector3 dir)
    {
        bool hadAgent = _agent && _agent.enabled;
        bool hadRoot  = _anim && _anim.applyRootMotion;
        bool hadRb    = _rb != null;

        // Desactivar NavMeshAgent más agresivamente
        if (hadAgent) 
        {
            _agent.isStopped = true;
            _agent.enabled = false; // Desactivarlo completamente
            if (debugLogs) Debug.Log($"[Knockback:{name}] NavMeshAgent desactivado");
        }
        if (hadRoot)  _anim.applyRootMotion = false;
        foreach (var b in behavioursToDisable) if (b) b.enabled = false;
        
        bool prevKinematic = false;
        if (hadRb)
        {
            prevKinematic = _rb.isKinematic;
            _rb.isKinematic = false;                 // temporalmente dinámico
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        if (debugLogs) Debug.Log($"[Knockback:{name}] Iniciando corrutina, duración: {knockDuration}s");

        float t = 0f;
        Vector3 startPos = transform.position;
        while (t < knockDuration)
        {
            float k = knockCurve.Evaluate(t / knockDuration);
            Vector3 delta = dir * (knockStrength * k) * Time.deltaTime;

            Vector3 oldPos = transform.position;
            if (_cc) 
            {
                _cc.Move(delta);
                if (debugLogs && t < 0.05f) Debug.Log($"[Knockback:{name}] CC.Move({delta}), pos cambió de {oldPos} a {transform.position}");
            }
            else 
            {
                transform.position += delta;
                if (debugLogs && t < 0.05f) Debug.Log($"[Knockback:{name}] Transform.position += {delta}, pos cambió de {oldPos} a {transform.position}");
            }

            t += Time.deltaTime;
            yield return null;
        }

        Vector3 endPos = transform.position;
        float totalDistance = Vector3.Distance(startPos, endPos);
        if (debugLogs) Debug.Log($"[Knockback:{name}] Knockback completado. Distancia movida: {totalDistance:F2}m");
        
        if (hadRb)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = prevKinematic;         // de vuelta a kinematic (no empujable)
        }

        // Reactivar NavMeshAgent
        if (hadAgent) 
        {
            _agent.enabled = true;
            _agent.isStopped = false;
            if (debugLogs) Debug.Log($"[Knockback:{name}] NavMeshAgent reactivado");
        }
        if (hadRoot)  _anim.applyRootMotion = true;
        foreach (var b in behavioursToDisable) if (b) b.enabled = true;
    }

    // ------------------ MUERTE ------------------
    void HandleDeath()
    {
        if (debugLogs) Debug.Log($"[Damageable:{name}] MUERTO");

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
            if (_agent) _agent.enabled = false;
            if (_cc) _cc.enabled = false;
            if (_rb) { _rb.isKinematic = true; _rb.detectCollisions = false; }
            enabled = false;
        }
    }

    // ------------------ HELPERS ------------------
    public void Heal(float amount)
    {
        if (amount <= 0f || Current <= 0f) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        if (debugLogs) Debug.Log($"[Damageable:{name}] +{amount:0.##} Heal -> HP:{Current:0.##}/{maxHealth}");
    }

    public void Kill()
    {
        if (Current <= 0f) return;
        Current = 0f;
        HandleDeath();
    }
    
    public void SetMaxAndCurrent(float max, float current)
    {
        // ajusta según tus nombres de campos
        var field = typeof(Damageable).GetField("maxHealth", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        if (field!=null) field.SetValue(this, Mathf.Max(1f, max));
        Current = Mathf.Clamp(current, 0f, Mathf.Max(1f,max));
    }

}
