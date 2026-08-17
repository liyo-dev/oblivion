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
/// Panel de créditos "rápido" para el MainMenu: al pulsar CRÉDITOS, en vez de cargar la escena
/// Credits.unity (el crawl cinemático que cierra la demo — se deja intacto, sigue existiendo para
/// esa función), se despliega un panel lateral con scroll manual sin salir del menú, estilo PEAK.
///
/// Construye toda su UI por código en Awake (mismo patrón que CreditsSceneController/
/// TitleLogoController: Canvas propio con sortingOrder alto, nada que montar en el Editor salvo
/// los pasos de abajo). No toca MainMenu.unity ni el botón CRÉDITOS existente en el Editor:
/// en Start() se localiza el botón por nombre/texto (o por <see cref="creditsButtonOverride"/> si
/// se asigna a mano) y se le reemplazan sus listeners en runtime, así que no hace falta editar el
/// OnClick() serializado del botón en el Inspector.
///
/// Setup en el Editor (una sola vez, en MainMenu.unity):
/// 1. GameObject vacío en la escena, ej. "CreditsFlyoutPanel" (no necesita ser hijo de nada en
///    concreto — se crea su propio Canvas).
/// 2. Añadir este componente.
/// 3. (Opcional) Si el autodetect del botón CRÉDITOS no lo encuentra, arrastrarlo a
///    <see cref="creditsButtonOverride"/>.
/// 4. Play. El botón CRÉDITOS ahora abre/cierra este panel en vez de cargar la escena de créditos.
///
/// El texto de <see cref="creditsText"/> es una copia del mismo contenido que usa
/// CreditsSceneController._creditsText — si se actualizan los créditos (nuevo asset, nuevo
/// colaborador...), hay que actualizarlo en los dos sitios.
/// </summary>
[DisallowMultipleComponent]
public class CreditsFlyoutPanel : MonoBehaviour
{
    [Header("Botón que abre el panel")]
    [Tooltip("Si se deja vacío, se busca en toda la escena un Button cuyo nombre o texto contenga 'credit'/'crédito' (con o sin tilde).")]
    [SerializeField] Button creditsButtonOverride;

    [Header("Contenido")]
    [TextArea(10, 60)]
    [SerializeField] string creditsText =
        // FIX (16 ago 2026): Raúl pidió quitar su nombre de los créditos y seguía apareciendo
        // — estaba también duplicado en CreditsSceneController.cs (el crawl completo de
        // Credits.unity), así que había que editar los dos sitios a la vez para que
        // desapareciera de verdad en ambas pantallas de créditos (el panel rápido de pausa y
        // el crawl cinemático final).
        "Créditos de assets\n\n\n\n" +
        "VFX\n\n" +
        "Matthew Guz - Spell Area of Effect, Orbs Effects\n\n" +
        "Hovl Studio - Magic Effects Pack, Procedural Fire\n\n" +
        "GabrielAguiarProductions - Free Quick Effects Vol.1\n\n" +
        "Lana Studio - Hyper Casual FX\n\n" +
        "G-Spot Lab - Magic Energy Seamless Textures\n\n" +
        "Ahmed Houidi - Plasma Shader\n\n" +
        "LushkinR - Vertical Fog Shader\n\n" +
        "Roman Chacornac - Realistic Rain VFX\n\n" +
        "Travis Game Assets - Hit Impact Effects\n\n" +
        "Eric Wang - Free Game VFX\n\n\n\n" +
        "ESCENARIOS Y MUNDO\n\n" +
        "polySoft3D (Alexander Zaytsev) - Modular Castle\n\n" +
        "ithappy - Sweet Land\n\n" +
        "Unvik 3D - Cross Plains\n\n" +
        "Quantum Mana Studio - Cartoon Skybox Pack\n\n" +
        "Dungeon Mason - RPG Tiny Fantasy World, RPG Tiny Hero World Bundle\n\n\n\n" +
        "PERSONAJES\n\n" +
        "Kevin Iglesias - Human Animations\n\n" +
        "Dungeon Mason - RPG Tiny Hero Duo\n\n\n\n" +
        "INTERFAZ (UI)\n\n" +
        "Pixel Play - Off Screen Target Indicator\n\n\n\n" +
        "AUDIO Y MÚSICA\n\n" +
        "ithappy - efectos de sonido\n\n" +
        "Yoshiki Ara - \"Mycologist of the Windy Valley\"\n\n\n\n" +
        "HERRAMIENTAS Y PLUGINS\n\n" +
        "Demigiant - DOTween\n\n" +
        "Invector - 3rd Person Controller\n\n" +
        "Ciro Continisio - Toon Shader\n\n" +
        "Unity Technologies - TextMesh Pro\n\n\n\n" +
        "TIPOGRAFÍA\n\n" +
        "Nunito - Vernon Adams (Google Fonts)";

