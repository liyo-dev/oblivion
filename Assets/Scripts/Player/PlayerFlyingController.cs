using Core;
using Invector.vCharacterController;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla el modo de vuelo del jugador (tipo Dragon Ball): doble salto para entrar,
/// joystick izquierdo para direccionarse y mantener pulsado salto para descender/salir.
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerFlyingController : MonoBehaviour
{
    private const float GroundCheckBuffer = 0.2f;

    // FIX (1 sep 2026) — "la animación de vuelo se quita y se pone": IsGrounded() usa un
    // Physics.CheckCapsule con un buffer pequeño (GroundCheckBuffer) contra el propio collider
    // del jugador. Volar a baja altura o rozar el terreno es parte normal del mecanismo (picados,
    // vuelo rasante) — bastaba un único frame de solape para que Update() llamara a ExitFlight()
    // de inmediato: eso desactiva el controller, restaura la gravedad y hace
    // PlayerActionManager.PopMode(ActionMode.Flying), así que el jugador empieza a caer aunque
    // siga con la intención de volar, y vuelve a entrar en vuelo al reintentar — visible como que
    // la animación se activa y desactiva en bucle. Igual que el resto del proyecto exige contacto
    // sostenido antes de aceptar una transición de movimiento (ver
    // FollowPlayerState._isFollowingSticky), exigimos que IsGrounded() se mantenga true durante
    // este margen antes de aceptarlo como aterrizaje real.
    private const float ExitFlightGroundedGrace = 0.12f;

    [Header("Animación")]
    [SerializeField] private int locomotionLayerIndex = 0;
    [SerializeField] private string flyIdleState = "fly_idle";
    [SerializeField] private string flyMoveState = "fly_move";
    [SerializeField] private string flyDiveState = "fly_dive";
    [SerializeField] private string flyLandingState = "Landing";
    [SerializeField] private float landingCrossfade = 0.08f;
    [SerializeField] private float hardLandingSpeed = -6f;
    [SerializeField] private string locomotionStateName = "Free Locomotion";

    [Header("Movimiento")]
    [SerializeField] private float horizontalSpeed = 12f;
    [SerializeField] private float verticalSpeed = 8f;
    [SerializeField] private float descendSpeed = 12f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float turnSpeed = 9f;
    [SerializeField] private float tiltAngleMultiplier = 15f;
    [SerializeField] private float tiltSpeed = 5f;
    [SerializeField] private float doubleTapWindow = 0.35f;
    [SerializeField] private float boostMultiplier = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Animator _animator;
    private Rigidbody _rigidbody;
    private vThirdPersonController _controller;
    private Invector.vCharacterController.vThirdPersonInput _inputController;
    private FieldInfo _lockMovementField;
    private FieldInfo _lockRotationField;
    private FieldInfo _isJumpingField;
    private FieldInfo _jumpCounterField;
    private PlayerActionManager _actionManager;
    private Core.PlayerInputManager _inputManager;
    private CapsuleCollider _capsule;
    private Transform _cameraTransform;
    private PlayerControls _controls;
    private InputAction _moveAction;
    private InputAction _cameraAction;
    private InputAction _jumpAction;
    private bool _ownsControls;

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
    private int _isFlyingHash = -1;
    private bool _pendingEnterFlight = false;
    private bool _flightArmed = false;
    private float _flightArmedExpires = -1f;
    private bool _justEnteredFlight;
    private float _currentPitch = 0f;
    private float _groundedWhileFlyingTimer;
    private bool _isPhysicsBobbingIdle;
    [SerializeField] private float flightArmedDuration = 2f;

    [Header("FX Vuelo")]
    [SerializeField] private Transform vfxAttach;
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private float trailSpeedThreshold = 1.5f;
    [SerializeField] private GameObject boostVfxPrefab;
    [SerializeField] private GameObject landingVfxPrefab;
     [Header("Vuelo - Movimiento visual")]
     [Tooltip("Amplitud (metros) del movimiento vertical oscilante mientras el jugador está en vuelo.")]
     [SerializeField] private float flightBobAmplitude = 0.8f;
     [Tooltip("Frecuencia (Hz) del movimiento oscilante mientras el jugador está en vuelo.")]
     [SerializeField] private float flightBobFrequency = 1.8f;
     [Tooltip("Transform raíz que contiene la malla/visual del personaje. Si se asigna, el bobbing se aplicará a la visual.")]
     [SerializeField] private Transform visualRoot;
     private float _visualRootBaseY;
     private bool _visualRootHasBase = false;
     [Header("Vuelo - Partículas de pies")]
    [Tooltip("Prefab (ParticleSystem) que se instanciará en los pies mientras el jugador vuela.")]
    [SerializeField] private GameObject footVfxPrefab;
    [Tooltip("Transform donde colocar las partículas de pies. Si es null, se usan las coordenadas del jugador.")]
    [SerializeField] private Transform feetAttach;

    private GameObject _trailInstance;
    private ParticleSystem _trailPs;
    private GameObject _boostInstance;
    private ParticleSystem _boostPs;
    // Instancia y sistema de partículas de los pies
    private GameObject _footVfxInstance;
    private ParticleSystem _footVfxPs;
    // Estado interno para evitar llamadas repetidas
    private bool _footVfxActive = false;

    public bool IsFlying => _isFlying;
    public bool IsBoosting => _isBoosting;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        _controller = GetComponent<vThirdPersonController>();
        _inputController = GetComponent<Invector.vCharacterController.vThirdPersonInput>();
        _actionManager = GetComponent<PlayerActionManager>();
        _inputManager = ServiceLocator.Get<Core.PlayerInputManager>(logIfMissing: false);
        _capsule = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
        CacheControllerLockFields();
        CacheCameraTransform();
        _flightArmUntil = -1f;

        if (_inputManager != null && _inputManager.Controls != null)
        {
            _controls = _inputManager.Controls;
            _ownsControls = false;
        }
        else
        {
            _controls = new PlayerControls();
            _ownsControls = true;
        }

        _moveAction = _controls.GamePlay.Move;
        _cameraAction = _controls.GamePlay.CameraLook;
        _jumpAction = _controls.GamePlay.Jump;

        // Try to auto-detect the animator layer that contains the flight states
        DetectFlightLayer();

        // cache isFlying parameter hash if present
        if (_animator != null)
        {
            try { _isFlyingHash = Animator.StringToHash("isFlying"); }
            catch { _isFlyingHash = -1; }
        }
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
        if (_ownsControls)
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

        if (_ownsControls)
            _controls?.Disable();
    }

    void Update()
    {
        if (_isFlying)
        {
            if (IsGrounded())
            {
                _groundedWhileFlyingTimer += Time.deltaTime;
                if (_groundedWhileFlyingTimer >= ExitFlightGroundedGrace)
                {
                    ExitFlight();
                    return;
                }
            }
            else
            {
                _groundedWhileFlyingTimer = 0f;
            }
            CacheCameraTransform();
            UpdateFlightAnimation();
        }

        // If user pressed jump twice but second press happened while still considered grounded,
        // wait until the player is airborne then auto-enter flight.
        if (!_isFlying && _pendingEnterFlight)
        {
            if (debugLogs) Debug.Log($"[PlayerFlyingController] Pending check now={Time.time:F2} flightArmUntil={_flightArmUntil:F2} IsGrounded={IsGrounded()} CanEnter={CanEnterFlight()}");
            // clear pending if window expired
            if (_flightArmUntil > 0f && Time.time > _flightArmUntil)
            {
                _pendingEnterFlight = false;
            }
            else if (CanEnterFlight() && !IsGrounded())
            {
                EnterFlight();
                _pendingEnterFlight = false;
                _flightArmUntil = -1f;
            }
        }
    }

     void FixedUpdate()
     {
         if (_isFlying)
             ApplyFlightMovement();
     }

     void LateUpdate()
     {
         // Aplicar bobbing visual si visualRoot existe y estamos volando
         if (visualRoot == null || !_isFlying)
             return;

         // Capturar la posición base la primera vez
         if (!_visualRootHasBase)
         {
             _visualRootBaseY = visualRoot.localPosition.y;
             _visualRootHasBase = true;
         }

         if (Mathf.Abs(flightBobAmplitude) > 0.0001f && Mathf.Abs(flightBobFrequency) > 0.0001f)
         {
             float bob = Mathf.Sin(Time.time * (2f * Mathf.PI * flightBobFrequency)) * flightBobAmplitude;
             Vector3 lp = visualRoot.localPosition;
             lp.y = _visualRootBaseY + bob;
             visualRoot.localPosition = lp;
         }
         else if (_visualRootHasBase)
         {
             // Si no hay movimiento, restablecer a la posición base
             Vector3 lp = visualRoot.localPosition;
             if (Mathf.Abs(lp.y - _visualRootBaseY) > 0.0001f)
             {
                 lp.y = _visualRootBaseY;
                 visualRoot.localPosition = lp;
             }
         }
     }

     private void OnMovePerformed(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => _moveInput = Vector2.zero;
    private void OnCameraPerformed(InputAction.CallbackContext ctx) => _cameraInput = PlayerSettings.ApplyLookInversion(ctx.ReadValue<Vector2>(), true);
    private void OnCameraCanceled(InputAction.CallbackContext ctx) => _cameraInput = Vector2.zero;

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        if (_inputManager != null && !_inputManager.CanProcess(PlayerAbility.Jump))
        {
            if (debugLogs) Debug.Log("[PlayerFlyingController] Salto ignorado: acción bloqueada por PlayerActionManager.");
            return;
        }

        _jumpHeld = true;

        if (_isFlying)
        {
            ExitFlight();
            return;
        }

        // If player presses Jump while already airborne, enter flight immediately.
        // This covers the common case: jump from ground, then press jump again in air.
        if (!IsGrounded())
        {
            if (CanEnterFlight())
            {
                if (debugLogs) Debug.Log("[PlayerFlyingController] Jump performed while airborne -> EnterFlight immediate.");
                EnterFlight();
                return;
            }
        }

        // Arm or attempt to enter flight. We allow the first press (from ground) to arm
        // and the second press while airborne to actually enter flight.
        float now = Time.time;
        if (debugLogs) Debug.Log($"[PlayerFlyingController] OnJumpPerformed now={now:F2} flightArmUntil={_flightArmUntil:F2} isGrounded={IsGrounded()} pending={_pendingEnterFlight} armed={_flightArmed}");

        bool inDoubleTapWindow = now <= _flightArmUntil;
        bool armedAndAirborne = _flightArmed && !IsGrounded() && now <= _flightArmedExpires;

        if (inDoubleTapWindow || armedAndAirborne)
        {
            // Second press within window or armed-and-now-airborne -> try to enter flight now.
            if (CanEnterFlight())
            {
                EnterFlight();
                _flightArmUntil = -1f;
                _pendingEnterFlight = false;
                _flightArmed = false;
            }
            else
            {
                // Not ready yet (likely still considered grounded). Mark pending so that
                // when the player becomes airborne we enter flight automatically.
                _pendingEnterFlight = true;
                _flightArmUntil = now + doubleTapWindow; // extend window while pending
                if (debugLogs) Debug.Log($"[PlayerFlyingController] Second jump but can't enter yet. Marked pending. new flightArmUntil={_flightArmUntil:F2}");
            }
        }
        else
        {
            // First press (or outside window): arm the flight trigger.
            _flightArmUntil = now + doubleTapWindow;
            _pendingEnterFlight = false;
            _flightArmed = true;
            _flightArmedExpires = now + flightArmedDuration;
            if (debugLogs) Debug.Log($"[PlayerFlyingController] First jump pressed - armed flight (expires {_flightArmedExpires:F2}) and window until {_flightArmUntil:F2}");
        }
    }

    private void OnJumpCanceled(InputAction.CallbackContext ctx) => _jumpHeld = false;

    /// <summary>
    /// Cancela cualquier estado de "doble salto armado" sin salir del vuelo activo.
    /// Llamar al iniciar interacciones que bloquean el vuelo (sentarse, diálogos, etc.)
    /// </summary>
    public void CancelFlightArming()
    {
        _pendingEnterFlight = false;
        _flightArmed = false;
        _flightArmUntil = -1f;
        _jumpHeld = false;
    }

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
         _groundedWhileFlyingTimer = 0f;
         _visualRootHasBase = false; // Resetear bobbing visual para que capture la posición base
         _flightArmUntil = -1f;
        _isBoosting = false;
        _justEnteredFlight = true;
        _jumpHeld = false; // evitar que el primer frame se considere dive por mantener salto
        if (_inputController != null)
            _inputController.CancelPendingJump();
        GamepadInputReader.IgnoreJumpButton(0.3f);
        if (_controller != null)
            _controller.suppressAirMovement = true;
        if (_actionManager != null)
            _actionManager.PushMode(ActionMode.Flying);
        if (_inputController != null)
            _inputController.DisableVerticalCameraRotation = true;
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
        SpawnFootVfxIfNeeded();

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

        // set animator flag
        if (_animator != null && _isFlyingHash != -1)
        {
            try { _animator.SetBool(_isFlyingHash, true); }
            catch { }
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
        _currentPitch = 0f;
        _isBoosting = false;
        _flightArmed = false;
        _pendingEnterFlight = false;
        _flightArmUntil = -1f;
        if (_inputController != null)
            _inputController.CancelPendingJump();
        GamepadInputReader.IgnoreJumpButton(0.2f);

        if (wasFlying && _actionManager != null)
            _actionManager.PopMode(ActionMode.Flying);
        if (_inputController != null)
            _inputController.DisableVerticalCameraRotation = false;

        float verticalVel = _rigidbody != null ? _rigidbody.linearVelocity.y : 0f;
        bool hardLanding = verticalVel <= hardLandingSpeed || IsGrounded();
        if (wasFlying && _animator != null)
        {
            if (hardLanding && HasAnimatorState(flyLandingState))
                _animator.CrossFade(flyLandingState, landingCrossfade, locomotionLayerIndex);
            else
                _animator.CrossFade(locomotionStateName, 0.1f, locomotionLayerIndex);
        }
        PlayLandingVfx();
        StopFlightVfx();
        StopFootVfx();

        // Restaurar posición local del visual root si se desplazó por bobbing
        if (visualRoot != null && _visualRootHasBase)
        {
            Vector3 lp = visualRoot.localPosition;
            lp.y = _visualRootBaseY;
            visualRoot.localPosition = lp;
        }
        _visualRootHasBase = false;

        if (_rigidbody != null)
        {
            var vel = _rigidbody.linearVelocity;
            // Presión mínima hacia abajo para clavar el personaje al suelo sin rebotar;
            // si ya cae más rápido (aterrizaje duro) se respeta esa velocidad.
            vel.y = Mathf.Min(vel.y, -0.5f);
            _rigidbody.linearVelocity = vel;
            _rigidbody.angularVelocity = Vector3.zero;
            if (_gravityStored)
                _rigidbody.useGravity = _storedUseGravity;
        }

        if (_controller != null)
        {
            SetControllerLocks(false);
            if (_extraGravitySuspended)
            {
                _controller.extraGravity = _cachedExtraGravity;
                _extraGravitySuspended = false;
            }
            _controller.suppressAirMovement = false;
            _isJumpingField?.SetValue(_controller, false);
            _jumpCounterField?.SetValue(_controller, 0f);
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

        // clear animator flag
        if (_animator != null && _isFlyingHash != -1)
        {
            try { _animator.SetBool(_isFlyingHash, false); }
            catch { }
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

        bool isIdleHover = planar.sqrMagnitude < 0.01f && !_jumpHeld && Mathf.Abs(verticalInput) < 0.1f;

         // Añadir control vertical del jugador
         desired += Vector3.up * verticalInput;

         Vector3 current = _rigidbody.linearVelocity;
        Vector3 target = Vector3.Lerp(current, desired, acceleration * Time.fixedDeltaTime);

        // Bobbing físico: solo cuando está quieto y no hay visualRoot (evita doble efecto)
        _isPhysicsBobbingIdle = isIdleHover && visualRoot == null
            && Mathf.Abs(flightBobAmplitude) > 0.0001f && flightBobFrequency > 0.0001f;
        if (_isPhysicsBobbingIdle)
        {
            float omega = 2f * Mathf.PI * flightBobFrequency;
            target.y = Mathf.Cos(Time.time * omega) * flightBobAmplitude * omega;
        }

        _rigidbody.linearVelocity = target;
        _currentPlanarSpeed = planar.magnitude * horizontalSpeed * speedMul;
        UpdateFlightVfx();
        UpdateFootVfxState();

        Vector3 faceDir = planar;
        if (faceDir.sqrMagnitude < 0.1f && Mathf.Abs(verticalInput) > 0.5f)
            faceDir = forward;
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            // Lógica de inclinación (pitch)
            float maxVertSpeed = Mathf.Max(verticalSpeed, descendSpeed);
            float normalizedVertical = maxVertSpeed > 0.01f ? verticalInput / maxVertSpeed : 0f;
            float targetPitch = -normalizedVertical * tiltAngleMultiplier; // Negativo para que al subir (positivo) incline hacia arriba
            _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, tiltSpeed * Time.fixedDeltaTime);

            Quaternion targetRot = Quaternion.LookRotation(faceDir, Vector3.up);
            // Aplicar la inclinación en el eje X local del personaje
            Quaternion pitchRotation = Quaternion.AngleAxis(_currentPitch, Vector3.right);
            Quaternion finalRot = targetRot * pitchRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, turnSpeed * Time.fixedDeltaTime);
        }
    }

    private void UpdateFlightAnimation()
    {
        if (_animator == null)
            return;

        float verticalVel = _rigidbody != null ? _rigidbody.linearVelocity.y : 0f;
        // Excluir el bobbing físico del chequeo de dive para no disparar la anim de picado sin input
        bool diving = (!_isPhysicsBobbingIdle && verticalVel < -0.5f) || _jumpHeld;
        // Consider joystick (left stick) movement as input to switch to dive state.
        bool stickMoved = _moveInput.sqrMagnitude > 0.01f; // small deadzone

        if (_justEnteredFlight)
        {
            _justEnteredFlight = false;
            PlayFlightState(flyIdleState);
            return;
        }

        if (stickMoved)
        {
            PlayFlightState(flyDiveState);
            return;
        }

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

        // Allow vertical camera rotation only in fly idle
        bool isIdle = string.Equals(stateName, flyIdleState, StringComparison.Ordinal);
        if (_inputController != null)
            _inputController.DisableVerticalCameraRotation = !isIdle;
    }

    private bool HasAnimatorState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || _animator == null)
            return false;
        try
        {
            int hash = Animator.StringToHash(stateName);
            return _animator.HasState(locomotionLayerIndex, hash);
        }
        catch { return false; }
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

    _cameraTransform = Camera.main ? Camera.main.transform : ServiceLocator.Get<Camera>(false)?.transform;
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

    // --- Gestión de VFX de pies ---
    void SpawnFootVfxIfNeeded()
    {
        if (_footVfxInstance != null || footVfxPrefab == null) return;
        var parent = feetAttach != null ? feetAttach : vfxAttach != null ? vfxAttach : transform;
        _footVfxInstance = Instantiate(footVfxPrefab, parent);
        _footVfxPs = _footVfxInstance.GetComponent<ParticleSystem>();
        // Inicialmente desactivadas hasta que el vuelo esté activo
        _footVfxInstance.SetActive(false);
    }

    void UpdateFootVfxState()
    {
        if (_footVfxInstance == null) return;

        bool shouldBeActive = _isFlying;

        if (shouldBeActive && !_footVfxActive)
        {
            _footVfxInstance.SetActive(true);
            if (_footVfxPs != null && !_footVfxPs.isPlaying) _footVfxPs.Play();
            _footVfxActive = true;
        }
        else if (!shouldBeActive && _footVfxActive)
        {
            if (_footVfxPs != null) _footVfxPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _footVfxInstance.SetActive(false);
            _footVfxActive = false;
        }
    }

    void StopFootVfx()
    {
        if (_footVfxInstance == null) return;
        if (_footVfxPs != null) _footVfxPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _footVfxInstance.SetActive(false);
        _footVfxActive = false;
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
        _isJumpingField = type.GetField("isJumping", flags);
        _jumpCounterField = type.GetField("jumpCounter", flags);
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