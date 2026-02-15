using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Core
{
    /// <summary>
    /// Punto único para leer entradas del gamepad a través del <see cref="PlayerControls"/>
    /// registrado en el <see cref="ServiceLocator"/>. Evita el uso de <c>Input.Get*</c>
    /// y garantiza que todos los sistemas consultan el mismo asset de Input System.
    /// </summary>
    public static class GamepadInputReader
    {
    public enum InputEventType
    {
        Submit,
        Cancel,
        Start,
        Navigate,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight,
        Interact,
        LeftShoulder,
        RightShoulder
    }

    public readonly struct InputEvent
    {
        public readonly InputEventType Type;
        public readonly Vector2 Value;
        public readonly InputActionPhase Phase;

        public InputEvent(InputEventType type, Vector2 value, InputActionPhase phase)
        {
            Type = type;
            Value = value;
            Phase = phase;
        }
    }

    public static event Action<InputEvent> OnInput;

    private static PlayerControls _controls;
    private static Vector2 _navCurrent;
    private static Vector2 _navPrevious;
    private static int _navFrame = -1;
    private static PlayerControls _boundControls;
    private static bool _pollingRegistered;
    private static readonly System.Collections.Generic.HashSet<object> _gameplaySuppressionOwners = new();
    private static int _uiNavigationScopeCount;
    private static float _ignoreCancelUntil = 0f; // Tiempo hasta el cual ignorar el botón Cancel/B
    private static float _ignoreJumpUntil = 0f; // Tiempo hasta el cual ignorar el botón Jump/A (después de interactuar)
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        OnInput = null;
        _controls = null;
        _navCurrent = Vector2.zero;
        _navPrevious = Vector2.zero;
        _navFrame = -1;
        _boundControls = null;
        _pollingRegistered = false;
        _gameplaySuppressionOwners.Clear();
        _uiNavigationScopeCount = 0;
        _ignoreCancelUntil = 0f;
        _ignoreJumpUntil = 0f;
        enableUIAudio = true;
    }
    #endif
    
    /// <summary>
    /// Si está activado, reproduce SFX de UI automáticamente en navegación, submit, cancel, etc.
    /// </summary>
    public static bool enableUIAudio = true;

#if ENABLE_INPUT_SYSTEM
    private static Gamepad GetGamepad()
    {
        var gp = Gamepad.current;
        if (gp == null && Gamepad.all.Count > 0)
            gp = Gamepad.all[0];
        return gp;
    }

    private static Joystick GetJoystick()
    {
        var js = Joystick.current;
        if (js == null && Joystick.all.Count > 0)
            js = Joystick.all[0];
        return js;
    }

    private static ButtonControl GetJoystickButton(Joystick js, params string[] names)
    {
        if (js == null || names == null) return null;

        foreach (var name in names)
        {
            var button = js.TryGetChildControl<ButtonControl>(name);
            if (button != null)
                return button;
        }

        return null;
    }

    private static DpadControl GetJoystickHat(Joystick js, params string[] names)
    {
        if (js == null || names == null) return null;

        foreach (var name in names)
        {
            var hat = js.TryGetChildControl<DpadControl>(name);
            if (hat != null)
                return hat;
        }

        return null;
    }
#endif

    private static PlayerControls Controls
    {
        get
        {
            if (_controls != null) return _controls;

            InitializeControls();

            return _controls;
        }
    }

    private static void InitializeControls()
    {
        if (_controls != null) return;

        if (ServiceLocator.TryGet(out PlayerInputManager pim))
        {
            _controls = pim.Controls;
            
            // Suscribirse a los eventos cuando se inicializa
            if (_controls != null && _boundControls != _controls)
            {
                UnsubscribeInputEvents();
                SubscribeInputEvents(_controls);
            }
            
            EnsurePollingRegistered();
        }
        else
        {
            // NO crear controles si PlayerInputManager no existe
            // Simplemente esperar a que esté disponible
            if (UnityEngine.Application.isPlaying)
            {
                Debug.LogWarning("[GamepadInputReader] PlayerInputManager aún no está disponible. Se inicializará cuando esté listo.");
            }
        }
    }

    public static void EnsureInputEventsSubscribed()
    {
        // Si _controls es null, intentar inicializar
        if (_controls == null)
        {
            InitializeControls();
            // Si sigue siendo null después de inicializar, no hay nada que hacer
            if (_controls == null) return;
        }

        // Si ya está suscrito, no hacer nada
        if (_boundControls == _controls) return;

        // Suscribirse a los eventos
        UnsubscribeInputEvents();
        SubscribeInputEvents(_controls);
        EnsurePollingRegistered();
    }

    private static void SubscribeInputEvents(PlayerControls controls)
    {
        if (controls == null) return;

        _boundControls = controls;

        controls.UI.Submit.performed += HandleSubmit;
        controls.UI.Cancel.performed += HandleCancel;
        controls.UI.Navigate.started += HandleNavigate;
        controls.UI.Navigate.performed += HandleNavigate;
        controls.UI.Navigate.canceled += HandleNavigate;
        controls.GamePlay.DPadUp.performed += HandleDpadUp;
        controls.GamePlay.DPadDown.performed += HandleDpadDown;
        controls.GamePlay.DPadLeft.performed += HandleDpadLeft;
        controls.GamePlay.DPadRight.performed += HandleDpadRight;
        controls.GamePlay.Interact.performed += HandleInteract;
        controls.GamePlay.Start.performed += HandleStart;
        controls.GamePlay.Strafe.performed += HandleLeftShoulder;
        controls.GamePlay.FlyToggle.performed += HandleRightShoulder;
    }

    private static void UnsubscribeInputEvents()
    {
        if (_boundControls == null) return;

        _boundControls.UI.Submit.performed -= HandleSubmit;
        _boundControls.UI.Cancel.performed -= HandleCancel;
        _boundControls.UI.Navigate.started -= HandleNavigate;
        _boundControls.UI.Navigate.performed -= HandleNavigate;
        _boundControls.UI.Navigate.canceled -= HandleNavigate;
        _boundControls.GamePlay.DPadUp.performed -= HandleDpadUp;
        _boundControls.GamePlay.DPadDown.performed -= HandleDpadDown;
        _boundControls.GamePlay.DPadLeft.performed -= HandleDpadLeft;
        _boundControls.GamePlay.DPadRight.performed -= HandleDpadRight;
        _boundControls.GamePlay.Interact.performed -= HandleInteract;
        _boundControls.GamePlay.Start.performed -= HandleStart;
        _boundControls.GamePlay.Strafe.performed -= HandleLeftShoulder;
        _boundControls.GamePlay.FlyToggle.performed -= HandleRightShoulder;

        _boundControls = null;
    }

    private static void EnsurePollingRegistered()
    {
#if ENABLE_INPUT_SYSTEM
        if (_pollingRegistered) return;
        InputSystem.onAfterUpdate += PollHardwareFallback;
        _pollingRegistered = true;
#endif
    }

    public static void PushGameplaySuppression(object owner)
    {
        if (owner == null) return;
        _gameplaySuppressionOwners.Add(owner);
    }

    public static void PopGameplaySuppression(object owner)
    {
        if (owner == null) return;
        _gameplaySuppressionOwners.Remove(owner);
    }

    public static void PushUiNavigationScope()
    {
        _uiNavigationScopeCount = Mathf.Max(0, _uiNavigationScopeCount) + 1;
    }

    public static void PopUiNavigationScope()
    {
        _uiNavigationScopeCount = Mathf.Max(0, _uiNavigationScopeCount - 1);
    }

    /// <summary>
    /// Ignora el botón Cancel/B durante el tiempo especificado.
    /// Útil para evitar que el botón que cerró un menú se procese en gameplay.
    /// </summary>
    public static void IgnoreCancelButton(float duration)
    {
        _ignoreCancelUntil = Time.unscaledTime + duration;
    }
    
    /// <summary>
    /// Ignora el botón Jump/A durante el tiempo especificado.
    /// Útil para evitar que el botón que activó una interacción se procese como salto.
    /// </summary>
    public static void IgnoreJumpButton(float duration)
    {
        _ignoreJumpUntil = Time.unscaledTime + duration;
    }

    public static void PlayUISound(string soundKey)
    {
        if (!enableUIAudio) return;
        if (AudioService.Instance == null) return;
        AudioService.Instance.PlaySFX(soundKey, volume: 1f);
    }

    private static void HandleSubmit(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.Submit, ctx, Vector2.zero);
    }
    
    private static void HandleCancel(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.Cancel, ctx, Vector2.zero);
    }
    
    private static void HandleStart(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.Start, ctx, Vector2.zero);
    }
    
    private static void HandleDpadUp(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.DpadUp, ctx, Vector2.up);
    }
    
    private static void HandleDpadDown(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.DpadDown, ctx, Vector2.down);
    }
    
    private static void HandleDpadLeft(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.DpadLeft, ctx, Vector2.left);
    }
    
    private static void HandleDpadRight(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.DpadRight, ctx, Vector2.right);
    }
    
    private static void HandleInteract(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.Interact, ctx, Vector2.zero);
    }
    
    private static void HandleNavigate(InputAction.CallbackContext ctx)
    {
        var value = ctx.ReadValue<Vector2>();
        Raise(InputEventType.Navigate, ctx, value);
    }
    
    private static void HandleLeftShoulder(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.LeftShoulder, ctx, Vector2.zero);
    }
    
    private static void HandleRightShoulder(InputAction.CallbackContext ctx)
    {
        Raise(InputEventType.RightShoulder, ctx, Vector2.zero);
    }

    private static void Raise(InputEventType type, InputAction.CallbackContext ctx, Vector2 value)
    {
        if (ShouldSuppress(type)) return;
        OnInput?.Invoke(new InputEvent(type, value, ctx.phase));

    }

    private static bool ShouldSuppress(InputEventType type)
    {
        bool suppressGameplay = _gameplaySuppressionOwners.Count > 0 || (_controls != null && !_controls.GamePlay.enabled);
        bool allowNavigationDuringUi = _uiNavigationScopeCount > 0;
        if (!suppressGameplay) return false;

        switch (type)
        {
            case InputEventType.DpadUp:
            case InputEventType.DpadDown:
            case InputEventType.DpadLeft:
            case InputEventType.DpadRight:
                return !allowNavigationDuringUi;
            default:
                return false;
        }
    }

    /// <summary>
    /// Verifica si los inputs de gameplay están actualmente suprimidos (por estar en UI)
    /// </summary>
    private static bool IsGameplaySuppressed()
    {
        return _gameplaySuppressionOwners.Count > 0 || (_controls != null && !_controls.GamePlay.enabled);
    }

