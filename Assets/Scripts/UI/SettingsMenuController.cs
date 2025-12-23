using System;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using UnityEngine.UI;
using Core;

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

    [Header("Slider input")]
    [SerializeField, Tooltip("Incremento aplicado al ajustar sliders con el gamepad.")]
    private float sliderStep = 0.05f;

    [Header("Visuals")]
    [Tooltip("Auto-add UISelectVisual to selectables under this root to show navigation feedback.")]
    [SerializeField] private bool autoAddSelectVisuals = true;

    [Header("Navigation")]
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar una orden de cierre.")]
    private float cancelInputGracePeriod = 0.25f;
    private bool _cancelRequested;
    private float _openedAt = -999f;
    private bool _sliderEditMode;
    private Slider _activeSlider;
    private bool _previousNavigationState = true;
    private bool _navigationOverridden;

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
            masterVolumeSlider.onValueChanged.AddListener(v => HandleSliderChanged(masterVolumeSlider, PlayerSettings.SetMasterVolume, v));
        if (sfxVolumeSlider)
            sfxVolumeSlider.onValueChanged.AddListener(v => HandleSliderChanged(sfxVolumeSlider, PlayerSettings.SetSfxVolume, v));
        if (musicVolumeSlider)
            musicVolumeSlider.onValueChanged.AddListener(v => HandleSliderChanged(musicVolumeSlider, PlayerSettings.SetMusicVolume, v));

        WireBinaryButton(invertLookYesButton, () => OnInvertLookClicked(true));
        WireBinaryButton(invertLookNoButton, () => OnInvertLookClicked(false));
        WireBinaryButton(invertFlightYesButton, () => OnInvertFlightClicked(true));
        WireBinaryButton(invertFlightNoButton, () => OnInvertFlightClicked(false));
        if (lookSensitivitySlider)
            lookSensitivitySlider.onValueChanged.AddListener(v => HandleSliderChanged(lookSensitivitySlider, PlayerSettings.SetLookSensitivity, v));
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
                
                // Add UIButtonAudio to buttons for click and hover sounds
                var btn = s as Button;
                if (btn != null && !go.GetComponent<UIButtonAudio>())
                {
                    go.AddComponent<UIButtonAudio>();
                }
                
                // Add UISliderAudio to sliders for select sounds
                var slider = s as Slider;
                if (slider != null && !go.GetComponent<UISliderAudio>())
                {
                    go.AddComponent<UISliderAudio>();
                }
            }
        }

    }

    void OnEnable()
    {
        RefreshUI();
        // EventSystem maneja toda la navegación automáticamente
    }

    void OnDisable()
    {
        RestoreNavigationEvents();
        _sliderEditMode = false;
        _activeSlider = null;
    }

    void Update()
    {
        if (root == null || !root.activeInHierarchy) return;

        // Detectar botones del gamepad usando GamepadInputReader centralizado
        bool submitPressed = GamepadInputReader.SubmitPressed;
        bool startPressed = GamepadInputReader.StartPressed;
        
        if (submitPressed)
        {
            TryToggleSliderEditMode();
        }

        // Start cierra el menú
        if (startPressed && Time.unscaledTime - _openedAt >= cancelInputGracePeriod)
        {
            if (!_sliderEditMode)
                Close();
        }

        if (_sliderEditMode)
        {
            MaintainSliderSelection();
            HandleSliderInputFromControls();
        }

        if (WasCancelPressedThisFrame())
        {
            if (!_sliderEditMode)
                Close();
        }
    }
    
    void HandleSliderInputFromControls()
    {
        if (_activeSlider == null) return;
        
        // Leer del gamepad usando GamepadInputReader centralizado
        // Move incluye el joystick izquierdo
        Vector2 nav = GamepadInputReader.Move;
        
        // DpadRaw lee solo el D-Pad (tiene prioridad sobre el joystick)
        var dpad = GamepadInputReader.DpadRaw;
        if (dpad.sqrMagnitude > 0.01f)
        {
            nav = dpad;
        }
        
        if (nav.sqrMagnitude <= 0.01f) return;
        
        float axis = Mathf.Abs(nav.x) >= Mathf.Abs(nav.y) ? nav.x : nav.y;
        if (Mathf.Abs(axis) < 0.1f) return;
        
        float step = _activeSlider.wholeNumbers
            ? Mathf.Max(1f, sliderStep * (_activeSlider.maxValue - _activeSlider.minValue))
            : Mathf.Max(0.001f, sliderStep * (_activeSlider.maxValue - _activeSlider.minValue));

        _activeSlider.value = Mathf.Clamp(
            _activeSlider.value + Mathf.Sign(axis) * step, 
            _activeSlider.minValue, 
            _activeSlider.maxValue
        );
    }
    
    bool TryToggleSliderEditMode()
    {
        if (_eventSystem == null)
            _eventSystem = EventSystem.current;

        var selected = _eventSystem != null ? _eventSystem.currentSelectedGameObject : null;
        if (!selected)
            return false;

        var slider = selected.GetComponent<Slider>();
        if (!IsEditableSlider(slider))
            return false;

        if (_sliderEditMode && _activeSlider == slider)
        {
            ExitSliderEditMode();
        }
        else
        {
            EnterSliderEditMode(slider);
        }

        return true;
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
        // Reproducir sonido de cancelar al cerrar
        if (AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX("UI_Cancel", 1f);
        }
        
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
        
        // Restaurar selección después de cambiar idioma
        // porque el botón presionado ahora está deshabilitado
        StartCoroutine(RestoreSelectionAfterLanguageChange());
    }
    
    System.Collections.IEnumerator RestoreSelectionAfterLanguageChange()
    {
        yield return null; // Esperar un frame
        
        // Buscar el primer botón interactable (el que NO está seleccionado)
        if (spanishButton && spanishButton.interactable)
        {
            spanishButton.Select();
            if (_eventSystem != null)
                _eventSystem.SetSelectedGameObject(spanishButton.gameObject);
        }
        else if (englishButton && englishButton.interactable)
        {
            englishButton.Select();
            if (_eventSystem != null)
                _eventSystem.SetSelectedGameObject(englishButton.gameObject);
        }
        else
        {
            // Fallback: seleccionar el primer elemento interactable del menú
            SelectInitial(null);
        }
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

        if (cancel)
        {
            if (_sliderEditMode)
            {
                ExitSliderEditMode();
                return false;
            }

            return true;
        }

        // Leer usando GamepadInputReader centralizado
        if (GamepadInputReader.CancelPressed)
        {
            if (_sliderEditMode)
            {
                ExitSliderEditMode();
                return false;
            }
            return true;
        }

        return false;
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

    bool IsEditableSlider(Slider slider)
    {
        return slider != null && (slider == masterVolumeSlider || slider == sfxVolumeSlider || slider == musicVolumeSlider || slider == lookSensitivitySlider);
    }

    void EnterSliderEditMode(Slider slider)
    {
        if (!_eventSystem)
            _eventSystem = EventSystem.current;

        if (_eventSystem != null)
        {
            _previousNavigationState = _eventSystem.sendNavigationEvents;
            _navigationOverridden = true;
            _eventSystem.sendNavigationEvents = false;
        }

        _sliderEditMode = true;
        _activeSlider = slider;
        MaintainSliderSelection();
    }

    void ExitSliderEditMode()
    {
        RestoreNavigationEvents();
        _sliderEditMode = false;
        _activeSlider = null;
    }

    void RestoreNavigationEvents()
    {
        if (!_navigationOverridden)
            return;

        if (!_eventSystem)
            _eventSystem = EventSystem.current;

        if (_eventSystem != null)
            _eventSystem.sendNavigationEvents = _previousNavigationState;

        _navigationOverridden = false;
    }

    void MaintainSliderSelection()
    {
        if (!_eventSystem)
            _eventSystem = EventSystem.current;

        if (_eventSystem == null || _activeSlider == null)
        {
            ExitSliderEditMode();
            return;
        }

        if (_eventSystem.currentSelectedGameObject != _activeSlider.gameObject)
            _eventSystem.SetSelectedGameObject(_activeSlider.gameObject);
    }


    void HandleSliderChanged(Slider slider, Action<float> setter, float value)
    {
        if (slider == null)
            return;

        if (_sliderEditMode && _activeSlider == slider)
        {
            setter?.Invoke(value);
            return;
        }

        slider.SetValueWithoutNotify(GetSettingValueForSlider(slider));
    }

    float GetSettingValueForSlider(Slider slider)
    {
        if (slider == masterVolumeSlider) return PlayerSettings.MasterVolume;
        if (slider == sfxVolumeSlider) return PlayerSettings.SfxVolume;
        if (slider == musicVolumeSlider) return PlayerSettings.MusicVolume;
        if (slider == lookSensitivitySlider) return PlayerSettings.LookSensitivity;
        return slider != null ? slider.value : 0f;
    }
}
