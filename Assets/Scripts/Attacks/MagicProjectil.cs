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
    
    // ✅ OPTIMIZACIÓN FASE 2: Buffers reutilizables para Physics queries
    private Collider[] _targetSearchBuffer = new Collider[16];
    private Collider[] _aoeHitBuffer = new Collider[32];
    
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
        
        // ✅ NOTA: NO forzamos isTrigger aquí - dejamos la configuración del prefab
        // El prefab debe decidir si es trigger o no según el tipo de proyectil
        // Los proyectiles de aliados deben tener collider NO-trigger para colisionar con enemigos trigger
        
        // Configurar Rigidbody para detección continua
        if (_rb != null)
        {
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        
        // Asegurar que el proyectil tenga la layer correcta para colisionar con enemigos
        if (gameObject.layer == 0) // Si está en Default, mover a Projectile
        {
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer != -1)
            {
                gameObject.layer = projectileLayer;
            }
        }

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

        // ✅ IMPORTANTE: Aplicar el lifeTime de la configuración del SO
        // Esto sobrescribe el lifeTimeSeconds del prefab si el SO define un valor
        if (_cfg.lifeTime > 0f)
        {
            CancelInvoke(nameof(EndByTTL));
            Invoke(nameof(EndByTTL), _cfg.lifeTime);
            _ttlScheduled = true;
        }

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
        Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        if (normalizedDirection.sqrMagnitude <= 0.0001f)
            normalizedDirection = Vector3.forward;

        transform.rotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);

        _cfg.initialSpeed = speed;
        _cfg.useGravity = useGravity;

        if (_rb != null)
        {
            // Nunca aplicar velocidades físicas a RB cinemático (evita warnings de Unity).
            if (!_rb.isKinematic)
            {
                _rb.useGravity = useGravity;
                _rb.angularVelocity = Vector3.zero;
                _rb.linearVelocity = normalizedDirection * Mathf.Max(0f, speed);
                _movementEnabled = true;
            }
            else
            {
                _rb.useGravity = false;
                _movementEnabled = true;
                transform.forward = normalizedDirection;
            }

            // Garantizar que el proyectil usa la posición actual como origen de rango
            _spawnPos = transform.position;
        }
        else
        {
            // Movimiento manual en Update usa _cfg.initialSpeed
            transform.forward = normalizedDirection;
            _spawnPos = transform.position;
            _movementEnabled = true;
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
        
        // ✅ Detección activa de enemigos (fallback cuando las colisiones de Unity fallan)
        CheckEnemyProximity();
    }
    
    /// <summary>
    /// Detecta enemigos cercanos usando OverlapSphere como fallback.
    /// Esto es necesario porque las colisiones entre layers pueden no funcionar
    /// si la Physics Matrix no está configurada correctamente.
    /// </summary>
    private void CheckEnemyProximity()
    {
        if (_ended) return;
        
        // Radio de detección del proyectil
        float detectionRadius = 1.2f;
        
        // Buscar enemigos en el radio usando hitLayers configurado
        LayerMask searchMask = _cfg.hitLayers;
        if (searchMask.value == 0)
        {
            // Si no hay hitLayers configurado, usar Enemy y Boss por defecto
            searchMask = LayerMask.GetMask("Enemy", "Boss");
        }
        
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _targetSearchBuffer, searchMask); // ✅ OPTIMIZACIÓN FASE 2: NonAlloc
        
        if (hitCount > 0)
        {
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _targetSearchBuffer[i];
                
                // Ignorar si es el instigador
                if (_instigator != null && hit.transform.IsChildOf(_instigator.transform))
                    continue;

                // Escudo NPC activo: bloquea impacto y evita daño.
                if (TryHandleNpcShieldBlock(hit, transform.position))
                    return;
                
                // Buscar Damageable
                var damageable = hit.GetComponent<Damageable>() ?? hit.GetComponentInParent<Damageable>();
                if (damageable != null)
                {
                    Debug.Log($"[MagicProjectile] 🎯 IMPACTO por PROXIMIDAD contra ENEMIGO: {hit.gameObject.name}!");
                    ApplyDamageAndKnockback(hit, transform.position);
                    SpawnImpactEffects(transform.position);
                    if (_cfg.destroyOnHit) End(true);
                    return;
                }
            }
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

        int otherLayer = other.gameObject.layer;
        string objectName = other.gameObject.name;
        string layerName = LayerMask.LayerToName(otherLayer);
        
        // ✅ IGNORAR: Battle Arenas, objetos TransparentFX, proyectiles propios, y objetos de batalla
        // Las arenas de batalla tienen colliders que no deben bloquear proyectiles de aliados
        bool isBattleArena = objectName.Contains("BattleArena") || 
                             objectName.Contains("Arena") ||
                             objectName.Contains("BossArena") ||
                             objectName.StartsWith("Battle");
        bool isTransparentOrIgnored = layerName == "TransparentFX" || 
                                       layerName == "Ignore Raycast" || 
                                       otherLayer == 1 ||
                                       otherLayer == 2; // IgnoreRaycast layer
        bool isOtherProjectile = layerName == "Projectile"; // Ignorar colisión con otros proyectiles aliados
        bool isEnemyProjectile = layerName == "ProjectileEnemy" || layerName == "EnemyProjectile";
        
        bool shouldIgnore = isBattleArena || isTransparentOrIgnored || isOtherProjectile || isEnemyProjectile;
            
        if (shouldIgnore)
        {
            // Solo log si es arena (los otros son muy frecuentes)
            if (isBattleArena)
            {
                // Debug.Log($"[MagicProjectile] ⚠️ Ignorando arena: {objectName}");
            }
            return;
        }

        // Escudo NPC activo: bloquear antes de cualquier lógica de daño.
        if (TryHandleNpcShieldBlock(other, hitPoint))
            return;
        
        // 🔍 DEBUG: Log de cada colisión (solo si no se ignora)
        // Debug.Log($"[MagicProjectile] OnHit: {objectName} (Layer: {layerName}, Tag: {other.tag})");

        // ✅ PRIORIDAD 1: Detectar colisión con proyectiles enemigos (layer "ProjectileEnemy" o "EnemyProjectile")
        if (layerName == "ProjectileEnemy" || layerName == "EnemyProjectile")
        {
            Debug.Log($"[MagicProjectile] 💥 Colisión con proyectil enemigo detectada!");
            ProjectileCollisionHandler.HandleCollision(gameObject, other.gameObject, hitPoint);
            return; // El handler se encarga de destruir ambos proyectiles
        }

        // ✅ PRIORIDAD 2: Detectar colisión con layer Enemy o Boss directamente
        // Detectamos por layer (Enemy o Boss) y opcionalmente por tag "Enemy"
        bool isEnemy = layerName == "Enemy" || 
                       layerName == "Boss" ||
                       SafeCompareTag(other, "Enemy");
        
        if (isEnemy)
        {
            Debug.Log($"[MagicProjectile] 🎯 IMPACTO CONTRA ENEMIGO: {other.gameObject.name}!");

            ApplyDamageAndKnockback(other, hitPoint);
            
            SpawnImpactEffects(hitPoint);
            if (_cfg.destroyOnHit) End(true);
            return;
        }

        // ✅ PRIORIDAD 3: Detectar interacción con puzzle (enredaderas, etc.)
        var burnable = other.GetComponent<Burnable>();
        if (burnable != null)
        {
            burnable.OnHitByMagic(_cfg.element, hitPoint);
            SpawnImpactEffects(hitPoint);
            if (_cfg.destroyOnHit) End(true);
            return;
        }

        // ✅ PRIORIDAD 4: Intentar aplicar daño si el objeto tiene Damageable (independiente del layer)
        // Esto permite dañar enemigos/bosses aunque estén en layer Default
        var targetDamageable = other.GetComponent<Damageable>();
        if (targetDamageable == null)
            targetDamageable = other.GetComponentInParent<Damageable>();
        
        if (targetDamageable != null)
        {
            // Verificar que no sea aliado (el propio lanzador o sus aliados)
            if (_instigator != null && targetDamageable.gameObject == _instigator)
            {
                return; // No dañarse a sí mismo
            }
            
            // ✅ Aplicar daño al Damageable encontrado
            Debug.Log($"[MagicProjectile] 🎯 Impacto contra {other.gameObject.name} - Aplicando {_cfg.damage} de daño via Damageable");
            
            if (_cfg.aoeRadius > 0f)
            {
                // Para AOE, buscar todos los Damageable en el radio
                int aoeCount = Physics.OverlapSphereNonAlloc(hitPoint, _cfg.aoeRadius, _aoeHitBuffer); // ✅ OPTIMIZACIÓN FASE 2: NonAlloc
                for (int i = 0; i < aoeCount; i++)
                {
                    var c = _aoeHitBuffer[i];
                    var d = c.GetComponent<Damageable>() ?? c.GetComponentInParent<Damageable>();
                    if (d != null && (_instigator == null || d.gameObject != _instigator))
                    {
                        ApplyDamageAndKnockback(c, hitPoint);
                    }
                }
            }
            else
            {
                ApplyDamageAndKnockback(other, hitPoint);
            }
            
            SpawnImpactEffects(hitPoint);
            if (_cfg.destroyOnHit) End(true);
            return;
        }

        // ✅ PRIORIDAD 5: Verificar colisión por layers (para objetos sin Damageable)
        bool shouldCollide = (_cfg.collisionLayers.value & (1 << otherLayer)) != 0;
        
        if (!shouldCollide) return; // No colisionar con esta capa

        // Verificar si esta capa recibe daño (fallback para objetos con Damageable en capas específicas)
        bool shouldDamage = (_cfg.hitLayers.value & (1 << otherLayer)) != 0;

        if (shouldDamage)
        {
            // AOE o impacto directo
            if (_cfg.aoeRadius > 0f)
            {
                int aoeCount = Physics.OverlapSphereNonAlloc(hitPoint, _cfg.aoeRadius, _aoeHitBuffer, _cfg.hitLayers, QueryTriggerInteraction.Ignore); // ✅ OPTIMIZACIÓN FASE 2: NonAlloc
                for (int i = 0; i < aoeCount; i++)
                {
                    ApplyDamageAndKnockback(_aoeHitBuffer[i], hitPoint);
                }
            }
            else
            {
                ApplyDamageAndKnockback(other, hitPoint);
            }
        }

        SpawnImpactEffects(hitPoint);
        if (_cfg.destroyOnHit) End(true);
    }
    
    /// <summary>
    /// Spawn VFX y SFX de impacto
    /// </summary>
    void SpawnImpactEffects(Vector3 hitPoint)
    {
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
    }

    void ApplyDamageAndKnockback(Collider col, Vector3 hitPoint)
    {
        // Si el objetivo está defendiendo con escudo NPC, no recibe daño ni knockback.
        if (col != null && TryGetDefendingNpcShield(col, out var defendingShield))
        {
            defendingShield.OnShieldHit();
            return;
        }

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

    private bool TryHandleNpcShieldBlock(Collider other, Vector3 hitPoint)
    {
        if (!TryGetDefendingNpcShield(other, out var shieldController))
            return false;

        shieldController.OnShieldHit();
        SpawnImpactEffects(hitPoint);
        End(true);
        Debug.Log($"[MagicProjectile] 🛡️ Bloqueado por escudo NPC: {shieldController.gameObject.name}");
        return true;
    }

    private static bool TryGetDefendingNpcShield(Collider other, out NPCShieldController shieldController)
    {
        shieldController = null;
        if (other == null) return false;

        if (TryGetDefendingNpcShieldFromTransform(other.transform, out shieldController))
            return true;

        if (other.attachedRigidbody != null &&
            TryGetDefendingNpcShieldFromTransform(other.attachedRigidbody.transform, out shieldController))
            return true;

        var damageable = other.GetComponent<Damageable>() ??
                         other.GetComponentInParent<Damageable>() ??
                         other.GetComponentInChildren<Damageable>(true);
        if (damageable != null && TryGetDefendingNpcShieldFromTransform(damageable.transform, out shieldController))
            return true;

        if (other.transform.root != null &&
            TryGetDefendingNpcShieldFromTransform(other.transform.root, out shieldController))
            return true;

        shieldController = null;
        return false;
    }

    private static bool TryGetDefendingNpcShieldFromTransform(Transform source, out NPCShieldController shieldController)
    {
        shieldController = null;
        if (source == null) return false;

        var shieldMarker = source.GetComponent<NPCShieldController.NPCShieldMarker>() ??
                           source.GetComponentInParent<NPCShieldController.NPCShieldMarker>() ??
                           source.GetComponentInChildren<NPCShieldController.NPCShieldMarker>(true);
        if (shieldMarker != null)
        {
            shieldController = shieldMarker.GetComponentInParent<NPCShieldController>() ??
                               shieldMarker.GetComponentInChildren<NPCShieldController>(true);
            if (shieldController != null && shieldController.IsDefending)
                return true;
        }

        shieldController = source.GetComponent<NPCShieldController>() ??
                           source.GetComponentInParent<NPCShieldController>() ??
                           source.GetComponentInChildren<NPCShieldController>(true);
        if (shieldController != null && shieldController.IsDefending)
            return true;

        shieldController = null;
        return false;
    }
    
    // ==== Helpers ==============================================================
    
    /// <summary>
    /// CompareTag seguro que no lanza excepción si el tag no existe.
    /// </summary>
    private static bool SafeCompareTag(Component obj, string tag)
    {
        if (obj == null || string.IsNullOrEmpty(tag)) return false;
        try
        {
            return obj.CompareTag(tag);
        }
        catch
        {
            return false;
        }
    }
}
