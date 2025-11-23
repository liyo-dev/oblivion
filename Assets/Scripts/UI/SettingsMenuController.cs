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

    [Header("Camera")]
    [SerializeField] private Toggle invertLookToggle;
    [SerializeField] private Toggle invertFlightToggle;

    private Action _onClosed;
    private EventSystem _eventSystem;

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

        if (invertLookToggle)
            invertLookToggle.onValueChanged.AddListener(PlayerSettings.SetInvertLook);
        if (invertFlightToggle)
            invertFlightToggle.onValueChanged.AddListener(PlayerSettings.SetInvertFlightLook);

        RefreshUI();
    }

    void OnEnable()
    {
        RefreshUI();
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

        if (invertLookToggle)
            invertLookToggle.onValueChanged.RemoveAllListeners();
        if (invertFlightToggle)
            invertFlightToggle.onValueChanged.RemoveAllListeners();
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
    public void SetInvertLook(bool invert) => PlayerSettings.SetInvertLook(invert);
    public void SetInvertFlightLook(bool invert) => PlayerSettings.SetInvertFlightLook(invert);

    private void RefreshUI()
    {
        PlayerSettings.EnsureLoaded();

        UpdateLanguageButtons();

        if (masterVolumeSlider)
            masterVolumeSlider.SetValueWithoutNotify(PlayerSettings.MasterVolume);
        if (sfxVolumeSlider)
            sfxVolumeSlider.SetValueWithoutNotify(PlayerSettings.SfxVolume);

        if (invertLookToggle)
            invertLookToggle.SetIsOnWithoutNotify(PlayerSettings.InvertLook);
        if (invertFlightToggle)
            invertFlightToggle.SetIsOnWithoutNotify(PlayerSettings.InvertFlightLook);
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
