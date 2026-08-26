using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Barra de vida para bosses. Se autoconfigura en Awake; BossArenaController llama a Show()
/// cuando el combate comienza. Se oculta automáticamente mientras haya un menú/diálogo abierto.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private string bossName = "Boss Demonio";
    [SerializeField] private Vector2 barSize     = new Vector2(400f, 40f);
    // INC-107: centro-superior de pantalla (convención habitual del género), antes
    // esquina inferior derecha. Ver anchorMin/anchorMax/pivot en CreateBossHealthBarUI().
    [SerializeField] private Vector2 barPosition = new Vector2(0f, -30f);

    [Header("Colores")]
    [SerializeField] private Color healthyColor          = new Color(0.8f, 0.2f, 0.2f);
    [SerializeField] private Color warningColor          = Color.yellow;
    [SerializeField] private Color criticalColor         = Color.red;
    [SerializeField] private float warningThreshold  = 0.5f;
    [SerializeField] private float criticalThreshold = 0.25f;

    [Header("Animación")]
    [SerializeField] private bool  animateHealthChanges = true;
    [SerializeField] private float animationSpeed       = 5f;
    [SerializeField] private float fadeInDuration       = 0.4f;
    [SerializeField] private float fadeOutDuration      = 0.5f;

    // Referencias generadas automáticamente
    private Damageable         _bossDamageable;
    private Canvas             _canvas;
    private GameObject         _barContainer;
    private Image              _healthBarFill;
    private Image              _healthBarBackground;
    private TextMeshProUGUI    _healthText;
    private TextMeshProUGUI    _bossNameText;
    private CanvasGroup        _canvasGroup;

    private float _targetFillAmount  = 1f;
    private float _currentFillAmount = 1f;

    // Estado de visibilidad: activo en batalla y si está suspendido por un menú
    private bool  _battleActive       = false;
    private bool  _suspendedByMenu    = false;
    private Tween _fadeTween;

    void Awake()
    {
        _bossDamageable = GetComponent<Damageable>();
        if (!_bossDamageable)
        {
            Debug.LogError("[BossHealthBar] No se encontró Damageable en el GameObject.", this);
            enabled = false;
            return;
        }
        CreateBossHealthBarUI();
    }

    void Start()
    {
        if (_bossDamageable)
        {
            _bossDamageable.OnDamaged += OnBossDamaged;
            _bossDamageable.OnDied   += OnBossDied;
            UpdateHealthBar();
        }
        // No auto-mostrar — BossArenaController llama a Show() cuando corresponde
    }

    void OnEnable()
    {
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
    }

    void OnDisable()
    {
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
    }

    void OnDestroy()
    {
        _fadeTween?.Kill();
        if (_bossDamageable)
        {
            _bossDamageable.OnDamaged -= OnBossDamaged;
            _bossDamageable.OnDied   -= OnBossDied;
        }
        if (_canvas != null && _canvas.gameObject != null)
            Destroy(_canvas.gameObject);
    }

    void Update()
    {
        if (!_battleActive || _suspendedByMenu) return;
        if (!animateHealthChanges) return;
        if (Mathf.Abs(_currentFillAmount - _targetFillAmount) <= 0.001f) return;

        _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount,
                                         Time.deltaTime * animationSpeed);
        if (_healthBarFill)
            _healthBarFill.fillAmount = _currentFillAmount;
    }

    // ── API pública ────────────────────────────────────────────────────────

    public void Show()
    {
        _battleActive    = true;
        _suspendedByMenu = false;

        // Si hay un menú abierto, esperar a que cierre
        if (MenuManager.AnyOpen())
        {
            _suspendedByMenu = true;
            return;
        }

        AnimateFade(1f, fadeInDuration);
    }

    public void Hide()
    {
        _battleActive = false;
        AnimateFade(0f, fadeOutDuration);
    }

    // ── MenuManager ────────────────────────────────────────────────────────

    private void OnMenuOpened(MenuKind kind)
    {
        if (!_battleActive) return;
        _suspendedByMenu = true;
        AnimateFade(0f, fadeOutDuration * 0.7f);
    }

    private void OnMenuClosed(MenuKind kind)
    {
        if (!_battleActive || !_suspendedByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía hay otro menú abierto
        _suspendedByMenu = false;
        AnimateFade(1f, fadeInDuration);
    }

    // ── Eventos del boss ───────────────────────────────────────────────────

    private void OnBossDamaged(float _)
    {
        UpdateHealthBar();
        if (!_battleActive) Show(); // mostrar si por algún motivo no se había mostrado
        FlashDamage();
    }

    private void OnBossDied()
    {
        UpdateHealthBar();
        _battleActive = false;
        _fadeTween?.Kill();
        _fadeTween = _canvasGroup.DOFade(0f, fadeOutDuration)
            .SetDelay(2f).SetEase(Ease.InQuad).SetUpdate(true);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void AnimateFade(float target, float duration)
    {
        if (_canvasGroup == null) return;
        _fadeTween?.Kill();
        _fadeTween = _canvasGroup.DOFade(target, duration)
            .SetEase(target > 0f ? Ease.OutCubic : Ease.InCubic)
            .SetUpdate(true);
    }

    private void FlashDamage()
    {
        if (_healthBarBackground == null) return;
        Color orig = _healthBarBackground.color;
        _healthBarBackground.DOColor(new Color(1f, 0.3f, 0.3f, 0.9f), 0.08f)
            .SetUpdate(true)
            .OnComplete(() => _healthBarBackground.DOColor(orig, 0.15f).SetUpdate(true));
    }

    private void UpdateHealthBar()
    {
        if (!_bossDamageable) return;

        float pct = _bossDamageable.Current / _bossDamageable.Max;
        _targetFillAmount = Mathf.Clamp01(pct);

        if (!animateHealthChanges)
        {
            _currentFillAmount = _targetFillAmount;
            if (_healthBarFill) _healthBarFill.fillAmount = _currentFillAmount;
        }

        if (_healthBarFill)
        {
            _healthBarFill.color = pct <= criticalThreshold ? criticalColor
                                 : pct <= warningThreshold  ? warningColor
                                 : healthyColor;
        }

        if (_healthText)
            _healthText.text = $"{Mathf.Ceil(_bossDamageable.Current)} / {_bossDamageable.Max}";
    }

    // ── Creación de UI ─────────────────────────────────────────────────────

    private void CreateBossHealthBarUI()
    {
        GameObject canvasObj = new GameObject("BossHealthBar_Canvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder  = 100;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        _barContainer = new GameObject("BarContainer");
        _barContainer.transform.SetParent(_canvas.transform, false);

        var containerRect = _barContainer.AddComponent<RectTransform>();
        // INC-107: centro-superior en vez de esquina inferior derecha.
        containerRect.anchorMin        = new Vector2(0.5f, 1f);
        containerRect.anchorMax        = new Vector2(0.5f, 1f);
        containerRect.pivot            = new Vector2(0.5f, 1f);
        containerRect.anchoredPosition = barPosition;
        containerRect.sizeDelta        = new Vector2(barSize.x + 20f, barSize.y + 60f);

        _canvasGroup       = _barContainer.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        // Nombre del boss
        var nameObj  = new GameObject("BossName");
        nameObj.transform.SetParent(_barContainer.transform, false);
        var nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0f, 1f);
        nameRect.anchorMax        = new Vector2(1f, 1f);
        nameRect.pivot            = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -5f);
        nameRect.sizeDelta        = new Vector2(0f, 30f);

        _bossNameText           = nameObj.AddComponent<TextMeshProUGUI>();
        _bossNameText.text      = bossName;
        _bossNameText.fontSize  = 24;
        _bossNameText.fontStyle = FontStyles.Bold;
        _bossNameText.alignment = TextAlignmentOptions.Center;
        _bossNameText.color     = Color.white;
        var shadow = nameObj.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);

        // Fondo de la barra
        var bgObj  = new GameObject("HealthBar_Background");
        bgObj.transform.SetParent(_barContainer.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0f, 0f);
        bgRect.anchorMax        = new Vector2(1f, 0f);
        bgRect.pivot            = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0f, 5f);
        bgRect.sizeDelta        = new Vector2(-20f, barSize.y);

        _healthBarBackground       = bgObj.AddComponent<Image>();
        _healthBarBackground.sprite = CreateSolidSprite();
        _healthBarBackground.color  = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        _healthBarBackground.type   = Image.Type.Sliced;
        var outline = bgObj.AddComponent<Outline>();
        outline.effectColor    = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Fill
        var fillObj  = new GameObject("HealthBar_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin        = Vector2.zero;
        fillRect.anchorMax        = Vector2.one;
        fillRect.pivot            = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta        = new Vector2(-4f, -4f);

        _healthBarFill            = fillObj.AddComponent<Image>();
        _healthBarFill.sprite     = CreateSolidSprite();
        _healthBarFill.color      = healthyColor;
        _healthBarFill.type       = Image.Type.Filled;
        _healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        _healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _healthBarFill.fillAmount = 1f;

        // Texto de HP
        var textObj  = new GameObject("HealthText");
        textObj.transform.SetParent(bgObj.transform, false);
        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin        = Vector2.zero;
        textRect.anchorMax        = Vector2.one;
        textRect.pivot            = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta        = Vector2.zero;

        _healthText           = textObj.AddComponent<TextMeshProUGUI>();
        _healthText.fontSize  = 18;
        _healthText.fontStyle = FontStyles.Bold;
        _healthText.alignment = TextAlignmentOptions.Center;
        _healthText.color     = Color.white;
        var textShadow = textObj.AddComponent<Shadow>();
        textShadow.effectColor    = new Color(0, 0, 0, 0.9f);
        textShadow.effectDistance = new Vector2(1, -1);
    }

    private static Sprite CreateSolidSprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f,
                             0, SpriteMeshType.FullRect);
    }
}
