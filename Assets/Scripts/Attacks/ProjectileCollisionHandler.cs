﻿using UnityEngine;
using DG.Tweening;

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
        
        [Header("Animaciones")]
        [Tooltip("Nombre de la animación que se reproduce en el jugador al colisionar")]
        public string playerCollisionAnimation = "RollBWD_Battle_RM_NoWeapon";
        
        [Tooltip("Habilitar reproducción de animaciones al colisionar")]
        public bool enableCollisionAnimations = true;
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
                collisionSFXKey = "ProjectileClash",
                playerCollisionAnimation = "RollBWD_Battle_RM_NoWeapon",
                enableCollisionAnimations = true
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
        
        // 4. Reproducir animaciones en jugador y NPC
        if (config.enableCollisionAnimations)
        {
            PlayCollisionAnimations(config);
        }
        
        // 5. Aplicar knockback a jugador y NPC
        ApplyKnockbackToEntities(playerProjectile, enemyProjectile, collisionPoint, config);
        
        // 6. Destruir ambos proyectiles
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
    
    private static void PlayCollisionAnimations(CollisionConfig config)
    {
        Debug.Log($"[ProjectileCollision] 🎬 PlayCollisionAnimations INICIADO");
        Debug.Log($"[ProjectileCollision]   - Player animation: '{config.playerCollisionAnimation}'");
        
        // ⭐ Solo reproducir animación de roll en el JUGADOR
        // El NPC NO hará animación de roll cuando los proyectiles colisionen
        if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
        {
            Debug.Log($"[ProjectileCollision] ✅ Player encontrado: '{playerGo.name}'");
            PlayAnimationOnTarget(playerGo, config.playerCollisionAnimation, "Player");
        }
        else
        {
            Debug.LogWarning($"[ProjectileCollision] ⚠️ Player NO encontrado");
        }
    }
    
    /// <summary>
    /// Reproduce una animación en un GameObject objetivo (Player o NPC)
    /// </summary>
    private static void PlayAnimationOnTarget(GameObject target, string animationName, string targetType)
    {
        Debug.Log($"[ProjectileCollision] 🎬 PlayAnimationOnTarget INICIADO para {targetType}");
        Debug.Log($"[ProjectileCollision]   - Target: {(target != null ? $"'{target.name}'" : "NULL")}");
        Debug.Log($"[ProjectileCollision]   - Animation: '{animationName}'");
        
        if (string.IsNullOrEmpty(animationName))
        {
            Debug.LogWarning($"[ProjectileCollision] ⚠️ No hay animación configurada para {targetType}");
            return;
        }
        
        if (target == null)
        {
            Debug.LogWarning($"[ProjectileCollision] ⚠️ Target {targetType} es null");
            return;
        }
        
        // Obtener el Animator del target
        var animator = target.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.Log($"[ProjectileCollision] No hay Animator en root, buscando en hijos...");
            animator = target.GetComponentInChildren<Animator>();
        }
        
        if (animator == null)
        {
            Debug.LogError($"[ProjectileCollision] ❌ {targetType} '{target.name}' NO TIENE ANIMATOR en ninguna parte");
            return;
        }
        
        Debug.Log($"[ProjectileCollision] ✅ Animator encontrado en '{animator.gameObject.name}'");
        Debug.Log($"[ProjectileCollision]   - Animator enabled: {animator.enabled}");
        Debug.Log($"[ProjectileCollision]   - Animator isActiveAndEnabled: {animator.isActiveAndEnabled}");
        
        // Reproducir la animación usando PlayInFixedTime para forzar reproducción inmediata
        animator.PlayInFixedTime(animationName, 0, 0f);
        
        Debug.Log($"[ProjectileCollision] 🎬✅ Animación '{animationName}' REPRODUCIDA en {targetType} '{target.name}'");
        
        // ✅ Aplicar desplazamiento físico hacia atrás (para compensar si no hay Root Motion)
        ApplyKnockbackMovement(target, targetType);
    }
    
    /// <summary>
    /// Aplica un desplazamiento hacia atrás al objetivo (para que el roll se vea físicamente)
    /// </summary>
    private static void ApplyKnockbackMovement(GameObject target, string targetType)
    {
        const float knockbackDistance = 3f; // Metros hacia atrás
        const float knockbackDuration = 0.5f; // Duración del desplazamiento
        
        // Dirección hacia atrás del objetivo
        Vector3 backwardDirection = -target.transform.forward;
        Vector3 targetPos = target.transform.position + (backwardDirection * knockbackDistance);
        
        // Intentar mover con NavMeshAgent si existe (NPCs)
        var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            Debug.Log($"[ProjectileCollision] 🏃 Aplicando desplazamiento a {targetType} con NavMeshAgent");
            
            // Validar posición en NavMesh
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out var hit, knockbackDistance + 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }
            
            // Desactivar temporalmente el agente y moverlo con DOTween
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
            
            target.transform.DOMove(targetPos, knockbackDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    if (agent != null && agent.isActiveAndEnabled)
                    {
                        agent.isStopped = false;
                        agent.updatePosition = true;
                        agent.updateRotation = true;
                    }
                });
            
            return;
        }
        
        // Si no hay NavMeshAgent, intentar con CharacterController (Player)
        var controller = target.GetComponent<CharacterController>();
        if (controller != null)
        {
            Debug.Log($"[ProjectileCollision] 🏃 Aplicando desplazamiento a {targetType} con CharacterController");
            
            // Mover el transform directamente (CharacterController se ajustará)
            target.transform.DOMove(targetPos, knockbackDuration).SetEase(Ease.OutQuad);
            return;
        }
        
        // Si no hay NavMeshAgent ni CharacterController, intentar con Rigidbody
        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"[ProjectileCollision] 🏃 Aplicando impulso a {targetType} con Rigidbody");
            rb.AddForce(backwardDirection * knockbackDistance * 2f, ForceMode.Impulse);
            return;
        }
        
        // Último recurso: mover el transform directamente con DOTween
        Debug.Log($"[ProjectileCollision] 🏃 Moviendo {targetType} directamente con Transform");
        target.transform.DOMove(targetPos, knockbackDuration).SetEase(Ease.OutQuad);
    }
    
    /// <summary>
    /// Busca el NPC más cercano al proyectil enemigo usando el registro de combate activo
    /// ⭐ DEPRECATED - Ya no se usa porque el NPC no hace roll
    /// </summary>
    [System.Obsolete("Ya no se usa - el NPC no hace animación de roll")]
    private static GameObject FindNearbyNPC(GameObject enemyProjectile)
    {
        if (enemyProjectile == null)
        {
            Debug.LogWarning("[ProjectileCollision] ⚠️ Proyectil enemigo es null, no se puede buscar NPC");
            return null;
        }
        
        Debug.Log($"[ProjectileCollision] 🔍 FindNearbyNPC: Buscando NPC en combate cercano a '{enemyProjectile.name}'");
        Debug.Log($"[ProjectileCollision]   - Posición proyectil: {enemyProjectile.transform.position}");
        Debug.Log($"[ProjectileCollision]   - NPCs registrados en combate: {ActiveCombatRegistry.Count}");
        
        // ✅ Usar el registro de combate para encontrar NPCs activos
        GameObject npc = ActiveCombatRegistry.GetClosestCombatNPC(
            enemyProjectile.transform.position, 
            maxDistance: 50f
        );
        
        if (npc != null)
        {
            float dist = Vector3.Distance(npc.transform.position, enemyProjectile.transform.position);
            Debug.Log($"[ProjectileCollision] ✅ NPC en combate encontrado: '{npc.name}' a {dist:F1}m");
            Debug.Log($"[ProjectileCollision]   - Posición NPC: {npc.transform.position}");
        }
        else
        {
            Debug.LogWarning($"[ProjectileCollision] ⚠️ NO hay NPCs en combate cerca del proyectil");
            Debug.LogWarning($"[ProjectileCollision]   - Verifica que el NPC haya llamado a EnterCombat()");
            Debug.LogWarning($"[ProjectileCollision]   - Verifica que el NPC tenga NPCBehaviourManagerV2");
        }
        
        return npc;
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
        // El proyectil del enemigo debería tener referencia real a su instigador (pendiente:
        // requeriría añadir el campo en EnemyProjectile y setearlo en cada AI que lo spawnea).
        // Mientras tanto, usamos el registro de combate activo (mismo patrón que el resto del
        // proyecto, ver ActiveCombatRegistry.GetClosestCombatNPC) en vez de Physics.OverlapSphere:
        // evita la allocation y limita la búsqueda a NPCs realmente en combate, no a cualquier
        // collider en la layer Enemy/Boss en 30m a la redonda.
        GameObject closestNPC = ActiveCombatRegistry.GetClosestCombatNPC(
            enemyProjectile.transform.position,
            maxDistance: 30f);

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

