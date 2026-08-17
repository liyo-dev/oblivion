using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sendero.Core.Feedback;

/// <summary>
/// Pantalla de créditos que cierra la demo: Logo (KingdomExitTransitionNode) → Créditos → Título.
///
/// Construye su UI por código en Awake (mismo patrón que TitleLogoController): Canvas, fondo
/// y bloque de créditos. No hace falta montar nada más en el Editor salvo, opcionalmente,
/// los campos de abajo.
///
/// Fondo: reutiliza DreamBackgroundController (nebulosa) y DreamSparkleOverlay (chispas/estrellas),
/// las mismas piezas que usa el sueño inicial de la demo (ver DramaticTextOverlayUI), sobre un
/// fondo sólido que pulsa con los mismos colores que DramaticTextOverlayUI en modo Sueño.
/// _dreamBackground/_dreamSparkles se pueden asignar en el Inspector con instancias ya colocadas
/// como hijas de este GameObject (para ajustar sus parámetros — cantidad de blobs, ritmo de
/// chispas, colores, etc.); si se dejan vacías, se crean automáticamente con los valores por defecto.
///
/// Título: se muestra el sprite del logo del juego (_logoSprite, el mismo que LogoTitulo en
/// MainMenu) en vez de texto. Si no se asigna, cae a un título de texto plano como respaldo.
///
/// Texto: el resto de créditos (_creditsText) sube en scroll vertical automático junto al logo
/// (ambos dentro de un único bloque con VerticalLayoutGroup). Se puede saltar pulsando UI/Submit.
/// Al terminar el scroll (o al saltar), funde a negro y carga _nextSceneName.
/// </summary>
public class CreditsSceneController : MonoBehaviour
{
    [Header("Logo del juego (sustituye al título de texto)")]
    [Tooltip("Sprite del logo/título del juego (el mismo que usa LogoTitulo en MainMenu). Se muestra arriba del bloque de créditos y sube con él. Si se deja vacío, se usa un título de texto de respaldo.")]
    [SerializeField] Sprite _logoSprite;
    [SerializeField] Vector2 _logoSize = new Vector2(1300f, 560f);

    [Header("Contenido")]
    [TextArea(10, 60)]
    [SerializeField] string _creditsText =
        // FIX (16 ago 2026): ver mismo comentario en CreditsFlyoutPanel.cs — el nombre de Raúl
        // estaba duplicado en las dos pantallas de créditos (panel rápido + este crawl
        // cinemático); quitarlo solo de una dejaba la otra sin corregir.
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

    [Tooltip("Frase final. No forma parte del scroll: aparece centrada al terminar de subir el bloque de arriba y se queda en pantalla hasta que acaba la música (ver _maxThankYouWaitSeconds).")]
    [SerializeField] string _thankYouText = "Gracias por jugar la demo";

    [Header("Texto")]
    [Tooltip("Fuente TMP para el texto de créditos (y el título de respaldo si no hay logo). Si se deja vacía, usa la fuente por defecto del proyecto.")]
    [SerializeField] TMP_FontAsset _font;
    [Min(8f)]   [SerializeField] float _fontSize = 34f;
    [Min(100f)] [SerializeField] float _textWidth = 1100f;
    [Min(0f)]   [SerializeField] float _blockSpacing = 40f;

    [Header("Fondo de sueño (reutiliza los scripts del sueño inicial)")]
    [Tooltip("Nebulosa procedural. Si se asigna una instancia ya colocada como hija de este GameObject (RectTransform estirado a pantalla completa), se usa esa; si se deja vacía, se crea una por defecto.")]
    [SerializeField] DreamBackgroundController _dreamBackground;
    [Tooltip("Chispas/estrellas procedurales. Mismo criterio que _dreamBackground.")]
    [SerializeField] DreamSparkleOverlay _dreamSparkles;
    [SerializeField] Color _bgDark  = new Color(0.03f, 0.05f, 0.16f, 1f);
    [SerializeField] Color _bgLight = new Color(0.06f, 0.09f, 0.24f, 1f);
    [Min(0.1f)] [SerializeField] float _bgPulseSeconds = 3.2f;

    [Header("Scroll")]
    [Tooltip("Velocidad de desplazamiento vertical, en píxeles de referencia (1920x1080) por segundo. Con todo el texto actual (~90 líneas contando los espacios entre artistas) más el logo grande, el bloque mide varios miles de píxeles: a 60 px/s tarda más de un minuto en cruzar la pantalla. Sube este valor si 'Gracias por jugar la demo' tarda mucho en aparecer.")]
    [Min(1f)] [SerializeField] float _scrollSpeed = 150f;

