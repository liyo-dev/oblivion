using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

[DisallowMultipleComponent]
public class MagicProjectileSpawner : MonoBehaviour
{
    /// <summary>
    /// Evento estático que se dispara cuando el jugador lanza un hechizo.
    /// Los compañeros del party pueden suscribirse para entrar en modo alerta/combate.
    /// </summary>
    public static event System.Action OnPlayerAttacked;
    
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

    // ClearSpawnPosition eliminado: ya no se ajusta el spawn dinámicamente

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

    LayerMask GetDamageLayers()
    {
        if (projectileSettings != null)
            return projectileSettings.damageableLayers;

        return LayerMask.GetMask("Enemy", "Boss");
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

        // 🔔 Notificar a compañeros que el jugador atacó
        OnPlayerAttacked?.Invoke();

        StartCoroutine(Co_SpawnAfterDelay(spell, origin));
    }

    private IEnumerator Co_SpawnAfterDelay(MagicSpellSO spell, Transform origin)
    {
        // ⭐ Reproducir SFX INMEDIATAMENTE al iniciar el cast (antes del delay de animación)
        if (!string.IsNullOrEmpty(spell.castSFXKey) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(spell.castSFXKey);
        }
        
        float d = Mathf.Max(0f, spell.castDelaySeconds);
        if (d > 0f) yield return new WaitForSeconds(d);

        if (spell.chargeTime > 0f)
            yield return Co_SpawnWithCharge(spell, origin);
        else
            SpawnNow(spell, origin, playSFX: false); // SFX ya se reprodujo arriba
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

    public void SpawnNow(MagicSpellSO spell, Transform originOverride = null, bool playSFX = true)
    {
        if (!spell || !spell.prefab) return;

        Transform origin = originOverride ? originOverride : transform;
        
        // Reproducir SFX solo si se solicita (para llamadas directas que no pasan por Co_SpawnAfterDelay)
        if (playSFX && !string.IsNullOrEmpty(spell.castSFXKey) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(spell.castSFXKey);
        }

        LaunchProjectile(spell, origin, null);
    }

    private IEnumerator Co_SpawnWithCharge(MagicSpellSO spell, Transform originOverride)
    {
        if (!spell || !spell.prefab) yield break;

        Transform origin = originOverride ? originOverride : transform;

        // Dirección y rotación inicial
        Vector3 baseForward = transform.forward;
        Vector3 dir = (targeting != null)
            ? targeting.GetAimDirectionFrom(origin ? origin : transform, baseForward)
            : baseForward;
        dir = spell.flattenDirection ? Vector3.ProjectOnPlane(dir, Vector3.up).normalized : dir.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = baseForward;

        Vector3 spawnPos = (origin ? origin.position : transform.position) + dir * spell.forwardOffset;
        
        // Aplicar offset de posición adicional
        // Y siempre es vertical (arriba/abajo en espacio mundial)
        // X y Z respetan la rotación del caster (derecha/adelante en espacio local)
        if (spell.positionOffset != Vector3.zero)
        {
            Transform casterTransform = origin ? origin : transform;
            
            // Y es siempre arriba/abajo (espacio mundial)
            spawnPos.y += spell.positionOffset.y;
            
            // X (derecha) y Z (adelante) en espacio local del caster
            if (spell.positionOffset.x != 0f || spell.positionOffset.z != 0f)
            {
                Vector3 localOffset = new Vector3(spell.positionOffset.x, 0f, spell.positionOffset.z);
                spawnPos += casterTransform.TransformDirection(localOffset);
            }
        }
        
        // Evitar que el proyectil nazca dentro de colliders del jugador (mano/cuerpo)
        // Usar posición de spawn original definida por el caster/spell
        Quaternion spawnRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        if (spell.spawnVFX)
        {
            var fx = Instantiate(spell.spawnVFX, spawnPos, spawnRt);
            if (spell.useScaleOverride)
                fx.transform.localScale = spell.scaleOverride;
            // 🔥 CORRECCIÓN: Siempre destruir el VFX después de un tiempo
            float destroyTime = spell.vfxLifetime > 0f ? spell.vfxLifetime : 3f; // 3s por defecto
            Destroy(fx, destroyTime);
        }

        GameObject go = Instantiate(spell.prefab, spawnPos, spawnRt);
        if (spell.useScaleOverride)
            go.transform.localScale = spell.scaleOverride;
        if (go == null) yield break;

        // Pausar física mientras carga
        Rigidbody cachedRb = null;
        bool cachedKinematic = false;
        bool cachedUseGravity = false;
        if (go != null && go.TryGetComponent<Rigidbody>(out var rbDuringCharge))
        {
            cachedRb = rbDuringCharge;
            cachedKinematic = rbDuringCharge.isKinematic;
            cachedUseGravity = rbDuringCharge.useGravity;
            rbDuringCharge.isKinematic = true;
            rbDuringCharge.useGravity = false;
                // Poner kinematic durante la carga para pausar la física.
                // NO toques `velocity` ni `angularVelocity` mientras sea kinematic
                // porque Unity lanza warnings y no aplica cambios a cuerpos kinematic.
        }

        Transform previousParent = null;
        if (go != null && spell.followOriginDuringCharge && origin != null)
        {
            previousParent = go.transform.parent;
            go.transform.SetParent(origin, worldPositionStays: true);
        }

        GameObject instigator = instigatorOverride ? instigatorOverride : gameObject;
        if (go != null) IgnoreCollisionsBetween(go, instigator);

        MagicProjectile mp = null;
        if (go != null && go.TryGetComponent<MagicProjectile>(out var proj))
        {
            mp = proj;
            var cfg = new MagicProjectile.ProjectileConfig
            {
                damage         = spell.damage,
                aoeRadius      = spell.aoeRadius,
                knockbackForce = spell.knockbackForce,
                hitLayers      = GetDamageLayers(),
                collisionLayers = GetDamageLayers(),
                destroyOnHit   = spell.destroyOnHit,
                lifeTime       = spell.lifeTime,
                maxRange       = spell.maxRange,
                initialSpeed   = spell.initialSpeed,
                useGravity     = spell.useGravity,
                impactVFX      = spell.impactVFX,
                despawnVFX     = spell.despawnVFX,
                vfxLifetime    = spell.vfxLifetime,
                impactSFXKey   = spell.impactSFXKey
            };
            mp.Configure(cfg, instigator);
            mp.SetKinematic(true);
        }

        float charge = Mathf.Max(0f, spell.chargeTime);
        float elapsed = 0f;
        while (elapsed < charge)
        {
            elapsed += Time.deltaTime;
            yield return null;
            if (go == null) yield break;
        }

        if (go != null && spell.followOriginDuringCharge && origin != null)
            go.transform.SetParent(previousParent, worldPositionStays: true);

        if (mp != null)
        {
            mp.SetKinematic(false);
            mp.Launch(dir, spell.initialSpeed, spell.useGravity);
        }
        else if (go != null && go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = spell.useGravity;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = dir * Mathf.Max(0f, spell.initialSpeed);
        }
        else if (cachedRb != null)
        {
            cachedRb.isKinematic = cachedKinematic;
            cachedRb.useGravity = spell.useGravity;
            // Si el cuerpo quedó dinámico tras restaurar, aplicamos velocidad limpia
            if (!cachedRb.isKinematic)
            {
                cachedRb.angularVelocity = Vector3.zero;
                cachedRb.linearVelocity = dir * Mathf.Max(0f, spell.initialSpeed);
            }
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
        // Configurar colisiones - ignorar jugador y todos sus hijos
        GameObject instigator = instigatorOverride ? instigatorOverride : gameObject;
        Vector3 spawnPos = (origin ? origin.position : transform.position) + dir * spell.forwardOffset;
        
        // Aplicar offset de posición adicional
        // Y siempre es vertical (arriba/abajo en espacio mundial)
        // X y Z respetan la rotación del caster (derecha/adelante en espacio local)
        if (spell.positionOffset != Vector3.zero)
        {
            Transform casterTransform = origin ? origin : transform;
            
            // Y es siempre arriba/abajo (espacio mundial)
            spawnPos.y += spell.positionOffset.y;
            
            // X (derecha) y Z (adelante) en espacio local del caster
            if (spell.positionOffset.x != 0f || spell.positionOffset.z != 0f)
            {
                Vector3 localOffset = new Vector3(spell.positionOffset.x, 0f, spell.positionOffset.z);
                spawnPos += casterTransform.TransformDirection(localOffset);
            }
        }
        
        // Evitar que el proyectil nazca dentro de colliders del jugador
        // Usar posición de spawn original definida por el caster/spell
        Quaternion spawnRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);

        if (spell.spawnVFX)
        {
            var fx = Instantiate(spell.spawnVFX, spawnPos, spawnRt);
            if (spell.useScaleOverride)
            {
                fx.transform.localScale = spell.scaleOverride;
            }
            // 🔥 CORRECCIÓN: Siempre destruir el VFX después de un tiempo
            float destroyTime = spell.vfxLifetime > 0f ? spell.vfxLifetime : 3f; // 3s por defecto
            Destroy(fx, destroyTime);
        }

        GameObject go = Instantiate(spell.prefab, spawnPos, spawnRt);
        if (spell.useScaleOverride)
        {
            go.transform.localScale = spell.scaleOverride;
        }

        IgnoreCollisionsBetween(go, instigator);

        if (go.TryGetComponent<MagicProjectile>(out var mp))
        {
            var cfg = new MagicProjectile.ProjectileConfig
            {
                damage         = spell.damage,
                aoeRadius      = spell.aoeRadius,
                knockbackForce = spell.knockbackForce,
                hitLayers      = GetDamageLayers(),
                collisionLayers = GetDamageLayers(),
                destroyOnHit   = spell.destroyOnHit,
                lifeTime       = spell.lifeTime,
                maxRange       = spell.maxRange,
                initialSpeed   = spell.initialSpeed,
                useGravity     = spell.useGravity,
                impactVFX      = spell.impactVFX,
                despawnVFX     = spell.despawnVFX,
                vfxLifetime    = spell.vfxLifetime,
                impactSFXKey   = spell.impactSFXKey,
                element        = spell.element
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
            rb.angularVelocity = Vector3.zero;
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
