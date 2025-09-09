using UnityEngine;
using UnityEngine.InputSystem;

namespace Invector.vCharacterController
{
    public class vThirdPersonInput : MonoBehaviour
    {
        #region Variables       

        [Header("Controller Input (legacy placeholders)")]
        [System.Obsolete("Use new Input System instead")] public string horizontalInput = "Horizontal";
        [System.Obsolete("Use new Input System instead")] public string verticallInput = "Vertical";
        [System.Obsolete("Use new Input System instead")] public KeyCode jumpInput = KeyCode.Space;
        [System.Obsolete("Use new Input System instead")] public KeyCode strafeInput = KeyCode.Tab;
        [System.Obsolete("Use new Input System instead")] public KeyCode sprintInput = KeyCode.LeftShift;

        [Header("Camera Input (legacy placeholders)")]
        [System.Obsolete("Use new Input System instead")] public string rotateCameraXInput = "Mouse X";
        [System.Obsolete("Use new Input System instead")] public string rotateCameraYInput = "Mouse Y";

        [Header("New Input System")]
        [SerializeField] private InputActionAsset inputActions;

        // Input Actions
        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction strafeAction;
        private InputAction cameraAction;
        private InputAction attackPhysicalAction;
        private InputAction attackMagicAction;

        [HideInInspector] public vThirdPersonController cc;
        [HideInInspector] public vThirdPersonCamera tpCamera;
        [HideInInspector] public Camera cameraMain;

        // Input values
        private Vector2 moveInput;
        private Vector2 cameraInput;
        private bool jumpPressed;
        private bool sprintHeld;
        private bool strafePressed;

        // Flags de ataque (se consumen en Update)
        private bool attackPhysicalPressed;
        private bool attackMagicPressed;

        // Para edge-detection (botón abajo este frame)
        private bool prevPhysicalHeld;
        private bool prevMagicHeld;

        [Header("Magic Combo (timing)")]
        [SerializeField] private float magicFirstTapWindow = 1.20f; // (seguimos respetándola si la usabas en debug)
        [SerializeField] private float magicInterTapGrace  = 0.75f; // (seguimos respetándola si la usabas en debug)
        private int   magicTapCount   = 0;    // (no se usa ya en la lógica nueva, lo mantengo por compatibilidad/inspector)
        private float magicAnchorUntil = 0f;  // (idem)
        private float magicLastTapTime = 0f;  // (idem)

        [Header("Debug")]
        [SerializeField] private bool debugMagic = false;

        // ===== NUEVOS (cadena 3+1 y reseteo limpio) =====
        [Header("Magic Chain (3 casts + 4º combo)")]
        [SerializeField] private float magicChainWindow = 1.50f;   // ventana total desde el primer tap
        [SerializeField] private float magicInterTapMax = 0.80f;   // máximo entre taps consecutivos
        private int   magicChainCount = 0;  // 0..3 (en 4º entra combo)
        private float magicChainExpireAt = 0f;
        private float magicLastTapAt = 0f;

        #endregion

        protected virtual void Awake()
        {
            InitializeInputActions();
        }