    [SerializeField] TMP_FontAsset font;
    [Min(8f)] [SerializeField] float fontSize = 26f;

    [Header("Créditos completos (crawl cinemático)")]
    [Tooltip("Nombre de la escena del crawl completo (Credits.unity). El panel rápido incluye un botón para saltar a la experiencia completa.")]
    [SerializeField] string fullCreditsSceneName = "Credits";

    [Header("Panel")]
    [Min(200f)] [SerializeField] float panelWidth = 560f;
    [SerializeField] Color panelColor = new Color(0.07f, 0.05f, 0.03f, 0.9f);
    [Tooltip("Dorado a juego con el cursor personalizado (CursorManager) — línea de acento y títulos.")]
    [SerializeField] Color accentColor = new Color(0.97f, 0.71f, 0.22f, 1f);
    [Min(0.05f)] [SerializeField] float animDuration = 0.32f;

    [Header("Navegación (mando)")]
    [Tooltip("Cerrar el panel con el mando usa el botón Cancelar real (East: B en Xbox, ○ en PlayStation, A en Switch) — el mismo que cierra Ajustes/Controles — en vez de exigir navegar hasta el botón \"X\" con el D-pad y confirmar. \"Ver créditos completos\" usa el botón North (Y en Xbox, △ en PlayStation, X en Switch).")]
    [SerializeField, Min(0f)] float cancelInputGracePeriod = 0.25f;

    [Header("Auto-scroll")]
    [Tooltip("Velocidad de subida automática del texto de créditos, en 'pantallas de contenido' por segundo. 0 = desactivado (solo scroll manual).")]
    [Min(0f)] [SerializeField] float autoScrollSpeed = 0.045f;
    [Tooltip("Espera antes de empezar a subir al abrir el panel, para dar tiempo a leer el principio.")]
    [Min(0f)] [SerializeField] float autoScrollStartDelay = 1.5f;
    [Tooltip("Cuánto se pausa la subida automática tras un scroll manual del jugador (rueda del ratón o arrastre).")]
    [Min(0f)] [SerializeField] float autoScrollResumeDelay = 2.5f;

    RectTransform _panelRoot;
    CanvasGroup _panelGroup;
    RectTransform _content;
    ScrollRect _scrollRect;
    GameObject _outsideCatcher;
    Button _closeButton;
    Button _openButton;
    Tween _slideTween;
    Coroutine _autoScrollRoutine;
    float _resumeAutoScrollAt;
    bool _isOpen;
    float _hiddenX;

