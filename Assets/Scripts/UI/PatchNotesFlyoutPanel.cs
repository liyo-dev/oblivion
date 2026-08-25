using System.Collections;
using System.Text;
using Core;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Panel de "Notas del Parche" para el MainMenu: al pulsar NOTAS DEL PARCHE, se despliega un panel
/// lateral con scroll manual sin salir del menú — mismo patrón visual y de construcción que
/// CreditsFlyoutPanel (Assets/Scripts/UI/CreditsFlyoutPanel.cs), simplificado (sin auto-scroll tipo
/// crawl ni footer de "ver versión completa" — aquí no hay una escena separada a la que saltar).
///
/// Construye toda su UI por código en Awake, no toca MainMenu.unity ni el botón NOTAS DEL PARCHE
/// existente en el Editor: en Start() se localiza el botón por nombre/texto (o por
/// <see cref="patchNotesButtonOverride"/> si se asigna a mano) y se le reemplazan sus listeners en
/// runtime.
///
/// Setup en el Editor (una sola vez, en MainMenu.unity) — ya lo hace
/// MainMenuPatchNotesBugReportBuilder.cs, no hace falta montarlo a mano:
/// 1. GameObject vacío en la escena ("PatchNotesFlyoutPanel").
/// 2. Añadir este componente.
/// 3. (Opcional) Si el autodetect del botón no lo encuentra, arrastrarlo a
///    <see cref="patchNotesButtonOverride"/>.
///
/// Contenido de las notas (24 ago 2026 — reemplaza el campo de texto fijo que había antes; 25 ago
/// 2026 — a petición de Raúl, se quitó el histórico de versiones anteriores, ver nota en
/// PatchNotesBuildGuard.cs): el texto ya NO se edita en el Inspector ni va hardcodeado aquí. Se
/// compone en runtime, en <see cref="BuildPatchNotesText"/>, a partir de:
/// - La versión real del build (<see cref="Application.version"/>) y la fecha real del build
///   (Resources/PatchNotes/BuildDate.txt) — así la cabecera SIEMPRE coincide con lo que muestra
///   <c>VersionLabelUI</c> en pantalla, nunca puede quedarse desfasada.
/// - Assets/Resources/PatchNotes/CurrentEntryBullets.txt — los cambios de la build en curso (solo
///   los bullets, sin cabecera). Esto SÍ se sigue editando a mano durante el desarrollo, igual que
///   antes, solo que ahora es un archivo de texto plano en vez de un campo del Inspector.
/// El panel muestra únicamente esta entrada (la de la versión que se acaba de subir) — ya no hay
/// histórico de versiones anteriores. <c>PatchNotesBuildGuard</c> (Editor) resetea
/// CurrentEntryBullets.txt automáticamente después de cada build real, para la siguiente. Ese mismo
/// script CANCELA el build si CurrentEntryBullets.txt está vacío o sin rellenar, para que nunca
/// vuelva a salir una build con notas de parche en blanco o con texto de marcador de posición
/// visible para los jugadores.
///
/// Localización (24 ago 2026): el título ("NOTAS DEL PARCHE") sí usa <see cref="LocalizedText"/>
/// (clave PatchNotes_Title, ui_es.json/ui_en.json) y se traduce solo. El CONTENIDO (los archivos de
/// Resources/PatchNotes/) NO — sigue en un solo idioma, igual que antes.
/// </summary>
[DisallowMultipleComponent]
public class PatchNotesFlyoutPanel : MonoBehaviour
{
    [Header("Botón que abre el panel")]
    [Tooltip("Si se deja vacío, se busca en toda la escena un Button cuyo nombre o texto contenga 'patch' o 'parche'.")]
    [SerializeField] Button patchNotesButtonOverride;

    [SerializeField] TMP_FontAsset font;
    [Min(8f)] [SerializeField] float fontSize = 26f;

