using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Spell", fileName = "NewMagicSpell")]
public class MagicSpellSO : ScriptableObject
{
    [Header("Identidad")]
    public string     displayName = "Fireball";
    public MagicKind  kind        = MagicKind.Projectile; 
    public MagicElement element   = MagicElement.Fire;  
    [Header("Prefab en vuelo")]
    public GameObject prefab;
    
    [Header("Casting")]
    [Tooltip("Retrasa el disparo para sincronizar con la animación (segundos).")]
    [Min(0f)] public float castDelaySeconds = 0.15f;

    [Header("Física / Vida")]
    public float initialSpeed = 18f;
    public bool  useGravity   = false;
    public float maxRange     = 40f;
    public float lifeTime     = 8f;

    [Header("Daño / Impacto")]
    public float     damage = 10f;
    public float     aoeRadius = 0f;
    public float     knockbackForce = 0f;
    public LayerMask hitLayers = ~0;
    public bool      destroyOnHit = true;

    [Header("Spawn / Dirección")]
    public float   forwardOffset = 0.35f;
    public Vector3 visualRotationOffsetEuler = Vector3.zero;
    public bool    flattenDirection = true;

    [Header("Costes / CD")]
    public float manaCost = 5f;
    public float cooldown = 0.25f;

    [Header("VFX (centralizado)")]
    public GameObject spawnVFX;
    public GameObject impactVFX;
    public GameObject despawnVFX;

    [Header("Reglas de slot")]
    public SpellSlotType slotType = SpellSlotType.Any; 
}