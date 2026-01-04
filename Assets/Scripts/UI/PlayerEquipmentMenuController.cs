using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

public class PlayerEquipmentMenuController : MonoBehaviour
{
    public static PlayerEquipmentMenuController Instance => _instance;
    public static bool IsOpen => _instance != null && _instance._isOpen;

    public readonly struct InventoryItemUseContext
    {
        public Inventory Inventory { get; }
        public ItemData Item { get; }
        public PlayerPickupCollector Collector { get; }

        public InventoryItemUseContext(Inventory inventory, ItemData item, PlayerPickupCollector collector)
        {
            Inventory = inventory;
            Item = item;
            Collector = collector;
        }
    }

    public struct InventoryItemUseResult
    {
        public bool handled;
        public bool consumed;
        public string message;

        public InventoryItemUseResult(bool handled, bool consumed, string message)
        {
            this.handled = handled;
            this.consumed = consumed;
            this.message = message;
        }

        public static InventoryItemUseResult NotHandled => new InventoryItemUseResult(false, false, null);

        public static InventoryItemUseResult Handled(string message = null, bool consumed = false)
            => new InventoryItemUseResult(true, consumed, message);
    }

    public delegate InventoryItemUseResult InventoryItemUseHandler(InventoryItemUseContext context);
    public static event InventoryItemUseHandler OnInventoryItemUseRequested;

    static InventoryItemUseResult DispatchInventoryUseRequest(InventoryItemUseContext context)
    {
        var handlers = OnInventoryItemUseRequested;
        if (handlers == null) return InventoryItemUseResult.NotHandled;

        var aggregated = InventoryItemUseResult.NotHandled;

        foreach (InventoryItemUseHandler handler in handlers.GetInvocationList())
        {
            try
            {
                var partial = handler(context);
                if (!partial.handled) continue;

                aggregated.handled = true;
                if (partial.consumed)
                    aggregated.consumed = true;
                if (!string.IsNullOrEmpty(partial.message))
                    aggregated.message = partial.message;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        return aggregated;
    }

    [Header("Persistencia")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Escena permitida")]
    [Tooltip("Nombre de la escena donde se permite abrir el menÃº de equipo.")]
    [SerializeField] private string allowedSceneName = "MainWorld";

    [Header("Contenedores UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Objeto raÃ­z del contenido del menÃº (se activa/desactiva al abrir/cerrar).")]
    [SerializeField] private GameObject windowRoot;

    [Header("Feedback")]
    [SerializeField, Tooltip("Tiempo que se mantiene visible el mensaje de feedback tras usar un objeto.")]
    private float feedbackDuration = 1.5f;

    [Header("PestaÃ±as")]
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button spellsTabButton;
    [SerializeField] private Button equipmentTabButton;

    [Header("Panel de jugador")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI mpText;

    string _levelLabel = "";
    string _hpLabel = "";
    string _mpLabel = "";
    bool _labelsCached;
    
    // Animaciones de feedback para HP/MP
    private Tween _hpTextTween;
    private Tween _mpTextTween;
    private Color _hpOriginalColor;
    private Color _mpOriginalColor;
    private bool _hpColorCached;
    private bool _mpColorCached;

    [Header("Habilidades")]
    [SerializeField] private GameObject abilitiesRoot;
    [SerializeField] private AbilityEntryReferences abilityEntries = new();

    [Header("SelecciÃ³n inicial")]
    [SerializeField] private GameObject initialSelectionOverride;

    [Header("Inventario")]
    [SerializeField] private InventoryBindings inventoryUI = new();

        [Header("Hechizos")]
        [SerializeField] private SpellBindings spellUI = new();

    [Header("Equipamiento")]
    [SerializeField] private EquipmentBindings equipmentUI = new();

    static PlayerEquipmentMenuController _instance;

    readonly List<Button> _tabButtons = new();

    InventoryView _inventoryView;
    SpellView _spellView;
    EquipmentView _equipmentView;
    [Header("CÃ¡mara de equipamiento")]
    [SerializeField] private float equipmentCameraDistance = 3f;
    [SerializeField] private float equipmentCameraHeight = 1.7f;
    [SerializeField] private Vector3 equipmentCameraLookOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private float equipmentCameraHorizontalOffset = -1.2f;
    [SerializeField] private float previewOrbitSpeed = 120f;
    [SerializeField, Tooltip("Transform de referencia para centrar la cÃ¡mara (busca 'PortraitAnchor' automÃ¡ticamente si es null)")]
    private Transform portraitAnchor;
    [SerializeField, Tooltip("Componente que gestiona el cambio temporal de layers para aislar al player del mundo")]
    private PortraitLayerSwapSRP portraitLayerSwap;
    [Header("Equipamiento - Visibilidad del jugador")]
    [SerializeField] private bool bringPlayerInFrontOfUi = true;
    [SerializeField] private int playerPreviewSortingOrder = 5000;
    [SerializeField, Min(0f), Tooltip("Tiempo mÃ­nimo tras abrir antes de permitir el cierre (para evitar rebotes de input).")]
    private float closeInputGracePeriod = 0.3f;
    
    // Referencia a la cÃ¡mara de retrato, encontrada automÃ¡ticamente en el player
    Camera _equipmentPreviewCamera;
    
    bool _equipmentCameraActive;
    Transform _playerPreviewTarget;
    Quaternion _storedPlayerRotation;
    Vector3 _previewBaseForward = Vector3.forward;
    float _previewPlayerYaw;
    Vector3 _fixedAnchorPosition; // PosiciÃ³n fija del anchor para que no se mueva cuando el player rota
    Vector3 _fixedCameraPosition; // PosiciÃ³n fija de la cÃ¡mara
    bool _wasInOrbitMode; // Rastrear si estuvimos en modo orbit en el frame anterior
    PlayerActionManager _actionManager;
    bool _actionModeActive;
    bool _toggleRequested;
    bool _cancelRequested;
    float _openedAt = -999f;
    float _toggleCooldownUntil;
    InputActionMapScope _inputScope;
    
    // Para mantener animaciones del player en el menÃº
    Animator _playerAnimator;
    AnimatorUpdateMode _storedAnimatorUpdateMode;

    readonly Dictionary<Renderer, RendererSortState> _playerRendererSortCache = new();

    bool _isOpen;
    int _activeTab;
    float _savedTimeScale = 1f;

    Coroutine _clearFeedbackRoutine;

    bool _warnedInventory;
    bool _warnedSpells;
    bool _warnedEquipment;

    // Cambiado a SubsystemRegistration para que se ejecute antes y busque en todas las escenas
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Debug.Log("[PlayerEquipmentMenuController] Bootstrap: Buscando instancia existente...");
        
        // Intentar obtener desde ServiceLocator primero
        if (ServiceLocator.TryGet<PlayerEquipmentMenuController>(out var existing) && existing != null)
        {
            Debug.Log("[PlayerEquipmentMenuController] Bootstrap: Encontrada instancia existente en ServiceLocator");
            _instance = existing;
            return;
        }
    
        
        // Si no hay instancia, no hacer nada - el menÃº debe estar configurado manualmente en la escena
        Debug.Log("[PlayerEquipmentMenuController] Bootstrap: No se encontrÃ³ instancia. El menÃº debe estar configurado manualmente en la escena.");
    }

    void Awake()
    {
        Debug.Log($"[PlayerEquipmentMenuController] Awake en GameObject '{gameObject.name}'");
        
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[PlayerEquipmentMenuController] Instancia duplicada detectada en '{gameObject.name}', destruyendo...");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ServiceLocator.Register(this);

        if (dontDestroyOnLoad && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[PlayerEquipmentMenuController] DontDestroyOnLoad aplicado a '{gameObject.name}'");
        }

        // Buscar componentes UI necesarios
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
            Debug.Log($"[PlayerEquipmentMenuController] Canvas encontrado: {(canvas != null ? canvas.gameObject.name : "NULL")}");
        }
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            Debug.Log($"[PlayerEquipmentMenuController] CanvasGroup encontrado: {(canvasGroup != null ? "SÃ­" : "No")}");
        }
        
        if (windowRoot == null && canvas != null)
        {
            windowRoot = canvas.gameObject;
            Debug.Log($"[PlayerEquipmentMenuController] WindowRoot asignado automÃ¡ticamente a Canvas: '{windowRoot.name}'");
        }
        
        // Verificar si tenemos lo mÃ­nimo necesario
        if (canvas == null)
        {
            Debug.LogError($"[PlayerEquipmentMenuController] âš ï¸ No se encontrÃ³ Canvas en '{gameObject.name}'");
            Debug.LogError("   El menÃº de equipamiento NO funcionarÃ¡ correctamente.");
            Debug.LogError("   AsegÃºrate de que el PlayerEquipmentMenuController estÃ© en un GameObject con Canvas configurado.");
            // No desactivar el componente para que se pueda configurar despuÃ©s
            enabled = false;
            return;
        }

        if (levelText != null)
            _levelLabel = levelText.text;
        if (hpText != null)
            _hpLabel = hpText.text;
        if (mpText != null)
            _mpLabel = mpText.text;

        SetCanvasState(false);

        RegisterTabButtons();
        
        // EnsureViews retorna false si no hay vistas configuradas
        if (!EnsureViews())
        {
            Debug.LogError("[PlayerEquipmentMenuController] âš ï¸ No se pudo inicializar ninguna vista del menÃº");
            Debug.LogError("   El menÃº no podrÃ¡ abrirse hasta que se configuren las vistas en el Inspector.");
            // No desactivar el componente para que se pueda configurar despuÃ©s
        }
        
        SetEquipmentCameraActive(false);
        // Unregister from MenuManager
        MenuManager.Close(MenuKind.Equipment);
        
        Debug.Log($"[PlayerEquipmentMenuController] Awake completado. Vistas configuradas: {(_inventoryView != null || _spellView != null || _equipmentView != null)}");
    }

    void OnDisable()
    {
        if (_isOpen)
            CloseMenu();
        else
            ExitUiInputScope();
    }

    void OnDestroy()
    {
        ExitUiInputScope();
        ApplyPlayerPreviewSorting(false);
        _inventoryView?.Dispose();
        _equipmentView?.Dispose();
        if (_instance == this)
            _instance = null;
    }

    void Update()
    {
        if (!IsAllowedInCurrentScene())
        {
            if (_isOpen) CloseMenu();
            return;
        }


        if (GameOverManager.Instance != null && GameOverManager.Instance.IsShown)
        {
            if (_isOpen) CloseMenu();
            return;
        }

        // Detectar botÃ³n Start para abrir/cerrar el menÃº usando GamepadInputReader
        if (GamepadInputReader.StartPressed)
        {
            _toggleRequested = true;
        }

        // Si el menÃº ya estÃ¡ abierto, evita leer el input de apertura para que el D-Pad
        // no interfiera con la navegaciÃ³n UI (el toggle se maneja al cerrarse).
        if (!_isOpen)
        {
            HandleToggleInput();
        }
        else
        {
            // Detectar botones del gamepad usando GamepadInputReader
            
            // BotÃ³n B (Cancel) o Start para cerrar el menÃº
            if (GamepadInputReader.CancelPressed || GamepadInputReader.StartPressed)
            {
                _cancelRequested = true;
            }
            
            // BotÃ³n Y para volver al MainMenu
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsYButtonPressed())
            {
                GamepadInputReader.PlayUISound("UI_Cancel");
                OnQuitToMainMenu();
            }
            
            // LB (Left Bumper) para pestaÃ±a anterior
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsLeftShoulderPressed())
            {
                GamepadInputReader.PlayUISound("UI_Navigate");
                ChangeTab(-1);
            }
            
            // RB (Right Bumper) para pestaÃ±a siguiente
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsRightShoulderPressed())
            {
                GamepadInputReader.PlayUISound("UI_Navigate");
                ChangeTab(1);
            }
            
            HandleCloseInput();
            UpdatePlayerInfoPanel();
            
            // Mantener el Animator en idle continuamente
            MaintainAnimatorIdle();
            
