using UnityEngine;
using UnityEngine.InputSystem;

namespace Invector.vCharacterController
{
    public class vThirdPersonInput : MonoBehaviour
    {
        [Header("New Input System")]
        [SerializeField] private InputActionAsset inputActions;

        // Acciones
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction strafeAction;
        private InputAction cameraAction;
        private InputAction attackMagicWestAction;   // X  -> izquierda
        private InputAction attackMagicEastAction;   // B  -> derecha
        private InputAction attackMagicNorthAction;  // Y  -> especial

        [SerializeField] private global::PlayerInputManager inputManager;
        [SerializeField, Tooltip("Optional reference that implements IActionValidator (e.g. PlayerActionManager)")]
        private MonoBehaviour actionValidatorSource;

        private IActionValidator actionValidator;

        [HideInInspector] public vThirdPersonController cc;
        [HideInInspector] public vThirdPersonCamera tpCamera;
        [HideInInspector] public Camera cameraMain;

        // Valores
        private Vector2 moveInput;
        private Vector2 cameraInput;
        private bool jumpPressed;
        private bool sprintHeld;
        private bool strafePressed;

        protected virtual void Awake()
        {
            ResolveServices();
            InitializeInputActions();
        }

        private void ResolveServices()
        {
            if (inputManager == null)
                global::ServiceLocator.TryGet(out inputManager);

            if (actionValidator == null)
            {
                if (actionValidatorSource != null)
                    actionValidator = actionValidatorSource as IActionValidator;

                if (actionValidator == null)
                    actionValidator = GetComponent<IActionValidator>();

                if (actionValidator == null && inputManager != null)
                    actionValidator = inputManager.GetComponent<IActionValidator>();

                if (actionValidator == null)
                    global::ServiceLocator.TryGet(out actionValidator);
            }
        }

        private void InitializeInputActions()
        {
            if (inputManager != null)
            {
                // Compartir el asset centralizado para evitar duplicados y búsquedas.
                inputActions = inputManager.PlayerInput != null ? inputManager.PlayerInput.actions : inputManager.Controls?.asset;
            }

            if (inputActions == null)
            {
                // Permitir asignación manual desde el inspector como respaldo.
                inputActions = this.inputActions;
            }

            if (inputActions == null)
            {
                Debug.LogError("[vThirdPersonInput] No InputActionAsset assigned. Please provide one or ensure PlayerInputManager is present.");
                return;
            }

            var gameplay = inputActions.FindActionMap("GamePlay");
            if (gameplay != null)
            {
                moveAction             = gameplay.FindAction("Move");
                jumpAction             = gameplay.FindAction("Jump");
                sprintAction           = gameplay.FindAction("Sprint");
                strafeAction           = gameplay.FindAction("Strafe");
                cameraAction           = gameplay.FindAction("CameraLook");
                attackMagicWestAction  = gameplay.FindAction("AttackMagicWest");
                attackMagicEastAction  = gameplay.FindAction("AttackMagicEast");
                attackMagicNorthAction = gameplay.FindAction("AttackMagicNorth");
            }
            else Debug.LogWarning("[vThirdPersonInput] GamePlay action map not found in InputActionAsset");
        }

        protected virtual void OnEnable()
        {
            if (inputActions == null) return;
            inputActions.Enable();

            if (moveAction != null)   { moveAction.performed += OnMoveInput;   moveAction.canceled += OnMoveInput; }
            if (jumpAction != null)   { jumpAction.performed += OnJumpInput; }
            if (sprintAction != null) { sprintAction.performed += OnSprintInput; sprintAction.canceled += OnSprintInput; }
            if (strafeAction != null) { strafeAction.performed += OnStrafeInput; }
            if (cameraAction != null) { cameraAction.performed += OnCameraInput; cameraAction.canceled += OnCameraInput; }

            // Magia (solo started para evitar dobles disparos)
            if (attackMagicWestAction  != null) attackMagicWestAction.started  += OnAttackMagicWestStarted;
            if (attackMagicEastAction  != null) attackMagicEastAction.started  += OnAttackMagicEastStarted;
            if (attackMagicNorthAction != null) attackMagicNorthAction.started += OnAttackMagicNorthStarted;
        }

        protected virtual void OnDisable()
        {
            if (inputActions == null) return;

            if (moveAction != null)   { moveAction.performed -= OnMoveInput;   moveAction.canceled -= OnMoveInput; }
            if (jumpAction != null)   { jumpAction.performed -= OnJumpInput; }
            if (sprintAction != null) { sprintAction.performed -= OnSprintInput; sprintAction.canceled -= OnSprintInput; }
            if (strafeAction != null) { strafeAction.performed -= OnStrafeInput; }
            if (cameraAction != null) { cameraAction.performed -= OnCameraInput; cameraAction.canceled  -= OnCameraInput; }

            if (attackMagicWestAction  != null) attackMagicWestAction.started  -= OnAttackMagicWestStarted;
            if (attackMagicEastAction  != null) attackMagicEastAction.started  -= OnAttackMagicEastStarted;
            if (attackMagicNorthAction != null) attackMagicNorthAction.started -= OnAttackMagicNorthStarted;

            inputActions.Disable();
        }

