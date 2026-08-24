using System.Collections;
using System.Text;
using Core;
using Core.InputGlyphs;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Panel de "Reportar un Fallo" del MainMenu: en vez de abrir el Google Form en el navegador del
/// sistema (enfoque anterior — ver BugReportButton.cs, ya no se usa, sustituido por este panel),
/// despliega un formulario nativo DENTRO del propio juego, con el mismo patrón visual que
/// PatchNotesFlyoutPanel/CreditsFlyoutPanel. Al pulsar ENVIAR, los datos se mandan directamente al
/// Google Form real por HTTP (UnityWebRequest.Post contra su endpoint /formResponse) — el jugador
/// nunca sale del juego, y las respuestas siguen cayendo en el mismo Google Form / hoja de cálculo
/// de siempre. Decisión tomada con Raúl el 24 ago 2026: quería que se sintiera "dentro del juego"
/// como en la captura de referencia de Billie Bust Up (que usa un WebView embebido real — aquí se
/// optó por replicar el formulario con UI nativa en vez de añadir una dependencia de pago para un
/// WebView de verdad, ver conversación para el resto de opciones consideradas).
///
/// Los IDs de entry.* y la URL de envío (<see cref="formResponseUrl"/>, <see cref="EntrySeveridad"/>
/// y compañía) se obtuvieron inspeccionando el formulario publicado
/// (window.FB_PUBLIC_LOAD_DATA_ en la página del formulario, la forma estándar de sacar la
/// estructura real de un Google Form) y se verificaron con un envío de prueba real contra el
/// formulario (respuesta de prueba visible en la pestaña "Respuestas" del Form — bórrala a mano si
/// sigue ahí, no se borra sola desde aquí). Si el formulario se recrea alguna vez desde cero, estos
/// valores hay que volver a sacarlos de la misma forma — no son adivinables ni estables entre
/// formularios distintos.
///
/// El texto de <see cref="SeverityOptions"/> tiene que coincidir carácter a carácter con las
/// opciones reales de la pregunta "Severidad del Fallo" en el Google Form — es lo que Google valida
/// contra la lista de opciones del campo al recibir el POST.
///
/// Localización (24 ago 2026, a petición de Raúl): título, etiquetas de campo, placeholders,
/// severidad y el botón ENVIAR usan claves de <see cref="LocalizedText"/> (mismo sistema que el
/// resto del menú, catálogos ui_es.json/ui_en.json) y se refrescan solos si el idioma cambia con el
/// panel abierto. Los mensajes de estado (validación/envío/éxito/error) se traducen al vuelo con
/// <see cref="Loc"/> cada vez que se muestran. EL GOOGLE FORM EN SÍ (documento externo de Google, no
/// pasa por este sistema de localización) también se hizo bilingüe ese mismo día, a elección de Raúl
/// ("Preguntas bilingües en el mismo Form"): título, descripción, las 4 preguntas y las 4 opciones de
/// severidad llevan ahora el texto en español seguido de " / " y su traducción al inglés, todo dentro
/// de la misma pregunta — en vez de dejarlo solo en español o crear un Form en inglés aparte. Por eso
/// <see cref="SeverityOptions"/> también lleva ambos idiomas: ver su comentario más abajo.
///
/// Construye toda su UI por código en Awake, no toca MainMenu.unity ni el botón REPORTAR UN FALLO
/// existente en el Editor: en Start() se localiza el botón por nombre/texto (o por
/// <see cref="bugReportButtonOverride"/> si se asigna a mano) y se le reemplazan sus listeners en
/// runtime — mismo patrón que PatchNotesFlyoutPanel.
///
/// Setup en el Editor (en MainMenu.unity) — ya lo hace MainMenuPatchNotesBugReportBuilder.cs, no
/// hace falta montarlo a mano (también limpia el GameObject 'BugReportButton' de la versión
/// anterior si sigue en la escena, para que no compita por el mismo botón).
/// </summary>
[DisallowMultipleComponent]
public class BugReportFlyoutPanel : MonoBehaviour
{
    [Header("Botón que abre el panel")]
    [Tooltip("Si se deja vacío, se busca en toda la escena un Button cuyo nombre o texto contenga 'bug' o 'fallo'.")]
    [SerializeField] Button bugReportButtonOverride;

