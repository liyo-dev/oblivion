using UnityEngine;
using UnityEngine.InputSystem;

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
            var gp = Gamepad.current;
            if (gp != null)
            {
                var nav = gp.leftStick.ReadValue();
                if (nav.sqrMagnitude > 0.01f) return nav;
                return gp.dpad.ReadValue();
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
            var gp = Gamepad.current;
            if (gp != null && gp.dpad.up.wasPressedThisFrame)
                return true;
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
            var gp = Gamepad.current;
            if (gp != null && gp.dpad.down.wasPressedThisFrame)
                return true;
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
            var gp = Gamepad.current;
            if (gp != null && gp.startButton.wasPressedThisFrame)
                return true;

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
