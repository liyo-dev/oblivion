using System.Collections;
using Alex.NPC.Common;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCSimpleAnimator : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Tooltip("Nombre EXACTO del estado (Blend Tree) de locomoción en la capa Base.")]
    [SerializeField] private string locomotionState = "Free Locomotion";

    [SerializeField] private string greetState = "Greeting01_NoWeapon";
    [SerializeField] private string interactState = "InteractWithPeople_NoWeapon";

    [Header("Saludo automático")]
    [SerializeField] private bool greetOnSight = true;
    [SerializeField] private float greetRadius = 3.0f;
    [Range(1f, 180f)] [SerializeField] private float fovDegrees = 110f;
    [SerializeField] private bool requirePlayerLookingAtMe = true;
    [Range(0f, 1f)] [SerializeField] private float playerLookDotThreshold = 0.6f;
    [SerializeField] private float greetCooldown = 4.0f;
    [SerializeField] private LayerMask occluders = ~0;

    [Header("Rotación al interactuar")]
    [SerializeField] private bool rotateToPlayerOnInteract = true;
    [SerializeField] private float rotateSpeed = 10f;

    [Header("Referencias")]
    [SerializeField] private Transform playerOverride;
    [SerializeField] private Transform lookFrom;
    [SerializeField] private AmbientInhibitor ambientInhibitor;

    [Header("Depuración")]
    [SerializeField] private bool drawGizmos = true;

    static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");

    AnimatorStateCache _stateCache;
    AnimatorClipCache _clipCache;

    bool _isInteracting;
    bool _greetOnCooldown;
    Transform _player;
    Transform _playerCam;
    Coroutine _faceRoutine;
    string _interactOverride;
    bool _clearOverrideOnEnd;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        lookFrom = transform;
        ambientInhibitor = GetComponent<AmbientInhibitor>();
    }

    void Awake()
    {
        animator ??= GetComponentInChildren<Animator>(true);
        lookFrom ??= transform;
        ambientInhibitor ??= GetComponent<AmbientInhibitor>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            _stateCache = new AnimatorStateCache(animator);
            _clipCache = new AnimatorClipCache(animator);
        }

        ResolvePlayerReferences();
        BindInteractable();
    }

    void Start()
    {
        PlayLocomotion();
        if (animator != null)
            animator.SetFloat(InputMagnitudeHash, 0f);
    }

    void Update()
    {
        if (!greetOnSight || _isInteracting || !_player || animator == null)
            return;

        var origin = LookTransform;
        var toPlayer = _player.position - origin.position;

        if (toPlayer.sqrMagnitude > greetRadius * greetRadius)
            return;

        if (!IsInsideFov(origin.forward, toPlayer))
            return;

        if (requirePlayerLookingAtMe && _playerCam && !IsPlayerLookingAtNpc(origin.position))
            return;

        if (HasOcclusion(origin.position, toPlayer))
            return;

        if (!_greetOnCooldown)
            StartCoroutine(DoGreeting());
    }

    void ResolvePlayerReferences()
    {
        _player = playerOverride ? playerOverride : PlayerLocator.ResolvePlayer();
        if (_player)
            PlayerService.RegisterComponent(_player, false);

        _playerCam = PlayerLocator.ResolvePlayerCamera();
    }

    void BindInteractable()
    {
        var interactable = GetComponent<Interactable>();
        if (interactable == null)
            return;

        interactable.OnStarted.AddListener(BeginInteraction);
        interactable.OnFinished.AddListener(EndInteraction);
    }

    Transform LookTransform => lookFrom ? lookFrom : transform;

    bool IsInsideFov(Vector3 forward, Vector3 toPlayer)
    {
        var dir = toPlayer.normalized;
        float dot = Vector3.Dot(forward, dir);
        float fovDot = Mathf.Cos(0.5f * fovDegrees * Mathf.Deg2Rad);
        return dot >= fovDot;
    }

    bool IsPlayerLookingAtNpc(Vector3 npcPosition)
    {
        Vector3 toNpc = (npcPosition - _playerCam.position).normalized;
        float lookDot = Vector3.Dot(_playerCam.forward, toNpc);
        return lookDot >= playerLookDotThreshold;
    }

    bool HasOcclusion(Vector3 origin, Vector3 toPlayer)
    {
        var dir = toPlayer.normalized;
        if (!Physics.Raycast(origin + Vector3.up * 1.6f, dir, out var hit, greetRadius, occluders))
            return false;

        return hit.transform != _player && !hit.transform.IsChildOf(_player);
    }

    // ===== Interacción =====
    public void BeginInteraction()
    {
        if (_isInteracting)
            return;

        _isInteracting = true;
        ambientInhibitor?.Lock();

        StopFacing();
        if (rotateToPlayerOnInteract && _player)
            _faceRoutine = StartCoroutine(FaceTarget(_player));

        string targetState = string.IsNullOrEmpty(_interactOverride) ? interactState : _interactOverride;
        CrossFade(targetState, 0.1f);
    }

    public void EndInteraction()
    {
        if (!_isInteracting)
            return;

        _isInteracting = false;

        StopFacing();
        PlayLocomotion();
        ambientInhibitor?.Unlock();

        if (_clearOverrideOnEnd)
            ClearInteractOverride();
    }

    IEnumerator FaceTarget(Transform target)
    {
        while (_isInteracting && target)
        {
            var anchor = LookTransform;
            Vector3 dir = target.position - anchor.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
                anchor.rotation = Quaternion.Slerp(anchor.rotation, desired, Time.deltaTime * rotateSpeed);
            }
            yield return null;
        }
    }

    void StopFacing()
    {
        if (_faceRoutine == null) return;
        StopCoroutine(_faceRoutine);
        _faceRoutine = null;
    }

    // ===== Saludo =====
    IEnumerator DoGreeting()
    {
        _greetOnCooldown = true;

        CrossFade(greetState, 0.08f);
        float len = Mathf.Max(0.01f, _clipCache?.GetLength(greetState) ?? 0f);
        yield return new WaitForSeconds(len);

        if (!_isInteracting)
            PlayLocomotion();

        yield return new WaitForSeconds(greetCooldown);
        _greetOnCooldown = false;
    }

    // ===== API pública =====
    public void SetPlayer(Transform newPlayer, Transform newPlayerCam = null)
    {
        _player = newPlayer;
        _playerCam = newPlayerCam ? newPlayerCam : (_playerCam ?? PlayerLocator.ResolvePlayerCamera());
    }

    public void SetInteractOverride(string stateName, bool clearOnEnd = true)
    {
        _interactOverride = stateName;
        _clearOverrideOnEnd = clearOnEnd && !string.IsNullOrEmpty(stateName);
    }

    public void ClearInteractOverride()
    {
        _interactOverride = null;
        _clearOverrideOnEnd = false;
    }

    public void TriggerGreeting()
    {
        if (!_greetOnCooldown && !_isInteracting)
            StartCoroutine(DoGreeting());
    }

    public void SetMovementSpeed(float normalizedSpeed, float dampTime = 0.1f)
    {
        if (!animator) return;
        animator.SetFloat(InputMagnitudeHash, Mathf.Clamp01(normalizedSpeed), dampTime, Time.deltaTime);
    }

    public void ResetMovement() => SetMovementSpeed(0f);

    public void PlayOneShot(string stateName)
    {
        StartCoroutine(CoPlayOneShot(stateName));
    }

    IEnumerator CoPlayOneShot(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null)
            yield break;

        CrossFade(stateName, 0.08f);
        float len = Mathf.Max(0.01f, _clipCache?.GetLength(stateName) ?? 0f);
        yield return new WaitForSeconds(len);

        if (!_isInteracting)
            PlayLocomotion();
    }

    // ===== Helpers de animación =====
    void PlayLocomotion()
    {
        if (string.IsNullOrEmpty(locomotionState))
            return;

        if (!_stateCache?.CrossFade(locomotionState, 0.1f) ?? true)
            animator?.CrossFadeInFixedTime(locomotionState, 0.1f, 0, 0f);
    }

    void CrossFade(string stateName, float fade)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null)
            return;

        if (!_stateCache?.CrossFade(stateName, fade) ?? true)
            animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        var origin = LookTransform;
        Vector3 pos = origin.position;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        Gizmos.DrawSphere(pos, greetRadius);

        float half = 0.5f * fovDegrees * Mathf.Deg2Rad;
        Vector3 fwd = origin.forward;
        Vector3 left = Quaternion.Euler(0, -Mathf.Rad2Deg * half, 0) * fwd;
        Vector3 right = Quaternion.Euler(0, Mathf.Rad2Deg * half, 0) * fwd;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawLine(pos, pos + left.normalized * greetRadius);
        Gizmos.DrawLine(pos, pos + right.normalized * greetRadius);
    }
}
