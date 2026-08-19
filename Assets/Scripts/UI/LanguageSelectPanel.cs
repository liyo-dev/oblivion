using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Selector de idioma de primer arranque. MainMenuController lo muestra una única vez, antes de
/// dejar ver el menú, mientras PlayerSettings.LanguageSelected siga en false (ver
/// PlayerSettings.MarkLanguageSelected y MainMenuController.ShowLanguageSelectFirst).
///
/// A propósito no depende de LocalizationManager: cada botón debe llevar ya su propio texto
/// escrito a mano en su idioma ("Español", "English"...) en el Editor, para que sea legible
/// antes de que exista ningún idioma elegido. Y a diferencia de sus hermanos de menú
/// (SettingsMenuController/ControlsMenuController), no se puede cerrar con Start/Cancel: el
/// jugador tiene que elegir un idioma para continuar, igual que en el selector de idioma de
/// arranque de una consola.
///
/// El CanvasGroup propio con ignoreParentGroups = true garantiza que el panel siga siendo
/// pulsable aunque MainMenuController suspenda la interacción de rootGroup mientras lo enseña
/// (SuspendMainMenuInteraction), sin importar dónde se coloque este panel en la jerarquía.
/// </summary>
[DisallowMultipleComponent]
public class LanguageSelectPanel : MonoBehaviour
{
    [Serializable]
    private class LanguageOption
    {
        [Tooltip("Locale tal y como lo esperan LocalizationManager/PlayerSettings (\"es\", \"en\"...).")]
        public string locale;

        [Tooltip("Botón ya rotulado a mano en su propio idioma (\"Español\", \"English\"...).")]
        public Button button;
    }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Idiomas")]
    [Tooltip("Un botón por idioma disponible. Añadir aquí para soportar más idiomas en el futuro sin tocar código.")]
    [SerializeField] private LanguageOption[] languageOptions;

    private Action _onClosed;
    private EventSystem _eventSystem;
    private bool _picking;

    public bool IsVisible => root != null && root.activeInHierarchy;

    void Awake()
    {
        if (!root)
            root = gameObject;

        if (!canvasGroup)
            canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        canvasGroup.ignoreParentGroups = true;

        _eventSystem = EventSystem.current;

        if (languageOptions == null)
            return;

        foreach (var option in languageOptions)
        {
            if (option?.button == null || string.IsNullOrWhiteSpace(option.locale))
                continue;

            // Capturar en variable local: el locale de cada opción debe quedar fijo por
            // closure, no leerse del array (que podría reordenarse) en el momento del click.
            string locale = option.locale;
            var button = option.button;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnLanguagePicked(locale));

            if (!button.GetComponent<UISelectVisual>())
            {
                var v = button.gameObject.AddComponent<UISelectVisual>();
                v.normalColor = Color.white;
                v.highlightColor = new Color(0.95f, 0.9f, 0.7f);
                v.selectedScale = 1.08f;
                v.animDuration = 0.12f;
                v.enablePulse = true;
                v.enableShadowPunch = true;
            }

            if (!button.GetComponent<UIButtonAudio>())
                button.gameObject.AddComponent<UIButtonAudio>();
        }
    }

    /// <param name="onClosed">Se invoca justo después de que el jugador elija un idioma.</param>
    public void Show(Action onClosed = null)
    {
        _onClosed = onClosed;
        _picking = false;

        if (root && !root.activeSelf)
            root.SetActive(true);

        if (canvasGroup)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        SelectFirstAvailable();
    }

    /// <param name="silent">Si es false, reproduce el SFX de cierre habitual (UI_Cancel). El
    /// cierre normal de este panel es siempre una elección, no una cancelación, así que
    /// OnLanguagePicked llama a Close(silent: true) — el SFX de confirmación ya se reprodujo
    /// aparte. El valor por defecto (true) también evita ruido al ocultarlo al arrancar
    /// (MainMenuController.Awake llama a Close(silent: true) igual que con Ajustes/Controles).</param>
    public void Close(bool silent = true)
    {
        _picking = false;

        if (canvasGroup)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        bool wasVisible = root && root.activeSelf;

        if (!silent && wasVisible)
            AudioService.Instance?.PlaySFX("UI_Cancel", 1f);

        if (wasVisible)
            root.SetActive(false);

        _onClosed?.Invoke();
        _onClosed = null;
    }

    void OnLanguagePicked(string locale)
    {
        // Guard contra doble clic/doble Submit mientras cerramos (mismo espíritu que el flag
        // _isLoading de MainMenuController).
        if (_picking)
            return;
        _picking = true;

        AudioService.Instance?.PlaySFX("UI_Submit");

        PlayerSettings.SetLanguage(locale);
        PlayerSettings.MarkLanguageSelected();

        Close(silent: true);
    }

    void SelectFirstAvailable()
    {
        if (!_eventSystem)
            _eventSystem = EventSystem.current;
        if (_eventSystem == null || languageOptions == null)
            return;

        var first = languageOptions
            .Select(o => o?.button)
            .FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.interactable);

        if (first != null)
            _eventSystem.SetSelectedGameObject(first.gameObject);
    }
}
