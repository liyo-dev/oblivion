using System.Collections.Generic;
using Invector.vCharacterController;
using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// Gestiona el modo de nado del jugador: detecta agua, mantiene la altura y lanza animaciones/mode stack.
/// </summary>
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PlayerSwimmingController : MonoBehaviour
{
    private const float RaycastVerticalOffset = 3f;
    private const float RaycastDepth = 10f;
    private const float SurfaceMemoryTime = 0.4f;
    private const float EnterBuffer = 0.05f;
    private const float ExitBuffer = 0.08f;
    private const float VerticalAlignmentSpeed = 5f;
    private const float MaxVerticalSpeed = 6f;
    private const float ExitCrossFade = 0.1f;
    private const float SwimMoveSpeed = 2.5f;
    private const float SwimAcceleration = 12f;
    private const float SwimResponsiveness = 18f;

    [Header("Animación")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private string swimStateName = "Swimming_Floating_NoWeapon";
    [SerializeField] private string locomotionStateName = "Free Locomotion";

    [Header("Altura visual")]
    [SerializeField, Tooltip("Distancia entre el pivot del jugador y la superficie para quedar medio cuerpo fuera.")]
    private float surfaceOffset = 0.9f;

    [SerializeField] private bool debugLogs = false;

    private readonly List<Collider> _waterVolumes = new();
    private readonly HashSet<Collider> _waterSet = new();
    private readonly RaycastHit[] _raycastHits = new RaycastHit[8];
    private int _waterMask;
    private int _floorMask;
    private Animator _animator;
    private PlayerActionManager _actionManager;
    private Rigidbody _rigidbody;
    private CapsuleCollider _capsule;
    private vThirdPersonController _thirdPersonController;
    private Transform _cameraTransform;
    private bool _isSwimming;
    private float _lastSurfaceSeenTime;
    private float _rememberedSurfaceY;
    private float _desiredHeight;
    private bool _hasDesiredHeight;
    private float _cachedExtraGravity;
    private bool _suspendedExtraGravity;
    private int _storedGroundMask;
    private bool _groundMaskOverridden;
    private float _originalAirSpeed;
    private float _originalAirSmooth;
    private bool _airTuningApplied;
    private PlayerControls _controls;
    private bool _ownsControls;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _actionManager = GetComponent<PlayerActionManager>();
        _rigidbody = GetComponent<Rigidbody>();
        _capsule = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
        _thirdPersonController = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();

        _waterMask = LayerMask.GetMask("Water");
        if (_waterMask == 0)
            _waterMask = ~0; // fallback: cualquier capa

        if (surfaceOffset <= 0.01f && _capsule != null)
            surfaceOffset = Mathf.Max(0.2f, _capsule.height * 0.5f);

        _floorMask = LayerMask.GetMask("Floor");
        CacheCameraTransform();

        _controls = PlayerInputManager.GetSharedOrNew(out _ownsControls);
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
        if (_isSwimming)
            ExitSwimming(force: true);
    }

    void Update()
    {
        if (_animator == null)
            return;

        bool hasWater = CleanupWaterList();
        bool hasSurface = false;
        float surfaceY = 0f;

        if (hasWater && TryGetWaterSurface(out surfaceY))
        {
            hasSurface = true;
            _rememberedSurfaceY = surfaceY;
            _lastSurfaceSeenTime = Time.time;
        }
        else if (_isSwimming && Time.time - _lastSurfaceSeenTime <= SurfaceMemoryTime)
        {
            hasSurface = true;
            surfaceY = _rememberedSurfaceY;
        }

        if (!hasWater && _isSwimming && !hasSurface)
        {
            ExitSwimming();
            return;
        }

        if (_isSwimming)
        {
            if (!hasSurface || ShouldExit(surfaceY))
                ExitSwimming();
            else
            {
                MaintainBuoyancy(surfaceY);
                ForceAnimatorGrounded();
            }
        }
        else if (hasSurface && ShouldEnter(surfaceY))
        {
            EnterSwimming(surfaceY);
            MaintainBuoyancy(surfaceY);
        }
    }

    void FixedUpdate()
    {
        if (!_isSwimming || _rigidbody == null)
            return;

        if (_hasDesiredHeight)
        {
            var pos = _rigidbody.position;
            pos.y = Mathf.MoveTowards(pos.y, _desiredHeight, VerticalAlignmentSpeed * Time.fixedDeltaTime);
            _rigidbody.MovePosition(pos);
        }

        var vel = _rigidbody.linearVelocity;
        vel.y = Mathf.Clamp(vel.y, -MaxVerticalSpeed, MaxVerticalSpeed);
        vel = ApplySwimHorizontalControl(vel);
        _rigidbody.linearVelocity = vel;
    }

    bool CleanupWaterList()
    {
        bool any = false;
        for (int i = _waterVolumes.Count - 1; i >= 0; --i)
        {
            var col = _waterVolumes[i];
            if (!col)
            {
                _waterVolumes.RemoveAt(i);
                _waterSet.Remove(col);
            }
            else
            {
                any = true;
            }
        }
        return any;
    }

    bool TryGetWaterSurface(out float surfaceY)
    {
        surfaceY = 0f;
        if (TrySampleSurfaceWithRay(out surfaceY))
            return true;

        bool found = false;
        for (int i = _waterVolumes.Count - 1; i >= 0; --i)
        {
            var col = _waterVolumes[i];
            if (!col)
            {
                _waterVolumes.RemoveAt(i);
                _waterSet.Remove(col);
                continue;
            }

            float candidate = ResolveSurfaceHeight(col);
            if (!found || candidate > surfaceY)
            {
                surfaceY = candidate;
                found = true;
            }
        }
        return found;
    }

    bool TrySampleSurfaceWithRay(out float surfaceY)
    {
        surfaceY = 0f;
        if (_waterVolumes.Count == 0)
            return false;

        Vector3 origin = transform.position + Vector3.up * RaycastVerticalOffset;
        float maxDistance = Mathf.Max(0.5f, RaycastVerticalOffset + RaycastDepth);
        int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _raycastHits, maxDistance, _waterMask, QueryTriggerInteraction.Collide);
        if (hits <= 0)
            return false;

        bool found = false;
        float best = float.MinValue;
        for (int i = 0; i < hits; ++i)
        {
            var hit = _raycastHits[i];
            if (!IsTrackedWaterCollider(hit.collider))
                continue;
            if (!found || hit.point.y > best)
            {
                best = hit.point.y;
                found = true;
            }
        }

        if (found)
            surfaceY = best;

        return found;
    }

    float ResolveSurfaceHeight(Collider col)
    {
        if (col == null)
            return _rememberedSurfaceY;

        switch (col)
        {
            case BoxCollider box:
                var topLocal = box.center + Vector3.up * (box.size.y * 0.5f);
                return col.transform.TransformPoint(topLocal).y;

            case CapsuleCollider capsule:
                var half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                var capTop = capsule.center + Vector3.up * half;
                return col.transform.TransformPoint(capTop).y + capsule.radius;

            default:
                return col.bounds.max.y;
        }
    }

    bool ShouldEnter(float surfaceY)
    {
        SampleBody(out _, out _, out float chest, out _);
        return chest + EnterBuffer < surfaceY;
    }

    bool ShouldExit(float surfaceY)
    {
        SampleBody(out _, out _, out _, out float hips);
        return hips + ExitBuffer > surfaceY;
    }

    void MaintainBuoyancy(float surfaceY)
    {
        float targetY = surfaceY - surfaceOffset;
        _desiredHeight = targetY;
        _hasDesiredHeight = true;

        if (_rigidbody == null)
        {
            var pos = transform.position;
            pos.y = Mathf.MoveTowards(pos.y, targetY, VerticalAlignmentSpeed * Time.deltaTime);
            transform.position = pos;
        }
    }

    void ForceAnimatorGrounded()
    {
        if (_animator == null) return;
        _animator.SetBool(vAnimatorParameters.IsGrounded, true);
        _animator.SetFloat(vAnimatorParameters.GroundDistance, 0f);
    }

    void EnterSwimming(float surfaceY)
    {
        if (_isSwimming)
            return;

        _isSwimming = true;
        _rememberedSurfaceY = surfaceY;

        if (_actionManager != null)
            _actionManager.PushMode(ActionMode.Swimming);

        _animator.Play(swimStateName, locomotionLayerIndex, 0f);

        if (_rigidbody != null)
        {
            var vel = _rigidbody.linearVelocity;
            if (vel.y < 0f) vel.y = 0f;
            _rigidbody.linearVelocity = vel;
            _rigidbody.useGravity = false;
        }

        if (_thirdPersonController != null && !_suspendedExtraGravity)
        {
            _cachedExtraGravity = _thirdPersonController.extraGravity;
            _thirdPersonController.extraGravity = 0f;
            _suspendedExtraGravity = true;
        }

        if (_thirdPersonController != null)
        {
            if (!_airTuningApplied)
            {
                _originalAirSpeed = _thirdPersonController.airSpeed;
                _originalAirSmooth = _thirdPersonController.airSmooth;
                _airTuningApplied = true;
            }

            _thirdPersonController.airSpeed = SwimMoveSpeed;
            _thirdPersonController.airSmooth = SwimResponsiveness;
        }

        if (_thirdPersonController != null && !_groundMaskOverridden && _floorMask != 0)
        {
            _storedGroundMask = _thirdPersonController.groundLayer.value;
            int newMask = _storedGroundMask & ~_floorMask;
            _thirdPersonController.groundLayer = newMask;
            _groundMaskOverridden = true;
        }

        if (debugLogs)
            Debug.Log("[PlayerSwimmingController] Enter Swimming");
    }

    void ExitSwimming(bool force = false)
    {
        if (!_isSwimming && !force)
            return;

        bool wasSwimming = _isSwimming;
        _isSwimming = false;
        _hasDesiredHeight = false;

        if (wasSwimming && _actionManager != null)
            _actionManager.PopMode(ActionMode.Swimming);

        if (wasSwimming && _animator != null)
            _animator.CrossFade(locomotionStateName, ExitCrossFade, locomotionLayerIndex);

        if (_rigidbody != null)
            _rigidbody.useGravity = true;

        if (_thirdPersonController != null && _suspendedExtraGravity)
        {
            _thirdPersonController.extraGravity = _cachedExtraGravity;
            _suspendedExtraGravity = false;
        }

        if (_thirdPersonController != null && _groundMaskOverridden)
        {
            _thirdPersonController.groundLayer = _storedGroundMask;
            _groundMaskOverridden = false;
        }

        if (_thirdPersonController != null && _airTuningApplied)
        {
            _thirdPersonController.airSpeed = _originalAirSpeed;
            _thirdPersonController.airSmooth = _originalAirSmooth;
            _airTuningApplied = false;
        }

        if (wasSwimming && debugLogs)
            Debug.Log("[PlayerSwimmingController] Exit Swimming");
    }

    void SampleBody(out float feet, out float head, out float chest, out float hips)
    {
        if (_capsule != null)
        {
            var bounds = _capsule.bounds;
            feet = bounds.min.y;
            head = bounds.max.y;
        }
        else
        {
            feet = transform.position.y;
            head = feet + 1.7f;
        }

        chest = Mathf.Lerp(feet, head, 0.75f);
        hips = Mathf.Lerp(feet, head, 0.45f);
    }

    void RegisterWater(Collider other)
    {
        if (!other || !_waterSet.Add(other))
            return;

        _waterVolumes.Add(other);
    }

    void UnregisterWater(Collider other)
    {
        if (!other) return;
        if (_waterSet.Remove(other))
            _waterVolumes.Remove(other);
    }

    bool IsWaterCollider(Collider other)
    {
        if (!other) return false;
        int mask = 1 << other.gameObject.layer;
        return (mask & _waterMask) != 0;
    }

    Vector3 ApplySwimHorizontalControl(Vector3 currentVelocity)
    {
        if (_controls == null) return currentVelocity;

        Vector2 moveInput = _controls.GamePlay.Move.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude > 1f)
            moveInput.Normalize();

        Transform basis = GetReferenceBasis();

        Vector3 forward = basis.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;
        forward.Normalize();

        Vector3 right = basis.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.001f)
            right = new Vector3(forward.z, 0f, -forward.x);
        right.Normalize();

        Vector3 desired = forward * moveInput.y + right * moveInput.x;
        if (desired.sqrMagnitude > 1f)
            desired.Normalize();
        desired *= SwimMoveSpeed;

        Vector3 horizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        horizontal = Vector3.MoveTowards(horizontal, desired, SwimAcceleration * Time.fixedDeltaTime);

        currentVelocity.x = horizontal.x;
        currentVelocity.z = horizontal.z;
        return currentVelocity;
    }

    bool IsTrackedWaterCollider(Collider other) => other && _waterSet.Contains(other);

    Transform GetReferenceBasis()
    {
        if (_cameraTransform == null)
            CacheCameraTransform();
        return _cameraTransform != null ? _cameraTransform : transform;
    }

    void CacheCameraTransform()
    {
        var cam = Camera.main;
        if (cam != null)
            _cameraTransform = cam.transform;
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsWaterCollider(other))
            RegisterWater(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (IsWaterCollider(other))
            UnregisterWater(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsWaterCollider(collision.collider))
            RegisterWater(collision.collider);
    }

    void OnCollisionExit(Collision collision)
    {
        if (IsWaterCollider(collision.collider))
            UnregisterWater(collision.collider);
    }

    void OnDrawGizmosSelected()
    {
        if (!_isSwimming) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
    }
}