    [Header("Envío del Google Form")]
    [Tooltip("Endpoint /formResponse del Google Form real (no la URL de 'ver formulario'). Sacado de FB_PUBLIC_LOAD_DATA_ del formulario publicado el 24 ago 2026.")]
    [SerializeField] string formResponseUrl =
        "https://docs.google.com/forms/d/e/1FAIpQLSdv0YjEGQFM1tRp3_B2vomj3eFXliErIc1FtAPe4cLAY2FMlg/formResponse";

    const string EntrySeveridad = "entry.869257947";
    const string EntryBuild = "entry.211084022";
    const string EntryDescripcion = "entry.78895582";
    const string EntryPasos = "entry.64206919";

    // Texto EXACTO de cada opción tal cual está en el Google Form (ver comentario de clase). Desde
    // el 24 ago 2026 el Form es bilingüe (ES / EN en la misma pregunta, ver AskUserQuestion de Raúl:
    // "Preguntas bilingües en el mismo Form"), así que cada opción real ahora incluye AMBOS idiomas
    // separados por " / " — es el string completo el que hay que enviar por POST, sea cual sea el
    // idioma activo del juego. Re-extraído el 24 ago 2026 vía FB_PUBLIC_LOAD_DATA_ tras el edit del
    // Form; cambiar este texto sin cambiar también las opciones del Form real haría que el envío
    // fallara (Google valida el value del POST contra la lista de opciones configuradas).
    static readonly string[] SeverityOptions =
    {
        "Game Breaking (un fallo que hace imposible continuar) / Game Breaking (a bug that makes it impossible to continue)",
        "Fallo Mayor (un fallo que arruina la experiencia, p. ej. quedarse atascado en el terreno o no poder realizar una acción) / Major Bug (a bug that ruins the experience, e.g. getting stuck in the terrain or being unable to perform an action)",
        "Fallo Menor (un fallo que no afecta a la jugabilidad, p. ej. animaciones, música, efectos de sonido) / Minor Bug (a bug that doesn't affect gameplay, e.g. animations, music, sound effects)",
        "Fallo Puntual (un fallo que ha ocurrido pero es difícil de reproducir) / One-off Bug (a bug that occurred but is hard to reproduce)",
    };

    // Etiquetas cortas para las filas del panel — el texto completo de arriba es el que se envía,
    // pero repetirlo entero en cada fila del panel (ya angosto) sería poco legible. Sí se localizan
    // (a diferencia de SeverityOptions, que tiene que quedarse en español porque así está el Form).
    static readonly string[] SeverityShortLabels =
    {
        "Game Breaking",
        "Fallo Mayor",
        "Fallo Menor",
        "Fallo Puntual",
    };
    static readonly string[] SeverityShortLabelKeys =
    {
        "BugReport_SeverityGameBreaking",
        "BugReport_SeverityMayor",
        "BugReport_SeverityMenor",
        "BugReport_SeverityPuntual",
    };

    [SerializeField] TMP_FontAsset font;
    [Min(8f)] [SerializeField] float fontSize = 24f;

    [Header("Panel")]
    [Min(200f)] [SerializeField] float panelWidth = 620f;
    [SerializeField] Color panelColor = new Color(0.07f, 0.05f, 0.03f, 0.9f);
    [Tooltip("Dorado a juego con el resto de paneles del menú (mismo tono que Créditos/Notas del Parche).")]
    [SerializeField] Color accentColor = new Color(0.97f, 0.71f, 0.22f, 1f);
    [Min(0.05f)] [SerializeField] float animDuration = 0.32f;

    [Header("Navegación (mando)")]
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