    [Header("Frase final (\"Gracias por jugar la demo\")")]
    [Min(0.1f)] [SerializeField] float _thankYouFontScale = 1.5f;
    [Min(0.05f)] [SerializeField] float _thankYouFadeInSeconds = 1f;
    [Tooltip("Espera de respaldo si no hay música sonando (o está en loop y no tiene un final con sentido). Con música normal, se usa el tiempo real que le queda al tema.")]
    [Min(0f)] [SerializeField] float _tailHoldSeconds = 4f;
    [Tooltip("Tope máximo de espera aunque la música tarde mucho en acabar, para no dejar la demo colgada.")]
    [Min(1f)] [SerializeField] float _maxThankYouWaitSeconds = 40f;

    [Header("Siguiente escena")]
    [SerializeField] string _nextSceneName = "MainMenu";
    [Min(0.05f)] [SerializeField] float _exitFadeSeconds = 0.5f;

    [Header("Salto manual")]
    [SerializeField] bool _allowSkip = true;

    RectTransform _scrollRect; // contenedor (logo + texto) que se desplaza como un bloque
    TextMeshProUGUI _thankYouLabel; // frase final, independiente del scroll
    CanvasGroup _thankYouGroup;     // controla la aparición de _thankYouLabel (ver BuildThankYouLabel)
    Tween _bgPulseTween;
    Coroutine _scrollRoutine;
    bool _finished;
    bool _pushedUIMode;

    void Awake()
    {
        BuildUI();
    }

    void OnEnable()
    {
        if (Core.PlayerInputManager.Instance != null)
        {
            Core.PlayerInputManager.Instance.PushUIMode();
            _pushedUIMode = true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[CreditsSceneController] OnEnable() -> arrancando coroutine Run().");
#endif
        _scrollRoutine = StartCoroutine(Run());
    }

    // Cuando _dreamBackground/_dreamSparkles son instancias ya colocadas en la escena
    // (en vez de creadas por código en BuildUI), Unity procesa Awake+OnEnable de este
    // objeto ANTES que el Awake de sus hijos (orden por objeto, no "todos los Awake
    // primero"): llamar a StartDream()/StartSparkles() desde OnEnable se ejecutaría
    // antes de que DreamBackgroundController/DreamSparkleOverlay hayan inicializado su
    // propio _rect en Awake, provocando NullReferenceException. Start() sí está
    // garantizado a ejecutarse después de todos los Awake/OnEnable de la escena.
    void Start()
    {
        _dreamBackground?.StartDream();
        _dreamSparkles?.StartSparkles();
    }

    void OnDisable()
    {
        if (_pushedUIMode && Core.PlayerInputManager.Instance != null)
        {
            Core.PlayerInputManager.Instance.PopUIMode();
            _pushedUIMode = false;
        }

        _bgPulseTween?.Kill();
        _bgPulseTween = null;

        _dreamBackground?.StopDream();
        _dreamSparkles?.StopSparkles();

        if (_scrollRoutine != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CreditsSceneController] OnDisable() -> cancelando _scrollRoutine que seguía en marcha (el scroll NO había terminado).");
#endif
            StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
        }
    }

