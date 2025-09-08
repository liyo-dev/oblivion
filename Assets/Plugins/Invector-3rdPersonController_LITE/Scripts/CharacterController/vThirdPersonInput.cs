using UnityEngine;
using UnityEngine.InputSystem;

namespace Invector.vCharacterController
{
    public class vThirdPersonInput : MonoBehaviour
    {
        #region Variables       

        [Header("Controller Input")]
        [System.Obsolete("Use new Input System instead")]
        public string horizontalInput = "Horizontal";
        [System.Obsolete("Use new Input System instead")]
        public string verticallInput = "Vertical";
        [System.Obsolete("Use new Input System instead")]
        public KeyCode jumpInput = KeyCode.Space;
        [System.Obsolete("Use new Input System instead")]
        public KeyCode strafeInput = KeyCode.Tab;
        [System.Obsolete("Use new Input System instead")]
        public KeyCode sprintInput = KeyCode.LeftShift;

        [Header("Camera Input")]
        [System.Obsolete("Use new Input System instead")]
        public string rotateCameraXInput = "Mouse X";
        [System.Obsolete("Use new Input System instead")]
        public string rotateCameraYInput = "Mouse Y";

        [Header("New Input System")]
        [SerializeField] private InputActionAsset inputActions;
        
        // Input Actions (alternative approach)
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction strafeAction;
        private InputAction cameraAction;

        [HideInInspector] public vThirdPersonController cc;
        [HideInInspector] public vThirdPersonCamera tpCamera;
        [HideInInspector] public Camera cameraMain;

        // Input values
        private Vector2 moveInput;
        private Vector2 cameraInput;
        private bool jumpPressed;
        private bool sprintHeld;
        private bool strafePressed;

        #endregion

        protected virtual void Awake()
        {
            InitializeInputActions();
        }

        private void InitializeInputActions()
        {
            // Try to find the PlayerControls asset automatically
            if (inputActions == null)
            {
                inputActions = Resources.Load<InputActionAsset>("PlayerControls");
                if (inputActions == null)
                {
                    // Search for any InputActionAsset in the project
                    var foundAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                    if (foundAssets.Length > 0)
                    {
                        inputActions = foundAssets[0];
                        Debug.Log($"Found Input Action Asset: {inputActions.name}");
                    }
                }
            }

            if (inputActions != null)
            {
                // Find actions by name in the GamePlay action map
                var gameplayMap = inputActions.FindActionMap("GamePlay");
                if (gameplayMap != null)
                {
                    moveAction = gameplayMap.FindAction("Move");
                    jumpAction = gameplayMap.FindAction("Jump");
                    sprintAction = gameplayMap.FindAction("Sprint");
                    strafeAction = gameplayMap.FindAction("Strafe");
                    cameraAction = gameplayMap.FindAction("CameraLook");
                    
                    Debug.Log("Input actions initialized successfully");
                }
                else
                {
                    Debug.LogWarning("GamePlay action map not found in InputActionAsset");
                }
            }
            else
            {
                Debug.LogError("No InputActionAsset found. Please assign it manually in the inspector.");
            }
        }

        protected virtual void OnEnable()
        {
            if (inputActions != null)
            {
                inputActions.Enable();
                
                // Subscribe to available input events
                if (moveAction != null)
                {
                    moveAction.performed += OnMoveInput;
                    moveAction.canceled += OnMoveInput;
                }
                
                if (jumpAction != null)
                {
                    jumpAction.performed += OnJumpInput;
                }
                
                if (sprintAction != null)
                {
                    sprintAction.performed += OnSprintInput;
                    sprintAction.canceled += OnSprintInput;
                }
                
                if (strafeAction != null)
                {
                    strafeAction.performed += OnStrafeInput;
                }
                
                if (cameraAction != null)
                {
                    cameraAction.performed += OnCameraInput;
                    cameraAction.canceled += OnCameraInput;
                }
            }
        }

