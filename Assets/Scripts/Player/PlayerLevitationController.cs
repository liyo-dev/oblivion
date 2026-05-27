using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;
using Sendero.Core.Feedback;

/// <summary>
/// Controlador de levitación del jugador (y de Liam como aliado IA).
///
/// Flujo:
///   1. Mantener el botón de magia → agarra los NPCs del cono y los levita (con drenaje de maná).
///   2. Soltar el botón → los lanza hacia adelante.
///
/// Para la IA aliada: llamar TriggerAILevitation(slot, spell).
/// </summary>
[DisallowMultipleComponent]
public class PlayerLevitationController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private vThirdPersonController controller;
    [SerializeField] private MagicCaster magicCaster;
    [SerializeField] private ManaPool manaPool;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerTargeting targeting;

    [Header("Configuración de Animación")]
    [Tooltip("Frame normalizado (0-1) en el que pausar la animación durante el hold.")]
    [SerializeField] private float holdPauseNormalizedTime = 0.3f;

    [Header("Configuración de Detección")]
    [Tooltip("Offset vertical desde el transform para el origen del cono de detección.")]
    [SerializeField] private float detectionHeightOffset = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;

    // ── Estado ──────────────────────────────────────────────────────────────
    private enum LevitationPhase { Idle, Levitating }
    private LevitationPhase _phase = LevitationPhase.Idle;

    private MagicSlot _activeSlot;
    private MagicSpellSO _activeSpell;
    private readonly List<LevitationTarget> _currentTargets = new List<LevitationTarget>();
    private float _levitationStartTime;

    // Evita re-entrar mientras el botón sigue pulsado
    private bool _leftButtonWasDown;
    private bool _rightButtonWasDown;

    // ── Animación ───────────────────────────────────────────────────────────
    private readonly int _upperBodyLayerIndex = 1;
    private string _currentMagicStatePath;
    private int _currentMagicStateHash;
    private Coroutine _animationCoroutine;

    // ── VFX ─────────────────────────────────────────────────────────────────
    private GameObject _holdVFXInstance;
    private readonly List<GameObject> _rangeIndicatorInstances = new List<GameObject>();

    // ── Buffer Physics ───────────────────────────────────────────────────────
    private readonly Collider[] _levitationTargetBuffer = new Collider[16];

    // ── Reflexión (bug I7 pendiente — sustituir por acceso directo) ──────────
    private static System.Type _gamepadReaderType;
    private static System.Reflection.PropertyInfo _leftHeldProp;
    private static System.Reflection.PropertyInfo _leftReleasedProp;
    private static System.Reflection.PropertyInfo _rightHeldProp;
    private static System.Reflection.PropertyInfo _rightReleasedProp;
    private static bool _reflectionInitialized;

    // ── API pública ──────────────────────────────────────────────────────────
    public bool IsLevitating => _phase == LevitationPhase.Levitating;
    public MagicSlot ActiveSlot => _activeSlot;
    public IReadOnlyList<LevitationTarget> CurrentTargets => _currentTargets;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _gamepadReaderType     = null;
        _leftHeldProp          = null;
        _leftReleasedProp      = null;
        _rightHeldProp         = null;
        _rightReleasedProp     = null;
        _reflectionInitialized = false;
    }
