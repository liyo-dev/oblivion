using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
    [SerializeField] private Button invertLookYesButton;
    [SerializeField] private Button invertLookNoButton;
    [SerializeField] private Button invertFlightYesButton;
    [SerializeField] private Button invertFlightNoButton;
    [SerializeField] private Slider lookSensitivitySlider;

    [Header("Accesibilidad / General")]
    [SerializeField] private Button vibrationYesButton;
    [SerializeField] private Button vibrationNoButton;
    [SerializeField] private Button fullscreenYesButton;
    [SerializeField] private Button fullscreenNoButton;

    private Action _onClosed;
    private EventSystem _eventSystem;

    [Header("State visuals")]
    [SerializeField] private Color activeStateColor = new Color(1f, 0.92f, 0.16f);
    [SerializeField] private Color inactiveStateColor = Color.white;

    [Header("Visuals")]
    [Tooltip("Auto-add UISelectVisual to selectables under this root to show navigation feedback.")]
    [SerializeField] private bool autoAddSelectVisuals = true;

    [Header("Navigation")]
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar una orden de cierre.")]
    private float cancelInputGracePeriod = 0.25f;
    private bool _cancelRequested;
    private float _openedAt = -999f;

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

        WireBinaryButton(invertLookYesButton, () => OnInvertLookClicked(true));
        WireBinaryButton(invertLookNoButton, () => OnInvertLookClicked(false));
        WireBinaryButton(invertFlightYesButton, () => OnInvertFlightClicked(true));
        WireBinaryButton(invertFlightNoButton, () => OnInvertFlightClicked(false));
        if (lookSensitivitySlider)
            lookSensitivitySlider.onValueChanged.AddListener(PlayerSettings.SetLookSensitivity);
        WireBinaryButton(vibrationYesButton, () => OnVibrationClicked(true));
        WireBinaryButton(vibrationNoButton, () => OnVibrationClicked(false));
        WireBinaryButton(fullscreenYesButton, () => OnFullscreenClicked(true));
        WireBinaryButton(fullscreenNoButton, () => OnFullscreenClicked(false));

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

        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    void Update()
    {
        if (root == null || !root.activeInHierarchy) return;

        if (WasCancelPressedThisFrame())
            Close();
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

        RemoveBinaryListener(invertLookYesButton);
        RemoveBinaryListener(invertLookNoButton);
        RemoveBinaryListener(invertFlightYesButton);
        RemoveBinaryListener(invertFlightNoButton);
        if (lookSensitivitySlider)
            lookSensitivitySlider.onValueChanged.RemoveAllListeners();
        RemoveBinaryListener(vibrationYesButton);
        RemoveBinaryListener(vibrationNoButton);
        RemoveBinaryListener(fullscreenYesButton);
        RemoveBinaryListener(fullscreenNoButton);
    }

    public void Show(GameObject initialSelection = null, Action onClosed = null)
    {
        _onClosed = onClosed;
        RefreshUI();
        if (root && !root.activeSelf)
            root.SetActive(true);

        SelectInitial(initialSelection);
        _cancelRequested = false;
        _openedAt = Time.unscaledTime;
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

        UpdateBinaryGroup(invertLookYesButton, invertLookNoButton, PlayerSettings.InvertLook);
        UpdateBinaryGroup(invertFlightYesButton, invertFlightNoButton, PlayerSettings.InvertFlightLook);
        if (lookSensitivitySlider)
            lookSensitivitySlider.SetValueWithoutNotify(PlayerSettings.LookSensitivity);
        UpdateBinaryGroup(vibrationYesButton, vibrationNoButton, PlayerSettings.Vibration);
        UpdateBinaryGroup(fullscreenYesButton, fullscreenNoButton, PlayerSettings.Fullscreen);
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

        var target = ResolveInitialSelection(initialSelection);
        if (_eventSystem != null && target != null)
            _eventSystem.SetSelectedGameObject(target);
    }

    public GameObject GetDefaultSelection() => ResolveInitialSelection(null);

    bool WasCancelPressedThisFrame()
    {
        if (Time.unscaledTime - _openedAt < cancelInputGracePeriod)
            return false;

        bool cancel = _cancelRequested;
        _cancelRequested = false;

        return cancel
            || Input.GetKeyDown(KeyCode.Escape)
            || Input.GetKeyDown(KeyCode.Backspace)
            || Input.GetKeyDown(KeyCode.JoystickButton1)
            || Input.GetKeyDown(KeyCode.JoystickButton7);
    }

    void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (input.Phase != InputActionPhase.Performed)
            return;

        if (input.Type == GamepadInputReader.InputEventType.Cancel || input.Type == GamepadInputReader.InputEventType.Start)
            _cancelRequested = true;
    }

    GameObject ResolveInitialSelection(GameObject requested)
    {
        if (requested != null)
            return requested;

        if (firstSelection)
            return firstSelection.gameObject;

        var firstSelectable = root != null
            ? root.GetComponentsInChildren<Selectable>(true).FirstOrDefault(s => s != null && s.IsActive() && s.interactable)
            : null;

        return firstSelectable ? firstSelectable.gameObject : null;
    }

    void WireBinaryButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (!btn) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);
    }

    void RemoveBinaryListener(Button btn)
    {
        if (!btn) return;
        btn.onClick.RemoveAllListeners();
    }

    void OnInvertLookClicked(bool invert)
    {
        PlayerSettings.SetInvertLook(invert);
        UpdateBinaryGroup(invertLookYesButton, invertLookNoButton, invert);
    }

    void OnInvertFlightClicked(bool invert)
    {
        PlayerSettings.SetInvertFlightLook(invert);
        UpdateBinaryGroup(invertFlightYesButton, invertFlightNoButton, invert);
    }

    void OnVibrationClicked(bool enabled)
    {
        PlayerSettings.SetVibration(enabled);
        UpdateBinaryGroup(vibrationYesButton, vibrationNoButton, enabled);
    }

    void OnFullscreenClicked(bool enabled)
    {
        PlayerSettings.SetFullscreen(enabled);
        UpdateBinaryGroup(fullscreenYesButton, fullscreenNoButton, enabled);
    }

    void UpdateBinaryGroup(Button yesButton, Button noButton, bool yesActive)
    {
        ApplyStateToButton(yesButton, yesActive);
        ApplyStateToButton(noButton, !yesActive);
    }

    void ApplyStateToButton(Button btn, bool active)
    {
        if (!btn)
            return;

        var graphic = btn.targetGraphic ?? btn.GetComponentInChildren<Graphic>();
        var color = active ? activeStateColor : inactiveStateColor;

        var visual = btn.GetComponent<UISelectVisual>();
        if (visual)
        {
            visual.normalColor = color;
            visual.highlightColor = activeStateColor;
            if (visual.targetGraphic)
                visual.targetGraphic.color = color;
        }
        else if (graphic)
        {
            graphic.color = color;
        }
    }
}
