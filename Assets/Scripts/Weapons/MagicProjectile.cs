using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MagicProjectile : MonoBehaviour
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
        new Keyframe(0f, 1f, 0f, -0.7f),   // 100% en el centro
        new Keyframe(1f, 0.3f, -0.7f, 0f)  // ~30% en el borde
    );

    [Header("Impact VFX")]
    [SerializeField] private GameObject explosionVFX;
    [SerializeField] private float destroyDelayAfterHit = 0.02f;

    [Header("Audio")]
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip explosionSound;

    // Eventos para extensibilidad
    public System.Action<Vector3> OnProjectileHit;
    public System.Action<Vector3, float> OnProjectileExplode; // posición, radio

    // Runtime
    private bool _exploded;
    private Rigidbody _rb;
    private Collider _col;
    private readonly HashSet<IDamageable> _damaged = new();
    
    // Cache para optimización
    private readonly Collider[] _aoeHitsCache = new Collider[32]; // Pool para evitar allocations
    private Transform _cachedTransform;

    // Quién disparó este proyectil (opcional)
    public GameObject Instigator { get; set; }

    private void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _cachedTransform = transform;
        
        // Validación inicial
        if (!_rb) Debug.LogError($"MagicProjectile {name} requiere Rigidbody!", this);
        if (!_col) Debug.LogError($"MagicProjectile {name} requiere Collider!", this);
    }

    private void OnEnable()
    {
        _exploded = false;
        _damaged.Clear();
        
        if (maxLifetime > 0f) 
        {
            CancelInvoke(nameof(DestroyOnTimeout));
            Invoke(nameof(DestroyOnTimeout), maxLifetime);
        }
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    private void DestroyOnTimeout()
    {
        if (!_exploded) 
        {
            // Explosión silenciosa por timeout
            Explode(_cachedTransform.position, Quaternion.identity, null, Vector3.up, false);
        }
    }

    // --- COLLISION (collider no trigger) ---
    private void OnCollisionEnter(Collision c)
    {
        if (_exploded) return; // Seguridad extra

        var contact = c.GetContact(0);
        Vector3 pos = contact.point;

        // Normal de empuje: desde el punto de impacto hacia fuera del objetivo
        Vector3 n = -contact.normal;

        Explode(pos, Quaternion.identity, c.collider, n, true);
    }

    // --- TRIGGER (collider trigger) ---
    private void OnTriggerEnter(Collider other)
    {
        if (_exploded) return; // Seguridad extra

        // Punto más cercano del objetivo al centro de la bola (seguro para todos los colliders)
        Vector3 hitPoint = GetSafeClosestPoint(other, _cachedTransform.position);

        // Normal radial: desde el centro de la explosión (proyectil) hacia el objetivo
        Vector3 n = (other.transform.position - _cachedTransform.position).normalized;

        Explode(hitPoint, Quaternion.identity, other, n, true);
    }

    // Versión con normal explícita (la usamos en ambos casos)
    private void Explode(Vector3 pos, Quaternion rot, Collider directHit, Vector3 hitNormal, bool playEffects = true)
    {
        if (_exploded) return;
        _exploded = true;

        // Cancelar timeout si existe
        CancelInvoke(nameof(DestroyOnTimeout));

        // 1) Cortar física/colisiones YA para evitar reentradas
        if (_col) _col.enabled = false;
        if (_rb)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
        }

        // 2) Daño directo
        if (directHit) TryDamageCollider(directHit, pos, hitNormal);

        // 3) AOE optimizado
        if (explosionRadius > 0.01f)
        {
            // Usar array cacheado para evitar allocations
            int hitCount = Physics.OverlapSphereNonAlloc(pos, explosionRadius, _aoeHitsCache, enemyMask, QueryTriggerInteraction.Collide);
            
            for (int i = 0; i < hitCount; i++)
            {
                var h = _aoeHitsCache[i];
                if (h == null || h == directHit) continue; // Skip si ya fue dañado directamente

                // Distancia al centro para calcular el porcentaje via curva (seguro)
                Vector3 closestPoint = GetSafeClosestPoint(h, pos);
                float d = Vector3.Distance(pos, closestPoint);
                float t = Mathf.Clamp01(d / explosionRadius);
                float scale = aoeHasFalloff ? aoeFalloff.Evaluate(t) : 1f;

                // Normal radial para cada objetivo del AOE
                Vector3 radial = (h.transform.position - pos).normalized;
                if (radial.sqrMagnitude < 0.01f) radial = Vector3.up; // Fallback

                ApplyDamageIfEnemy(h, closestPoint, radial, scale);
            }

            // Limpiar referencias del cache
            System.Array.Clear(_aoeHitsCache, 0, hitCount);
        }

        // 4) Eventos
        OnProjectileHit?.Invoke(pos);
        if (explosionRadius > 0.01f) OnProjectileExplode?.Invoke(pos, explosionRadius);

        // 5) Efectos solo si se solicita
        if (playEffects)
        {
            // VFX
            if (explosionVFX)
            {
                var vfx = Instantiate(explosionVFX, pos, rot);
                if (!vfx.GetComponent<AutoDestroyVFX>()) 
                    Destroy(vfx, 4f);
            }

            // Audio
            if (explosionRadius > 0.01f && explosionSound)
                AudioSource.PlayClipAtPoint(explosionSound, pos);
            else if (impactSound)
                AudioSource.PlayClipAtPoint(impactSound, pos);
        }

        // 6) Destruir el proyectil
        Destroy(gameObject, destroyDelayAfterHit);
    }

    private void TryDamageCollider(Collider col, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!col) return;
        ApplyDamageIfEnemy(col, hitPoint, hitNormal, 1f);
    }

    private void ApplyDamageIfEnemy(Collider col, Vector3 hitPoint, Vector3 hitNormal, float damageScale)
    {
        // *** Filtra por la capa del ROOT ***
        Transform rootTransform = col.transform.root;
        int rootLayer = rootTransform.gameObject.layer;
        if (((1 << rootLayer) & enemyMask) == 0) return;

        // Busca IDamageable en el objeto o sus padres
        if (!col.TryGetComponent<IDamageable>(out var dmgable))
            dmgable = col.GetComponentInParent<IDamageable>();
        if (dmgable == null) return;

        // Evita duplicar daño al mismo objetivo en esta explosión
        if (_damaged.Contains(dmgable)) return;

        float finalDamage = Mathf.Max(0f, damage * damageScale);
        if (finalDamage <= 0f) return;

        // Validar que el proyectil no se dañe a sí mismo
        if (Instigator && (col.transform.IsChildOf(Instigator.transform) || 
                          rootTransform == Instigator.transform)) return;

        // Construcción del DamageInfo
        var info = new DamageInfo
        {
            amount = finalDamage,
            kind = kind,
            point = hitPoint,
            normal = hitNormal,          // <- dirección "explosión → objetivo"
            source = this.gameObject,
            instigator = Instigator
        };

        dmgable.ApplyDamage(in info);
        _damaged.Add(dmgable);
    }

    // API pública para explosión manual
    public void ExplodeManually(Vector3? customPosition = null)
    {
        Vector3 pos = customPosition ?? _cachedTransform.position;
        Explode(pos, Quaternion.identity, null, Vector3.up, true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (explosionRadius > 0.01f)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, explosionRadius);
            
            // Mostrar curva de falloff
            Gizmos.color = Color.yellow;
            for (float angle = 0; angle < 360; angle += 30)
            {
                Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                Vector3 start = transform.position + dir * explosionRadius * 0.3f;
                Vector3 end = transform.position + dir * explosionRadius;
                Gizmos.DrawLine(start, end);
            }
        }
    }