#if ENABLE_INPUT_SYSTEM
    private static void PollHardwareFallback()
    {
        var gp = GetGamepad();
        if (gp == null) return;

        if (gp.startButton != null && gp.startButton.wasPressedThisFrame)
        {
            OnInput?.Invoke(new InputEvent(InputEventType.Start, Vector2.zero, InputActionPhase.Performed));
        }

        var dpad = gp.dpad;
        if (dpad != null)
        {
            if (dpad.up.wasPressedThisFrame && !ShouldSuppress(InputEventType.DpadUp)) OnInput?.Invoke(new InputEvent(InputEventType.DpadUp, Vector2.up, InputActionPhase.Performed));
            if (dpad.down.wasPressedThisFrame && !ShouldSuppress(InputEventType.DpadDown)) OnInput?.Invoke(new InputEvent(InputEventType.DpadDown, Vector2.down, InputActionPhase.Performed));
            if (dpad.left.wasPressedThisFrame && !ShouldSuppress(InputEventType.DpadLeft)) OnInput?.Invoke(new InputEvent(InputEventType.DpadLeft, Vector2.left, InputActionPhase.Performed));
            if (dpad.right.wasPressedThisFrame && !ShouldSuppress(InputEventType.DpadRight)) OnInput?.Invoke(new InputEvent(InputEventType.DpadRight, Vector2.right, InputActionPhase.Performed));
        }
        if (gp.buttonEast != null && gp.buttonEast.wasPressedThisFrame)
        {
            // Ignorar buttonEast si estamos dentro del período de ignorar (después de cerrar menús)
            if (Time.unscaledTime < _ignoreCancelUntil)
            {
                // Botón ignorado, no hacer nada
            }
            else
            {
                OnInput?.Invoke(new InputEvent(InputEventType.Cancel, Vector2.zero, InputActionPhase.Performed));
            }
        }
        if (gp.buttonSouth != null && gp.buttonSouth.wasPressedThisFrame)
            OnInput?.Invoke(new InputEvent(InputEventType.Submit, Vector2.zero, InputActionPhase.Performed));
        if (gp.leftShoulder != null && gp.leftShoulder.wasPressedThisFrame)
            OnInput?.Invoke(new InputEvent(InputEventType.LeftShoulder, Vector2.left, InputActionPhase.Performed));
        if (gp.rightShoulder != null && gp.rightShoulder.wasPressedThisFrame)
            OnInput?.Invoke(new InputEvent(InputEventType.RightShoulder, Vector2.right, InputActionPhase.Performed));
    }