    // Estado del formulario
    int _selectedSeverity = -1;
    Image[] _severityBackgrounds;
    TMP_InputField _buildField;
    TMP_InputField _descriptionField;
    TMP_InputField _stepsField;
    Button _submitButton;
    TMP_Text _statusText;
    bool _submitting;

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
        StartCoroutine(WireBugReportButtonWithRetry());
    }

    IEnumerator WireBugReportButtonWithRetry()
    {
        const float maxWaitSeconds = 2f;
        float deadline = Time.unscaledTime + maxWaitSeconds;

        while (Time.unscaledTime < deadline)
        {
            if (TryWireBugReportButton())
                yield break;

            yield return null;
        }

        if (!TryWireBugReportButton())
        {
            Debug.LogWarning("[BugReportFlyoutPanel] No se encontró el botón REPORTAR UN FALLO automáticamente " +
                              $"tras reintentar durante {2f:0.#}s. Asigna 'Bug Report Button Override' a mano en el Inspector.");
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

    // ── Conexión con el botón REPORTAR UN FALLO ya existente en MainMenu.unity ───────

    bool TryWireBugReportButton()
    {
        if (_openButton != null)
            return true;

        _openButton = bugReportButtonOverride != null ? bugReportButtonOverride : FindBugReportButton();

        if (_openButton == null)
            return false;

        _openButton.onClick.RemoveAllListeners();
        _openButton.onClick.AddListener(TogglePanel);
        return true;
    }

    Button FindBugReportButton()
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
        return n.Contains("bug") || n.Contains("fallo");
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
        var panelGo = new GameObject("BugReportPanel", typeof(RectTransform));
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
        ConfigureLabel(title, "REPORTAR UN FALLO", fontSize * 1.3f);
        title.fontStyle = FontStyles.Bold;
        title.color = accentColor;
        title.alignment = TextAlignmentOptions.Left;
        titleGo.AddComponent<LocalizedText>().key = "BugReport_Title";

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
        var closeLabelRt = (RectTransform)closeLabelGo.transform;
        StretchToParent(closeLabelRt);
        var closeLabel = closeLabelGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(closeLabel, "X", fontSize);
        closeLabel.fontStyle = FontStyles.Bold;
        closeLabel.alignment = TextAlignmentOptions.Center;
        closeLabel.margin = Vector4.zero;
        closeLabel.raycastTarget = false;
        _closeLabel = closeLabel;
        // FIX (24 ago 2026, tras una segunda captura de Raúl donde la "X" seguía sin verse centrada):
        // la ronda anterior compensaba con un offset horizontal fijo de -6, calibrado a ojo contra UNA
        // captura concreta — insuficiente/impreciso, y el mismo problema existía también (sin ningún
        // fix) en CreditsFlyoutPanel.cs/PatchNotesFlyoutPanel.cs con Capline. En vez de seguir afinando
        // constantes a mano, se mide la tinta renderizada de verdad (bounds real del mesh de TMP tras
        // forzar su generación) y se centra el RectTransform contra eso — válido para cualquier fuente
        // o carácter, sin depender de métricas de fuente que no coinciden con los píxeles pintados.
        // Mismo fix aplicado en los otros dos paneles.
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
        layout.spacing = 6f;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportRt;
        scrollRect.content = _content;

        BuildFormFields();
    }

    // ── Campos del formulario ────────────────────────────────────────────

    void BuildFormFields()
    {
        CreateFieldLabel("Severidad del Fallo *", "BugReport_LabelSeveridad");
        CreateSeverityOptions();
        CreateSpacer(18f);

        CreateFieldLabel("Número de Build *", "BugReport_LabelBuild");
        _buildField = CreateInputField("0.0.0", "BugReport_PlaceholderBuild", multiline: false);
        // FIX (24 ago 2026, Raúl: "confuso ver en el placeholder 0.1.5 y que la versión sea 0.1.7"):
        // un placeholder con un número de versión concreto ("0.1.5") se queda desactualizado en
        // cuanto sale una build nueva — este proyecto además autoincrementa la versión en cada build
        // (BuildVersionIncrementer.cs), así que un valor fijo aquí garantiza volver a quedar mal más
        // pronto que tarde. El placeholder ahora es un patrón genérico ("0.0.0", nunca una versión
        // real) y, más importante, el campo se PRERRELLENA con la versión real de la build en curso
        // (mismo Application.version que pinta VersionLabelUI.cs en la esquina inferior izquierda) —
        // así el jugador no tiene ni que teclearla, y nunca puede desincronizarse de la build real.
        _buildField.text = Application.version;
        CreateFieldHint("Puedes encontrarlo en la esquina inferior izquierda de la pantalla", "BugReport_HelpBuild");
        CreateSpacer(18f);

        CreateFieldLabel("Descripción del Fallo *", "BugReport_LabelDescripcion");
        _descriptionField = CreateInputField("Intenta ser lo más específico posible.", "BugReport_PlaceholderDescripcion", multiline: true);
        CreateSpacer(18f);

        CreateFieldLabel("Pasos para Reproducirlo (opcional)", "BugReport_LabelPasos");
        _stepsField = CreateInputField("¿Cómo puedo reproducir el fallo?", "BugReport_PlaceholderPasos", multiline: true);
        CreateSpacer(28f);

        CreateSubmitButton();
        CreateStatusText();
        CreateSpacer(20f);
    }

    // El texto que se escribe aquí (en español) es también el fallback que usa LocalizedText si al
    // componente ISO EN todavía le falta la clave, o si LocalizationManager aún no ha cargado — no
    // hace falta duplicar el fallback a mano.
    void CreateFieldLabel(string text, string locKey)
    {
        var go = new GameObject("FieldLabel", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(label, text, fontSize * 0.92f);
        label.fontStyle = FontStyles.Bold;
        label.color = accentColor;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize * 1.4f;
        go.AddComponent<LocalizedText>().key = locKey;
    }

    // FIX (24 ago 2026, captura de Raúl: "el placeholder no se ve entero y da mala imagen"): el texto
    // de ayuda de Número de Build ("Puedes encontrarlo en la esquina inferior izquierda de la
    // pantalla") vivía como PLACEHOLDER dentro de un campo de una sola línea — no cabía entero ni
    // envolviendo (se recortaba la mitad de abajo tapado por el RectMask2D del campo, bug ya corregido
    // antes) ni en una sola línea (se recortaba por la derecha con "…", que es justo lo que se ve en
    // la captura). El propio Google Form real (ver mejora-menu-principal-fase-patchnotes-bugreport...
    // .md) muestra este mismo texto como AYUDA bajo la pregunta, no dentro del campo — aquí se hace
    // igual: el placeholder del campo pasa a ser un ejemplo corto ("0.1.5"), y el texto de ayuda largo
    // se pinta aparte, debajo del campo, como una línea de texto normal que envuelve sin límite de
    // alto ni recorte.
    void CreateFieldHint(string text, string locKey)
    {
        var go = new GameObject("FieldHint", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(label, text, fontSize * 0.72f);
        label.color = new Color(1f, 1f, 1f, 0.5f);
        label.alignment = TextAlignmentOptions.TopLeft;
        label.margin = new Vector4(2f, 4f, 2f, 0f);
        // Sin LayoutElement.preferredHeight fijo a propósito: TextMeshProUGUI ya implementa
        // ILayoutElement y calcula su alto real según en cuántas líneas envuelve el texto dentro del
        // ancho que le da el VerticalLayoutGroup (childControlWidth/Height están activos en _content,
        // ver BuildScrollView) — más fiable que adivinar una altura fija a mano (el mismo error que
        // causó el bug original del placeholder cortado).
        go.AddComponent<LocalizedText>().key = locKey;
    }

    void CreateSpacer(float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    void CreateSeverityOptions()
    {
        _severityBackgrounds = new Image[SeverityOptions.Length];

        for (int i = 0; i < SeverityOptions.Length; i++)
        {
            int index = i;

            var rowGo = new GameObject($"SeverityOption_{i}", typeof(RectTransform));
            rowGo.transform.SetParent(_content, false);
            var rowLe = rowGo.AddComponent<LayoutElement>();
            rowLe.preferredHeight = fontSize * 1.9f;
            rowLe.minHeight = fontSize * 1.9f;

            var bg = rowGo.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);
            _severityBackgrounds[i] = bg;

            var button = rowGo.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SelectSeverity(index));

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(14f, 4f);
            labelRt.offsetMax = new Vector2(-14f, -4f);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            ConfigureLabel(label, SeverityShortLabels[i], fontSize * 0.9f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            labelGo.AddComponent<LocalizedText>().key = SeverityShortLabelKeys[i];
        }
    }

    void SelectSeverity(int index)
    {
        _selectedSeverity = index;
        for (int i = 0; i < _severityBackgrounds.Length; i++)
        {
            _severityBackgrounds[i].color = i == index
                ? new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f)
                : new Color(1f, 1f, 1f, 0.06f);
        }
        AudioService.Instance?.PlaySFX("UI_Move");
    }

    // TMP_InputField es sensible al orden de Awake/OnEnable si textComponent/placeholder todavía no
    // están asignados cuando Unity lo activa por primera vez — por eso el GameObject se crea inactivo
    // y solo se reactiva al final, ya con todo configurado (evita null refs internos de TMP).
    TMP_InputField CreateInputField(string placeholder, string placeholderLocKey, bool multiline)
    {
        float height = multiline ? fontSize * 4.2f : fontSize * 2.1f;

        var fieldGo = new GameObject("InputField", typeof(RectTransform));
        fieldGo.transform.SetParent(_content, false);
        fieldGo.SetActive(false);

        var le = fieldGo.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;

        var bg = fieldGo.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.06f);

        var textAreaGo = new GameObject("TextArea", typeof(RectTransform));
        textAreaGo.transform.SetParent(fieldGo.transform, false);
        var textAreaRt = (RectTransform)textAreaGo.transform;
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.offsetMin = new Vector2(12f, 6f);
        textAreaRt.offsetMax = new Vector2(-12f, -6f);
        textAreaGo.AddComponent<RectMask2D>();

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(textAreaRt, false);
        StretchToParent((RectTransform)placeholderGo.transform);
        var placeholderLabel = placeholderGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(placeholderLabel, placeholder, fontSize * 0.85f);
        placeholderLabel.color = new Color(1f, 1f, 1f, 0.4f);
        placeholderLabel.fontStyle = FontStyles.Italic;
        placeholderLabel.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        // Bug (24 ago 2026, capturas de Raúl): ConfigureLabel deja el wrap en Normal para todos los
        // labels, pero un campo de una sola línea (multiline: false, p. ej. "Número de Build") mide
        // solo fontSize*2.1 de alto — si el placeholder es largo ("Puedes encontrarlo en la esquina
        // inferior izquierda de la pantalla"), envuelve a 2 líneas y el RectMask2D de TextArea recorta
        // la mitad de abajo, dejándolo cortado a la vista. En campos de una línea el placeholder no
        // debe envolver: mejor que se recorte por la derecha (comportamiento estándar de un input de
        // una línea) que perder la mitad inferior del texto.
        if (!multiline)
        {
            placeholderLabel.textWrappingMode = TextWrappingModes.NoWrap;
            placeholderLabel.overflowMode = TextOverflowModes.Truncate;
        }
        placeholderGo.AddComponent<LocalizedText>().key = placeholderLocKey;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(textAreaRt, false);
        StretchToParent((RectTransform)textGo.transform);
        var textLabel = textGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(textLabel, "", fontSize * 0.85f);
        textLabel.color = Color.white;
        textLabel.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        // Mismo motivo que el placeholder de arriba: en un campo de una sola línea, lo que escriba el
        // jugador tampoco debe envolver a una segunda línea invisible/recortada.
        if (!multiline)
        {
            textLabel.textWrappingMode = TextWrappingModes.NoWrap;
            textLabel.overflowMode = TextOverflowModes.Truncate;
        }

        var input = fieldGo.AddComponent<TMP_InputField>();
        input.textViewport = textAreaRt;
        input.textComponent = textLabel;
        input.placeholder = placeholderLabel;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        input.targetGraphic = bg;

        fieldGo.SetActive(true);
        return input;
    }

    void CreateSubmitButton()
    {
        var go = new GameObject("SubmitButton", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize * 2.3f;
        le.minHeight = fontSize * 2.3f;

        var bg = go.AddComponent<Image>();
        bg.color = accentColor;
        _submitButton = go.AddComponent<Button>();
        _submitButton.targetGraphic = bg;
        _submitButton.onClick.AddListener(HandleSubmitPressed);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        StretchToParent((RectTransform)labelGo.transform);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(label, "ENVIAR", fontSize);
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.1f, 0.07f, 0.02f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        labelGo.AddComponent<LocalizedText>().key = "BugReport_Submit";
    }

    void CreateStatusText()
    {
        var go = new GameObject("StatusText", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(label, "", fontSize * 0.82f);
        label.alignment = TextAlignmentOptions.Center;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize * 2.2f;
        _statusText = label;
    }

    // ── Envío ─────────────────────────────────────────────────────────────

    void HandleSubmitPressed()
    {
        if (_submitting) return;

        if (_selectedSeverity < 0)
        {
            ShowStatus(Loc("BugReport_ErrorSeveridad", "Elige la severidad del fallo."), isError: true);
            return;
        }
        if (string.IsNullOrWhiteSpace(_buildField.text))
        {
            ShowStatus(Loc("BugReport_ErrorBuild", "Escribe el número de build."), isError: true);
            return;
        }
        if (string.IsNullOrWhiteSpace(_descriptionField.text))
        {
            ShowStatus(Loc("BugReport_ErrorDescripcion", "Describe el fallo antes de enviar."), isError: true);
            return;
        }

        StartCoroutine(SubmitReport());
    }

    IEnumerator SubmitReport()
    {
        _submitting = true;
        _submitButton.interactable = false;
        ShowStatus(Loc("BugReport_Sending", "Enviando…"), isError: false);

        var form = new WWWForm();
        form.AddField(EntrySeveridad, SeverityOptions[_selectedSeverity]);
        form.AddField(EntryBuild, _buildField.text);
        form.AddField(EntryDescripcion, _descriptionField.text);
        form.AddField(EntryPasos, string.IsNullOrWhiteSpace(_stepsField.text) ? "" : _stepsField.text);

        using (var request = UnityWebRequest.Post(formResponseUrl, form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                AudioService.Instance?.PlaySFX("UI_Submit");
                ShowStatus(Loc("BugReport_Success", "¡Gracias! Reporte enviado."), isError: false);
                ClearForm();
            }
            else
            {
                Debug.LogWarning($"[BugReportFlyoutPanel] Fallo al enviar el reporte: {request.error} (código {request.responseCode})");
                ShowStatus(Loc("BugReport_Error", "No se pudo enviar. Comprueba tu conexión e inténtalo de nuevo."), isError: true);
            }
        }

        _submitting = false;
        _submitButton.interactable = true;
    }

    void ClearForm()
    {
        _selectedSeverity = -1;
        for (int i = 0; i < _severityBackgrounds.Length; i++)
            _severityBackgrounds[i].color = new Color(1f, 1f, 1f, 0.06f);

        _buildField.text = "";
        _descriptionField.text = "";
        _stepsField.text = "";
    }

    void ShowStatus(string message, bool isError)
    {
        if (_statusText == null) return;
        _statusText.text = message;
        _statusText.color = isError ? new Color(1f, 0.45f, 0.4f, 1f) : new Color(0.6f, 1f, 0.6f, 1f);
    }

    // Para los mensajes de estado (validación/envío/éxito/error): se generan en el momento, así que
    // no hay un TMP_Text estático al que colgarle un LocalizedText — simplemente se pide la
    // traducción actual cada vez que se muestran. Los textos estáticos (título, etiquetas,
    // placeholders, botón, severidad) sí usan LocalizedText más arriba, para refrescarse solos si el
    // jugador cambia de idioma con el panel ya abierto.
    static string Loc(string key, string fallback) =>
        LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallback) : fallback;

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
    // FIX (24 ago 2026, captura de Raúl: "la X primero sale mal y al hacer click se pone bien"): esto
    // se llama desde BuildHeader(), justo después de AddComponent<CanvasScaler>() en el mismo Awake()
    // — el CanvasScaler recién añadido todavía no ha calculado su escala real (eso lo hace Unity en su
    // propio paso de actualización de Canvas, no de forma síncrona al añadir el componente), así que
    // medir aquí sin más se hacía contra un estado de canvas todavía a medio asentar. Cualquier
    // interacción posterior (como el propio click en el botón) fuerza ya de paso una actualización de
    // canvas y de ahí que "se viera bien" después — pero el desplazamiento ya se había aplicado una
    // vez con datos viejos y se quedaba mal para siempre. `Canvas.ForceUpdateCanvases()` fuerza esa
    // actualización pendiente (layout + canvas) de forma síncrona ANTES de medir, así que se mide
    // siempre contra el estado ya asentado, no hace falta esperar a ninguna interacción del jugador.
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