        protected virtual void OnDisable()
        {
            if (inputActions != null)
            {
                // Unsubscribe from input events
                if (moveAction != null)
                {
                    moveAction.performed -= OnMoveInput;
                    moveAction.canceled -= OnMoveInput;
                }
                
                if (jumpAction != null)
                {
                    jumpAction.performed -= OnJumpInput;
                }
                
                if (sprintAction != null)
                {
                    sprintAction.performed -= OnSprintInput;
                    sprintAction.canceled -= OnSprintInput;
                }
                
                if (strafeAction != null)
                {
                    strafeAction.performed -= OnStrafeInput;
                }
                
                if (cameraAction != null)
                {
                    cameraAction.performed -= OnCameraInput;
                    cameraAction.canceled -= OnCameraInput;
                }
                
                inputActions.Disable();
            }
        }

        protected virtual void OnDestroy()
        {
            // InputActionAsset doesn't need explicit disposal when loaded from Resources
            // Just make sure it's disabled
            if (inputActions != null)
            {
                inputActions.Disable();
            }
        }

        #region Input Callbacks

        private void OnMoveInput(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        private void OnJumpInput(InputAction.CallbackContext context)
        {
            if (context.performed)
                jumpPressed = true;
        }

        private void OnSprintInput(InputAction.CallbackContext context)
        {
            sprintHeld = context.ReadValueAsButton();
        }

        private void OnStrafeInput(InputAction.CallbackContext context)
        {
            if (context.performed)
                strafePressed = true;
        }

        private void OnCameraInput(InputAction.CallbackContext context)
        {
            cameraInput = context.ReadValue<Vector2>();
        }

        #endregion

        protected virtual void Start()
        {
            InitilizeController();
            InitializeTpCamera();
        }

        protected virtual void FixedUpdate()
        {
            cc.UpdateMotor();               // updates the ThirdPersonMotor methods
            cc.ControlLocomotionType();     // handle the controller locomotion type and movespeed
            cc.ControlRotationType();       // handle the controller rotation type
        }

        protected virtual void Update()
        {
            InputHandle();                  // update the input methods
            cc.UpdateAnimator();            // updates the Animator Parameters
        }

        public virtual void OnAnimatorMove()
        {
            cc.ControlAnimatorRootMotion(); // handle root motion animations 
        }

        #region Basic Locomotion Inputs

        protected virtual void InitilizeController()
        {
            cc = GetComponent<vThirdPersonController>();

            if (cc != null)
                cc.Init();
        }

        protected virtual void InitializeTpCamera()
        {
            if (tpCamera == null)
            {
                tpCamera = FindFirstObjectByType<vThirdPersonCamera>();
                if (tpCamera == null)
                    return;
                if (tpCamera)
                {
                    tpCamera.SetMainTarget(this.transform);
                    tpCamera.Init();
                }
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

            if (cameraMain)
            {
                cc.UpdateMoveDirection(cameraMain.transform);
            }

            if (tpCamera == null)
                return;

            // Use new input system values
            var y = cameraInput.y;
            var x = cameraInput.x;

            tpCamera.RotateCamera(x, y);
        }

        protected virtual void StrafeInput()
        {
            if (strafePressed)
            {
                cc.Strafe();
                strafePressed = false; // Reset flag
            }
        }

        protected virtual void SprintInput()
        {
            cc.Sprint(sprintHeld);
        }

        /// <summary>
        /// Conditions to trigger the Jump animation & behavior
        /// </summary>
        /// <returns></returns>
        protected virtual bool JumpConditions()
        {
            return cc.isGrounded && cc.GroundAngle() < cc.slopeLimit && !cc.isJumping && !cc.stopMove;
        }

        /// <summary>
        /// Input to trigger the Jump 
        /// </summary>
        protected virtual void JumpInput()
        {
            if (jumpPressed && JumpConditions())
            {
                cc.Jump();
                jumpPressed = false; // Reset flag
            }
        }

        #endregion       
    }
}