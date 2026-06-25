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
    
    [Header("Audio")]
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al spawnearse el proyectil")]
    [SerializeField] private string spawnSFXKey;
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al impactar/explotar")]
    [SerializeField] private string impactSFXKey;
    
    private Vector3 direction;
    private float damage;
    private bool initialized = false;
    private bool hasHit = false;
    private float _spawnTime;
    private Rigidbody rb;
    private System.Collections.Generic.List<GameObject> _attachedVfx;
    
    private Collider[] _playerDetectionBuffer = new Collider[8];
    private int _playerLayerMask;

    // AoE al impactar (configurado en tiempo de ejecución para lluvia de rocas)
    private bool _dealAoEOnImpact;
    private float _aoeRadius;
    private float _aoeDamage;
    private bool _bypassInvulnerabilityOnHit;

    void Awake()
    {
        // ✅ Configurar el collider - NO forzamos isTrigger para permitir OnCollisionEnter con el player
        var col = GetComponent<SphereCollider>();
        if (col) col.radius = 0.5f;

        _playerLayerMask = LayerMask.GetMask("Player");

        rb = GetComponent<Rigidbody>();
        if (rb)
        {
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.freezeRotation = true;
        }
        
    }

    public void Initialize(Vector3 dir, float dmg)
    {
        direction = dir.normalized;
        damage = dmg;
        initialized = true;
        _spawnTime = Time.time;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[EnemyProjectile] Inicializado — daño: {damage}, velocidad: {speed}");
#endif
        
        // 🔊 Reproducir SFX de spawn
        if (!string.IsNullOrEmpty(spawnSFXKey))
        {
            AudioService.Instance?.PlaySFX(spawnSFXKey, worldPosition: transform.position);
        }
        
        // ✅ Aplicar velocidad inicial usando física
        if (rb)
        {
            rb.linearVelocity = direction * speed;
        }
        
        // Destruir después del tiempo de vida
        Destroy(gameObject, lifetime);
    }
    
    /// <summary>
    /// Activa el daño de área al aterrizar. Usado para rocas de lluvia del Golem.
    /// El AoE ignora iframes pero respeta el escudo del jugador.
    /// </summary>
    public void ConfigureAoE(float radius, float aoeDmg)
    {
        _dealAoEOnImpact = true;
        _aoeRadius = radius;
        _aoeDamage = aoeDmg;
        _bypassInvulnerabilityOnHit = true;
    }

    private void ApplyAoEImpact()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _aoeRadius, _playerDetectionBuffer, _playerLayerMask);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _playerDetectionBuffer[i];

            var shield = hit.GetComponentInParent<PlayerShieldController>();
            if (shield != null && shield.IsDefending)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[EnemyProjectile] 🛡️ AoE de lluvia bloqueado por escudo");
