using UnityEngine;

[CreateAssetMenu(menuName = "Game/Projectile Settings", fileName = "ProjectileSettings")]
public class ProjectileSettingsSO : ScriptableObject
{
    [Header("Layers Globales")]
    [Tooltip("Capas que reciben daño de los proyectiles (Enemy, Boss, etc.)")]
    public LayerMask damageableLayers = ~0;
    
    [Tooltip("Capas con las que los proyectiles colisionan y explotan (Enemy, Default para árboles, etc.)")]
    public LayerMask collisionLayers = ~0;
    
    [Header("Ignorar Colisiones")]
    [Tooltip("Capas que los proyectiles deben ignorar completamente (Player, PlayerWeapons, etc.)")]
    public LayerMask ignoreCollisionLayers = 0;
}