#endif

    private static void UpdateNavigationCache()
    {
        if (_navFrame == Time.frameCount) return;

        _navPrevious = _navCurrent;
        _navCurrent = Navigation;
        _navFrame = Time.frameCount;
    }

    private static bool DirectionStarted(Vector2 direction, float deadZone = 0.45f)
    {
        UpdateNavigationCache();
        if (_navCurrent.sqrMagnitude < deadZone * deadZone) return false;

        var normalized = _navCurrent.normalized;
        var prevNorm = _navPrevious.sqrMagnitude > 0.0001f ? _navPrevious.normalized : Vector2.zero;
        float dot = Vector2.Dot(normalized, direction);
        float prevDot = Vector2.Dot(prevNorm, direction);
        return dot > 0.8f && prevDot <= 0.8f;
    }


    public static Vector2 Navigation
    {
        get
        {
            if (Controls != null && Controls.UI.Navigate.enabled)
                return Controls.UI.Navigate.ReadValue<Vector2>();

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null)
            {
                var nav = gp.leftStick.ReadValue();
                if (nav.sqrMagnitude > 0.01f) return nav;
                return gp.dpad.ReadValue();
            }

            var js = GetJoystick();
            if (js != null)
            {
                var nav = js.stick.ReadValue();
                if (nav.sqrMagnitude > 0.01f) return nav;
                try
                {
                    var hat = GetJoystickHat(js, "hat", "hatSwitch", "pov", "povHat");
                    if (hat != null)
                    {
                        var value = hat.ReadValue();
                        if (value.sqrMagnitude > 0.01f) return value;
                    }
                }
                catch { }
            }
#endif

            return Vector2.zero;
        }
    }

    /// <summary>
    /// Lectura exclusiva del D-Pad (sin incluir el left stick ni bindings UI.Navigate).
    /// Útil cuando queremos distinguir entradas del D-Pad del stick analógico.
    /// </summary>
    public static Vector2 DpadRaw
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null)
            {
                var v = gp.dpad.ReadValue();
                if (v.sqrMagnitude > 0.0001f) return v;
            }

            var js = GetJoystick();
            if (js != null)
            {
                try
                {
                    var hat = GetJoystickHat(js, "hat", "hatSwitch", "pov", "povHat");
                    if (hat != null)
                    {
                        var value = hat.ReadValue();
                        if (value.sqrMagnitude > 0.0001f) return value;
                    }
                }
                catch { }
            }
