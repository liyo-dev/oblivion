using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;

public class MagicProjectileSpawner : MonoBehaviour
{
    public enum DirectionMode { PlayerForward, OriginForward, AimTransform }

    [Header("Listen")]
    [SerializeField] private vThirdPersonController controller;

    [Header("Prefabs")]
    [SerializeField] private GameObject basicFireballPrefab;
    [SerializeField] private GameObject comboFireballPrefab;

    [Header("Timing / Sync")]
    [SerializeField] private float spawnDelaySeconds = 0.12f;

    [Header("Direction")]
    [SerializeField] private DirectionMode directionMode = DirectionMode.PlayerForward;
    [SerializeField] private Transform aimTransform;               // solo si eliges AimTransform
    [SerializeField] private Vector3 visualRotationOffsetEuler = Vector3.zero;

    [Header("Auto-Aim")]
    [SerializeField] private bool autoAimFireball = true;          // ← NUEVO
    [SerializeField] private float forwardOffset = 0.35f;

    [Header("Projectile Physics")]
    [SerializeField] private float speedBasic = 18f;
    [SerializeField] private float speedCombo = 22f;
    [SerializeField] private bool  useGravity = false;
    [SerializeField] private bool  ignoreCasterColliders = true;

    private readonly List<Collider> _casterCols = new();
    private PlayerTargeting _targeting;

    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (controller && ignoreCasterColliders)
            _casterCols.AddRange(controller.GetComponentsInChildren<Collider>(true));

        _targeting = controller ? controller.GetComponent<PlayerTargeting>() : GetComponentInParent<PlayerTargeting>();
        if (directionMode == DirectionMode.AimTransform && !aimTransform && Camera.main)
            aimTransform = Camera.main.transform;
    }

    void OnEnable()  { if (controller != null) controller.OnMagicCast += HandleMagicCast; }
    void OnDisable() { if (controller != null) controller.OnMagicCast -= HandleMagicCast; }

    void HandleMagicCast(GameObject caster, Transform origin, Vector3 originSuggestedDir, MagicCastType type)
    {
        if (origin != transform) return;
        StartCoroutine(Co_SpawnAfterDelay(caster, origin, type));
    }

    IEnumerator Co_SpawnAfterDelay(GameObject caster, Transform origin, MagicCastType type)
    {
        if (spawnDelaySeconds > 0f) yield return new WaitForSeconds(spawnDelaySeconds);

        // 1) Fallback forward según modo seleccionado
        Vector3 baseDir = directionMode switch
        {
            DirectionMode.OriginForward => transform.forward,
            DirectionMode.AimTransform  => (aimTransform ? aimTransform.forward : (controller ? controller.transform.forward : transform.forward)),
            _                           => (controller ? controller.transform.forward : transform.forward),
        };

        // 2) Auto-aim (si hay target cercano, disparamos hacia él)
        Vector3 dir = baseDir;
        if (autoAimFireball && _targeting)
            dir = _targeting.GetAimDirectionFrom(origin, baseDir);
        dir.Normalize();

        // 3) Posición, rotación y prefab
        Vector3 spawnPos = origin.position + dir * forwardOffset;
        GameObject prefab = (type == MagicCastType.Combo && comboFireballPrefab != null) ? comboFireballPrefab : basicFireballPrefab;
        if (!prefab) yield break;

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(visualRotationOffsetEuler);
        GameObject go = Instantiate(prefab, spawnPos, rot);

        // 4) Física
        if (go.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.useGravity = useGravity;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = dir * ((type == MagicCastType.Combo) ? speedCombo : speedBasic);
        }

        // 5) Ignorar colisiones con el jugador
        if (ignoreCasterColliders && go.TryGetComponent<Collider>(out var projCol))
            foreach (var c in _casterCols) if (c && c.enabled) Physics.IgnoreCollision(projCol, c, true);

        // 6) (opcional) pasar instigator al proyectil
        if (go.TryGetComponent<FireballProjectile>(out var fp))
            fp.Instigator = controller ? controller.gameObject : null;
    }
}
