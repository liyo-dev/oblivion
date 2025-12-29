using UnityEngine;

/// <summary>
/// Sistema que maneja las colisiones entre proyectiles del jugador y enemigos.
/// Cuando dos proyectiles colisionan, se empujan mutuamente, reproducen VFX y se destruyen.
/// </summary>
public static class ProjectileCollisionHandler
{
    /// <summary>
    /// Configuración global de colisiones entre proyectiles
    /// </summary>
    [System.Serializable]
    public class CollisionConfig
    {
        [Header("Fuerzas de Empuje")]
        [Tooltip("Fuerza aplicada al jugador cuando su proyectil colisiona")]
        public float playerKnockbackForce = 8f;
        
        [Tooltip("Fuerza aplicada al NPC cuando su proyectil colisiona")]
        public float npcKnockbackForce = 8f;
        
        [Header("VFX")]
        [Tooltip("Prefab del VFX que se reproduce en el punto de colisión")]
        public GameObject collisionVFX;
        
        [Tooltip("Duración del VFX (0 = autodestrucción del VFX)")]
        public float vfxLifetime = 2f;
        
        [Header("Efectos de Cámara")]
        [Tooltip("Intensidad del camera shake al colisionar")]
        public float cameraShakeIntensity = 0.5f;
        
        [Tooltip("Duración del camera shake")]
        public float cameraShakeDuration = 0.3f;
        
        [Header("Audio")]
        [Tooltip("Clave del SFX para la colisión de proyectiles")]
        public string collisionSFXKey = "ProjectileClash";
    }
    
    private static CollisionConfig _config;
    
    /// <summary>
    /// Inicializa la configuración de colisiones. Llamar desde un ScriptableObject o Manager.
    /// </summary>
    public static void Initialize(CollisionConfig config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Obtiene la configuración actual o una por defecto
    /// </summary>
    private static CollisionConfig GetConfig()
    {
        if (_config == null)
        {
            // Configuración por defecto
            _config = new CollisionConfig
            {
                playerKnockbackForce = 8f,
                npcKnockbackForce = 8f,
                vfxLifetime = 2f,
                cameraShakeIntensity = 0.5f,
                cameraShakeDuration = 0.3f,
                collisionSFXKey = "ProjectileClash"
            };
        }
        return _config;
    }
    
    /// <summary>
    /// Maneja la colisión entre un proyectil del jugador y uno del enemigo
    /// </summary>
    /// <param name="playerProjectile">Proyectil del jugador (layer "Projectile")</param>
    /// <param name="enemyProjectile">Proyectil del enemigo (layer "ProjectileEnemy")</param>
    /// <param name="collisionPoint">Punto donde colisionaron</param>
    public static void HandleCollision(
        GameObject playerProjectile, 
        GameObject enemyProjectile, 
        Vector3 collisionPoint)
    {
        var config = GetConfig();
        
        Debug.Log($"[ProjectileCollision] 💥 Colisión detectada en {collisionPoint}");
        
        // 1. Reproducir VFX en el punto de colisión
        SpawnCollisionVFX(collisionPoint, config);
        
        // 2. Reproducir SFX
        PlayCollisionSound(config);
        
        // 3. Camera shake para feedback
        ApplyCameraShake(config);
        
        // 4. Aplicar knockback a jugador y NPC
        ApplyKnockbackToEntities(playerProjectile, enemyProjectile, collisionPoint, config);
        
        // 5. Destruir ambos proyectiles
        DestroyProjectiles(playerProjectile, enemyProjectile);
    }
    
    private static void SpawnCollisionVFX(Vector3 position, CollisionConfig config)
    {
        if (config.collisionVFX == null)
        {
            Debug.LogWarning("[ProjectileCollision] No hay VFX configurado para la colisión");
            return;
        }
        
        GameObject vfx = Object.Instantiate(config.collisionVFX, position, Quaternion.identity);
        
        // 🔥 CORRECCIÓN: Siempre destruir el VFX después de un tiempo
        float destroyTime = config.vfxLifetime > 0f ? config.vfxLifetime : 3f; // 3s por defecto
        Object.Destroy(vfx, destroyTime);
        
        Debug.Log($"[ProjectileCollision] ✨ VFX spawneado en {position}");
    }
    
    private static void PlayCollisionSound(CollisionConfig config)
    {
        if (string.IsNullOrEmpty(config.collisionSFXKey))
            return;
        
        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(config.collisionSFXKey);
            Debug.Log($"[ProjectileCollision] 🔊 SFX reproducido: {config.collisionSFXKey}");
        }
    }
    
