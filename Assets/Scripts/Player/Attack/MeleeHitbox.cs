using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MeleeHitbox : MonoBehaviour, IAttackHitbox
{
    [Header("Damage")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private DamageKind kind = DamageKind.Physical;
    [SerializeField] private LayerMask enemyMask; // solo capa Enemy

    [Header("Ventana de golpe")]
    [SerializeField] private float defaultActiveTime = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private readonly HashSet<IDamageable> _hitThisSwing = new();
    private bool _armed;
    private Collider _col;

    void Awake()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true;

        // Asegura un Rigidbody kinemático para que OnTriggerEnter funcione siempre
        if (!TryGetComponent<Rigidbody>(out var rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (debugLogs) Debug.Log($"[MeleeHitbox:{name}] Añadido Rigidbody kinemático auto.");
        }
    }

    /// Activa el hitbox durante 't' segundos
    public void ArmForSeconds(float t)
    {
        StopAllCoroutines();
        StartCoroutine(Co_Arm(t <= 0f ? defaultActiveTime : t));
    }

    public void ArmBegin()
    {
        _armed = true;
        _hitThisSwing.Clear();
        if (debugLogs) Debug.Log($"[MeleeHitbox:{name}] ARM BEGIN");
    }

    public void ArmEnd()
    {
        _armed = false;
        _hitThisSwing.Clear();
        if (debugLogs) Debug.Log($"[MeleeHitbox:{name}] ARM END");
    }

    private IEnumerator Co_Arm(float t)
    {
        ArmBegin();
        yield return new WaitForSeconds(t);
        ArmEnd();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;

        if (((1 << other.gameObject.layer) & enemyMask) == 0)
        {
            if (debugLogs) Debug.Log($"[MeleeHitbox:{name}] Ignora {other.name} (no Enemy mask).");
            return;
        }

        if (!other.TryGetComponent<IDamageable>(out var dmg))
            dmg = other.GetComponentInParent<IDamageable>();
        if (dmg == null)
        {
            if (debugLogs) Debug.Log($"[MeleeHitbox:{name}] {other.name} no tiene IDamageable.");
            return;
        }
        if (_hitThisSwing.Contains(dmg)) return;

        Vector3 p = other.ClosestPoint(transform.position);
        Vector3 n = (other.transform.position - transform.position).normalized;

        var info = new DamageInfo(damage, kind, this.gameObject, null, p, n);
        dmg.ApplyDamage(in info);
        _hitThisSwing.Add(dmg);

        if (debugLogs) Debug.Log($"[MeleeHitbox:{name}] HIT {other.name} -> {damage}");
    }

#if UNITY_EDITOR
    // para ver el volumen del golpe en Scene
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.2f, 0.35f);
        if (TryGetComponent<Collider>(out var c))
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
#endif
}