            // DEBUG: Verificar estado de inputs cada 60 frames
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[PlayerEquipmentMenu] Frame {Time.frameCount} - IsOpen: {_isOpen}, ActiveTab: {_activeTab}, SubmitPressed: {GamepadInputReader.SubmitPressed}");
            }
            
            // Manejar inputs especÃ­ficos de cada tab
            if (_activeTab == 0) // Inventario
            {
                // Manejar Submit (A button)
                if (GamepadInputReader.SubmitPressed)
                {
                    Debug.Log("[PlayerEquipmentMenu] â­ Submit detectado en inventario!");
                    bool handled = _inventoryView?.TryHandleSubmit() ?? false;
                    Debug.Log($"[PlayerEquipmentMenu] Submit handled: {handled}");
                }
                
                // Manejar Cancel (B button) - pero solo si el inventario no lo maneja primero
                if (GamepadInputReader.CancelPressed)
                {
                    bool handled = _inventoryView?.TryHandleCancel() ?? false;
                    if (handled)
                        _cancelRequested = false; // Evitar que cierre el menÃº
                }
            }
            else if (_activeTab == 1) // Hechizos
            {
                _spellView?.HandleInput();
            }
        }
    }

    // MÃ©todos auxiliares simplificados - usan GamepadInputReader centralizado
    // Estos leen del Action Map UI para navegaciÃ³n de menÃºs
    bool IsLeftShoulderPressed()
    {
        return GamepadInputReader.LeftShoulderPressedUI;
    }

    bool IsRightShoulderPressed()
    {
        return GamepadInputReader.RightShoulderPressedUI;
    }

    bool IsYButtonPressed()
    {
        return GamepadInputReader.YButtonPressedUI;
    }

    void RegisterTabButtons()
    {
        if (inventoryTabButton != null)
        {
            inventoryTabButton.onClick.AddListener(() => ShowTab(0));
            _tabButtons.Add(inventoryTabButton);
            
            // Asegurar que tiene componente de audio
            if (inventoryTabButton.GetComponent<UIButtonAudio>() == null)
                inventoryTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
        if (spellsTabButton != null)
        {
            spellsTabButton.onClick.AddListener(() => ShowTab(1));
            _tabButtons.Add(spellsTabButton);
            
            // Asegurar que tiene componente de audio
            if (spellsTabButton.GetComponent<UIButtonAudio>() == null)
                spellsTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
        if (equipmentTabButton != null)
        {
            equipmentTabButton.onClick.AddListener(() => ShowTab(2));
            _tabButtons.Add(equipmentTabButton);
            
            // Asegurar que tiene componente de audio
            if (equipmentTabButton.GetComponent<UIButtonAudio>() == null)
                equipmentTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
    }

    void HandleToggleInput()
    {
        bool pressed = _toggleRequested;
        _toggleRequested = false;

        if (Time.unscaledTime < _toggleCooldownUntil)
            return;

        if (!pressed) return;

        if (_isOpen)
        {
            CloseMenu();
        }
        else
        {
            if (!GameState.CanOpenInventory) return;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
            OpenMenu();
            _toggleCooldownUntil = Time.unscaledTime + 0.25f;
        }
    }


    void HandleCloseInput()
    {
        // Evitar cerrar inmediatamente si todavÃ­a estamos procesando el input que abriÃ³ el menÃº.
        if (Time.unscaledTime - _openedAt < closeInputGracePeriod)
            return;

        bool cancel = _cancelRequested;
        _cancelRequested = false;

        if (cancel)
        {
            bool handled = false;
            if (_activeTab == 0 && _inventoryView != null)
                handled = _inventoryView.TryHandleCancel();
            else if (_activeTab == 1 && _spellView != null)
                handled = _spellView.TryHandleCancel();
            else if (_activeTab == 2 && _equipmentView != null)
                handled = _equipmentView.TryHandleCancel();

            if (handled)
                return;

            CloseMenu();
        }
    }

    void ChangeTab(int delta)
    {
        if (delta == 0) return;

        var availableTabs = GetAvailableTabs();
        if (availableTabs.Count == 0) return;

        int currentIndex = availableTabs.IndexOf(_activeTab);
        if (currentIndex < 0) currentIndex = 0;

        int nextIndex = (currentIndex + delta + availableTabs.Count) % availableTabs.Count;
        int nextTab = availableTabs[nextIndex];
        bool forceRebuild = nextTab == 0 && nextTab != _activeTab;
        ShowTab(nextTab, forceRebuild);
    }



    List<int> GetAvailableTabs()
    {
        var tabs = new List<int>(3);
        if (_inventoryView != null) tabs.Add(0);
        if (_spellView != null) tabs.Add(1);
        if (_equipmentView != null) tabs.Add(2);
        if (tabs.Count == 0)
            tabs.Add(_activeTab);
        return tabs;
    }

    void OpenMenu()
    {
        // Reproducir sonido de apertura de menÃº
        GamepadInputReader.PlayUISound("UI_Submit");
        
        Debug.Log("[PlayerEquipmentMenu] OpenMenu() llamado");
        
        // VerificaciÃ³n temprana: Â¿tenemos Canvas?
        if (canvas == null)
        {
            Debug.LogError("[PlayerEquipmentMenu] âŒ No se puede abrir - Canvas es NULL");
            Debug.LogError("   El PlayerEquipmentMenuController no estÃ¡ correctamente configurado.");
            Debug.LogError("   Debe estar en un GameObject con un Canvas configurado.");
            return;
        }
        
        // VerificaciÃ³n temprana: Â¿hay al menos una vista configurada?
        if (_inventoryView == null && _spellView == null && _equipmentView == null)
        {
            Debug.LogError("[PlayerEquipmentMenu] âŒ No se puede abrir - NINGUNA VISTA CONFIGURADA");
            Debug.LogError("   Configura al menos una vista (Inventory, Spell o Equipment) en el Inspector.");
            Debug.LogError("   Revisa los logs anteriores de EnsureViews() para mÃ¡s detalles.");
            return;
        }
        
        if (!GameState.CanOpenInventory)
        {
            Debug.Log("[PlayerEquipmentMenu] No se puede abrir - GameState.CanOpenInventory = false");
            return;
        }
        
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            Debug.Log("[PlayerEquipmentMenu] No se puede abrir - DiÃ¡logo activo");
            return;
        }

        // Ask central manager for permission to open
        if (!MenuManager.TryOpen(MenuKind.Equipment))
        {
            Debug.Log("[PlayerEquipmentMenuController] Apertura denegada por MenuManager");
            return;
        }

        Debug.Log("[PlayerEquipmentMenu] MenuManager permitiÃ³ la apertura, verificando vistas...");
        
        if (!EnsureViews())
        {
            Debug.LogError("[PlayerEquipmentMenu] EnsureViews() retornÃ³ false - cerrando menÃº");
            MenuManager.Close(MenuKind.Equipment);
            return;
        }

        Debug.Log("[PlayerEquipmentMenu] Vistas verificadas, inicializando ActionManager...");
        
        EnsureActionManager();
        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Inventory);
            _actionModeActive = true;
        }
        
        Debug.Log("[PlayerEquipmentMenu] Llamando a EnterUiInputScope()");
        EnterUiInputScope();

        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        
        // Cambiar el Animator a Unscaled Time para que las animaciones sigan funcionando
        if (_playerAnimator != null)
        {
            _storedAnimatorUpdateMode = _playerAnimator.updateMode;
            _playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            Debug.Log("[PlayerEquipmentMenu] Animator cambiado a UnscaledTime para mantener animaciones en el menÃº");
        }

        Debug.Log("[PlayerEquipmentMenu] Configurando canvas y pestaÃ±as...");
        SetCanvasState(true);

        // Cachear colores originales de HP/MP si no se han cacheado aÃºn
        if (!_hpColorCached && hpText != null)
        {
            _hpOriginalColor = hpText.color;
            _hpColorCached = true;
        }
        if (!_mpColorCached && mpText != null)
        {
            _mpOriginalColor = mpText.color;
            _mpColorCached = true;
        }

        int defaultTab = GetDefaultTab();
        bool forceRebuild = defaultTab == 0;
        ShowTab(defaultTab, forceRebuild);
        UpdatePlayerInfoPanel();

        _isOpen = true;
        GameState.Push(GamePhase.Inventory);
        GameState.Push(GamePhase.Equipment);
        SelectInitial();
        
        Debug.Log("[PlayerEquipmentMenu] Activando cÃ¡mara de equipamiento...");
        // Activar la cÃ¡mara de equipamiento siempre que el menÃº estÃ© abierto
        SetEquipmentCameraActive(true);

        // Marcar el instante de apertura para filtrar cierres accidentales en el mismo frame.
        _openedAt = Time.unscaledTime;
        _cancelRequested = false; // Limpiar cualquier cancel previo para evitar cierres inmediatos.
        
        Debug.Log("[PlayerEquipmentMenu] MenÃº abierto completamente");
    }

    void CloseMenu(bool playSound = true)
    {
        // Solo reproducir sonido si el menÃº realmente estaba abierto
        if (playSound && _isOpen)
        {
            GamepadInputReader.PlayUISound("UI_Cancel");
        }
        
        // Limpiar animaciones de HP/MP
        _hpTextTween?.Kill();
        _hpTextTween = null;
        _mpTextTween?.Kill();
        _mpTextTween = null;
        
        // Restaurar colores originales si estÃ¡n cacheados
        if (_hpColorCached && hpText != null)
            hpText.color = _hpOriginalColor;
        if (_mpColorCached && mpText != null)
            mpText.color = _mpOriginalColor;
        
        SetCanvasState(false);
        _spellView?.CancelSlotSelection(true);
        Time.timeScale = _savedTimeScale;
        
        // Restaurar el AnimatorUpdateMode original
        if (_playerAnimator != null)
        {
            _playerAnimator.updateMode = _storedAnimatorUpdateMode;
            Debug.Log("[PlayerEquipmentMenu] Animator restaurado a su UpdateMode original");
        }
        
        // Resetear posiciones fijas para que se recalculen la prÃ³xima vez
        _fixedCameraPosition = Vector3.zero;
        _fixedAnchorPosition = Vector3.zero;
        _wasInOrbitMode = false;
        
        _isOpen = false;
        ExitUiInputScope();
        if (_actionModeActive && _actionManager != null)
        {
            _actionManager.PopMode(ActionMode.Inventory);
            _actionModeActive = false;
        }
        _toggleCooldownUntil = Time.unscaledTime + 0.2f;
        if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
        if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
        SetEquipmentCameraActive(false);
        MenuManager.Close(MenuKind.Equipment);
    }

    void OnQuitToMainMenu()
    {
        // Cerrar el menÃº SIN reproducir sonido (ya sonÃ³ UI_Cancel arriba)
        if (_isOpen)
        {
            CloseMenu(playSound: false);
        }
        
        // Asegurar que el tiempo estÃ¡ a escala normal
        Time.timeScale = 1f;
        
        // Cargar la escena del MainMenu (esto limpiarÃ¡ automÃ¡ticamente los estados)
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    void SetCanvasState(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (windowRoot != null)
            windowRoot.SetActive(visible);

        if (canvas != null && canvasGroup == null)
            canvas.gameObject.SetActive(visible);
    }

    void ShowTab(int index, bool forceRebuild = false)
    {
        int previousTab = _activeTab;
        _activeTab = Mathf.Clamp(index, 0, 2);

        if (_spellView != null && _activeTab != 1)
            _spellView.CancelSlotSelection(true);

        if (_inventoryView != null)
        {
            _inventoryView.SetVisible(_activeTab == 0);
            if (_activeTab == 0)
            {
                _inventoryView.Refresh(forceRebuild);
                StartCoroutine(_inventoryView.EnsureSelectionDelayed());
            }
        }

        if (_spellView != null)
        {
            _spellView.SetVisible(_activeTab == 1);
            if (_activeTab == 1) _spellView.Refresh();
        }

        if (_equipmentView != null)
        {
            _equipmentView.SetVisible(_activeTab == 2);
            if (_activeTab == 2)
            {
                _equipmentView.Refresh();
                _equipmentView.EnsureSelection();
            }
        }

        UpdateTabButtonStates();
        if (_isOpen && previousTab != _activeTab)
            SelectInitial();
        
        // Mantener la cÃ¡mara activa en todas las pestaÃ±as mientras el menÃº estÃ© abierto
        SetEquipmentCameraActive(_isOpen);
    }

    void EnterUiInputScope()
    {
        Debug.Log("[PlayerEquipmentMenu] EnterUiInputScope() - Cambiando a modo UI");
        _inputScope?.Dispose();
        _inputScope = InputActionMapScope.EnterUiScope();
        
        // Asegurar que los eventos de input estÃ¡n suscritos (para sonidos automÃ¡ticos de LB/RB)
        GamepadInputReader.EnsureInputEventsSubscribed();
        
        Debug.Log("[PlayerEquipmentMenu] InputScope creado");
    }

    void ExitUiInputScope()
    {
        _inputScope?.Dispose();
        _inputScope = null;
    }

    void EnsureActionManager()
    {
        if (_actionManager != null) return;
        PlayerService.TryGetComponent(out _actionManager, includeInactive: true, allowSceneLookup: true);
    }

    void LateUpdate()
    {
        if (!_equipmentCameraActive || _equipmentPreviewCamera == null) return;
        
        // Solo permitir Ã³rbita en la pestaÃ±a de Equipamiento (index 2)
        bool allowOrbit = _activeTab == 2;
        
        // Si salimos del modo orbit, resetear las posiciones fijas
        if (_wasInOrbitMode && !allowOrbit)
        {
            _fixedCameraPosition = Vector3.zero;
            _fixedAnchorPosition = Vector3.zero;
            
            // Forzar recalculo inmediato de la cÃ¡mara para evitar que el personaje se vea cortado
            UpdateEquipmentCamera(allowOrbit);
        }
        _wasInOrbitMode = allowOrbit;
        
        UpdateEquipmentCamera(allowOrbit);
    }

    void UpdateEquipmentCamera(bool allowOrbit)
    {
        if (_playerPreviewTarget == null)
        {
            if (!TrySetupPreviewTarget())
                return;
        }

        if (allowOrbit) 
        {
            // Leer directamente del hardware para evitar restricciones de supresiÃ³n
            float rotateInput = GamepadInputReader.CameraLookRaw.x;
            
            if (Mathf.Abs(rotateInput) > 0.01f)
            {
                _previewPlayerYaw += rotateInput * previewOrbitSpeed * Time.unscaledDeltaTime;
            }
            
            // Rotar al player sobre sÃ­ mismo
            // Inicia mirando hacia la cÃ¡mara (180Â°) y gira segÃºn el input del joystick
            _playerPreviewTarget.rotation = Quaternion.Euler(0f, 180f - _previewPlayerYaw, 0f);
        }
        else
        {
            // En modo normal, resetear el yaw del player y restaurar su rotaciÃ³n inicial
            _previewPlayerYaw = 0f;
            
            // Restaurar la rotaciÃ³n del player para que mire hacia la cÃ¡mara (180Â°)
            if (_playerPreviewTarget != null)
            {
                _playerPreviewTarget.rotation = Quaternion.Euler(0f, 180f, 0f);
            }
        }
        
        if (allowOrbit)
        {
            // Modo ORBIT: La cÃ¡mara estÃ¡ COMPLETAMENTE FIJA
            // Usamos las posiciones guardadas del Ãºltimo frame sin orbit
            
            // Si aÃºn no tenemos posiciones fijas guardadas, usar las actuales
            if (_fixedCameraPosition == Vector3.zero)
            {
                _fixedCameraPosition = _equipmentPreviewCamera.transform.position;
                Transform anchorPoint = portraitAnchor != null ? portraitAnchor : _playerPreviewTarget;
                _fixedAnchorPosition = anchorPoint.position + equipmentCameraLookOffset;
            }
            
            _equipmentPreviewCamera.transform.position = _fixedCameraPosition;
            _equipmentPreviewCamera.transform.rotation = Quaternion.LookRotation(
                (_fixedAnchorPosition - _fixedCameraPosition).normalized, 
                Vector3.up
            );
        }
        else
        {
            // Modo NORMAL: La cÃ¡mara puede orbitar (usado en otros tabs)
            Transform anchorPoint = portraitAnchor != null ? portraitAnchor : _playerPreviewTarget;
            
            var cameraForward = _previewBaseForward;
            if (cameraForward.sqrMagnitude < 0.001f) cameraForward = Vector3.forward;
            cameraForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
            
            var rotatedForward = Quaternion.Euler(0f, _previewPlayerYaw, 0f) * cameraForward;
            rotatedForward = Vector3.ProjectOnPlane(rotatedForward, Vector3.up).normalized;
            Vector3 cameraRight = Vector3.Cross(Vector3.up, rotatedForward).normalized;

            Vector3 anchorPos = anchorPoint.position + equipmentCameraLookOffset;
            Vector3 cameraPos = anchorPos
                                - rotatedForward * equipmentCameraDistance
                                + Vector3.up * equipmentCameraHeight
                                - cameraRight * equipmentCameraHorizontalOffset;

            _equipmentPreviewCamera.transform.position = cameraPos;
            _equipmentPreviewCamera.transform.rotation = Quaternion.LookRotation((anchorPos - cameraPos).normalized, Vector3.up);
            
            // Guardar las posiciones para cuando cambiemos a modo orbit
            _fixedCameraPosition = cameraPos;
            _fixedAnchorPosition = anchorPos;
        }
    }

    bool TrySetupPreviewTarget()
    {
        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
        {
            if (_equipmentPreviewCamera != null)
                _equipmentPreviewCamera.enabled = false;
            return false;
        }

        _playerPreviewTarget = player.transform;
        _storedPlayerRotation = _playerPreviewTarget.rotation;
        
        // Buscar automÃ¡ticamente el PortraitAnchor si no estÃ¡ asignado
        if (portraitAnchor == null)
        {
            portraitAnchor = _playerPreviewTarget.Find("PortraitAnchor");
            if (portraitAnchor != null)
            {
                Debug.Log($"[PlayerEquipmentMenuController] PortraitAnchor encontrado automÃ¡ticamente: {portraitAnchor.name}");
            }
            else
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] No se encontrÃ³ 'PortraitAnchor' como hijo del player. Se usarÃ¡ el transform raÃ­z.");
            }
        }
        
        // Buscar el Animator del player para poder mantener sus animaciones activas en el menÃº
        if (_playerAnimator == null)
        {
            _playerAnimator = _playerPreviewTarget.GetComponentInChildren<Animator>();
            if (_playerAnimator != null)
            {
                Debug.Log($"[PlayerEquipmentMenuController] Animator del player encontrado: {_playerAnimator.name}");
            }
            else
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] No se encontrÃ³ Animator en el player. Las animaciones no funcionarÃ¡n en el menÃº.");
            }
        }
        
        // Forzar al Animator a ir a idle (detener animaciones de movimiento)
        if (_playerAnimator != null)
        {
            // Resetear parÃ¡metros comunes de movimiento a 0 para forzar idle
            if (_playerAnimator.parameters.Any(p => p.name == "InputMagnitude"))
                _playerAnimator.SetFloat("InputMagnitude", 0f);
            if (_playerAnimator.parameters.Any(p => p.name == "Speed"))
                _playerAnimator.SetFloat("Speed", 0f);
            if (_playerAnimator.parameters.Any(p => p.name == "VerticalVelocity"))
                _playerAnimator.SetFloat("VerticalVelocity", 0f);
            
            Debug.Log("[PlayerEquipmentMenuController] Animator forzado a idle");
        }
        
        // Usar Vector3.forward FIJO para que la cÃ¡mara siempre estÃ© en la misma posiciÃ³n relativa
        _previewBaseForward = Vector3.forward;
        _previewPlayerYaw = 0f;
        
        // Rotar al player para que mire HACIA la cÃ¡mara inicial (180Â° en Y)
        _playerPreviewTarget.rotation = Quaternion.Euler(0f, 180f, 0f);
        
        if (_equipmentPreviewCamera != null)
        {
            _equipmentPreviewCamera.enabled = true;
        }
        
        return true;
    }

    void SetEquipmentCameraActive(bool value)
    {
        // Si no hay cÃ¡mara encontrada, buscarla en el player
        if (_equipmentPreviewCamera == null)
        {
            _equipmentPreviewCamera = FindPortraitCameraInPlayer();
            if (_equipmentPreviewCamera == null)
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] No se encontrÃ³ la cÃ¡mara de retrato en el player. AsegÃºrate de que existe y tiene el tag 'PortraitCamera' o se llama 'PortraitCamera'.");
                return;
            }
        }
        
        if (_equipmentCameraActive == value) return;
        
        _equipmentCameraActive = value;
        _equipmentPreviewCamera.gameObject.SetActive(value);
        
        if (_equipmentCameraActive)
        {
            // IMPORTANTE: Forzar reset del preview target para garantizar posicionamiento consistente
            // Esto asegura que _previewBaseForward y _previewPlayerYaw se recalculen desde cero
            _playerPreviewTarget = null;
            TrySetupPreviewTarget();
            ApplyPlayerPreviewSorting(true);
            
            // Activar el sistema de cambio de layers
            if (portraitLayerSwap != null && _playerPreviewTarget != null)
            {
                portraitLayerSwap.Setup(_equipmentPreviewCamera, _playerPreviewTarget);
                portraitLayerSwap.enabled = true;
            }
        }
        else
        {
            // Desactivar y limpiar el sistema de cambio de layers
            if (portraitLayerSwap != null)
            {
                portraitLayerSwap.Cleanup();
                portraitLayerSwap.enabled = false;
            }
            
            ApplyPlayerPreviewSorting(false);
            
            if (_playerPreviewTarget != null)
                _playerPreviewTarget.rotation = _storedPlayerRotation;
            
            _playerPreviewTarget = null;
            _equipmentPreviewCamera.enabled = false;
        }
    }

    void MaintainAnimatorIdle()
    {
        if (!_isOpen || _playerAnimator == null)
            return;

        // Forzar parámetros a 0 para mantener idle
        _playerAnimator.SetFloat("InputMagnitude", 0f);
        _playerAnimator.SetFloat("Speed", 0f);
        _playerAnimator.SetFloat("VerticalVelocity", 0f);

        // Asegurar que el AnimatorUpdateMode esté en UnscaledTime
        if (_playerAnimator.updateMode != AnimatorUpdateMode.UnscaledTime)
        {
            _playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    /// <summary>
    /// Busca la cÃ¡mara de retrato dentro del player usando el ServiceLocator.
    /// Primero intenta por tag "PortraitCamera", luego por nombre.
    /// </summary>
    Camera FindPortraitCameraInPlayer()
    {
        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true) || player == null)
        {
            Debug.LogWarning("[PlayerEquipmentMenuController] No se pudo encontrar el player en el ServiceLocator.");
            return null;
        }

        // Buscar todas las cÃ¡maras en el player y sus hijos
        var cameras = player.GetComponentsInChildren<Camera>(true);
        
        // Si no se encuentran en los hijos, buscar en hermanos (mismo padre)
        if (cameras.Length == 0 && player.transform.parent != null)
        {
            Debug.Log("[PlayerEquipmentMenuController] No se encontraron cÃ¡maras en hijos del player, buscando en hermanos...");
            cameras = player.transform.parent.GetComponentsInChildren<Camera>(true);
        }
        
        // Si aÃºn no se encuentra, buscar en la raÃ­z de la escena
        if (cameras.Length == 0)
        {
            Debug.Log("[PlayerEquipmentMenuController] No se encontraron cÃ¡maras en hermanos, buscando en toda la escena...");
            cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }
        
        Debug.Log($"[PlayerEquipmentMenuController] Total de cÃ¡maras encontradas: {cameras.Length}");
        
        // 1. Intentar por tag "PortraitCamera"
        foreach (var cam in cameras)
        {
            if (cam.CompareTag("PortraitCamera"))
            {
                Debug.Log($"[PlayerEquipmentMenuController] CÃ¡mara de retrato encontrada por tag: {cam.name}");
                return cam;
            }
        }
        
        // 2. Intentar por nombre que contenga "Portrait"
        foreach (var cam in cameras)
        {
            if (cam.name.Contains("Portrait", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[PlayerEquipmentMenuController] CÃ¡mara de retrato encontrada por nombre: {cam.name}");
                return cam;
            }
        }
        
        // 3. Si no hay ninguna, mostrar advertencia con las cÃ¡maras disponibles
        if (cameras.Length > 0)
        {
            Debug.LogWarning($"[PlayerEquipmentMenuController] Se encontraron {cameras.Length} cÃ¡mara(s), pero ninguna tiene tag 'PortraitCamera' o nombre 'Portrait'. CÃ¡maras disponibles: {string.Join(", ", System.Array.ConvertAll(cameras, c => c.name))}");
        }
        else
        {
            Debug.LogWarning("[PlayerEquipmentMenuController] No se encontraron cÃ¡maras en ningÃºn lugar.");
        }
        
        return null;
    }

    void ApplyPlayerPreviewSorting(bool bringToFront)
    {
        if (!bringPlayerInFrontOfUi)
            return;

        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true) || player == null)
        {
            if (!bringToFront)
                _playerRendererSortCache.Clear();
            return;
        }

        if (bringToFront)
        {
            _playerRendererSortCache.Clear();
            var renderers = player.GetComponentsInChildren<Renderer>(true);
            int uiLayerId = ResolveUiSortingLayer();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                _playerRendererSortCache[renderer] = new RendererSortState
                {
                    order = renderer.sortingOrder,
                    layer = renderer.sortingLayerID
                };
                renderer.sortingOrder = playerPreviewSortingOrder;
                if (uiLayerId >= 0)
                    renderer.sortingLayerID = uiLayerId;
            }
        }
        else
        {
            foreach (var kvp in _playerRendererSortCache)
            {
                if (kvp.Key == null) continue;
                kvp.Key.sortingOrder = kvp.Value.order;
                kvp.Key.sortingLayerID = kvp.Value.layer;
            }
            _playerRendererSortCache.Clear();
        }
    }

    int ResolveUiSortingLayer()
    {
        foreach (var layer in SortingLayer.layers)
        {
            if (string.Equals(layer.name, "UI", StringComparison.OrdinalIgnoreCase))
                return layer.id;
        }
        return -1;
    }

    // Scope simple para gestionar el cambio UI/Gameplay usando PlayerInputManager centralizado
    sealed class InputActionMapScope : IDisposable
    {
        bool _disposed;

        InputActionMapScope()
        {
            Debug.Log("[InputActionMapScope] Constructor - Iniciando");
            
            GamepadInputReader.PushGameplaySuppression(this);
            GamepadInputReader.PushUiNavigationScope();

            // Cambiar a modo UI centralizado
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
            {
                Debug.Log("[InputActionMapScope] PlayerInputManager encontrado, llamando a PushUIMode()");
                pim.PushUIMode();
                Debug.Log($"[InputActionMapScope] PushUIMode ejecutado. IsInUIMode: {pim.IsInUIMode}");
            }
            else
            {
                Debug.LogError("[InputActionMapScope] PlayerInputManager NO encontrado en ServiceLocator!");
            }
        }

        public static InputActionMapScope EnterUiScope()
        {
            return new InputActionMapScope();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Restaurar modo Gameplay centralizado
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PopUIMode();

            GamepadInputReader.PopGameplaySuppression(this);
            GamepadInputReader.PopUiNavigationScope();
        }
    }

    int GetDefaultTab()
    {
        if (_inventoryView != null) return 0;
        if (_spellView != null) return 1;
        if (_equipmentView != null) return 2;
        return 0;
    }

    void UpdateTabButtonStates()
    {
        for (int i = 0; i < _tabButtons.Count; i++)
        {
            var button = _tabButtons[i];
            if (button == null) continue;
            button.interactable = i != _activeTab;
        }
    }

    void SelectInitial()
    {
        var finalTarget = ResolveInitialTarget();
        Debug.Log($"[PlayerEquipmentMenu] SelectInitial tab={_activeTab} default={finalTarget?.name ?? "null"} rows={_inventoryView?.RowCount.ToString() ?? "-"} override={initialSelectionOverride?.name ?? "null"} -> target={finalTarget?.name ?? "null"}");

        if (finalTarget != null)
            StartCoroutine(SelectOnNextFrame(finalTarget));
    }

    GameObject ResolveInitialTarget()
    {
        GameObject tabDefault = null;
        switch (_activeTab)
        {
            case 0:
                tabDefault = _inventoryView?.DefaultSelection;
                break;
            case 1:
                tabDefault = _spellView?.DefaultSelection;
                break;
            case 2:
                tabDefault = _equipmentView?.DefaultSelection;
                break;
        }

        return tabDefault ?? initialSelectionOverride ?? inventoryTabButton?.gameObject;
    }

    System.Collections.IEnumerator SelectOnNextFrame(GameObject target)
    {
        yield return null;
        if (target != null)
        {
            SelectGameObjectImmediate(target);
        }
    }

    void SelectGameObjectImmediate(GameObject target)
    {
        if (target == null) return;
        var selectable = target.GetComponent<Selectable>();
        if (selectable != null)
            selectable.Select();
    }

    bool IsInsideMenu(GameObject go)
    {
        if (go == null) return false;
        if (windowRoot != null)
            return go == windowRoot || go.transform.IsChildOf(windowRoot.transform);
        return go == gameObject || go.transform.IsChildOf(transform);
    }

    void UpdatePlayerInfoPanel()
    {
        bool hasStatsText = levelText != null || hpText != null || mpText != null;
        bool hasAbilityUI = abilitiesRoot != null || abilityEntries.HasAnyEntry;
        if (!hasStatsText && !hasAbilityUI) return;

        PlayerPresetSO preset = null;
        if (GameBootService.IsAvailable && GameBootService.Profile != null)
            preset = GameBootService.Profile.GetActivePresetResolved();

        if (hasStatsText)
        {
            CacheBaseLabelsIfNeeded();

            if (levelText != null)
            {
                var value = preset != null ? preset.level.ToString() : "?";
                levelText.text = string.IsNullOrEmpty(_levelLabel) ? value : $"{_levelLabel} {value}";
            }

            if (hpText != null)
            {
                string hpValue;
                if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var health, includeInactive: true, allowSceneLookup: true))
                    hpValue = $"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}";
                else if (preset != null)
                    hpValue = $"{Mathf.CeilToInt(preset.currentHP)} / {Mathf.CeilToInt(preset.maxHP)}";
                else
                    hpValue = "?";

                hpText.text = string.IsNullOrEmpty(_hpLabel) ? hpValue : $"{_hpLabel} {hpValue}";
            }

            if (mpText != null)
            {
                string mpValue;
                if (PlayerService.TryGetComponent<ManaPool>(out var mana, includeInactive: true, allowSceneLookup: true))
                    mpValue = $"{Mathf.CeilToInt(mana.Current)} / {Mathf.CeilToInt(mana.Max)}";
                else if (preset != null)
                    mpValue = $"{Mathf.CeilToInt(preset.currentMP)} / {Mathf.CeilToInt(preset.maxMP)}";
                else
                    mpValue = "?";

                mpText.text = string.IsNullOrEmpty(_mpLabel) ? mpValue : $"{_mpLabel} {mpValue}";
            }
        }

        UpdateAbilitiesPanel(preset);
    }

    void CacheBaseLabelsIfNeeded()
    {
        if (_labelsCached) return;

        // Capturar las etiquetas ya traducidas por LocalizedText una sola vez
        if (levelText != null)
            _levelLabel = levelText.text;
        if (hpText != null)
            _hpLabel = hpText.text;
        if (mpText != null)
            _mpLabel = mpText.text;

        _labelsCached = true;
    }

    void UpdateAbilitiesPanel(PlayerPresetSO preset)
    {
        if (!abilityEntries.HasAnyEntry)
        {
            if (abilitiesRoot != null)
                abilitiesRoot.SetActive(false);
            return;
        }

        var abilities = preset?.abilities ?? new PlayerAbilities();

        SetAbilityEntryActive(AbilityKey.Swim, abilities.swim);
        SetAbilityEntryActive(AbilityKey.Jump, abilities.jump);
        SetAbilityEntryActive(AbilityKey.Climb, abilities.climb);
        SetAbilityEntryActive(AbilityKey.Magic, abilities.magic);
        SetAbilityEntryActive(AbilityKey.Fly, abilities.fly);

        if (abilitiesRoot != null)
        {
            abilitiesRoot.SetActive(abilityEntries.HasAnyEntry);
        }
    }

    void SetAbilityEntryActive(AbilityKey key, bool active)
    {
        var entry = abilityEntries.Get(key);
        if (entry != null)
            entry.SetActive(active);
    }

    void AnimateHealthRestoreFeedback()
    {
        if (hpText == null) return;

        // Cachear el color original
        if (!_hpColorCached)
        {
            _hpOriginalColor = hpText.color;
            _hpColorCached = true;
        }

        // Matar animaciÃ³n previa si existe
        _hpTextTween?.Kill();

        // Color verde para indicar curaciÃ³n
        var healColor = new Color(0.2f, 1f, 0.3f, 1f);

        // Secuencia de animaciÃ³n: escala + color + regreso
        var sequence = DOTween.Sequence();
        sequence.Append(hpText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 8, elasticity: 0.6f).SetUpdate(true));
        sequence.Join(hpText.DOColor(healColor, 0.15f).SetUpdate(true));
        sequence.Append(hpText.DOColor(_hpOriginalColor, 0.25f).SetUpdate(true));
        
        _hpTextTween = sequence;
    }

    void AnimateManaRestoreFeedback()
    {
        if (mpText == null) return;

        // Cachear el color original
        if (!_mpColorCached)
        {
            _mpOriginalColor = mpText.color;
            _mpColorCached = true;
        }

        // Matar animaciÃ³n previa si existe
        _mpTextTween?.Kill();

        // Color azul/cyan para indicar restauraciÃ³n de manÃ¡
        var manaColor = new Color(0.3f, 0.7f, 1f, 1f);

        // Secuencia de animaciÃ³n: escala + color + regreso
        var sequence = DOTween.Sequence();
        sequence.Append(mpText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 8, elasticity: 0.6f).SetUpdate(true));
        sequence.Join(mpText.DOColor(manaColor, 0.15f).SetUpdate(true));
        sequence.Append(mpText.DOColor(_mpOriginalColor, 0.25f).SetUpdate(true));
        
        _mpTextTween = sequence;
    }

    bool EnsureViews()
    {
        bool anyViewConfigured = false;
        
        Debug.Log($"[PlayerEquipmentMenuController] EnsureViews() - Verificando vistas...");
        Debug.Log($"  - _inventoryView: {(_inventoryView != null ? "EXISTS" : "NULL")}");
        Debug.Log($"  - _spellView: {(_spellView != null ? "EXISTS" : "NULL")}");
        Debug.Log($"  - _equipmentView: {(_equipmentView != null ? "EXISTS" : "NULL")}");

        if (_inventoryView == null)
        {
            Debug.Log($"[PlayerEquipmentMenuController] Verificando inventoryUI.IsConfigured...");
            if (inventoryUI.IsConfigured)
            {
                _inventoryView = new InventoryView(inventoryUI);
                anyViewConfigured = true;
                Debug.Log("[PlayerEquipmentMenuController] Vista de inventario creada");
            }
            else if (!_warnedInventory)
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] Inventario no configurado:");
                Debug.LogWarning($"  - root: {(inventoryUI.root != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - rowsParent: {(inventoryUI.rowsParent != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - rowPrefab: {(inventoryUI.rowPrefab != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - itemName: {(inventoryUI.itemName != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - itemDescription: {(inventoryUI.itemDescription != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - itemCount: {(inventoryUI.itemCount != null ? "OK" : "FALTA")}");
                Debug.LogWarning($"  - feedbackText: {(inventoryUI.feedbackText != null ? "OK" : "FALTA")}");
                _warnedInventory = true;
            }
        }
        else
        {
            anyViewConfigured = true;
            Debug.Log("[PlayerEquipmentMenuController] Vista de inventario ya existe");
        }

        if (_spellView == null)
        {
            Debug.Log($"[PlayerEquipmentMenuController] Verificando spellUI.IsConfigured...");
            if (spellUI.IsConfigured)
            {
                _spellView = new SpellView(spellUI);
                anyViewConfigured = true;
                Debug.Log("[PlayerEquipmentMenuController] Vista de hechizos creada");
            }
            else if (!_warnedSpells)
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] Vista de hechizos no configurada: asigna root, botones de slots, contenedor y prefab de filas.");
                _warnedSpells = true;
            }
        }
        else
        {
            anyViewConfigured = true;
            Debug.Log("[PlayerEquipmentMenuController] Vista de hechizos ya existe");
        }

        if (_equipmentView == null)
        {
            Debug.Log($"[PlayerEquipmentMenuController] Verificando equipmentUI.IsConfigured...");
            if (equipmentUI.IsConfigured)
            {
                _equipmentView = new EquipmentView(equipmentUI);
                anyViewConfigured = true;
                Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento creada");
            }
            else if (!_warnedEquipment)
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] Vista de equipamiento no configurada: aÃ±ade filas con categorÃ­a y botones.");
                _warnedEquipment = true;
            }
        }
        else
        {
            anyViewConfigured = true;
            Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento ya existe");
        }

        Debug.Log($"[PlayerEquipmentMenuController] EnsureViews() retornando: {anyViewConfigured}");
        
        if (!anyViewConfigured)
        {
            Debug.LogError("[PlayerEquipmentMenuController] âŒ NINGUNA VISTA ESTÃ CONFIGURADA");
            Debug.LogError("â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—");
            Debug.LogError("â•‘ SOLUCIÃ“N: El PlayerEquipmentMenuController necesita un Canvas UI  â•‘");
            Debug.LogError("â•‘ correctamente configurado con las siguientes vistas:               â•‘");
            Debug.LogError("â• â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•£");
            Debug.LogError("â•‘ 1. Crea un prefab 'PlayerEquipmentMenuCanvas' en la escena        â•‘");
            Debug.LogError("â•‘ 2. Asigna en el Inspector:                                         â•‘");
            Debug.LogError("â•‘    â€¢ Inventory UI: root, rowsParent, rowPrefab, etc.               â•‘");
            Debug.LogError("â•‘    â€¢ Spell UI: root, slotsContainer, etc.                          â•‘");
            Debug.LogError("â•‘    â€¢ Equipment UI: root y categorÃ­as configuradas                  â•‘");
            Debug.LogError("â•‘ 3. AÃ±ade el componente PlayerEquipmentMenuController al Canvas    â•‘");
            Debug.LogError("â•‘ 4. El controller debe estar en la escena Start o como DontDestroy â•‘");
            Debug.LogError("â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
            Debug.LogError($"GameObject actual: '{gameObject.name}' (Canvas: {(canvas != null ? "SÃ­" : "No")}, WindowRoot: {(windowRoot != null ? "SÃ­" : "No")})");
        }
        
        return anyViewConfigured;
    }

    bool IsAllowedInCurrentScene()
    {
        if (string.IsNullOrEmpty(allowedSceneName)) return true;

        var activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() &&
               string.Equals(activeScene.name, allowedSceneName, StringComparison.OrdinalIgnoreCase);
    }

    struct RendererSortState
    {
        public int order;
        public int layer;
    }

    [Serializable]
    struct AbilityEntryReferences
    {
        public GameObject swim;
        public GameObject jump;
        public GameObject climb;
        public GameObject magic;
        public GameObject fly;

        public bool HasAnyEntry => swim != null || jump != null || climb != null || magic != null || fly != null;

        public GameObject Get(AbilityKey key)
        {
            return key switch
            {
                AbilityKey.Swim => swim,
                AbilityKey.Jump => jump,
                AbilityKey.Climb => climb,
                AbilityKey.Magic => magic,
                AbilityKey.Fly => fly,
                _ => null
            };
        }
    }

    [Serializable]
    class InventoryBindings
    {
        public GameObject root;
        public Transform rowsParent;
        public InventoryRowWidget rowPrefab;
        public Text itemName;
        public Text itemDescription;
        public Text itemCount;
        public Text feedbackText;
        public Button useButton;
        
        [Header("Feedback visual")]
        public Color slotSelectionColor = new Color(1f, 0.82f, 0.16f, 1f); // Amarillo para resaltado

        public bool IsConfigured =>
            root != null &&
            rowsParent != null &&
            rowPrefab != null &&
            itemName != null &&
            itemDescription != null &&
            itemCount != null &&
            feedbackText != null;
    }

        class InventoryView
        {
            readonly InventoryBindings _ui;
            readonly List<InventoryRowWidget> _rows = new();
            Inventory _inventory;
            Inventory _boundInventory;
            PlayerPickupCollector _collector;
            ItemData _selectedItem;
            InventoryRowWidget _lastSelectedRow;
            InventoryRowWidget _highlightedRow; // Fila actualmente resaltada (navegaciÃ³n)
            readonly ScrollRect _scrollRect;
            enum InventoryInteractionState { Browsing, UseButtonFocused }
            InventoryInteractionState _interactionState = InventoryInteractionState.Browsing;
            Vector3 _useButtonBaseScale;
            ColorBlock _useButtonDefaultColors;
            bool _useButtonVisualCached;

        public InventoryView(InventoryBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            if (_ui.rowsParent != null)
                _scrollRect = _ui.rowsParent.GetComponentInParent<ScrollRect>();

            if (_ui.useButton != null)
            {
                _ui.useButton.onClick.AddListener(UseSelectedItem);
                _useButtonBaseScale = _ui.useButton.transform.localScale;
                _useButtonDefaultColors = _ui.useButton.colors;
                _useButtonVisualCached = true;

                var nav = _ui.useButton.navigation;
                nav.mode = Navigation.Mode.None;
                _ui.useButton.navigation = nav;
            }
        }

        public GameObject DefaultSelection => _rows.Count > 0 ? _rows[0].ButtonGameObject : null;
        public int RowCount => _rows.Count;

        public void Dispose()
        {
            if (_boundInventory != null)
                _boundInventory.OnInventoryChanged -= HandleInventoryChanged;

            if (_ui.useButton != null)
                _ui.useButton.onClick.RemoveListener(UseSelectedItem);
        }

        public void SetVisible(bool value)
        {
            if (_ui.root != null)
                _ui.root.SetActive(value);

            if (!value && _boundInventory != null)
            {
                _boundInventory.OnInventoryChanged -= HandleInventoryChanged;
                _boundInventory = null;
            }
        }

        public void Refresh(bool rebuildList)
        {
            if (!PlayerService.TryGetComponent(out _inventory, includeInactive: true, allowSceneLookup: true))
                _inventory = null;

            PlayerService.TryGetComponent(out _collector, includeInactive: true, allowSceneLookup: true);

            if (_boundInventory != _inventory)
            {
                if (_boundInventory != null)
                    _boundInventory.OnInventoryChanged -= HandleInventoryChanged;
                if (_inventory != null)
                    _inventory.OnInventoryChanged += HandleInventoryChanged;
                _boundInventory = _inventory;
            }

            if (_inventory == null)
            {
                ClearList();
                UpdateEmptyState("Inventario no disponible");
                return;
            }

            if (rebuildList)
                BuildList();
            else
                UpdateRowTexts();

            // Priorizar restaurar la selecciÃ³n previa; si no existe, enfocar la primera fila para permitir la navegaciÃ³n inmediata
            if (_selectedItem != null)
            {
                UpdateSelectedItemDetails();
            }
            else
            {
                // Limpiar detalles si no hay selecciÃ³n
                if (_ui.itemName != null) _ui.itemName.text = "";
                if (_ui.itemDescription != null) _ui.itemDescription.text = "";
                if (_ui.useButton != null) _ui.useButton.gameObject.SetActive(false);
                _interactionState = InventoryInteractionState.Browsing;
                ResetUseButtonFeedback();
            }
        }

        void BuildList()
        {
            ClearList();

            var items = _inventory.GetAllItems();
            items.Sort((a, b) => string.Compare(a.item ? a.item.displayName : string.Empty,
                                                b.item ? b.item.displayName : string.Empty,
                                                StringComparison.OrdinalIgnoreCase));

            foreach (var entry in items)
            {
                var widget = UnityEngine.Object.Instantiate(_ui.rowPrefab, _ui.rowsParent);
                widget.Configure(entry.item);
                widget.RefreshLabel(_inventory);

                // Garantizar auto-scroll al seleccionar: aÃ±adir/configurar ScrollOnSelectRelay
                var rect = widget.GetComponent<RectTransform>();
                if (rect != null && _scrollRect != null)
                {
                    var relay = widget.GetComponent<ScrollOnSelectRelay>();
                    if (relay == null)
                        relay = widget.gameObject.AddComponent<ScrollOnSelectRelay>();
                    relay.scrollRect = _scrollRect;
                    relay.target = rect;
                }

                var capturedWidget = widget;
                var capturedItem = entry.item;
                widget.RegisterClickHandler(() => HandleRowActivated(capturedWidget, capturedItem, true));
                widget.RegisterSelectedHandler(() => HandleRowActivated(capturedWidget, capturedItem, false));

                _rows.Add(widget);
            }

            UpdateRowNavigation();
            
            // Inicializar sin selecciÃ³n
            _highlightedRow = null;
            _selectedItem = null;
            UpdateRowVisuals();

            if (_rows.Count == 0)
                UpdateEmptyState("Inventario vacÃ­o");
        }

        void HandleRowActivated(InventoryRowWidget widget, ItemData item, bool focus)
        {
            bool selectionChanged = _selectedItem != item;
            _selectedItem = item;
            _highlightedRow = widget;
            _lastSelectedRow = widget; // Asignar tambiÃ©n para que TryHandleSubmit funcione
            
            // Actualizar resaltado visual de todas las filas SIEMPRE (para que se vea al navegar)
            UpdateRowVisuals();
            
            // Solo hacer focus si es necesario
            if (focus)
                FocusRow(widget, true);
            
            UpdateSelectedItemDetails();

            // Si cambiÃ³ la selecciÃ³n, limpiar feedback
            if (selectionChanged)
            {
                ClearFeedbackImmediate();
                ExitUseButtonFocus(false);
            }

            // NO llamar a HandleRowSubmit automÃ¡ticamente - solo con Submit del gamepad
        }

        /// <summary>
        /// Actualiza el resaltado visual de todas las filas (amarillo para la seleccionada)
        /// </summary>
        void UpdateRowVisuals()
        {
            foreach (var row in _rows)
            {
                if (row == null) continue;
                bool isHighlighted = row == _highlightedRow;
                row.SetHighlighted(isHighlighted, _ui.slotSelectionColor);
            }
        }

        void FocusRow(InventoryRowWidget widget, bool forceFocus)
        {
            if (widget == null) return;

            if (forceFocus)
                widget.Focus();

            ScrollToRow(widget);
        }

        void ScrollToRow(InventoryRowWidget widget)
        {
            if (widget == null) return;
            if (_scrollRect == null)
            {
                Debug.LogWarning("[InventoryView] ScrollRect no encontrado en el padre de rowsParent. Verifica que el contenedor estÃ© bajo un ScrollRect.");
                return;
            }
            var rect = widget.GetComponent<RectTransform>();
            ScrollRectAutoScroller.ScrollTo(_scrollRect, rect, 10f);
        }

        void UpdateRowTexts()
        {
            foreach (var widget in _rows)
                widget?.RefreshLabel(_inventory);
        }

        void ClearList()
        {
            foreach (var widget in _rows)
            {
                if (widget != null)
                    UnityEngine.Object.Destroy(widget.gameObject);
            }
            _rows.Clear();
            _selectedItem = null;
            _lastSelectedRow = null;
            _interactionState = InventoryInteractionState.Browsing;
            ResetUseButtonFeedback();
        }

        void UpdateSelectedItemDetails()
        {
            if (_selectedItem == null)
            {
                UpdateEmptyState("Selecciona un objeto");
                return;
            }

            if (_ui.itemName != null)
                _ui.itemName.text = _selectedItem.displayName;

            if (_ui.itemDescription != null)
                _ui.itemDescription.text = string.IsNullOrEmpty(_selectedItem.useDescription) ? "Sin descripciÃ³n." : _selectedItem.useDescription;

            if (_ui.itemCount != null)
            {
                int count = _inventory != null ? _inventory.Count(_selectedItem.itemId) : 0;
                _ui.itemCount.text = $"Cantidad: {count}";
            }

            if (_ui.useButton != null)
            {
                _ui.useButton.gameObject.SetActive(true);
                // El botÃ³n permanece deshabilitado hasta que se haga Submit en el item
                _ui.useButton.interactable = false;
            }
        }

        void UpdateEmptyState(string message)
        {
            if (_ui.itemName != null) _ui.itemName.text = message;
            if (_ui.itemDescription != null) _ui.itemDescription.text = string.Empty;
            if (_ui.itemCount != null) _ui.itemCount.text = string.Empty;
            ClearFeedbackImmediate();
            if (_ui.useButton != null)
            {
                _ui.useButton.interactable = false;
                ResetUseButtonFeedback();
            }
            _interactionState = InventoryInteractionState.Browsing;
        }

        void UseSelectedItem()
        {
            if (_inventory == null || _selectedItem == null) return;
            
            // Cambiar estado pero NO resetear visualmente todavÃ­a
            _interactionState = InventoryInteractionState.Browsing;

            // Detectar quÃ© efectos tiene el item para animar despuÃ©s
            bool hasHealthRestore = false;
            bool hasManaRestore = false;
            
            if (_selectedItem.useEffects != null)
            {
                foreach (var effect in _selectedItem.useEffects)
                {
                    if (effect.effectType == PickupEffectType.HealthRestore)
                        hasHealthRestore = true;
                    else if (effect.effectType == PickupEffectType.ManaRestore)
                        hasManaRestore = true;
                }
            }

            var context = new InventoryItemUseContext(_inventory, _selectedItem, _collector);
            var result = DispatchInventoryUseRequest(context);

            if (!result.handled)
            {
                if (!InventoryUseUtility.TryUseItem(_inventory, _selectedItem, _collector, out var reason, out var consumed))
                {
                    ShowFeedback(string.IsNullOrEmpty(reason) ? "No se pudo usar." : reason);
                    // Resetear el botÃ³n porque fallÃ³
                    ResetUseButtonAfterUse(false);
                    return;
                }

                result.handled = true;
                result.consumed = consumed;
            }

            if (result.consumed && _inventory.Count(_selectedItem.itemId) == 0)
                _selectedItem = null;

            Refresh(true);

            EnsureSelection();

            if (string.IsNullOrEmpty(result.message))
                result.message = "Usado correctamente.";

            ShowFeedback(result.message);

            // Refrescar panel de estadÃ­sticas inmediatamente (especialmente al usar pociones)
            Instance?.UpdatePlayerInfoPanel();
            
            // Animar feedback visual segÃºn el tipo de efecto
            if (hasHealthRestore)
                Instance?.AnimateHealthRestoreFeedback();
            if (hasManaRestore)
                Instance?.AnimateManaRestoreFeedback();
            
            // Resetear el botÃ³n despuÃ©s de un breve delay para que se vea el efecto
            ResetUseButtonAfterUse(true);
        }
        
        void ResetUseButtonAfterUse(bool restoreSelection)
        {
            // PequeÃ±o delay para que se vean las animaciones antes de resetear
            if (_ui.useButton != null)
            {
                _ui.useButton.transform
                    .DOScale(_useButtonBaseScale, 0.2f)
                    .SetDelay(0.3f)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true)
                    .OnComplete(() => {
                        if (_ui.useButton != null)
                        {
                            _ui.useButton.interactable = false;
                            _ui.useButton.colors = _useButtonDefaultColors;
                            _ui.useButton.transform.localScale = _useButtonBaseScale;
                        }
                        
                        if (restoreSelection && _lastSelectedRow != null)
                            FocusRow(_lastSelectedRow, true);
                    });
            }
            else if (restoreSelection && _lastSelectedRow != null)
            {
                FocusRow(_lastSelectedRow, true);
            }
        }

        static void ShowFeedback(string message)
        {
            var instance = Instance;
            var view = instance?._inventoryView;
            if (view == null || view._ui.feedbackText == null)
                return;

            if (instance._clearFeedbackRoutine != null)
                instance.StopCoroutine(instance._clearFeedbackRoutine);

            view._ui.feedbackText.text = message ?? string.Empty;

            if (instance.feedbackDuration > 0f)
                instance._clearFeedbackRoutine = instance.StartCoroutine(view.ClearFeedbackAfterDelay(instance.feedbackDuration));
        }

        static void ClearFeedbackImmediate()
        {
            var instance = Instance;
            var view = instance?._inventoryView;
            if (view == null)
                return;

            if (view._ui.feedbackText != null)
                view._ui.feedbackText.text = string.Empty;

            if (instance._clearFeedbackRoutine != null)
            {
                instance.StopCoroutine(instance._clearFeedbackRoutine);
                instance._clearFeedbackRoutine = null;
            }
        }

        System.Collections.IEnumerator ClearFeedbackAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ClearFeedbackImmediate();
        }

        void HandleInventoryChanged(ItemData item, int newAmount)
        {
            Refresh(false);
        }

        public bool EnsureSelection()
        {
            if (_rows.Count == 0) return false;

            // Solo restaurar el foco si ya habÃ­a una selecciÃ³n previa
            // No forzar selecciÃ³n automÃ¡tica al abrir el menÃº
            if (_lastSelectedRow != null)
            {
                _lastSelectedRow.Focus();
                return true;
            }

            var first = _rows[0];
            HandleRowActivated(first, first.Item, true);
            return true;
        }

        public System.Collections.IEnumerator EnsureSelectionDelayed()
        {
            // Esperar un frame para que Unity cree los elementos
            yield return null;
            
            if (_rows.Count == 0) yield break;

            // Restaurar la selecciÃ³n previa si existe
            if (_lastSelectedRow != null)
            {
                yield return null;
                _lastSelectedRow.Focus();
                yield break;
            }

            // Seleccionar el primer elemento
            var first = _rows[0];
            if (first != null && first.ButtonGameObject != null)
            {
                yield return null;
                HandleRowActivated(first, first.Item, true);
                Debug.Log($"[PlayerEquipmentMenu] Inventario - Seleccionado: {first.Item?.displayName}");
            }
        }

        void UpdateRowNavigation()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var button = _rows[i] != null ? _rows[i].GetComponent<Button>() : null;
                if (button == null) continue;

                var nav = button.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? _rows[i - 1]?.GetComponent<Button>() : button;
                nav.selectOnDown = i < _rows.Count - 1 ? _rows[i + 1]?.GetComponent<Button>() : button;
                button.navigation = nav;
            }
        }

        bool CanUseSelectedItem()
        {
            if (_selectedItem == null || !_selectedItem.usableFromInventory)
                return false;
            if (_inventory == null)
                return false;
            return _inventory.Count(_selectedItem.itemId) > 0;
        }

        void HandleRowSubmit()
        {
            if (!CanUseSelectedItem())
                return;

            FocusUseButton();
        }

        void FocusUseButton()
        {
            if (_ui.useButton == null) return;
            if (_selectedItem == null || !_selectedItem.usableFromInventory) return;

            _interactionState = InventoryInteractionState.UseButtonFocused;

            // Habilitar el botÃ³n si no lo estÃ¡
            if (!_ui.useButton.interactable)
                _ui.useButton.interactable = true;

            _ui.useButton.Select();
            PlayUseButtonFeedback();
            
            // Reproducir sonido de selecciÃ³n/confirmaciÃ³n
            GamepadInputReader.PlayUISound("UI_Select");
        }

        void ExitUseButtonFocus(bool restoreSelection)
        {
            if (_interactionState != InventoryInteractionState.UseButtonFocused)
                return;

            _interactionState = InventoryInteractionState.Browsing;
            ResetUseButtonFeedback();
            
            // Deshabilitar el botÃ³n al volver a la lista
            if (_ui.useButton != null)
                _ui.useButton.interactable = false;

            if (restoreSelection && _lastSelectedRow != null)
                FocusRow(_lastSelectedRow, true);
        }

        void PlayUseButtonFeedback()
        {
            if (_ui.useButton == null || !_ui.useButton.interactable)
                return;

            if (!_useButtonVisualCached)
            {
                _useButtonBaseScale = _ui.useButton.transform.localScale;
                _useButtonDefaultColors = _ui.useButton.colors;
                _useButtonVisualCached = true;
            }

            // Cambiar color a amarillo/dorado activo - MUY IMPORTANTE: hacer esto SIEMPRE
            var accent = new Color(1f, 0.85f, 0.2f, 1f);
            var colors = new ColorBlock
            {
                normalColor = accent,
                highlightedColor = accent * 1.05f,
                selectedColor = accent * 1.05f,
                pressedColor = accent * 0.9f,
                disabledColor = _useButtonDefaultColors.disabledColor,
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
            _ui.useButton.colors = colors;

            // Primero establecer la escala aumentada (110%)
            var targetScale = _useButtonBaseScale * 1.1f;
            
            // AnimaciÃ³n de punch/rebote desde la escala base a la escala aumentada
            _ui.useButton.transform.localScale = _useButtonBaseScale;
            _ui.useButton.transform
                .DOScale(targetScale, 0.2f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => {
                    // Asegurarse de que el color y la escala persistan FORZANDO el color de nuevo
                    if (_ui.useButton != null)
                    {
                        _ui.useButton.transform.localScale = targetScale;
                        // FORZAR el color de nuevo despuÃ©s de la animaciÃ³n
                        _ui.useButton.colors = colors;
                    }
                });
        }

        void ResetUseButtonFeedback()
        {
            if (_ui.useButton == null || !_useButtonVisualCached)
                return;

            _ui.useButton.transform.localScale = _useButtonBaseScale;
            _ui.useButton.colors = _useButtonDefaultColors;
        }

        public bool TryHandleCancel()
        {
            if (_interactionState == InventoryInteractionState.UseButtonFocused)
            {
                ExitUseButtonFocus(true);
                return true;
            }
            return false;
        }

        public bool TryHandleSubmit()
        {
            Debug.Log($"[InventoryView] TryHandleSubmit - Estado: {_interactionState}, SelectedRow: {(_lastSelectedRow != null ? "OK" : "NULL")}, SelectedItem: {(_selectedItem != null ? _selectedItem.displayName : "NULL")}");
            
            if (_interactionState == InventoryInteractionState.UseButtonFocused)
            {
                // Segunda pulsaciÃ³n: Usar el item
                Debug.Log("[InventoryView] Segunda pulsaciÃ³n - Usando item");
                UseSelectedItem();
                return true;
            }

            if (_lastSelectedRow != null && _selectedItem != null)
            {
                // Primera pulsaciÃ³n: Enfocar botÃ³n de usar
                Debug.Log("[InventoryView] Primera pulsaciÃ³n - Enfocando botÃ³n de usar");
                HandleRowSubmit();
                return true;
            }

            Debug.Log("[InventoryView] TryHandleSubmit - No hay nada que hacer");
            return false;
        }
    }

    [Serializable]
    class SpellBindings
    {
        public GameObject root;
        
        [Header("Slots - BotÃ³n izquierdo (X)")]
        public Button leftSlotButton;
        public Text leftSlotLabel;

        [Header("Slots - BotÃ³n derecho (B)")]
        public Button rightSlotButton;
        public Text rightSlotLabel;

        [Header("Slots - BotÃ³n especial (Y)")]
        public Button specialSlotButton;
        public Text specialSlotLabel;
        
        [Header("Lista de hechizos")]
        public Transform rowsParent;
        public SpellRowWidget rowPrefab;
        public Text detailsText;
        
        [Header("Feedback visual")]
        public Color slotSelectionColor = new Color(1f, 0.82f, 0.16f, 1f);

        public bool IsConfigured =>
            root != null &&
            leftSlotButton != null &&
            rightSlotButton != null &&
            specialSlotButton != null &&
            leftSlotLabel != null &&
            rightSlotLabel != null &&
            specialSlotLabel != null &&
            rowsParent != null &&
            rowPrefab != null &&
            detailsText != null;
    }

    class SpellView
    {
        enum FocusArea
        {
            Slots,
            SpellList
        }

        enum AssignmentMode
        {
            None,
            WaitingForSpellSelection,
            WaitingForSlotSelection
        }

        readonly SpellBindings _ui;
        readonly List<RowEntry> _rows = new();
        readonly Dictionary<Button, ColorBlock> _slotDefaultColors = new();
        readonly Dictionary<Button, MagicSlot> _buttonToSlot = new();
        readonly Dictionary<MagicSlot, Button> _slotToButton = new();
        readonly Dictionary<Button, Vector3> _slotBaseScales = new();
        readonly Dictionary<Button, Tween> _slotFeedbackTweens = new();
        readonly ScrollRect _scrollRect;

        PlayerPresetSO _preset;
        SpellLibrarySO _library;
        PlayerPresetService _presetService;
        SpellId _highlightedSpell = SpellId.None;
        RowEntry _highlightedRow;
        SpellId _pendingSpell = SpellId.None;
        MagicSlot _pendingSlot = MagicSlot.Left;
        MagicSlot _focusedSlot = MagicSlot.Left;
        FocusArea _focusArea = FocusArea.SpellList;
        AssignmentMode _assignmentMode = AssignmentMode.None;

        class RowEntry
        {
            public SpellId spellId;
            public SpellRowWidget widget;
        }

        public SpellView(SpellBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            ConfigureSlotButton(_ui.leftSlotButton, MagicSlot.Left);
            ConfigureSlotButton(_ui.rightSlotButton, MagicSlot.Right);
            ConfigureSlotButton(_ui.specialSlotButton, MagicSlot.Special);
            if (_ui.rowsParent != null)
                _scrollRect = _ui.rowsParent.GetComponentInParent<ScrollRect>();
        }

        public GameObject DefaultSelection
        {
            get
            {
                if (_rows.Count > 0)
                {
                    var first = _rows[0]?.widget;
                    if (first != null)
                        return first.ButtonGameObject;
                }
                if (_ui.leftSlotButton != null)
                    return _ui.leftSlotButton.gameObject;
                if (_ui.rightSlotButton != null)
                    return _ui.rightSlotButton.gameObject;
                if (_ui.specialSlotButton != null)
                    return _ui.specialSlotButton.gameObject;
                return _ui.root;
            }
        }

        public void SetVisible(bool value)
        {
            if (_ui.root != null)
                _ui.root.SetActive(value);
            if (!value)
            {
                CancelSlotSelection(true);
                KillAllSlotFeedback();
            }
        }

        public void Refresh()
        {
            if (!(GameBootService.IsAvailable && GameBootService.Profile != null))
            {
                _preset = null;
                _library = null;
                _presetService = null;
                CancelSlotSelection(true);
                ClearList();
                UpdateSlotLabels();
                ShowSpellDetails(SpellId.None);
                return;
            }

            _preset = GameBootService.Profile.GetActivePresetResolved();
            PlayerService.TryGetComponent(out _presetService, includeInactive: true, allowSceneLookup: true);
            _library = _presetService != null ? _presetService.SpellLibrary : null;

            EnforcePresetSlotRules();
            UpdateSlotLabels();
            BuildSpellList();
            UpdateSlotButtonVisuals();
            CancelSlotSelection(true);
            // No seleccionar ningÃºn hechizo al abrir, limpiar detalles
            _highlightedSpell = SpellId.None;
            _highlightedRow = null;
            ShowSpellDetails(SpellId.None);
        }

        public void HandleInput()
        {
            if (_ui.root == null || !_ui.root.activeInHierarchy)
                return;

            if (_assignmentMode == AssignmentMode.WaitingForSlotSelection)
            {
                if (_focusArea != FocusArea.Slots)
                    FocusSlots(_focusedSlot);
                return;
            }

            if (_focusArea != FocusArea.SpellList)
                FocusSpellList();
        }

        void FocusSlots(MagicSlot? slotOverride = null)
        {
            if (_slotToButton.Count == 0)
                return;

            var targetSlot = slotOverride ?? _focusedSlot;
            if (_assignmentMode == AssignmentMode.WaitingForSlotSelection && !CanAssign(targetSlot, _pendingSpell))
            {
                foreach (var candidate in _slotToButton.Keys)
                {
                    if (!CanAssign(candidate, _pendingSpell)) continue;
                    targetSlot = candidate;
                    break;
                }
            }

            if (!_slotToButton.TryGetValue(targetSlot, out var button) || button == null)
            {
                foreach (var kvp in _slotToButton)
                {
                    if (kvp.Value != null)
                    {
                        targetSlot = kvp.Key;
                        button = kvp.Value;
                        break;
                    }
                }
            }

            _focusedSlot = targetSlot;
            _focusArea = FocusArea.Slots;

            if (button != null)
            {
                var es = EventSystem.current;
                if (es != null)
                    es.SetSelectedGameObject(button.gameObject);
            }
        }

        void FocusSpellList()
        {
            if (_rows.Count == 0)
                return;

            if (_highlightedRow == null)
                SelectFirstRow();

            _focusArea = FocusArea.SpellList;
            _highlightedRow?.widget?.Focus();
        }

        MagicSlot GetPreferredSlotForSpell(SpellId spellId)
        {
            if (spellId != SpellId.None && _library != null)
            {
                var spell = _library.Get(spellId);
                if (spell != null && spell.slotType == SpellSlotType.SpecialOnly)
                    return MagicSlot.Special;
            }

            if (CanAssign(MagicSlot.Left, spellId)) return MagicSlot.Left;
            if (CanAssign(MagicSlot.Right, spellId)) return MagicSlot.Right;
            if (CanAssign(MagicSlot.Special, spellId)) return MagicSlot.Special;
            return MagicSlot.Left;
        }

        void UpdateSlotLabels()
        {
            UpdateSlotLabel(_ui.leftSlotLabel, _preset != null ? _preset.leftSpellId : SpellId.None);
            UpdateSlotLabel(_ui.rightSlotLabel, _preset != null ? _preset.rightSpellId : SpellId.None);
            UpdateSlotLabel(_ui.specialSlotLabel, _preset != null ? _preset.specialSpellId : SpellId.None);
        }

        void UpdateSlotLabel(Text label, SpellId spellId)
        {
            if (label == null) return;
            label.gameObject.SetActive(true);
            label.text = ResolveName(spellId);
        }

        void BuildSpellList()
        {
            ClearList();

            if (_preset == null)
                return;

            var added = new HashSet<SpellId>();

            if (_preset.unlockedSpells != null)
            {
                foreach (var id in _preset.unlockedSpells)
                {
                    if (!added.Add(id)) continue;
                    AddSpellRow(id);
                }
            }

            // No seleccionar ninguna fila al abrir
            _highlightedRow = null;
            _highlightedSpell = SpellId.None;
            UpdateRowVisuals();
            ConfigureRowNavigation();
            UpdateSlotNavigationTargets();
        }

        void AddSpellRow(SpellId spellId)
        {
            var widget = UnityEngine.Object.Instantiate(_ui.rowPrefab, _ui.rowsParent);
            widget.SetLabel(ResolveName(spellId));
            widget.SetIcon(GetSpellAsset(spellId)?.attackIcon);
            var rowEntry = new RowEntry { spellId = spellId, widget = widget };
            widget.RegisterClickHandler(() => HandleRowClicked(rowEntry));
            widget.RegisterSelectedHandler(() => HandleRowSelected(rowEntry, true));

            _rows.Add(rowEntry);
        }

        void HandleRowSelected(RowEntry entry, bool fromUser)
        {
            if (entry == null) return;

            _highlightedSpell = entry.spellId;
            _highlightedRow = entry;

            // Only force selection when the EventSystem is not already pointing at this row.
            if (!fromUser)
            {
                entry.widget?.Focus();
            }
            else
            {
                entry.widget?.Focus();
            }

            if (fromUser)
                _focusArea = FocusArea.SpellList;
            UpdateRowVisuals();
            ShowSpellDetails(entry.spellId);
            ScrollToEntry(entry);
        }

        void UpdateRowVisuals()
        {
            foreach (var row in _rows)
            {
                if (row?.widget == null) continue;
                bool isSelected = row == _highlightedRow;
                row.widget.SetHighlighted(isSelected, _ui.slotSelectionColor);
            }
        }

        void HandleRowClicked(RowEntry entry)
        {
            if (entry == null) return;

            if (_assignmentMode == AssignmentMode.WaitingForSpellSelection)
            {
                AssignSpellToSlot(_pendingSlot, entry.spellId);
                CompleteAssignment();
                return;
            }

            _pendingSpell = entry.spellId;
            _assignmentMode = AssignmentMode.WaitingForSlotSelection;
            _focusedSlot = GetPreferredSlotForSpell(entry.spellId);
            _pendingSlot = _focusedSlot;
            FocusSlots(_focusedSlot);
            UpdateSlotButtonVisuals();
            ShowSpellDetails(entry.spellId);
        }

        void HandleSlotButtonPressed(MagicSlot slot)
        {
            if (_assignmentMode == AssignmentMode.WaitingForSlotSelection)
            {
                _focusedSlot = slot;
                if (!CanAssign(slot, _pendingSpell)) return;
                AssignSpellToSlot(slot, _pendingSpell);
                CompleteAssignment();
                return;
            }

            if (_assignmentMode == AssignmentMode.WaitingForSpellSelection)
            {
                if (!CanAssign(slot, _pendingSpell)) return;
                AssignSpellToSlot(slot, _pendingSpell);
                CompleteAssignment();
                return;
            }

            _pendingSlot = slot;
            _assignmentMode = AssignmentMode.WaitingForSpellSelection;
            _focusArea = FocusArea.SpellList;
            FocusSpellList();
            UpdateSlotButtonVisuals();
            ShowSpellDetails(_highlightedSpell);
        }

        void BeginSpellSelectionFromSlot(MagicSlot slot)
        {
            _pendingSlot = slot;
            _pendingSpell = SpellId.None;
            _assignmentMode = AssignmentMode.WaitingForSpellSelection;
            FocusSpellList();
            UpdateSlotButtonVisuals();
            ShowSpellDetails(_highlightedSpell);
        }

        void AssignSpellToSlot(MagicSlot slot, SpellId id)
        {
            if (_preset == null) return;

            // Primero limpiamos duplicados de otros slots antes de asignar
            // para evitar que ConfigureSpells vea duplicados temporales
            EnsureUniqueAssignment(id, slot);

            switch (slot)
            {
                case MagicSlot.Left: _preset.leftSpellId = id; break;
                case MagicSlot.Right: _preset.rightSpellId = id; break;
                case MagicSlot.Special: _preset.specialSpellId = id; break;
            }

            // No restaurar inventario al cambiar hechizos (solo actualizar spells)
            _presetService?.ApplyCurrentPreset(includeInventory: false, includeAbilities: false);
            UpdateSlotLabels();
            PlaySlotConfirmFeedback(slot);
        }

        void EnsureUniqueAssignment(SpellId id, MagicSlot targetSlot)
        {
            if (id == SpellId.None) return;

            if (targetSlot != MagicSlot.Left && _preset.leftSpellId == id)
                _preset.leftSpellId = SpellId.None;
            if (targetSlot != MagicSlot.Right && _preset.rightSpellId == id)
                _preset.rightSpellId = SpellId.None;
            if (targetSlot != MagicSlot.Special && _preset.specialSpellId == id)
                _preset.specialSpellId = SpellId.None;
        }

        void CompleteAssignment()
        {
            _assignmentMode = AssignmentMode.None;
            _pendingSpell = SpellId.None;
            UpdateSlotButtonVisuals();
            FocusSpellList();
            ShowSpellDetails(_highlightedSpell);
        }

        public void CancelSlotSelection(bool silent)
        {
            ResetState(silent);
        }

        void ResetState(bool silent)
        {
            _assignmentMode = AssignmentMode.None;
            _pendingSpell = SpellId.None;
            _pendingSlot = MagicSlot.Left;
            _focusArea = FocusArea.SpellList;
            _focusedSlot = MagicSlot.Left;
            UpdateSlotButtonVisuals();
            if (!silent)
            {
                FocusSpellList();
                ShowSpellDetails(_highlightedSpell);
            }
        }

        public bool TryHandleCancel()
        {
            if (_assignmentMode == AssignmentMode.None)
                return false;

            CancelSlotSelection(false);
            return true;
        }

        void ShowSpellDetails(SpellId id)
        {
            if (_ui.detailsText == null) return;

            string description;

            if (id == SpellId.None)
            {
                description = "Sin asignar.";
            }
            else
            {
                var spell = GetSpellAsset(id);
                if (spell == null)
                {
                    description = "Hechizo sin informaciÃ³n.";
                }
                else
                {
                    description = $"{spell.displayName}\nDaÃ±o: {spell.damage}\nCoste de manÃ¡: {spell.manaCost}\nCooldown: {spell.cooldown:F2}s";
                }
            }

            switch (_assignmentMode)
            {
                case AssignmentMode.WaitingForSpellSelection:
                    description += "\nSelecciona un hechizo con A o cancela con B.";
                    break;
                case AssignmentMode.WaitingForSlotSelection:
                    description += "\nSelecciona un slot con A o cancela con B.";
                    break;
                default:
                    description += "\nPulsa A sobre un hechizo y luego escoge el slot al que asignarlo.";
                    break;
            }

            _ui.detailsText.text = description;
        }

        void ClearList()
        {
            foreach (var entry in _rows)
            {
                if (entry?.widget != null)
                    UnityEngine.Object.Destroy(entry.widget.gameObject);
            }
            _rows.Clear();
            _highlightedRow = null;
        }

        bool SelectRow(SpellId id)
        {
            if (_rows.Count == 0) return false;

            foreach (var entry in _rows)
            {
                if (entry == null || entry.spellId != id) continue;
                HandleRowSelected(entry, false);
                return true;
            }

            return false;
        }

        void SelectFirstRow()
        {
            if (_rows.Count == 0) return;
            var first = _rows[0];
            HandleRowSelected(first, false);
        }

        void ScrollToEntry(RowEntry entry)
        {
            if (_scrollRect == null || entry?.widget == null)
                return;
            var rect = entry.widget.GetComponent<RectTransform>();
            ScrollRectAutoScroller.ScrollTo(_scrollRect, rect, 10f);
        }

        void ConfigureSlotButton(Button button, MagicSlot slot)
        {
            if (button == null) return;

            button.onClick.AddListener(() => HandleSlotButtonPressed(slot));
            var listener = button.gameObject.GetComponent<SlotSelectListener>();
            if (listener == null)
                listener = button.gameObject.AddComponent<SlotSelectListener>();
            var capturedSlot = slot;
            listener.onSelect = () => HandleSlotFocused(button, capturedSlot);

            if (!_slotDefaultColors.ContainsKey(button))
                _slotDefaultColors[button] = button.colors;

            if (!_buttonToSlot.ContainsKey(button))
                _buttonToSlot[button] = slot;
            if (!_slotToButton.ContainsKey(slot))
                _slotToButton[slot] = button;
        }

        void ConfigureSlotNavigation(Button button, Button up, Button down, Selectable right)
        {
            if (button == null) return;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = up != null ? up : button;
            nav.selectOnDown = down != null ? down : button;
            nav.selectOnLeft = button;
            nav.selectOnRight = right;
            button.navigation = nav;
        }

        void UpdateSlotNavigationTargets()
        {
            var firstRowSelectable = GetFirstRowSelectable();
            ConfigureSlotNavigation(_ui.leftSlotButton, _ui.specialSlotButton, _ui.rightSlotButton, firstRowSelectable);
            ConfigureSlotNavigation(_ui.rightSlotButton, _ui.leftSlotButton, _ui.specialSlotButton, firstRowSelectable);
            ConfigureSlotNavigation(_ui.specialSlotButton, _ui.rightSlotButton, _ui.leftSlotButton, firstRowSelectable);
        }

        Selectable GetFirstRowSelectable()
        {
            return _rows.Count > 0 ? _rows[0]?.widget?.Selectable : null;
        }

        Button GetPrimarySlotSelectable()
        {
            if (_ui.leftSlotButton != null && _ui.leftSlotButton.IsInteractable())
                return _ui.leftSlotButton;
            if (_ui.rightSlotButton != null && _ui.rightSlotButton.IsInteractable())
                return _ui.rightSlotButton;
            if (_ui.specialSlotButton != null && _ui.specialSlotButton.IsInteractable())
                return _ui.specialSlotButton;
            return null;
        }

        void UpdateSlotButtonVisuals()
        {
            UpdateSlotButtonState(_ui.leftSlotButton, MagicSlot.Left);
            UpdateSlotButtonState(_ui.rightSlotButton, MagicSlot.Right);
            UpdateSlotButtonState(_ui.specialSlotButton, MagicSlot.Special);
        }

        void UpdateSlotButtonState(Button button, MagicSlot slot)
        {
            if (button == null) return;

            bool canAssignPending = _assignmentMode == AssignmentMode.WaitingForSlotSelection && CanAssign(slot, _pendingSpell);
            bool isWaitingForSlot = _assignmentMode == AssignmentMode.WaitingForSlotSelection;

            if (isWaitingForSlot)
            {
                if (canAssignPending)
                {
                    PlaySlotPulseFeedback(slot);
                }
                else
                {
                    KillSlotFeedback(slot);
                    if (_slotDefaultColors.TryGetValue(button, out var defaultColors))
                        button.colors = defaultColors;
                }
            }
            else
            {
                KillSlotFeedback(slot);
                if (_slotDefaultColors.TryGetValue(button, out var defaultColors))
                    button.colors = defaultColors;
            }
        }

        void HandleSlotFocused(Button button, MagicSlot slot)
        {
            _focusedSlot = slot;
        }

        bool CanAssign(MagicSlot slot, SpellId spellId)
        {
            if (spellId == SpellId.None) return true;
            if (_library == null) return false;

            var spell = _library.Get(spellId);
            if (spell == null) return false;

            if (spell.slotType == SpellSlotType.SpecialOnly)
                return slot == MagicSlot.Special;
            
            // SpellSlotType.Any puede ir en cualquier slot
            return true;
        }

        void EnforcePresetSlotRules()
        {
            if (_preset == null || _library == null) return;

            if (!CanAssign(MagicSlot.Left, _preset.leftSpellId))
                _preset.leftSpellId = SpellId.None;
            if (!CanAssign(MagicSlot.Right, _preset.rightSpellId))
                _preset.rightSpellId = SpellId.None;
            if (!CanAssign(MagicSlot.Special, _preset.specialSpellId))
                _preset.specialSpellId = SpellId.None;
        }

        string ResolveName(SpellId id)
        {
            if (id == SpellId.None) return "Sin asignar";
            var spell = GetSpellAsset(id);
            return spell != null ? spell.displayName : id.ToString();
        }

        MagicSpellSO GetSpellAsset(SpellId id)
        {
            return _library != null ? _library.Get(id) : null;
        }

        void PlaySlotPulseFeedback(MagicSlot slot)
        {
            if (!_slotToButton.TryGetValue(slot, out var button) || button == null)
                return;

            KillSlotFeedback(slot);

            if (!_slotDefaultColors.ContainsKey(button))
                _slotDefaultColors[button] = button.colors;

            if (!_slotBaseScales.ContainsKey(button))
                _slotBaseScales[button] = button.transform.localScale;

            var pulseColor = new Color(1f, 0.9f, 0.3f, 1f);
            var colors = button.colors;
            colors.normalColor = pulseColor;
            colors.highlightedColor = pulseColor * 1.1f;
            colors.selectedColor = pulseColor * 1.1f;
            button.colors = colors;

            var baseScale = _slotBaseScales[button];
            var tween = button.transform
                .DOPunchScale(Vector3.one * 0.1f, 0.6f, vibrato: 4, elasticity: 0.5f)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
            _slotFeedbackTweens[button] = tween;
        }

        void PlaySlotConfirmFeedback(MagicSlot slot)
        {
            if (!_slotToButton.TryGetValue(slot, out var button) || button == null)
                return;

            KillSlotFeedback(slot);

            if (!_slotBaseScales.ContainsKey(button))
                _slotBaseScales[button] = button.transform.localScale;

            var confirmColor = new Color(0.3f, 1f, 0.3f, 1f);
            var colors = button.colors;
            colors.normalColor = confirmColor;
            colors.highlightedColor = confirmColor * 1.1f;
            colors.selectedColor = confirmColor * 1.1f;
            button.colors = colors;

            var baseScale = _slotBaseScales[button];
            var tween = button.transform
                .DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 8, elasticity: 0.6f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (button != null && _slotDefaultColors.TryGetValue(button, out var defaultColors))
                    {
                        button.colors = defaultColors;
                        if (_slotBaseScales.TryGetValue(button, out var bs))
                            button.transform.localScale = bs;
                    }
                });
            _slotFeedbackTweens[button] = tween;
        }

        void KillSlotFeedback(MagicSlot slot)
        {
            if (!_slotToButton.TryGetValue(slot, out var button) || button == null)
                return;

            if (_slotFeedbackTweens.TryGetValue(button, out var tween))
            {
                tween?.Kill();
                _slotFeedbackTweens.Remove(button);
            }

            if (_slotBaseScales.TryGetValue(button, out var baseScale))
                button.transform.localScale = baseScale;
        }

        void KillAllSlotFeedback()
        {
            foreach (var kvp in _slotFeedbackTweens)
                kvp.Value?.Kill();
            _slotFeedbackTweens.Clear();

            foreach (var kvp in _slotToButton)
            {
                var button = kvp.Value;
                if (button != null && _slotBaseScales.TryGetValue(button, out var baseScale))
                    button.transform.localScale = baseScale;
            }
        }

        void ConfigureRowNavigation()
        {
            var leftTarget = GetPrimarySlotSelectable();
            for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                var entry = _rows[rowIndex];
                if (entry?.widget?.Selectable != null)
                {
                    ConfigureButtonNavigation(entry.widget.Selectable, rowIndex, leftTarget);
                }
            }
        }

        void ConfigureButtonNavigation(Selectable button, int rowIndex, Selectable leftTarget)
        {
            if (button == null) return;

            var nav = button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = leftTarget;
            nav.selectOnRight = button;
            nav.selectOnUp = ResolveVertical(rowIndex, -1);
            nav.selectOnDown = ResolveVertical(rowIndex, +1);
            button.navigation = nav;
        }

        Selectable ResolveVertical(int rowIndex, int step)
        {
            int idx = rowIndex;
            while (true)
            {
                idx += step;
                if (idx < 0 || idx >= _rows.Count)
                    break;
                var row = _rows[idx];
                if (row?.widget?.Selectable != null && IsSelectable(row.widget.Selectable))
                    return row.widget.Selectable;
            }

            var currentRow = GetRow(rowIndex);
            if (currentRow?.widget?.Selectable != null && IsSelectable(currentRow.widget.Selectable))
                return currentRow.widget.Selectable;
            
            return null;
        }

        RowEntry GetRow(int index)
        {
            if (index < 0 || index >= _rows.Count) return null;
            return _rows[index];
        }

        static bool IsSelectable(Selectable selectable)
        {
            return selectable != null && selectable.IsInteractable();
        }

        class SlotSelectListener : MonoBehaviour, ISelectHandler
        {
            public System.Action onSelect;
            public void OnSelect(BaseEventData eventData) => onSelect?.Invoke();
        }
    }

    [Serializable]
    class EquipmentBindings
    {
        public GameObject root;
        public EquipmentBindings.RowBinding[] rows;
        [Header("Feedback visual")]
        public Color rowSelectionColor = new Color(1f, 0.82f, 0.16f, 1f);

        public bool IsConfigured => root != null && rows != null && rows.Length > 0;

        [Serializable]
        public class RowBinding
        {
            public bool enabled = true;
            public PartCategory category;
            public Text label;
            public Button previousButton;
            public Button nextButton;
            public Button clearButton;
        }
    }

    class EquipmentView
    {
        readonly EquipmentBindings _ui;
        readonly Dictionary<PartCategory, EquipmentBindings.RowBinding> _rows = new();
        readonly List<EquipmentBindings.RowBinding> _orderedRows = new();
        bool _rowOrderDirty = true;
        Component _builder;
        Component _wardrobe;
        Component _boundWardrobe;
        PlayerPresetService _presetService;

        public EquipmentView(EquipmentBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            if (_ui.rows != null)
            {
                foreach (var row in _ui.rows)
                {
                    if (row == null || !row.enabled) continue;
                    _rows[row.category] = row;
                    var capturedCategory = row.category;
                    if (row.previousButton != null)
                        row.previousButton.onClick.AddListener(() => Cycle(capturedCategory, -1));
                    if (row.nextButton != null)
                        row.nextButton.onClick.AddListener(() => Cycle(capturedCategory, +1));
                    if (row.clearButton != null)
                        row.clearButton.onClick.AddListener(() => Clear(capturedCategory));
                }
            }
        }

        public GameObject DefaultSelection
        {
            get
            {
                var ordered = GetOrderedRows();
                int idx = 0;
                Button btn = null;
                while (btn == null && idx < 3 && idx < ordered.Count)
                {
                    var row = ordered[idx];
                    for (int columnIndex = 0; columnIndex < 3; columnIndex++)
                    {
                        btn = GetButtonByColumn(row, columnIndex);
                        if (IsSelectable(btn)) return btn.gameObject;
                    }
                    idx++;
                }
                return _ui.root;
            }
        }

        public void SetVisible(bool value)
        {
            if (_ui.root != null)
                _ui.root.SetActive(value);
        }

        public void Refresh()
        {
            PlayerService.TryGetComponent(out _builder, includeInactive: true, allowSceneLookup: true);
            PlayerService.TryGetComponent(out _wardrobe, includeInactive: true, allowSceneLookup: true);

            if (_boundWardrobe != _wardrobe)
            {
                if (_boundWardrobe != null)
                {
                    var prevType = _boundWardrobe.GetType();
                    var prevEvent = prevType.GetEvent("OnWardrobeChanged");
                    if (prevEvent != null)
                    {
                        try
                        {
                            var handler = System.Delegate.CreateDelegate(prevEvent.EventHandlerType, this, nameof(HandleWardrobeChanged));
                            prevEvent.RemoveEventHandler(_boundWardrobe, handler);
                        }
                        catch { }
                    }
                }

                if (_wardrobe != null)
                {
                    var wardrobeType = _wardrobe.GetType();
                    var wardrobeEvent = wardrobeType.GetEvent("OnWardrobeChanged");
                    if (wardrobeEvent != null)
                    {
                        try
                        {
                            var handler = System.Delegate.CreateDelegate(wardrobeEvent.EventHandlerType, this, nameof(HandleWardrobeChanged));
                            wardrobeEvent.AddEventHandler(_wardrobe, handler);
                        }
                        catch { }
                    }
                }

                _boundWardrobe = _wardrobe;
            }

            PlayerService.TryGetComponent(out _presetService, includeInactive: true, allowSceneLookup: true);

            foreach (var kvp in _rows)
            {
                var row = kvp.Value;
                var category = kvp.Key;

                bool hasOptions = false;
                if (_wardrobe != null)
                {
                    var method = _wardrobe.GetType().GetMethod("GetUnlockedOptions");
                    if (method != null)
                    {
                        try
                        {
                            var paramType = method.GetParameters()[0].ParameterType;
                            var enumVal = System.Enum.Parse(paramType, category.ToString());
                            var result = method.Invoke(_wardrobe, new object[] { enumVal });
                            if (result != null)
                            {
                                var count = (int)result.GetType().GetProperty("Count").GetValue(result);
                                hasOptions = count > 0;
                            }
                        }
                        catch { }
                    }
                }

                bool allowClear = _wardrobe == null ? _builder != null : hasOptions;
                SetInteractable(row, _builder != null || hasOptions, allowClear);
            }

            UpdateLabels();
            ConfigureRowNavigation();
        }

        void ConfigureRowNavigation()
        {
            for (int rowIndex = 0; rowIndex < GetOrderedRows().Count; rowIndex++)
            {
                var row = GetOrderedRows()[rowIndex];
                ConfigureButtonNavigation(row, row?.previousButton, rowIndex, 0);
                ConfigureButtonNavigation(row, row?.nextButton, rowIndex, 1);
                ConfigureButtonNavigation(row, row?.clearButton, rowIndex, 2);
            }
        }

        void ConfigureButtonNavigation(EquipmentBindings.RowBinding row, Button button, int rowIndex, int columnIndex)
        {
            if (row == null || button == null) return;

            var nav = button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = ResolveHorizontal(rowIndex, columnIndex, -1);
            nav.selectOnRight = ResolveHorizontal(rowIndex, columnIndex, +1);
            nav.selectOnUp = ResolveVertical(rowIndex, columnIndex, -1);
            nav.selectOnDown = ResolveVertical(rowIndex, columnIndex, +1);
            button.navigation = nav;
            ApplyButtonHighlight(button);
        }

        void ResetAllHighlights()
        {
            foreach (var row in _rows.Values)
            {
                ResetButtonColor(row?.previousButton);
                ResetButtonColor(row?.nextButton);
                ResetButtonColor(row?.clearButton);
            }
        }

        void ResetButtonColor(Button button)
        {
            if (button == null) return;
            var highlight = button.GetComponent<ButtonHighlight>();
            if (highlight != null)
            {
                highlight.ResetColor();
            }
        }

        void ApplyButtonHighlight(Button button)
        {
            if (button == null) return;
            var highlight = button.GetComponent<ButtonHighlight>();
            if (highlight == null)
                highlight = button.gameObject.AddComponent<ButtonHighlight>();
            highlight.Configure(_ui.rowSelectionColor);
        }

        Button ResolveHorizontal(int rowIndex, int columnIndex, int step)
        {
            var row = GetRow(rowIndex);
            if (row == null) return null;
            int idx = columnIndex;
            while (true)
            {
                idx += step;
                if (idx < 0 || idx > 2)
                    break;
                var btn = GetButtonByColumn(row, idx);
                if (IsSelectable(btn)) return btn;
            }
            var current = GetButtonByColumn(row, columnIndex);
            if (IsSelectable(current)) return current;
            return GetDefaultButton(row);
        }

        Button ResolveVertical(int rowIndex, int columnIndex, int step)
        {
            var ordered = GetOrderedRows();
            int idx = rowIndex;
            while (true)
            {
                idx += step;
                if (idx < 0 || idx >= ordered.Count)
                    break;
                var row = ordered[idx];
                if (row == null) continue;
                var btn = GetButtonByColumn(row, columnIndex);
                if (IsSelectable(btn)) return btn;
                var fallback = GetDefaultButton(row);
                if (IsSelectable(fallback)) return fallback;
            }

            var currentRow = GetRow(rowIndex);
            var current = GetButtonByColumn(currentRow, columnIndex);
            if (IsSelectable(current)) return current;
            return GetDefaultButton(currentRow);
        }

        EquipmentBindings.RowBinding GetRow(int index)
        {
            var ordered = GetOrderedRows();
            if (index < 0 || index >= ordered.Count) return null;
            return ordered[index];
        }

        static Button GetButtonByColumn(EquipmentBindings.RowBinding row, int columnIndex)
        {
            if (row == null) return null;
            return columnIndex switch
            {
                0 => row.previousButton,
                1 => row.nextButton,
                2 => row.clearButton,
                _ => null
            };
        }

        static Button GetDefaultButton(EquipmentBindings.RowBinding row)
        {
            if (row == null) return null;
            if (IsSelectable(row.previousButton)) return row.previousButton;
            if (IsSelectable(row.nextButton)) return row.nextButton;
            if (IsSelectable(row.clearButton)) return row.clearButton;
            return null;
        }

        static bool IsSelectable(Button button)
        {
            return button != null && button.IsInteractable();
        }

        void Cycle(PartCategory category, int step)
        {
            if (_builder == null) return;

            bool changed = false;

            if (_wardrobe != null)
            {
                changed = TryCycleWithWardrobe(category, step);
                if (!changed)
                    return;
            }
            else
            {
                InvokeBuilderNextPrev(category, step);
                changed = true;
            }

            Snapshot();
            UpdateLabels();
        }

        void Clear(PartCategory category)
        {
            if (_builder == null) return;
            InvokeBuilderSetByName(category, null);
            Snapshot();
            UpdateLabels();
        }

        void Snapshot()
        {
            _presetService?.SnapshotAppearanceToPreset();
            // No restaurar inventario al cambiar apariencia (solo actualizar appearance)
            _presetService?.ApplyCurrentPreset(includeInventory: false, includeAbilities: false);
        }

        void UpdateLabels()
        {
            if (_builder == null) return;
            var selection = _builder.GetType().GetMethod("GetSelection")?.Invoke(_builder, null) as Dictionary<PartCategory, string>;

            foreach (var kvp in _rows)
            {
                var row = kvp.Value;
                if (row?.label == null) continue;

                string value = "Sin asignar";
                if (selection != null && selection.TryGetValue(kvp.Key, out var part) && !string.IsNullOrEmpty(part))
                    value = ResolveDisplayName(kvp.Key, part);

                row.label.text = $"{FormatCategory(kvp.Key)}: {value}";
            }
        }

        void SetInteractable(EquipmentBindings.RowBinding row, bool canCycle, bool allowClear)
        {
            if (row == null) return;
            if (row.previousButton != null) row.previousButton.interactable = canCycle;
            if (row.nextButton != null) row.nextButton.interactable = canCycle;
            if (row.clearButton != null) row.clearButton.interactable = allowClear;
        }

        bool TryCycleWithWardrobe(PartCategory category, int step)
        {
            var method = _wardrobe?.GetType().GetMethod("GetUnlockedOptions");
            if (method == null) return false;

            try
            {
                var paramType = method.GetParameters()[0].ParameterType;
                var enumVal = System.Enum.Parse(paramType, category.ToString());
                var result = method.Invoke(_wardrobe, new object[] { enumVal });
                if (result == null) return false;

                var listType = result.GetType();
                var count = (int)listType.GetProperty("Count").GetValue(result);
                if (count == 0) return false;

                string current = GetSelectionFor(category);
                int currentIndex = -1;

                for (int i = 0; i < count; i++)
                {
                    var item = listType.GetProperty("Item").GetValue(result, new object[] { i });
                    var partName = (string)item.GetType().GetField("partName").GetValue(item);
                    if (string.Equals(partName, current, StringComparison.OrdinalIgnoreCase))
                    {
                        currentIndex = i;
                        break;
                    }
                }

                if (currentIndex < 0)
                    currentIndex = step > 0 ? 0 : count - 1;

                int nextIndex = (currentIndex + step) % count;
                if (nextIndex < 0) nextIndex += count;

                var entry = listType.GetProperty("Item").GetValue(result, new object[] { nextIndex });
                var nextPartName = (string)entry.GetType().GetField("partName").GetValue(entry);
                if (string.IsNullOrEmpty(nextPartName)) return false;

                InvokeBuilderSetByName(category, nextPartName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        void ClearSelection(PartCategory category)
        {
            if (_builder == null) return;
            InvokeBuilderSetByName(category, null);
        }

        string GetSelectionFor(PartCategory category)
        {
            if (_builder == null) return null;
            var selection = _builder.GetType().GetMethod("GetSelection")?.Invoke(_builder, null) as Dictionary<PartCategory, string>;
            if (selection != null && selection.TryGetValue(category, out var part))
                return part;
            return null;
        }

        string ResolveDisplayName(PartCategory category, string partName)
        {
            if (string.IsNullOrEmpty(partName)) return "Sin asignar";
            if (_wardrobe != null)
            {
                var method = _wardrobe.GetType().GetMethod("TryGetEntry");
                if (method != null)
                {
                    try
                    {
                        var paramType = method.GetParameters()[0].ParameterType;
                        var enumVal = System.Enum.Parse(paramType, category.ToString());
                        var parameters = new object[] { enumVal, partName, null };
                        var success = (bool)method.Invoke(_wardrobe, parameters);
                        if (success && parameters[2] != null)
                        {
                            var displayName = (string)parameters[2].GetType().GetField("displayName").GetValue(parameters[2]);
                            return string.IsNullOrEmpty(displayName) ? partName : displayName;
                        }
                    }
                    catch { }
                }
            }
            return partName;
        }

        void HandleWardrobeChanged()
        {
            UpdateLabels();
        }

        public void EnsureSelection()
        {
            var target = DefaultSelection;
            if (target == null) return;
            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(null);
                es.SetSelectedGameObject(target);
            }
            var selectable = target.GetComponent<Selectable>();
            selectable?.Select();
        }

        public bool TryHandleCancel() => false;

        public void Dispose()
        {
            if (_boundWardrobe != null)
            {
                var prevType = _boundWardrobe.GetType();
                var prevEvent = prevType.GetEvent("OnWardrobeChanged");
                if (prevEvent != null)
                {
                    try
                    {
                        var handler = System.Delegate.CreateDelegate(prevEvent.EventHandlerType, this, nameof(HandleWardrobeChanged));
                        prevEvent.RemoveEventHandler(_boundWardrobe, handler);
                    }
                    catch { }
                }
            }
        }

        string FormatCategory(PartCategory cat)
        {
            return cat switch
            {
                PartCategory.WeaponR => "Arma Mano Derecha",
                PartCategory.ShieldR => "Escudo Mano Izquierda",
                PartCategory.Bow => "Arco",
                PartCategory.Body => "Cuerpo",
                PartCategory.Cloak => "Capa",
                PartCategory.Head => "Cabeza",
                PartCategory.Hair => "Pelo",
                PartCategory.Eyes => "Ojos",
                PartCategory.Mouth => "Boca",
                PartCategory.Hat => "Casco",
                PartCategory.Eyebrow => "Ceja",
                PartCategory.Accessory => "Accesorio",
                _ => cat.ToString()
            };
        }

        // Invoca Next/Prev en el builder usando reflexiÃ³n para evitar usar PartCat directamente
        void InvokeBuilderNextPrev(PartCategory category, int step)
        {
            if (_builder == null) return;
            var type = _builder.GetType();
            var nextMethod = type.GetMethod("Next", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                          ?? type.GetMethod("Next", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var prevMethod = type.GetMethod("Prev", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                          ?? type.GetMethod("Prev", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var paramType = nextMethod?.GetParameters()[0].ParameterType ?? prevMethod?.GetParameters()[0].ParameterType;
            if (paramType == null) return;

            object enumVal;
            try { enumVal = System.Enum.Parse(paramType, category.ToString()); }
            catch { return; }

            int dir = step >= 0 ? 1 : -1;
            int times = Mathf.Max(1, Mathf.Abs(step));

            if (dir > 0)
            {
                if (nextMethod == null) return;
                var parms = nextMethod.GetParameters();
                if (parms.Length >= 2)
                {
                    nextMethod.Invoke(_builder, new object[] { enumVal, step });
                }
                else
                {
                    for (int i = 0; i < times; i++)
                        nextMethod.Invoke(_builder, new object[] { enumVal });
                }
            }
            else
            {
                if (prevMethod != null)
                {
                    for (int i = 0; i < times; i++)
                        prevMethod.Invoke(_builder, new object[] { enumVal });
                }
                else if (nextMethod != null)
                {
                    var parms = nextMethod.GetParameters();
                    if (parms.Length >= 2)
                        nextMethod.Invoke(_builder, new object[] { enumVal, step });
                    else
                    {
                        for (int i = 0; i < times; i++)
                            nextMethod.Invoke(_builder, new object[] { enumVal });
                    }
                }
            }
        }

        void InvokeBuilderSetByName(PartCategory category, string nameOrNull)
        {
            if (_builder == null) return;
            var type = _builder.GetType();
            var method = type.GetMethod("SetByName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                      ?? type.GetMethod("SetByName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null) return;
            var paramType = method.GetParameters()[0].ParameterType;
            try
            {
                var enumVal = System.Enum.Parse(paramType, category.ToString());
                method.Invoke(_builder, new object[] { enumVal, nameOrNull });
            }
            catch (System.Exception) { }
        }

        List<EquipmentBindings.RowBinding> GetOrderedRows()
        {
            if (!_rowOrderDirty)
                return _orderedRows;

            _orderedRows.Clear();
            if (_ui.rows != null)
            {
                foreach (var row in _ui.rows)
                {
                    // Filtrar solo las filas habilitadas
                    if (row != null && row.enabled)
                        _orderedRows.Add(row);
                }
                _orderedRows.Sort((a, b) => GetRowSortValue(a).CompareTo(GetRowSortValue(b)));
            }

            _rowOrderDirty = false;
            return _orderedRows;
        }

        float GetRowSortValue(EquipmentBindings.RowBinding row)
        {
            Transform t = null;
            if (row.label != null) t = row.label.transform;
            else if (row.previousButton != null) t = row.previousButton.transform;
            else if (row.nextButton != null) t = row.nextButton.transform;
            else if (row.clearButton != null) t = row.clearButton.transform;
            return t != null ? -t.position.y : float.MinValue;
        }

        class ButtonHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
        {
            public Graphic target;
            public Color highlightColor = Color.white;
            Color _baseColor;

            void Awake()
            {
                if (target == null)
                    target = GetComponent<Graphic>();
                if (target == null)
                    target = GetComponentInChildren<Graphic>();
                if (target != null)
                    _baseColor = target.color;
            }

            public void Configure(Color color)
            {
                highlightColor = color;
                if (target != null)
                    _baseColor = target.color;
            }

            public void OnSelect(BaseEventData eventData) => Set(true);
            public void OnDeselect(BaseEventData eventData) => Set(false);

            void Set(bool selected)
            {
                if (target != null)
                    target.color = selected ? highlightColor : _baseColor;
            }

            public void ResetColor()
            {
                if (target != null)
                    target.color = _baseColor;
            }
        }
    }
}
