using UnityEngine;

/// <summary>
/// Proyectil disparado por el boss demonio
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float baseDamage = 10f; // Daño base configurado en el prefab
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool usePhysicsMovement = true; // Usar Rigidbody para movimiento suave
    
    private Vector3 direction;
    private float damage;
    private bool initialized = false;
    private bool hasHit = false;
    private Rigidbody rb;
    private System.Collections.Generic.List<GameObject> _attachedVfx;

    void Awake()
    {
        // ✅ Configurar el collider para colisiones FÍSICAS (no trigger)
        // Esto permite detectar obstáculos Default correctamente
        var col = GetComponent<SphereCollider>();
        if (col)
        {
            col.isTrigger = false; // ← Cambio CRÍTICO: Usar colisiones físicas
            col.radius = 0.5f;
        }

        // Configurar el rigidbody para movimiento suave CON colisiones físicas
        rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Mejor detección
            
            // ✅ SIEMPRE usar física no-kinematic para detectar colisiones
            rb.isKinematic = false;
            rb.linearDamping = 0f; // Sin fricción
            rb.angularDamping = 0f;
            
            // ✅ Congelar rotación para que no gire al colisionar
            rb.freezeRotation = true;
        }
    }

    public void Initialize(Vector3 dir, float dmg = -1f)
    {
        direction = dir.normalized;
        // Si no se pasa daño, usar el baseDamage del prefab
        damage = dmg > 0f ? dmg : baseDamage;
        initialized = true;
        
        Debug.Log($"[EnemyProjectile] Inicializado con {damage} de daño");
        
        // ✅ Aplicar velocidad inicial usando física
        if (rb)
        {
            rb.linearVelocity = direction * speed;
        }
        
        // Destruir después del tiempo de vida
        Destroy(gameObject, lifetime);
    }
    
    /// <summary>
    /// Obtiene el daño configurado del proyectil (baseDamage del prefab)
    /// </summary>
    public float GetDamage()
    {
        return baseDamage;
    }

    // Permite registrar VFX asociados al proyectil para que se destruyan cuando este desaparezca
    public void RegisterAttachedVFX(GameObject vfx, bool parentToProjectile = true)
    {
        if (vfx == null) return;
        _attachedVfx ??= new System.Collections.Generic.List<GameObject>();
        _attachedVfx.Add(vfx);
        if (parentToProjectile)
        {
            try { vfx.transform.SetParent(transform, true); } catch { }
        }
    }

    void FixedUpdate()
    {
        // ✅ Usar SOLO física para movimiento (detección de colisiones garantizada)
        if (!initialized || hasHit) return;
        
        // Mantener la velocidad constante
        if (rb)
        {
            rb.linearVelocity = direction * speed;
        }
    }

    // ✅ OnCollisionEnter: Para colisiones FÍSICAS (obstáculos Default, jugador)
    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        
        Collider other = collision.collider;
        
        // Ignorar enemigos
        if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Enemy")) 
        {
            return;
        }
        
        // ✅ PRIORIDAD 1: Detectar colisión con layer Default (entorno/obstáculos)
        if (other.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            hasHit = true;
            Debug.Log($"[EnemyProjectile] 💥 Impacto FÍSICO contra objeto Default: {other.gameObject.name}");
            DestroyProjectile();
            return;
        }
        
        // ✅ PRIORIDAD 2: Si impacta contra el escudo del jugador
        if (other.GetComponent<PlayerShieldController.ShieldMarker>() != null)
        {
            hasHit = true;
            Debug.Log($"[EnemyProjectile] 🛡️ Bloqueado por escudo del jugador");
            DestroyProjectile();
            return;
        }

        // ✅ PRIORIDAD 3: Aplicar daño si es el jugador
        Transform checkTransform = other.transform;
        for (int i = 0; i < 3; i++)
        {
            if (checkTransform.CompareTag("Player"))
            {
                hasHit = true;
                ApplyDamage(checkTransform.gameObject);
                DestroyProjectile();
                return;
            }
            
            if (checkTransform.parent != null)
                checkTransform = checkTransform.parent;
            else
                break;
        }

        var playerHealth = other.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(damage);
            Debug.Log($"[EnemyProjectile] Daño aplicado: {damage} (encontrado por componente)");
            DestroyProjectile();
            return;
        }

        // ✅ Cualquier otra colisión física
        hasHit = true;
        Debug.Log($"[EnemyProjectile] 💥 Impacto contra: {other.gameObject.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
        DestroyProjectile();
    }

    // ✅ OnTriggerEnter: Solo para proyectiles del jugador (que usan triggers)
    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        
        // Ignorar enemigos
        if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Enemy")) 
        {
            return;
        }
        
        // ✅ SOLO para colisión con proyectiles del jugador (layer "Projectile")
        if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
        {
            hasHit = true;
            Debug.Log($"[EnemyProjectile] 💥 Colisión con proyectil del jugador detectada!");
            Vector3 collisionPoint = other.ClosestPoint(transform.position);
            ProjectileCollisionHandler.HandleCollision(other.gameObject, gameObject, collisionPoint);
            return;
        }
    }

    private void ApplyDamage(GameObject target)
    {
        // Intentar primero con PlayerHealthSystem
        var playerHealth = target.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Debug.Log($"[EnemyProjectile] Daño aplicado: {damage} (PlayerHealthSystem)");
            return;
        }

        // Si no tiene PlayerHealthSystem, intentar con IDamageable
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            Debug.Log($"[EnemyProjectile] Daño aplicado: {damage} (IDamageable)");
            return;
        }

        Debug.LogWarning($"[EnemyProjectile] No se pudo aplicar daño a {target.name}");
    }

    public void DestroyProjectile()
    {
        // Detener movimiento antes de destruir
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Destruir cualquier VFX registrado/adjunto
        if (_attachedVfx != null)
        {
            for (int i = _attachedVfx.Count - 1; i >= 0; i--)
            {
                var go = _attachedVfx[i];
                _attachedVfx.RemoveAt(i);
                if (go == null) continue;
                try { Destroy(go); } catch { }
            }
        }

        // Efecto visual de impacto
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // Destruir el proyectil
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Salvaguarda: si se destruye por lifetime o desde fuera, limpiar VFX registrados
        if (_attachedVfx != null)
        {
            for (int i = _attachedVfx.Count - 1; i >= 0; i--)
            {
                var go = _attachedVfx[i];
                _attachedVfx.RemoveAt(i);
                if (go == null) continue;
                try { Destroy(go); } catch { }
            }
        }
    }
}
