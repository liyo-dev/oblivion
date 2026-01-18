using UnityEngine;

/// <summary>
/// Proyectil mágico que se lanza desde MagicProjectileSpawner.
/// Maneja movimiento, colisiones, daño y efectos visuales/sonoros.
/// </summary>
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
        public LayerMask hitLayers;      // Capas que reciben daño (Enemy, Boss, etc.)
        public LayerMask collisionLayers; // Capas con las que colisiona (Enemy, Default, etc.)
        public bool destroyOnHit;

        // Vida / movimiento
        public float lifeTime;           // 0 = infinito
        public float maxRange;           // 0 = infinito
        public float initialSpeed;       // usado si NO hay Rigidbody
        public bool  useGravity;         // si hay Rigidbody

        // VFX (opcionales)
        public GameObject impactVFX;     // al impactar
        public GameObject despawnVFX;    // al morir sin impacto (TTL/rango)
        public float vfxLifetime;        // tiempo antes de destruir VFX (0 = no destruir)
        
        // Audio
        public string impactSFXKey;      // clave SFX al impactar
        
        // Elemento de magia (para interacciones con puzzle)
        public MagicElement element;     // Fire, Ice, etc.
    }

    // ==== Estado ===============================================================
    Rigidbody _rb;
    bool      _hasRb;
    bool      _ended;
    bool      _movementEnabled = true;

    ProjectileConfig _cfg;
    GameObject       _instigator;

    Vector3 _spawnPos;
    float   _spawnTime;
    
    [Header("Lifetime (optional)")]
    [Tooltip("If > 0, overrides the spell lifetime and this projectile will auto-despawn after these seconds.")]
    [SerializeField, Min(0f)] private float lifeTimeSeconds = 0f;

    // ==== Ciclo de vida ========================================================
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Si no hay Rigidbody, añadimos uno cinemático para que las colisiones funcionen.
        // Nota: Unity solo envía OnTriggerEnter/OnCollisionEnter si al menos uno tiene Rigidbody.
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
        }
        
        // IMPORTANTE: Collider NO debe ser trigger para detectar colisiones con árboles y otros objetos
        // OnCollisionEnter funciona tanto con triggers como con colliders normales
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        RefreshHasRigidbody();
    }

    bool _ttlScheduled;

    void OnEnable()
    {
        _spawnPos  = transform.position;
        _spawnTime = Time.time;

        // Si el propio componente define un TTL, usarlo ya desde OnEnable
        _ttlScheduled = false;
        CancelInvoke(nameof(EndByTTL));
        if (lifeTimeSeconds > 0f)
        {
            Invoke(nameof(EndByTTL), lifeTimeSeconds);
            _ttlScheduled = true;
        }

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

    /// <summary>
    /// Activa o desactiva el uso de Rigidbody en runtime (para cargas).
    /// Recalcula los flags internos para que el proyectil se mueva correctamente tras el cambio.
    /// </summary>
    public void SetKinematic(bool value)
    {
        if (_rb == null) return;
        _rb.isKinematic = value;
        RefreshHasRigidbody();
        // Cuando el proyectil se marca como kinematic queremos PAUSAR
        // su movimiento manual (esto se usa durante cargas/charge).
        _movementEnabled = !value;
        if (!_hasRb)
            _rb.useGravity = false;
    }

    /// <summary>
    /// Lanza el proyectil en la dirección dada aplicando velocidad/rotación.
    /// </summary>
    public void Launch(Vector3 direction, float speed, bool useGravity)
    {
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        _cfg.initialSpeed = speed;
        _cfg.useGravity = useGravity;

        if (_rb != null)
        {
            _rb.useGravity = useGravity;
                // Asegurar estado físico limpio antes de aplicar velocidad
                _rb.angularVelocity = Vector3.zero;
                _rb.linearVelocity = direction.normalized * Mathf.Max(0f, speed);
                // Garantizar que el proyectil usa la posición actual como origen de rango
                _spawnPos = transform.position;
                _movementEnabled = true;
        }
        else
        {
            // Movimiento manual en Update usa _cfg.initialSpeed
            transform.forward = direction.normalized;
                _spawnPos = transform.position;
        }
    }

    void RefreshHasRigidbody()
    {
        _hasRb = _rb != null && !_rb.isKinematic;
    }

    void Update()
    {
        if (_ended) return;

        // Movimiento manual si no hay Rigidbody
        if (!_hasRb && _movementEnabled && _cfg.initialSpeed > 0f)
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
    {
        Vector3 hitPoint = transform.position;
        
        // ClosestPoint solo funciona con Box, Sphere, Capsule y Mesh convexo
        if (other is BoxCollider || other is SphereCollider || other is CapsuleCollider || 
            (other is MeshCollider meshCol && meshCol.convex))
        {
            hitPoint = other.ClosestPoint(transform.position);
        }
        
        ResolveHit(other, hitPoint);
    }

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

        // ✅ PRIORIDAD 1: Detectar colisión con proyectiles enemigos (layer "ProjectileEnemy")
        if (other.gameObject.layer == LayerMask.NameToLayer("ProjectileEnemy"))
        {
            Debug.Log($"[MagicProjectile] 💥 Colisión con proyectil enemigo detectada!");
            ProjectileCollisionHandler.HandleCollision(gameObject, other.gameObject, hitPoint);
            return; // El handler se encarga de destruir ambos proyectiles
        }

        // ✅ PRIORIDAD 2: Detectar interacción con puzzle (enredaderas, etc.)
        var burnable = other.GetComponent<Burnable>();
        if (burnable != null)
        {
            burnable.OnHitByMagic(_cfg.element, hitPoint);
            
            // VFX de impacto
            if (_cfg.impactVFX)
            {
                var fx = Instantiate(_cfg.impactVFX, hitPoint, Quaternion.identity);
                float destroyTime = _cfg.vfxLifetime > 0f ? _cfg.vfxLifetime : 3f;
                Destroy(fx, destroyTime);
            }
            
            // SFX de impacto
            if (!string.IsNullOrEmpty(_cfg.impactSFXKey))
            {
                AudioService.Instance?.PlaySFX(_cfg.impactSFXKey, worldPosition: hitPoint);
            }
            
            // Destruir proyectil tras quemar
            if (_cfg.destroyOnHit) End(true);
            return;
        }

        // Verificar si colisionamos con esta capa (para destruir el proyectil)
        int layer = other.gameObject.layer;
        bool shouldCollide = (_cfg.collisionLayers.value & (1 << layer)) != 0;
        
        if (!shouldCollide) return; // No colisionar con esta capa

        // Verificar si esta capa recibe daño
        bool shouldDamage = (_cfg.hitLayers.value & (1 << layer)) != 0;


        if (shouldDamage)
        {
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
        }

        // VFX de impacto (siempre se muestra aunque no haga daño)
        if (_cfg.impactVFX)
        {
            var fx = Instantiate(_cfg.impactVFX, hitPoint, Quaternion.identity);
            // 🔥 CORRECCIÓN: Siempre destruir el VFX después de un tiempo
            float destroyTime = _cfg.vfxLifetime > 0f ? _cfg.vfxLifetime : 3f; // 3s por defecto
            Destroy(fx, destroyTime);
        }
        
        // 🔊 SFX de impacto
        if (!string.IsNullOrEmpty(_cfg.impactSFXKey))
        {
            AudioService.Instance?.PlaySFX(_cfg.impactSFXKey, worldPosition: hitPoint);
        }

        // Destruir proyectil al colisionar con cualquier cosa válida
        if (_cfg.destroyOnHit) End(true);
    }

    void ApplyDamageAndKnockback(Collider col, Vector3 hitPoint)
    {
        // Daño simple
        if (col)
        {
            // Buscar Damageable en el propio collider o en sus padres (enemigos con colisionadores hijos)
            if (!col.TryGetComponent<Damageable>(out var d))
                d = col.GetComponentInParent<Damageable>();
            if (d != null)
                d.TakeDamage(_cfg.damage);
        }

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

    public void End(bool byImpact)
    {
        if (_ended) return;
        _ended = true;


        // Si muere sin impactar (TTL o rango), dispara VFX de despawn
        if (!byImpact && _cfg.despawnVFX)
        {
            var fx = Instantiate(_cfg.despawnVFX, transform.position, Quaternion.identity);
            // 🔥 CORRECCIÓN: Siempre destruir el VFX después de un tiempo
            float destroyTime = _cfg.vfxLifetime > 0f ? _cfg.vfxLifetime : 3f; // 3s por defecto
            Destroy(fx, destroyTime);
        }

        // Si usas pooling, reemplaza por Despawn
        Destroy(gameObject);
    }
}
