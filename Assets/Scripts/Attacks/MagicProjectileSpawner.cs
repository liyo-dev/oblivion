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

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        OnPlayerAttacked = null;
    }
#endif

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

    // FIX M8 (auditoría 2026-08-07): mientras Co_SpawnWithCharge espera chargeTime, el proyectil
    // vive parenteado a la mano (followOriginDuringCharge) y en kinematic. Si este spawner se
    // desactiva a mitad de la carga (p.ej. el player se desactiva por cinemática/muerte/cambio de
    // escena), Unity aborta la corrutina sin ejecutar el resto — el proyectil se queda pegado a la
    // mano para siempre, sobre todo si spell.lifeTime==0 (nunca arranca su temporizador porque
    // Launch() nunca llega a llamarse). OnDisable limpia cualquier proyectil que se haya quedado
    // a medio cargar.
    private readonly System.Collections.Generic.List<GameObject> _chargingProjectiles = new System.Collections.Generic.List<GameObject>();

    [Header("Opciones")]
    [SerializeField] private bool ignoreCasterColliders = true;
    [SerializeField] private GameObject instigatorOverride;

    [Header("Velocidad dinámica (INC-049)")]
    [Tooltip("Multiplicador de velocidad del proyectil mientras el jugador está volando.")]
    [SerializeField] private float flyingSpeedMultiplier = 1.5f;
    [Tooltip("Multiplicador de velocidad del proyectil mientras el jugador está esprintando (en tierra).")]
    [SerializeField] private float sprintSpeedMultiplier = 1.3f;

    // Referencias para detectar vuelo/sprint (mismo criterio que SprintVFXController)
    private Animator _animator;
    private PlayerFlyingController _flyingController;
    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!targeting)  targeting  = GetComponentInParent<PlayerTargeting>();
        if (!instigatorOverride) instigatorOverride = gameObject;

        if (!_animator) _animator = GetComponentInParent<Animator>();
        if (!_flyingController) _flyingController = GetComponentInParent<PlayerFlyingController>();
    }

    /// <summary>
    /// FIX INC-049: los hechizos deben ir más rápido que el player mientras vuela, y también
    /// más rápido mientras esprinta. Usa el mismo criterio que SprintVFXController (parámetros
    /// del Animator de Invector) para detectar sprint, y PlayerFlyingController para el vuelo.
    /// </summary>
    private float GetSpeedMultiplier()
    {
        if (_flyingController != null && _flyingController.IsFlying)
            return flyingSpeedMultiplier;

        if (_animator != null)
        {
            bool isGrounded = _animator.GetBool(HashIsGrounded);
            float inputMag  = _animator.GetFloat(HashInputMagnitude);
            if (isGrounded && inputMag > 1.05f) // InputMagnitude > 1.0 = sprint en Invector
                return sprintSpeedMultiplier;
        }

        return 1f;
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

        // FIX M8: ver comentario en _chargingProjectiles. La corrutina de carga se aborta al
        // desactivarse este componente sin llegar a soltar el proyectil; lo destruimos aquí para
        // no dejarlo pegado a la mano indefinidamente.
        for (int i = 0; i < _chargingProjectiles.Count; i++)
        {
            var go = _chargingProjectiles[i];
            if (go)
            {
                go.transform.SetParent(null, worldPositionStays: true);
                Destroy(go);
            }
        }
        _chargingProjectiles.Clear();
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

        if (spell.kind == MagicKind.Zone)
        {
            // Los hechizos de zona no cargan ni vuelan: se materializan al instante en el punto
            // de impacto calculado (ver SpawnZoneNow). El SFX de casteo ya sonó arriba.
            SpawnZoneNow(spell, origin);
        }
        else if (spell.chargeTime > 0f)
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

    /// Disparo cinemático: ignora maná, cooldowns y ActionManager.
    /// Usa el hechizo del slot si está equipado; si no, usa <paramref name="fallbackSpell"/>.
    /// <paramref name="directionOverride"/> permite forzar una dirección exacta ignorando el sistema de targeting.
    /// Devuelve el GameObject del proyectil spawneado (puede ser null si el hechizo no tiene prefab).
    /// Llamar desde secuenciadores de cinemáticas — nunca desde gameplay normal.
    public GameObject SpawnForCinematic(MagicSlot slot, MagicSpellSO fallbackSpell, Transform originOverride = null, Vector3? directionOverride = null)
    {
        var (slotSpell, slotOrigin) = GetSpellAndOrigin(slot);
        MagicSpellSO spell  = (slotSpell != null && slotSpell.prefab != null) ? slotSpell : fallbackSpell;
        Transform    origin = originOverride != null ? originOverride : slotOrigin;

        if (spell == null || spell.prefab == null) return null;
        return SpawnNow(spell, origin, directionOverride: directionOverride);
    }

    public GameObject SpawnNow(MagicSpellSO spell, Transform originOverride = null, bool playSFX = true, Vector3? directionOverride = null)
    {
        if (!spell || !spell.prefab) return null;

        Transform origin = originOverride ? originOverride : transform;

        if (playSFX && !string.IsNullOrEmpty(spell.castSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(spell.castSFXKey);

        if (spell.kind == MagicKind.Zone)
            return SpawnZoneNow(spell, origin, directionOverride);

        return LaunchProjectile(spell, origin, directionOverride);
    }

    private IEnumerator Co_SpawnWithCharge(MagicSpellSO spell, Transform originOverride)
    {
        if (!spell || !spell.prefab) yield break;

        // FIX INC-049: velocidad efectiva ajustada si el player vuela o esprinta.
        float effectiveSpeed = spell.initialSpeed * GetSpeedMultiplier();

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
            // FIX (auditoría 2026-08-12): VfxPoolService en vez de Instantiate+Destroy directo
            // (regla del proyecto — AGENTS.md §2, VFX de un solo uso siempre por pool).
            float destroyTime = spell.vfxLifetime > 0f ? spell.vfxLifetime : 3f; // 3s por defecto
            var fxTransform = VfxPoolService.Instance.Play(spell.spawnVFX, spawnPos, spawnRt, destroyTime);
            if (spell.useScaleOverride && fxTransform != null)
                fxTransform.localScale = spell.scaleOverride;
        }

        GameObject go = Instantiate(spell.prefab, spawnPos, spawnRt);
        if (spell.useScaleOverride)
            go.transform.localScale = spell.scaleOverride;
        if (go == null) yield break;

        // FIX M8: registrar como "cargando" hasta que se suelte (o hasta OnDisable, ver arriba).
        _chargingProjectiles.Add(go);

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
                initialSpeed   = effectiveSpeed,
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

        // FIX M8: ya se va a soltar (Launch/velocity más abajo) — deja de estar "cargando".
        _chargingProjectiles.Remove(go);

        // FIX INC-049: recalcular la velocidad justo antes de soltar el proyectil, no al
        // empezar la carga. Con hechizos con chargeTime > 0 el jugador puede empezar a volar
        // o esprintar (o dejar de hacerlo) DURANTE la carga; usar el valor capturado al inicio
        // dejaba el proyectil con la velocidad "de a pie" aunque se soltara volando/esprintando.
        effectiveSpeed = spell.initialSpeed * GetSpeedMultiplier();

        if (mp != null)
        {
            mp.SetKinematic(false);
            mp.Launch(dir, effectiveSpeed, spell.useGravity);
        }
        else if (go != null && go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.useGravity = spell.useGravity;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = dir * Mathf.Max(0f, effectiveSpeed);
        }
        else if (cachedRb != null)
        {
            cachedRb.isKinematic = cachedKinematic;
            cachedRb.useGravity = spell.useGravity;
            // Si el cuerpo quedó dinámico tras restaurar, aplicamos velocidad limpia
            if (!cachedRb.isKinematic)
            {
                cachedRb.angularVelocity = Vector3.zero;
                cachedRb.linearVelocity = dir * Mathf.Max(0f, effectiveSpeed);
            }
        }
    }

    /// <summary>
    /// Materializa un hechizo de MagicKind.Zone: reproduce el VFX de casteo en la mano (mismo
    /// camino visual que un proyectil, para aprovechar la animación de casteo existente — "sale
    /// de la mano") y, al instante, instancia el prefab de la zona ya en su posición final —
    /// centrada en el objetivo fijado si 'zoneSnapToTarget' lo permite y hay uno, o a
    /// 'zoneRange' metros delante del lanzador en la dirección de apuntado. Un raycast hacia
    /// abajo apoya la zona sobre el suelo real para que no quede flotando en terreno irregular.
    /// </summary>
    private GameObject SpawnZoneNow(MagicSpellSO spell, Transform origin, Vector3? directionOverride = null)
    {
        if (!spell || !spell.prefab) return null;

        Transform o = origin ? origin : transform;

        // Dirección de apuntado: mismo criterio que un proyectil (usa el target fijado si lo hay).
        Vector3 baseForward = transform.forward;
        Vector3 dir = directionOverride ?? ((targeting != null)
            ? targeting.GetAimDirectionFrom(o, baseForward)
            : baseForward);
        dir = spell.flattenDirection ? Vector3.ProjectOnPlane(dir, Vector3.up).normalized : dir.normalized;
        if (dir.sqrMagnitude < 0.001f) dir = baseForward;

        // Punto donde aparece la zona.
        Vector3 zonePos;
        if (spell.zoneSnapToTarget && targeting != null && targeting.TryGetTarget(out Transform aimedTarget) && aimedTarget != null)
        {
            zonePos = aimedTarget.position;
        }
        else
        {
            zonePos = o.position + dir * spell.zoneRange;
        }

        // Apoyar la zona sobre el suelo real (raycast hacia abajo desde bien arriba del punto).
        // Se eleva 'zoneGroundOffset' sobre el punto de impacto: exactamente a la altura del
        // suelo, el VFX se mezcla/hace z-fighting con la geometría (mismo problema visual que
        // tenían los puntos de guardado).
        Vector3 rayStart = zonePos + Vector3.up * 25f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit groundHit, 60f, spell.zoneGroundLayers, QueryTriggerInteraction.Ignore))
        {
            zonePos.y = groundHit.point.y + spell.zoneGroundOffset;
        }
        else
        {
            zonePos.y = o.position.y + spell.zoneGroundOffset; // fallback: altura del lanzador si no hay suelo detectado
        }

        // VFX de casteo en la mano — el "sale de la mano" del pedido de diseño, aunque el
        // hechizo en sí se materialice a distancia.
        if (spell.spawnVFX)
        {
            float handFxLifetime = spell.vfxLifetime > 0f ? spell.vfxLifetime : 3f;
            Quaternion handRt = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(spell.visualRotationOffsetEuler);
            var fxTransform = VfxPoolService.Instance.Play(spell.spawnVFX, o.position + dir * spell.forwardOffset, handRt, handFxLifetime);
            if (spell.useScaleOverride && fxTransform != null)
                fxTransform.localScale = spell.scaleOverride;
        }

        GameObject go = Instantiate(spell.prefab, zonePos, Quaternion.identity);
        if (spell.useScaleOverride)
            go.transform.localScale = spell.scaleOverride;

        GameObject instigator = instigatorOverride ? instigatorOverride : gameObject;
        if (go.TryGetComponent<MagicZoneEffect>(out var zone))
        {
            var cfg = new MagicZoneEffect.ZoneConfig
            {
                damagePerTick  = spell.damage,
                tickInterval   = spell.zoneTickInterval,
                radius         = spell.zoneRadius,
                duration       = spell.zoneDuration,
                knockbackForce = spell.knockbackForce,
                hitLayers      = GetDamageLayers(),
                tickSFXKey     = spell.impactSFXKey,
                despawnVFX     = spell.despawnVFX,
                vfxLifetime    = spell.vfxLifetime
            };
            zone.Configure(cfg, instigator);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning($"[MagicProjectileSpawner] El prefab de '{spell.displayName}' es MagicKind.Zone pero no tiene MagicZoneEffect — no hará nada.");
        }
#endif

        return go;
    }

    private GameObject LaunchProjectile(MagicSpellSO spell, Transform origin, Vector3? directionOverride)
    {
        if (!spell || !spell.prefab) return null;

        // FIX INC-049: velocidad efectiva ajustada si el player vuela o esprinta.
        float effectiveSpeed = spell.initialSpeed * GetSpeedMultiplier();

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
            // FIX (auditoría 2026-08-12): VfxPoolService en vez de Instantiate+Destroy directo
            // (regla del proyecto — AGENTS.md §2, VFX de un solo uso siempre por pool).
            float destroyTime = spell.vfxLifetime > 0f ? spell.vfxLifetime : 3f; // 3s por defecto
            var fxTransform = VfxPoolService.Instance.Play(spell.spawnVFX, spawnPos, spawnRt, destroyTime);
            if (spell.useScaleOverride && fxTransform != null)
            {
                fxTransform.localScale = spell.scaleOverride;
            }
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
                initialSpeed   = effectiveSpeed,
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
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = dir * Mathf.Max(0f, effectiveSpeed);
        }

        return go;
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
