using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Ataques/Projectile Settings", fileName = "ProjectileSettings")]
public class ProjectileSettingsSO : ScriptableObject
{
    [Tooltip("Capas que recibirán daño y contra las que colisionarán los proyectiles.")]
    public LayerMask damageableLayers = ~0;
}
