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
    [Tooltip("Offset de posición adicional en espacio local (X=derecha, Y=arriba, Z=adelante). Útil para ajustar la altura de spawn y evitar que se vea metido en el suelo.")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 visualRotationOffsetEuler = Vector3.zero;
    public bool    flattenDirection = true;
    [Tooltip("Si se marca, se forzará esta escala al instanciar el proyectil y el VFX de spawn. Si está desmarcado, se usará la escala del prefab.")]
    public bool     useScaleOverride = false;
    public Vector3  scaleOverride = Vector3.one;

    [Header("Costes / CD")]
    public float manaCost = 5f;
    public float cooldown = 0.25f;

    [Header("Levitación (solo para MagicKind.Levitation)")]
    [Tooltip("Rango máximo para detectar y afectar NPCs con levitación.")]
    [Min(0f)] public float levitationRange = 10f;
    [Tooltip("Ángulo de detección en grados (cono frontal del jugador).")]
    [Range(1f, 180f)] public float levitationAngle = 45f;
    [Tooltip("Fuerza de atracción hacia el jugador durante la fase de carga (mantener botón).")]
    [Min(0f)] public float levitationPullForce = 5f;
    [Tooltip("Fuerza de repulsión al soltar el botón.")]
    [Min(0f)] public float levitationPushForce = 15f;
    [Tooltip("Distancia delante del jugador donde se posiciona el NPC levitado.")]
    [Min(1f)] public float levitationHoldDistance = 3f;
    [Tooltip("Altura a la que se eleva el NPC durante la levitación.")]
    [Min(0f)] public float levitationHeight = 2f;
    [Tooltip("Velocidad de elevación del NPC.")]
    [Min(0f)] public float levitationLiftSpeed = 3f;
    [Tooltip("Layers que pueden ser afectados por levitación.")]
    public LayerMask levitationTargetLayers = ~0;
    [Tooltip("Tiempo en segundos antes de empezar a drenar maná mientras se mantiene la levitación.")]
    [Min(0f)] public float levitationDrainDelay = 1f;
    [Tooltip("Maná drenado por segundo mientras se mantiene la levitación (después del delay).")]
    [Min(0f)] public float levitationManaDrainPerSecond = 5f;
    
    [Header("Levitación - VFX")]
    [Tooltip("VFX que aparece en el jugador mientras mantiene la levitación.")]
    public GameObject levitationHoldVFX;
    [Tooltip("VFX que aparece al soltar/lanzar el NPC.")]
    public GameObject levitationReleaseVFX;
    [Tooltip("VFX para mostrar el área de detección (círculos de purpurina).")]
    public GameObject levitationRangeIndicatorVFX;
    [Tooltip("Cantidad de círculos de VFX para el indicador de rango.")]
    [Range(1, 10)] public int rangeIndicatorCount = 3;
    
    [Header("Levitación - Feedback")]
    [Tooltip("Intensidad del camera shake al capturar un NPC.")]
    [Min(0f)] public float levitationCaptureShakeIntensity = 0.3f;
    [Tooltip("Duración del camera shake al capturar.")]
    [Min(0f)] public float levitationCaptureShakeDuration = 0.2f;
    [Tooltip("Intensidad del camera shake al soltar/lanzar un NPC.")]
    [Min(0f)] public float levitationReleaseShakeIntensity = 0.5f;
    [Tooltip("Duración del camera shake al soltar.")]
    [Min(0f)] public float levitationReleaseShakeDuration = 0.3f;

    [Header("Levitación - Daño de Impacto")]
    [Tooltip("Daño base aplicado al NPC al impactar contra una superficie tras ser lanzado.")]
    [Min(0f)] public float levitationImpactDamage = 25f;
    [Tooltip("Velocidad mínima de impacto (m/s) para que se aplique daño. Por debajo de este valor no hay daño.")]
    [Min(0f)] public float levitationImpactMinSpeed = 4f;

    [Header("VFX (centralizado)")]
    public GameObject spawnVFX;
    public GameObject impactVFX;
    public GameObject despawnVFX;
    [Tooltip("Tiempo en segundos antes de destruir los VFX automáticamente. Si es 0, no se destruirán (para VFX con ParticleSystem que se autodestruyen).")]
    [Min(0f)] public float vfxLifetime = 3f;
    
    [Header("Audio")]
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al lanzar el hechizo (ej: 'Spell_Fire', 'Spell_Ice')")]
    public string castSFXKey;
    [Tooltip("Clave del SFX en AudioGraphProfile para reproducir al impactar/explotar (ej: 'Spell_Fire_Explosion')")]
    public string impactSFXKey;

    [Header("Reglas de slot")]
    public SpellSlotType slotType = SpellSlotType.Any;

    [Header("UI")]
    [Tooltip("Icono que se mostrará en el HUD cuando este hechizo esté equipado.")]
    public Sprite attackIcon;
}
