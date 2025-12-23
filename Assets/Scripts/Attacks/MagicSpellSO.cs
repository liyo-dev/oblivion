using UnityEngine;

[CreateAssetMenu(menuName = "Magic/Spell", fileName = "NewMagicSpell")]
public class MagicSpellSO : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("ID único del hechizo para identificarlo en el sistema.")]
    public SpellId    spellId      = SpellId.None;
    public string     displayName = "Fireball";
    public MagicKind  kind        = MagicKind.Projectile; 
    public MagicElement element   = MagicElement.Fire;  
    [Header("Prefab en vuelo")]
    public GameObject prefab;
    
    [Header("Casting")]
    [Tooltip("Retrasa el disparo para sincronizar con la animación (segundos).")]
    [Min(0f)] public float castDelaySeconds = 0.15f;
    
    [Header("Carga (solo para especiales)")]
    [Tooltip("Si > 0, el proyectil aparecerá en la mano y crecerá durante este tiempo antes de dispararse (estilo Kamehameha).")]
    [Min(0f)] public float chargeTime = 0f;
    [Tooltip("Escala inicial del proyectil durante la carga (0.1 = 10% del tamaño normal).")]
    [Range(0.01f, 1f)] public float chargeStartScale = 0.1f;
    [Tooltip("Si está marcado, el proyectil seguirá la posición del origin durante la carga.")]
    public bool followOriginDuringCharge = true;

    [Header("Física / Vida")]
    public float initialSpeed = 18f;
    public bool  useGravity   = false;
    public float maxRange     = 40f;
    public float lifeTime     = 8f;

    [Header("Daño / Impacto")]
    public float     damage = 10f;
    public float     aoeRadius = 0f;
    public float     knockbackForce = 0f;
    public bool      destroyOnHit = true;

    [Header("Spawn / Dirección")]
    public float   forwardOffset = 0.35f;
    public Vector3 visualRotationOffsetEuler = Vector3.zero;
    public bool    flattenDirection = true;
    [Tooltip("Si se marca, se forzará esta escala al instanciar el proyectil y el VFX de spawn. Si está desmarcado, se usará la escala del prefab.")]
    public bool     useScaleOverride = false;
    public Vector3  scaleOverride = Vector3.one;

    [Header("Costes / CD")]
    public float manaCost = 5f;
    public float cooldown = 0.25f;

    [Header("VFX (centralizado)")]
    public GameObject spawnVFX;
    public GameObject impactVFX;
    public GameObject despawnVFX;
    
    [Header("Audio")]
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al lanzar el hechizo (ej: 'Spell_Fire', 'Spell_Ice')")]
    public string castSFXKey;

    [Header("Reglas de slot")]
    public SpellSlotType slotType = SpellSlotType.Any;

    [Header("UI")]
    [Tooltip("Icono que se mostrará en el HUD cuando este hechizo esté equipado.")]
    public Sprite attackIcon;
}
