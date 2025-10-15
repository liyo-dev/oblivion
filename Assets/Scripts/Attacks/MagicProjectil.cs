using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class MagicProjectile : MonoBehaviour
{
    // ==== Config que inyecta el Spawner (no hay referencia a SO) =============

    [System.Serializable]
    public struct ProjectileConfig
    {
        // Daño / impacto
        public float damage;
        public float aoeRadius;          // 0 = impacto directo
        public float knockbackForce;     // 0 = sin empuje
        public LayerMask hitLayers;
        public bool destroyOnHit;

        // Vida / movimiento
        public float lifeTime;           // 0 = infinito
        public float maxRange;           // 0 = infinito
        public float initialSpeed;       // usado si NO hay Rigidbody
        public bool  useGravity;         // si hay Rigidbody

        // VFX (opcionales)
        public GameObject impactVFX;     // al impactar
        public GameObject despawnVFX;    // al morir sin impacto (TTL/rango)
    }

    // ==== Estado ===============================================================
    Rigidbody _rb;
    bool      _hasRb;
    bool      _ended;

    ProjectileConfig _cfg;
    GameObject       _instigator;

    Vector3 _spawnPos;
    float   _spawnTime;

    // ==== Ciclo de vida ========================================================
    void Awake()
    {
        _rb    = GetComponent<Rigidbody>();
        _hasRb = _rb != null;
    }

    void OnEnable()
    {
        _spawnPos  = transform.position;
        _spawnTime = Time.time;

        // TTL (si procede) — si luego hay impacto, End() cancela el resto
        if (_cfg.lifeTime > 0f)
            Invoke(nameof(EndByTTL), _cfg.lifeTime);

        // Seguridad extra: si hay RB y se configuró gravedad
        if (_hasRb) _rb.useGravity = _cfg.useGravity;
    }

    /// <summary>
    /// Inyecta toda la configuración del proyectil y el instigador.
    /// Llamar inmediatamente tras instanciar el prefab.
    /// </summary>
    public void Configure(in ProjectileConfig cfg, GameObject instigator, bool ignoreSelfCollision = true)
    {
        _cfg        = cfg;
        _instigator = instigator;

        // Ignorar colisiones con el instigador
        if (ignoreSelfCollision && _instigator)
        {
            var myCols  = GetComponentsInChildren<Collider>(true);
            var hisCols = _instigator.GetComponentsInChildren<Collider>(true);
            foreach (var a in myCols)
                foreach (var b in hisCols)
                    if (a && b) Physics.IgnoreCollision(a, b, true);
        }
    }

    void Update()
    {
        if (_ended) return;

        // Movimiento manual si no hay Rigidbody
        if (!_hasRb && _cfg.initialSpeed > 0f)
            transform.position += transform.forward * (_cfg.initialSpeed * Time.deltaTime);

        // Fin por rango
        if (_cfg.maxRange > 0f)
        {
            float sqr = (transform.position - _spawnPos).sqrMagnitude;
            if (sqr >= _cfg.maxRange * _cfg.maxRange) End(false);
        }
    }

    // ==== Colisiones ===========================================================

    void OnTriggerEnter(Collider other)
        => ResolveHit(other, other.ClosestPoint(transform.position));

    void OnCollisionEnter(Collision c)
        => ResolveHit(c.collider, c.GetContact(0).point);

    void OnParticleCollision(GameObject other)
    {
        var col = other ? other.GetComponent<Collider>() : null;
        ResolveHit(col, transform.position);
    }

    void ResolveHit(Collider other, Vector3 hitPoint)
    {
        if (_ended || other == null) return;

        // Evitar golpearnos a nosotros mismos
        if (_instigator && other.transform.IsChildOf(_instigator.transform)) return;

        // Filtro de capas
        if ( (_cfg.hitLayers.value & (1 << other.gameObject.layer)) == 0 ) return;

        // AOE o impacto directo
        if (_cfg.aoeRadius > 0f)
        {
            var cols = Physics.OverlapSphere(hitPoint, _cfg.aoeRadius, _cfg.hitLayers, QueryTriggerInteraction.Ignore);
            foreach (var c in cols) ApplyDamageAndKnockback(c, hitPoint);
        }
        else
        {
            ApplyDamageAndKnockback(other, hitPoint);
        }

        // VFX de impacto
        if (_cfg.impactVFX) Instantiate(_cfg.impactVFX, hitPoint, Quaternion.identity);

        if (_cfg.destroyOnHit) End(true);
    }

    void ApplyDamageAndKnockback(Collider col, Vector3 hitPoint)
    {
        // Daño simple
        if (col && col.TryGetComponent<Damageable>(out var d))
            d.TakeDamage(_cfg.damage);

        // Knockback simple (si hay RB dinámico)
        if (_cfg.knockbackForce > 0f && col)
        {
            var rb = col.attachedRigidbody ? col.attachedRigidbody : col.GetComponentInParent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                Vector3 dir = (rb.worldCenterOfMass - (hitPoint != Vector3.zero ? hitPoint : transform.position)).normalized;
                dir.y = 0f; // empuje horizontal
                rb.AddForce(dir * _cfg.knockbackForce, ForceMode.Impulse);
            }
        }
    }

    // ==== Fin de vida ==========================================================

    void EndByTTL() => End(false);

    void End(bool byImpact)
    {
        if (_ended) return;
        _ended = true;

        // Si muere sin impactar (TTL o rango), dispara VFX de despawn
        if (!byImpact && _cfg.despawnVFX)
            Instantiate(_cfg.despawnVFX, transform.position, Quaternion.identity);

        // Si usas pooling, reemplaza por Despawn
        Destroy(gameObject);
    }
}
