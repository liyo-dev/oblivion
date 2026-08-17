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
        private static System.Reflection.PropertyInfo shoulderLeftPressedProp;
        private static System.Reflection.PropertyInfo attackMagicLeftPressedProp;
        private static System.Reflection.PropertyInfo attackMagicRightPressedProp;
        private static System.Reflection.PropertyInfo attackMagicSpecialPressedProp;
        private static bool reflectionInitialized;

        // FIX COMPILACIÓN (16 ago 2026): este script vive en Assets/Plugins (assembly separado,
        // compilado ANTES que Assets/Scripts). Referenciar "Core.PlayerInputManager" directamente
        // aquí, como se hizo en un fix anterior de este mismo método, no compila
        // (CS0103: The name 'Core' does not exist in the current context) por la misma razón exacta
        // por la que GamepadInputReader/PlayerSettings de arriba se acceden vía reflexión en vez de
        // por referencia directa — ver también el comentario equivalente en
        // vThirdPersonCamera.cs sobre EnvironmentQuery como puente hacia EnvironmentController.
        // Mismo patrón de caché que gamepadInputReaderType: se resuelve una sola vez en
        // InitializeReflection(), no en cada Update().
        private static Type playerInputManagerType;
        private static System.Reflection.PropertyInfo playerInputManagerInstanceProp;
        private static System.Reflection.PropertyInfo isInUIModeProp;

        [HideInInspector] public vThirdPersonController cc;
        [HideInInspector] public vThirdPersonCamera tpCamera;
        [HideInInspector] public Camera cameraMain;

        // Flight control
        public bool DisableVerticalCameraRotation { get; set; } = false;

        /// <summary>
        /// Cuando está activo, suprime movimiento, sprint y salto pero mantiene la rotación de cámara.
        /// Usado por PartyControlManager cuando posee un compañero.
        /// </summary>
        public bool SuppressMoveInput { get; set; } = false;

        // Valores capturados del GamepadInputReader
        private Vector2 moveInput;
        private Vector2 cameraInput;
        private bool jumpPressed;
        private bool sprintHeld;
        private bool shoulderLeftPressed;

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
                shoulderLeftPressedProp = gamepadInputReaderType.GetProperty("ShoulderLeftPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                attackMagicLeftPressedProp = gamepadInputReaderType.GetProperty("AttackMagicLeftPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                attackMagicRightPressedProp = gamepadInputReaderType.GetProperty("AttackMagicRightPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                attackMagicSpecialPressedProp = gamepadInputReaderType.GetProperty("AttackMagicSpecialPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                // Ver comentario junto a los campos playerInputManagerType/... más arriba.
                playerInputManagerType = Type.GetType("Core.PlayerInputManager, Assembly-CSharp");
                if (playerInputManagerType == null)
                {
                    Debug.LogWarning("[vThirdPersonInput] No se pudo encontrar Core.PlayerInputManager. La supresión de cámara al abrir menús no funcionará.");
                }
                else
                {
                    playerInputManagerInstanceProp = playerInputManagerType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    isInUIModeProp = playerInputManagerType.GetProperty("IsInUIMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[vThirdPersonInput] Error inicializando reflexión: {ex.Message}");
            }
        }

        /// <summary>
        /// True si Core.PlayerInputManager.Instance.IsInUIMode está activo ahora mismo, resuelto vía
        /// reflexión (ver comentario junto a playerInputManagerType). Nunca lanza: cualquier fallo
        /// de reflexión se trata como "no suprimir", el comportamiento que había antes de este fix.
        /// </summary>
        private static bool IsPlayerInputManagerInUIMode()
        {
            if (playerInputManagerInstanceProp == null || isInUIModeProp == null) return false;

            try
            {
                var instance = playerInputManagerInstanceProp.GetValue(null);
                if (instance == null) return false;
                return (bool)isInUIModeProp.GetValue(instance);
            }
            catch (Exception)
            {
                return false;
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

        protected virtual void Update()
        {
            // Leer inputs de GamepadInputReader (centralizado)
            ReadInputsFromGamepadReader();

            InputHandle();

            // FIX (demo 14 ago): el motor de movimiento vivía en FixedUpdate (cadencia física fija,
            // p.ej. 50Hz) mientras que vThirdPersonCamera.cs pasó de FixedUpdate a LateUpdate ese
            // mismo día (commit a17b6b85a, "enhance cloud and dialogue camera functionality").
            // Resultado: a framerates > 50fps la cámara reevaluaba y suavizaba su posición/rotación
            // CADA frame renderizado, pero 'target.position' (este mismo transform) solo cambiaba
            // 50 veces por segundo — se quedaba quieto varios frames y luego saltaba de golpe. Eso
            // es justo la sensación de "retraso/lentitud" y mareo reportada en la demo. UpdateMotor/
            // ControlLocomotionType/ControlRotationType/AirVelocity ya usan Time.deltaTime
            // internamente (no Time.fixedDeltaTime), así que moverlos aquí es seguro: quedan en la
            // misma cadencia por frame que la cámara, sin descoordinación entre movimiento y render.
            //
            // FIX (15 ago 2026): con SuppressMoveInput=true (PlayerAmbientActivityHandler mientras
            // el jugador está sentado/comiendo/durmiendo en un NPCWorldPoint — CC ya desactivado a
            // propósito por ese handler) este bloque seguía ejecutándose igual. cc.UpdateMotor()
            // hace su propio ground-check y, con el CC apagado, concluye "no grounded"; cc.UpdateAnimator()
            // empuja ese IsGrounded=false al Animator aquí mismo, en Update(). Unity evalúa las
            // transiciones del Animator justo después de Update() y ANTES de LateUpdate() — así que
            // cualquier corrección de IsGrounded hecha en LateUpdate (como la de PlayerAmbientActivityHandler
            // o SleepTrigger) siempre llega un frame tarde, y la transición hacia el estado de caída ya
            // se disparó. Mismo bug exacto que ya se encontró y arregló en SleepTrigger (ahí se
            // desactivó todo el componente); aquí no podemos hacer lo mismo porque este método también
            // procesa CameraInput() dentro de InputHandle(), y sentado/comiendo el jugador debe poder
            // seguir mirando alrededor. Saltar solo el bloque de motor/animator cuando SuppressMoveInput
            // está activo deja CameraInput() funcionando y evita el push erróneo de IsGrounded.
            if (!SuppressMoveInput)
            {
                cc.UpdateMotor();
                cc.ControlLocomotionType();
                cc.ControlRotationType();
                cc.AirVelocity();

                cc.UpdateAnimator();
            }
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
                
                // Cámara: normalmente siempre disponible (para poder seguir mirando alrededor
                // durante bocadillos/diálogos que no empujan modo UI), PERO se suprime mientras
                // hay una pantalla de UI a pantalla completa abierta (tienda, menú principal,
                // inventario...). Sin esto, mover el ratón sobre los botones de esas pantallas
                // seguía llegando aquí y acumulando en mouseX/mouseY de vThirdPersonCamera —  y
                // como esas pantallas paran el tiempo (Time.timeScale = 0), el Slerp de
                // CameraMovement() no podía ir aplicando esa rotación en tiempo real: se quedaba
                // "guardada" y se descargaba de golpe al cerrar la pantalla y reanudar el tiempo,
                // lo que se veía como la cámara temblando/girando sola un rato hasta converger.
                // FIX (16/08/2026): cámara tiembla al volver de tienda/menú a gameplay.
                bool suppressCameraLook = IsPlayerInputManagerInUIMode();
                if (cameraLookProp != null)
                    cameraInput = suppressCameraLook ? Vector2.zero : ApplyLookInversionSafe((Vector2)cameraLookProp.GetValue(null));
                
                // Sprint (respeta supresión y validación)
                if (CanSprint() && sprintHeldProp != null)
                    sprintHeld = (bool)sprintHeldProp.GetValue(null);
                else
                    sprintHeld = false;
                
                // Jump (respeta supresión y validación)
                if (CanJump() && jumpPressedProp != null && (bool)jumpPressedProp.GetValue(null))
                    jumpPressed = true;
                
                // Gatillo izquierdo (LB/L1/L) — hoy sin acción de movimiento asociada, ver StrafeInput() más abajo
                if (shoulderLeftPressedProp != null && (bool)shoulderLeftPressedProp.GetValue(null))
                    shoulderLeftPressed = true;
                
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
                tpCamera = FindAnyObjectByType<vThirdPersonCamera>();
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
            if (SuppressMoveInput) { cc.input.x = 0; cc.input.z = 0; return; }
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
            if (shoulderLeftPressed)
            {
                //cc.Strafe();
                shoulderLeftPressed = false;
            }
        }

        protected virtual void SprintInput() => cc.Sprint(SuppressMoveInput ? false : sprintHeld);

        protected virtual bool JumpConditions()
        {
            return cc.isGrounded && cc.GroundAngle() < cc.slopeLimit && !cc.isJumping && !cc.stopMove;
        }

        protected virtual void JumpInput()
        {
            if (!SuppressMoveInput && jumpPressed && JumpConditions())
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

        /// <summary>
        /// Cancela el salto pendiente en cola. Llamar al salir del vuelo para evitar
        /// que el salto que activó el vuelo se ejecute automáticamente al aterrizar.
        /// </summary>
        public void CancelPendingJump() => jumpPressed = false;
    }
}
