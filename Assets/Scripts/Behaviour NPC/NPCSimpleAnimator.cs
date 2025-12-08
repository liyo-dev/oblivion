using System.Collections;
using Game.NPC.Common;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCSimpleAnimator : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Tooltip("Nombre EXACTO del estado (Blend Tree) de locomoción en la capa Base.")]
    [SerializeField] private string locomotionState = "Free Locomotion";

    [Tooltip("Estado de idle durante batalla en la Base Layer (ej: Idle_Battle_NoWeapon)")]
    [SerializeField] private string battleIdleState = "Idle_Battle_NoWeapon";

    [Header("Layers del Animator")]
    [Tooltip("Índice de la capa para animaciones del torso superior (ataques, etc.)")]
    [SerializeField] private int upperBodyLayer = 1;
    [Tooltip("Estado de idle de la capa UpperBody (ej: UpperIdle)")]
    [SerializeField, HideInInspector] private string upperBodyIdleState = "UpperIdle";

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
    [SerializeField] private bool verboseLogs = false;
    [Tooltip("Para depurar máscaras/capas: reproducir one-shots en capa base")]
    [SerializeField] private bool forceOneShotsOnBaseLayer = false;

    static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");
    [Header("Locomoción - Tuning")]
    [Tooltip("Escala para el parámetro de entrada de locomoción (InputMagnitude)")]
    [SerializeField, Range(0.5f, 2.0f)] private float movementParamScale = 1.25f;
    [Tooltip("Multiplicador de velocidad de reproducción del Animator durante locomoción")]
    [SerializeField, Range(0.6f, 2.0f)] private float locomotionAnimSpeed = 1.25f;
    [Tooltip("Valor mínimo de blend cuando hay movimiento para evitar patinaje")]
    [SerializeField, Range(0f, 1f)] public float minBlendWhileMoving = 0.7f;

    AnimatorStateCache _stateCache;
    AnimatorClipCache _clipCache;

    bool _isInteracting;
    bool _greetOnCooldown;
    bool _inBattleMode;
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
        if (normalizedSpeed > 0.05f)
            normalizedSpeed = Mathf.Max(normalizedSpeed, minBlendWhileMoving);
        // Calibrar el parámetro de locomoción y la velocidad de reproducción para reducir foot sliding
        float scaled = Mathf.Clamp01(normalizedSpeed * movementParamScale);
        animator.SetFloat(InputMagnitudeHash, scaled, dampTime, Time.deltaTime);
        // Acelera la reproducción de la locomoción en función del movimiento, pero no sobrepasa el tope
        animator.speed = Mathf.Lerp(1f, locomotionAnimSpeed, scaled);
        // Si hay movimiento apreciable, asegurarse de estar en locomoción
        if (_inBattleMode && normalizedSpeed >= 0.05f)
            PlayLocomotion();
    }

    public void ResetMovement() => SetMovementSpeed(0f, 0f);

    public void SetBattleMode(bool enable)
    {
        _inBattleMode = enable;
        if (animator)
        {
            // Asegurar que la capa de UpperBody tenga peso cuando estamos en combate
            float w = enable ? 1f : 0f;
            int layer = Mathf.Clamp(upperBodyLayer, 0, animator.layerCount - 1);
            if (layer > 0)
                animator.SetLayerWeight(layer, w);
        }
        if (!enable)
        {
            PlayLocomotion();
        }
        // Resetear velocidad global para no afectar a futuros one-shots
        if (animator) animator.speed = 1f;
        // Cuando se activa el modo batalla, mantener locomotion activo para poder moverse
        // El battleIdleState se activará automáticamente cuando speed = 0
    }

    public void PlayBattleIdle()
    {
        if (_inBattleMode && !string.IsNullOrEmpty(battleIdleState))
        {
            if (animator) animator.speed = 1f;
            CrossFade(battleIdleState, 0.2f);
        }
    }

    public void PlayOneShot(string stateName, int layer = 0)
    {
        StartCoroutine(CoPlayOneShot(stateName, layer));
    }

    IEnumerator CoPlayOneShot(string stateName, int layer = 0)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null)
            yield break;

        // Asegurar que la velocidad global es 1 para no acelerar el ataque
        animator.speed = 1f;
        int targetLayer = forceOneShotsOnBaseLayer ? 0 : Mathf.Clamp(layer, 0, animator.layerCount - 1);
        // Si la capa es distinta de base, asegurar que tiene peso
        if (targetLayer > 0)
            animator.SetLayerWeight(targetLayer, 1f);

        // Lanzar el crossfade hacia el estado solicitado
        int stateHash = Animator.StringToHash(stateName);
        if (verboseLogs) Debug.Log($"[NPCSimpleAnimator] PlayOneShot -> '{stateName}' en capa {targetLayer}", this);
        CrossFade(stateName, 0.08f, targetLayer);

        // Dejar arrancar la transición al siguiente frame
        yield return null;

        // Intentar determinar la duración real del estado activo en esa capa
        float waitSeconds = 0.25f; // fallback breve por si no se puede resolver
        var current = animator.GetCurrentAnimatorStateInfo(targetLayer);
        var next = animator.GetNextAnimatorStateInfo(targetLayer);
        if (current.shortNameHash == stateHash)
        {
            waitSeconds = Mathf.Max(0.01f, current.length);
            if (verboseLogs) Debug.Log($"[NPCSimpleAnimator] Estado activo coincide. len={waitSeconds:F2}", this);
        }
        else if (next.shortNameHash == stateHash)
        {
            waitSeconds = Mathf.Max(0.01f, next.length);
            if (verboseLogs) Debug.Log($"[NPCSimpleAnimator] Estado en transición coincide. len={waitSeconds:F2}", this);
        }
        else
        {
            // Fallback por nombre de clip en cache
            float clipLen = _clipCache?.GetLength(stateName) ?? 0f;
            if (clipLen > 0f) waitSeconds = clipLen;
            if (verboseLogs) Debug.LogWarning($"[NPCSimpleAnimator] No se pudo resolver estado por hash en capa {targetLayer}. Fallback len={waitSeconds:F2}", this);
        }

        // Esperar a que la animación termine (preferible por normalizedTime)
        float safetyCap = Mathf.Min(3.0f, waitSeconds + 0.2f);
        float elapsed = 0f;
        bool finished = false;
        while (elapsed < safetyCap)
        {
            var st = animator.GetCurrentAnimatorStateInfo(targetLayer);
            if (st.shortNameHash == stateHash && st.normalizedTime >= 0.98f)
            {
                finished = true;
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (verboseLogs) Debug.Log($"[NPCSimpleAnimator] PlayOneShot fin. finished={finished}, elapsed={elapsed:F2}", this);

        // Después del ataque, volver al idle de batalla si estamos en modo batalla
        // (El CombatBrain se encargará de cambiar a locomotion cuando necesite moverse)
        if (_inBattleMode)
        {
            // Si fue una animación en upperBodyLayer, volver a idle correspondiente de UpperBody
            if (targetLayer == upperBodyLayer && !string.IsNullOrEmpty(upperBodyIdleState))
                CrossFade(upperBodyIdleState, 0.15f, targetLayer);
            else if (!string.IsNullOrEmpty(battleIdleState))
                CrossFade(battleIdleState, 0.15f);
        }
        else if (!_isInteracting)
        {
            if (animator) animator.speed = 1f;
            PlayLocomotion();
        }
    }

    // ===== Helpers de animación =====
    void PlayLocomotion()
    {
        if (string.IsNullOrEmpty(locomotionState))
            return;

        if (!_stateCache?.CrossFade(locomotionState, 0.1f) ?? true)
            animator?.CrossFadeInFixedTime(locomotionState, 0.1f, 0, 0f);
    }

    void CrossFade(string stateName, float fade, int layer = 0)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null)
            return;

        // Si se especifica una capa != 0, usar directamente el animator
        if (layer != 0)
        {
            int hash = Animator.StringToHash(stateName);
            // Si el estado no existe en esa capa, intentar encontrar una capa válida
            if (layer < animator.layerCount && animator.HasState(layer, hash))
            {
                animator.CrossFadeInFixedTime(hash, fade, layer, 0f);
            }
            else
            {
                // Buscar en otras capas
                for (int i = 1; i < animator.layerCount; i++)
                {
                    if (animator.HasState(i, hash))
                    {
                        // Asegurar peso de la capa
                        animator.SetLayerWeight(i, 1f);
                        animator.CrossFadeInFixedTime(hash, fade, i, 0f);
                        return;
                    }
                }
                // Fallback a capa base si nada coincide
                animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
            }
            return;
        }

        // Para capa 0, usar el cache que resuelve automáticamente la capa
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