        protected virtual void Start()
        {
            InitilizeController();
            InitializeTpCamera();
        }

        protected virtual void FixedUpdate()
        {
            cc.UpdateMotor();
            cc.ControlLocomotionType();
            cc.ControlRotationType();
        }

        protected virtual void Update()
        {
            InputHandle();
            cc.UpdateAnimator();
        }

        public virtual void OnAnimatorMove()
        {
            cc.ControlAnimatorRootMotion();
        }

        // ===== Helpers / movimiento =====
        private void OnMoveInput(InputAction.CallbackContext context)   => moveInput = context.ReadValue<Vector2>();
        private void OnJumpInput(InputAction.CallbackContext context)
        {
            if (context.performed && CanJump())
                jumpPressed = true;
        }

        private void OnSprintInput(InputAction.CallbackContext context)
        {
            if (!CanSprint())
            {
                sprintHeld = false;
                return;
            }
            sprintHeld = context.ReadValueAsButton();
        }
        private void OnStrafeInput(InputAction.CallbackContext context) { if (context.performed) strafePressed = true; }
        private void OnCameraInput(InputAction.CallbackContext context) => cameraInput = ApplyLookInversionSafe(context.ReadValue<Vector2>());

        // Some projects may provide a `PlayerSettings` helper class. The Invector plugin
        // ships under Plugins and may compile in a different assembly where `PlayerSettings`
        // (defined in the main Assembly-CSharp) is not available. To avoid a hard dependency
        // we call it via reflection if present; otherwise we return the original input.
        private static Vector2 ApplyLookInversionSafe(Vector2 input)
        {
            try
            {
                var t = System.Type.GetType("PlayerSettings");
                if (t != null)
                {
                    var mi = t.GetMethod("ApplyLookInversion", new System.Type[] { typeof(Vector2) });
                    if (mi != null)
                    {
                        var res = mi.Invoke(null, new object[] { input });
                        if (res is Vector2 v) return v;
                    }
                    // overload with flightContext bool?
                    mi = t.GetMethod("ApplyLookInversion", new System.Type[] { typeof(Vector2), typeof(bool) });
                    if (mi != null)
                    {
                        var res2 = mi.Invoke(null, new object[] { input, false });
                        if (res2 is Vector2 v2) return v2;
                    }
                }
            }
            catch (System.Exception)
            {
                // swallow reflection errors and fall back to raw input
            }
            return input;
        }

        protected virtual void InitilizeController()
        {
            cc = GetComponent<vThirdPersonController>();
            if (cc != null) cc.Init();
        }

        protected virtual void InitializeTpCamera()
        {
            if (tpCamera == null)
            {
                tpCamera = FindFirstObjectByType<vThirdPersonCamera>();
                if (tpCamera == null) return;

                tpCamera.SetMainTarget(this.transform);
                tpCamera.Init();
            }
        }

        protected virtual void InputHandle()
        {
            MoveInput();
            CameraInput();
            SprintInput();
            StrafeInput();
            JumpInput();
        }

        public virtual void MoveInput()
        {
            cc.input.x = moveInput.x;
            cc.input.z = moveInput.y;
        }

        protected virtual void CameraInput()
        {
            if (!cameraMain)
            {
                if (!Camera.main) Debug.Log("Missing a Camera with the tag MainCamera, please add one.");
                else
                {
                    cameraMain = Camera.main;
                    cc.rotateTarget = cameraMain.transform;
                }
            }

            if (cameraMain) cc.UpdateMoveDirection(cameraMain.transform);
            if (tpCamera == null) return;

            tpCamera.RotateCamera(cameraInput.x, cameraInput.y);
        }

        protected virtual void StrafeInput()
        {
            if (strafePressed)
            {
                cc.Strafe();
                strafePressed = false;
            }
        }

        protected virtual void SprintInput() => cc.Sprint(sprintHeld);

        protected virtual bool JumpConditions()
        {
            return cc.isGrounded && cc.GroundAngle() < cc.slopeLimit && !cc.isJumping && !cc.stopMove;
        }

        protected virtual void JumpInput()
        {
            if (jumpPressed && JumpConditions())
            {
                cc.Jump();
                jumpPressed = false;
            }
        }

        private IActionValidator GetValidator()
        {
            if (actionValidator != null)
                return actionValidator;

            if (inputManager != null)
                actionValidator = inputManager.GetComponent<IActionValidator>();

            return actionValidator;
        }

        private bool CanJump()   => GetValidator()?.CanJump() ?? true;
        private bool CanSprint() => GetValidator()?.CanSprint() ?? true;
        private bool CanMagic()  => GetValidator()?.CanCastMagic() ?? true;

        // Named callbacks for magic inputs so they can be unsubscribed reliably
        private void OnAttackMagicWestStarted(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (!CanMagic()) return;
            if (cc != null) cc.CastMagicLeft();
        }

        private void OnAttackMagicEastStarted(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (!CanMagic()) return;
            if (cc != null) cc.CastMagicRight();
        }

        private void OnAttackMagicNorthStarted(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (!CanMagic()) return;
            if (cc != null) cc.CastMagicSpecial();
        }
    }
}
