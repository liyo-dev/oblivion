using Invector.vCharacterController;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla el modo de vuelo del jugador (tipo Dragon Ball): doble salto para entrar,
/// joystick izquierdo para direccionarse y mantener pulsado salto para descender/salir.
/// </summary>
[DefaultExecutionOrder(180)]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerFlyingController : MonoBehaviour
{
    private const float GroundCheckBuffer = 0.2f;

    [Header("Animación")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private string flyIdleState = "fly_idle";
    [SerializeField] private string flyMoveState = "fly_move";
    [SerializeField] private string flyDiveState = "fly_dive";
    [SerializeField] private string locomotionStateName = "Free Locomotion";

    [Header("Movimiento")]
    [SerializeField] private float horizontalSpeed = 12f;
    [SerializeField] private float verticalSpeed = 8f;
    [SerializeField] private float descendSpeed = 12f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float turnSpeed = 9f;
    [SerializeField] private float doubleTapWindow = 0.35f;
    [SerializeField] private float boostMultiplier = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Animator _animator;
    private Rigidbody _rigidbody;
    private vThirdPersonController _controller;
    private FieldInfo _lockMovementField;
    private FieldInfo _lockRotationField;
    private PlayerActionManager _actionManager;
    private CapsuleCollider _capsule;
    private Transform _cameraTransform;
    private PlayerControls _controls;
    private InputAction _moveAction;
    private InputAction _cameraAction;
    private InputAction _jumpAction;

    private Vector2 _moveInput;
    private Vector2 _cameraInput;
    private bool _jumpHeld;
    private bool _isFlying;
    private bool _extraGravitySuspended;
    private float _cachedExtraGravity;
    private bool _gravityStored;
    private bool _storedUseGravity;
    private float _flightArmUntil = -1f;
    private float _currentPlanarSpeed;
    private bool _isBoosting;
    private bool _controllerWasEnabled = true;
    private bool _animRootMotionPrev;
    private float _prevFlightLayerWeight = -1f;

    [Header("FX Vuelo")]
    [SerializeField] private Transform vfxAttach;
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private float trailSpeedThreshold = 1.5f;
    [SerializeField] private GameObject boostVfxPrefab;
    [SerializeField] private GameObject landingVfxPrefab;

    private GameObject _trailInstance;
    private ParticleSystem _trailPs;
    private GameObject _boostInstance;
    private ParticleSystem _boostPs;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        _controller = GetComponent<vThirdPersonController>();
        _actionManager = GetComponent<PlayerActionManager>();
        _capsule = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
        CacheControllerLockFields();
        CacheCameraTransform();
        _flightArmUntil = -1f;

        _controls = new PlayerControls();
        _moveAction = _controls.GamePlay.Move;
        _cameraAction = _controls.GamePlay.CameraLook;
        _jumpAction = _controls.GamePlay.Jump;

        // Try to auto-detect the animator layer that contains the flight states
        DetectFlightLayer();
    }

    private void DetectFlightLayer()
    {
        if (_animator == null) return;

        string[] statesToFind = new string[] { flyIdleState, flyMoveState, flyDiveState };
        for (int layer = 0; layer < _animator.layerCount; layer++)
        {
            foreach (var s in statesToFind)
            {
                if (string.IsNullOrEmpty(s)) continue;
                int hash = Animator.StringToHash(s);
                try
                {
                    if (_animator.HasState(layer, hash))
                    {
                        locomotionLayerIndex = layer;
                        if (debugLogs) Debug.Log($"[PlayerFlyingController] Detected flight state '{s}' on animator layer {layer}. Using that layer for flight animations.");
                        return;
                    }
                }
                catch { }
            }
        }
    }

    void OnEnable()
    {
        _controls?.Enable();
        if (_jumpAction != null)
        {
            _jumpAction.performed += OnJumpPerformed;
            _jumpAction.canceled += OnJumpCanceled;
        }
        if (_moveAction != null)
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
        }
        if (_cameraAction != null)
        {
            _cameraAction.performed += OnCameraPerformed;
            _cameraAction.canceled += OnCameraCanceled;
        }
    }

    void OnDisable()
    {
        if (_isFlying)
            ExitFlight(force: true);
        StopFlightVfx();
        _flightArmUntil = -1f;

        if (_jumpAction != null)
        {
            _jumpAction.performed -= OnJumpPerformed;
            _jumpAction.canceled -= OnJumpCanceled;
        }
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
        }
        if (_cameraAction != null)
        {
            _cameraAction.performed -= OnCameraPerformed;
            _cameraAction.canceled -= OnCameraCanceled;
        }

        _controls?.Disable();
    }

    void Update()
    {
        if (_isFlying)
        {
            CacheCameraTransform();
            UpdateFlightAnimation();
        }
    }

    void FixedUpdate()
    {
        if (_isFlying)
            ApplyFlightMovement();
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => _moveInput = Vector2.zero;
    private void OnCameraPerformed(InputAction.CallbackContext ctx) => _cameraInput = PlayerSettings.ApplyLookInversion(ctx.ReadValue<Vector2>(), true);
    private void OnCameraCanceled(InputAction.CallbackContext ctx) => _cameraInput = Vector2.zero;

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        _jumpHeld = true;

        if (_isFlying)
        {
            ExitFlight();
            return;
        }

        if (!CanEnterFlight())
            return;

        float now = Time.time;
        if (now <= _flightArmUntil)
        {
            EnterFlight();
            _flightArmUntil = -1f;
        }
        else
        {
            _flightArmUntil = now + doubleTapWindow;
        }
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx) => _jumpHeld = false;

    private bool CanEnterFlight()
    {
        if (_actionManager != null && !_actionManager.CanFly())
            return false;
        if (IsGrounded())
            return false;
        if (_actionManager != null && _actionManager.IsInMode(ActionMode.Swimming))
            return false;
        return true;
    }

    private void EnterFlight()
    {
        if (_isFlying)
            return;

        _isFlying = true;
        _flightArmUntil = -1f;
        _isBoosting = false;
        if (_actionManager != null)
            _actionManager.PushMode(ActionMode.Flying);
        if (_controller != null)
        {
            _controllerWasEnabled = _controller.enabled;
            _controller.enabled = false; // evitar que el controlador base fuerce animaciones de caída
        }
        if (_animator != null)
        {
            _animRootMotionPrev = _animator.applyRootMotion;
            _animator.applyRootMotion = false; // controlamos el movimiento vía Rigidbody
        }

        CacheCameraTransform();
        PlayFlightState(flyIdleState);
        SpawnTrailIfNeeded();

        // Ensure flight animator layer has full weight so flight states take precedence
        if (_animator != null && locomotionLayerIndex >= 0 && locomotionLayerIndex < _animator.layerCount)
        {
            try
            {
                _prevFlightLayerWeight = _animator.GetLayerWeight(locomotionLayerIndex);
                _animator.SetLayerWeight(locomotionLayerIndex, 1f);
            }
            catch { }
        }

        if (_rigidbody != null)
        {
            if (!_gravityStored)
            {
                _storedUseGravity = _rigidbody.useGravity;
                _gravityStored = true;
            }
            var vel = _rigidbody.linearVelocity;
            if (vel.y < 0f) vel.y = 0f;
            _rigidbody.linearVelocity = vel;
            _rigidbody.useGravity = false;
        }

        if (_controller != null)
        {
            SetControllerLocks(true);
            if (!_extraGravitySuspended)
            {
                _cachedExtraGravity = _controller.extraGravity;
                _controller.extraGravity = 0f;
                _extraGravitySuspended = true;
            }
        }

        if (debugLogs)
            Debug.Log("[PlayerFlyingController] Enter Flight");
    }

    private void ExitFlight(bool force = false)
    {
        if (!_isFlying && !force)
            return;

        bool wasFlying = _isFlying;
        _isFlying = false;
        _currentPlanarSpeed = 0f;
        _isBoosting = false;

        if (wasFlying && _actionManager != null)
            _actionManager.PopMode(ActionMode.Flying);

        if (wasFlying && _animator != null)
            _animator.CrossFade(locomotionStateName, 0.1f, locomotionLayerIndex);
        PlayLandingVfx();
        StopFlightVfx();

        if (_rigidbody != null && _gravityStored)
            _rigidbody.useGravity = _storedUseGravity;

        if (_controller != null)
        {
            SetControllerLocks(false);
            if (_extraGravitySuspended)
            {
                _controller.extraGravity = _cachedExtraGravity;
                _extraGravitySuspended = false;
            }
            _controller.enabled = _controllerWasEnabled;
        }

        if (wasFlying && debugLogs)
            Debug.Log("[PlayerFlyingController] Exit Flight");

        if (_animator != null)
            _animator.applyRootMotion = _animRootMotionPrev;

        // Restore previous flight layer weight if we changed it
        if (_animator != null && locomotionLayerIndex >= 0 && locomotionLayerIndex < _animator.layerCount)
        {
            try
            {
                if (_prevFlightLayerWeight >= 0f)
                    _animator.SetLayerWeight(locomotionLayerIndex, _prevFlightLayerWeight);
            }
            catch { }
            _prevFlightLayerWeight = -1f;
        }
    }

    private void ApplyFlightMovement()
    {
        if (_rigidbody == null)
            return;

        Vector3 forward = GetCameraForwardOnPlane();
        Vector3 right = GetCameraRightOnPlane();

        Vector3 planar = (forward * _moveInput.y) + (right * _moveInput.x);
        if (planar.sqrMagnitude > 1f) planar.Normalize();
        _isBoosting = planar.sqrMagnitude > 0.01f && Mathf.Abs(_moveInput.y) > 0.8f;
        float speedMul = _isBoosting ? boostMultiplier : 1f;
        Vector3 desired = planar * horizontalSpeed * speedMul;

        float verticalInput = 0f;
        // Solo el stick derecho controla altura, y solo mientras avanzas con el izquierdo.
        if (planar.sqrMagnitude > 0.01f && Mathf.Abs(_cameraInput.y) > 0.1f)
            verticalInput += _cameraInput.y * verticalSpeed;

        // Mantener salto para descender siempre (independiente del stick)
        if (_jumpHeld)
            verticalInput -= descendSpeed;

        desired += Vector3.up * verticalInput;

        Vector3 current = _rigidbody.linearVelocity;
        Vector3 target = Vector3.Lerp(current, desired, acceleration * Time.fixedDeltaTime);
        _rigidbody.linearVelocity = target;
        _currentPlanarSpeed = planar.magnitude * horizontalSpeed * speedMul;
        UpdateFlightVfx();

        Vector3 faceDir = planar;
        if (faceDir.sqrMagnitude < 0.1f && Mathf.Abs(verticalInput) > 0.5f)
            faceDir = forward;
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(faceDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        }
    }

    private void UpdateFlightAnimation()
    {
        if (_animator == null)
            return;

        float verticalVel = _rigidbody != null ? _rigidbody.linearVelocity.y : 0f;
        bool diving = verticalVel < -0.5f || _jumpHeld;
        bool moving = _currentPlanarSpeed > 0.5f;

        if (diving)
        {
            PlayFlightState(flyDiveState);
        }
        else
        {
            // Use move state only when it is explicitly different from idle/dive
            bool useMove = moving && !string.IsNullOrEmpty(flyMoveState)
                           && !string.Equals(flyMoveState, flyIdleState, StringComparison.Ordinal)
                           && !string.Equals(flyMoveState, flyDiveState, StringComparison.Ordinal);

            if (useMove)
                PlayFlightState(flyMoveState);
            else
                PlayFlightState(flyIdleState);
        }
    }

    private void PlayFlightState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || _animator == null)
            return;
        _animator.Play(stateName, locomotionLayerIndex);
    }

    private bool IsGrounded()
    {
        if (_capsule == null)
            return Physics.Raycast(transform.position, Vector3.down, 0.6f, LayerMask.GetMask("Default"));

        Vector3 top = transform.TransformPoint(_capsule.center + Vector3.up * (Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius)));
        Vector3 bottom = transform.TransformPoint(_capsule.center - Vector3.up * (Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius)));
        float radius = _capsule.radius * 0.95f;

        return Physics.CheckCapsule(top, bottom, radius + GroundCheckBuffer, _controller != null ? _controller.groundLayer : ~0);
    }

    private void CacheCameraTransform()
    {
        if (_cameraTransform != null)
            return;

#if UNITY_2022_3_OR_NEWER
        _cameraTransform = Camera.main ? Camera.main.transform : FindFirstObjectByType<Camera>()?.transform;
#else
#pragma warning disable 618
        _cameraTransform = Camera.main ? Camera.main.transform : FindObjectOfType<Camera>()?.transform;
#pragma warning restore 618
#endif
    }

    void SpawnTrailIfNeeded()
    {
        if (_trailInstance != null || trailPrefab == null) return;
        var parent = vfxAttach != null ? vfxAttach : transform;
        _trailInstance = Instantiate(trailPrefab, parent);
        _trailPs = _trailInstance.GetComponent<ParticleSystem>();
        _trailInstance.SetActive(false);
    }

    void UpdateFlightVfx()
    {
        if (_trailInstance != null)
        {
            bool shouldTrail = _isFlying && _currentPlanarSpeed >= trailSpeedThreshold;
            if (_trailInstance.activeSelf != shouldTrail)
                _trailInstance.SetActive(shouldTrail);
            if (shouldTrail && _trailPs != null && !_trailPs.isPlaying) _trailPs.Play();
        }

        if (_isFlying && _isBoosting && boostVfxPrefab != null)
        {
            if (_boostInstance == null)
            {
                var parent = vfxAttach != null ? vfxAttach : transform;
                _boostInstance = Instantiate(boostVfxPrefab, parent);
                _boostPs = _boostInstance.GetComponent<ParticleSystem>();
            }
            if (!_boostInstance.activeSelf) _boostInstance.SetActive(true);
            if (_boostPs != null && !_boostPs.isPlaying) _boostPs.Play();
        }
        else if (_boostInstance != null && _boostInstance.activeSelf)
        {
            if (_boostPs != null) _boostPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _boostInstance.SetActive(false);
        }
    }

    void PlayLandingVfx()
    {
        if (landingVfxPrefab == null) return;
        var parent = vfxAttach != null ? vfxAttach : transform;
        Instantiate(landingVfxPrefab, parent.position, parent.rotation);
    }

    void StopFlightVfx()
    {
        if (_trailInstance != null)
        {
            if (_trailPs != null) _trailPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _trailInstance.SetActive(false);
        }
        if (_boostInstance != null)
        {
            if (_boostPs != null) _boostPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _boostInstance.SetActive(false);
        }
    }

    private void CacheControllerLockFields()
    {
        if (_controller == null) return;

        var type = _controller.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        _lockMovementField = type.GetField("lockMovement", flags);
        _lockRotationField = type.GetField("lockRotation", flags);
    }

    private void SetControllerLocks(bool value)
    {
        if (_controller == null) return;
        if (_lockMovementField == null || _lockRotationField == null)
            CacheControllerLockFields();

        _lockMovementField?.SetValue(_controller, value);
        _lockRotationField?.SetValue(_controller, value);
    }

    private Vector3 GetCameraForwardOnPlane()
    {
        CacheCameraTransform();
        if (_cameraTransform == null)
            return Vector3.forward;

        Vector3 forward = _cameraTransform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private Vector3 GetCameraRightOnPlane()
    {
        CacheCameraTransform();
        if (_cameraTransform == null)
            return Vector3.right;

        Vector3 right = _cameraTransform.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
    }
}