    [Header("Panel")]
    [Min(200f)] [SerializeField] float panelWidth = 560f;
    [SerializeField] Color panelColor = new Color(0.07f, 0.05f, 0.03f, 0.9f);
    [Tooltip("Dorado a juego con el resto de paneles del menú (mismo tono que CreditsFlyoutPanel).")]
    [SerializeField] Color accentColor = new Color(0.97f, 0.71f, 0.22f, 1f);
    [Min(0.05f)] [SerializeField] float animDuration = 0.32f;

    [Header("Navegación (mando)")]
    [Tooltip("Cerrar el panel con el mando usa el botón Cancelar real (East: B en Xbox, ○ en PlayStation, A en Switch) — el mismo que cierra Créditos/Ajustes/Controles.")]
    [SerializeField, Min(0f)] float cancelInputGracePeriod = 0.25f;

    RectTransform _panelRoot;
    CanvasGroup _panelGroup;
    RectTransform _content;
    ScrollRect _scrollRect;
    GameObject _outsideCatcher;
    Button _closeButton;
    Button _openButton;
    Tween _slideTween;
    bool _isOpen;
    float _hiddenX;
    float _openedAt = -999f;

    TMP_Text _closeLabel;
    Image _closeIcon;

    void Awake()
    {
        BuildUI();
    }

    void OnEnable()
    {
        InputGlyphService.FamilyChanged += HandleFamilyChanged;
        RefreshGlyphs();
    }

    void OnDisable()
    {
        InputGlyphService.FamilyChanged -= HandleFamilyChanged;
    }

    void Start()
    {
        // Mismo fix documentado en CreditsFlyoutPanel: el botón del menú puede seguir inactivo en el
        // primer frame (animación de entrada del ButtonPanel todavía no ha terminado), así que se
        // reintenta durante un margen en vez de buscar una sola vez.
        StartCoroutine(WirePatchNotesButtonWithRetry());
    }

    IEnumerator WirePatchNotesButtonWithRetry()
    {
        const float maxWaitSeconds = 2f;
        float deadline = Time.unscaledTime + maxWaitSeconds;

        while (Time.unscaledTime < deadline)
        {
            if (TryWirePatchNotesButton())
                yield break;

            yield return null;
        }

        if (!TryWirePatchNotesButton())
        {
            Debug.LogWarning("[PatchNotesFlyoutPanel] No se encontró el botón NOTAS DEL PARCHE automáticamente " +
                              $"tras reintentar durante {2f:0.#}s. Asigna 'Patch Notes Button Override' a mano en el Inspector.");
        }
    }

    void Update()
    {
        if (!_isOpen) return;
        if (Time.unscaledTime - _openedAt < cancelInputGracePeriod) return;

        if (GamepadInputReader.CancelPressed)
            ClosePanel();
    }

    void OnDestroy()
    {
        _slideTween?.Kill();
    }

    void HandleFamilyChanged(InputGlyphDeviceFamily _) => RefreshGlyphs();

    void RefreshGlyphs()
    {
        bool gamepad = InputGlyphService.CurrentFamily != InputGlyphDeviceFamily.KeyboardMouse;

        if (_closeLabel != null) _closeLabel.enabled = !gamepad;
        if (_closeIcon != null)
        {
            var sprite = gamepad ? InputGlyphService.GetSprite(InputGlyphNames.East) : null;
            _closeIcon.sprite = sprite;
            _closeIcon.enabled = sprite != null;
        }
    }

    // ── Conexión con el botón NOTAS DEL PARCHE ya existente en MainMenu.unity ───────

    bool TryWirePatchNotesButton()
    {
        if (_openButton != null)
            return true;

        _openButton = patchNotesButtonOverride != null ? patchNotesButtonOverride : FindPatchNotesButton();

        if (_openButton == null)
            return false;

        _openButton.onClick.RemoveAllListeners();
        _openButton.onClick.AddListener(TogglePanel);
        return true;
    }

