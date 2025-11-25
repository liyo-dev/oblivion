using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SettingsMenuController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Selectable firstSelection;

    [Header("Language")]
    [SerializeField] private Button spanishButton;
    [SerializeField] private Button englishButton;

    [Header("Audio")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;

    [Header("Camera")]
    [SerializeField] private Toggle invertLookToggle;
    [SerializeField] private Toggle invertFlightToggle;
    [SerializeField] private Slider lookSensitivitySlider;

    [Header("Accesibilidad / General")]
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Toggle fullscreenToggle;

    private Action _onClosed;
    private EventSystem _eventSystem;

    [Header("Visuals")]
    [Tooltip("Auto-add UISelectVisual to selectables under this root to show navigation feedback.")]
    [SerializeField] private bool autoAddSelectVisuals = true;

    [Header("Navigation")]
    [Range(0f, 1f)] public float navDeadzone = 0.2f;
    public bool enableManualNavigation = true; // enable fallback navigation polling
    private int _navHeldSign; // -1,0,1

    public bool IsVisible => root != null && root.activeInHierarchy;

    void Awake()
    {
        if (!root)
            root = gameObject;

        _eventSystem = EventSystem.current;

        // In this menu we use the shared cancel/input mapping (B) instead of a dedicated back button.

        if (spanishButton)
            spanishButton.onClick.AddListener(() => SetLanguage("es"));
        if (englishButton)
            englishButton.onClick.AddListener(() => SetLanguage("en"));

        if (masterVolumeSlider)
            masterVolumeSlider.onValueChanged.AddListener(PlayerSettings.SetMasterVolume);
        if (sfxVolumeSlider)
            sfxVolumeSlider.onValueChanged.AddListener(PlayerSettings.SetSfxVolume);
        if (musicVolumeSlider)
            musicVolumeSlider.onValueChanged.AddListener(PlayerSettings.SetMusicVolume);

        if (invertLookToggle)
            invertLookToggle.onValueChanged.AddListener(PlayerSettings.SetInvertLook);
        if (invertFlightToggle)
            invertFlightToggle.onValueChanged.AddListener(PlayerSettings.SetInvertFlightLook);
        if (lookSensitivitySlider)
            lookSensitivitySlider.onValueChanged.AddListener(PlayerSettings.SetLookSensitivity);
        if (vibrationToggle)
            vibrationToggle.onValueChanged.AddListener(PlayerSettings.SetVibration);
        if (fullscreenToggle)
            fullscreenToggle.onValueChanged.AddListener(PlayerSettings.SetFullscreen);

        RefreshUI();

        // Ensure select visuals exist on selectable controls so navigation shows DOTween highlight/pulse
        if (autoAddSelectVisuals && root != null)
        {
            var selects = root.GetComponentsInChildren<Selectable>(true);
            foreach (var s in selects)
            {
                if (s == null) continue;
                var go = s.gameObject;
                if (!go.GetComponent<UISelectVisual>())
                {
                    var v = go.AddComponent<UISelectVisual>();
                    v.normalColor = Color.white;
                    v.highlightColor = new Color(1f, 0.92f, 0.16f);
                    v.selectedScale = 1.06f;
                    v.animDuration = 0.12f;
                    v.enablePulse = true;
                    v.enableShadowPunch = true;
                }
            }
        }
    }

    void OnEnable()
    {
        RefreshUI();
    }

    void Update()
    {
        if (!enableManualNavigation) return;
        if (root == null || !root.activeInHierarchy) return;

        if (WasCancelPressedThisFrame())
        {
            Close();
            return;
        }

        // Read input from common sources and move selection accordingly
        float vert = 0f;
#if ENABLE_INPUT_SYSTEM
        try
        {
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null)
            {
                var d = gp.dpad.ReadValue();
                if (Mathf.Abs(d.y) > navDeadzone) vert = d.y;
                else
                {
                    var s = gp.leftStick.ReadValue();
                    if (Mathf.Abs(s.y) > navDeadzone) vert = s.y;
                }
            }
            else
            {
                var js = UnityEngine.InputSystem.Joystick.current;
                if (js != null)
                {
                    var s = js.stick.ReadValue();
                    if (Mathf.Abs(s.y) > navDeadzone) vert = s.y;
                }
            }
        }
        catch { }
#else
        vert = Input.GetAxisRaw("Vertical");
#endif

        if (Mathf.Abs(vert) > navDeadzone)
        {
            int sign = vert > 0f ? 1 : -1;
            if (_navHeldSign != sign)
            {
                _navHeldSign = sign;
                // Use spatial navigation to pick the most sensible selectable
                if (sign > 0) MoveSelection(Vector2.up);
                else MoveSelection(Vector2.down);
            }
        }
        else
        {
            _navHeldSign = 0;
        }

        // Submit handling (basic)
        bool submit = false;
#if ENABLE_INPUT_SYSTEM
        var g = UnityEngine.InputSystem.Gamepad.current;
        if (g != null && g.buttonSouth.wasPressedThisFrame) submit = true;
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetButtonDown("Submit")) submit = true;
#endif

        if (submit)
        {
            var es = EventSystem.current;
            var go = es?.currentSelectedGameObject;
            if (go != null)
            {
                var btn = go.GetComponent<UnityEngine.UI.Button>();
                if (btn != null) btn.onClick.Invoke();
                else
                {
                    var tog = go.GetComponent<UnityEngine.UI.Toggle>();
                    if (tog != null)
                    {
                        tog.isOn = !tog.isOn;
                        tog.onValueChanged?.Invoke(tog.isOn);
                    }
                    else
                    {
                        // try to execute submit handler
                        ExecuteEvents.Execute(go, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
                    }
                }
            }
        }
    }

    void MoveSelection(Vector2 dir)
    {
        var es = EventSystem.current;
        if (es == null || root == null) return;

        var all = root.GetComponentsInChildren<Selectable>(true)
            .Where(s => s != null && s.IsActive() && s.interactable)
            .ToArray();

        if (all == null || all.Length == 0) return;

        var currentGO = es.currentSelectedGameObject;
        // If nothing selected, pick top-most (highest Y)
        if (currentGO == null)
        {
            var top = all.OrderByDescending(s => RectTransformUtility.WorldToScreenPoint(null, s.transform.position).y).FirstOrDefault();
            if (top != null)
            {
                es.SetSelectedGameObject(top.gameObject);
                top.Select();
            }
            return;
        }

        var sel = currentGO.GetComponent<Selectable>();
        Vector2 curPos = RectTransformUtility.WorldToScreenPoint(null, currentGO.transform.position);

        bool moveUp = dir == Vector2.up;

        // Candidates in desired vertical direction
        var candidates = all.Where(s =>
        {
            var p = RectTransformUtility.WorldToScreenPoint(null, s.transform.position);
            return moveUp ? p.y > curPos.y + 2f : p.y < curPos.y - 2f;
        }).ToArray();

        if (candidates.Length == 0)
        {
            // no candidate in that direction: do nothing
            return;
        }

        // Choose closest by vertical distance, then horizontal distance
        Selectable best = null;
        float bestScore = float.MaxValue;
        foreach (var c in candidates)
        {
            var p = RectTransformUtility.WorldToScreenPoint(null, c.transform.position);
            float dy = Mathf.Abs(p.y - curPos.y);
            float dx = Mathf.Abs(p.x - curPos.x);
            float score = dy * 1000f + dx; // prioritize vertical closeness
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        if (best != null)
        {
            es.SetSelectedGameObject(best.gameObject);
            best.Select();
        }
    }

    void OnDestroy()
    {
        // no dedicated backButton listener to remove (uses shared cancel mapping)
        if (spanishButton)
            spanishButton.onClick.RemoveAllListeners();
        if (englishButton)
            englishButton.onClick.RemoveAllListeners();

        if (masterVolumeSlider)
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
        if (sfxVolumeSlider)
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        if (musicVolumeSlider)
            musicVolumeSlider.onValueChanged.RemoveAllListeners();

        if (invertLookToggle)
            invertLookToggle.onValueChanged.RemoveAllListeners();
        if (invertFlightToggle)
            invertFlightToggle.onValueChanged.RemoveAllListeners();
        if (lookSensitivitySlider)
            lookSensitivitySlider.onValueChanged.RemoveAllListeners();
        if (vibrationToggle)
            vibrationToggle.onValueChanged.RemoveAllListeners();
        if (fullscreenToggle)
            fullscreenToggle.onValueChanged.RemoveAllListeners();
    }

    public void Show(GameObject initialSelection = null, Action onClosed = null)
    {
        _onClosed = onClosed;
        RefreshUI();
        if (root && !root.activeSelf)
            root.SetActive(true);

        SelectInitial(initialSelection);
    }

    public void Close()
    {
        if (root && root.activeSelf)
            root.SetActive(false);

        _onClosed?.Invoke();
        _onClosed = null;
    }

    public void ToggleVisibility()
    {
        if (root && root.activeSelf)
            Close();
        else
            Show();
    }

    public void SetLanguage(string locale)
    {
        PlayerSettings.SetLanguage(locale);
        UpdateLanguageButtons();
    }

    public void SetMasterVolume(float value) => PlayerSettings.SetMasterVolume(value);
    public void SetSfxVolume(float value) => PlayerSettings.SetSfxVolume(value);
    public void SetMusicVolume(float value) => PlayerSettings.SetMusicVolume(value);
    public void SetInvertLook(bool invert) => PlayerSettings.SetInvertLook(invert);
    public void SetInvertFlightLook(bool invert) => PlayerSettings.SetInvertFlightLook(invert);
    public void SetLookSensitivity(float value) => PlayerSettings.SetLookSensitivity(value);
    public void SetSubtitles(bool value) => PlayerSettings.SetSubtitles(value);
    public void SetVibration(bool value) => PlayerSettings.SetVibration(value);
    public void SetFullscreen(bool value) => PlayerSettings.SetFullscreen(value);

    private void RefreshUI()
    {
        PlayerSettings.EnsureLoaded();

        UpdateLanguageButtons();

        if (masterVolumeSlider)
            masterVolumeSlider.SetValueWithoutNotify(PlayerSettings.MasterVolume);
        if (sfxVolumeSlider)
            sfxVolumeSlider.SetValueWithoutNotify(PlayerSettings.SfxVolume);
        if (musicVolumeSlider)
            musicVolumeSlider.SetValueWithoutNotify(PlayerSettings.MusicVolume);

        if (invertLookToggle)
            invertLookToggle.SetIsOnWithoutNotify(PlayerSettings.InvertLook);
        if (invertFlightToggle)
            invertFlightToggle.SetIsOnWithoutNotify(PlayerSettings.InvertFlightLook);
        if (lookSensitivitySlider)
            lookSensitivitySlider.SetValueWithoutNotify(PlayerSettings.LookSensitivity);
        if (vibrationToggle)
            vibrationToggle.SetIsOnWithoutNotify(PlayerSettings.Vibration);
        if (fullscreenToggle)
            fullscreenToggle.SetIsOnWithoutNotify(PlayerSettings.Fullscreen);
    }

    private void UpdateLanguageButtons()
    {
        string current = PlayerSettings.Language.ToLowerInvariant();
        if (spanishButton)
            spanishButton.interactable = current != "es";
        if (englishButton)
            englishButton.interactable = current != "en";
    }

    private void SelectInitial(GameObject initialSelection)
    {
        if (_eventSystem == null)
            _eventSystem = EventSystem.current;

        var target = initialSelection != null ? initialSelection : firstSelection ? firstSelection.gameObject : null;
        if (_eventSystem != null && target != null)
            _eventSystem.SetSelectedGameObject(target);
    }

    bool WasCancelPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame))
                return true;

            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
                return true;
        }
        catch { }
#endif

        return Input.GetKeyDown(KeyCode.Escape)
            || Input.GetKeyDown(KeyCode.Backspace)
            || Input.GetKeyDown(KeyCode.JoystickButton1)
            || Input.GetKeyDown(KeyCode.JoystickButton7);
    }
}
