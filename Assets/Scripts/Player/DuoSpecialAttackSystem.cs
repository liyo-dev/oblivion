using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;
using Game.NPC;

/// <summary>
/// Gestiona los ataques especiales dúo con Estela (LT solo) y Liam (RT solo).
/// Cada compañero tiene su propio medidor de carga, que se llena con los orbes
/// que sueltan los enemigos cuando ese compañero los golpea.
/// LT + RT simultáneo sigue siendo el escudo (PlayerShieldController).
/// </summary>
[DisallowMultipleComponent]
public class DuoSpecialAttackSystem : MonoBehaviour
{
    [Header("Ataques")]
    [SerializeField] private SpecialAttackSO estelaAttack; // LT solo
    [SerializeField] private SpecialAttackSO liamAttack;   // RT solo

    [Header("Medidores de carga (uno por compañero)")]
    [SerializeField] private SpecialChargeMeter estelaChargeMeter;
    [SerializeField] private SpecialChargeMeter liamChargeMeter;

    [Header("Referencias")]
    [SerializeField] private MagicCaster magicCaster;
    [SerializeField] private Transform   attackOrigin;

    [Header("Feedback de no disponible")]
    [SerializeField] private string notAvailableSFXKey = "ui_denied";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;

    private PlayerControls _controls;
    private bool           _ownsControls;
    private bool           _isExecuting;

    private const float TriggerThreshold = 0.5f;

    // ── Propiedades para la UI ──────────────────────────────────────────────

    public SpecialChargeMeter EstelaChargeMeter => estelaChargeMeter;
    public SpecialChargeMeter LiamChargeMeter   => liamChargeMeter;

    // ── Ciclo de vida ──────────────────────────────────────────────────────

    void Awake()
    {
        _controls = Core.PlayerInputManager.GetSharedOrNew(out _ownsControls);
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>();
    }

    void OnEnable()
    {
        if (_controls == null) return;
        if (_ownsControls) _controls.Enable();

        _controls.GamePlay.LT.performed += OnLTPerformed;
        _controls.GamePlay.RT.performed += OnRTPerformed;
    }

    void OnDisable()
    {
        if (_controls == null) return;

        _controls.GamePlay.LT.performed -= OnLTPerformed;
        _controls.GamePlay.RT.performed -= OnRTPerformed;

        if (_ownsControls) _controls.Disable();
    }

    // ── API pública para los orbes ─────────────────────────────────────────

    /// <summary>
    /// Añade carga al medidor del compañero indicado. Llamado por BattleOrb al recogerlo.
    /// </summary>
    public void AddCharge(DuoCompanion companion, float amount)
    {
        GetMeter(companion)?.AddCharge(amount);
    }

    // ── Input ──────────────────────────────────────────────────────────────

    private void OnLTPerformed(InputAction.CallbackContext ctx)
    {
        if (!GameState.CanProcessGameplayInput) return;

        float lt = ctx.ReadValue<float>();
        float rt = _controls.GamePlay.RT.ReadValue<float>();

        if (lt < TriggerThreshold || rt >= TriggerThreshold) return;

        TryExecute(estelaAttack, DuoCompanion.Estela);
    }

    private void OnRTPerformed(InputAction.CallbackContext ctx)
    {
        if (!GameState.CanProcessGameplayInput) return;

        float rt = ctx.ReadValue<float>();
        float lt = _controls.GamePlay.LT.ReadValue<float>();

        if (rt < TriggerThreshold || lt >= TriggerThreshold) return;

        TryExecute(liamAttack, DuoCompanion.Liam);
    }

    // ── Lógica ─────────────────────────────────────────────────────────────

    private void TryExecute(SpecialAttackSO attack, DuoCompanion companion)
    {
        if (_isExecuting || attack == null) return;

        var meter = GetMeter(companion);

        if (meter == null || !meter.IsFullyCharged)
        {
            PlayDenied();
            if (showDebugLogs)
                Debug.Log($"[DuoSpecial] Bloqueado: barra de {companion} no llena ({meter?.Normalized:P0})");
            return;
        }

        Transform companionTransform = FindCompanionInRange(companion, attack.companionMaxDistance);
        if (companionTransform == null)
        {
            PlayDenied();
            if (showDebugLogs)
                Debug.Log($"[DuoSpecial] Bloqueado: {companion} no está en rango ({attack.companionMaxDistance}m)");
            return;
        }

        if (magicCaster != null && magicCaster.IsCasting) return;

        meter.TryConsume(meter.MaxCharge);

        StartCoroutine(Co_Execute(attack, companionTransform));
    }

    private IEnumerator Co_Execute(SpecialAttackSO attack, Transform companion)
    {
        _isExecuting = true;

        TriggerAnimation(gameObject,        attack.playerAnimationTrigger);
        TriggerAnimation(companion.gameObject, attack.companionAnimationTrigger);

        if (!string.IsNullOrEmpty(attack.castSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(attack.castSFXKey);

        Transform origin = attackOrigin ? attackOrigin : transform;
        if (attack.vfxPrefab != null)
        {
            var vfx = Instantiate(attack.vfxPrefab,
                                  origin.position + origin.forward * 1.5f,
                                  Quaternion.LookRotation(origin.forward));
            Destroy(vfx, Mathf.Max(0.5f, attack.vfxLifetime));
        }

        if (attack.damageDelay > 0f)
            yield return new WaitForSeconds(attack.damageDelay);

        ApplyAoeDamage(attack, origin);

        _isExecuting = false;
    }

    private void ApplyAoeDamage(SpecialAttackSO attack, Transform origin)
    {
        if (attack.damage <= 0f && attack.knockbackForce <= 0f) return;

        Vector3 center  = origin.position + origin.forward * 2.5f;
        int     hitMask = LayerMask.GetMask("Enemy", "Boss");

        var hits = Physics.OverlapSphere(center, attack.aoeRadius, hitMask);
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var damageable))
                damageable.TakeDamage(attack.damage);

            if (attack.knockbackForce > 0f && hit.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 dir = (hit.transform.position - center).normalized;
                rb.AddForce(dir * attack.knockbackForce, ForceMode.Impulse);
            }
        }

        if (showDebugLogs)
            Debug.Log($"[DuoSpecial] AoE aplicado: {hits.Length} objetivos en radio {attack.aoeRadius}m");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private SpecialChargeMeter GetMeter(DuoCompanion companion)
        => companion == DuoCompanion.Estela ? estelaChargeMeter : liamChargeMeter;

    private Transform FindCompanionInRange(DuoCompanion companion, float maxDist)
    {
        string name  = companion == DuoCompanion.Estela ? "Estela" : "Liam";
        var    party = PlayerParty.Instance;
        if (party == null) return null;

        var member = party.GetMemberByName(name);
        if (member == null) return null;

        return member.GetDistanceToPlayer() <= maxDist ? member.transform : null;
    }

    private static void TriggerAnimation(GameObject go, string trigger)
    {
        if (string.IsNullOrEmpty(trigger)) return;
        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger(trigger);
    }

    private void PlayDenied()
    {
        if (!string.IsNullOrEmpty(notAvailableSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(notAvailableSFXKey);
    }
}
