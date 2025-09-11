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

    public enum DirectionMode { PlayerForward, OriginForward, AimTransform }

    [Header("Dirección / Auto-Aim")]
    [SerializeField] private DirectionMode directionMode = DirectionMode.AimTransform;
    [SerializeField] private Transform aimTransform;     // si eliges AimTransform
    [SerializeField] private bool autoAim = true;
    [SerializeField] private Vector3 visualRotationOffsetEuler = Vector3.zero;
    [SerializeField] private float forwardOffset = 0.5f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float spawnDelaySeconds = 0.5f;

    [Header("Colisiones")]
    [SerializeField] private bool ignoreCasterColliders = true;

    [Header("Datos")]
    [SerializeField] private MagicLoadout loadout;

    private readonly List<Collider> _casterCols = new();
    private PlayerTargeting _targeting;

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!loadout)    loadout    = GetComponentInParent<MagicLoadout>();

        if (controller && ignoreCasterColliders)
            _casterCols.AddRange(controller.GetComponentsInChildren<Collider>(true));

        _targeting = controller ? controller.GetComponent<PlayerTargeting>() : GetComponentInParent<PlayerTargeting>();

        if (directionMode == DirectionMode.AimTransform && !aimTransform && Camera.main)
            aimTransform = Camera.main.transform;
    }

    void OnEnable()
    {
        if (controller) controller.OnMagicSlotCast += HandleSlotCast; // int slotId
    }

    void OnDisable()
    {
        if (controller) controller.OnMagicSlotCast -= HandleSlotCast;
    }

    private void HandleSlotCast(int slotId)
    {
        // Mapear int -> tu enum
        MagicSlot slot = slotId == 0 ? MagicSlot.Left
                         : slotId == 1 ? MagicSlot.Right
                         : MagicSlot.Special;

        // Elegir origen
        Transform origin = slot switch
        {
            MagicSlot.Left    => (leftOrigin    ? leftOrigin    : transform),
            MagicSlot.Right   => (rightOrigin   ? rightOrigin   : transform),
            MagicSlot.Special => (specialOrigin ? specialOrigin : transform),
            _ => transform
        };

        StartCoroutine(Co_SpawnAfterDelay(origin, slot));
    }

    private IEnumerator Co_SpawnAfterDelay(Transform origin, MagicSlot slot)
    {
        if (spawnDelaySeconds > 0f) yield return new WaitForSeconds(spawnDelaySeconds);

        // 1) Hechizo asignado y aprendido
        var spell = loadout ? loadout.GetForSlot(slot) : null;
        if (!spell || !spell.prefab) yield break;

        // 2) Dirección base - asegurar que sea horizontal y hacia adelante
        Vector3 baseDir;
        if (directionMode == DirectionMode.OriginForward && origin)
        {
            baseDir = origin.forward;
        }
        else if (directionMode == DirectionMode.AimTransform && aimTransform)
        {
            baseDir = aimTransform.forward;
        }
        else if (controller)
        {
            baseDir = controller.transform.forward;
        }
        else
        {
            baseDir = transform.forward;
        }

        // Normalizar y asegurar que tenga componente horizontal
        baseDir = Vector3.ProjectOnPlane(baseDir, Vector3.up).normalized;
        if (baseDir.sqrMagnitude < 0.01f) baseDir = transform.forward;

        // 3) Auto-aim si hay targeting
        Vector3 dir = baseDir;
        if (autoAim && _targeting) dir = _targeting.GetAimDirectionFrom(origin ? origin : transform, baseDir);
        dir.Normalize();

        // 4) Posición y rotación final
        Vector3 spawnPos = (origin ? origin.position : transform.position) + dir * forwardOffset;
        Quaternion rot   = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(visualRotationOffsetEuler);

        // 5) Instanciado
        GameObject go = Instantiate(spell.prefab, spawnPos, rot);

        // 6) Física
        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.useGravity = spell.useGravity;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = dir * Mathf.Max(0f, spell.initialSpeed);
        }

        // 7) Ignorar colisiones con el caster
        if (ignoreCasterColliders && go.TryGetComponent<Collider>(out var projCol))
        {
            foreach (var c in _casterCols)
                if (c && c.enabled) Physics.IgnoreCollision(projCol, c, true);
        }

        // 8) Instigator (solo MagicProjectile)
        if (go.TryGetComponent<MagicProjectile>(out var mp))
            mp.Instigator = controller ? controller.gameObject : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!loadout)    loadout    = GetComponentInParent<MagicLoadout>();
    }
#endif
}
