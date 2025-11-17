using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;

[DisallowMultipleComponent]
public class MagicProjectileSpawner : MonoBehaviour
{
    [Header("Listen")]
    [SerializeField] private vThirdPersonController controller;
    [SerializeField] private PlayerTargeting targeting;  // <- NUEVO
    
    [Header("Configuración Global")]
    [SerializeField] private ProjectileSettingsSO projectileSettings;

    [Header("Orígenes (mano izq/dcha/especial)")]
    [SerializeField] private Transform leftOrigin;
    [SerializeField] private Transform rightOrigin;
    [SerializeField] private Transform specialOrigin;

    private MagicSpellSO leftSpell, rightSpell, specialSpell;

    [Header("Opciones")]
    [SerializeField] private bool ignoreCasterColliders = true;
    [SerializeField] private GameObject instigatorOverride;

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!targeting)  targeting  = GetComponentInParent<PlayerTargeting>();
        if (!instigatorOverride) instigatorOverride = gameObject;
    }

    void IgnoreCollisionsBetween(GameObject projectile, GameObject instigator)
    {
        if (!ignoreCasterColliders || projectile == null || instigator == null) return;

        // Obtener TODOS los colliders del proyectil
        var projCols = projectile.GetComponentsInChildren<Collider>(true);
        
        // Obtener TODOS los colliders del instigator (jugador) y sus hijos
        var instigatorCols = instigator.GetComponentsInChildren<Collider>(true);

        // CRITICO: Deshabilitar colliders del proyectil temporalmente
        foreach (var pc in projCols)
        {
            if (pc) pc.enabled = false;
        }

        // Ignorar colisiones entre todos ellos
        foreach (var pc in projCols)
        {
            if (!pc) continue;
            foreach (var ic in instigatorCols)
            {
                if (ic)
                    Physics.IgnoreCollision(pc, ic, true);
            }
        }

        // Reactivar colliders después de un frame (asegurar que la física procese la ignoración)
        StartCoroutine(ReenableCollidersNextFrame(projCols));
    }

    System.Collections.IEnumerator ReenableCollidersNextFrame(Collider[] colliders)
    {
        yield return new WaitForFixedUpdate();
        foreach (var pc in colliders)
        {
            if (pc) pc.enabled = true;
        }
    }

    (LayerMask hitLayers, LayerMask collisionLayers) GetProjectileLayers(MagicSpellSO spell)
    {
        if (projectileSettings != null)
        {
            return (projectileSettings.damageableLayers, projectileSettings.collisionLayers);
        }
        // Fallback si no hay ProjectileSettings configurado (usar todas las capas)
        return (LayerMask.GetMask("Enemy", "Boss"), LayerMask.GetMask("Enemy", "Boss", "Default"));
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

        StartCoroutine(Co_SpawnAfterDelay(spell, origin));
    }

    private IEnumerator Co_SpawnAfterDelay(MagicSpellSO spell, Transform origin)
    {
        float d = Mathf.Max(0f, spell.castDelaySeconds);
        if (d > 0f) yield return new WaitForSeconds(d);
        SpawnNow(spell, origin);
    }

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
        StartCoroutine(Co_SpawnAfterDelay(spell, origin));
    }

    public void SpawnNow(MagicSpellSO spell, Transform originOverride = null)
    {
        if (!spell || !spell.prefab) return;

        Transform origin = originOverride ? originOverride : transform;

        // Si el hechizo tiene tiempo de carga, usar la coroutine especial
        if (spell.chargeTime > 0f)
        {
            StartCoroutine(Co_ChargeAndLaunch(spell, origin));
            return;
        }

        // Lanzamiento inmediato sin carga
        LaunchProjectile(spell, origin, null);
    }

    private IEnumerator Co_ChargeAndLaunch(MagicSpellSO spell, Transform origin)
    {
        // === Dirección inicial ===
        Vector3 baseForward = transform.forward;
        Vector3 dir = (targeting != null)
            ? targeting.GetAimDirectionFrom(origin ? origin : transform, baseForward)
            : baseForward;

        dir = spell.flattenDirection ? Vector3.ProjectOnPlane(dir, Vector3.up).normalized : dir.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = baseForward;

        // Posición/rotación iniciales (en la mano)
        Vector3 spawnPos = (origin ? origin.position : transform.position) + dir * spell.forwardOffset;
        Quaternion spawnRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        // Spawn VFX de inicio
        if (spell.spawnVFX)
        {
            var fx = Instantiate(spell.spawnVFX, spawnPos, spawnRt);
            if (spell.useScaleOverride)
                fx.transform.localScale = spell.scaleOverride;
        }

        // Instanciar proyectil con escala inicial pequeña
        GameObject go = Instantiate(spell.prefab, spawnPos, spawnRt);
        Vector3 targetScale = spell.useScaleOverride ? spell.scaleOverride : go.transform.localScale;
        go.transform.localScale = targetScale * spell.chargeStartScale;

        // Configurar colisiones - ignorar jugador y todos sus hijos
        GameObject instigator = instigatorOverride ? instigatorOverride : gameObject;
        IgnoreCollisionsBetween(go, instigator);

        // Configurar proyectil pero sin velocidad aún
        if (go.TryGetComponent<MagicProjectile>(out var mp))
        {
            var (hitLayers, collisionLayers) = GetProjectileLayers(spell);
            var cfg = new MagicProjectile.ProjectileConfig
            {
                damage         = spell.damage,
                aoeRadius      = spell.aoeRadius,
                knockbackForce = spell.knockbackForce,
                hitLayers      = hitLayers,
                collisionLayers = collisionLayers,
                destroyOnHit   = spell.destroyOnHit,
                lifeTime       = spell.lifeTime,
                maxRange       = spell.maxRange,
                initialSpeed   = 0f, // Sin velocidad durante la carga
                useGravity     = false, // Sin gravedad durante la carga
                impactVFX      = spell.impactVFX,
                despawnVFX     = spell.despawnVFX
            };
            mp.Configure(cfg, instigatorOverride ? instigatorOverride : gameObject);
        }

        // Si hay Rigidbody, hacerlo cinemático durante la carga
        Rigidbody rb = go.GetComponent<Rigidbody>();
        bool hadRb = rb != null;
        if (hadRb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
        }

        // === Fase de carga: crecer y seguir la mano ===
        float elapsed = 0f;
        while (elapsed < spell.chargeTime)
        {
            if (go == null) yield break; // Destruido prematuramente
            
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spell.chargeTime);
            
            // Interpolación de escala
            float scale = Mathf.Lerp(spell.chargeStartScale, 1f, t);
            go.transform.localScale = targetScale * scale;
            
            // Seguir la posición del origin si está habilitado
            if (spell.followOriginDuringCharge && origin != null)
            {
                Vector3 currentDir = (targeting != null)
                    ? targeting.GetAimDirectionFrom(origin, baseForward)
                    : transform.forward;
                
                currentDir = spell.flattenDirection 
                    ? Vector3.ProjectOnPlane(currentDir, Vector3.up).normalized 
                    : currentDir.normalized;
                
                if (currentDir.sqrMagnitude < 0.001f) currentDir = baseForward;
                
                go.transform.position = origin.position + currentDir * spell.forwardOffset;
                go.transform.rotation = Quaternion.LookRotation(currentDir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);
            }
            
            yield return null;
        }

        if (go == null) yield break;

        // === Lanzamiento final ===
        // Recalcular dirección final
        Vector3 finalDir = (targeting != null)
            ? targeting.GetAimDirectionFrom(origin ? origin : transform, baseForward)
            : transform.forward;
        
        finalDir = spell.flattenDirection 
            ? Vector3.ProjectOnPlane(finalDir, Vector3.up).normalized 
            : finalDir.normalized;
        
        if (finalDir.sqrMagnitude < 0.001f) finalDir = baseForward;
        
        go.transform.rotation = Quaternion.LookRotation(finalDir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);
        go.transform.localScale = targetScale; // Escala final

        // Activar física
        if (mp != null)
        {
            var (hitLayers, collisionLayers) = GetProjectileLayers(spell);
            // Actualizar config con velocidad y gravedad reales
            var finalCfg = new MagicProjectile.ProjectileConfig
            {
                damage         = spell.damage,
                aoeRadius      = spell.aoeRadius,
                knockbackForce = spell.knockbackForce,
                hitLayers      = hitLayers,
                collisionLayers = collisionLayers,
                destroyOnHit   = spell.destroyOnHit,
                lifeTime       = spell.lifeTime,
                maxRange       = spell.maxRange,
                initialSpeed   = spell.initialSpeed,
                useGravity     = spell.useGravity,
                impactVFX      = spell.impactVFX,
                despawnVFX     = spell.despawnVFX
            };
            mp.Configure(finalCfg, instigatorOverride ? instigatorOverride : gameObject);
        }

        if (hadRb && rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = spell.useGravity;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // Evitar giros al colisionar
            rb.linearVelocity = finalDir * Mathf.Max(0f, spell.initialSpeed);
        }
    }

    private void LaunchProjectile(MagicSpellSO spell, Transform origin, Vector3? directionOverride)
    {
        if (!spell || !spell.prefab) return;

        // === Dirección: si hay targeting activo, usa la dirección de APUNTADO ===
        Vector3 baseForward = transform.forward;
        Vector3 dir = directionOverride ?? ((targeting != null)
            ? targeting.GetAimDirectionFrom(origin ? origin : transform, baseForward)
            : baseForward);

        // Respeta la nivelación definida por el hechizo
        dir = spell.flattenDirection ? Vector3.ProjectOnPlane(dir, Vector3.up).normalized : dir.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = baseForward;

        // Posición/rotación finales
        Vector3 spawnPos = (origin ? origin.position : transform.position) + dir * spell.forwardOffset;
        Quaternion spawnRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        if (spell.spawnVFX)
        {
            var fx = Instantiate(spell.spawnVFX, spawnPos, spawnRt);
            if (spell.useScaleOverride)
            {
                fx.transform.localScale = spell.scaleOverride;
            }
        }

        GameObject go = Instantiate(spell.prefab, spawnPos, spawnRt);
        if (spell.useScaleOverride)
        {
            go.transform.localScale = spell.scaleOverride;
        }

        // Configurar colisiones - ignorar jugador y todos sus hijos
        GameObject instigator = instigatorOverride ? instigatorOverride : gameObject;
        IgnoreCollisionsBetween(go, instigator);

        if (go.TryGetComponent<MagicProjectile>(out var mp))
        {
            var (hitLayers, collisionLayers) = GetProjectileLayers(spell);
            var cfg = new MagicProjectile.ProjectileConfig
            {
                damage         = spell.damage,
                aoeRadius      = spell.aoeRadius,
                knockbackForce = spell.knockbackForce,
                hitLayers      = hitLayers,
                collisionLayers = collisionLayers,
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

        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.useGravity = spell.useGravity;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // Evitar giros al colisionar
            rb.linearVelocity = dir * Mathf.Max(0f, spell.initialSpeed);
        }
    }

    // === Setters para servicios ===============================================
    public void SetSpells(MagicSpellSO left, MagicSpellSO right, MagicSpellSO special)
    { leftSpell = left; rightSpell = right; specialSpell = special; }

    public void SetOrigins(Transform left, Transform right, Transform special)
    { leftOrigin = left; rightOrigin = right; specialOrigin = special; }

    public void SetInstigator(GameObject instigator) => instigatorOverride = instigator;

    public void SetController(vThirdPersonController c)
    {
        if (controller) controller.OnMagicSlotCast -= HandleSlotCast;
        controller = c;
        if (controller) controller.OnMagicSlotCast += HandleSlotCast;
    }

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
        if (!targeting)  targeting  = GetComponentInParent<PlayerTargeting>();
        if (!instigatorOverride) instigatorOverride = gameObject;
    }
#endif
}
