using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;

[DisallowMultipleComponent]
public class MagicProjectileSpawner : MonoBehaviour
{
    [Header("Listen")]
    [SerializeField] private vThirdPersonController controller;

    [Header("Orígenes (mano izq/dcha/especial)")]
    [SerializeField] private Transform leftOrigin;
    [SerializeField] private Transform rightOrigin;
    [SerializeField] private Transform specialOrigin;

    private MagicSpellSO leftSpell;
    private MagicSpellSO rightSpell;
    private MagicSpellSO specialSpell;

    [Header("Opciones")]
    [Tooltip("Ignora colisiones del proyectil con los colliders del Player.")]
    [SerializeField] private bool ignoreCasterColliders = true;

    [Tooltip("Instigador del disparo (por defecto, este GameObject).")]
    [SerializeField] private GameObject instigatorOverride;

    private readonly List<Collider> _casterCols = new();

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();

        if (ignoreCasterColliders)
            _casterCols.AddRange(GetComponentsInChildren<Collider>(true));

        if (!instigatorOverride) instigatorOverride = gameObject;
    }

    void OnEnable()
    {
        if (controller) controller.OnMagicSlotCast += HandleSlotCast; // 0=L,1=R,2=S
    }

    void OnDisable()
    {
        if (controller) controller.OnMagicSlotCast -= HandleSlotCast;
    }

    private void HandleSlotCast(int slotId)
    {
        var slot = slotId == 0 ? MagicSlot.Left
                 : slotId == 1 ? MagicSlot.Right
                 : MagicSlot.Special;

        var (spell, origin) = GetSpellAndOrigin(slot);
        if (!spell || !spell.prefab) return;

        // Respeta el delay definido en el SO
        StartCoroutine(Co_SpawnAfterDelay(spell, origin));
    }

    private IEnumerator Co_SpawnAfterDelay(MagicSpellSO spell, Transform origin)
    {
        float d = Mathf.Max(0f, spell.castDelaySeconds);
        if (d > 0f) yield return new WaitForSeconds(d); // usa tiempo escalado (anim)

        SpawnNow(spell, origin);
    }

    // === API opcional (si llamas desde Animation Event, pon castDelaySeconds=0) ===
    public void SpawnLeft()    => Spawn(MagicSlot.Left);
    public void SpawnRight()   => Spawn(MagicSlot.Right);
    public void SpawnSpecial() => Spawn(MagicSlot.Special);

    public void SpawnByIndex(int slotIndex)
    {
        var slot = slotIndex == 0 ? MagicSlot.Left
                 : slotIndex == 1 ? MagicSlot.Right
                 : MagicSlot.Special;
        Spawn(slot);
    }

    public void Spawn(MagicSlot slot)
    {
        var (spell, origin) = GetSpellAndOrigin(slot);
        if (!spell || !spell.prefab) return;
        // Si llamas manualmente a Spawn(), también respeta el delay del SO
        StartCoroutine(Co_SpawnAfterDelay(spell, origin));
    }

    public void SpawnNow(MagicSpellSO spell, Transform originOverride = null)
    {
        if (!spell || !spell.prefab) return;

        Transform origin = originOverride ? originOverride : transform;

        // Dirección SIEMPRE la del Player
        Vector3 dir = transform.forward;
        dir = spell.flattenDirection ? Vector3.ProjectOnPlane(dir, Vector3.up).normalized : dir.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;

        // Posición/rotación finales
        Vector3 spawnPos = (origin ? origin.position : transform.position) + dir * spell.forwardOffset;
        Quaternion spawnRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        // VFX de salida
        if (spell.spawnVFX)
            Instantiate(spell.spawnVFX, spawnPos, spawnRt);

        // Instanciar proyectil
        GameObject go = Instantiate(spell.prefab, spawnPos, spawnRt);

        // Ignorar colisiones con el caster
        if (ignoreCasterColliders)
        {
            var projCols = go.GetComponentsInChildren<Collider>(true);
            foreach (var pc in projCols)
            {
                if (!pc) continue;
                foreach (var cc in _casterCols)
                    if (cc && cc.enabled) Physics.IgnoreCollision(pc, cc, true);
            }
        }

        // Config del proyectil (si existe)
        if (go.TryGetComponent<MagicProjectile>(out var mp))
        {
            var cfg = new MagicProjectile.ProjectileConfig
            {
                damage         = spell.damage,
                aoeRadius      = spell.aoeRadius,
                knockbackForce = spell.knockbackForce,
                hitLayers      = spell.hitLayers,
                destroyOnHit   = spell.destroyOnHit,
                lifeTime       = spell.lifeTime,
                maxRange       = spell.maxRange,
                initialSpeed   = spell.initialSpeed,
                useGravity     = spell.useGravity,
                impactVFX      = spell.impactVFX,
                despawnVFX     = spell.despawnVFX
            };
            mp.Configure(cfg, instigatorOverride ? instigatorOverride : gameObject);
        }

        // Física inicial si hay Rigidbody
        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.useGravity = spell.useGravity;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = dir * Mathf.Max(0f, spell.initialSpeed); // <- corregido
        }
    }

    // === Setters para servicios ===============================================
    public void SetSpells(MagicSpellSO left, MagicSpellSO right, MagicSpellSO special)
    {
        leftSpell = left; rightSpell = right; specialSpell = special;
    }
    public void SetOrigins(Transform left, Transform right, Transform special)
    {
        leftOrigin = left; rightOrigin = right; specialOrigin = special;
    }
    public void SetInstigator(GameObject instigator) => instigatorOverride = instigator;
    public void SetController(vThirdPersonController c) => controller = c;

    // === Helpers ===============================================================
    (MagicSpellSO, Transform) GetSpellAndOrigin(MagicSlot slot)
    {
        switch (slot)
        {
            case MagicSlot.Left:    return (leftSpell,    leftOrigin    ? leftOrigin    : transform);
            case MagicSlot.Right:   return (rightSpell,   rightOrigin   ? rightOrigin   : transform);
            case MagicSlot.Special: return (specialSpell, specialOrigin ? specialOrigin : transform);
            default:                return (null, transform);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!instigatorOverride) instigatorOverride = gameObject;
    }
#endif
}