    // Icono/etiqueta del botón cerrar y del footer "Ver créditos completos", según familia de
    // dispositivo activa — ver RefreshGlyphs().
    TMP_Text _closeLabel;
    Image _closeIcon;
    TMP_Text _footerLabel;
    Image _footerIcon;
    float _openedAt = -999f;

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
        // FIX (16 ago 2026): "el botón CRÉDITOS del menú principal a veces funciona y a veces no".
        // WireCreditsButton() localizaba el botón CRÉDITOS con FindObjectsByType<Button>(...) UNA
        // sola vez, en este Start(). Ese overload de FindObjectsByType excluye objetos inactivos
        // por defecto, y el botón/panel de MainMenu.unity puede no estar activo todavía en el mismo
        // frame en que este Start() se ejecuta (depende del orden de ejecución de scripts frente a
        // lo que active el panel de botones — animación de entrada, MainMenuFlyingCompanion,
        // TitleLogoController, etc. — que no está garantizado respecto a este componente). Si ese
        // frame concreto el botón todavía estaba inactivo, FindCreditsButton() no lo encontraba,
        // se quedaba en el Debug.LogWarning silencioso de abajo y el botón CRÉDITOS se quedaba con
        // el listener original (o sin ninguno) para el resto de la sesión — de ahí la
        // intermitencia: dependía de qué tan rápido/lento arrancara el resto del menú esa partida
        // concreta. Mismo patrón que NarrativeGraphStarter.WaitForHubAndStart(): reintentar durante
        // un margen de frames en vez de un intento único.
        StartCoroutine(WireCreditsButtonWithRetry());
    }

    IEnumerator WireCreditsButtonWithRetry()
    {
        const float maxWaitSeconds = 2f;
        float deadline = Time.unscaledTime + maxWaitSeconds;

        while (Time.unscaledTime < deadline)
        {
            if (TryWireCreditsButton())
                yield break;

            yield return null;
        }

        // Último intento tras agotar el margen, para que el log de abajo (si falla) refleje el
        // estado final real en vez de un intento del primer frame.
        if (!TryWireCreditsButton())
        {
            Debug.LogWarning("[CreditsFlyoutPanel] No se encontró el botón CRÉDITOS automáticamente " +
                              $"tras reintentar durante {2f:0.#}s. Asigna 'Credits Button Override' a mano en el Inspector.");
        }
    }

    void Update()
    {
        // Atajos de mando directos mientras el panel está abierto — sin esto, un jugador con mando
        // solo podía cerrar/saltar a créditos completos navegando con el D-pad hasta el Button
        // correspondiente y confirmando con South, en vez de un botón dedicado como en el resto de
        // paneles del menú (Ajustes/Controles ya usan Start/Cancel del mando para cerrarse, ver
        // ControlsMenuController/SettingsMenuController).
        if (!_isOpen) return;
        if (Time.unscaledTime - _openedAt < cancelInputGracePeriod) return;

        if (GamepadInputReader.CancelPressed)
            ClosePanel();
        else if (GamepadInputReader.YButtonPressedUI)
            GoToFullCredits();
    }

    void OnDestroy()
    {
        _slideTween?.Kill();
    }

    void HandleFamilyChanged(InputGlyphDeviceFamily _) => RefreshGlyphs();

    // FIX: la "X" del botón cerrar es un carácter de teclado/ratón (convención de ventana), no un
    // botón real de ningún mando — mostrarla ahí confundía en vez de indicar qué pulsar. En mando se
    // sustituye por el icono real del botón que YA cierra el panel (Cancelar/East, ver Update()), y
    // el footer "Ver créditos completos" antepone el icono real de su propio atajo (North). En
    // teclado/ratón se deja el texto tal cual (la "X" sí es una convención válida ahí).
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

        if (_footerIcon != null)
        {
            var sprite = gamepad ? InputGlyphService.GetSprite(InputGlyphNames.North) : null;
            _footerIcon.sprite = sprite;
            _footerIcon.enabled = sprite != null;
        }
        if (_footerLabel != null)
            _footerLabel.text = gamepad ? "Ver créditos completos" : "Ver créditos completos >>";
    }

    // ── Conexión con el botón CRÉDITOS ya existente en MainMenu.unity ───────

    bool TryWireCreditsButton()
    {
        // Ya conectado en un intento anterior de esta misma sesión — no hace falta seguir.
        if (_openButton != null)
            return true;

        _openButton = creditsButtonOverride != null ? creditsButtonOverride : FindCreditsButton();

        if (_openButton == null)
            return false;

        // Sustituye el listener existente (el que cargaba Credits.unity) por el toggle del panel.
        // No se toca el botón en el Editor: esto ocurre en runtime, así que MainMenu.unity queda intacto.
        _openButton.onClick.RemoveAllListeners();
        _openButton.onClick.AddListener(TogglePanel);
        return true;
    }

    static Button FindCreditsButton()
    {
        // FIX (16 ago 2026, ver comentario en Start()): FindObjectsInactive.Include en vez del
        // valor por defecto (Exclude) — el botón puede estar temporalmente inactivo (panel de
        // menú aún no revelado por su animación de entrada) en el frame en que se busca.
        var all = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in all)
        {
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
        return n.Contains("credit");
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

        if (_autoScrollRoutine != null) StopCoroutine(_autoScrollRoutine);
        if (autoScrollSpeed > 0f && _scrollRect != null)
        {
            _resumeAutoScrollAt = Time.unscaledTime + autoScrollStartDelay;
            _autoScrollRoutine = StartCoroutine(AutoScrollRoutine());
        }
    }

    public void ClosePanel()
    {
        if (!_isOpen) return;
        _isOpen = false;

        AudioService.Instance?.PlaySFX("UI_Cancel", 1f);

        _panelGroup.blocksRaycasts = false;
        _panelGroup.interactable = false;

        if (_autoScrollRoutine != null)
        {
            StopCoroutine(_autoScrollRoutine);
            _autoScrollRoutine = null;
        }

        _slideTween?.Kill();
        _slideTween = _panelRoot.DOAnchorPosX(_hiddenX, animDuration).SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => _outsideCatcher.SetActive(false));

        if (_openButton != null)
            EventSystem.current?.SetSelectedGameObject(_openButton.gameObject);
    }

    // Sube el contenido lentamente como un crawl de créditos. Se pausa unos segundos si el
    // jugador interviene manualmente (rueda del ratón o arrastre, ver PauseAutoScroll) para no
    // pelearse con su scroll.
    IEnumerator AutoScrollRoutine()
    {
        while (_isOpen)
        {
            if (Time.unscaledTime >= _resumeAutoScrollAt && _scrollRect.verticalNormalizedPosition > 0f)
            {
                _scrollRect.verticalNormalizedPosition = Mathf.Max(0f,
                    _scrollRect.verticalNormalizedPosition - autoScrollSpeed * Time.unscaledDeltaTime);
            }
            yield return null;
        }
    }

    // Enganchado a BeginDrag/Scroll del ScrollRect (ver BuildScrollView) para que la subida
    // automática no compita con un scroll manual del jugador.
    void PauseAutoScroll(BaseEventData _)
    {
        _resumeAutoScrollAt = Time.unscaledTime + autoScrollResumeDelay;
    }

    void GoToFullCredits()
    {
        if (string.IsNullOrEmpty(fullCreditsSceneName))
        {
            Debug.LogWarning("[CreditsFlyoutPanel] fullCreditsSceneName vacío — no se puede abrir el crawl completo.");
            return;
        }

        AudioService.Instance?.PlaySFX("UI_Submit");
        SceneTransitionLoader.Load(fullCreditsSceneName);
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

    // Capa transparente a pantalla completa, solo activa mientras el panel está abierto:
    // cerrar al hacer click fuera (y evita que ese click le llegue a los botones del menú de detrás).
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
        var panelGo = new GameObject("CreditsPanel", typeof(RectTransform));
        panelGo.transform.SetParent(transform, false);
        _panelRoot = (RectTransform)panelGo.transform;
        _panelRoot.anchorMin = new Vector2(1f, 0f);
        _panelRoot.anchorMax = new Vector2(1f, 1f);
        // FIX: con pivot.x=0 (borde izquierdo del panel como pivote) y anclado al borde derecho de
        // la pantalla, anchoredPosition.x=0 deja el panel ENTERO fuera de pantalla (se extiende hacia
        // la derecha desde el borde), no pegado y visible como parecía la intención. Por eso
        // OpenPanel() (que anima a x=0) nunca mostraba nada: el panel viajaba de "fuera de pantalla"
        // a "fuera de pantalla". Con pivot.x=1 (borde derecho del panel como pivote), x=0 sí queda
        // pegado al borde derecho y visible, y _hiddenX (positivo) lo saca de pantalla como se espera.
        _panelRoot.pivot = new Vector2(1f, 0.5f);
        _panelRoot.sizeDelta = new Vector2(panelWidth, 0f);
        _panelRoot.anchoredPosition = Vector2.zero;

        _hiddenX = panelWidth + 40f;
        _panelRoot.anchoredPosition = new Vector2(_hiddenX, 0f);

        _panelGroup = panelGo.AddComponent<CanvasGroup>();
        _panelGroup.alpha = 1f; // el propio slide fuera de pantalla ya lo "oculta"
        _panelGroup.blocksRaycasts = false;
        _panelGroup.interactable = false;

        var bg = panelGo.AddComponent<Image>();
        bg.color = panelColor;

        // Línea de acento dorada en el borde izquierdo del panel
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
        BuildFooter();
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
        titleRt.offsetMax = new Vector2(-56f, 0f); // deja hueco al botón de cerrar
        var title = titleGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(title, "CRÉDITOS", fontSize * 1.3f);
        title.fontStyle = FontStyles.Bold;
        title.color = accentColor;
        title.alignment = TextAlignmentOptions.Left;

        // Botón cerrar ("X" en teclado/ratón; icono real del botón Cancelar del mando cuando la
        // familia activa es un mando — ver RefreshGlyphs()), esquina superior derecha del panel.
        // FIX: usaba el glifo unicode "✕" (U+2715), que la fuente TMP del proyecto no tiene en su
        // atlas — se veía como un cuadrado "tofu" en vez del icono. Se sustituye por una X normal,
        // garantizada en cualquier fuente, sin depender de generar el atlas con ese glifo extra.
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
        // FIX: TextAlignmentOptions.Center centra verticalmente usando la línea completa
        // (ascender-descender) de la fuente, no la altura real del glifo — para un carácter sin
        // descendentes como "X" eso deja el trazo visualmente pegado hacia arriba dentro del botón
        // 48x48. Capline centra respecto a la altura de mayúscula (cap height), que sí coincide con
        // los píxeles que realmente se ven, así que la "X" queda centrada de verdad en el botón.
        closeLabel.horizontalAlignment = HorizontalAlignmentOptions.Center;
        closeLabel.verticalAlignment = VerticalAlignmentOptions.Capline;
        closeLabel.margin = Vector4.zero;
        closeLabel.raycastTarget = false;
        _closeLabel = closeLabel;

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
        _closeIcon.enabled = false; // RefreshGlyphs() lo activa si la familia activa es un mando
    }

    void BuildScrollView()
    {
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
        scrollGo.transform.SetParent(_panelRoot, false);
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(28f, 90f);   // deja hueco al footer
        scrollRt.offsetMax = new Vector2(-28f, -110f); // deja hueco a la cabecera

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;
        _scrollRect = scrollRect;

        // Pausar la subida automática mientras el jugador scrollea a mano.
        var trigger = scrollGo.AddComponent<EventTrigger>();
        AddTriggerEntry(trigger, EventTriggerType.BeginDrag, PauseAutoScroll);
        AddTriggerEntry(trigger, EventTriggerType.Scroll, PauseAutoScroll);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollRt, false);
        var viewportRt = (RectTransform)viewportGo.transform;
        StretchToParent(viewportRt);
        viewportGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // necesario para el RectMask2D
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

        var textGo = new GameObject("CreditsText", typeof(RectTransform));
        textGo.transform.SetParent(_content, false);
        var label = textGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(label, creditsText, fontSize);
        label.alignment = TextAlignmentOptions.TopLeft;

        scrollRect.viewport = viewportRt;
        scrollRect.content = _content;
    }

    void BuildFooter()
    {
        var footerGo = new GameObject("Footer", typeof(RectTransform));
        footerGo.transform.SetParent(_panelRoot, false);
        var footerRt = (RectTransform)footerGo.transform;
        footerRt.anchorMin = new Vector2(0f, 0f);
        footerRt.anchorMax = new Vector2(1f, 0f);
        footerRt.pivot = new Vector2(0.5f, 0f);
        footerRt.sizeDelta = new Vector2(-40f, 64f);
        footerRt.anchoredPosition = new Vector2(0f, 20f);

        footerGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        var btn = footerGo.AddComponent<Button>();
        btn.onClick.AddListener(GoToFullCredits);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(footerRt, false);
        StretchToParent((RectTransform)labelGo.transform);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        // FIX: mismo problema de glifo que el botón de cerrar — "↗" (U+2197) no está en el atlas
        // de la fuente y se veía como un cuadrado. ">>" usa solo caracteres ASCII normales. En
        // mando, RefreshGlyphs() quita el ">>" (no es un botón real) y antepone el icono real de
        // North en su lugar (ver _footerIcon más abajo).
        ConfigureLabel(label, "Ver créditos completos >>", fontSize * 0.85f);
        label.alignment = TextAlignmentOptions.Center;
        label.color = accentColor;
        label.raycastTarget = false;
        _footerLabel = label;

        var footerIconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        footerIconGo.transform.SetParent(footerRt, false);
        var footerIconRt = (RectTransform)footerIconGo.transform;
        footerIconRt.anchorMin = new Vector2(0f, 0.5f);
        footerIconRt.anchorMax = new Vector2(0f, 0.5f);
        footerIconRt.pivot = new Vector2(0f, 0.5f);
        footerIconRt.sizeDelta = new Vector2(32f, 32f);
        footerIconRt.anchoredPosition = new Vector2(16f, 0f);
        _footerIcon = footerIconGo.GetComponent<Image>();
        _footerIcon.preserveAspect = true;
        _footerIcon.raycastTarget = false;
        _footerIcon.enabled = false; // RefreshGlyphs() lo activa si la familia activa es un mando
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

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }
}