    void Update()
    {
        if (_finished || !_allowSkip) return;

        var controls = Core.PlayerInputManager.Instance != null ? Core.PlayerInputManager.Instance.Controls : null;
        if (controls != null && controls.UI.Submit.WasPressedThisFrame())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CreditsSceneController] Salto manual detectado (UI/Submit) en Update().");
#endif
            FinishAndGoToTitle();
        }
    }

    // ── Construcción de UI ────────────────────────────────────────────────

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        BuildBackground();
        BuildScrollContent();
        BuildThankYouLabel();
    }

    void BuildBackground()
    {
        // --- Fondo sólido (pulsa entre _bgDark y _bgLight) ---
        var solidGo = new GameObject("SolidBackground", typeof(RectTransform));
        solidGo.transform.SetParent(transform, false);
        StretchToParent((RectTransform)solidGo.transform);
        var solidImg = solidGo.AddComponent<Image>();
        solidImg.color = _bgDark;
        solidImg.raycastTarget = false;
        _bgPulseTween = solidImg
            .DOColor(_bgLight, _bgPulseSeconds)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);

        // --- Nebulosa y chispas: reutilizan instancias ya colocadas (Inspector) o se crean por defecto ---
        if (_dreamBackground == null)
        {
            var nebulaGo = new GameObject("DreamNebula", typeof(RectTransform));
            nebulaGo.transform.SetParent(transform, false);
            StretchToParent((RectTransform)nebulaGo.transform);
            _dreamBackground = nebulaGo.AddComponent<DreamBackgroundController>();
        }

        if (_dreamSparkles == null)
        {
            var sparkleGo = new GameObject("DreamSparkles", typeof(RectTransform));
            sparkleGo.transform.SetParent(transform, false);
            StretchToParent((RectTransform)sparkleGo.transform);
            _dreamSparkles = sparkleGo.AddComponent<DreamSparkleOverlay>();
        }

        // Orden de dibujado explícito (independiente de si las piezas vienen del Inspector o
        // se acaban de crear): sólido detrás de todo, luego nebulosa, luego chispas.
        solidGo.transform.SetSiblingIndex(0);
        _dreamBackground.transform.SetSiblingIndex(1);
        _dreamSparkles.transform.SetSiblingIndex(2);
    }

    void BuildScrollContent()
    {
        var scrollGo = new GameObject("CreditsScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(transform, false);
        _scrollRect = (RectTransform)scrollGo.transform;
        _scrollRect.anchorMin = new Vector2(0.5f, 0f);
        _scrollRect.anchorMax = new Vector2(0.5f, 0f);
        _scrollRect.pivot     = new Vector2(0.5f, 0f);
        _scrollRect.sizeDelta = new Vector2(_textWidth, 0f);
        _scrollRect.SetAsLastSibling(); // siempre delante del fondo

        var layout = scrollGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = _blockSpacing;

        var fitter = scrollGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        if (_logoSprite != null)
        {
            var logoGo = new GameObject("Logo", typeof(RectTransform));
            logoGo.transform.SetParent(_scrollRect, false);
            var logoImg = logoGo.AddComponent<Image>();
            logoImg.sprite = _logoSprite;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            var logoLayout = logoGo.AddComponent<LayoutElement>();
            logoLayout.preferredWidth = _logoSize.x;
            logoLayout.preferredHeight = _logoSize.y;
        }
        else
        {
            // Respaldo si aún no se ha asignado el sprite del logo.
            var titleGo = new GameObject("TitleFallback", typeof(RectTransform));
            titleGo.transform.SetParent(_scrollRect, false);
            var titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
            ConfigureLabel(titleLabel, "EL SENDERO DE LAS ESTRELLAS", _fontSize * 1.3f);
            titleLabel.fontStyle = FontStyles.Bold;
            titleGo.AddComponent<LayoutElement>().preferredWidth = _textWidth;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[CreditsSceneController] _logoSprite no asignado — usando título de texto de respaldo.");
#endif
        }

        var textGo = new GameObject("CreditsText", typeof(RectTransform));
        textGo.transform.SetParent(_scrollRect, false);
        var label = textGo.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(label, _creditsText, _fontSize);
        // LayoutElement con ancho fijo: sin esto, VerticalLayoutGroup (childControlWidth=true)
        // usaría el preferredWidth propio de TMP (texto en una sola línea sin envolver) en vez
        // de _textWidth, rompiendo el word-wrap.
        textGo.AddComponent<LayoutElement>().preferredWidth = _textWidth;
    }

    void BuildThankYouLabel()
    {
        // Centrado en pantalla, independiente del bloque que hace scroll: no se mueve con él,
        // solo aparece (fade-in) cuando el bloque termina de subir. Empieza invisible.
        //
        // El fade usa un CanvasGroup (igual que TitleLogoController/DramaticTextOverlayUI) en
        // vez de animar el alpha del color de TMP directamente: un TextMeshProUGUI creado con
        // alpha=0 desde el primer frame puede quedar con cullTransparentMesh sin generar malla
        // nunca, y subir el alpha después no lo hace reaparecer. CanvasGroup no tiene ese problema.
        var go = new GameObject("ThankYouText", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(_textWidth, 300f);
        rt.SetAsLastSibling(); // por delante de todo, incluido el bloque de scroll

        _thankYouGroup = go.AddComponent<CanvasGroup>();
        _thankYouGroup.alpha = 0f;
        _thankYouGroup.blocksRaycasts = false;
        _thankYouGroup.interactable = false;

        _thankYouLabel = go.AddComponent<TextMeshProUGUI>();
        ConfigureLabel(_thankYouLabel, _thankYouText, _fontSize * _thankYouFontScale);
        _thankYouLabel.alignment = TextAlignmentOptions.Center;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CreditsSceneController] ThankYouText construido. text='{_thankYouText}'");
#endif
    }

    void ConfigureLabel(TextMeshProUGUI label, string text, float fontSize)
    {
        label.alignment = TextAlignmentOptions.Top;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.text = text;
        label.raycastTarget = false;
        if (_font != null) label.font = _font;
    }

    static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── Secuencia ─────────────────────────────────────────────────────────

    IEnumerator Run()
    {
        // Dejar que VerticalLayoutGroup/ContentSizeFitter calculen el layout antes de medir.
        yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect);
        float blockHeight = Mathf.Max(_scrollRect.rect.height, 50f);

        // BUGFIX: antes endY = 1080f + blockHeight, lo que obligaba al bloque a recorrer un
        // blockHeight ENTERO de más tras quedar ya completamente invisible (su borde inferior,
        // con pivot.y=0, ya está fuera de pantalla en cuanto y >= 1080f). Con créditos largos
        // (bloque de varios miles de píxeles) eso añadía decenas de segundos de pantalla en
        // blanco antes de que apareciera "Gracias por jugar la demo" o se cargara la siguiente
        // escena — se percibía como que los créditos se quedaban colgados sin motivo.
        float startY = -blockHeight;         // el bloque arranca justo debajo de la pantalla
        float endY   = 1080f;                // el borde inferior del bloque ya deja todo fuera de pantalla
        _scrollRect.anchoredPosition = new Vector2(0f, startY);

        float distance = endY - startY;
        float duration  = distance / _scrollSpeed;
        float elapsed   = 0f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CreditsSceneController] Run() -> blockHeight={blockHeight:F0}, distance={distance:F0}, scrollSpeed={_scrollSpeed:F0}, duration={duration:F2}s. Empieza el scroll.");
        float _nextHeartbeat = 2f;
