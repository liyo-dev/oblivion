using UnityEngine;
using UnityEngine.InputSystem;
using Invector.vCharacterController;

/// <summary>
/// Control básico de escalada: detecta paredes en una capa concreta y bloquea el movimiento normal
/// mientras permite desplazarse verticalmente con animaciones dedicadas.
/// </summary>
[DefaultExecutionOrder(170)]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerClimbingController : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private LayerMask climbableLayers;
    [SerializeField] private float checkDistance = 0.6f;
    [SerializeField] private float checkRadius = 0.25f;
    [SerializeField, Tooltip("Distancia a la pared mientras se trepa.")]
    private float wallOffset = 0.35f;

    [Header("Movimiento")]
    [SerializeField] private float climbSpeed = 2.5f;
    [SerializeField] private float stickToWallSpeed = 10f;
    [SerializeField] private float reattachDelay = 0.35f;
    [SerializeField, Tooltip("Tiempo de gracia sin contacto antes de soltar la pared.")]
    private float loseGripGrace = 0.2f;

    [Header("Animación")]
    [SerializeField] private string climbUpState = "ClimbUp_RM_NoWeapon";
    [SerializeField] private string climbDownState = "ClimbDown_RM_NoWeapon";
    [SerializeField] private string climbIdleState = "ClimbIdle_RM_NoWeapon";
    [SerializeField, Tooltip("Índice de capa del Animator que contiene los estados de escalar. Base layer = 0.")]
    private int climbAnimatorLayer = 0;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField, Tooltip("Forzar al Animator como grounded mientras se escala para evitar estados de caída.")]
    private bool forceGroundedWhileClimbing = true;

    private Animator _animator;
    private PlayerActionManager _actionManager;
    private Rigidbody _rigidbody;
    private CapsuleCollider _capsule;
    private vThirdPersonController _controller;
    private PlayerControls _controls;
    private bool _ownsControls;
    private bool _isClimbing;
    private Vector3 _currentNormal;
    private float _lastDetachTime = -999f;
    private float _lastValidHitTime = -999f;
    private float _originalExtraGravity;
    private bool _cachedExtraGravity;
    private bool _controllerWasEnabled = true;
    private RaycastHit _lastHit;
    private string _lastMissingStateWarn;
    private int _lastClimbStateHash = -1;
    private float _originalAnimatorSpeed = 1f;

    private const float MinInputToAnimate = 0.1f;

    void OnValidate()
    {
        // Si no se configuró explícitamente, usa automáticamente la capa "Climb"
        if (climbableLayers == 0)
        {
            int climbLayer = LayerMask.NameToLayer("Climb");
            if (climbLayer >= 0)
                climbableLayers = 1 << climbLayer;
        }
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _actionManager = GetComponent<PlayerActionManager>();
        _rigidbody = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>();
        _controller = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
        _controls = PlayerInputManager.GetSharedOrNew(out _ownsControls);

        // Refuerzo en runtime por si el prefab se quedó sin la capa asignada
        if (climbableLayers == 0)
        {
            int climbLayer = LayerMask.NameToLayer("Climb");
            if (climbLayer >= 0)
                climbableLayers = 1 << climbLayer;
        }
    }

    void OnEnable()
    {
        if (_ownsControls)
            _controls?.Enable();
    }

    void OnDisable()
    {
        if (_ownsControls)
            _controls?.Disable();
        if (_isClimbing)
            ExitClimb(force: true);
    }

    void Update()
    {
        if (_animator == null || _rigidbody == null)
            return;

        if (_isClimbing)
        {
            if (!TryGetClimbHit(out var hit))
            {
                ExitClimb();
                return;
            }

            MaintainAttachment(hit);
            HandleClimbMovement();
            return;
        }

        if (Time.time - _lastDetachTime < reattachDelay)
            return;

        if (!CanStartClimb())
            return;

        if (FindClimbable(out var climbHit))
            EnterClimb(climbHit);
    }

    private bool CanStartClimb()
    {
        if (_actionManager != null)
        {
            if (!_actionManager.CanClimb())
            {
                if (debugLogs) Debug.Log("[PlayerClimbingController] Climb bloqueado por ActionManager");
                return false;
            }
            if (_actionManager.IsInMode(ActionMode.Swimming) || _actionManager.IsInMode(ActionMode.Flying))
            {
                if (debugLogs) Debug.Log("[PlayerClimbingController] Climb bloqueado por modo Swimming/Flying");
                return false;
            }
        }
        return climbableLayers.value != 0; // se necesita una capa específica
    }

    private bool TryGetClimbHit(out RaycastHit hit)
    {
        bool detected = FindClimbable(out var newHit);
        if (detected)
        {
            _lastHit = newHit;
            _lastValidHitTime = Time.time;
            hit = newHit;
            return true;
        }

        // Permite un breve margen para no soltar instantáneamente si el spherecast pierde contacto un frame
        if (Time.time - _lastValidHitTime <= loseGripGrace)
        {
            hit = _lastHit;
            return true;
        }

        hit = default;
        return false;
    }

    private bool FindClimbable(out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * (_capsule != null ? _capsule.height * 0.4f : 0.5f);
        Vector3 dir = transform.forward;
        bool detected = Physics.SphereCast(origin, checkRadius, dir, out hit, checkDistance, climbableLayers, QueryTriggerInteraction.Collide);
        if (debugLogs)
            Debug.DrawRay(origin, dir * checkDistance, detected ? Color.green : Color.red);
        return detected;
    }

    void OnDrawGizmosSelected()
    {
        // Ayuda visual para depurar el spherecast de trepa
        if (!debugLogs) return;
        Vector3 origin = transform.position + Vector3.up * (_capsule != null ? _capsule.height * 0.4f : 0.5f);
        Vector3 dir = transform.forward * checkDistance;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, checkRadius);
        Gizmos.DrawWireSphere(origin + dir, checkRadius);
        Gizmos.DrawLine(origin, origin + dir);
    }

    private void AlignToWall(RaycastHit hit)
    {
        _currentNormal = hit.normal;
        Vector3 targetPos = hit.point + hit.normal * wallOffset;
        Vector3 pos = transform.position;
        pos.x = targetPos.x;
        pos.z = targetPos.z;
        transform.position = pos;

        Vector3 forward = -hit.normal;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void EnterClimb(RaycastHit hit)
    {
        if (_isClimbing)
            return;

        _isClimbing = true;
        _currentNormal = hit.normal;
        AlignToWall(hit);

        if (_animator != null)
        {
            _originalAnimatorSpeed = _animator.speed;
            _animator.speed = 1f;
        }

        if (_actionManager != null)
            _actionManager.PushMode(ActionMode.Climbing);

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.useGravity = false;
        }

        if (_controller != null)
        {
            _controllerWasEnabled = _controller.enabled;
            _cachedExtraGravity = true;
            _originalExtraGravity = _controller.extraGravity;
            _controller.extraGravity = 0f;
            _controller.enabled = false;
        }

        PlayClimb(1f);

        if (debugLogs)
            Debug.Log("[PlayerClimbingController] Enter Climb");
    }

    private void HandleClimbMovement()
    {
        Vector2 move = _controls != null ? _controls.GamePlay.Move.ReadValue<Vector2>() : Vector2.zero;
        float vertical = Mathf.Clamp(move.y, -1f, 1f);

        PlayClimb(vertical);

        Vector3 motion = Vector3.up * vertical * climbSpeed * Time.deltaTime;
        transform.position += motion;

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (forceGroundedWhileClimbing && _animator != null)
        {
            try
            {
                _animator.SetBool(Invector.vCharacterController.vAnimatorParameters.IsGrounded, true);
                _animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, Mathf.Abs(vertical));
            }
            catch { }
        }
    }

    private void MaintainAttachment(RaycastHit hit)
    {
        _currentNormal = hit.normal;
        Vector3 targetPos = hit.point + hit.normal * wallOffset;
        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, targetPos.x, stickToWallSpeed * Time.deltaTime);
        pos.z = Mathf.Lerp(pos.z, targetPos.z, stickToWallSpeed * Time.deltaTime);
        transform.position = pos;

        Vector3 forward = -_currentNormal;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, stickToWallSpeed * Time.deltaTime);
        }
    }

    private void PlayClimb(float vertical)
    {
        if (_animator == null)
            return;

        string state;
        if (Mathf.Abs(vertical) <= MinInputToAnimate)
            state = climbIdleState;
        else
            state = vertical > 0f ? climbUpState : climbDownState;

        if (string.IsNullOrEmpty(state))
            return;

        if (climbAnimatorLayer < 0 || climbAnimatorLayer >= _animator.layerCount)
        {
            if (debugLogs && _lastMissingStateWarn != "_layer")
            {
                Debug.LogWarning("[PlayerClimbingController] Layer de escalada inválido (" + climbAnimatorLayer + ")");
                _lastMissingStateWarn = "_layer";
            }
            return;
        }

        int hash = Animator.StringToHash(state);
        if (!_animator.HasState(climbAnimatorLayer, hash))
        {
            if (debugLogs && _lastMissingStateWarn != state)
            {
                Debug.LogWarning("[PlayerClimbingController] Estado de animación no encontrado: " + state + " en capa " + climbAnimatorLayer);
                _lastMissingStateWarn = state;
            }
            return;
        }

        var current = _animator.GetCurrentAnimatorStateInfo(climbAnimatorLayer);

        // Si estamos casi quietos, usamos el último estado válido y pausamos la animación
        if (Mathf.Abs(vertical) <= MinInputToAnimate)
        {
            int targetHash = _lastClimbStateHash >= 0 ? _lastClimbStateHash : hash;
            if (current.shortNameHash != targetHash)
            {
                _animator.CrossFade(targetHash, 0.05f, climbAnimatorLayer);
            }
            _lastClimbStateHash = targetHash;
            _animator.speed = 0f;
            return;
        }

        // Con movimiento, reproducimos up/down y aseguramos velocidad normal
        if (current.shortNameHash != hash)
            _animator.CrossFade(hash, 0.05f, climbAnimatorLayer);

        _lastClimbStateHash = hash;
        _animator.speed = 1f;
    }

    private void ExitClimb(bool force = false)
    {
        if (!_isClimbing && !force)
            return;

        bool wasClimbing = _isClimbing;
        _isClimbing = false;
        _lastDetachTime = Time.time;

        if (wasClimbing && _actionManager != null)
            _actionManager.PopMode(ActionMode.Climbing);

        if (_rigidbody != null)
            _rigidbody.useGravity = true;

        if (_controller != null)
        {
            _controller.enabled = _controllerWasEnabled;
            if (_cachedExtraGravity)
                _controller.extraGravity = _originalExtraGravity;
        }

        if (_animator != null)
            _animator.speed = _originalAnimatorSpeed;

        if (debugLogs)
            Debug.Log("[PlayerClimbingController] Exit Climb");
    }
}