    Button FindPatchNotesButton()
    {
        var all = FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (var b in all)
        {
            if (b.transform.IsChildOf(transform)) continue;

            if (Matches(b.gameObject.name)) return b;

            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && Matches(label.text)) return b;

            var legacyLabel = b.GetComponentInChildren<Text>(true);
            if (legacyLabel != null && Matches(legacyLabel.text)) return b;
        }
        return null;
    }

    static bool Matches(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var n = StripAccents(s.ToLowerInvariant());
        return n.Contains("patch") || n.Contains("parche");
    }

    static string StripAccents(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case 'á': sb.Append('a'); break;
                case 'é': sb.Append('e'); break;
                case 'í': sb.Append('i'); break;
                case 'ó': sb.Append('o'); break;
                case 'ú': sb.Append('u'); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // ── Abrir / cerrar ────────────────────────────────────────────────────

    public void TogglePanel()
    {
        if (_isOpen) ClosePanel();
        else OpenPanel();
    }

    public void OpenPanel()
    {
        if (_isOpen) return;
        _isOpen = true;
        _openedAt = Time.unscaledTime;

        AudioService.Instance?.PlaySFX("UI_Submit");

        _outsideCatcher.SetActive(true);
        _panelGroup.blocksRaycasts = true;
        _panelGroup.interactable = true;

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f; // empezar siempre desde arriba

        _slideTween?.Kill();
        _slideTween = _panelRoot.DOAnchorPosX(0f, animDuration).SetEase(Ease.OutCubic).SetUpdate(true);

        if (_closeButton != null)
            EventSystem.current?.SetSelectedGameObject(_closeButton.gameObject);
    }

    public void ClosePanel()
    {
        if (!_isOpen) return;
        _isOpen = false;

        AudioService.Instance?.PlaySFX("UI_Cancel", 1f);

        _panelGroup.blocksRaycasts = false;
        _panelGroup.interactable = false;

        _slideTween?.Kill();
        _slideTween = _panelRoot.DOAnchorPosX(_hiddenX, animDuration).SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => _outsideCatcher.SetActive(false));

        if (_openButton != null)
            EventSystem.current?.SetSelectedGameObject(_openButton.gameObject);
    }

    // ── Construcción de UI ────────────────────────────────────────────────

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500; // por delante del Canvas del MainMenu

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        BuildOutsideCatcher();
        BuildPanel();
    }

    void BuildOutsideCatcher()
    {
        _outsideCatcher = new GameObject("OutsideCatcher", typeof(RectTransform));
        _outsideCatcher.transform.SetParent(transform, false);
        StretchToParent((RectTransform)_outsideCatcher.transform);

        var img = _outsideCatcher.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f); // invisible, pero bloquea raycasts
        var btn = _outsideCatcher.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(ClosePanel);

        _outsideCatcher.SetActive(false);
    }

    void BuildPanel()
    {
        var panelGo = new GameObject("PatchNotesPanel", typeof(RectTransform));
        panelGo.transform.SetParent(transform, false);
        _panelRoot = (RectTransform)panelGo.transform;
        _panelRoot.anchorMin = new Vector2(1f, 0f);
        _panelRoot.anchorMax = new Vector2(1f, 1f);
        _panelRoot.pivot = new Vector2(1f, 0.5f);
        _panelRoot.sizeDelta = new Vector2(panelWidth, 0f);

        _hiddenX = panelWidth + 40f;
        _panelRoot.anchoredPosition = new Vector2(_hiddenX, 0f);

        _panelGroup = panelGo.AddComponent<CanvasGroup>();
        _panelGroup.alpha = 1f;
        _panelGroup.blocksRaycasts = false;
        _panelGroup.interactable = false;

        var bg = panelGo.AddComponent<Image>();
        bg.color = panelColor;

        var accentGo = new GameObject("AccentLine", typeof(RectTransform));
        accentGo.transform.SetParent(_panelRoot, false);
        var accentRt = (RectTransform)accentGo.transform;
        accentRt.anchorMin = new Vector2(0f, 0f);
        accentRt.anchorMax = new Vector2(0f, 1f);
        accentRt.pivot = new Vector2(0f, 0.5f);
        accentRt.sizeDelta = new Vector2(4f, 0f);
        accentRt.anchoredPosition = Vector2.zero;
        accentGo.AddComponent<Image>().color = accentColor;

        BuildHeader();
        BuildScrollView();
    }

    void BuildHeader()
    {
        var headerGo = new GameObject("Header", typeof(RectTransform));
        headerGo.transform.SetParent(_panelRoot, false);
        var headerRt = (RectTransform)headerGo.transform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.sizeDelta = new Vector2(-40f, 90f);
        headerRt.anchoredPosition = new Vector2(0f, -20f);

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(headerRt, false);
        var titleRt = (RectTransform)titleGo.transform;
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = new Vector2(-56f, 0f);
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(title, "NOTAS DEL PARCHE", fontSize * 1.3f);
        title.fontStyle = FontStyles.Bold;
        title.color = accentColor;
        title.alignment = TextAlignmentOptions.Left;
        titleGo.AddComponent<LocalizedText>().key = "PatchNotes_Title";

        var closeGo = new GameObject("CloseButton", typeof(RectTransform));
        closeGo.transform.SetParent(headerRt, false);
        var closeRt = (RectTransform)closeGo.transform;
        closeRt.anchorMin = new Vector2(1f, 0.5f);
        closeRt.anchorMax = new Vector2(1f, 0.5f);
        closeRt.pivot = new Vector2(1f, 0.5f);
        closeRt.sizeDelta = new Vector2(48f, 48f);
        closeRt.anchoredPosition = Vector2.zero;
        closeGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        _closeButton = closeGo.AddComponent<Button>();
        _closeButton.onClick.AddListener(ClosePanel);

        var closeLabelGo = new GameObject("Label", typeof(RectTransform));
        closeLabelGo.transform.SetParent(closeRt, false);
        StretchToParent((RectTransform)closeLabelGo.transform);
        var closeLabel = closeLabelGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(closeLabel, "X", fontSize);
        closeLabel.fontStyle = FontStyles.Bold;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.margin = Vector4.zero;
        closeLabel.raycastTarget = false;
        _closeLabel = closeLabel;
        // FIX (24 ago 2026 — "X" de cerrar descentrada, ver el mismo fix en CreditsFlyoutPanel.cs y
        // BugReportFlyoutPanel.cs): en vez de centrar por métricas de fuente (que dejaban el glifo
        // descuadrado según el carácter/fuente), se mide la tinta renderizada real del mesh de TMP y
        // se centra el RectTransform contra eso. Válido para cualquier fuente sin recalibrar a mano.
        CenterGlyphOnRenderedInk(closeLabel);

        var closeIconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        closeIconGo.transform.SetParent(closeRt, false);
        var closeIconRt = (RectTransform)closeIconGo.transform;
        closeIconRt.anchorMin = Vector2.zero;
        closeIconRt.anchorMax = Vector2.one;
        closeIconRt.offsetMin = new Vector2(8f, 8f);
        closeIconRt.offsetMax = new Vector2(-8f, -8f);
        _closeIcon = closeIconGo.GetComponent<Image>();
        _closeIcon.preserveAspect = true;
        _closeIcon.raycastTarget = false;
        _closeIcon.enabled = false;
    }

    void BuildScrollView()
    {
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
        scrollGo.transform.SetParent(_panelRoot, false);
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(28f, 28f);
        scrollRt.offsetMax = new Vector2(-28f, -110f); // deja hueco a la cabecera

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;
        _scrollRect = scrollRect;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollRt, false);
        var viewportRt = (RectTransform)viewportGo.transform;
        StretchToParent(viewportRt);
        viewportGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportRt, false);
        _content = (RectTransform)contentGo.transform;
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.sizeDelta = Vector2.zero;

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var textGo = new GameObject("PatchNotesText", typeof(RectTransform));
        textGo.transform.SetParent(_content, false);
        var label = textGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(label, BuildPatchNotesText(), fontSize);
        label.alignment = TextAlignmentOptions.TopLeft;

        scrollRect.viewport = viewportRt;
        scrollRect.content = _content;
    }

    // ── Composición del texto (versión + fecha reales + contenido en Resources/PatchNotes/) ──

    string BuildPatchNotesText()
    {
        string bullets = LoadResourceText("PatchNotes/CurrentEntryBullets").Trim();
        string dateText = LoadResourceText("PatchNotes/BuildDate").Trim();

        // Respaldo solo para probar en el Editor sin haber hecho nunca un build de Player todavía
        // (BuildDate.txt lo escribe PatchNotesBuildGuard en cada build real) — no debería usarse en
        // ningún build compilado de verdad.
        if (string.IsNullOrEmpty(dateText))
            dateText = PatchNotesDateFormatter.FormatSpanishDate(System.DateTime.Now);

        if (string.IsNullOrEmpty(bullets))
        {
            Debug.LogWarning("[PatchNotesFlyoutPanel] Resources/PatchNotes/CurrentEntryBullets.txt " +
                              "está vacío — la entrada más reciente se mostrará sin contenido. En un " +
                              "build real esto no debería pasar nunca (PatchNotesBuildGuard cancela el " +
                              "build si detecta esto).");
        }

        string header = $"v{Application.version} — Pre-Alpha ({dateText})";
        return string.IsNullOrEmpty(bullets) ? header : $"{header}\n\n{bullets}";
    }

    static string LoadResourceText(string resourcePath)
    {
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[PatchNotesFlyoutPanel] No se encontró el recurso de texto " +
                              $"'{resourcePath}'. Revisa que exista el .txt correspondiente en " +
                              "Assets/Resources/PatchNotes/.");
            return string.Empty;
        }
        return asset.text;
    }

    void ConfigureLabel(TextMeshProUGUI label, string text, float size)
    {
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        if (font != null) label.font = font;
    }

    // Centra un TMP_Text por la tinta que realmente pinta (bounds del mesh generado), no por las
    // métricas de la fuente (avance de carácter, cap height, ascender/descender) que usa
    // TextAlignmentOptions y que no siempre coinciden con los píxeles visibles de un glifo concreto
    // — evita tener que recalibrar constantes a mano cada vez que cambia la fuente o el carácter.
    // FIX (24 ago 2026, ver BugReportFlyoutPanel.cs — mismo bug, "la X primero sale mal y al hacer
    // click se pone bien"): esto se llama en el mismo Awake() en el que se acaba de añadir el
    // CanvasScaler del panel, que todavía no ha calculado su escala real en ese instante (Unity lo
    // hace en su propio paso de actualización de Canvas, no de forma síncrona al añadir el
    // componente) — medir aquí sin más se hacía contra un canvas a medio asentar, y cualquier
    // interacción posterior forzaba de paso una actualización de canvas que dejaba ver la posición
    // "real", pero el desplazamiento ya aplicado una vez con datos viejos se quedaba mal para
    // siempre. `Canvas.ForceUpdateCanvases()` fuerza esa actualización pendiente de forma síncrona
    // ANTES de medir.
    static void CenterGlyphOnRenderedInk(TextMeshProUGUI label)
    {
        Canvas.ForceUpdateCanvases();
        label.ForceMeshUpdate(true, true);
        Vector3 inkCenter = label.textBounds.center;
        label.rectTransform.anchoredPosition -= new Vector2(inkCenter.x, inkCenter.y);
    }

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
