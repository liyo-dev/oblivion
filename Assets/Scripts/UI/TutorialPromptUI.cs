using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Core.InputGlyphs;

/// <summary>
/// Línea de tutorial in-world: icono de botón opcional + texto. No bloquea la acción.
/// Vive en el Canvas persistente (Start.unity). Singleton.
/// </summary>
public class TutorialPromptUI : MonoBehaviour
{
    public static TutorialPromptUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    [Header("Referencias UI")]
    [SerializeField] CanvasGroup _rootGroup;
    [SerializeField] GameObject _iconContainer;
    [SerializeField] Image _icon;
    [SerializeField] TextMeshProUGUI _label;

    [Header("Animación")]
    [SerializeField] float _fadeInDuration = 0.3f;
    [SerializeField] float _fadeOutDuration = 0.25f;

    // Plantilla de texto activa (puede contener el token {BOTON}) y el nombre de glifo
    // (InputGlyphNames) usados para recalcular texto+icono en caliente. Antes Show() fijaba texto e
    // icono UNA sola vez y se quedaban obsoletos si el jugador cambiaba de mando/teclado con el
    // prompt ya visible (p.ej. soltar el mando y tocar una tecla a mitad de un "Pulsa A...").
    const string ButtonToken = "{BOTON}";
    string _textTemplate;
    string _buttonName;
    Sprite _fallbackIcon;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;
    }

    void OnEnable()
    {
        InputGlyphService.FamilyChanged += HandleFamilyChanged;
    }

    void OnDisable()
    {
        InputGlyphService.FamilyChanged -= HandleFamilyChanged;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            _rootGroup?.DOKill();
        }
    }

    void HandleFamilyChanged(InputGlyphDeviceFamily _)
    {
        // Solo recalcular si el prompt activo usa resolución dinámica (Show con buttonName) — el
        // Show(text, icon) "de toda la vida" deja _textTemplate a null y no debe tocarse aquí.
        if (_textTemplate != null) RefreshContent();
    }

    // ── API pública ───────────────────────────────────────────────────────

    public bool IsVisible => gameObject.activeSelf && _rootGroup.alpha > 0f;

    [ContextMenu("TEST Show")]
    void TestShow() => Show("Pulsa {BOTON} para despertar", InputGlyphNames.Confirm);

    [ContextMenu("TEST Hide")]
    void TestHide() => Hide();

    /// <summary>
    /// Prompt con texto e icono FIJOS, sin resolución dinámica por dispositivo. Mantenido por
    /// compatibilidad con llamadas que ya traen su propio sprite resuelto; para prompts que dependen
    /// del botón/tecla real activa (la inmensa mayoría) usar el overload con <c>buttonName</c>.
    /// </summary>
    public void Show(string text, Sprite icon = null)
    {
        _textTemplate = null;
        _buttonName = null;
        _fallbackIcon = null;

        _label.text = text;
        SetIcon(icon);
        FadeIn();
    }

    /// <summary>
    /// Prompt dinámico: <paramref name="textTemplate"/> puede contener el token literal "{BOTON}",
    /// que se sustituye por el nombre corto de la tecla/botón real (<see cref="InputGlyphLabels"/>)
    /// según el dispositivo activo; el icono se resuelve con <see cref="InputGlyphService.GetSprite"/>
    /// a partir de <paramref name="buttonName"/> (constantes de <see cref="InputGlyphNames"/>). Si
    /// <paramref name="textTemplate"/> no contiene el token, se muestra tal cual (solo cambia el
    /// icono). Ambos se recalculan solos si el jugador cambia de mando/teclado con el prompt visible.
    /// <paramref name="fallbackIcon"/> se usa únicamente si no hay sprite resuelto para la familia
    /// activa (p.ej. mientras no exista arte de teclado todavía para ese botón concreto).
    /// </summary>
    public void Show(string textTemplate, string buttonName, Sprite fallbackIcon = null)
    {
        _textTemplate = textTemplate;
        _buttonName = buttonName;
        _fallbackIcon = fallbackIcon;

        RefreshContent();
        FadeIn();
    }

    public void Hide()
    {
        _rootGroup.DOKill();
        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
    }

    // ── Interno ───────────────────────────────────────────────────────────

    void RefreshContent()
    {
        _label.text = ResolveText();

        Sprite resolvedIcon = _fallbackIcon;
        if (!string.IsNullOrEmpty(_buttonName))
        {
            var dynamicIcon = InputGlyphService.GetSprite(_buttonName);
            if (dynamicIcon != null) resolvedIcon = dynamicIcon;
        }
        SetIcon(resolvedIcon);
    }

    string ResolveText()
    {
        if (string.IsNullOrEmpty(_textTemplate)) return _textTemplate;
        if (string.IsNullOrEmpty(_buttonName) || !_textTemplate.Contains(ButtonToken)) return _textTemplate;

        string label = InputGlyphLabels.GetLabel(_buttonName, InputGlyphService.CurrentFamily);
        return _textTemplate.Replace(ButtonToken, label);
    }

    void SetIcon(Sprite icon)
    {
        if (_iconContainer != null) _iconContainer.SetActive(icon != null);
        if (_icon != null) _icon.sprite = icon;
    }

    void FadeIn()
    {
        _rootGroup.DOKill();
        _rootGroup.DOFade(1f, _fadeInDuration).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }
}