        private void InitializeInputActions()
        {
            if (inputActions == null)
            {
                inputActions = Resources.Load<InputActionAsset>("PlayerControls");
                if (inputActions == null)
                {
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
                var gameplayMap = inputActions.FindActionMap("GamePlay");
                if (gameplayMap != null)
                {
                    moveAction           = gameplayMap.FindAction("Move");
                    jumpAction           = gameplayMap.FindAction("Jump");
                    sprintAction         = gameplayMap.FindAction("Sprint");
                    strafeAction         = gameplayMap.FindAction("Strafe");
                    cameraAction         = gameplayMap.FindAction("CameraLook");
                    attackPhysicalAction = gameplayMap.FindAction("AttackPhysical");
                    attackMagicAction    = gameplayMap.FindAction("AttackMagic");
                }
                else Debug.LogWarning("GamePlay action map not found in InputActionAsset");
            }
            else Debug.LogError("No InputActionAsset found. Please assign it manually in the inspector.");
        }

        protected virtual void OnEnable()
        {
            if (inputActions == null) return;
            inputActions.Enable();

            if (moveAction != null)
            {
                moveAction.performed += OnMoveInput;
                moveAction.canceled  += OnMoveInput;
            }
            if (jumpAction != null)   jumpAction.performed   += OnJumpInput;
            if (sprintAction != null)
            {
                sprintAction.performed += OnSprintInput;
                sprintAction.canceled  += OnSprintInput;
            }
            if (strafeAction != null) strafeAction.performed += OnStrafeInput;
            if (cameraAction != null)
            {
                cameraAction.performed += OnCameraInput;
                cameraAction.canceled  += OnCameraInput;
            }

            // Suscribimos BOTH: started y performed (por si tu acción es Tap/PressOnly/Release)
            if (attackPhysicalAction != null)
            {
                attackPhysicalAction.started   += OnAttackPhysicalEvent;
                attackPhysicalAction.performed += OnAttackPhysicalEvent;
            }
            if (attackMagicAction != null)
            {
                attackMagicAction.started   += OnAttackMagicEvent;
                attackMagicAction.performed += OnAttackMagicEvent;
            }

            prevPhysicalHeld = prevMagicHeld = false;
        }

        protected virtual void OnDisable()
        {
            if (inputActions == null) return;

            if (moveAction != null)
            {
                moveAction.performed -= OnMoveInput;
                moveAction.canceled  -= OnMoveInput;
            }
            if (jumpAction != null)   jumpAction.performed   -= OnJumpInput;
            if (sprintAction != null)
            {
                sprintAction.performed -= OnSprintInput;
                sprintAction.canceled  -= OnSprintInput;
            }
            if (strafeAction != null) strafeAction.performed -= OnStrafeInput;
            if (cameraAction != null)
            {
                cameraAction.performed -= OnCameraInput;
                cameraAction.canceled  -= OnCameraInput;
            }

            if (attackPhysicalAction != null)
            {
                attackPhysicalAction.started   -= OnAttackPhysicalEvent;
                attackPhysicalAction.performed -= OnAttackPhysicalEvent;
            }
            if (attackMagicAction != null)
            {
                attackMagicAction.started   -= OnAttackMagicEvent;
                attackMagicAction.performed -= OnAttackMagicEvent;
            }

            inputActions.Disable();
        }

        protected virtual void OnDestroy()
        {
            if (inputActions != null) inputActions.Disable();
        }

        #region Input Callbacks

        private void OnMoveInput(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
        private void OnJumpInput(InputAction.CallbackContext context) { if (context.performed) jumpPressed = true; }
        private void OnSprintInput(InputAction.CallbackContext context) => sprintHeld = context.ReadValueAsButton();
        private void OnStrafeInput(InputAction.CallbackContext context) { if (context.performed) strafePressed = true; }
        private void OnCameraInput(InputAction.CallbackContext context) => cameraInput = context.ReadValue<Vector2>();

        // Eventos de ataque (ambos tipos de evento para máxima compatibilidad)
        private void OnAttackPhysicalEvent(InputAction.CallbackContext _) { attackPhysicalPressed = true; }
        private void OnAttackMagicEvent(InputAction.CallbackContext _)    { attackMagicPressed    = true; }

        #endregion

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
            // Edge-detection adicional por polling (por si los eventos no llegan con Tap/Release-only)
            GatherAttackEdges();

            InputHandle();
            cc.UpdateAnimator();

            // Reset del combo clásico si vence (mantengo tu lógica antigua intacta por compatibilidad de inspector)
            if (magicTapCount > 0 && Time.time >= magicAnchorUntil)
            {
                if (debugMagic) Debug.Log("Combo mágico: ventana expirada, reset");
                magicTapCount   = 0;
                magicAnchorUntil = 0f;
            }

            // Expiración de la cadena nueva (3+1)
            if (magicChainCount > 0 && Time.time > magicChainExpireAt)
            {
                magicChainCount = 0;
                magicChainExpireAt = 0f;
                magicLastTapAt = 0f;
            }
        }

        public virtual void OnAnimatorMove()
        {
            cc.ControlAnimatorRootMotion();
        }

        #region Helpers

        // Asegura detectar el “down” aunque la acción sea Tap/Release-only/etc.
        private void GatherAttackEdges()
        {
            // Physical
            bool physHeld = attackPhysicalAction != null &&
                             (attackPhysicalAction.ReadValue<float>() > 0.5f || attackPhysicalAction.triggered);
            if (physHeld && !prevPhysicalHeld) attackPhysicalPressed = true;
            prevPhysicalHeld = physHeld;

            // Magic
            bool magicHeld = attackMagicAction != null &&
                             (attackMagicAction.ReadValue<float>() > 0.5f || attackMagicAction.triggered);
            if (magicHeld && !prevMagicHeld) attackMagicPressed = true;
            prevMagicHeld = magicHeld;
        }

        #endregion

        #region Basic Locomotion Inputs

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

            AttackPhysicalInput();
            AttackMagicInput();
        }

        // === NUEVO: helper para resetear cadena de magia si pulsas otras acciones relevantes ===
        private void ResetMagicChain()
        {
            magicChainCount = 0;
            magicChainExpireAt = 0f;
            magicLastTapAt = 0f;
        }

        private void AttackPhysicalInput()
        {
            if (!attackPhysicalPressed) return;
            attackPhysicalPressed = false;

            // Al pegar físico, rompemos la cadena de magia para que no contamine
            ResetMagicChain();

            Debug.Log("[Input] AttackPhysical presionado - llamando cc.AttackPhysical()");
            cc.AttackPhysical();
        }

        // Magic: 1ª, 2ª y 3ª -> CastMagic1, y en la 4ª -> CastMagicFinish (combo)
        private void AttackMagicInput()
        {
            if (!attackMagicPressed) return;
            attackMagicPressed = false;

            float now = Time.time;

            // ¿nueva cadena?
            if (magicChainCount == 0 || now > magicChainExpireAt)
            {
                magicChainCount   = 0;
                magicLastTapAt    = 0f;
                magicChainExpireAt = now + magicChainWindow;
            }

            // ¿exceso entre taps?
            if (magicChainCount > 0 && (now - magicLastTapAt) > magicInterTapMax)
            {
                magicChainCount   = 0;
                magicChainExpireAt = now + magicChainWindow;
            }

            magicChainCount++;
            magicLastTapAt = now;

            if (debugMagic) Debug.Log($"[Magic] Tap #{magicChainCount}");

            if (magicChainCount <= 3)
            {
                cc.CastMagic1();      // el controller protege de reinicios bruscos si spameas
                return;
            }

            // 4º tap: combo
            if (magicChainCount == 4)
            {
                cc.CastMagicFinish();
                magicChainCount   = 0;
                magicChainExpireAt = 0f;
                magicLastTapAt     = 0f;
            }
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
                // Si quieres romper la cadena al saltar, descomenta:
                // ResetMagicChain();
            }
        }

        #endregion
    }
}