#endif

    /// <summary>
    /// Obtiene el punto más cercano de un collider de manera segura, 
    /// manejando todos los tipos de colliders incluso los no compatibles con ClosestPoint()
    /// </summary>
    private Vector3 GetSafeClosestPoint(Collider collider, Vector3 position)
    {
        if (!collider) return position;

        // Intentar usar ClosestPoint si el collider lo soporta
        try
        {
            // Verificar tipos compatibles
            if (collider is BoxCollider || 
                collider is SphereCollider || 
                collider is CapsuleCollider ||
                (collider is MeshCollider meshCol && meshCol.convex))
            {
                return collider.ClosestPoint(position);
            }
        }
        catch (System.Exception)
        {
            // Si falla por cualquier razón, usar fallback
        }

        // Fallback para colliders no compatibles (MeshCollider no convexo, etc.)
        return GetClosestPointFallback(collider, position);
    }

    /// <summary>
    /// Método alternativo para obtener el punto más cercano cuando ClosestPoint() no está disponible
    /// </summary>
    private Vector3 GetClosestPointFallback(Collider collider, Vector3 position)
    {
        // Usar el bounds del collider como aproximación
        Bounds bounds = collider.bounds;
        
        // Si el punto está dentro del bounds, usar el centro
        if (bounds.Contains(position))
        {
            return bounds.center;
        }
        
        // Si está fuera, encontrar el punto más cercano en la superficie del bounds
        Vector3 closest = bounds.ClosestPoint(position);
        
        // Opcional: usar raycast para mayor precisión en MeshColliders complejos
        Vector3 direction = (closest - position).normalized;
        if (Physics.Raycast(position, direction, out RaycastHit hit, Vector3.Distance(position, closest) + 1f))
        {
            if (hit.collider == collider)
            {
                return hit.point;
            }
        }
        
        return closest;
    }
}