#endif
                continue;
            }

            var playerHealth = hit.GetComponent<PlayerHealthSystem>() ?? hit.GetComponentInParent<PlayerHealthSystem>();
            if (playerHealth != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[EnemyProjectile] 💥 AoE lluvia: {_aoeDamage} daño (ignora iframes)");
#endif
                playerHealth.TakeDamage(_aoeDamage, ignoreInvulnerability: true);
                return;
            }
        }
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
        
        // ✅ Detección activa del player - CharacterController no dispara OnTriggerEnter correctamente
        CheckPlayerProximity();
    }
    
    /// <summary>
    /// Detecta si el proyectil está cerca del player o aliados por distancia directa.
    /// Necesario porque CharacterController no dispara OnTriggerEnter de forma confiable.
    /// También respeta el escudo del jugador y detecta compañeros de party.
    /// </summary>
    private void CheckPlayerProximity()
    {
        if (hasHit) return;

        float detectionRadius = 0.8f;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _playerDetectionBuffer, _playerLayerMask);

        if (hitCount > 0)
        {
            for (int i = 0; i < hitCount; i++)
            {
                var hit = _playerDetectionBuffer[i];

                var playerHealth = hit.GetComponent<PlayerHealthSystem>() ?? hit.GetComponentInParent<PlayerHealthSystem>();
                if (playerHealth != null)
                {
                    // Respetar escudo: si el jugador está defendiendo, bloquear sin dañar
                    var shield = hit.GetComponentInParent<PlayerShieldController>();
                    if (shield != null && shield.IsDefending)
                    {
                        hasHit = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log("[EnemyProjectile] 🛡️ Bloqueado por escudo (proximity)");
#endif
                        DestroyProjectile();
                        return;
                    }

                    hasHit = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[EnemyProjectile] 🎯 Impacto JUGADOR por proximidad: {damage} daño");
#endif
                    playerHealth.TakeDamage(damage, _bypassInvulnerabilityOnHit);
                    DestroyProjectile();
                    return;
                }

                Transform checkTransform = hit.transform;
                for (int j = 0; j < 5 && checkTransform != null; j++)
                {
                    if (checkTransform.CompareTag("Player"))
                    {
                        var shield = checkTransform.GetComponentInChildren<PlayerShieldController>();
                        if (shield != null && shield.IsDefending)
                        {
                            hasHit = true;
                            DestroyProjectile();
                            return;
                        }
                        hasHit = true;
                        ApplyDamage(checkTransform.gameObject);
                        DestroyProjectile();
                        return;
                    }
                    checkTransform = checkTransform.parent;
                }
            }
        }

    }

    // ✅ OnTriggerEnter: PRINCIPAL - Para colisiones con triggers (jugador, obstáculos)
    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        // Grace period: ignorar colisiones inmediatas al spawnear (evita explotar dentro del lanzador)
        if (Time.time - _spawnTime < 0.15f) return;
        
        // Ignorar enemigos y el boss (proyectil de enemy no debe dañar a otros enemies)
        // Nota: "Boss" es un Tag, no un Layer → usar CompareTag, no LayerMask
        if (other.CompareTag("Enemy") || other.CompareTag("Boss") ||
            other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            return;
        }

        // Ignorar el arena del boss (layer TransparentFX)
        if (other.gameObject.layer == LayerMask.NameToLayer("TransparentFX"))
        {
            return;
        }

        // Ignorar layers que no deben detener el proyectil
        if (other.gameObject.layer == LayerMask.NameToLayer("EnemyProjectile") ||
            other.gameObject.layer == LayerMask.NameToLayer("ProjectileEnemy") ||
            other.gameObject.layer == LayerMask.NameToLayer("Interactable") ||
            other.gameObject.layer == LayerMask.NameToLayer("InteractHint") ||
            other.gameObject.layer == LayerMask.NameToLayer("Minimap") ||
            other.gameObject.layer == LayerMask.NameToLayer("UI") ||
            other.gameObject.layer == LayerMask.NameToLayer("PauseUI"))
        {
            return;
        }
        
        // ✅ PRIORIDAD 1: Si impacta contra el escudo del jugador
        if (other.GetComponent<PlayerShieldController.ShieldMarker>() != null)
        {
            hasHit = true;
            DestroyProjectile();
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            hasHit = true;
            ApplyDamage(other.gameObject);
            DestroyProjectile();
            return;
        }

        Transform checkTransform = other.transform;
        for (int i = 0; i < 5; i++)
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
            DestroyProjectile();
            return;
        }

        // Aliados NPC del party: el proyectil los atraviesa sin dañarlos (solo el player recibe daño)
        var partyMember = other.GetComponentInParent<Game.NPC.NPCPartyMember>();
        if (partyMember != null && partyMember.IsInParty)
            return;

        // ✅ Colisión con proyectiles del jugador (layer "Projectile")
        if (other.gameObject.layer == LayerMask.NameToLayer("Projectile"))
        {
            hasHit = true;
            ProjectileCollisionHandler.HandleCollision(other.gameObject, gameObject, other.ClosestPoint(transform.position));
            return;
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("Default"))
        {
            if (other.isTrigger) return;
            hasHit = true;
            if (_dealAoEOnImpact) ApplyAoEImpact();
            DestroyProjectile();
            return;
        }

        if (!other.isTrigger)
        {
            hasHit = true;
            if (_dealAoEOnImpact) ApplyAoEImpact();
            DestroyProjectile();
        }
    }
    
    // OnCollisionEnter: Fallback para colisiones físicas (si el collider NO es trigger)
    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        if (Time.time - _spawnTime < 0.15f) return;

        Collider other = collision.collider;
        OnTriggerEnter(other);
    }

    private void ApplyDamage(GameObject target)
    {
        // GetComponentInParent como fallback por si el collider que recibió el hit es un hijo del root
        var playerHealth = target.GetComponent<PlayerHealthSystem>()
                           ?? target.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, _bypassInvulnerabilityOnHit);
            return;
        }

        var damageable = target.GetComponent<IDamageable>()
                         ?? target.GetComponentInParent<IDamageable>();
        if (damageable != null && damageable.IsAlive)
        {
            damageable.TakeDamage(damage);
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
        
        // 🔊 Reproducir SFX de impacto/explosión
        if (!string.IsNullOrEmpty(impactSFXKey))
        {
            AudioService.Instance?.PlaySFX(impactSFXKey, worldPosition: transform.position);
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
