using UnityEngine;
using UnityEngine.InputSystem;
using Invector.vCharacterController;
using Core;
using Game.NPC;

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
    [SerializeField, Tooltip("Impulso hacia arriba al salir de la escalada (VelocityChange)")]
    private float launchUpImpulse = 6f;
    [SerializeField, Tooltip("Impulso hacia delante al salir de la escalada (VelocityChange)")]
    private float launchForwardImpulse = 3f;

    [Header("Animación")]
    [SerializeField] private string climbUpState = "ClimbUp_RM_NoWeapon";
    [SerializeField] private string climbDownState = "ClimbDown_RM_NoWeapon";
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
        _controls = Core.PlayerInputManager.GetSharedOrNew(out _ownsControls);

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

            // Permitir al jugador salir de la escalada con el botón Interact (A)
            try
            {
                if (_controls != null && _controls.GamePlay.Interact.triggered)
                {
                    LaunchOffClimb();
                }
            }
            catch { }
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
            if (!_rigidbody.isKinematic)
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

        PlayClimb(1f, true);

        // Notificar al sistema de party que el player ha iniciado una escalada.
        // Usamos la posición actual como "base" de la escalada para que los NPCs
        // puedan posicionarse en la base de la pared y esperar espacio.
        try
        {
            if (PlayerParty.HasInstance)
                PlayerParty.Instance.NotifyPlayerClimbStarted(transform.position);
        }
        catch { }

        if (debugLogs)
            Debug.Log("[PlayerClimbingController] Enter Climb");
    }

    private void HandleClimbMovement()
    {
        Vector2 move = _controls != null ? _controls.GamePlay.Move.ReadValue<Vector2>() : Vector2.zero;
        float vertical = Mathf.Clamp(move.y, -1f, 1f);
        // Usar SOLO el eje vertical para determinar si mostrar la animación de escalada
        // Evita que pequeñas desviaciones horizontales en el stick activen el loop.
        bool hasMovementInput = Mathf.Abs(vertical) > MinInputToAnimate;

        // Reproducir animación SOLO mientras haya input vertical
        PlayClimb(vertical, hasMovementInput);

        Vector3 motion = Vector3.up * vertical * climbSpeed * Time.deltaTime;
        transform.position += motion;

        if (_rigidbody != null && !_rigidbody.isKinematic)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (forceGroundedWhileClimbing && _animator != null)
        {
            try
            {
                _animator.SetBool(Invector.vCharacterController.vAnimatorParameters.IsGrounded, true);
                _animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, hasMovementInput ? 1f : 0f);
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

    private void PlayClimb(float vertical, bool hasMovementInput)
    {
        if (_animator == null)
            return;

        if (!hasMovementInput)
        {
            // Congelar el animator en el frame actual, sin CrossFade
            _animator.speed = 0f;
            return;
        }

        if (climbAnimatorLayer < 0 || climbAnimatorLayer >= _animator.layerCount)
        {
            if (debugLogs && _lastMissingStateWarn != "_layer")
            {
                Debug.LogWarning("[PlayerClimbingController] Layer de escalada inválido (" + climbAnimatorLayer + ")");
                _lastMissingStateWarn = "_layer";
            }
            return;
        }

        // Restaurar velocidad antes del CrossFade para que la transición avance desde el inicio
        _animator.speed = 1f;

        string targetState = vertical > 0f ? climbUpState : climbDownState;
        if (string.IsNullOrEmpty(targetState))
            return;

        int hash = Animator.StringToHash(targetState);
        if (!_animator.HasState(climbAnimatorLayer, hash))
        {
            if (debugLogs && _lastMissingStateWarn != targetState)
            {
                Debug.LogWarning("[PlayerClimbingController] Estado de animación no encontrado: " + targetState + " en capa " + climbAnimatorLayer);
                _lastMissingStateWarn = targetState;
            }
            return;
        }

        // Evitar reiniciar CrossFade si ya estamos en ese estado o ya hay una transición hacia él
        var current = _animator.GetCurrentAnimatorStateInfo(climbAnimatorLayer);
        var next = _animator.GetNextAnimatorStateInfo(climbAnimatorLayer);
        if (current.shortNameHash == hash || next.shortNameHash == hash)
            return;

        _animator.CrossFade(hash, 0.05f, climbAnimatorLayer);
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

        // Notificar al party que la escalada terminó (descenso o soltado).
        try
        {
            if (PlayerParty.HasInstance)
                PlayerParty.Instance.NotifyPlayerClimbStopped(transform.position);
        }
        catch { }

        if (debugLogs)
            Debug.Log("[PlayerClimbingController] Exit Climb");
    }

    /// <summary>
    /// Salir de la escalada impulsando al jugador hacia arriba y hacia delante.
    /// Llamado al pulsar Interact (A) mientras se escala.
    /// </summary>
    private void LaunchOffClimb()
    {
        // Valores ajustables (tweak si es necesario)
        // Valores tomados desde campos serializables
        float upImpulse = launchUpImpulse;
        float forwardImpulse = launchForwardImpulse;

        // Evitar que el mismo botón A se interprete como salto/activador de vuelo
        // justo después de abandonar la pared. Esto delega en PlayerActionManager
        // para activar el cooldown que también llama a GamepadInputReader.IgnoreJumpButton.
        try
        {
            if (_actionManager != null)
                _actionManager.SetInteractCooldown();
        }
        catch { }

        // Además pedir al controlador de vuelo que cancele cualquier arming/pending
        // para evitar que un doble-press previo active vuelo por estar armado.
        try
        {
            var fly = GetComponent<PlayerFlyingController>();
            if (fly != null) fly.CancelFlightArming();
        }
        catch { }

        // Salir del modo escalada primero para restaurar control/rigidbody
        ExitClimb(force: false);

        if (_rigidbody != null)
        {
            // Normalizar componente vertical para evitar acumulación de velocidad
            // que pueda convertir el impulso en vuelo no deseado.
            try
            {
                // Usar linearVelocity para evitar API obsoleta en esta versión de Unity
                Vector3 v = _rigidbody.linearVelocity;
                v.y = 0f; // eliminar velocidad vertical previa
                _rigidbody.linearVelocity = v;
            }
            catch { }

            // Aplicar impulso directo (VelocityChange para evitar depender de masa)
            Vector3 impulse = transform.up * upImpulse + transform.forward * forwardImpulse;
            try { _rigidbody.AddForce(impulse, ForceMode.VelocityChange); } catch { }
        }
    }
}
