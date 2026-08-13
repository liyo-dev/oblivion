using System.Collections;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una fila de la pantalla de Controles: icono (si la entrada tiene glyphName), etiqueta corta de
/// tecla/botón y descripción. Se refresca sola cuando cambia la familia de dispositivo activa
/// (teclado ⇄ mando) — igual que Core.InputGlyphs.InputGlyphIcon — así que si el jugador cambia de
/// mando a teclado con la pantalla de Controles abierta, la fila cambia de icono/etiqueta sin
/// necesidad de cerrarla y reabrirla.
///
/// FIX: la descripción (y los overrides de etiqueta tipo "Ratón"/"Espacio"/"Stick derecho") se
/// pintaban con el literal del ScriptableObject, que está en español. Jugando en inglés la fila
/// salía a medias — "Left click / Hechizo — slot izquierdo" — porque la etiqueta de tecla sí pasa
/// por InputGlyphLabels (claves GLYPH_*) pero el texto del asset no pasaba por ningún sitio. Ahora
/// cada texto se resuelve contra LocalizationManager con la clave que trae la propia entrada
/// (descriptionKey/keyboardLabelKey/gamepadLabelKey) y el literal español queda solo de fallback.
/// También nos suscribimos a OnLocaleChanged para que cambiar de idioma con el panel abierto
/// repinte las filas (el panel construye sus filas UNA vez, ver ControlsMenuController.BuildRowsIfNeeded,
/// así que sin esto se quedarían con el idioma con el que se abrió la primera vez).
/// </summary>
[DisallowMultipleComponent]
public class ControlRowWidget : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private TMP_Text descriptionLabel;

    ControlsSchemeEntry _entry;
    Coroutine _waitForLocalization;
    bool _localeSubscribed;

    public void Bind(ControlsSchemeEntry entry)
    {
        _entry = entry;
        Refresh();
    }

    void OnEnable()
    {
        InputGlyphService.FamilyChanged += HandleFamilyChanged;
        SubscribeToLocale();
        Refresh();
    }

    void OnDisable()
    {
        InputGlyphService.FamilyChanged -= HandleFamilyChanged;
        UnsubscribeFromLocale();

        if (_waitForLocalization != null)
        {
            StopCoroutine(_waitForLocalization);
            _waitForLocalization = null;
        }
    }

    void OnDestroy() => UnsubscribeFromLocale();

    void HandleFamilyChanged(InputGlyphDeviceFamily _) => Refresh();

    // Mismo patrón que LocalizedText: si el manager todavía no ha hecho su Awake() no podemos
    // suscribirnos ni traducir, así que esperamos a que exista en vez de quedarnos para siempre
    // con el fallback en español.
    void SubscribeToLocale()
    {
        if (_localeSubscribed) return;

        if (LocalizationManager.Instance == null)
        {
            if (_waitForLocalization == null && isActiveAndEnabled)
                _waitForLocalization = StartCoroutine(WaitForLocalizationManager());
            return;
        }

        LocalizationManager.Instance.OnLocaleChanged += Refresh;
        _localeSubscribed = true;
    }

    void UnsubscribeFromLocale()
    {
        if (!_localeSubscribed) return;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLocaleChanged -= Refresh;
        _localeSubscribed = false;
    }

    IEnumerator WaitForLocalizationManager()
    {
        while (LocalizationManager.Instance == null)
            yield return null;

        _waitForLocalization = null;
        SubscribeToLocale();
        Refresh();
    }

    /// <summary>
    /// Texto de <paramref name="key"/> en el idioma activo, con <paramref name="fallback"/> (el
    /// literal en español del asset) si no hay clave, no hay manager o la clave no está en el
    /// catálogo del idioma cargado.
    /// </summary>
    static string Loc(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;

        var loc = LocalizationManager.Instance;
        if (loc == null) return fallback;

        var text = loc.Get(key, fallback);
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    void Refresh()
    {
        if (_entry == null) return;

        var family = InputGlyphService.CurrentFamily;
        bool kb = family == InputGlyphDeviceFamily.KeyboardMouse;

        if (descriptionLabel)
            descriptionLabel.text = Loc(_entry.descriptionKey, _entry.description);

        string overrideLabel = kb ? _entry.keyboardLabelOverride : _entry.gamepadLabelOverride;
        string overrideKey = kb ? _entry.keyboardLabelKey : _entry.gamepadLabelKey;

        if (!string.IsNullOrEmpty(_entry.glyphName))
        {
            if (icon)
            {
                var sprite = InputGlyphService.GetSprite(_entry.glyphName);
                icon.enabled = sprite != null;
                icon.sprite = sprite;
                icon.preserveAspect = true;
            }

            // Con override → se traduce con su clave; sin override → InputGlyphLabels ya devuelve
            // texto localizado por su cuenta.
            string label = string.IsNullOrEmpty(overrideLabel)
                ? InputGlyphLabels.GetLabel(_entry.glyphName, family)
                : Loc(overrideKey, overrideLabel);

            if (keyLabel) keyLabel.text = label;
        }
        else
        {
            // Fila solo de texto (sin arte de icono propio, p.ej. Cámara/ratón).
            if (icon) icon.enabled = false;
            if (keyLabel) keyLabel.text = Loc(overrideKey, overrideLabel);
        }
    }
}
