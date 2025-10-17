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
    private PlayerActionManager _actionManager;
    private PlayerAbilities _playerAbilities;

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
            // Si el jugador NO tiene la ability de magia, mostrar color de no-mana
            if (!HasMagicAbility()) _manaFill.color = noManaColor;
            else _manaFill.color = (_manaPool != null && _manaPool.Max > 0f) ? manaColor : noManaColor;
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

        // Inicializar la UI de maná a un estado seguro (0/0, color no-mana) para evitar que se muestre el valor serializado
        // (por ejemplo 50/50) antes de que el preset/PlayerPresetService aplique los valores apropiados.
        if (_manaSlider != null)
        {
            _manaSlider.gameObject.SetActive(true);
            _manaSlider.value = 0f;
        }
        if (_manaText != null)
        {
            _manaText.gameObject.SetActive(true);
            _manaText.text = "0/0";
        }
        if (_manaFill != null)
        {
            _manaFill.color = noManaColor;
        }

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
        bgImage.sprite = CreateDefaultSpellIcon();
        bgImage.type = Image.Type.Sliced;
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
        _healthFill.sprite = CreateDefaultSpellIcon();
        _healthFill.type = Image.Type.Sliced;
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
        bgImage.sprite = CreateDefaultSpellIcon();
        bgImage.type = Image.Type.Sliced;
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
        _manaFill.sprite = CreateDefaultSpellIcon();
        _manaFill.type = Image.Type.Sliced;
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
        // Si el jugador NO tiene la ability de magia (detectada por varias fuentes), mostrar 0/0 y color noManaColor
        if (!HasMagicAbility())
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
        Debug.Log($"[PlayerHUDComplete] preset abilities.magic={(presetDbg!=null && presetDbg.abilities!=null?presetDbg.abilities.magic.ToString():"null")} ");
        // Re-resolver referencias por si no estuvieran aún
        if (_healthSystem == null) _healthSystem = UnityEngine.Object.FindFirstObjectByType<PlayerHealthSystem>();
        if (_manaPool == null) _manaPool = UnityEngine.Object.FindFirstObjectByType<ManaPool>();
        if (_magicCaster == null) _magicCaster = UnityEngine.Object.FindFirstObjectByType<MagicCaster>();
        if (_actionManager == null) _actionManager = UnityEngine.Object.FindFirstObjectByType<PlayerActionManager>();
        if (_playerAbilities == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) _playerAbilities = ResolvePlayerAbilitiesFromHierarchy(playerGo);
        }
        Debug.Log($"[PlayerHUDComplete] ForceRefresh: found ManaPool={( _manaPool != null ? _manaPool.gameObject.name : "null")}, values={( _manaPool != null ? _manaPool.Current + "/" + _manaPool.Max : "n/a")} ");

        // Salud (snap inmediato)
        if (_healthSystem && _healthSlider && _healthText)
        {
            float hp = _healthSystem.HealthPercentage;
            _healthSlider.value = hp;
            _healthText.text = $"{_healthSystem.CurrentHealth:0}/{_healthSystem.MaxHealth:0}";
        }

        // Maná (snap inmediato)
        // Si el jugador NO tiene la ability de magia, mostrar 0/0
        if (!HasMagicAbility())
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
            Debug.Log("[PlayerHUDComplete] Player lacks magic ability -> HUD set to 0/0");
            return;
        }

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

        // Si el preset explícitamente indica no magia, ya lo cubre HasMagicAbility() arriba
         else
         {
             if (_manaPool && _manaSlider && _manaText)
             {
                 float mp = _manaPool.Max > 0f ? _manaPool.Current / _manaPool.Max : 0f;
                 _manaSlider.gameObject.SetActive(true);
                 _manaText.gameObject.SetActive(true);
                 _manaSlider.value = mp;
                 _manaText.text = $"{_manaPool.Current:0}/{_manaPool.Max:0}";
                 // Actualizar color del fill según tenga maná o no
                if (_manaFill)
                {
                    _manaFill.color = (_manaPool != null && _manaPool.Max > 0f && _manaPool.Current > 0f) ? manaColor : noManaColor;
                }
                 Debug.Log($"[PlayerHUDComplete] HUD populated from ManaPool: {_manaPool.gameObject.name} -> {_manaPool.Current}/{_manaPool.Max}");
             }
         }
     }

    // Determina si el jugador tiene la habilidad de magia consultando:
    // 1) PlayerActionManager.AllowMagic si existe
    // 2) El preset activo (GameBootService.Profile)
    // 3) Componente PlayerAbilities en el Player GameObject
    private bool HasMagicAbility()
    {
        // Preferir la fuente runtime que aplica presets al jugador
        if (_actionManager == null)
        {
            _actionManager = UnityEngine.Object.FindFirstObjectByType<PlayerActionManager>();
            // intentar localizar en player si no se encontró globalmente
            if (_actionManager == null)
            {
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null) _actionManager = playerGo.GetComponent<PlayerActionManager>() ?? playerGo.GetComponentInParent<PlayerActionManager>();
            }
        }
        if (_actionManager != null) return _actionManager.AllowMagic;

        // Comprobar preset activo
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset != null && preset.abilities != null)
        {
            return preset.abilities.magic;
        }

        // Consultar PlayerAbilities (Scriptable/POCO) desde preset o desde componentes que lo expongan
        if (_playerAbilities == null)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) _playerAbilities = ResolvePlayerAbilitiesFromHierarchy(playerGo);
        }
        if (_playerAbilities != null) return _playerAbilities.magic;

        // Por defecto, asumir que sí tiene magia (compatibilidad hacia atrás)
        return true;
    }

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
        // Implementación defensiva mínima: no asume APIs concretas del MagicCaster
        if (slot == null || slot.slotObject == null) return;

        // Asegurarse de que los componentes básicos existan
        if (slot.iconImage == null)
        {
            // intentar localizar un Image llamado "Icon" en los hijos
            var iconT = slot.slotObject.transform.Find("Icon");
            if (iconT != null) slot.iconImage = iconT.GetComponent<Image>();
        }

        // Si no hay sprite asignado, usar uno por defecto para evitar errores en runtime
        if (slot.iconImage != null && slot.iconImage.sprite == null)
        {
            slot.iconImage.sprite = CreateDefaultSpellIcon();
            slot.iconImage.color = availableColor;
        }

        // Desactivar overlay de cooldown por defecto (implementación simple)
        if (slot.cooldownOverlay != null)
        {
            slot.cooldownOverlay.gameObject.SetActive(false);
            slot.cooldownOverlay.fillAmount = 0f;
        }
        if (slot.cooldownText != null)
        {
            slot.cooldownText.gameObject.SetActive(false);
            slot.cooldownText.text = "";
        }

        // Asignar texto del botón (si está presente) según el tipo de slot
        if (slot.buttonText != null)
        {
            switch (slot.slotType)
            {
                case MagicSlot.Left:
                    slot.buttonText.text = leftButtonText;
                    if (slot.buttonBackground != null) slot.buttonBackground.color = xboxXColor;
                    break;
                case MagicSlot.Right:
                    slot.buttonText.text = rightButtonText;
                    if (slot.buttonBackground != null) slot.buttonBackground.color = xboxBColor;
                    break;
                case MagicSlot.Special:
                    slot.buttonText.text = upButtonText;
                    if (slot.buttonBackground != null) slot.buttonBackground.color = xboxYColor;
                    break;
                default:
                    slot.buttonText.text = "";
                    break;
            }
        }

        // Si no hay MagicCaster o ManaPool no continuamos
        if (_magicCaster == null || _manaPool == null) return;

        // Obtener estado del slot desde MagicCaster
        var spell = _magicCaster.GetSpellForSlot(slot.slotType);
        bool hasSpell = spell != null;
        if (!hasSpell)
        {
            // No hay hechizo asignado: ocultar icono y elementos relacionados
            slot.slotObject.SetActive(false);
            return;
        }
        slot.slotObject.SetActive(true);

        // Determinar si se puede lanzar (usar overload que devuelve motivo)
        bool canCast = _magicCaster.CanCastSpell(slot.slotType, spell, out string reason);
        bool isOnCooldown = _magicCaster.IsOnCooldown(slot.slotType);
        float cooldownTime = _magicCaster.GetCooldownTime(slot.slotType);
        bool hasEnoughMana = _manaPool.Current >= spell.manaCost;

        // Colores objetivo
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
                // bloqueado por otra razón (ej. acción bloqueada)
                targetIconColor = cooldownColor;
                targetBgColor = backgroundColorInactive;
            }
        }

        // Aplicar colores suavemente
        if (slot.iconImage != null)
            slot.iconImage.color = Color.Lerp(slot.iconImage.color, targetIconColor, Time.deltaTime * 8f);
        if (slot.backgroundImage != null)
            slot.backgroundImage.color = Color.Lerp(slot.backgroundImage.color, targetBgColor, Time.deltaTime * 8f);

        // Actualizar icono: si en el futuro MagicSpellSO expone un sprite podríamos asignarlo aquí.
        // Ahora coloreamos según el elemento como diferenciador (si no hay sprite concreto)
        if (slot.iconImage != null && spell != null)
        {
            // si se añadiera spell.iconSprite en el futuro, preferirlo:
            // if (spell.iconSprite != null) slot.iconImage.sprite = spell.iconSprite;

            // pequeña heurística: tintar según elemento
            switch (spell.element)
            {
                case MagicElement.Fire:
                    slot.iconImage.color = Color.Lerp(slot.iconImage.color, new Color(1f, 0.6f, 0.2f, 1f), Time.deltaTime * 6f);
                    break;
                case MagicElement.Ice:
                    slot.iconImage.color = Color.Lerp(slot.iconImage.color, new Color(0.6f, 0.8f, 1f, 1f), Time.deltaTime * 6f);
                    break;
                case MagicElement.Light:
                    slot.iconImage.color = Color.Lerp(slot.iconImage.color, new Color(1f, 1f, 0.8f, 1f), Time.deltaTime * 6f);
                    break;
                default:
                    // mantener el color actual / availableColor
                    break;
            }
        }

        // Cooldown visual
        if (isOnCooldown && cooldownTime > 0f)
        {
            if (slot.cooldownOverlay != null) slot.cooldownOverlay.gameObject.SetActive(true);
            if (slot.cooldownText != null) slot.cooldownText.gameObject.SetActive(true);

            float maxCooldown = Mathf.Max(0.0001f, spell.cooldown);
            float cooldownPercent = Mathf.Clamp01(cooldownTime / maxCooldown);

            if (slot.cooldownOverlay != null) slot.cooldownOverlay.fillAmount = cooldownPercent;

            // Mostrar tiempo: 1 decimal si < 1s, entero si >=1
            if (slot.cooldownText != null)
            {
                slot.cooldownText.text = cooldownTime < 1f ? cooldownTime.ToString("F1") : Mathf.CeilToInt(cooldownTime).ToString();
            }

            // Pulso final cuando cooldown < 0.5s
            if (cooldownTime < 0.5f)
            {
                float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                float scale = 1f + pulse * 0.12f;
                slot.slotObject.transform.localScale = Vector3.one * scale;
            }
            else
            {
                slot.slotObject.transform.localScale = Vector3.Lerp(slot.slotObject.transform.localScale, Vector3.one, Time.deltaTime * 8f);
            }
        }
        else
        {
            if (slot.cooldownOverlay != null) slot.cooldownOverlay.gameObject.SetActive(false);
            if (slot.cooldownText != null) slot.cooldownText.gameObject.SetActive(false);
            slot.slotObject.transform.localScale = Vector3.Lerp(slot.slotObject.transform.localScale, Vector3.one, Time.deltaTime * 8f);
        }

        // Mostrar nombre del hechizo dentro del slot si existe el texto de cooldown (cuando no hay cooldown lo mostramos opcionalmente)
        if (slot.cooldownText != null && !isOnCooldown)
        {
            slot.cooldownText.gameObject.SetActive(true);
            slot.cooldownText.text = spell.displayName;
            // Lo mantenemos visible por simplicidad; una mejora sería ocultarlo tras x segundos o animarlo
        }
    }

    // BindUIElementsFromRoot: intenta enlazar referencias si se asignó un UI preconstruido
    private void BindUIElementsFromRoot(GameObject root)
    {
        if (root == null) return;

        // Intentos simples por ruta esperada. Si no existe, no fallar.
        Transform t = null;

        t = root.transform.Find("HealthContainer/HealthSlider");
        if (t != null) _healthSlider = t.GetComponent<Slider>();
        t = root.transform.Find("HealthContainer/HealthSlider/HealthText");
        if (t != null) _healthText = t.GetComponent<TextMeshProUGUI>();

        t = root.transform.Find("ManaContainer/ManaSlider");
        if (t != null) _manaSlider = t.GetComponent<Slider>();
        t = root.transform.Find("ManaContainer/ManaSlider/ManaText");
        if (t != null) _manaText = t.GetComponent<TextMeshProUGUI>();

        // Slots (nombres basados en CreateSlotInRow). Intentar localizar iconos
        t = root.transform.Find("SlotsRow/Slot_Left/Icon");
        if (t != null)
        {
            _leftSlot = new MagicSlotUI();
            _leftSlot.slotObject = t.parent != null ? t.parent.gameObject : t.gameObject;
            _leftSlot.iconImage = t.GetComponent<Image>();
        }
        t = root.transform.Find("SlotsRow/Slot_Right/Icon");
        if (t != null)
        {
            _rightSlot = new MagicSlotUI();
            _rightSlot.slotObject = t.parent != null ? t.parent.gameObject : t.gameObject;
            _rightSlot.iconImage = t.GetComponent<Image>();
        }
        t = root.transform.Find("SlotsRow/Slot_Special/Icon");
        if (t != null)
        {
            _upSlot = new MagicSlotUI();
            _upSlot.slotObject = t.parent != null ? t.parent.gameObject : t.gameObject;
            _upSlot.iconImage = t.GetComponent<Image>();
        }

        // Intentar obtener fills desde los sliders si existen
        if (_healthSlider != null && _healthSlider.fillRect != null)
            _healthFill = _healthSlider.fillRect.GetComponent<Image>();
        if (_manaSlider != null && _manaSlider.fillRect != null)
            _manaFill = _manaSlider.fillRect.GetComponent<Image>();
    }

    private void FindPlayerComponents()
    {
        if (_healthSystem == null) _healthSystem = UnityEngine.Object.FindFirstObjectByType<PlayerHealthSystem>();
        if (_manaPool == null) _manaPool = UnityEngine.Object.FindFirstObjectByType<ManaPool>();
        if (_magicCaster == null) _magicCaster = UnityEngine.Object.FindFirstObjectByType<MagicCaster>();
        if (_actionManager == null) _actionManager = UnityEngine.Object.FindFirstObjectByType<PlayerActionManager>();

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null)
        {
            // _playerAbilities NO es un Component; resolver de forma segura desde el preset o desde componentes que lo expongan
            if (_playerAbilities == null) _playerAbilities = ResolvePlayerAbilitiesFromHierarchy(playerGo);
            if (_actionManager == null) _actionManager = playerGo.GetComponent<PlayerActionManager>() ?? playerGo.GetComponentInParent<PlayerActionManager>();

            // Si no tenemos PlayerAbilities (ni en el preset ni en componentes), crear un componente pequeño para exponerlas
            if (_playerAbilities == null)
            {
                var existingComp = playerGo.GetComponent<PlayerAbilitiesComponent>();
                if (existingComp == null)
                {
                    existingComp = playerGo.AddComponent<PlayerAbilitiesComponent>();
                    // Si hay un preset activo, aplicar sus abilities a este componente
                    var preset = GameBootService.Profile?.GetActivePresetResolved();
                    if (preset != null && preset.abilities != null)
                    {
                        existingComp.abilities = preset.abilities;
                    }
                    Debug.Log("[PlayerHUDComplete] PlayerAbilitiesComponent añadido automáticamente al Player.");
                }
                _playerAbilities = existingComp.abilities;
            }
        }
        // Suscribir al evento de maná si lo encontramos para recibir actualizaciones inmediatas
        if (_manaPool != null)
        {
            try
            {
                // Evitar suscripciones duplicadas
                _manaPool.OnManaChanged.RemoveListener(OnManaChangedListener);
            }
            catch { }
            _manaPool.OnManaChanged.AddListener(OnManaChangedListener);
        }
     }

    private void OnDestroy()
    {
        if (_manaPool != null)
        {
            try { _manaPool.OnManaChanged.RemoveListener(OnManaChangedListener); } catch { }
        }
        PlayerPresetService.OnPresetApplied -= ForceRefresh;
    }

    private IEnumerator FindComponentsDelayed()
    {
        // Esperar un pequeño tiempo para dar chance a servicios que inicializan en Start/Awake
        yield return new WaitForSeconds(0.5f);
        FindPlayerComponents();
        ForceRefresh();
    }

    private Sprite CreateDefaultSpellIcon()
    {
        // Crear un sprite mínimo en tiempo de ejecución para evitar nulos si no hay assets
        var tex = new Texture2D(2, 2);
        tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
    }

    private PlayerAbilities ResolvePlayerAbilitiesFromHierarchy(GameObject playerGo)
    {
        if (playerGo == null) return null;

        // 1) Preferir el preset activo si está disponible
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset != null && preset.abilities != null)
            return preset.abilities;

        // 2) Buscar en componentes del GameObject (y en padres) algún MonoBehaviour que exponga
        //    un campo o propiedad pública llamado 'abilities' de tipo PlayerAbilities.
        PlayerAbilities TryFromGameObject(GameObject go)
        {
            if (go == null) return null;
            var comps = go.GetComponents<MonoBehaviour>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var t = c.GetType();
                var f = t.GetField("abilities");
                if (f != null && f.FieldType == typeof(PlayerAbilities))
                {
                    var v = f.GetValue(c) as PlayerAbilities;
                    if (v != null) return v;
                }
                var p = t.GetProperty("abilities");
                if (p != null && p.PropertyType == typeof(PlayerAbilities))
                {
                    var v2 = p.GetValue(c) as PlayerAbilities;
                    if (v2 != null) return v2;
                }
            }
            return null;
        }

        // Buscar en el propio GO
        var found = TryFromGameObject(playerGo);
        if (found != null) return found;

        // Buscar recursivamente en padres
        var parent = playerGo.transform.parent;
        while (parent != null)
        {
            found = TryFromGameObject(parent.gameObject);
            if (found != null) return found;
            parent = parent.parent;
        }

        // Buscar recursivamente en hijos (último recurso)
        foreach (Transform child in playerGo.transform)
        {
            found = TryFromGameObject(child.gameObject);
            if (found != null) return found;
        }

        return null;
    }

    #endregion
}