#endif

    // ────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>();
        if (!manaPool)    manaPool    = GetComponentInParent<ManaPool>();
        if (!animator)    animator    = GetComponentInParent<Animator>();
        if (!targeting)   targeting   = GetComponentInParent<PlayerTargeting>();

        InitializeReflection();
    }

    void Start()
    {
        if (!magicCaster)
            Debug.LogError("[PlayerLevitationController] No se encontró MagicCaster.");
    }

    void Update()
    {
        if (_phase == LevitationPhase.Idle)
            CheckForLevitationStart();
        else
        {
            UpdateLevitation();
            CheckForRelease();
        }
    }

    // ── Inicio de levitación ─────────────────────────────────────────────────

    void CheckForLevitationStart()
    {
        if (!magicCaster) return;

        bool leftHeld  = GetLeftHeld();
        bool rightHeld = GetRightHeld();

        if (!leftHeld)  _leftButtonWasDown  = false;
        if (!rightHeld) _rightButtonWasDown = false;

        var leftSpell = magicCaster.GetSpellForSlot(MagicSlot.Left);
        if (leftSpell != null && leftSpell.kind == MagicKind.Levitation
            && leftHeld && !_leftButtonWasDown)
        {
            _leftButtonWasDown = true;
            TryStartLevitation(MagicSlot.Left, leftSpell);
            return;
        }

        var rightSpell = magicCaster.GetSpellForSlot(MagicSlot.Right);
        if (rightSpell != null && rightSpell.kind == MagicKind.Levitation
            && rightHeld && !_rightButtonWasDown)
        {
            _rightButtonWasDown = true;
            TryStartLevitation(MagicSlot.Right, rightSpell);
        }
    }

    bool TryStartLevitation(MagicSlot slot, MagicSpellSO spell)
    {
        if (!magicCaster.CanCastSpell(slot)) return false;

        if (manaPool != null && !manaPool.TrySpend(spell.manaCost))
        {
            if (showDebugLogs) Debug.Log("[Levitation] Maná insuficiente");
            return false;
        }

        var targets = FindTargetsInCone(spell);

        _phase               = LevitationPhase.Levitating;
        _activeSlot          = slot;
        _activeSpell         = spell;
        _levitationStartTime = Time.time;

        _currentTargets.Clear();
        _currentTargets.AddRange(targets);

        foreach (var t in targets)
            t.BeginLevitation(this, spell);

        PlayHoldAnimation(slot);
        SpawnHoldVFX(spell);
        SpawnRangeIndicators(spell);

        if (targets.Count > 0)
            FeedbackService.CameraShake(spell.levitationCaptureShakeIntensity, spell.levitationCaptureShakeDuration);

        if (!string.IsNullOrEmpty(spell.castSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(spell.castSFXKey);

        if (showDebugLogs) Debug.Log($"[Levitation] Iniciando con {targets.Count} objetivos");
        return true;
    }

    // ── Actualización durante el hold ────────────────────────────────────────

    void UpdateLevitation()
    {
        if (_activeSpell == null) return;

        float elapsed = Time.time - _levitationStartTime;

        // Drenaje de maná tras el delay inicial
        if (elapsed > _activeSpell.levitationDrainDelay && manaPool != null)
        {
            float drain = _activeSpell.levitationManaDrainPerSecond * Time.deltaTime;
            if (manaPool.Current < drain)
            {
                CancelLevitationNoMana();
                return;
            }
            manaPool.TrySpend(drain);
        }

        // Sin objetivos: seguir buscando mientras el botón esté pulsado
        if (_currentTargets.Count == 0)
        {
            var newTargets = FindTargetsInCone(_activeSpell);
            if (newTargets.Count > 0)
            {
                _currentTargets.AddRange(newTargets);
                foreach (var t in newTargets)
                    t.BeginLevitation(this, _activeSpell);
                FeedbackService.CameraShake(_activeSpell.levitationCaptureShakeIntensity, _activeSpell.levitationCaptureShakeDuration);
                if (showDebugLogs) Debug.Log($"[Levitation] Objetivo capturado durante hold: {newTargets.Count}");
            }
        }

        Vector3 holdPos = transform.position + transform.forward * _activeSpell.levitationHoldDistance;

        for (int i = _currentTargets.Count - 1; i >= 0; i--)
        {
            var t = _currentTargets[i];
            if (t == null || !t.IsBeingLevitated)
            {
                _currentTargets.RemoveAt(i);
                continue;
            }
            t.UpdateLevitation(_activeSpell, holdPos, _activeSpell.levitationPullForce);
        }
    }

    // ── Lanzamiento al soltar ────────────────────────────────────────────────

    void CheckForRelease()
    {
        bool released  = (_activeSlot == MagicSlot.Left  && GetLeftReleased()) ||
                         (_activeSlot == MagicSlot.Right && GetRightReleased());
        bool stillHeld = (_activeSlot == MagicSlot.Left  && GetLeftHeld()) ||
                         (_activeSlot == MagicSlot.Right && GetRightHeld());

        if (released || !stillHeld)
            EndLevitation();
    }

    void EndLevitation()
    {
        if (_phase != LevitationPhase.Levitating) return;

        Vector3 pushDir = transform.forward;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 0.01f) pushDir = Vector3.forward;
        pushDir.Normalize();

        if (_currentTargets.Count > 0 && _activeSpell != null)
            FeedbackService.CameraShake(_activeSpell.levitationReleaseShakeIntensity, _activeSpell.levitationReleaseShakeDuration);

        foreach (var t in _currentTargets)
        {
            if (t == null) continue;
            SpawnReleaseVFX(t.transform.position);
            t.EndLevitation(_activeSpell, pushDir, _activeSpell.levitationPushForce);
        }

        DestroyHoldVFX();
        DestroyRangeIndicators();
        PlayReleaseAnimation();

        _phase = LevitationPhase.Idle;
        _activeSpell = null;
        _currentTargets.Clear();

        if (showDebugLogs) Debug.Log("[Levitation] Lanzamiento ejecutado");
    }

    void CancelLevitationNoMana()
    {
        if (_phase != LevitationPhase.Levitating) return;

        foreach (var t in _currentTargets)
        {
            if (t != null) t.CancelLevitation();
        }

        DestroyHoldVFX();
        DestroyRangeIndicators();
        StopHoldAnimationCoroutine();
        StartCoroutine(Co_LowerLayerWeight());

        _phase = LevitationPhase.Idle;
        _activeSpell = null;
        _currentTargets.Clear();
    }

    // ── API para IA aliada ───────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta levitación desde la IA aliada. Si hay objetivos en el cono,
    /// los levita hasta que se llame EndLevitation (o se agote el maná).
    /// Devuelve true si se inició con éxito.
    /// </summary>
    public bool TriggerAILevitation(MagicSlot slot, MagicSpellSO spell)
    {
        if (_phase != LevitationPhase.Idle) return false;
        if (spell == null || spell.kind != MagicKind.Levitation) return false;
        return TryStartLevitation(slot, spell);
    }

    /// <summary>
    /// Fuerza el lanzamiento (para la IA, que decide cuándo soltar).
    /// </summary>
    public void AIEndLevitation() => EndLevitation();

    // ── Detección de objetivos ───────────────────────────────────────────────

    List<LevitationTarget> FindTargetsInCone(MagicSpellSO spell)
    {
        var results   = new List<LevitationTarget>();
        Vector3 origin  = transform.position + Vector3.up * detectionHeightOffset;
        float halfAngle = spell.levitationAngle * 0.5f;

        int count = Physics.OverlapSphereNonAlloc(origin, spell.levitationRange, _levitationTargetBuffer, spell.levitationTargetLayers);

        for (int i = 0; i < count; i++)
        {
            var target = _levitationTargetBuffer[i].GetComponentInParent<LevitationTarget>();
            if (target == null || !target.CanBeLevitated) continue;

            Vector3 toTarget = target.transform.position - origin;
            toTarget.y = 0f;
            if (Vector3.Angle(transform.forward, toTarget) > halfAngle) continue;

            if (!results.Contains(target))
                results.Add(target);
        }

        return results;
    }

    // ── Animación ────────────────────────────────────────────────────────────

    void PlayHoldAnimation(MagicSlot slot)
    {
        if (animator == null) return;

        _currentMagicStatePath = slot == MagicSlot.Left ? "UpperBody.Magic.MagicLeft" : "UpperBody.Magic.MagicRight";
        _currentMagicStateHash = Animator.StringToHash(_currentMagicStatePath);

        animator.SetLayerWeight(_upperBodyLayerIndex, 1f);
        animator.Play(_currentMagicStatePath, _upperBodyLayerIndex, 0f);
    }

    /// <summary>
    /// Mantiene la pose de hold en el upper body sin tocar la velocidad global del Animator.
    /// Ejecutar en LateUpdate garantiza que el override llega DESPUÉS de que el Animator
    /// procese sus layers internamente, por lo que el frame renderizado siempre muestra
    /// holdPauseNormalizedTime sin el jitter que produce hacerlo en un coroutine de Update.
    /// </summary>
    void LateUpdate()
    {
        if (_phase != LevitationPhase.Levitating) return;
        if (animator == null || string.IsNullOrEmpty(_currentMagicStatePath)) return;

        var info = animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
        bool inRightState = info.fullPathHash  == _currentMagicStateHash
                         || info.shortNameHash == _currentMagicStateHash;
        if (!inRightState || info.normalizedTime < holdPauseNormalizedTime) return;

        animator.Play(_currentMagicStatePath, _upperBodyLayerIndex, holdPauseNormalizedTime);
        // Forzar re-evaluación inmediata para este frame (sin avanzar tiempo)
        animator.Update(0f);
    }

    void StopHoldAnimationCoroutine()
    {
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
    }

    void PlayReleaseAnimation()
    {
        // Al cambiar _phase a Idle, LateUpdate deja de sobreescribir → la animación
        // continúa desde holdPauseNormalizedTime hacia el final naturalmente.
        if (animator == null) return;

        if (!string.IsNullOrEmpty(_currentMagicStatePath))
            animator.Play(_currentMagicStatePath, _upperBodyLayerIndex, holdPauseNormalizedTime);

        StartCoroutine(Co_WaitAnimationEndAndLowerLayer());
    }

    IEnumerator Co_LowerLayerWeight()
    {
        float t = 0f, duration = 0.22f;
        float start = animator != null ? animator.GetLayerWeight(_upperBodyLayerIndex) : 0f;

        while (t < duration && animator != null)
        {
            t += Time.deltaTime;
            animator.SetLayerWeight(_upperBodyLayerIndex, Mathf.Lerp(start, 0f, t / duration));
            yield return null;
        }

        if (animator != null) animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }

    IEnumerator Co_WaitAnimationEndAndLowerLayer()
    {
        if (animator == null) yield break;

        float clipDuration = 0.5f;
        var stateInfo = animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
        if (stateInfo.length > 0) clipDuration = stateInfo.length;

        yield return new WaitForSeconds(clipDuration * (1f - holdPauseNormalizedTime) + 0.1f);

        yield return StartCoroutine(Co_LowerLayerWeight());
    }

    // ── VFX ─────────────────────────────────────────────────────────────────

    void SpawnHoldVFX(MagicSpellSO spell)
    {
        if (spell.levitationHoldVFX == null) return;
        _holdVFXInstance = Instantiate(spell.levitationHoldVFX, transform.position, transform.rotation);
        _holdVFXInstance.transform.SetParent(transform);
        _holdVFXInstance.transform.localPosition = Vector3.up * 1.2f;
    }

    void DestroyHoldVFX()
    {
        if (_holdVFXInstance != null)
        {
            Destroy(_holdVFXInstance);
            _holdVFXInstance = null;
        }
    }

    void SpawnRangeIndicators(MagicSpellSO spell)
    {
        if (spell.levitationRangeIndicatorVFX == null) return;
        DestroyRangeIndicators();

        float range = spell.levitationRange;
        int count   = Mathf.Max(1, spell.rangeIndicatorCount);

        for (int i = 0; i < count; i++)
        {
            float distance = (range / count) * (i + 1);
            Vector3 pos    = transform.position + transform.forward * distance;
            pos.y          = transform.position.y + 0.1f;

            var indicator = Instantiate(spell.levitationRangeIndicatorVFX, pos, Quaternion.identity);
            indicator.transform.SetParent(transform);
            _rangeIndicatorInstances.Add(indicator);
        }
    }

    void DestroyRangeIndicators()
    {
        foreach (var v in _rangeIndicatorInstances)
            if (v != null) Destroy(v);
        _rangeIndicatorInstances.Clear();
    }

    void SpawnReleaseVFX(Vector3 position)
    {
        if (_activeSpell == null || _activeSpell.levitationReleaseVFX == null) return;
        var vfx = Instantiate(_activeSpell.levitationReleaseVFX, position, Quaternion.identity);
        if (_activeSpell.vfxLifetime > 0f) Destroy(vfx, _activeSpell.vfxLifetime);
    }

    // ── Input (Reflexión — bug I7 pendiente) ─────────────────────────────────

    void InitializeReflection()
    {
        if (_reflectionInitialized) return;
        _reflectionInitialized = true;

        try
        {
            _gamepadReaderType = System.Type.GetType("Core.GamepadInputReader, Assembly-CSharp");
            if (_gamepadReaderType == null) return;

            const System.Reflection.BindingFlags f = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
            _leftHeldProp      = _gamepadReaderType.GetProperty("AttackMagicLeftHeld",     f);
            _leftReleasedProp  = _gamepadReaderType.GetProperty("AttackMagicLeftReleased", f);
            _rightHeldProp     = _gamepadReaderType.GetProperty("AttackMagicRightHeld",    f);
            _rightReleasedProp = _gamepadReaderType.GetProperty("AttackMagicRightReleased",f);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerLevitationController] Error en reflexión: {ex.Message}");
        }
    }

    bool GetLeftHeld()      => _leftHeldProp      != null && (bool)_leftHeldProp.GetValue(null);
    bool GetLeftReleased()  => _leftReleasedProp  != null && (bool)_leftReleasedProp.GetValue(null);
    bool GetRightHeld()     => _rightHeldProp     != null && (bool)_rightHeldProp.GetValue(null);
    bool GetRightReleased() => _rightReleasedProp != null && (bool)_rightReleasedProp.GetValue(null);

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    void OnDisable()
    {
        foreach (var t in _currentTargets)
            if (t != null) t.CancelLevitation();

        StopHoldAnimationCoroutine();
        DestroyHoldVFX();
        DestroyRangeIndicators();

        if (animator != null)
            animator.SetLayerWeight(_upperBodyLayerIndex, 0f);

        _phase = LevitationPhase.Idle;
        _currentTargets.Clear();
        _leftButtonWasDown  = false;
        _rightButtonWasDown = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        MagicSpellSO spell = null;
        if (Application.isPlaying && magicCaster != null)
        {
            spell = magicCaster.GetSpellForSlot(MagicSlot.Left);
            if (spell == null || spell.kind != MagicKind.Levitation)
                spell = magicCaster.GetSpellForSlot(MagicSlot.Right);
        }
        if (spell == null || spell.kind != MagicKind.Levitation) return;

        Vector3 origin    = transform.position + Vector3.up * detectionHeightOffset;
        float halfAngle   = spell.levitationAngle * 0.5f;
        Vector3 fwd       = transform.forward * spell.levitationRange;

        Gizmos.color = _phase == LevitationPhase.Levitating ? Color.magenta : Color.cyan;
        Gizmos.DrawLine(origin, origin + fwd);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0, -halfAngle, 0) * fwd);
        Gizmos.DrawLine(origin, origin + Quaternion.Euler(0,  halfAngle, 0) * fwd);
        Gizmos.DrawWireSphere(origin, 0.2f);
    }
#endif
}
