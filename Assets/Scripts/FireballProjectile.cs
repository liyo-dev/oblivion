using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class FireballProjectile : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 6f;

    [Header("Damage")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private DamageKind kind = DamageKind.Magic;
    [SerializeField] private LayerMask enemyMask; // SOLO la capa Enemy

    [Tooltip("Si > 0, aplica daño en área al explotar.")]
    [SerializeField] private float explosionRadius = 0f;

    [Tooltip("¿Escalar el daño del AOE según distancia al centro?")]
    [SerializeField] private bool aoeHasFalloff = true;

    [Tooltip("Curva de caída de daño (x=0 centro -> 1 borde).")]
    [SerializeField] private AnimationCurve aoeFalloff = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -0.7f),  // 100% en el centro
        new Keyframe(1f, 0.3f, -0.7f, 0f) // ~30% en el borde
    );

    [Header("Impact VFX")]
    [SerializeField] private GameObject explosionVFX;
    [SerializeField] private float destroyDelayAfterHit = 0.02f;

    // Runtime
    private bool _exploded;
    private Rigidbody _rb;
    private Collider _col;
    private readonly HashSet<IDamageable> _damaged = new();

    // (opcional) quién disparó este proyectil (útil para estadísticas, evitar friendly fire, etc.)
    public GameObject Instigator { get; set; }

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (maxLifetime > 0f) Destroy(gameObject, maxLifetime);
    }

    // Usa uno u otro según tu collider (recomendado: NO trigger)
    private void OnCollisionEnter(Collision c)
    {
        var contact = c.GetContact(0);
        Explode(contact.point, Quaternion.LookRotation(-contact.normal), c.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        Explode(transform.position, transform.rotation, other);
    }

    private void Explode(Vector3 pos, Quaternion rot, Collider directHit)
    {
        if (_exploded) return;
        _exploded = true;

        // 1) Cortar física/colisiones YA para evitar reentradas en frames siguientes
        if (_col) _col.enabled = false;
        if (_rb)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
        }

        // 2) Daño directo (si golpeó algo en la capa Enemy)
        TryDamageCollider(directHit, pos, rot * Vector3.forward);

        // 3) AOE opcional con falloff
        if (explosionRadius > 0.01f)
        {
            var hits = Physics.OverlapSphere(pos, explosionRadius, enemyMask, QueryTriggerInteraction.Collide);

            foreach (var h in hits)
            {
                if (h == null) continue;

                // Distancia al centro para calcular el porcentaje via curva
                float d = Vector3.Distance(pos, h.ClosestPoint(pos));
                float t = Mathf.Clamp01(d / explosionRadius);
                float scale = aoeHasFalloff ? aoeFalloff.Evaluate(t) : 1f;

                // Mismo flujo de daño que en el impacto directo
                ApplyDamageIfEnemy(h, pos, (h.transform.position - pos).normalized, scale);
            }
        }

        // 4) VFX de explosión (usa AutoDestroyVFX si lo tienes; si no, fallback)
        if (explosionVFX)
        {
            var vfx = Instantiate(explosionVFX, pos, rot);
            if (!vfx.GetComponent<AutoDestroyVFX>()) Destroy(vfx, 4f);
        }

        // 5) Destruir el proyectil
        Destroy(gameObject, destroyDelayAfterHit);
    }

    private void TryDamageCollider(Collider col, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!col) return;
        ApplyDamageIfEnemy(col, hitPoint, hitNormal, 1f);
    }

    private void ApplyDamageIfEnemy(Collider col, Vector3 hitPoint, Vector3 hitNormal, float damageScale)
    {
        // Filtra por máscara (solo capa Enemy)
        if (((1 << col.gameObject.layer) & enemyMask) == 0) return;

        // Busca IDamageable en el objeto o sus padres
        if (!col.TryGetComponent<IDamageable>(out var dmgable))
            dmgable = col.GetComponentInParent<IDamageable>();
        if (dmgable == null) return;

        // Evita duplicar daño al mismo objetivo en esta explosión
        if (_damaged.Contains(dmgable)) return;

        float finalDamage = Mathf.Max(0f, damage * damageScale);
        if (finalDamage <= 0f) return;

        var info = new DamageInfo(
            amount: finalDamage,
            kind: kind,
            source: this.gameObject,
            instigator: Instigator,
            point: hitPoint,
            normal: hitNormal
        );

        dmgable.ApplyDamage(in info);
        _damaged.Add(dmgable);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (explosionRadius > 0.01f)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, explosionRadius);
        }
    }
#endif
}
