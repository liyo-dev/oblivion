using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Barra de vida para bosses que aparece automáticamente en la esquina inferior derecha.
/// Se autoconfigura completamente, solo añade este script al GameObject del boss.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private string bossName = "Boss Demonio";
    [SerializeField] private Vector2 barSize = new Vector2(400f, 40f);
    [SerializeField] private Vector2 barPosition = new Vector2(-20f, 80f); // Desde la esquina inferior derecha

    [Header("Colores")]
    [SerializeField] private Color healthyColor = new Color(0.8f, 0.2f, 0.2f); // Rojo oscuro
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;
    [SerializeField] private float warningThreshold = 0.5f;
    [SerializeField] private float criticalThreshold = 0.25f;

    [Header("Animación")]
    [SerializeField] private bool animateHealthChanges = true;
    [SerializeField] private float animationSpeed = 5f;

    // Referencias generadas automáticamente
    private Damageable _bossDamageable;
    private Canvas _canvas;
    private GameObject _barContainer;
    private Image _healthBarFill;
    private Image _healthBarBackground;
    private TextMeshProUGUI _healthText;
    private TextMeshProUGUI _bossNameText;
    private CanvasGroup _canvasGroup;

    private float _targetFillAmount = 1f;
    private float _currentFillAmount = 1f;
    private bool _isVisible = false;

    void Awake()
    {
        // Buscar el componente Damageable en este GameObject
        _bossDamageable = GetComponent<Damageable>();
        
        if (!_bossDamageable)
        {
            Debug.LogError("[BossHealthBar] No se encontró el componente Damageable en el GameObject. Este script debe estar en el mismo GameObject que el boss.");
            enabled = false;
            return;
        }

        // Crear toda la UI automáticamente
        CreateBossHealthBarUI();
    }

    void Start()
    {
        if (_bossDamageable)
        {
            // Suscribirse a eventos
            _bossDamageable.OnDamaged += OnBossDamaged;
            _bossDamageable.OnDied += OnBossDied;

            // Inicializar la barra
            UpdateHealthBar();
            
            // Mostrar la barra
            Show();
        }
    }

    void OnDestroy()
    {
        if (_bossDamageable)
        {
            _bossDamageable.OnDamaged -= OnBossDamaged;
            _bossDamageable.OnDied -= OnBossDied;
        }

        // Destruir el canvas creado
        if (_canvas != null && _canvas.gameObject != null)
        {
            Destroy(_canvas.gameObject);
        }
    }

    void Update()
    {
        if (!_isVisible) return;

        // Animar la barra suavemente
        if (animateHealthChanges && Mathf.Abs(_currentFillAmount - _targetFillAmount) > 0.001f)
        {
            _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount, Time.deltaTime * animationSpeed);
            
            if (_healthBarFill)
            {
                _healthBarFill.fillAmount = _currentFillAmount;
            }
        }
    }

    private void CreateBossHealthBarUI()
    {
        // 1. Crear Canvas principal
        GameObject canvasObj = new GameObject("BossHealthBar_Canvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100; // Asegurar que esté por encima de otras UI
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. Crear contenedor principal (posicionado en esquina inferior derecha)
        _barContainer = new GameObject("BarContainer");
        _barContainer.transform.SetParent(_canvas.transform, false);
        
        RectTransform containerRect = _barContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(1f, 0f); // Esquina inferior derecha
        containerRect.anchorMax = new Vector2(1f, 0f);
        containerRect.pivot = new Vector2(1f, 0f);
        containerRect.anchoredPosition = barPosition;
        containerRect.sizeDelta = new Vector2(barSize.x + 20f, barSize.y + 60f); // Espacio extra para nombre

        _canvasGroup = _barContainer.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f; // Empezar oculto

        // 3. Crear texto del nombre del boss (arriba de la barra)
        GameObject nameObj = new GameObject("BossName");
        nameObj.transform.SetParent(_barContainer.transform, false);
        
        RectTransform nameRect = nameObj.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -5f);
        nameRect.sizeDelta = new Vector2(0f, 30f);

        _bossNameText = nameObj.AddComponent<TextMeshProUGUI>();
        _bossNameText.text = bossName;
        _bossNameText.fontSize = 24;
        _bossNameText.fontStyle = FontStyles.Bold;
        _bossNameText.alignment = TextAlignmentOptions.Center;
        _bossNameText.color = Color.white;
        
        // Añadir sombra al texto
        var shadow = nameObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(2, -2);

        // 4. Crear background de la barra
        GameObject bgObj = new GameObject("HealthBar_Background");
        bgObj.transform.SetParent(_barContainer.transform, false);
        
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0f);
        bgRect.pivot = new Vector2(0.5f, 0f);
        bgRect.anchoredPosition = new Vector2(0f, 5f);
        bgRect.sizeDelta = new Vector2(-20f, barSize.y);

        _healthBarBackground = bgObj.AddComponent<Image>();
        _healthBarBackground.sprite = CreateSolidSprite(Color.white);
        _healthBarBackground.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        _healthBarBackground.type = Image.Type.Sliced;

        // Añadir borde
        var outline = bgObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // 5. Crear barra de vida (fill)
        GameObject fillObj = new GameObject("HealthBar_Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(-4f, -4f); // Padding interno

        _healthBarFill = fillObj.AddComponent<Image>();
        _healthBarFill.sprite = CreateSolidSprite(Color.white);
        _healthBarFill.color = healthyColor;
        _healthBarFill.type = Image.Type.Filled;
        _healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        _healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _healthBarFill.fillAmount = 1f;

        // 6. Crear texto de vida (encima de la barra)
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(bgObj.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        _healthText = textObj.AddComponent<TextMeshProUGUI>();
        _healthText.fontSize = 18;
        _healthText.fontStyle = FontStyles.Bold;
        _healthText.alignment = TextAlignmentOptions.Center;
        _healthText.color = Color.white;
        
        // Añadir sombra al texto
        var textShadow = textObj.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0, 0, 0, 0.9f);
        textShadow.effectDistance = new Vector2(1, -1);

        Debug.Log($"[BossHealthBar] UI creada automáticamente para '{bossName}'");
    }

    private Sprite CreateSolidSprite(Color color)
    {
        // Crear una textura simple de 1x1 pixel
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        texture.filterMode = FilterMode.Point;

        // Crear sprite desde la textura
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
        sprite.name = "AutoGeneratedSprite";

        return sprite;
    }

    private void OnBossDamaged(float damageAmount)
    {
        UpdateHealthBar();

        // Efecto de flash
        if (_healthBarBackground)
        {
            StartCoroutine(DamageFlash());
        }

        // Mostrar la barra si estaba oculta
        if (!_isVisible)
        {
            Show();
        }
    }

    private void OnBossDied()
    {
        UpdateHealthBar();
        StartCoroutine(HideAfterDelay(2f));
    }

    private void UpdateHealthBar()
    {
        if (!_bossDamageable) return;

        float healthPercentage = _bossDamageable.Current / _bossDamageable.Max;
        _targetFillAmount = Mathf.Clamp01(healthPercentage);

        if (!animateHealthChanges)
        {
            _currentFillAmount = _targetFillAmount;
            if (_healthBarFill)
            {
                _healthBarFill.fillAmount = _currentFillAmount;
            }
        }

        // Actualizar color según salud
        if (_healthBarFill)
        {
            if (healthPercentage <= criticalThreshold)
                _healthBarFill.color = criticalColor;
            else if (healthPercentage <= warningThreshold)
                _healthBarFill.color = warningColor;
            else
                _healthBarFill.color = healthyColor;
        }

        // Actualizar texto de salud
        if (_healthText)
        {
            _healthText.text = $"{Mathf.Ceil(_bossDamageable.Current)} / {_bossDamageable.Max}";
        }
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (_healthBarBackground)
        {
            Color originalColor = _healthBarBackground.color;
            _healthBarBackground.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            
            yield return new WaitForSeconds(0.15f);
            
            _healthBarBackground.color = originalColor;
        }
    }

    private System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }

    public void Show()
    {
        _isVisible = true;
        if (_canvasGroup)
        {
            StartCoroutine(FadeIn());
        }
    }

    public void Hide()
    {
        _isVisible = false;
        if (_canvasGroup)
        {
            StartCoroutine(FadeOut());
        }
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
    }

    private System.Collections.IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}

