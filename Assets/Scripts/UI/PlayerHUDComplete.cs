using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// HUD completo del player con salud, maná y slots de magia integrados.
/// Se crea automáticamente al añadir el script al GameObject.
/// Incluye colores dinámicos en la barra de vida y botones del mando Xbox.
/// </summary>
public class PlayerHUDComplete : MonoBehaviour
{
    [Header("Configuración General")]
    [SerializeField] private bool showDebugInfo;

    [Header("Posiciones")]
    [SerializeField] private Vector2 hudPosition = new Vector2(30, 30); // Esquina inferior izquierda: 30px desde la izquierda y 30px desde abajo

    [Header("Colores de Vida (Degradado según HP)")]
    [SerializeField] private Color healthColorHigh = new Color(0.2f, 0.8f, 0.2f, 1f); // Verde
    [SerializeField] private Color healthColorMid = new Color(0.9f, 0.7f, 0.2f, 1f); // Amarillo
    [SerializeField] private Color healthColorLow = new Color(0.9f, 0.2f, 0.2f, 1f); // Rojo
    [SerializeField] private Color manaColor = new Color(0.2f, 0.4f, 0.9f, 1f); // Azul

    [Header("Colores de Fondo")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.25f); // Mucho más transparente
    [SerializeField] private Color backgroundColorActive = new Color(0.2f, 0.2f, 0.2f, 0.6f); // Menos opaco
    [SerializeField] private Color backgroundColorInactive = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Menos opaco

    [Header("Slots de Magia")]
    [SerializeField] private float slotSize = 70f;
    [SerializeField] private float slotSpacing = 15f;
    [SerializeField] private Color availableColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color noManaColor = new Color(1f, 0.3f, 0.3f, 0.8f);

    [Header("Botones del Mando Xbox")]
    [SerializeField] private string leftButtonText = "X"; // Botón X (Oeste)
    [SerializeField] private string rightButtonText = "B"; // Botón B (Este)
    [SerializeField] private string upButtonText = "Y"; // Botón Y (Norte)
    [SerializeField] private Color xboxXColor = new Color(0.3f, 0.5f, 0.9f, 1f); // Azul
    [SerializeField] private Color xboxYColor = new Color(0.9f, 0.8f, 0.2f, 1f); // Amarillo
    [SerializeField] private Color xboxBColor = new Color(0.9f, 0.3f, 0.3f, 1f); // Rojo

    [Header("Canvas")]
    [Tooltip("Si está activado, el HUD siempre creará su propio Canvas en ScreenSpaceOverlay para evitar problemas de render en builds donde otros Canvas usan cámaras diferentes.")]
    [SerializeField] private bool forceCreateCanvas = true;

    // Nueva sección: referencias generadas desde el Editor
    [Header("Editor / Prebuilt UI (asignar desde Editor)")]
    [Tooltip("Opcional: si asignas aquí un panel raíz (generado por el editor), el script usará ese UI y NO creará elementos en tiempo de ejecución.")]
    [SerializeField] private GameObject editorRootPanel;
    [Tooltip("Opcional: Canvas asociado al UI preconstruido. Si se deja vacío, el script buscará el Canvas padre del root asignado.")]
    [SerializeField] private Canvas editorCanvas;

    // Referencias automáticas
    private PlayerHealthSystem _healthSystem;
    private ManaPool _manaPool;
    private MagicCaster _magicCaster;

    // Manejador para recibir updates inmediatos de maná
    private void OnManaChangedListener(float percent)
    {
        Debug.Log($"[PlayerHUDComplete] OnManaChangedListener called on HUD. ManaPool={( _manaPool != null ? _manaPool.gameObject.name : "null")}, percent={percent}");
        if (_manaSlider)
        {
            // Snap inmediato
            _manaSlider.value = percent;
            _manaSlider.gameObject.SetActive(true);
        }
        if (_manaText)
        {
            _manaText.gameObject.SetActive(true);
            _manaText.text = _manaPool != null ? $"{_manaPool.Current:0}/{_manaPool.Max:0}" : "0/0";
        }
        if (_manaFill)
        {
            _manaFill.color = (_manaPool != null && _manaPool.Max > 0f) ? manaColor : noManaColor;
        }
    }

    // UI Elements - Stats
    private Canvas _canvas;
    private GameObject _statsPanel;
    private Slider _healthSlider;
    private Slider _manaSlider;
    private Image _healthFill;
    private Image _manaFill;
    private TextMeshProUGUI _healthText;
    private TextMeshProUGUI _manaText;

    // UI Elements - Slots
    private GameObject _slotsPanel;
    private MagicSlotUI _leftSlot;
    private MagicSlotUI _rightSlot;
    private MagicSlotUI _upSlot;
    
    private bool _createdCanvas;     
    private GameObject _rootPanel;

    // Clase para manejar cada slot individual
    [System.Serializable]
    private class MagicSlotUI
    {
        public GameObject slotObject;
        public Image backgroundImage;
        public Image iconImage;
        public Image cooldownOverlay;
        public TextMeshProUGUI buttonText;
        public Image buttonBackground;
        public TextMeshProUGUI cooldownText;
        public MagicSlot slotType;
    }

