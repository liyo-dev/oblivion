using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Punto único para leer entradas del gamepad a través del <see cref="PlayerControls"/>
/// registrado en el <see cref="ServiceLocator"/>. Evita el uso de <c>Input.Get*</c>
/// y garantiza que todos los sistemas consultan el mismo asset de Input System.
/// </summary>
public static class GamepadInputReader
{
    private static PlayerControls _controls;
    private static Vector2 _navCurrent;
    private static Vector2 _navPrevious;
    private static int _navFrame = -1;

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

            if (ServiceLocator.TryGet(out PlayerInputManager pim) && pim.Controls != null)
            {
                _controls = pim.Controls;
            }
            else
            {
                _controls = PlayerInputManager.GetSharedOrNew(out _);
            }

            if (_controls != null)
            {
                if (!_controls.GamePlay.enabled) _controls.GamePlay.Enable();
                if (!_controls.UI.enabled) _controls.UI.Enable();
            }

            return _controls;
        }
    }

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

    public static PlayerControls ControlsOrNull => Controls;

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
    public static bool NavigateDown => DirectionStarted(Vector2.down);
    public static bool NavigateLeft => DirectionStarted(Vector2.left);
    public static bool NavigateRight => DirectionStarted(Vector2.right);

    public static bool SubmitPressed => Controls != null && Controls.UI.Submit.triggered;

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

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                return true;
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

    public static bool DpadDownPressed
    {
        get
        {
            if (Controls != null && Controls.GamePlay.DPadDown.triggered)
                return true;

#if ENABLE_INPUT_SYSTEM
            var gp = GetGamepad();
            if (gp != null && gp.dpad.down.wasPressedThisFrame)
                return true;

            var js = GetJoystick();
            if (js != null)
            {
                var stick = js.stick;
                if (stick != null && stick.down.wasPressedThisFrame)
                    return true;

                try
                {
                    var hat = GetJoystickHat(js, "hat", "hatSwitch", "pov", "povHat");
                    if (hat != null && hat.down.wasPressedThisFrame)
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

            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                return true;
#endif

            return false;
        }
    }

    public static bool InteractPressed => Controls != null && Controls.GamePlay.Interact.triggered;
    public static Vector2 Move => Controls != null ? Controls.GamePlay.Move.ReadValue<Vector2>() : Vector2.zero;
    public static Vector2 CameraLook => Controls != null ? Controls.GamePlay.CameraLook.ReadValue<Vector2>() : Vector2.zero;
}
