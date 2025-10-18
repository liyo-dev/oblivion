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
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool usePhysicsMovement = true; // Usar Rigidbody para movimiento suave
    
    private Vector3 direction;
    private float damage;
    private bool initialized = false;
    private bool hasHit = false;
    private Rigidbody rb;

    void Awake()
    {
        // Configurar el collider automáticamente
        var col = GetComponent<SphereCollider>();
        if (col)
        {
            col.isTrigger = true;
            col.radius = 0.5f;
        }

        // Configurar el rigidbody para movimiento suave
        rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate; // IMPORTANTE para movimiento suave
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // Mejor detección
            
            if (usePhysicsMovement)
            {
                rb.isKinematic = false;
                rb.linearDamping = 0f; // Sin fricción
                rb.angularDamping = 0f;
            }
            else
            {
                rb.isKinematic = true;
            }
        }
    }

    public void Initialize(Vector3 dir, float dmg)
    {
        direction = dir.normalized;
        damage = dmg;
        initialized = true;
        
        Debug.Log($"[DemonProjectile] Inicializado con {dmg} de daño");
        
        // Si usa física, aplicar velocidad inicial
        if (usePhysicsMovement && rb)
        {
            rb.linearVelocity = direction * speed;
        }
        
        // Destruir después del tiempo de vida
        Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        // Usar FixedUpdate para movimiento de física (más suave)
        if (!initialized || hasHit) return;
        
        if (usePhysicsMovement && rb)
        {
            // Mantener la velocidad constante (sin gravedad ni fricción lo afectan)
            rb.linearVelocity = direction * speed;
        }
        else
        {
            // Movimiento manual en FixedUpdate (más suave que Update)
            transform.position += direction * speed * Time.fixedDeltaTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return; // Evitar múltiples hits
        
        Debug.Log($"[DemonProjectile] Colisionó con: {other.gameObject.name}, Tag: {other.tag}, IsTrigger: {other.isTrigger}");
        
        // Ignorar al propio demonio y sus triggers
        if (other.CompareTag("Enemy") || other.gameObject.layer == LayerMask.NameToLayer("Enemy")) 
        {
            return;
        }

        // Aplicar daño si es el jugador (buscar en el objeto o en su padre)
        Transform checkTransform = other.transform;
        for (int i = 0; i < 3; i++) // Revisar hasta 3 niveles de jerarquía
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

        // Si no encontró el tag, intentar buscar el componente directamente
        var playerHealth = other.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(damage);
            Debug.Log($"[DemonProjectile] Daño aplicado: {damage} (encontrado por componente)");
            DestroyProjectile();
            return;
        }

        // Si colisionó con algo sólido que no es el jugador, destruir
        if (!other.isTrigger)
        {
            hasHit = true;
            DestroyProjectile();
        }
    }

    private void ApplyDamage(GameObject target)
    {
        // Intentar primero con PlayerHealthSystem
        var playerHealth = target.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Debug.Log($"[DemonProjectile] Daño aplicado: {damage} (PlayerHealthSystem)");
            return;
        }

        // Si no tiene PlayerHealthSystem, intentar con IDamageable
        var damageable = target.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
            Debug.Log($"[DemonProjectile] Daño aplicado: {damage} (IDamageable)");
            return;
        }

        Debug.LogWarning($"[DemonProjectile] No se pudo aplicar daño a {target.name}");
    }

    private void DestroyProjectile()
    {
        // Detener movimiento antes de destruir
        if (rb)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Efecto visual de impacto
        if (hitEffectPrefab)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // Destruir el proyectil
        Destroy(gameObject);
    }
}