#endif

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float y = Mathf.Lerp(startY, endY, Mathf.Clamp01(elapsed / duration));
            _scrollRect.anchoredPosition = new Vector2(0f, y);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (elapsed >= _nextHeartbeat)
            {
                Debug.Log($"[CreditsSceneController] Run() en marcha: {elapsed:F1}/{duration:F1}s.");
                _nextHeartbeat += 2f;
            }
#endif
            yield return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[CreditsSceneController] Run() -> bucle de scroll terminado, entrando a ShowThankYouUntilMusicEnds().");
#endif

        yield return ShowThankYouUntilMusicEnds();

        FinishAndGoToTitle();
    }

    /// <summary>
    /// Hace aparecer la frase final y la mantiene en pantalla hasta que se acabe la música
    /// (AudioService.GetMusicRemainingSeconds), con _tailHoldSeconds como respaldo si no hay
    /// música sonando o está en loop, y _maxThankYouWaitSeconds como tope de seguridad.
    /// </summary>
    IEnumerator ShowThankYouUntilMusicEnds()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CreditsSceneController] Scroll terminado. _thankYouGroup={(_thankYouGroup != null)}, iniciando fade de {_thankYouFadeInSeconds}s.");
#endif
        yield return FadeThankYouLabel(0f, 1f, _thankYouFadeInSeconds);

        float musicRemaining = AudioService.Instance != null ? AudioService.Instance.GetMusicRemainingSeconds() : -1f;
        float wait = musicRemaining < 0f ? _tailHoldSeconds : musicRemaining;
        wait = Mathf.Min(wait, _maxThankYouWaitSeconds);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CreditsSceneController] ThankYou visible. AudioService.Instance={(AudioService.Instance != null)}, " +
                  $"musicRemaining={musicRemaining:F2}, esperando {wait:F2}s antes de continuar a '{_nextSceneName}'.");
#endif

        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);
    }

    IEnumerator FadeThankYouLabel(float from, float to, float duration)
    {
        if (_thankYouGroup == null) yield break;

        _thankYouGroup.alpha = from;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _thankYouGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _thankYouGroup.alpha = to;
    }

    void FinishAndGoToTitle()
    {
        if (_finished) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // El stack trace de este log dice quién llamó: si viene de Update() fue un salto manual;
        // si viene de Run()/ShowThankYouUntilMusicEnds() fue el flujo normal tras la música.
        Debug.Log("[CreditsSceneController] FinishAndGoToTitle() invocado.");
#endif

        _finished = true;

        if (_scrollRoutine != null)
        {
            StopCoroutine(_scrollRoutine);
            _scrollRoutine = null;
        }

        StartCoroutine(GoToTitleRoutine());
    }

    IEnumerator GoToTitleRoutine()
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, _exitFadeSeconds, fadeIn: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CreditsSceneController] Créditos terminados → cargando '{_nextSceneName}'.");
#endif
        SceneTransitionLoader.Load(_nextSceneName);
    }
}
