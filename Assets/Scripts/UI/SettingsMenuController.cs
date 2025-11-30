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
    [Range(0f, 1f)] public float navDeadzone = 0.2f;
    public bool enableManualNavigation = true; // enable fallback navigation polling
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar una orden de cierre.")]
    private float cancelInputGracePeriod = 0.25f;
    private Vector2Int _navHeldDir = Vector2Int.zero; // normalized cardinal dir of held input
    private bool _editingSlider;
    private Slider _activeSlider;
    private float _sliderAdjustCooldown;
    private const float SLIDER_REPEAT_DELAY = 0.16f;
    private Vector2 _navInputEvent;
    private bool _navFromContinuous;
    private float _navEventExpiry;
    private bool _submitRequested;
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

        ConfigureNavigationLinks();
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
        if (!enableManualNavigation) return;
        if (root == null || !root.activeInHierarchy) return;

        if (WasCancelPressedThisFrame())
        {
            if (_editingSlider)
                EndSliderEdit();
            else
                Close();
            return;
        }

        _sliderAdjustCooldown -= Time.unscaledDeltaTime;

        if (!_navFromContinuous && _navInputEvent != Vector2.zero && Time.unscaledTime > _navEventExpiry)
            _navInputEvent = Vector2.zero;

        // Read input from common sources and move selection accordingly
        Vector2 navInput = _navInputEvent;
        if (navInput == Vector2.zero)
            navInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        if (navInput.magnitude > navDeadzone)
        {
            // choose dominant axis (cardinal navigation)
            Vector2 dir = Mathf.Abs(navInput.x) > Mathf.Abs(navInput.y)
                ? new Vector2(Mathf.Sign(navInput.x), 0f)
                : new Vector2(0f, Mathf.Sign(navInput.y));

            var cardinal = new Vector2Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.y));
            if (_editingSlider)
            {
                if (cardinal.x != 0 && _sliderAdjustCooldown <= 0f)
                {
                    AdjustActiveSlider(cardinal.x);
                    _sliderAdjustCooldown = SLIDER_REPEAT_DELAY;
                }
            }
            else if (cardinal != Vector2Int.zero && cardinal != _navHeldDir)
            {
                _navHeldDir = cardinal;
                MoveSelection(dir);
            }
        }
        else
        {
            _navHeldDir = Vector2Int.zero;
            if (!_editingSlider)
                _sliderAdjustCooldown = 0f;
        }

        // Submit handling (basic)
        bool submit = _submitRequested;
        _submitRequested = false;

        if (!submit && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
            submit = true;

        if (submit)
        {
            var es = EventSystem.current;
            var go = es?.currentSelectedGameObject;
            if (_editingSlider)
            {
                EndSliderEdit();
            }
            else if (go != null)
            {
                var slider = go.GetComponent<UnityEngine.UI.Slider>();
                if (slider != null)
                {
                    BeginSliderEdit(slider);
                }
                else
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
    }

    void MoveSelection(Vector2 dir)
    {
        var es = EventSystem.current;
        if (es == null || root == null) return;

        if (dir == Vector2.zero) return;

        var dirNorm = dir.normalized;

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

        // Choose candidate in direction, prioritizing alignment then distance
        Selectable best = null;
        float bestScore = float.MaxValue;
        foreach (var c in all)
        {
            if (c == sel) continue;

            var p = RectTransformUtility.WorldToScreenPoint(null, c.transform.position);
            var delta = p - curPos;
            if (delta.sqrMagnitude < 1e-4f) continue;

            var deltaNorm = delta.normalized;
            float alignment = Vector2.Dot(deltaNorm, dirNorm);
            if (alignment <= 0.3f) // discard options too far off desired direction
                continue;

            float distance = delta.magnitude;
            float anglePenalty = 1f - alignment; // 0 when perfectly aligned
            float score = anglePenalty * 1000f + distance; // prefer alignment, then closeness

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

    void ConfigureNavigationLinks()
    {
        // Alinear la navegación vertical en el bloque de idiomas/audio
        if (spanishButton && masterVolumeSlider)
        {
            var nav = spanishButton.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnDown = masterVolumeSlider;
            spanishButton.navigation = nav;
        }

        if (englishButton && masterVolumeSlider)
        {
            var nav = englishButton.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnDown = masterVolumeSlider;
            englishButton.navigation = nav;
        }

        if (masterVolumeSlider)
        {
            var nav = masterVolumeSlider.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = spanishButton ? spanishButton : englishButton;
            nav.selectOnDown = sfxVolumeSlider;
            masterVolumeSlider.navigation = nav;
        }

        if (sfxVolumeSlider)
        {
            var nav = sfxVolumeSlider.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = masterVolumeSlider;
            nav.selectOnDown = musicVolumeSlider;
            sfxVolumeSlider.navigation = nav;
        }

        if (musicVolumeSlider)
        {
            var nav = musicVolumeSlider.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = sfxVolumeSlider;
            musicVolumeSlider.navigation = nav;
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
        switch (input.Type)
        {
            case GamepadInputReader.InputEventType.Navigate:
                if (input.Phase == InputActionPhase.Canceled)
                {
                    _navInputEvent = Vector2.zero;
                    _navFromContinuous = false;
                }
                else
                {
                    _navInputEvent = input.Value;
                    _navFromContinuous = true;
                }
                break;

            case GamepadInputReader.InputEventType.DpadUp:
            case GamepadInputReader.InputEventType.DpadDown:
            case GamepadInputReader.InputEventType.DpadLeft:
            case GamepadInputReader.InputEventType.DpadRight:
                if (input.Phase == InputActionPhase.Performed)
                {
                    _navInputEvent = input.Value;
                    _navFromContinuous = false;
                    _navEventExpiry = Time.unscaledTime + 0.1f;
                }
                break;

            case GamepadInputReader.InputEventType.Submit when input.Phase == InputActionPhase.Performed:
                _submitRequested = true;
                break;

            case GamepadInputReader.InputEventType.Cancel when input.Phase == InputActionPhase.Performed:
            case GamepadInputReader.InputEventType.Start when input.Phase == InputActionPhase.Performed:
                _cancelRequested = true;
                break;
        }
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

    void BeginSliderEdit(Slider slider)
    {
        if (!slider) return;
        _editingSlider = true;
        _activeSlider = slider;
        _navHeldDir = Vector2Int.zero;
        _sliderAdjustCooldown = 0f;
    }

    void EndSliderEdit()
    {
        _editingSlider = false;
        _activeSlider = null;
        _navHeldDir = Vector2Int.zero;
        _sliderAdjustCooldown = 0f;
    }

    void AdjustActiveSlider(int direction)
    {
        if (!_editingSlider || _activeSlider == null) return;

        float step = _activeSlider.wholeNumbers
            ? 1f
            : Mathf.Max(0.01f, (_activeSlider.maxValue - _activeSlider.minValue) * 0.05f);

        float next = Mathf.Clamp(
            _activeSlider.value + (direction * step),
            _activeSlider.minValue,
            _activeSlider.maxValue);

        _activeSlider.value = next;
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