#endif
            return Vector2.zero;
        }
    }

    public static bool NavigateUp => DirectionStarted(Vector2.up);

    public static bool SubmitPressed
    {
        get
        {
            if (Controls != null && Controls.UI.Submit.triggered)
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonSouth.wasPressedThisFrame)
                return true;
#endif

            return false;
        }
    }

    public static bool CancelPressed
    {
        get
        {
            if (Controls != null && Controls.UI.Cancel.triggered)
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = Gamepad.current;
            if (gp != null && gp.buttonEast.wasPressedThisFrame)
                return true;
#endif

            return false;
        }
    }

    /// <summary>
    /// Lee el botón Left Shoulder (LB/L1) del Action Map UI para navegación de menús.
    /// NO tiene restricciones de supresión.
    /// Solo lee del ActionMap UI, no del hardware directamente para evitar conflictos con gameplay.
    /// </summary>
    public static bool LeftShoulderPressedUI
    {
        get
        {
            // SOLO leer del ActionMap UI, no del hardware
            if (Controls != null && Controls.UI.enabled && Controls.UI.LB.triggered)
                return true;


            return false;
        }
    }

    /// <summary>
    /// Lee el botón Right Shoulder (RB/R1) del Action Map UI para navegación de menús.
    /// NO tiene restricciones de supresión.
    /// Solo lee del ActionMap UI, no del hardware directamente para evitar conflictos con gameplay.
    /// </summary>
    public static bool RightShoulderPressedUI
    {
        get
        {
            // SOLO leer del ActionMap UI, no del hardware
            if (Controls != null && Controls.UI.enabled && Controls.UI.RB.triggered)
                return true;


            return false;
        }
    }

    /// <summary>
    /// Lee el botón Y (Triangle en PlayStation) del gamepad para la UI.
    /// NO tiene restricciones de supresión - se usa específicamente en menús.
    /// Lee directamente del hardware ya que no hay un action Y en el ActionMap UI.
    /// </summary>
    public static bool YButtonPressedUI
    {
        get
        {
            // Verificar que estemos en modo UI para evitar conflictos
            if (ServiceLocator.TryGet(out PlayerInputManager pim) && !pim.IsInUIMode)
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.yButton.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var y = GetJoystickButton(js, "buttonNorth", "triangle", "button3");
                if (y != null && y.wasPressedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    public static bool DpadUpPressed
    {
        get
        {
            if (Controls != null && Controls.GamePlay.DPadUp.triggered)
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.dpad.up.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var stick = js.stick;
                if (stick != null && stick.up.wasPressedThisFrame)
                    return true;

                try
                {
                    var hat = GetJoystickHat(js, "hat", "hatSwitch", "pov", "povHat");
                    if (hat != null && hat.up.wasPressedThisFrame)
                        return true;
                }
                catch { }
            }
#endif

            return false;
        }
    }

    public static bool StartPressed
    {
        get
        {
            if (Controls != null && Controls.GamePlay.Start.triggered)
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.startButton.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var start = GetJoystickButton(js, "start", "startButton", "menu", "options", "button9", "button10");
                if (start != null && start.wasPressedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    public static bool LeftShoulderPressed
    {
        get
        {
            // Si los inputs de gameplay están suprimidos (menú abierto), NO leer el botón
            // para que no afecte al player en gameplay
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.leftShoulder.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var lb = GetJoystickButton(js, "leftShoulder", "L1", "button4", "button6");
                if (lb != null && lb.wasPressedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    public static bool RightShoulderPressed
    {
        get
        {
            // Si los inputs de gameplay están suprimidos (menú abierto), NO leer el botón
            // para que no afecte al player en gameplay
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.rightShoulder.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var rb = GetJoystickButton(js, "rightShoulder", "R1", "button5", "button7");
                if (rb != null && rb.wasPressedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    public static bool YButtonPressed
    {
        get
        {
            // Si los inputs de gameplay están suprimidos (menú abierto), NO leer el botón
            // para que no afecte al player en gameplay
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.yButton.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var y = GetJoystickButton(js, "buttonNorth", "triangle", "button3");
                if (y != null && y.wasPressedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    /// <summary>
    /// Botón X (West/Square) - Usado para magia izquierda.
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool AttackMagicLeftPressed
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonWest.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var x = GetJoystickButton(js, "buttonWest", "square", "button0");
                if (x != null && x.wasPressedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    /// <summary>
    /// Botón B (East/Circle) - Usado para magia derecha.
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool AttackMagicRightPressed
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonEast.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var b = GetJoystickButton(js, "buttonEast", "circle", "button1");
                if (b != null && b.wasPressedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    /// <summary>
    /// Botón Y (North/Triangle) - Usado para magia especial.
    /// Respeta supresión de gameplay. Es un alias de YButtonPressed para consistencia.
    /// </summary>
    public static bool AttackMagicSpecialPressed => YButtonPressed;

    /// <summary>
    /// Botón X (West/Square) - Mantenido presionado (para levitación izquierda).
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool AttackMagicLeftHeld
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonWest.isPressed)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var x = GetJoystickButton(js, "buttonWest", "square", "button0");
                if (x != null && x.isPressed)
                    return true;
            }
#endif

            return false;
        }
    }

    /// <summary>
    /// Botón X (West/Square) - Soltado este frame (para levitación izquierda).
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool AttackMagicLeftReleased
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonWest.wasReleasedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var x = GetJoystickButton(js, "buttonWest", "square", "button0");
                if (x != null && x.wasReleasedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    /// <summary>
    /// Botón B (East/Circle) - Mantenido presionado (para levitación derecha).
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool AttackMagicRightHeld
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonEast.isPressed)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var b = GetJoystickButton(js, "buttonEast", "circle", "button1");
                if (b != null && b.isPressed)
                    return true;
            }
#endif

            return false;
        }
    }

    /// <summary>
    /// Botón B (East/Circle) - Soltado este frame (para levitación derecha).
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool AttackMagicRightReleased
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonEast.wasReleasedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var b = GetJoystickButton(js, "buttonEast", "circle", "button1");
                if (b != null && b.wasReleasedThisFrame)
                    return true;
            }
#endif

            return false;
        }
    }

    public static Vector2 Move => Controls != null ? Controls.GamePlay.Move.ReadValue<Vector2>() : Vector2.zero;
    public static Vector2 CameraLook => Controls != null ? Controls.GamePlay.CameraLook.ReadValue<Vector2>() : Vector2.zero;
    
    /// <summary>
    /// Lee si el botón de sprint está mantenido presionado.
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool SprintHeld
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

            if (Controls != null && Controls.GamePlay.Sprint.IsPressed())
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.leftShoulder.isPressed)
                return true;
#endif

            return false;
        }
    }

    /// <summary>
    /// Lee si el botón de jump fue presionado este frame.
    /// Respeta supresión de gameplay y cooldown de interacción.
    /// </summary>
    public static bool JumpPressed
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;
            
            // IMPORTANTE: Ignorar Jump si estamos en el período de cooldown de interacción
            // Esto evita que el botón A usado para interactuar se procese como salto
            if (Time.unscaledTime < _ignoreJumpUntil)
                return false;

            if (Controls != null && Controls.GamePlay.Jump.triggered)
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.buttonSouth.wasPressedThisFrame)
                return true;
#endif

            return false;
        }
    }

    /// <summary>
    /// Lee si el botón de strafe fue presionado este frame.
    /// Respeta supresión de gameplay.
    /// </summary>
    public static bool StrafePressed
    {
        get
        {
            if (IsGameplaySuppressed())
                return false;

            if (Controls != null && Controls.GamePlay.Strafe.triggered)
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.rightShoulder.wasPressedThisFrame)
                return true;
#endif

            return false;
        }
    }
    
    /// <summary>
    /// Lee CameraLook directamente del hardware sin restricciones de supresión.
    /// Útil para sistemas que necesitan leer el input independientemente del estado del juego.
    /// </summary>
    public static Vector2 CameraLookRaw
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.rightStick != null)
            {
                return gp.rightStick.ReadValue();
            }
            
            var js = GetJoystick();
            if (js != null)
            {
                // Intentar leer del segundo stick si existe
                var rightStick = js.TryGetChildControl<UnityEngine.InputSystem.Controls.StickControl>("rightStick");
                if (rightStick != null)
                {
                    return rightStick.ReadValue();
                }
            }
#endif
            return Vector2.zero;
        }
    }
    }
}
