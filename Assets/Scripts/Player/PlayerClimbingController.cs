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

    [Header("Animación")]
    [SerializeField] private string climbUpState = "ClimbUp_RM_NoWeapon";
    [SerializeField] private string climbDownState = "ClimbDown_RM_NoWeapon";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

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
    private float _originalExtraGravity;
    private bool _cachedExtraGravity;
    private bool _controllerWasEnabled = true;

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
            if (!CanKeepClimbing(out var hit))
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
            if (!_actionManager.CanClimb()) return false;
            if (_actionManager.IsInMode(ActionMode.Swimming) || _actionManager.IsInMode(ActionMode.Flying))
                return false;
        }
        return climbableLayers.value != 0; // se necesita una capa específica
    }

    private bool CanKeepClimbing(out RaycastHit hit)
    {
        bool valid = FindClimbable(out hit);
        if (!valid)
            return false;
        if (_actionManager != null && !_actionManager.CanClimb())
            return false;
        return true;
    }

    private bool FindClimbable(out RaycastHit hit)
    {
        Vector3 origin = transform.position + Vector3.up * (_capsule != null ? _capsule.height * 0.4f : 0.5f);
        Vector3 dir = transform.forward;
        bool detected = Physics.SphereCast(origin, checkRadius, dir, out hit, checkDistance, climbableLayers, QueryTriggerInteraction.Ignore);
        if (debugLogs)
            Debug.DrawRay(origin, dir * checkDistance, detected ? Color.green : Color.red);
        return detected;
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

        PlayClimb(true);

        if (debugLogs)
            Debug.Log("[PlayerClimbingController] Enter Climb");
    }

    private void HandleClimbMovement()
    {
        Vector2 move = _controls != null ? _controls.GamePlay.Move.ReadValue<Vector2>() : Vector2.zero;
        float vertical = Mathf.Clamp(move.y, -1f, 1f);

        if (Mathf.Abs(vertical) > MinInputToAnimate)
            PlayClimb(vertical > 0f);

        Vector3 motion = Vector3.up * vertical * climbSpeed * Time.deltaTime;
        transform.position += motion;
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

    private void PlayClimb(bool movingUp)
    {
        if (_animator == null)
            return;

        string state = movingUp ? climbUpState : climbDownState;
        if (!string.IsNullOrEmpty(state))
            _animator.CrossFade(state, 0.05f);
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

        if (debugLogs)
            Debug.Log("[PlayerClimbingController] Exit Climb");
    }
}
