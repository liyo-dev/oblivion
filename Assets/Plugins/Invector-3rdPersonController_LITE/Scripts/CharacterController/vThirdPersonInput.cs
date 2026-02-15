using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Invector.vCharacterController
{
    /// <summary>
    /// Input handler para vThirdPersonController.
    /// IMPORTANTE: Ahora usa GamepadInputReader centralizado (via reflexión) en lugar de leer directamente del Input System.
    /// Esto asegura que la supresión de gameplay funcione correctamente cuando hay menús abiertos.
    /// </summary>
    public class vThirdPersonInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional reference that implements IActionValidator (e.g. PlayerActionManager)")]
        private MonoBehaviour actionValidatorSource;

        private IActionValidator actionValidator;

        private static bool lookInversionCached;
        private static Func<Vector2, Vector2> lookInversionDelegate;
        
        // Cache de reflexión para GamepadInputReader
        private static Type gamepadInputReaderType;
        private static System.Reflection.PropertyInfo moveProp;
        private static System.Reflection.PropertyInfo cameraLookProp;
        private static System.Reflection.PropertyInfo sprintHeldProp;
        private static System.Reflection.PropertyInfo jumpPressedProp;
        private static System.Reflection.PropertyInfo strafePressedProp;
        private static System.Reflection.PropertyInfo attackMagicLeftPressedProp;
        private static System.Reflection.PropertyInfo attackMagicRightPressedProp;
        private static System.Reflection.PropertyInfo attackMagicSpecialPressedProp;
        private static bool reflectionInitialized;

        [HideInInspector] public vThirdPersonController cc;
        [HideInInspector] public vThirdPersonCamera tpCamera;
        [HideInInspector] public Camera cameraMain;

        // Flight control
        public bool DisableVerticalCameraRotation { get; set; } = false;

        // Valores capturados del GamepadInputReader
        private Vector2 moveInput;
        private Vector2 cameraInput;
        private bool jumpPressed;
        private bool sprintHeld;
        private bool strafePressed;

        protected virtual void Awake()
        {
            ResolveServices();
            CacheLookInversionDelegate();
            InitializeReflection();
        }
        
        private static void InitializeReflection()
        {
            if (reflectionInitialized) return;
            reflectionInitialized = true;
            
            try
            {
                gamepadInputReaderType = Type.GetType("Core.GamepadInputReader, Assembly-CSharp");
                if (gamepadInputReaderType == null)
                {
                    Debug.LogError("[vThirdPersonInput] No se pudo encontrar Core.GamepadInputReader. Inputs no funcionarán.");
                    return;
                }
                
                moveProp = gamepadInputReaderType.GetProperty("Move", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                cameraLookProp = gamepadInputReaderType.GetProperty("CameraLook", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                sprintHeldProp = gamepadInputReaderType.GetProperty("SprintHeld", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                jumpPressedProp = gamepadInputReaderType.GetProperty("JumpPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                strafePressedProp = gamepadInputReaderType.GetProperty("StrafePressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                attackMagicLeftPressedProp = gamepadInputReaderType.GetProperty("AttackMagicLeftPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                attackMagicRightPressedProp = gamepadInputReaderType.GetProperty("AttackMagicRightPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                attackMagicSpecialPressedProp = gamepadInputReaderType.GetProperty("AttackMagicSpecialPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[vThirdPersonInput] Error inicializando reflexión: {ex.Message}");
            }
        }

        private void ResolveServices()
        {
            if (actionValidator == null)
            {
                if (actionValidatorSource != null)
                    actionValidator = actionValidatorSource as IActionValidator;

                if (actionValidator == null)
                    actionValidator = GetComponent<IActionValidator>();
            }
        }

        protected virtual void OnEnable()
        {
            // Ya no nos suscribimos a InputActions, leemos de GamepadInputReader en Update
        }

        protected virtual void OnDisable()
        {
            // Ya no nos desuscribimos de InputActions
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
            // Leer inputs de GamepadInputReader (centralizado)
            ReadInputsFromGamepadReader();
            
            InputHandle();
            cc.UpdateAnimator();
        }

        /// <summary>
        /// Lee todos los inputs del GamepadInputReader centralizado via reflexión.
        /// Esto respeta la supresión de gameplay cuando hay menús abiertos.
        /// </summary>
        private void ReadInputsFromGamepadReader()
        {
            if (gamepadInputReaderType == null) return;
            
            try
            {
                // Movimiento (respeta supresión de gameplay)
                if (moveProp != null)
                    moveInput = (Vector2)moveProp.GetValue(null);
                
                // Cámara (siempre disponible)
                if (cameraLookProp != null)
                    cameraInput = ApplyLookInversionSafe((Vector2)cameraLookProp.GetValue(null));
                
                // Sprint (respeta supresión y validación)
                if (CanSprint() && sprintHeldProp != null)
                    sprintHeld = (bool)sprintHeldProp.GetValue(null);
                else
                    sprintHeld = false;
                
                // Jump (respeta supresión y validación)
                if (CanJump() && jumpPressedProp != null && (bool)jumpPressedProp.GetValue(null))
                    jumpPressed = true;
                
                // Strafe toggle
                if (strafePressedProp != null && (bool)strafePressedProp.GetValue(null))
                    strafePressed = true;
                
                // Magia (respeta supresión y validación)
                if (CanMagic() && cc != null)
                {
                    if (attackMagicLeftPressedProp != null && (bool)attackMagicLeftPressedProp.GetValue(null))
                        cc.CastMagicLeft();
                    
                    if (attackMagicRightPressedProp != null && (bool)attackMagicRightPressedProp.GetValue(null))
                        cc.CastMagicRight();
                    
                    if (attackMagicSpecialPressedProp != null && (bool)attackMagicSpecialPressedProp.GetValue(null))
                        cc.CastMagicSpecial();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[vThirdPersonInput] Error leyendo inputs: {ex.Message}");
            }
        }

        public virtual void OnAnimatorMove()
        {
            cc.ControlAnimatorRootMotion();
        }

        // ===== Look inversion helper =====
        // Some projects may provide a `PlayerSettings` helper class. To avoid a hard dependency
        // we call it via reflection if present; otherwise we return the original input.
        private static Vector2 ApplyLookInversionSafe(Vector2 input)
        {
            if (lookInversionDelegate == null)
                return input;

            try
            {
                return lookInversionDelegate.Invoke(input);
            }
            catch (Exception)
            {
                return input;
            }
        }

        private static void CacheLookInversionDelegate()
        {
            if (lookInversionCached)
                return;

            lookInversionCached = true;

            try
            {
                // Buscar en Assembly-CSharp ya que PlayerSettings está ahí (no en firstpass)
                var t = Type.GetType("PlayerSettings, Assembly-CSharp");
                if (t == null)
                {
                    // Fallback: buscar sin assembly (por si está en el mismo)
                    t = Type.GetType("PlayerSettings");
                }
                if (t == null)
                {
                    Debug.LogWarning("[vThirdPersonInput] No se encontró PlayerSettings. Inversión de cámara no funcionará.");
                    return;
                }

                // Preferred overload: Vector2 -> Vector2
                var mi = t.GetMethod("ApplyLookInversion", new Type[] { typeof(Vector2) });
                if (mi != null && mi.IsStatic)
                {
                    lookInversionDelegate = Delegate.CreateDelegate(typeof(Func<Vector2, Vector2>), mi, throwOnBindFailure: false) as Func<Vector2, Vector2>;
                }

                if (lookInversionDelegate != null)
                {
                    Debug.Log("[vThirdPersonInput] LookInversion configurado correctamente (sobrecarga simple).");
                    return;
                }

                // Fallback overload: Vector2 + context bool
                mi = t.GetMethod("ApplyLookInversion", new Type[] { typeof(Vector2), typeof(bool) });
                if (mi != null && mi.IsStatic)
                {
                    lookInversionDelegate = (Vector2 v) =>
                    {
                        var res = mi.Invoke(null, new object[] { v, false });
                        return res is Vector2 vec ? vec : v;
                    };
                    Debug.Log("[vThirdPersonInput] LookInversion configurado correctamente (sobrecarga con contexto).");
                }
                else
                {
                    Debug.LogWarning("[vThirdPersonInput] No se encontró método ApplyLookInversion en PlayerSettings.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[vThirdPersonInput] Error configurando LookInversion: {ex.Message}");
                lookInversionDelegate = null;
            }
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

            float y = DisableVerticalCameraRotation ? 0f : cameraInput.y;
            tpCamera.RotateCamera(cameraInput.x, y);
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

            return actionValidator;
        }

        private bool CanJump()   => GetValidator()?.CanJump() ?? true;
        private bool CanSprint() => GetValidator()?.CanSprint() ?? true;
        private bool CanMagic()  => GetValidator()?.CanCastMagic() ?? true;
    }
}