    void Awake()
    {
        // Si el usuario asignó un UI preconstruido desde el Editor, usarlo y enlazar referencias.
        if (editorRootPanel != null)
        {
            _rootPanel = editorRootPanel;
            _canvas = editorCanvas != null ? editorCanvas : _rootPanel.GetComponentInParent<Canvas>();
            _createdCanvas = false;
            BindUIElementsFromRoot(_rootPanel);
            if (showDebugInfo) Debug.Log("[PlayerHUDComplete] Usando UI preconstruido asignado desde el Editor.");
        }
        else
        {
            CreateCompleteHUD();
        }

        FindPlayerComponents();

        // Garantizar actualización del HUD cuando el preset se aplique (p.ej. GameBootService llega después)
        PlayerPresetService.OnPresetApplied -= ForceRefresh;
        PlayerPresetService.OnPresetApplied += ForceRefresh;

        // Si el GameBootService ya está listo, forzar refresco ahora para evitar condiciones de carrera
        if (GameBootService.IsAvailable)
        {
            Debug.Log("[PlayerHUDComplete] GameBootService ya disponible en Awake -> ForceRefresh inmediato");
            ForceRefresh();
        }
    }

    void Start()
    {
        if (!_healthSystem || !_manaPool || !_magicCaster)
        {
            if (showDebugInfo)
                Debug.LogWarning("[PlayerHUDComplete] Faltan componentes. Buscando en toda la escena...");
            StartCoroutine(FindComponentsDelayed());
        }

        // Intentar hacer un refresh inmediato para evitar que en builds el HUD aparezca vacío
        ForceRefresh();
    }

    void Update()
    {
        UpdateStatsUI();
        UpdateSlotsUI();
    }

    private void CreateCompleteHUD()
    {
        // Si forzamos la creación de Canvas, intentamos reutilizar uno con nombre específico
        if (forceCreateCanvas)
        {
            var existing = GameObject.Find("PlayerHUD_Canvas");
            if (existing != null)
            {
                _canvas = existing.GetComponent<Canvas>();
                if (_canvas == null) _canvas = existing.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 1000;
                _createdCanvas = false; // no lo creamos nosotros ahora
                _rootPanel = CreateIntegratedHUD();
                // Si creamos aquí en runtime, enlazar elementos para uso inmediato
                BindUIElementsFromRoot(_rootPanel);
                return;
            }
            // si no existe lo creamos abajo como siempre
        }
         // Buscar Canvas existente que sea ScreenSpaceOverlay
         // Use la API moderna FindObjectsByType para evitar la advertencia de obsolescencia
         Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
         _canvas = null;
         foreach (var c in canvases)
         {
             if (c.renderMode == RenderMode.ScreenSpaceOverlay)
             {
                 _canvas = c;
                 break;
             }
         }

         // Si no hay un Canvas Overlay, crear uno propio para asegurar visibilidad en build
         if (_canvas == null)
         {
             var canvasGO = new GameObject("PlayerHUD_Canvas");
             _canvas = canvasGO.AddComponent<Canvas>();
             _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
             _canvas.sortingOrder = 1000; // asegurar que esté por encima
             var scaler = canvasGO.AddComponent<CanvasScaler>();
             scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
             scaler.referenceResolution = new Vector2(1920, 1080);
             canvasGO.AddComponent<GraphicRaycaster>();
             // Asegurarse que está en la layer UI si existe
             int uiLayer = LayerMask.NameToLayer("UI");
             if (uiLayer != -1) canvasGO.layer = uiLayer;
             _createdCanvas = true;
         }

         _rootPanel = CreateIntegratedHUD(); // ← guarda el panel raíz
        // Enlazar elementos creados dinámicamente
        BindUIElementsFromRoot(_rootPanel);
        // Forzar actualización de canvas para evitar que no se renderice en algunas plataformas
        Canvas.ForceUpdateCanvases();
        if (showDebugInfo) Debug.Log("[PlayerHUDComplete] HUD creado y Canvas actualizado.");
     }

    #region Integrated HUD (Todo en un panel)

    private GameObject  CreateIntegratedHUD()
    {
        // Panel principal que contiene TODO en VERTICAL
        var mainPanel = new GameObject("PlayerHUD_Main");
        mainPanel.transform.SetParent(_canvas.transform, false);

        var mainRect = mainPanel.AddComponent<RectTransform>();
        // Anclar al borde inferior-izquierdo
        mainRect.anchorMin = new Vector2(0, 0);
        mainRect.anchorMax = new Vector2(0, 0);
        mainRect.pivot = new Vector2(0, 0);
        mainRect.anchoredPosition = hudPosition;
        mainRect.sizeDelta = new Vector2(260, 180); // Más alto para acomodar todo verticalmente

        // Background principal con borde sutil
        var mainBg = mainPanel.AddComponent<Image>();
        mainBg.color = backgroundColor;
        mainBg.raycastTarget = false;

        // Borde exterior para dar profundidad
        var borderGO = new GameObject("MainBorder");
        borderGO.transform.SetParent(mainPanel.transform, false);
        borderGO.transform.SetAsFirstSibling();
        
        var borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);
        
        var borderImage = borderGO.AddComponent<Image>();
        borderImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        borderImage.raycastTarget = false;