    private static void ApplyCameraShake(CollisionConfig config)
    {
        if (config.cameraShakeIntensity > 0f && config.cameraShakeDuration > 0f)
        {
            Sendero.Core.Feedback.FeedbackService.CameraShake(
                config.cameraShakeIntensity, 
                config.cameraShakeDuration);
            
            Debug.Log($"[ProjectileCollision] 📹 Camera shake aplicado: {config.cameraShakeIntensity}");
        }
    }
    
    private static void ApplyKnockbackToEntities(
        GameObject playerProjectile, 
        GameObject enemyProjectile, 
        Vector3 collisionPoint, 
        CollisionConfig config)
    {
        // Dirección: del punto de colisión hacia cada proyectil
        Vector3 playerDirection = (playerProjectile.transform.position - collisionPoint).normalized;
        Vector3 enemyDirection = (enemyProjectile.transform.position - collisionPoint).normalized;
        
        // Aplicar knockback al JUGADOR (propietario del proyectil del jugador)
        ApplyKnockbackToPlayer(playerDirection, config.playerKnockbackForce);
        
        // Aplicar knockback al NPC (propietario del proyectil del enemigo)
        ApplyKnockbackToNPC(enemyProjectile, enemyDirection, config.npcKnockbackForce);
    }
    
    private static void ApplyKnockbackToPlayer(Vector3 direction, float force)
    {
        // Buscar el jugador
        if (!PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) || playerGo == null)
        {
            Debug.LogWarning("[ProjectileCollision] No se encontró el jugador para aplicar knockback");
            return;
        }
        
        // Intentar obtener el Rigidbody del jugador
        var rb = playerGo.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            // Aplicar fuerza horizontal (sin componente Y para no lanzar al aire)
            Vector3 knockback = direction;
            knockback.y = 0f;
            knockback = knockback.normalized * force;
            
            rb.AddForce(knockback, ForceMode.Impulse);
            Debug.Log($"[ProjectileCollision] 👊 Knockback aplicado al jugador: {knockback}");
        }
        else
        {
            Debug.LogWarning("[ProjectileCollision] El jugador no tiene Rigidbody o es kinematic");
        }
    }
    
    private static void ApplyKnockbackToNPC(GameObject enemyProjectile, Vector3 direction, float force)
    {
        // El proyectil del enemigo debería tener referencia a su instigador
        // Intentar encontrar el NPC que disparó el proyectil
        
        // Opción 1: Buscar en el tag del proyectil o en su nombre
        // Opción 2: Los proyectiles deberían tener una referencia al instigador
        
        // Por ahora, buscar NPCs cercanos al proyectil
        Collider[] nearbyColliders = Physics.OverlapSphere(
            enemyProjectile.transform.position, 
            30f, 
            LayerMask.GetMask("Enemy", "Boss"));
        
        GameObject closestNPC = null;
        float closestDistance = float.MaxValue;
        
        foreach (var col in nearbyColliders)
        {
            float dist = Vector3.Distance(col.transform.position, enemyProjectile.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestNPC = col.gameObject;
            }
        }
        
        if (closestNPC != null)
        {
            var rb = closestNPC.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                // Aplicar fuerza horizontal
                Vector3 knockback = direction;
                knockback.y = 0f;
                knockback = knockback.normalized * force;
                
                rb.AddForce(knockback, ForceMode.Impulse);
                Debug.Log($"[ProjectileCollision] 👊 Knockback aplicado al NPC '{closestNPC.name}': {knockback}");
            }
            else
            {
                Debug.LogWarning($"[ProjectileCollision] El NPC '{closestNPC.name}' no tiene Rigidbody o es kinematic");
            }
        }
        else
        {
            Debug.LogWarning("[ProjectileCollision] No se encontró NPC cercano para aplicar knockback");
        }
    }
    
    private static void DestroyProjectiles(GameObject playerProjectile, GameObject enemyProjectile)
    {
        if (playerProjectile != null)
        {
            var magicProj = playerProjectile.GetComponent<MagicProjectile>();
            if (magicProj != null)
            {
                magicProj.End(true); // byImpact = true
            }
            else
            {
                Object.Destroy(playerProjectile);
            }
            
            Debug.Log("[ProjectileCollision] 💀 Proyectil del jugador destruido");
        }
        
        if (enemyProjectile != null)
        {
            var enemyProj = enemyProjectile.GetComponent<EnemyProjectile>();
            if (enemyProj != null)
            {
                enemyProj.DestroyProjectile();
            }
            else
            {
                Object.Destroy(enemyProjectile);
            }
            
            Debug.Log("[ProjectileCollision] 💀 Proyectil del enemigo destruido");
        }
    }
}

