using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// Panel de UI para el panic input: icono parpadeante + barra de progreso.
/// Si no está en escena se auto-construye en runtime sobre el canvas del TutorialPromptUI.
[DisallowMultipleComponent]
public class PanicInputUI : MonoBehaviour
{
    [Header("Referencias UI (se auto-asignan si se crea en runtime)")]
    [SerializeField] private CanvasGroup _rootGroup;
    [SerializeField] private Image       _buttonIcon;
    [SerializeField] private Slider      _progressBar;

    [Header("Parpadeo")]
    [SerializeField] private float _blinkMin      = 0.15f;
    [SerializeField] private float _blinkMax      = 1f;
    [SerializeField] private float _blinkDuration = 0.3f;

    [Header("Fade")]
    [SerializeField] private float _fadeInDuration  = 0.15f;
    [SerializeField] private float _fadeOutDuration = 0.2f;

    private Tween              _blinkTween;
    private PanicInputDetector _detector;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        // Solo inicializar si los campos ya están asignados (prefab en escena).
        // Si _rootGroup es null, GetOrCreate está en medio de la construcción
        // y llamará Setup() justo después del AddComponent.
        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        _blinkTween?.Kill();
        Unsubscribe();
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// Asigna las referencias y deja el panel listo (usado por la creación en runtime).
    public void Setup(CanvasGroup cg, Image icon, Slider slider)
    {
        _rootGroup   = cg;
        _buttonIcon  = icon;
        _progressBar = slider;
        _rootGroup.alpha          = 0f;
        _rootGroup.blocksRaycasts = false;
        _rootGroup.interactable   = false;
        gameObject.SetActive(false);
    }

    /// Actualiza el icono de botón mostrado sin reconstruir el panel — usado para refrescarlo con el
    /// icono generado por <c>Core.InputGlyphs.InputGlyphService</c> (Xbox/PlayStation/Switch/Teclado)
    /// cada vez que se activa la secuencia de pánico, por si el jugador cambió de dispositivo mientras tanto.
    public void SetIcon(Sprite icon)
    {
        if (_buttonIcon != null && icon != null) _buttonIcon.sprite = icon;
    }

    public void Activate(PanicInputDetector detector)
    {
        _detector = detector;
        if (_detector != null)
        {
            _detector.OnProgressChanged += OnProgress;
            _detector.OnSuccess         += Deactivate;
            _detector.OnFailure         += Deactivate;
        }

        gameObject.SetActive(true);
        if (_progressBar != null) _progressBar.value = 0f;

        _rootGroup?.DOKill();
        _rootGroup?.DOFade(1f, _fadeInDuration).SetUpdate(true);

        StartBlink();
    }

    public void Deactivate()
    {
        Unsubscribe();
        _blinkTween?.Kill();

        if (_buttonIcon != null)
        {
            var c = _buttonIcon.color;
            c.a = 1f;
            _buttonIcon.color = c;
        }

        if (_rootGroup != null)
        {
            _rootGroup.DOKill();
            _rootGroup.DOFade(0f, _fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // ── Factory en runtime ────────────────────────────────────────────────

    /// Devuelve la instancia existente o crea el panel sobre el canvas del juego.
    public static PanicInputUI GetOrCreate(Sprite buttonSprite = null)
    {
        var existing = FindAnyObjectByType<PanicInputUI>();
        if (existing != null) return existing;

        // Preferir el canvas del TutorialPromptUI (garantizado DontDestroyOnLoad)
        Canvas canvas = TutorialPromptUI.Instance != null
            ? TutorialPromptUI.Instance.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return null;

        var root = new GameObject("PanicInputUI", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta        = new Vector2(1000f, 1000f);

        var cg = root.AddComponent<CanvasGroup>();

        // Fondo semitransparente
        var bgGO = new GameObject("BG", typeof(RectTransform));
        bgGO.transform.SetParent(root.transform, false);
        FillRT(bgGO.GetComponent<RectTransform>());
        bgGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Icono del botón
        var iconGO = new GameObject("ButtonIcon", typeof(RectTransform));
        iconGO.transform.SetParent(root.transform, false);
        var iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin  = new Vector2(0.05f, 0.18f);
        iconRT.anchorMax  = new Vector2(0.95f, 0.98f);
        iconRT.offsetMin  = iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;
        if (buttonSprite != null) iconImg.sprite = buttonSprite;

        // Barra de progreso
        var sliderGO = new GameObject("ProgressBar", typeof(RectTransform));
        sliderGO.transform.SetParent(root.transform, false);
        var sliderRT = sliderGO.GetComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.05f, 0.05f);
        sliderRT.anchorMax = new Vector2(0.95f, 0.2f);
        sliderRT.offsetMin = sliderRT.offsetMax = Vector2.zero;

        var sliderBgGO = new GameObject("BG", typeof(RectTransform));
        sliderBgGO.transform.SetParent(sliderGO.transform, false);
        FillRT(sliderBgGO.GetComponent<RectTransform>());
        sliderBgGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var fillAreaGO = new GameObject("FillArea", typeof(RectTransform));
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        FillRT(fillAreaGO.GetComponent<RectTransform>());

        var fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        fillGO.AddComponent<Image>().color = new Color(1f, 0.45f, 0.08f, 1f);

        var slider = sliderGO.AddComponent<Slider>();
        slider.fillRect   = fillRT;
        slider.direction  = Slider.Direction.LeftToRight;
        slider.minValue   = 0f;
        slider.maxValue   = 1f;
        slider.value      = 0f;
        slider.interactable = false;

        // AddComponent dispara Awake; en ese momento _rootGroup es null → no hace nada
        var ui = root.AddComponent<PanicInputUI>();
        ui.Setup(cg, iconImg, slider);

        return ui;
    }

    // ── Internos ──────────────────────────────────────────────────────────

    private void StartBlink()
    {
        if (_buttonIcon == null) return;
        _blinkTween?.Kill();
        _blinkTween = _buttonIcon
            .DOFade(_blinkMin, _blinkDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetEase(Ease.InOutSine)
            .From(_blinkMax);
    }

    private void OnProgress(float progress)
    {
        if (_progressBar != null) _progressBar.value = progress;
    }

    private void Unsubscribe()
    {
        if (_detector == null) return;
        _detector.OnProgressChanged -= OnProgress;
        _detector.OnSuccess         -= Deactivate;
        _detector.OnFailure         -= Deactivate;
        _detector = null;
    }

    private static void FillRT(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