        // Crear contenedor principal con layout vertical
        CreateVerticalLayout(mainPanel);
        return mainPanel; 
    }

    private void CreateVerticalLayout(GameObject parent)
    {
        // Contenedor con layout vertical para TODO
        var verticalContainer = new GameObject("VerticalContainer");
        verticalContainer.transform.SetParent(parent.transform, false);

        var containerRect = verticalContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = new Vector2(12, 12);
        containerRect.offsetMax = new Vector2(-12, -12);

        var verticalLayout = verticalContainer.AddComponent<VerticalLayoutGroup>();
        verticalLayout.spacing = 8;
        verticalLayout.padding = new RectOffset(0, 0, 0, 0);
        verticalLayout.childControlHeight = false;
        verticalLayout.childControlWidth = true;
        verticalLayout.childForceExpandWidth = true;
        // Alinear contenidos hacia la izquierda (mantener el orden de arriba hacia abajo)
        verticalLayout.childAlignment = TextAnchor.UpperLeft;

        // Crear las barras de salud y maná
        CreateHealthBarInContainer(verticalContainer);
        CreateManaBarInContainer(verticalContainer);
        
        // Crear los slots de magia debajo
        CreateSlotsRow(verticalContainer);
    }

    private void CreateHealthBarInContainer(GameObject parent)
    {
        var healthContainer = new GameObject("HealthContainer");
        healthContainer.transform.SetParent(parent.transform, false);
        
        var healthContainerRect = healthContainer.AddComponent<RectTransform>();
        healthContainerRect.sizeDelta = new Vector2(0, 38);

        // Slider de salud
        var healthSliderGO = new GameObject("HealthSlider");
        healthSliderGO.transform.SetParent(healthContainer.transform, false);
        
        var healthSliderRect = healthSliderGO.AddComponent<RectTransform>();
        healthSliderRect.anchorMin = Vector2.zero;
        healthSliderRect.anchorMax = Vector2.one;
        healthSliderRect.offsetMin = new Vector2(0, 0);
        healthSliderRect.offsetMax = new Vector2(0, 0);

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
        bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        bgImage.raycastTarget = false;
        _healthSlider.targetGraphic = bgImage;

        // Fill del slider
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(healthSliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(2, 2);
        fillAreaRect.offsetMax = new Vector2(-2, -2);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        _healthFill = fillGO.AddComponent<Image>();
        _healthFill.color = healthColorHigh;
        _healthFill.raycastTarget = false;
        _healthSlider.fillRect = fillRect;

        // Texto de salud (centrado en la barra)
        var healthTextGO = new GameObject("HealthText");
        healthTextGO.transform.SetParent(healthSliderGO.transform, false);
        var healthTextRect = healthTextGO.AddComponent<RectTransform>();
        healthTextRect.anchorMin = Vector2.zero;
        healthTextRect.anchorMax = Vector2.one;
        healthTextRect.offsetMin = Vector2.zero;
        healthTextRect.offsetMax = Vector2.zero;

        // Asegurar CanvasRenderer para TextMeshProUGUI (puede faltar en runtime/build)
        if (healthTextGO.GetComponent<CanvasRenderer>() == null) healthTextGO.AddComponent<CanvasRenderer>();
        _healthText = healthTextGO.AddComponent<TextMeshProUGUI>();
        _healthText.text = "100 / 100";
        _healthText.fontSize = 16;
        _healthText.fontStyle = FontStyles.Bold;
        _healthText.color = Color.white;
        _healthText.alignment = TextAlignmentOptions.Center;
        _healthText.raycastTarget = false;
        
        // Sombra para mejor legibilidad
        var shadow = healthTextGO.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        
        // Label "HP" en la esquina superior izquierda
        var hpLabelGO = new GameObject("HPLabel");
        hpLabelGO.transform.SetParent(healthSliderGO.transform, false);
        var hpLabelRect = hpLabelGO.AddComponent<RectTransform>();
        hpLabelRect.anchorMin = new Vector2(0, 1);
        hpLabelRect.anchorMax = new Vector2(0, 1);
        hpLabelRect.pivot = new Vector2(0, 1);
        hpLabelRect.anchoredPosition = new Vector2(5, 0);
        hpLabelRect.sizeDelta = new Vector2(30, 15);

        if (hpLabelGO.GetComponent<CanvasRenderer>() == null) hpLabelGO.AddComponent<CanvasRenderer>();
        var hpLabel = hpLabelGO.AddComponent<TextMeshProUGUI>();
        hpLabel.text = "HP";
        hpLabel.fontSize = 11;
        hpLabel.fontStyle = FontStyles.Bold;
        hpLabel.color = new Color(1f, 1f, 1f, 0.7f);
        hpLabel.alignment = TextAlignmentOptions.MidlineLeft;
        hpLabel.raycastTarget = false;
    }

    private void CreateManaBarInContainer(GameObject parent)
    {
        var manaContainer = new GameObject("ManaContainer");
        manaContainer.transform.SetParent(parent.transform, false);
        
        var manaContainerRect = manaContainer.AddComponent<RectTransform>();
        manaContainerRect.sizeDelta = new Vector2(0, 38);

        // Slider de maná
        var manaSliderGO = new GameObject("ManaSlider");
        manaSliderGO.transform.SetParent(manaContainer.transform, false);
        
        var manaSliderRect = manaSliderGO.AddComponent<RectTransform>();
        manaSliderRect.anchorMin = Vector2.zero;
        manaSliderRect.anchorMax = Vector2.one;
        manaSliderRect.offsetMin = new Vector2(0, 0);
        manaSliderRect.offsetMax = new Vector2(0, 0);

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
        bgImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        bgImage.raycastTarget = false;
        _manaSlider.targetGraphic = bgImage;

        // Fill del slider
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(manaSliderGO.transform, false);
        var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(2, 2);
        fillAreaRect.offsetMax = new Vector2(-2, -2);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        _manaFill = fillGO.AddComponent<Image>();
        _manaFill.color = manaColor;
        _manaFill.raycastTarget = false;
        _manaSlider.fillRect = fillRect;

        // Texto de maná (centrado en la barra)
        var manaTextGO = new GameObject("ManaText");
        manaTextGO.transform.SetParent(manaSliderGO.transform, false);
        var manaTextRect = manaTextGO.AddComponent<RectTransform>();
        manaTextRect.anchorMin = Vector2.zero;
        manaTextRect.anchorMax = Vector2.one;
        manaTextRect.offsetMin = Vector2.zero;
        manaTextRect.offsetMax = Vector2.zero;

        if (manaTextGO.GetComponent<CanvasRenderer>() == null) manaTextGO.AddComponent<CanvasRenderer>();
        _manaText = manaTextGO.AddComponent<TextMeshProUGUI>();
        _manaText.text = "50 / 50";
        _manaText.fontSize = 16;
        _manaText.fontStyle = FontStyles.Bold;
        _manaText.color = Color.white;
        _manaText.alignment = TextAlignmentOptions.Center;
        _manaText.raycastTarget = false;
        
        // Sombra para mejor legibilidad
        var shadow = manaTextGO.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Label "MP" en la esquina superior izquierda
        var mpLabelGO = new GameObject("MPLabel");
        mpLabelGO.transform.SetParent(manaSliderGO.transform, false);
        var mpLabelRect = mpLabelGO.AddComponent<RectTransform>();
        mpLabelRect.anchorMin = new Vector2(0, 1);
        mpLabelRect.anchorMax = new Vector2(0, 1);
        mpLabelRect.pivot = new Vector2(0, 1);
        mpLabelRect.anchoredPosition = new Vector2(5, 0);
        mpLabelRect.sizeDelta = new Vector2(30, 15);

        if (mpLabelGO.GetComponent<CanvasRenderer>() == null) mpLabelGO.AddComponent<CanvasRenderer>();
        var mpLabel = mpLabelGO.AddComponent<TextMeshProUGUI>();
        mpLabel.text = "MP";
        mpLabel.fontSize = 11;
        mpLabel.fontStyle = FontStyles.Bold;
        mpLabel.color = new Color(1f, 1f, 1f, 0.7f);
        mpLabel.alignment = TextAlignmentOptions.MidlineLeft;
        mpLabel.raycastTarget = false;
    }

    private void CreateSlotsRow(GameObject parent)
    {
        // Contenedor para los 3 slots en horizontal
        _slotsPanel = new GameObject("SlotsRow");
        _slotsPanel.transform.SetParent(parent.transform, false);

        var slotsRect = _slotsPanel.AddComponent<RectTransform>();
        slotsRect.sizeDelta = new Vector2(0, 60); // Altura fija para la fila de slots

        // Layout horizontal para los slots
        var horizontalLayout = _slotsPanel.AddComponent<HorizontalLayoutGroup>();
        horizontalLayout.spacing = 8;
        horizontalLayout.padding = new RectOffset(0, 0, 5, 5);
        horizontalLayout.childControlHeight = true;
        horizontalLayout.childControlWidth = true;
        horizontalLayout.childForceExpandHeight = false;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childAlignment = TextAnchor.MiddleCenter;

        // Crear los 3 slots en horizontal
        CreateSlotInRow(MagicSlot.Left, leftButtonText, xboxXColor, out _leftSlot);
        CreateSlotInRow(MagicSlot.Special, upButtonText, xboxYColor, out _upSlot);
        CreateSlotInRow(MagicSlot.Right, rightButtonText, xboxBColor, out _rightSlot);
    }

    private void CreateSlotInRow(MagicSlot slotType, string buttonText, Color buttonColor, out MagicSlotUI slotUI)
    {
        slotUI = new MagicSlotUI();
        slotUI.slotType = slotType;

        // GameObject principal del slot
        slotUI.slotObject = new GameObject($"Slot_{slotType}");
        slotUI.slotObject.transform.SetParent(_slotsPanel.transform, false);

        var slotRect = slotUI.slotObject.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(50, 50); // Tamaño fijo del slot

        // Layout element para controlar el tamaño en el HorizontalLayoutGroup
        var layoutElement = slotUI.slotObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 50;
        layoutElement.preferredHeight = 50;
        layoutElement.flexibleWidth = 0;
        layoutElement.flexibleHeight = 0;

        // Background del slot con borde
        var borderGO = new GameObject("Border");
        borderGO.transform.SetParent(slotUI.slotObject.transform, false);
        var borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        var borderImage = borderGO.AddComponent<Image>();
        borderImage.color = buttonColor;
        borderImage.raycastTarget = false;

        slotUI.backgroundImage = slotUI.slotObject.AddComponent<Image>();
        slotUI.backgroundImage.color = backgroundColorActive;
        slotUI.backgroundImage.raycastTarget = false;

        // Icono del hechizo
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slotUI.slotObject.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(5, 5);
        iconRect.offsetMax = new Vector2(-5, -5);

        slotUI.iconImage = iconGO.AddComponent<Image>();
        slotUI.iconImage.color = availableColor;
        slotUI.iconImage.raycastTarget = false;
        slotUI.iconImage.sprite = CreateDefaultSpellIcon();

        // Overlay de cooldown
        var cooldownGO = new GameObject("CooldownOverlay");
        cooldownGO.transform.SetParent(slotUI.slotObject.transform, false);
        var cooldownRect = cooldownGO.AddComponent<RectTransform>();
        cooldownRect.anchorMin = Vector2.zero;
        cooldownRect.anchorMax = Vector2.one;
        cooldownRect.offsetMin = new Vector2(2, 2);
        cooldownRect.offsetMax = new Vector2(-2, -2);

        slotUI.cooldownOverlay = cooldownGO.AddComponent<Image>();
        slotUI.cooldownOverlay.color = new Color(0f, 0f, 0f, 0.8f);
        slotUI.cooldownOverlay.raycastTarget = false;
        slotUI.cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
        slotUI.cooldownOverlay.type = Image.Type.Filled;
        slotUI.cooldownOverlay.fillOrigin = 2;
        slotUI.cooldownOverlay.gameObject.SetActive(false);

        // Badge del botón (arriba a la derecha)
        var buttonBadgeGO = new GameObject("ButtonBadge");
        buttonBadgeGO.transform.SetParent(slotUI.slotObject.transform, false);
        var buttonBadgeRect = buttonBadgeGO.AddComponent<RectTransform>();
        buttonBadgeRect.anchorMin = new Vector2(1, 1);
        buttonBadgeRect.anchorMax = new Vector2(1, 1);
        buttonBadgeRect.pivot = new Vector2(1, 1);
        buttonBadgeRect.anchoredPosition = new Vector2(2, 2);
        buttonBadgeRect.sizeDelta = new Vector2(18, 18);

        slotUI.buttonBackground = buttonBadgeGO.AddComponent<Image>();
        slotUI.buttonBackground.color = buttonColor;
        slotUI.buttonBackground.raycastTarget = false;

        // Texto del botón
        var buttonTextGO = new GameObject("ButtonText");
        buttonTextGO.transform.SetParent(buttonBadgeGO.transform, false);
        var buttonTextRect = buttonTextGO.AddComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        if (buttonTextGO.GetComponent<CanvasRenderer>() == null) buttonTextGO.AddComponent<CanvasRenderer>();
        slotUI.buttonText = buttonTextGO.AddComponent<TextMeshProUGUI>();
        slotUI.buttonText.text = buttonText;
        slotUI.buttonText.fontSize = 12;
        slotUI.buttonText.fontStyle = FontStyles.Bold;
        slotUI.buttonText.color = Color.white;
        slotUI.buttonText.alignment = TextAlignmentOptions.Center;
        slotUI.buttonText.raycastTarget = false;

        // Texto de cooldown
        var cooldownTextGO = new GameObject("CooldownText");
        cooldownTextGO.transform.SetParent(slotUI.slotObject.transform, false);
        var cooldownTextRect = cooldownTextGO.AddComponent<RectTransform>();
        cooldownTextRect.anchorMin = Vector2.zero;
        cooldownTextRect.anchorMax = Vector2.one;
        cooldownTextRect.offsetMin = Vector2.zero;
        cooldownTextRect.offsetMax = Vector2.zero;

        if (cooldownTextGO.GetComponent<CanvasRenderer>() == null) cooldownTextGO.AddComponent<CanvasRenderer>();
        slotUI.cooldownText = cooldownTextGO.AddComponent<TextMeshProUGUI>();
        slotUI.cooldownText.text = "";
        slotUI.cooldownText.fontSize = 18;
        slotUI.cooldownText.fontStyle = FontStyles.Bold;
        slotUI.cooldownText.color = Color.white;
        slotUI.cooldownText.alignment = TextAlignmentOptions.Center;
        slotUI.cooldownText.raycastTarget = false;
        slotUI.cooldownText.gameObject.SetActive(false);

        // Sombra al texto de cooldown
        var cdShadow = cooldownTextGO.AddComponent<UnityEngine.UI.Shadow>();
        cdShadow.effectColor = new Color(0, 0, 0, 0.9f);
        cdShadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void UpdateStatsUI()
    {
        // Actualizar salud con colores degradados
        if (_healthSystem && _healthSlider && _healthText && _healthFill)
        {
            float healthPercent = _healthSystem.HealthPercentage;
            _healthSlider.value = Mathf.Lerp(_healthSlider.value, healthPercent, Time.deltaTime * 8f);
            _healthText.text = $"{_healthSystem.CurrentHealth:0}/{_healthSystem.MaxHealth:0}";

            // Color degradado según el porcentaje de vida
            Color targetColor;
            if (healthPercent > 0.6f)
            {
                // Verde a Amarillo (60% - 100%)
                float t = (healthPercent - 0.6f) / 0.4f;
                targetColor = Color.Lerp(healthColorMid, healthColorHigh, t);
            }
            else if (healthPercent > 0.3f)
            {
                // Amarillo a Rojo (30% - 60%)
                float t = (healthPercent - 0.3f) / 0.3f;
                targetColor = Color.Lerp(healthColorLow, healthColorMid, t);
            }
            else
            {
                // Rojo (0% - 30%)
                targetColor = healthColorLow;
                
                // Pulso cuando HP crítico
                if (healthPercent > 0f && healthPercent < 0.25f)
                {
                    float pulse = Mathf.PingPong(Time.time * 2f, 1f);
                    targetColor = Color.Lerp(healthColorLow, Color.white, pulse * 0.3f);
                }
            }

            _healthFill.color = Color.Lerp(_healthFill.color, targetColor, Time.deltaTime * 6f);
        }

        // Maná (con guardia de división)
        // Ocultar la UI de maná si el preset indica que el jugador NO tiene magia
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset != null && preset.abilities != null && !preset.abilities.magic)
        {
            // Mostrar la barra pero con valores 0/0 para indicar que no hay magia
            if (_manaSlider)
            {
                _manaSlider.gameObject.SetActive(true);
                _manaSlider.value = 0f;
            }
            if (_manaText)
            {
                _manaText.gameObject.SetActive(true);
                _manaText.text = "0/0";
            }
            if (_manaFill) _manaFill.color = noManaColor;

            return;
        }

        if (_manaPool && _manaSlider && _manaText)
        {
            // Si el ManaPool existe pero Max es 0 o negativo, forzar 0/0 (protección contra división por cero)
            if (_manaPool.Max <= 0f)
            {
                if (_manaSlider)
                {
                    _manaSlider.gameObject.SetActive(true);
                    _manaSlider.value = 0f;
                }
                if (_manaText)
                {
                    _manaText.gameObject.SetActive(true);
                    _manaText.text = "0/0";
                }
                if (_manaFill) _manaFill.color = noManaColor;
            }
            else
            {
                float denom = Mathf.Max(0.0001f, _manaPool.Max);
                float manaPercent = _manaPool.Current / denom;
                _manaSlider.gameObject.SetActive(true);
                _manaText.gameObject.SetActive(true);
                _manaSlider.value = Mathf.Lerp(_manaSlider.value, manaPercent, Time.deltaTime * 8f);
                _manaText.text = $"{_manaPool.Current:0}/{_manaPool.Max:0}";
            }
        }
    }

    public void ForceRefresh()
    {
        Debug.Log("[PlayerHUDComplete] ForceRefresh called");
        var presetDbg = GameBootService.Profile?.GetActivePresetResolved();
        Debug.Log($"[PlayerHUDComplete] preset abilities.magic={(presetDbg!=null && presetDbg.abilities!=null?presetDbg.abilities.magic.ToString():"null")}");
        // Re-resolver referencias por si no estuvieran aún
        if (_healthSystem == null) _healthSystem = UnityEngine.Object.FindFirstObjectByType<PlayerHealthSystem>();
        if (_manaPool == null) _manaPool = UnityEngine.Object.FindFirstObjectByType<ManaPool>();
        if (_magicCaster == null) _magicCaster = UnityEngine.Object.FindFirstObjectByType<MagicCaster>();
        Debug.Log($"[PlayerHUDComplete] ForceRefresh: found ManaPool={( _manaPool != null ? _manaPool.gameObject.name : "null")}, values={( _manaPool != null ? _manaPool.Current + "/" + _manaPool.Max : "n/a")} ");

        // Salud (snap inmediato)
        if (_healthSystem && _healthSlider && _healthText)
        {
            float hp = _healthSystem.HealthPercentage;
            _healthSlider.value = hp;
            _healthText.text = $"{_healthSystem.CurrentHealth:0}/{_healthSystem.MaxHealth:0}";
        }

        // Maná (snap inmediato)
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        // Si el ManaPool existe y su Max es 0 -> forzar 0/0 (protección adicional)
        if (_manaPool != null && _manaPool.Max <= 0f)
        {
            if (_manaSlider)
            {
                _manaSlider.gameObject.SetActive(true);
                _manaSlider.value = 0f;
            }
            if (_manaText)
            {
                _manaText.gameObject.SetActive(true);
                _manaText.text = "0/0";
            }
            if (_manaFill) _manaFill.color = noManaColor;
            Debug.Log("[PlayerHUDComplete] ManaPool reports Max<=0 -> HUD set to 0/0");
            return;
        }

        if (preset != null && preset.abilities != null && !preset.abilities.magic)
        {
            // Mostrar la barra pero como 0/0 para indicar ausencia de magia
            if (_manaSlider)
            {
                _manaSlider.gameObject.SetActive(true);
                _manaSlider.value = 0f;
            }
            if (_manaText)
            {
                _manaText.gameObject.SetActive(true);
                _manaText.text = "0/0";
            }
            if (_manaFill) _manaFill.color = noManaColor;
            Debug.Log("[PlayerHUDComplete] Preset indicates no magic -> HUD set to 0/0");
        }
        else
        {
            if (_manaPool && _manaSlider && _manaText)
            {
                float mp = _manaPool.Max > 0f ? _manaPool.Current / _manaPool.Max : 0f;
                _manaSlider.gameObject.SetActive(true);
                _manaText.gameObject.SetActive(true);
                _manaSlider.value = mp;
                _manaText.text = $"{_manaPool.Current:0}/{_manaPool.Max:0}";
                Debug.Log($"[PlayerHUDComplete] HUD populated from ManaPool: {_manaPool.gameObject.name} -> {_manaPool.Current}/{_manaPool.Max}");
            }
        }
    }


    #endregion

    #region Slots Panel (Magia)

    private void UpdateSlotsUI()
    {
        if (!_magicCaster || !_manaPool) return;

        UpdateSlot(_leftSlot);
        UpdateSlot(_rightSlot);
        UpdateSlot(_upSlot);
    }

    private void UpdateSlot(MagicSlotUI slot)
    {
        if (slot?.slotObject == null) return;

        var spell = _magicCaster.GetSpellForSlot(slot.slotType);
        bool hasSpell = spell != null;
        bool canCast = _magicCaster.CanCastSpell(slot.slotType);
        bool isOnCooldown = _magicCaster.IsOnCooldown(slot.slotType);
        float cooldownTime = _magicCaster.GetCooldownTime(slot.slotType);
        bool hasEnoughMana = hasSpell && _manaPool.Current >= spell.manaCost;

        // Actualizar visibilidad del slot
        slot.slotObject.SetActive(hasSpell);
        if (!hasSpell) return;

        // Actualizar colores con transiciones suaves
        Color targetIconColor = availableColor;
        Color targetBgColor = backgroundColorActive;
        
        if (!canCast)
        {
            if (isOnCooldown)
            {
                targetIconColor = cooldownColor;
                targetBgColor = backgroundColorInactive;
            }
            else if (!hasEnoughMana)
            {
                targetIconColor = noManaColor;
                targetBgColor = backgroundColorInactive;
            }
            else
            {
                targetIconColor = cooldownColor;
                targetBgColor = backgroundColorInactive;
            }
        }
        
        slot.iconImage.color = Color.Lerp(slot.iconImage.color, targetIconColor, Time.deltaTime * 8f);
        slot.backgroundImage.color = Color.Lerp(slot.backgroundImage.color, targetBgColor, Time.deltaTime * 8f);

        // Actualizar cooldown visual
        if (isOnCooldown && cooldownTime > 0)
        {
            slot.cooldownOverlay.gameObject.SetActive(true);
            slot.cooldownText.gameObject.SetActive(true);
            
            float maxCooldown = spell.cooldown;
            float cooldownPercent = cooldownTime / maxCooldown;
            slot.cooldownOverlay.fillAmount = cooldownPercent;
            
            if (cooldownTime < 1f)
                slot.cooldownText.text = cooldownTime.ToString("F1");
            else
                slot.cooldownText.text = Mathf.CeilToInt(cooldownTime).ToString();
            
            // Pulso al final
            if (cooldownTime < 0.5f)
            {
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                float scale = 1f + pulse * 0.12f;
                slot.slotObject.transform.localScale = Vector3.one * scale;
                slot.iconImage.color = Color.Lerp(targetIconColor, Color.white, pulse * 0.4f);
            }
            else
            {
                slot.slotObject.transform.localScale = Vector3.Lerp(
                    slot.slotObject.transform.localScale, Vector3.one, Time.deltaTime * 10f);
            }
        }
        else
        {
            slot.cooldownOverlay.gameObject.SetActive(false);
            slot.cooldownText.gameObject.SetActive(false);
            slot.slotObject.transform.localScale = Vector3.Lerp(
                slot.slotObject.transform.localScale, Vector3.one, Time.deltaTime * 10f);
            
            // Flash cuando está listo
            if (canCast && slot.iconImage.color != availableColor)
            {
                StartCoroutine(FlashSlotReady(slot));
            }
        }
    }

    private IEnumerator FlashSlotReady(MagicSlotUI slot)
    {
        if (slot?.slotObject == null) yield break;
        
        Color originalColor = slot.iconImage.color;
        slot.iconImage.color = Color.white;
        slot.slotObject.transform.localScale = Vector3.one * 1.15f;
        
        yield return new WaitForSeconds(0.08f);
        
        float elapsed = 0f;
        float duration = 0.12f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            slot.iconImage.color = Color.Lerp(Color.white, originalColor, t);
            slot.slotObject.transform.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one, t);
            yield return null;
        }
        
        slot.iconImage.color = originalColor;
        slot.slotObject.transform.localScale = Vector3.one;
    }

    #endregion

    private void FindPlayerComponents()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            _healthSystem = player.GetComponent<PlayerHealthSystem>() ?? player.GetComponentInParent<PlayerHealthSystem>();
            _manaPool = player.GetComponent<ManaPool>() ?? player.GetComponentInParent<ManaPool>();
            _magicCaster = player.GetComponent<MagicCaster>() ?? player.GetComponentInParent<MagicCaster>();
        }

        if (!_healthSystem) _healthSystem = GetComponent<PlayerHealthSystem>() ?? GetComponentInParent<PlayerHealthSystem>();
        if (!_manaPool) _manaPool = GetComponent<ManaPool>() ?? GetComponentInParent<ManaPool>();
        if (!_magicCaster) _magicCaster = GetComponent<MagicCaster>() ?? GetComponentInParent<MagicCaster>();

        // Suscribir al evento de cambios de maná para que la UI se actualice inmediatamente
        if (_manaPool != null)
        {
            // Evitar múltiples suscripciones
            _manaPool.OnManaChanged.RemoveListener(OnManaChangedListener);
            _manaPool.OnManaChanged.AddListener(OnManaChangedListener);
        }
    }

    private IEnumerator FindComponentsDelayed()
    {
        yield return new WaitForSeconds(1f);

        if (!_healthSystem) _healthSystem = UnityEngine.Object.FindFirstObjectByType<PlayerHealthSystem>();
        if (!_manaPool) _manaPool = UnityEngine.Object.FindFirstObjectByType<ManaPool>();
        if (!_magicCaster) _magicCaster = UnityEngine.Object.FindFirstObjectByType<MagicCaster>();

        if (showDebugInfo)
        {
            Debug.Log($"[PlayerHUDComplete] Componentes encontrados - Health:{_healthSystem != null} Mana:{_manaPool != null} Caster:{_magicCaster != null}");
        }

        // Suscribir al evento de cambios de maná si lo encontramos ahora
        if (_manaPool != null)
        {
            _manaPool.OnManaChanged.RemoveListener(OnManaChangedListener);
            _manaPool.OnManaChanged.AddListener(OnManaChangedListener);
        }

        // Una vez encontrados, forzar refresco para que el HUD muestre valores en build
        ForceRefresh();
     }

    private Sprite CreateDefaultSpellIcon()
    {
        var texture = new Texture2D(64, 64);
        var center = new Vector2(32, 32);
        
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= 28)
                {
                    float alpha = 1f - (distance / 28f) * 0.3f;
                    texture.SetPixel(x, y, new Color(0.9f, 0.5f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
    }

    // Encuentra recursivamente un hijo por nombre bajo un transform
    private Transform FindRecursive(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            var found = FindRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }
    
    // Enlaza componentes UI existentes bajo un root (usado cuando el UI se crea desde el Editor)
    private void BindUIElementsFromRoot(GameObject root)
    {
        if (root == null) return;

        // Health Slider
        var hs = FindRecursive(root.transform, "HealthSlider");
        if (hs) _healthSlider = hs.GetComponent<Slider>();
        var hf = FindRecursive(root.transform, "Fill");
        if (hf) _healthFill = hf.GetComponent<Image>();
        var ht = FindRecursive(root.transform, "HealthText");
        if (ht) _healthText = ht.GetComponent<TextMeshProUGUI>();

        // Mana Slider
        var ms = FindRecursive(root.transform, "ManaSlider");
        if (ms) _manaSlider = ms.GetComponent<Slider>();
        var mf = FindRecursive(root.transform, "Fill");
        // Note: "Fill" may be duplicated; prefer the one under Mana if possible
        if (mf && _manaSlider != null)
        {
            var candidate = ms.Find("Fill Area/Fill");
            if (candidate) _manaFill = candidate.GetComponent<Image>();
        }
        var mt = FindRecursive(root.transform, "ManaText");
        if (mt) _manaText = mt.GetComponent<TextMeshProUGUI>();

        // Slots
        var slots = FindRecursive(root.transform, "SlotsRow");
        if (slots) _slotsPanel = slots.gameObject;

        // Crear contenedores de slot y enlazar subcomponentes (si existen)
        _leftSlot = new MagicSlotUI();
        _rightSlot = new MagicSlotUI();
        _upSlot = new MagicSlotUI();

        var leftGO = FindRecursive(root.transform, "Slot_Left");
        if (leftGO) BindSlotFromGO(_leftSlot, leftGO.gameObject, MagicSlot.Left);
        var rightGO = FindRecursive(root.transform, "Slot_Right");
        if (rightGO) BindSlotFromGO(_rightSlot, rightGO.gameObject, MagicSlot.Right);
        var upGO = FindRecursive(root.transform, "Slot_Special");
        if (upGO) BindSlotFromGO(_upSlot, upGO.gameObject, MagicSlot.Special);

        if (showDebugInfo)
            Debug.Log($"[PlayerHUDComplete] BindUIElementsFromRoot - Health:{_healthSlider!=null} Mana:{_manaSlider!=null} Slots:{_slotsPanel!=null}");
     }

    private void BindSlotFromGO(MagicSlotUI slotUI, GameObject go, MagicSlot type)
    {
        if (go == null) return;
        slotUI.slotObject = go;
        slotUI.slotType = type;
        slotUI.backgroundImage = go.GetComponent<Image>();
        var iconT = FindRecursive(go.transform, "Icon");
        if (iconT) slotUI.iconImage = iconT.GetComponent<Image>();
        var coolT = FindRecursive(go.transform, "CooldownOverlay");
        if (coolT) slotUI.cooldownOverlay = coolT.GetComponent<Image>();
        var btnTextT = FindRecursive(go.transform, "ButtonText");
        if (btnTextT) slotUI.buttonText = btnTextT.GetComponent<TextMeshProUGUI>();
        var btnBgT = FindRecursive(go.transform, "ButtonBadge");
        if (btnBgT) slotUI.buttonBackground = btnBgT.GetComponent<Image>();
        var cdTextT = FindRecursive(go.transform, "CooldownText");
        if (cdTextT) slotUI.cooldownText = cdTextT.GetComponent<TextMeshProUGUI>();
    }

    void OnDestroy()
    {
        // Si el root fue asignado desde el Editor (editorRootPanel != null), no lo destruimos.
        if (_createdCanvas && _canvas) DestroyImmediate(_canvas.gameObject);
         if (_rootPanel != null && editorRootPanel == null)
         {
             DestroyImmediate(_rootPanel);
         }

        // Limpiar suscripción de maná
        if (_manaPool != null)
        {
            _manaPool.OnManaChanged.RemoveListener(OnManaChangedListener);
        }

        // Limpiar suscripción al preset
        PlayerPresetService.OnPresetApplied -= ForceRefresh;
    }

}
