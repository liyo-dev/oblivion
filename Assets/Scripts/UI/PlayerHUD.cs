using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// HUD automático del player con salud y maná.
/// Se crea automáticamente al añadir el script al GameObject.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private Vector2 hudPosition = new Vector2(50, -50); // Arriba izquierda
    [SerializeField] private bool showDebugInfo = false;

    [Header("Animación de Barras")]
    [Tooltip("Si está activo, las barras se interpolan suavemente hacia el valor objetivo")]
    [SerializeField] private bool animateBars = true;
    [Tooltip("Velocidad de interpolación de las barras (mayor = más rápido)")]
    [SerializeField] private float barLerpSpeed = 8f;

    [Header("Colores")]
    [SerializeField] private Color healthColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color manaColor = new Color(0.3f, 0.5f, 1f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.3f); // Mucho más transparente
    [SerializeField] private Color barBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Background de las barras más sutil

    // Referencias automáticas
    private PlayerHealthSystem _healthSystem; // ← CAMBIADO: usar tu sistema real
    private ManaPool _manaPool;

    // UI Elements
    private Canvas _canvas;
    private GameObject _hudPanel;
    private Slider _healthSlider;
    private Slider _manaSlider;
    private TextMeshProUGUI _healthText;
    private TextMeshProUGUI _manaText;

    // Objetivos de animación (0..1)
    private float _healthTarget = 1f;
    private float _manaTarget = 1f;

    private Sprite _whiteSprite;

    private Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        _whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        return _whiteSprite;
    }

    void Awake()
    {
        CreateHUD();
        FindPlayerComponents();
    }

    void Start()
    {
        if (!_healthSystem || !_manaPool)
        {
            if (showDebugInfo)
                Debug.LogWarning("[PlayerHUD] No se encontraron HealthPool o ManaPool. Buscando en toda la escena...");
            StartCoroutine(FindComponentsDelayed());
        }
    }

    void Update()
    {
        UpdateHUD();
    }

    private void CreateHUD()
    {
        // Buscar Canvas principal o crear uno
        _canvas = FindObjectOfType<Canvas>();
        if (!_canvas)
        {
            var canvasGO = new GameObject("PlayerHUD_Canvas");
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Panel principal del HUD
        _hudPanel = new GameObject("PlayerHUD_Panel");
        _hudPanel.transform.SetParent(_canvas.transform, false);

        var hudRect = _hudPanel.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0, 1);
        hudRect.anchorMax = new Vector2(0, 1);
        hudRect.pivot = new Vector2(0, 1);
        hudRect.anchoredPosition = hudPosition;
        hudRect.sizeDelta = new Vector2(300, 120);

        // Background del panel
        var panelBg = _hudPanel.AddComponent<Image>();
        panelBg.color = backgroundColor;
        panelBg.raycastTarget = false;

        // Layout vertical
        var verticalLayout = _hudPanel.AddComponent<VerticalLayoutGroup>();
        verticalLayout.spacing = 10;
        verticalLayout.padding = new RectOffset(15, 15, 15, 15);
        verticalLayout.childControlHeight = false;
        verticalLayout.childControlWidth = true;
        verticalLayout.childForceExpandWidth = true;

        CreateHealthBar();
        CreateManaBar();

        // Añadir esquinas redondeadas si es posible
        AddRoundedCorners(_hudPanel);
    }

    private void CreateHealthBar()
    {
        // Contenedor de salud
        var healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(_hudPanel.transform, false);
        
        var healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 35);

        // Slider de salud
        var healthSliderGO = new GameObject("HealthSlider");
        healthSliderGO.transform.SetParent(healthContainer.transform, false);
        
        var healthSliderRect = healthSliderGO.AddComponent<RectTransform>();
        healthSliderRect.anchorMin = Vector2.zero;
        healthSliderRect.anchorMax = Vector2.one;
        healthSliderRect.offsetMin = new Vector2(0, 0);
        healthSliderRect.offsetMax = new Vector2(-80, 0);

        _healthSlider = healthSliderGO.AddComponent<Slider>();
        _healthSlider.interactable = false;

        // Background del slider
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(healthSliderGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = barBackgroundColor;
        bgImage.raycastTarget = false;
        if (bgImage.sprite == null) bgImage.sprite = GetWhiteSprite();
        _healthSlider.targetGraphic = bgImage;

        // Fill del slider
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(healthSliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = healthColor;
        fillImage.raycastTarget = false;
        if (fillImage.sprite == null) fillImage.sprite = GetWhiteSprite();
        _healthSlider.fillRect = fillRect;

        // Texto de salud
        var healthTextGO = new GameObject("HealthText");
        healthTextGO.transform.SetParent(healthContainer.transform, false);
        var healthTextRect = healthTextGO.AddComponent<RectTransform>();
        healthTextRect.anchorMin = new Vector2(1, 0);
        healthTextRect.anchorMax = new Vector2(1, 1);
        healthTextRect.pivot = new Vector2(1, 0.5f);
        healthTextRect.offsetMin = new Vector2(-75, 0);
        healthTextRect.offsetMax = new Vector2(0, 0);

        _healthText = healthTextGO.AddComponent<TextMeshProUGUI>();
        _healthText.text = "100/100";
        _healthText.fontSize = 14;
        _healthText.color = Color.white;
        _healthText.alignment = TextAlignmentOptions.MidlineRight;
        _healthText.raycastTarget = false;
    }

    private void CreateManaBar()
    {
        // Contenedor de maná
        var manaContainer = new GameObject("ManaContainer");
        manaContainer.transform.SetParent(_hudPanel.transform, false);
        
        var manaContainerRect = manaContainer.AddComponent<RectTransform>();
        manaContainerRect.sizeDelta = new Vector2(0, 35);

        // Slider de maná
        var manaSliderGO = new GameObject("ManaSlider");
        manaSliderGO.transform.SetParent(manaContainer.transform, false);
        
        var manaSliderRect = manaSliderGO.AddComponent<RectTransform>();
        manaSliderRect.anchorMin = Vector2.zero;
        manaSliderRect.anchorMax = Vector2.one;
        manaSliderRect.offsetMin = new Vector2(0, 0);
        manaSliderRect.offsetMax = new Vector2(-80, 0);

        _manaSlider = manaSliderGO.AddComponent<Slider>();
        _manaSlider.interactable = false;

        // Background del slider
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(manaSliderGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = barBackgroundColor;
        bgImage.raycastTarget = false;
        if (bgImage.sprite == null) bgImage.sprite = GetWhiteSprite();
        _manaSlider.targetGraphic = bgImage;

        // Fill del slider
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(manaSliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = manaColor;
        fillImage.raycastTarget = false;
        if (fillImage.sprite == null) fillImage.sprite = GetWhiteSprite();
        _manaSlider.fillRect = fillRect;

        // Texto de maná
        var manaTextGO = new GameObject("ManaText");
        manaTextGO.transform.SetParent(manaContainer.transform, false);
        var manaTextRect = manaTextGO.AddComponent<RectTransform>();
        manaTextRect.anchorMin = new Vector2(1, 0);
        manaTextRect.anchorMax = new Vector2(1, 1);
        manaTextRect.pivot = new Vector2(1, 0.5f);
        manaTextRect.offsetMin = new Vector2(-75, 0);
        manaTextRect.offsetMax = new Vector2(0, 0);

        _manaText = manaTextGO.AddComponent<TextMeshProUGUI>();
        _manaText.text = "50/50";
        _manaText.fontSize = 14;
        _manaText.color = Color.white;
        _manaText.alignment = TextAlignmentOptions.MidlineRight;
        _manaText.raycastTarget = false;
    }

    private void FindPlayerComponents()
    {
        // Buscar componentes en el player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            _healthSystem = player.GetComponent<PlayerHealthSystem>() ?? player.GetComponentInParent<PlayerHealthSystem>();
            _manaPool = player.GetComponent<ManaPool>() ?? player.GetComponentInParent<ManaPool>();
        }

        // Si no se encontraron, buscar en este GameObject
        if (!_healthSystem) _healthSystem = GetComponent<PlayerHealthSystem>() ?? GetComponentInParent<PlayerHealthSystem>();
        if (!_manaPool) _manaPool = GetComponent<ManaPool>() ?? GetComponentInParent<ManaPool>();

        SubscribeToEventsIfReady();
        InitTargetsIfReady();
    }

    private IEnumerator FindComponentsDelayed()
    {
        yield return new WaitForSeconds(1f);

        // Buscar en toda la escena como último recurso
        if (!_healthSystem) _healthSystem = FindObjectOfType<PlayerHealthSystem>();
        if (!_manaPool) _manaPool = FindObjectOfType<ManaPool>();

        if (showDebugInfo)
        {
            if (_healthSystem) Debug.Log("[PlayerHUD] PlayerHealthSystem encontrado");
            if (_manaPool) Debug.Log("[PlayerHUD] ManaPool encontrado");
        }

        SubscribeToEventsIfReady();
        InitTargetsIfReady();
    }

    private void SubscribeToEventsIfReady()
    {
        if (_healthSystem != null)
        {
            // Evitar doble suscripción
            _healthSystem.OnHealthChanged.RemoveListener(OnHealthPercentChanged);
            _healthSystem.OnHealthChanged.AddListener(OnHealthPercentChanged);
        }
        if (_manaPool != null)
        {
            _manaPool.OnManaChanged.RemoveListener(OnManaPercentChanged);
            _manaPool.OnManaChanged.AddListener(OnManaPercentChanged);
        }
    }

    private void InitTargetsIfReady()
    {
        if (_healthSystem)
        {
            _healthTarget = _healthSystem.HealthPercentage;
            if (_healthSlider) _healthSlider.value = _healthTarget;
        }
        if (_manaPool)
        {
            _manaTarget = (_manaPool.Max > 0f) ? (_manaPool.Current / _manaPool.Max) : 0f;
            if (_manaSlider) _manaSlider.value = _manaTarget;
        }
        // Sincronizar textos iniciales
        UpdateTexts();
    }

    // --- NUEVO: Forzar refresco desde las fuentes actuales (HealthSystem/ManaPool) ---
    public void ForceRefresh()
    {
        // Reasegurar referencias y suscripciones
        if (_healthSystem == null || _manaPool == null)
        {
            FindPlayerComponents();
        }
        else
        {
            SubscribeToEventsIfReady();
            InitTargetsIfReady();
        }
    }

    private void OnHealthPercentChanged(float percent)
    {
        _healthTarget = Mathf.Clamp01(percent);
    }

    private void OnManaPercentChanged(float percent)
    {
        _manaTarget = Mathf.Clamp01(percent);
    }

    private void UpdateHUD()
    {
        if (_healthSystem && _healthSlider && _healthText)
        {
            if (animateBars)
                _healthSlider.value = Mathf.Lerp(_healthSlider.value, _healthTarget, Mathf.Clamp01(barLerpSpeed * Time.deltaTime));
            else
                _healthSlider.value = _healthTarget;
        }

        if (_manaPool && _manaSlider && _manaText)
        {
            if (animateBars)
                _manaSlider.value = Mathf.Lerp(_manaSlider.value, _manaTarget, Mathf.Clamp01(barLerpSpeed * Time.deltaTime));
            else
                _manaSlider.value = _manaTarget;
        }

        UpdateTexts();
    }

    private void UpdateTexts()
    {
        if (_healthSystem && _healthText && _healthSlider)
        {
            float maxHp = _healthSystem.MaxHealth;
            float shownHp = Mathf.Round(_healthSlider.value * maxHp);
            _healthText.text = $"{shownHp:0}/{maxHp:0}";
        }
        if (_manaPool && _manaText && _manaSlider)
        {
            float maxMp = _manaPool.Max;
            float shownMp = Mathf.Round(_manaSlider.value * maxMp);
            _manaText.text = $"{shownMp:0}/{maxMp:0}";
        }
    }

    private void AddRoundedCorners(GameObject panel)
    {
        // Intentar añadir esquinas redondeadas si hay algún componente disponible
        // Esto es opcional y funcionará si tienes algún asset de UI con esquinas redondeadas
        var image = panel.GetComponent<Image>();
        if (image && image.sprite == null)
        {
            // Crear un sprite simple para background
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            image.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        }
    }

    void OnDestroy()
    {
        // Desuscripción segura
        if (_healthSystem)
            _healthSystem.OnHealthChanged.RemoveListener(OnHealthPercentChanged);
        if (_manaPool)
            _manaPool.OnManaChanged.RemoveListener(OnManaPercentChanged);

        // Limpiar el HUD cuando se destruya el componente
        if (_hudPanel) DestroyImmediate(_hudPanel);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_hudPanel && _hudPanel.GetComponent<RectTransform>())
        {
            _hudPanel.GetComponent<RectTransform>().anchoredPosition = hudPosition;
        }
    }
#endif
}
