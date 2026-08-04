﻿﻿using UnityEngine;

/// <summary>
/// Configuración global para las colisiones entre proyectiles del jugador y enemigos.
/// Crea un asset de este tipo en Project > Create > Game > Projectile Collision Settings
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Ataques/Projectile Collision Settings", fileName = "ProjectileCollisionSettings")]
public class ProjectileCollisionSettingsSO : ScriptableObject
{
    [Header("VFX de Colisión")]
    [Tooltip("Prefab del VFX que se reproduce cuando colisionan proyectiles")]
    public GameObject collisionVFX;
    
    [Tooltip("Duración del VFX (0 = autodestrucción del VFX)")]
    [Min(0f)]
    public float vfxLifetime = 2f;
    
    [Header("Fuerzas de Empuje")]
    [Tooltip("Fuerza aplicada al jugador cuando su proyectil colisiona")]
    [Min(0f)]
    public float playerKnockbackForce = 8f;
    
    [Tooltip("Fuerza aplicada al NPC cuando su proyectil colisiona")]
    [Min(0f)]
    public float npcKnockbackForce = 8f;
    
    [Header("Efectos de Cámara")]
    [Tooltip("Intensidad del camera shake al colisionar")]
    [Range(0f, 2f)]
    public float cameraShakeIntensity = 0.5f;
    
    [Tooltip("Duración del camera shake")]
    [Range(0f, 1f)]
    public float cameraShakeDuration = 0.3f;
    
    [Header("Audio")]
    [Tooltip("Clave del SFX para la colisión de proyectiles")]
    public string collisionSFXKey = "ProjectileClash";

    [Header("Lanzamiento Aéreo del Jugador (estilo Kingdom Hearts)")]
    [Tooltip("Distancia horizontal hacia atrás del lanzamiento aéreo del jugador")]
    [Min(0f)]
    public float playerAerialKnockbackDistance = 3.5f;

    [Tooltip("Altura máxima del arco del lanzamiento aéreo del jugador")]
    [Min(0f)]
    public float playerAerialKnockbackHeight = 2.2f;

    [Tooltip("Duración total del lanzamiento aéreo del jugador (subida + caída)")]
    [Min(0.05f)]
    public float playerAerialKnockbackDuration = 0.6f;

    /// <summary>
    /// Convierte este ScriptableObject en la configuración para el handler
    /// </summary>
    public ProjectileCollisionHandler.CollisionConfig ToConfig()
    {
        return new ProjectileCollisionHandler.CollisionConfig
        {
            collisionVFX = collisionVFX,
            vfxLifetime = vfxLifetime,
            playerKnockbackForce = playerKnockbackForce,
            npcKnockbackForce = npcKnockbackForce,
            cameraShakeIntensity = cameraShakeIntensity,
            cameraShakeDuration = cameraShakeDuration,
            collisionSFXKey = collisionSFXKey,
            // Animación solo para el player
            playerCollisionAnimation = "RollBWD_Battle_RM_NoWeapon",
            enableCollisionAnimations = true,
            playerAerialKnockbackDistance = playerAerialKnockbackDistance,
            playerAerialKnockbackHeight = playerAerialKnockbackHeight,
            playerAerialKnockbackDuration = playerAerialKnockbackDuration
        };
    }
}

