using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingsMenuController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Selectable firstSelection;
    [SerializeField] private Button backButton;

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
    [SerializeField] private Toggle subtitlesToggle;
    [SerializeField] private Toggle vibrationToggle;
    [SerializeField] private Toggle fullscreenToggle;

    private Action _onClosed;
    private EventSystem _eventSystem;

    [Header("Navigation")]
    [Min(0f)] public float navRepeatDelay = 0.15f;
    [Range(0f,1f)] public float navDeadzone = 0.3f;
    private float _navCooldown;
    private int _navHeldSign; // -1,0,1
    public bool enableManualNavigation = true; // enable fallback navigation polling

    void Awake()
    {
        if (!root)
            root = gameObject;

        _eventSystem = EventSystem.current;

        if (backButton)
            backButton.onClick.AddListener(Close);

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
        if (subtitlesToggle)
            subtitlesToggle.onValueChanged.AddListener(PlayerSettings.SetSubtitles);
        if (vibrationToggle)
            vibrationToggle.onValueChanged.AddListener(PlayerSettings.SetVibration);
        if (fullscreenToggle)
            fullscreenToggle.onValueChanged.AddListener(PlayerSettings.SetFullscreen);

        RefreshUI();
    }

    void OnEnable()
    {
        RefreshUI();
    }

    void Update()
    {
        if (!enableManualNavigation) return;
        if (root == null || !root.activeInHierarchy) return;

        // Reduce cooldown timer
        if (_navCooldown > 0f) _navCooldown -= Time.unscaledDeltaTime;
        else _navHeldSign = 0;

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

        if (Mathf.Abs(vert) > navDeadzone && _navCooldown <= 0f)
        {
            int sign = vert > 0f ? 1 : -1;
            if (_navHeldSign != sign)
            {
                _navHeldSign = sign;
                if (sign > 0) MoveSelection(Vector2.up);
                else MoveSelection(Vector2.down);
                _navCooldown = navRepeatDelay;
            }
        }

        // Submit handling (basic)
        bool submit = false;
#if ENABLE_INPUT_SYSTEM
        var g = UnityEngine.InputSystem.Gamepad.current;
        if (g != null && g.buttonSouth.wasPressedThisFrame) submit = true;
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) submit = true;
#endif

        if (submit)
        {
            var es = EventSystem.current;
            var go = es?.currentSelectedGameObject;
            if (go != null)
            {
                var sel = go.GetComponent<UnityEngine.UI.Button>();
                if (sel != null) sel.onClick.Invoke();
                else
                {
                    // try to execute submit handler
                    ExecuteEvents.Execute(go, new UnityEngine.EventSystems.BaseEventData(es), UnityEngine.EventSystems.ExecuteEvents.submitHandler);
                }
            }
        }
    }

    void MoveSelection(Vector2 dir)
    {
        var es = EventSystem.current;
        if (es == null) return;
        var current = es.currentSelectedGameObject;
        var sel = current ? current.GetComponent<Selectable>() : null;
        Selectable next = null;
        if (sel == null)
        {
            // pick first selectable in this root
            var all = root.GetComponentsInChildren<Selectable>(true);
            if (all != null && all.Length > 0) next = all[0];
        }
        else
        {
            if (dir == Vector2.up) next = sel.FindSelectableOnUp();
            else if (dir == Vector2.down) next = sel.FindSelectableOnDown();
        }

        if (next != null)
        {
            es.SetSelectedGameObject(next.gameObject);
            next.Select();
        }
    }

    void OnDestroy()
    {
        if (backButton)
            backButton.onClick.RemoveListener(Close);

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
        if (subtitlesToggle)
            subtitlesToggle.onValueChanged.RemoveAllListeners();
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
        if (subtitlesToggle)
            subtitlesToggle.SetIsOnWithoutNotify(PlayerSettings.Subtitles);
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
}
