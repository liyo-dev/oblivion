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
using EasyTransition;

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
    [Tooltip("Nombre de la escena donde se permite abrir el menú de equipo.")]
    [SerializeField] private string allowedSceneName = "MainWorld";

    [Header("Transición al Main Menu")]
    [Tooltip("TransitionManager para transiciones suaves (opcional, se busca automáticamente si es null)")]
    [SerializeField] private TransitionManager transitionManager;
    [Tooltip("Settings de transición al salir al Main Menu")]
    [SerializeField] private TransitionSettings mainMenuTransitionSettings;
    [Tooltip("Delay antes de iniciar la transición al Main Menu")]
    [SerializeField] private float mainMenuTransitionDelay = 0.1f;

    [Header("Contenedores UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Objeto raíz del contenido del menú (se activa/desactiva al abrir/cerrar).")]
    [SerializeField] private GameObject windowRoot;

    [Header("Feedback")]
    [SerializeField, Tooltip("Tiempo que se mantiene visible el mensaje de feedback tras usar un objeto.")]
    private float feedbackDuration = 1.5f;

    [Header("Pestañas")]
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button spellsTabButton;
    [SerializeField] private Button equipmentTabButton;
    [SerializeField] private Color tabActiveColor   = Color.white;
    [SerializeField] private Color tabInactiveColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("Panel de jugador")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI mpText;
    [Tooltip("Image con Type=Filled (Horizontal). Opcional: si esta vacio no se hace nada.")]
    [SerializeField] private Image hpBarFill;
    [SerializeField] private Image mpBarFill;

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

    [Header("Efecto sueño")]
    [Tooltip("Blobs nebulosa que aparecen al abrir el inventario (opcional).")]
    [SerializeField] private DreamBackgroundController dreamBackground;
    [Tooltip("Chispas flotantes al abrir el inventario (opcional).")]
    [SerializeField] private DreamSparkleOverlay dreamSparkles;

    [Header("Selección inicial")]
    [SerializeField] private GameObject initialSelectionOverride;

    [Header("Inventario")]
    [SerializeField] private InventoryBindings inventoryUI = new();

        [Header("Hechizos")]
        [SerializeField] private SpellBindings spellUI = new();

    [Header("Equipamiento")]
    [SerializeField] private EquipmentBindings equipmentUI = new();

    static PlayerEquipmentMenuController _instance;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
    }
    #endif

    readonly List<Button> _tabButtons = new();
    readonly Dictionary<Button, int> _tabButtonIndices = new();
    readonly Dictionary<Button, ColorBlock> _tabOriginalColors = new();

    InventoryView _inventoryView;
    SpellView _spellView;
    EquipmentView _equipmentView;
    [Header("Cámara de equipamiento - órbita de Will")]
    [SerializeField] private float previewOrbitSpeed = 120f;
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de permitir el cierre (para evitar rebotes de input).")]
    private float closeInputGracePeriod = 0.3f;

    [Header("Cámara de equipamiento - desplazamiento de la cámara principal")]
    [SerializeField, Tooltip("Cámara principal en tercera persona (Invector). Se busca automáticamente vía ServiceLocator si es null.")]
    private vThirdPersonCamera mainThirdPersonCamera;
    [SerializeField, Range(0.5f, 1f), Tooltip("Fracción horizontal de pantalla donde debe quedar centrado Will (0.5 = centro, 0.75 = centro de la mitad derecha).")]
    private float equipmentMenuTargetScreenX = 0.75f;
    [SerializeField, Tooltip("Altura aproximada (en metros, desde los pies) del punto al que mira la cámara nivelada. Debe rondar la altura del pecho/cara de Will para que no se vea desde arriba ni desde abajo.")]
    private float equipmentMenuCameraLookHeight = 1.6f;
    [SerializeField, Min(0f), Tooltip("Duración de la transición de la cámara principal al abrir/cerrar el menú.")]
    private float equipmentMenuCameraTransitionDuration = 0.4f;

    // Cámara principal desplazada temporalmente mientras el menú está abierto
    Camera _mainCamera;
    Vector3 _mainCameraOriginalPosition;
    Quaternion _mainCameraOriginalRotation;
    bool _mainCameraOffsetActive;
    Tween _mainCameraTween;

    bool _equipmentCameraActive;
    Transform _playerPreviewTarget;
    Quaternion _storedPlayerRotation;
    float _previewBaseYaw; // Yaw hacia el que Will mira por defecto (mirando a la cámara desplazada)
    float _previewPlayerYaw;
    bool _wasInOrbitMode; // Rastrear si estuvimos en modo orbit en el frame anterior
    PlayerActionManager _actionManager;
    bool _actionModeActive;
    bool _toggleRequested;
    bool _cancelRequested;
    float _openedAt = -999f;
    float _toggleCooldownUntil;
    InputActionMapScope _inputScope;
    
    // Para mantener animaciones del player en el menú
    Animator _playerAnimator;
    AnimatorUpdateMode _storedAnimatorUpdateMode;

    // Hashes cacheados para parámetros del Animator (evitar búsquedas por string)
    static readonly int AnimHash_InputMagnitude = Animator.StringToHash("InputMagnitude");
    static readonly int AnimHash_Speed = Animator.StringToHash("Speed");
    static readonly int AnimHash_VerticalVelocity = Animator.StringToHash("VerticalVelocity");

    bool _isOpen;
    int _activeTab;
    float _savedTimeScale = 1f;
    
    // Flag para controlar animaciones de uso de items
    bool _isUsingItem;

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
        // Debug.Log("[PlayerEquipmentMenuController] Bootstrap: Buscando instancia existente...");
        
        // Intentar obtener desde ServiceLocator primero
        if (ServiceLocator.TryGet<PlayerEquipmentMenuController>(out var existing) && existing != null)
        {
            // Debug.Log("[PlayerEquipmentMenuController] Bootstrap: Encontrada instancia existente en ServiceLocator");
            _instance = existing;
            return;
        }
    
        
        // Si no hay instancia, no hacer nada - el menú debe estar configurado manualmente en la escena
        // Debug.Log("[PlayerEquipmentMenuController] Bootstrap: No se encontró instancia. El menú debe estar configurado manualmente en la escena.");
    }

    void Awake()
    {
        // Debug.Log($"[PlayerEquipmentMenuController] Awake en GameObject '{gameObject.name}'");
        
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
            // Debug.Log($"[PlayerEquipmentMenuController] DontDestroyOnLoad aplicado a '{gameObject.name}'");
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
            Debug.Log($"[PlayerEquipmentMenuController] CanvasGroup encontrado: {(canvasGroup != null ? "Sí" : "No")}");
        }
        
        if (windowRoot == null && canvas != null)
        {
            windowRoot = canvas.gameObject;
            Debug.Log($"[PlayerEquipmentMenuController] WindowRoot asignado automáticamente a Canvas: '{windowRoot.name}'");
        }
        
        // Verificar si tenemos lo mínimo necesario
        if (canvas == null)
        {
            Debug.LogError($"[PlayerEquipmentMenuController] âš ï¸ No se encontró Canvas en '{gameObject.name}'");
            Debug.LogError("   El menú de equipamiento NO funcionará correctamente.");
            Debug.LogError("   Asegúrate de que el PlayerEquipmentMenuController esté en un GameObject con Canvas configurado.");
            // No desactivar el componente para que se pueda configurar después
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
            Debug.LogError("[PlayerEquipmentMenuController] âš ï¸ No se pudo inicializar ninguna vista del menú");
            Debug.LogError("   El menú no podrá abrirse hasta que se configuren las vistas en el Inspector.");
            // No desactivar el componente para que se pueda configurar después
        }
        
        SetEquipmentCameraActive(false);
        // Unregister from MenuManager
        MenuManager.Close(MenuKind.Equipment);
        
        // Debug.Log($"[PlayerEquipmentMenuController] Awake completado. Vistas configuradas: {(_inventoryView != null || _spellView != null || _equipmentView != null)}");
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(PlayerEquipmentMenuController));
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        if (_isOpen)
            CloseMenu();
        else
            ExitUiInputScope();
    }

    private void HandleProfileReady()
    {
        // El menú de equipamiento accede al Profile para obtener el preset activo
        // No necesita inicialización especial, solo necesita que el Profile esté disponible
        // cuando accede a él en RefreshInventoryTab() y otros métodos
    }

    void OnDestroy()
    {
        // IMPORTANTE: limpiar todo el estado de bloqueo de input antes de destruir el objeto.
        // Si el objeto se destruye con el menú abierto (p.ej. cambio de escena), el PopMode
        // y MenuManager.Close nunca se llamarían desde CloseMenu(), dejando el stack de modos
        // y el MenuManager en estado corrupto para siempre.
        if (_isOpen)
        {
            // Limpiar ActionMode sin depender de _actionManager (puede ya estar destruido)
            if (_actionModeActive && _actionManager != null)
            {
                _actionManager.PopMode(ActionMode.Inventory);
                _actionModeActive = false;
            }
            // Restaurar GameState
            if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
            if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
            // Limpiar registro de MenuManager
            MenuManager.Close(MenuKind.Equipment);
            // Restaurar timeScale
            Time.timeScale = _savedTimeScale;
            _isOpen = false;
        }
        ExitUiInputScope();
        // Si la cámara principal quedó desplazada (p.ej. el objeto se destruye con el menú
        // abierto por un cambio de escena), restaurarla de forma inmediata para no dejar al
        // jugador con la cámara desplazada y el vThirdPersonCamera deshabilitado para siempre.
        if (_mainCameraOffsetActive)
        {
            _mainCameraTween?.Kill();
            if (_mainCamera != null)
            {
                _mainCamera.transform.position = _mainCameraOriginalPosition;
                _mainCamera.transform.rotation = _mainCameraOriginalRotation;
            }
            if (mainThirdPersonCamera != null)
                mainThirdPersonCamera.enabled = true;
            _mainCameraOffsetActive = false;
        }
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

        // Durante el minijuego el Start se usa para abortar — no abrir el menú
        if (TagMinigameController.IsAnyMinigameActive) return;

        // Detectar botón Start para abrir/cerrar el menú usando GamepadInputReader
        if (GamepadInputReader.StartPressed)
        {
            _toggleRequested = true;
        }

        // Si el menú ya está abierto, evita leer el input de apertura para que el D-Pad
        // no interfiera con la navegación UI (el toggle se maneja al cerrarse).
        if (!_isOpen)
        {
            HandleToggleInput();
        }
        else
        {
            // Detectar botones del gamepad usando GamepadInputReader
            
            // Botón B (Cancel) o Start para cerrar el menú
            if (GamepadInputReader.CancelPressed || GamepadInputReader.StartPressed)
            {
                _cancelRequested = true;
            }
            
            // Botón Y para volver al MainMenu
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsYButtonPressed())
            {
                GamepadInputReader.PlayUISound("UI_Cancel");
                var popup = ConfirmationPopupUI.Instance;
                if (popup != null)
                {
                    string msg = LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.Get("CONFIRM_MAINMENU_QUIT", "¿Salir al menú principal?")
                        : "¿Salir al menú principal?";
                    popup.Show(msg, onConfirm: OnQuitToMainMenu);
                }
                else
                {
                    OnQuitToMainMenu();
                }
            }
            
            // LB (Left Bumper) para pestaña anterior
            // Leer directamente del gamepad porque GamepadInputReader suprime estos botones en UI
            if (IsLeftShoulderPressed())
            {
                GamepadInputReader.PlayUISound("UI_Navigate");
                ChangeTab(-1);
            }
            
            // RB (Right Bumper) para pestaña siguiente
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
            
            // Manejar inputs específicos de cada tab
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
                        _cancelRequested = false; // Evitar que cierre el menú
                }
            }
            else if (_activeTab == 1) // Hechizos
            {
                _spellView?.HandleInput();
            }
        }
    }

    // Métodos auxiliares simplificados - usan GamepadInputReader centralizado
    // Estos leen del Action Map UI para navegación de menús
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
            _tabButtonIndices[inventoryTabButton] = 0;

            if (inventoryTabButton.GetComponent<UIButtonAudio>() == null)
                inventoryTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
        if (spellsTabButton != null)
        {
            spellsTabButton.onClick.AddListener(() => ShowTab(1));
            _tabButtons.Add(spellsTabButton);
            _tabButtonIndices[spellsTabButton] = 1;

            if (spellsTabButton.GetComponent<UIButtonAudio>() == null)
                spellsTabButton.gameObject.AddComponent<UIButtonAudio>();
        }
        if (equipmentTabButton != null)
        {
            equipmentTabButton.onClick.AddListener(() => ShowTab(2));
            _tabButtons.Add(equipmentTabButton);
            _tabButtonIndices[equipmentTabButton] = 2;

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
        if (TagMinigameController.IsAnyMinigameActive) return;

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
        // Evitar cerrar inmediatamente si todavía estamos procesando el input que abrió el menú.
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
        if (TagMinigameController.IsAnyMinigameActive)
        {
            Debug.LogWarning("[PlayerEquipmentMenu] OpenMenu() bloqueado — minijuego activo");
            return;
        }

        // Reproducir sonido de apertura de menú
        GamepadInputReader.PlayUISound("UI_Submit");

        Debug.Log("[PlayerEquipmentMenu] OpenMenu() llamado");
        
        // Verificación temprana: Â¿tenemos Canvas?
        if (canvas == null)
        {
            Debug.LogError("[PlayerEquipmentMenu] âŒ No se puede abrir - Canvas es NULL");
            Debug.LogError("   El PlayerEquipmentMenuController no está correctamente configurado.");
            Debug.LogError("   Debe estar en un GameObject con un Canvas configurado.");
            return;
        }
        
        // Verificación temprana: Â¿hay al menos una vista configurada?
        if (_inventoryView == null && _spellView == null && _equipmentView == null)
        {
            Debug.LogError("[PlayerEquipmentMenu] âŒ No se puede abrir - NINGUNA VISTA CONFIGURADA");
            Debug.LogError("   Configura al menos una vista (Inventory, Spell o Equipment) en el Inspector.");
            Debug.LogError("   Revisa los logs anteriores de EnsureViews() para más detalles.");
            return;
        }
        
        if (!GameState.CanOpenInventory)
        {
            Debug.Log("[PlayerEquipmentMenu] No se puede abrir - GameState.CanOpenInventory = false");
            return;
        }
        
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            Debug.Log("[PlayerEquipmentMenu] No se puede abrir - Diálogo activo");
            return;
        }

        // Ask central manager for permission to open
        if (!MenuManager.TryOpen(MenuKind.Equipment))
        {
            Debug.Log("[PlayerEquipmentMenuController] Apertura denegada por MenuManager");
            return;
        }

        Debug.Log("[PlayerEquipmentMenu] MenuManager permitió la apertura, verificando vistas...");
        
        if (!EnsureViews())
        {
            Debug.LogError("[PlayerEquipmentMenu] EnsureViews() retornó false - cerrando menú");
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
            Debug.Log("[PlayerEquipmentMenu] Animator cambiado a UnscaledTime para mantener animaciones en el menú");
        }

        Debug.Log("[PlayerEquipmentMenu] Configurando canvas y pestañas...");
        SetCanvasState(true);
        dreamBackground?.StartDream();
        dreamSparkles?.StartSparkles();

        // Ocultar el HUD y el icono de estado del tiempo mientras el menú está abierto
        // (ahora se ve el mundo real detrás de Will, así que estorbarían en pantalla).
        Sendero.UI.PlayerHUDV2.Instance?.HideHUD();
        Sendero.UI.TimeOfDayIndicator.Instance?.Hide();

        // Cachear colores originales de HP/MP si no se han cacheado aún
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
        
        Debug.Log("[PlayerEquipmentMenu] Activando cámara de equipamiento...");
        // Activar la cámara de equipamiento siempre que el menú esté abierto
        SetEquipmentCameraActive(true);

        // Marcar el instante de apertura para filtrar cierres accidentales en el mismo frame.
        _openedAt = Time.unscaledTime;
        _cancelRequested = false; // Limpiar cualquier cancel previo para evitar cierres inmediatos.
        
        Debug.Log("[PlayerEquipmentMenu] Menú abierto completamente");
    }

    void CloseMenu(bool playSound = true)
    {
        // Solo reproducir sonido si el menú realmente estaba abierto
        if (playSound && _isOpen)
        {
            GamepadInputReader.PlayUISound("UI_Cancel");
        }
        
        // Limpiar animaciones de HP/MP
        _hpTextTween?.Kill();
        _hpTextTween = null;
        _mpTextTween?.Kill();
        _mpTextTween = null;
        
        // Restaurar colores originales si están cacheados
        if (_hpColorCached && hpText != null)
            hpText.color = _hpOriginalColor;
        if (_mpColorCached && mpText != null)
            mpText.color = _mpOriginalColor;
        
        dreamBackground?.StopDream();
        dreamSparkles?.StopSparkles();
        SetCanvasState(false);

        // Restaurar el HUD y el icono de estado del tiempo al cerrar el menú
        Sendero.UI.PlayerHUDV2.Instance?.ShowHUD();
        Sendero.UI.TimeOfDayIndicator.Instance?.Show();
        _spellView?.CancelSlotSelection(true);
        Time.timeScale = _savedTimeScale;
        
        // Restaurar el AnimatorUpdateMode original
        if (_playerAnimator != null)
        {
            _playerAnimator.updateMode = _storedAnimatorUpdateMode;
            Debug.Log("[PlayerEquipmentMenu] Animator restaurado a su UpdateMode original");
        }
        
        // Resetear estado de órbita para que se recalcule la próxima vez
        _wasInOrbitMode = false;
        
        _isOpen = false;
        ExitUiInputScope();
        // Evitar que el botón B que cerró el menú dispare acciones de gameplay en el mismo frame.
        GamepadInputReader.IgnoreCancelButton(0.2f);
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
        Debug.Log("[PlayerEquipmentMenuController] Iniciando transición al Main Menu");

        // Cerrar el menú SIN reproducir sonido (ya sonó UI_Cancel arriba)
        if (_isOpen)
        {
            CloseMenu(playSound: false);
        }
        
        // Asegurar que el tiempo está a escala normal
        Time.timeScale = 1f;
        
        // Debounce de input para MainMenu (similar a GameOverManager)
        MainMenuController.RequestInputDebounce();
        
        // Usar transición si está disponible, sino carga directa
        var tm = ResolveTransitionManager();
        if (tm != null && mainMenuTransitionSettings != null)
        {
            Debug.Log("[PlayerEquipmentMenuController] Usando transición con settings configurados");
            tm.Transition("MainMenu", mainMenuTransitionSettings, mainMenuTransitionDelay);
        }
        else
        {
            if (tm == null)
                Debug.LogWarning("[PlayerEquipmentMenuController] TransitionManager no disponible, cargando escena directamente");
            else
                Debug.LogWarning("[PlayerEquipmentMenuController] MainMenuTransitionSettings no configurado, cargando escena directamente");
            
            SceneManager.LoadScene("MainMenu");
        }
    }

    TransitionManager ResolveTransitionManager()
    {
        // Si ya tenemos referencia serializada, usarla
        if (transitionManager != null) return transitionManager;

        // Intentar obtener del ServiceLocator
        if (ServiceLocator.TryGet(out TransitionManager cached) && cached != null)
        {
            transitionManager = cached;
            return transitionManager;
        }

        // Intentar obtener la instancia singleton
        try
        {
            transitionManager = TransitionManager.Instance();
            if (transitionManager != null)
            {
                ServiceLocator.Register(transitionManager);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerEquipmentMenuController] TransitionManager.Instance() falló: {ex.Message}");
        }

        return transitionManager;
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
        
        // Mantener la cámara activa en todas las pestañas mientras el menú esté abierto
        SetEquipmentCameraActive(_isOpen);
    }

    void EnterUiInputScope()
    {
        Debug.Log("[PlayerEquipmentMenu] EnterUiInputScope() - Cambiando a modo UI");
        _inputScope?.Dispose();
        _inputScope = InputActionMapScope.EnterUiScope();
        
        // Asegurar que los eventos de input están suscritos (para sonidos automáticos de LB/RB)
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
        if (!_equipmentCameraActive || _playerPreviewTarget == null) return;

        // Solo permitir órbita en la pestaña de Equipamiento (index 2)
        bool allowOrbit = _activeTab == 2;

        if (_wasInOrbitMode && !allowOrbit)
        {
            // Salir de órbita: resetear yaw y volver a mirar hacia la cámara
            _previewPlayerYaw = 0f;
            ApplyPreviewFacingRotation();
        }
        _wasInOrbitMode = allowOrbit;

        if (allowOrbit)
        {
            // Leer directamente del hardware para evitar restricciones de supresión
            float rotateInput = GamepadInputReader.CameraLookRaw.x;

            if (Mathf.Abs(rotateInput) > 0.01f)
            {
                _previewPlayerYaw += rotateInput * previewOrbitSpeed * Time.unscaledDeltaTime;
            }

            // Rotar a Will sobre sí mismo: parte mirando hacia la cámara desplazada
            // y gira según el input del joystick para inspeccionar el equipo puesto.
            _playerPreviewTarget.rotation = Quaternion.Euler(0f, _previewBaseYaw - _previewPlayerYaw, 0f);
        }
        else if (!_isUsingItem)
        {
            // En el resto de pestañas, resetear el yaw y mantener a Will mirando a la cámara
            _previewPlayerYaw = 0f;
            ApplyPreviewFacingRotation();
        }
    }

    /// <summary>
    /// Orienta a Will hacia la posición actual de la cámara principal (desplazada para el menú),
    /// usando el yaw base calculado al abrir el menú.
    /// </summary>
    void ApplyPreviewFacingRotation()
    {
        if (_playerPreviewTarget == null) return;
        _playerPreviewTarget.rotation = Quaternion.Euler(0f, _previewBaseYaw, 0f);
    }

    bool TrySetupPreviewTarget()
    {
        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
        {
            return false;
        }

        _playerPreviewTarget = player.transform;
        _storedPlayerRotation = _playerPreviewTarget.rotation;

        // Buscar el Animator del player para poder mantener sus animaciones activas en el menú
        if (_playerAnimator == null)
        {
            _playerAnimator = _playerPreviewTarget.GetComponentInChildren<Animator>();
            if (_playerAnimator != null)
            {
                Debug.Log($"[PlayerEquipmentMenuController] Animator del player encontrado: {_playerAnimator.name}");
            }
            else
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] No se encontró Animator en el player. Las animaciones no funcionarán en el menú.");
            }
        }

        // Forzar al Animator a ir a idle (detener animaciones de movimiento)
        if (_playerAnimator != null)
        {
            // Resetear parámetros comunes de movimiento a 0 para forzar idle (solo si existen)
            TrySetAnimatorFloat(AnimHash_InputMagnitude, 0f);
            TrySetAnimatorFloat(AnimHash_Speed, 0f);
            TrySetAnimatorFloat(AnimHash_VerticalVelocity, 0f);

            Debug.Log("[PlayerEquipmentMenuController] Animator forzado a idle");
        }

        _previewPlayerYaw = 0f;
        // La rotación final hacia la cámara se aplica en ApplyEquipmentMenuCameraOffset(),
        // una vez calculada la posición desplazada de la cámara principal.

        return true;
    }

    void SetEquipmentCameraActive(bool value)
    {
        if (_equipmentCameraActive == value) return;

        _equipmentCameraActive = value;

        if (_equipmentCameraActive)
        {
            // IMPORTANTE: Forzar reset del preview target para garantizar posicionamiento consistente
            // Esto asegura que _previewPlayerYaw se recalcule desde cero
            _playerPreviewTarget = null;
            if (TrySetupPreviewTarget())
                ApplyEquipmentMenuCameraOffset();
        }
        else
        {
            if (_playerPreviewTarget != null)
                _playerPreviewTarget.rotation = _storedPlayerRotation;

            _playerPreviewTarget = null;
            RestoreEquipmentMenuCamera();
        }
    }

    void EnsureMainCameraRefs()
    {
        if (mainThirdPersonCamera == null)
            mainThirdPersonCamera = ServiceLocator.Get<vThirdPersonCamera>(false);
        if (mainThirdPersonCamera != null && _mainCamera == null)
            _mainCamera = mainThirdPersonCamera.GetComponent<Camera>();
    }

    /// <summary>
    /// Desplaza lateralmente la cámara principal (Invector) para dejar a Will centrado en la mitad
    /// derecha de la pantalla mientras el menú de equipamiento está abierto. Desactiva el seguimiento
    /// normal de la cámara mientras dure el desplazamiento y restaura su posición exacta al cerrar.
    /// </summary>
    void ApplyEquipmentMenuCameraOffset()
    {
        EnsureMainCameraRefs();
        if (mainThirdPersonCamera == null || _mainCamera == null)
        {
            Debug.LogWarning("[PlayerEquipmentMenuController] No se encontró la cámara principal (vThirdPersonCamera). No se puede desplazar para el menú de equipamiento.");
            return;
        }
        if (_mainCameraOffsetActive) return;

        Transform camT = _mainCamera.transform;
        _mainCameraOriginalPosition = camT.position;
        _mainCameraOriginalRotation = camT.rotation;

        Vector3 targetWorldPos = _playerPreviewTarget != null ? _playerPreviewTarget.position : camT.position + camT.forward * 3f;
        Vector3 lookPoint = targetWorldPos + Vector3.up * equipmentMenuCameraLookHeight;

        // Nivelar la cámara: conservar el yaw horizontal original pero eliminar cualquier
        // inclinación (pitch/roll) para que Will se vea recto en vez de "desde arriba".
        //
        // FIX INC-065: en una AmbientZone con ZoneCameraMode.TopDown la cámara mira casi en
        // vertical (camT.forward casi paralelo a Vector3.up), así que su componente horizontal
        // es minúscula y está dominada por ruido numérico. Al normalizarla, "nivelar" a partir de
        // ese vector casi degenerado producía un giro horizontal prácticamente aleatorio cada vez
        // que se abría/cerraba el menú de equipamiento en esas zonas ("la cámara se vuelve loca").
        // Si la cámara está casi vertical, usamos la orientación del propio personaje (siempre
        // bien definida, no depende del ángulo de la cámara) como base en su lugar.
        Vector3 flatForward = Vector3.zero;
        bool cameraNearVertical = Mathf.Abs(camT.forward.y) > 0.85f;
        if (!cameraNearVertical)
            flatForward = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f && _playerPreviewTarget != null)
            flatForward = Vector3.ProjectOnPlane(_playerPreviewTarget.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.ProjectOnPlane(targetWorldPos - _mainCameraOriginalPosition, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        Quaternion levelRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        Vector3 rightDir = levelRotation * Vector3.right;

        // Posición nivelada: mismo desplazamiento horizontal (X/Z) que la cámara original respecto
        // a Will, pero a la altura del punto de mira, ya sin inclinación.
        Vector3 levelPosition = new Vector3(_mainCameraOriginalPosition.x, lookPoint.y, _mainCameraOriginalPosition.z);

        Vector3 toTarget = lookPoint - levelPosition;
        float depth = Vector3.Dot(toTarget, flatForward);
        if (depth < 0.1f) depth = 3f; // Fallback de seguridad si el cálculo da un valor degenerado

        float halfWidthAtDepth = Mathf.Tan(_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth * _mainCamera.aspect;
        float xOld = Vector3.Dot(toTarget, rightDir);
        float xDesired = (equipmentMenuTargetScreenX - 0.5f) * 2f * halfWidthAtDepth;
        float lateralShift = xDesired - xOld;

        Vector3 targetPos = levelPosition - rightDir * lateralShift;

        mainThirdPersonCamera.enabled = false;
        _mainCameraOffsetActive = true;

        _mainCameraTween?.Kill();
        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(camT.DOMove(targetPos, equipmentMenuCameraTransitionDuration).SetEase(Ease.OutCubic));
        seq.Join(camT.DORotateQuaternion(levelRotation, equipmentMenuCameraTransitionDuration).SetEase(Ease.OutCubic));
        _mainCameraTween = seq;

        // Orientar a Will hacia la nueva posición de la cámara para que quede mirando de frente
        if (_playerPreviewTarget != null)
        {
            Vector3 dirToCamera = targetPos - _playerPreviewTarget.position;
            dirToCamera.y = 0f;
            if (dirToCamera.sqrMagnitude > 0.001f)
            {
                Quaternion faceRot = Quaternion.LookRotation(dirToCamera.normalized);
                _previewBaseYaw = faceRot.eulerAngles.y;
                _playerPreviewTarget.rotation = faceRot;
            }
        }
    }

    /// <summary>
    /// Devuelve la cámara principal a su posición/rotación previas al abrir el menú de equipamiento
    /// y reactiva su seguimiento normal al terminar la transición.
    /// </summary>
    void RestoreEquipmentMenuCamera()
    {
        if (!_mainCameraOffsetActive) return;

        _mainCameraTween?.Kill();

        if (_mainCamera == null)
        {
            _mainCameraOffsetActive = false;
            if (mainThirdPersonCamera != null)
                mainThirdPersonCamera.enabled = true;
            return;
        }

        Transform camT = _mainCamera.transform;
        Vector3 savedPos = _mainCameraOriginalPosition;
        Quaternion savedRot = _mainCameraOriginalRotation;

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(camT.DOMove(savedPos, equipmentMenuCameraTransitionDuration).SetEase(Ease.OutCubic));
        seq.Join(camT.DORotateQuaternion(savedRot, equipmentMenuCameraTransitionDuration).SetEase(Ease.OutCubic));
        seq.OnComplete(() =>
        {
            camT.position = savedPos;
            camT.rotation = savedRot;
            if (mainThirdPersonCamera != null)
                mainThirdPersonCamera.enabled = true;
            _mainCameraOffsetActive = false;
        });
        _mainCameraTween = seq;
    }

    void MaintainAnimatorIdle()
    {
        if (!_isOpen || _playerAnimator == null)
            return;
            
        // NUEVO: Si se está usando un item, no forzar idle
        if (_isUsingItem) return;

        // Verificar si se está reproduciendo una animación específica (como beber poción)
        var currentClipInfo = _playerAnimator.GetCurrentAnimatorClipInfo(0);
        if (currentClipInfo.Length > 0)
        {
            var clipName = currentClipInfo[0].clip.name;
            // NO forzar idle si se está reproduciendo una animación de uso de item
            if (clipName.Contains("DrinkPotion") || clipName.Contains("UseItem") || clipName.Contains("Consume"))
            {
                return;
            }
        }

        // Forzar parámetros a 0 para mantener idle (solo si existen en el Animator)
        TrySetAnimatorFloat(AnimHash_InputMagnitude, 0f);
        TrySetAnimatorFloat(AnimHash_Speed, 0f);
        TrySetAnimatorFloat(AnimHash_VerticalVelocity, 0f);

        // Asegurar que el AnimatorUpdateMode esté en UnscaledTime
        if (_playerAnimator.updateMode != AnimatorUpdateMode.UnscaledTime)
        {
            _playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }
    
    // NUEVO: Método público para establecer el flag
    public void SetUsingItem(bool value, float duration = 0f)
    {
        _isUsingItem = value;
        if (value && duration > 0f)
        {
            // Usar string para StopCoroutine por seguridad si la corrutina no estaba corriendo
            StopCoroutine("ResetUsingItemFlag"); 
            StartCoroutine(ResetUsingItemFlag(duration));
        }
    }

    System.Collections.IEnumerator ResetUsingItemFlag(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        _isUsingItem = false;
    }

    /// <summary>
    /// Intenta establecer un parámetro float del Animator solo si existe.
    /// </summary>
    void TrySetAnimatorFloat(int paramHash, float value)
    {
        if (_playerAnimator == null) return;
        
        // Verificar si el parámetro existe en el Animator
        foreach (var param in _playerAnimator.parameters)
        {
            if (param.nameHash == paramHash && param.type == AnimatorControllerParameterType.Float)
            {
                _playerAnimator.SetFloat(paramHash, value);
                return;
            }
        }
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
        foreach (var button in _tabButtons)
        {
            if (button == null) continue;

            // Usar el índice real del tab registrado para este botón (no el índice en la lista)
            int tabIndex = _tabButtonIndices.TryGetValue(button, out var idx) ? idx : -1;
            bool isActive = tabIndex == _activeTab;
            button.interactable = !isActive;

            var colors = button.colors;
            if (isActive)
            {
                // Activo (interactable=false): disabledColor blanco para que se vea brillante
                colors.disabledColor  = Color.white;
                colors.colorMultiplier = 1f;
            }
            else
            {
                // Inactivos: tenues pero visibles
                colors.normalColor    = Color.white;
                colors.colorMultiplier = 0.45f;
            }
            button.colors = colors;
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
        bool hasStatsText = levelText != null || hpText != null || mpText != null
                            || hpBarFill != null || mpBarFill != null;
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

            // Vida: se resuelve una sola vez y alimenta tanto el texto como la barra.
            {
                float cur = -1f, max = -1f;
                if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var health, includeInactive: true, allowSceneLookup: true))
                {
                    cur = health.CurrentHealth; max = health.MaxHealth;
                }
                else if (preset != null)
                {
                    cur = preset.currentHP; max = preset.maxHP;
                }

                if (hpText != null)
                {
                    string hpValue = max > 0f ? $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}" : "?";
                    hpText.text = string.IsNullOrEmpty(_hpLabel) ? hpValue : $"{_hpLabel} {hpValue}";
                }
                if (hpBarFill != null)
                    hpBarFill.fillAmount = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
            }

            // Mana: mismo patron.
            {
                float cur = -1f, max = -1f;
                if (PlayerService.TryGetComponent<ManaPool>(out var mana, includeInactive: true, allowSceneLookup: true))
                {
                    cur = mana.Current; max = mana.Max;
                }
                else if (preset != null)
                {
                    cur = preset.currentMP; max = preset.maxMP;
                }

                if (mpText != null)
                {
                    string mpValue = max > 0f ? $"{Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}" : "?";
                    mpText.text = string.IsNullOrEmpty(_mpLabel) ? mpValue : $"{_mpLabel} {mpValue}";
                }
                if (mpBarFill != null)
                    mpBarFill.fillAmount = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
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

        // Matar animación previa si existe
        _hpTextTween?.Kill();

        // Color verde para indicar curación
        var healColor = new Color(0.2f, 1f, 0.3f, 1f);

        // Secuencia de animación: escala + color + regreso
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

        // Matar animación previa si existe
        _mpTextTween?.Kill();

        // Color azul/cyan para indicar restauración de maná
        var manaColor = new Color(0.3f, 0.7f, 1f, 1f);

        // Secuencia de animación: escala + color + regreso
        var sequence = DOTween.Sequence();
        sequence.Append(mpText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, vibrato: 8, elasticity: 0.6f).SetUpdate(true));
        sequence.Join(mpText.DOColor(manaColor, 0.15f).SetUpdate(true));
        sequence.Append(mpText.DOColor(_mpOriginalColor, 0.25f).SetUpdate(true));
        
        _mpTextTween = sequence;
    }

    bool EnsureViews()
    {
        bool anyViewConfigured = false;
        
        // Debug.Log($"[PlayerEquipmentMenuController] EnsureViews() - Verificando vistas...");
        // Debug.Log($"  - _inventoryView: {(_inventoryView != null ? "EXISTS" : "NULL")}");
        // Debug.Log($"  - _spellView: {(_spellView != null ? "EXISTS" : "NULL")}");
        // Debug.Log($"  - _equipmentView: {(_equipmentView != null ? "EXISTS" : "NULL")}");

        if (_inventoryView == null)
        {
            // Debug.Log($"[PlayerEquipmentMenuController] Verificando inventoryUI.IsConfigured...");
            if (inventoryUI.IsConfigured)
            {
                _inventoryView = new InventoryView(inventoryUI);
                anyViewConfigured = true;
                // Debug.Log("[PlayerEquipmentMenuController] Vista de inventario creada");
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
            // Debug.Log("[PlayerEquipmentMenuController] Vista de inventario ya existe");
        }

        if (_spellView == null)
        {
            // Debug.Log($"[PlayerEquipmentMenuController] Verificando spellUI.IsConfigured...");
            if (spellUI.IsConfigured)
            {
                _spellView = new SpellView(spellUI);
                anyViewConfigured = true;
                // Debug.Log("[PlayerEquipmentMenuController] Vista de hechizos creada");
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
            // Debug.Log("[PlayerEquipmentMenuController] Vista de hechizos ya existe");
        }

        if (_equipmentView == null)
        {
            // Debug.Log($"[PlayerEquipmentMenuController] Verificando equipmentUI.IsConfigured...");
            if (equipmentUI.IsConfigured)
            {
                _equipmentView = new EquipmentView(equipmentUI);
                anyViewConfigured = true;
                // Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento creada");
                
                // CRÍTICO: Refrescar la vista para suscribirla a eventos
                _equipmentView.Refresh();
                // Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento refrescada y suscrita");
            }
            else if (!_warnedEquipment)
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] Vista de equipamiento no configurada: añade filas con categoría y botones.");
                _warnedEquipment = true;
            }
        }
        else
        {
            anyViewConfigured = true;
            // Debug.Log("[PlayerEquipmentMenuController] Vista de equipamiento ya existe");
        }

        // Debug.Log($"[PlayerEquipmentMenuController] EnsureViews() retornando: {anyViewConfigured}");
        
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
            Debug.LogError("â•‘    â€¢ Equipment UI: root y categorías configuradas                  â•‘");
            Debug.LogError("â•‘ 3. Añade el componente PlayerEquipmentMenuController al Canvas    â•‘");
            Debug.LogError("â•‘ 4. El controller debe estar en la escena Start o como DontDestroy â•‘");
            Debug.LogError("â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•");
            Debug.LogError($"GameObject actual: '{gameObject.name}' (Canvas: {(canvas != null ? "Sí" : "No")}, WindowRoot: {(windowRoot != null ? "Sí" : "No")})");
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
        
        [Header("Scroll (opcional - se busca automáticamente si no se asigna)")]
        [Tooltip("ScrollRect del inventario. Si no se asigna, se busca automáticamente desde rowsParent.")]
        public ScrollRect scrollRect;
        
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
            InventoryRowWidget _highlightedRow; // Fila actualmente resaltada (navegación)
            readonly ScrollRect _scrollRect;
            enum InventoryInteractionState { Browsing, UseButtonFocused }
            InventoryInteractionState _interactionState = InventoryInteractionState.Browsing;
            Vector3 _useButtonBaseScale;
            bool _useButtonVisualCached;
            DG.Tweening.Tween _useButtonTween; // Tween del botón para poder cancelarlo al cerrar

        public InventoryView(InventoryBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            // Intentar usar el ScrollRect asignado manualmente, o buscarlo automáticamente
            if (_ui.scrollRect != null)
            {
                _scrollRect = _ui.scrollRect;
                // Debug.Log($"[InventoryView] ✅ ScrollRect asignado manualmente: {_scrollRect.name}");
            }
            else if (_ui.rowsParent != null)
            {
                _scrollRect = _ui.rowsParent.GetComponentInParent<ScrollRect>();
                if (_scrollRect != null)
                    Debug.Log($"[InventoryView] ✅ ScrollRect encontrado automáticamente: {_scrollRect.name}");
                else
                    Debug.LogWarning($"[InventoryView] ⚠️ ScrollRect NO encontrado. Asigna manualmente el ScrollRect en el Inspector (Inventory UI → Scroll Rect) o verifica que '{_ui.rowsParent.name}' esté bajo un GameObject con ScrollRect.");
            }

            if (_ui.useButton != null)
            {
                _ui.useButton.onClick.AddListener(UseSelectedItem);
                _useButtonBaseScale = _ui.useButton.transform.localScale;
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

            if (!value)
            {
                ExitUseButtonFocus(false);
                ResetUseButtonFeedback();
                if (_ui.useButton != null)
                {
                    _ui.useButton.interactable = false;
                    _ui.useButton.gameObject.SetActive(false);
                }

                var selected = EventSystem.current?.currentSelectedGameObject;
                if (_ui.root != null && selected != null && selected.transform.IsChildOf(_ui.root.transform))
                    EventSystem.current?.SetSelectedGameObject(null);

                // Matar tweens pendientes al ocultar
                _useButtonTween?.Kill();
                _useButtonTween = null;
                
                if (_boundInventory != null)
                {
                    _boundInventory.OnInventoryChanged -= HandleInventoryChanged;
                    _boundInventory = null;
                }
            }
        }

        bool IsInventoryInputContextValid()
        {
            if (Instance == null) return false;
            if (Instance._activeTab != 0) return false;
            if (_ui.root == null) return false;
            return _ui.root.activeInHierarchy;
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
                UpdateEmptyState(LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_UNAVAILABLE", "Inventario no disponible")
                    : "Inventario no disponible");
                return;
            }

            if (rebuildList)
                BuildList();
            else
                UpdateRowTexts();

            // Priorizar restaurar la selección previa; si no existe, enfocar la primera fila para permitir la navegación inmediata
            if (_selectedItem != null)
            {
                UpdateSelectedItemDetails();
            }
            else
            {
                // Limpiar detalles si no hay selección
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

                // Garantizar auto-scroll al seleccionar: añadir/configurar ScrollOnSelectRelay
                var rect = widget.GetComponent<RectTransform>();
                if (rect != null && _scrollRect != null)
                {
                    var relay = widget.GetComponent<ScrollOnSelectRelay>();
                    if (relay == null)
                        relay = widget.gameObject.AddComponent<ScrollOnSelectRelay>();
                    relay.scrollRect = _scrollRect;
                    relay.target = rect;
                    Debug.Log($"[InventoryView] ScrollOnSelectRelay configurado para item '{entry.item?.displayName ?? "null"}'");
                }
                else if (_scrollRect == null)
                {
                    Debug.LogWarning($"[InventoryView] ⚠️ No se puede añadir ScrollOnSelectRelay: ScrollRect es null");
                }

                var capturedWidget = widget;
                var capturedItem = entry.item;
                widget.RegisterClickHandler(() => HandleRowActivated(capturedWidget, capturedItem, true));
                widget.RegisterSelectedHandler(() => HandleRowActivated(capturedWidget, capturedItem, false));

                _rows.Add(widget);
            }

            UpdateRowNavigation();
            
            // Inicializar sin selección
            _highlightedRow = null;
            _selectedItem = null;
            UpdateRowVisuals();

            if (_rows.Count == 0)
                UpdateEmptyState(LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_EMPTY", "Inventario vacío")
                    : "Inventario vacío");
        }

        void HandleRowActivated(InventoryRowWidget widget, ItemData item, bool focus)
        {
            bool selectionChanged = _selectedItem != item;
            _selectedItem = item;
            _highlightedRow = widget;
            _lastSelectedRow = widget; // Asignar también para que TryHandleSubmit funcione
            
            // Actualizar resaltado visual de todas las filas SIEMPRE (para que se vea al navegar)
            UpdateRowVisuals();
            
            // Solo hacer focus si es necesario
            if (focus)
                FocusRow(widget, true);
            
            UpdateSelectedItemDetails();

            // Si cambió la selección, limpiar feedback
            if (selectionChanged)
            {
                ClearFeedbackImmediate();
                ExitUseButtonFocus(false);
            }

            // NO llamar a HandleRowSubmit automáticamente - solo con Submit del gamepad
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
            if (widget == null || !widget.gameObject.activeInHierarchy) return;

            if (forceFocus)
                widget.Focus();

            ScrollToRow(widget);
        }

        void ScrollToRow(InventoryRowWidget widget)
        {
            if (widget == null) return;
            if (_scrollRect == null)
            {
                Debug.LogWarning("[InventoryView] ScrollRect no encontrado en el padre de rowsParent. Verifica que el contenedor esté bajo un ScrollRect.");
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
                UpdateEmptyState(LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_SELECT_ITEM", "Selecciona un objeto")
                    : "Selecciona un objeto");
                return;
            }

            if (_ui.itemName != null)
                _ui.itemName.text = _selectedItem.GetLocalizedName();

            if (_ui.itemDescription != null)
            {
                string localizedDesc = _selectedItem.GetLocalizedDescription();
                _ui.itemDescription.text = string.IsNullOrEmpty(localizedDesc)
                    ? (LocalizationManager.Instance != null
                        ? LocalizationManager.Instance.Get("INVENTORY_NO_DESCRIPTION", "Sin descripción.")
                        : "Sin descripción.")
                    : localizedDesc;
            }

            if (_ui.itemCount != null)
            {
                int count = _inventory != null ? _inventory.Count(_selectedItem.itemId) : 0;
                string countFmt = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("INVENTORY_QUANTITY_LABEL", "Cantidad: {0}")
                    : "Cantidad: {0}";
                _ui.itemCount.text = string.Format(countFmt, count);
            }

            if (_ui.useButton != null)
            {
                _ui.useButton.gameObject.SetActive(true);
                // El botón permanece deshabilitado hasta que se haga Submit en el item
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
            if (!IsInventoryInputContextValid())
            {
                Debug.LogWarning("[InventoryView] Ignorando UseSelectedItem fuera del tab de inventario.");
                return;
            }

            if (_inventory == null || _selectedItem == null) return;
            
            // NUEVO: Activar flag INMEDIATAMENTE
            if (Instance != null)
            {
                Instance.SetUsingItem(true, 2.5f); // Aumentado un poco por seguridad
            }
            
            // Cambiar estado pero NO resetear visualmente todavía
            _interactionState = InventoryInteractionState.Browsing;

            // Detectar qué efectos tiene el item para animar después
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
                    // Resetear el botón porque falló
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

            // Refrescar panel de estadísticas inmediatamente (especialmente al usar pociones)
            Instance?.UpdatePlayerInfoPanel();
            
            // Animar feedback visual según el tipo de efecto
            if (hasHealthRestore)
                Instance?.AnimateHealthRestoreFeedback();
            if (hasManaRestore)
                Instance?.AnimateManaRestoreFeedback();
            
            // Resetear el botón después de un breve delay para que se vea el efecto
            ResetUseButtonAfterUse(true);
        }
        
        void ResetUseButtonAfterUse(bool restoreSelection)
        {
            // Matar cualquier tween previo
            _useButtonTween?.Kill();
            _useButtonTween = null;
            
            // Pequeño delay para que se vean las animaciones antes de resetear
            if (_ui.useButton != null)
            {
                _useButtonTween = _ui.useButton.transform
                    .DOScale(_useButtonBaseScale, 0.2f)
                    .SetDelay(0.3f)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true)
                    .OnComplete(() => {
                        _useButtonTween = null;
                        
                        // Verificar que el UI sigue activo antes de hacer cualquier cosa
                        if (_ui.root == null || !_ui.root.activeInHierarchy)
                            return;
                        
                        if (_ui.useButton != null)
                        {
                            _ui.useButton.interactable = false;
                            // Restaurar color del Image
                            var buttonImage = _ui.useButton.GetComponent<UnityEngine.UI.Image>();
                            if (buttonImage != null)
                            {
                                buttonImage.color = Color.white;
                            }
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

            // Solo restaurar el foco si ya había una selección previa
            // No forzar selección automática al abrir el menú
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

            // Restaurar la selección previa si existe
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

            Debug.Log("[InventoryView] FocusUseButton - Cambiando estado a UseButtonFocused");
            _interactionState = InventoryInteractionState.UseButtonFocused;

            // Habilitar el botón si no lo está
            if (!_ui.useButton.interactable)
            {
                Debug.Log("[InventoryView] Habilitando botón useButton");
                _ui.useButton.interactable = true;
            }

            // Aplicar el feedback visual inmediatamente (sin esperar frames)
            PlayUseButtonFeedback();
            
            // Reproducir sonido de selección/confirmación
            GamepadInputReader.PlayUISound("UI_Select");
            
            Debug.Log($"[InventoryView] FocusUseButton completado - Estado final: {_interactionState}");
        }

        void ExitUseButtonFocus(bool restoreSelection)
        {
            if (_interactionState != InventoryInteractionState.UseButtonFocused)
                return;

            _interactionState = InventoryInteractionState.Browsing;
            ResetUseButtonFeedback();
            
            // Deshabilitar el botón al volver a la lista
            if (_ui.useButton != null)
                _ui.useButton.interactable = false;

            if (restoreSelection && _lastSelectedRow != null)
                FocusRow(_lastSelectedRow, true);
        }

        void PlayUseButtonFeedback()
        {
            if (_ui.useButton == null) return;

            // Color amarillo/dorado
            Color yellowColor = new Color(1f, 0.85f, 0.2f, 1f);
            
            // Obtener el Image del botón y cambiar su color directamente
            var buttonImage = _ui.useButton.GetComponent<UnityEngine.UI.Image>();
            if (buttonImage != null)
            {
                // ⭐ SOLUCIÓN SIMPLE: Cambiar el color del Image directamente
                buttonImage.color = yellowColor;
                Debug.Log("[InventoryView] Color amarillo aplicado directamente al Image");
            }

            // Animación de escala simple
            if (!_useButtonVisualCached)
            {
                _useButtonBaseScale = _ui.useButton.transform.localScale;
                _useButtonVisualCached = true;
            }
            
            var targetScale = _useButtonBaseScale * 1.1f;
            _ui.useButton.transform
                .DOScale(targetScale, 0.2f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
                
            // Seleccionar el botón
            _ui.useButton.Select();
        }

        void ResetUseButtonFeedback()
        {
            if (_ui.useButton == null || !_useButtonVisualCached)
                return;

            // Restaurar escala
            _ui.useButton.transform.localScale = _useButtonBaseScale;
            
            // Restaurar color del Image a blanco (el color por defecto)
            var buttonImage = _ui.useButton.GetComponent<UnityEngine.UI.Image>();
            if (buttonImage != null)
            {
                buttonImage.color = Color.white; // Color por defecto
            }
        }

        public bool TryHandleCancel()
        {
            if (!IsInventoryInputContextValid())
                return false;

            if (_interactionState == InventoryInteractionState.UseButtonFocused)
            {
                ExitUseButtonFocus(true);
                return true;
            }
            return false;
        }

        public bool TryHandleSubmit()
        {
            if (!IsInventoryInputContextValid())
                return false;

            Debug.Log($"[InventoryView] TryHandleSubmit - Estado: {_interactionState}, SelectedRow: {(_lastSelectedRow != null ? "OK" : "NULL")}, SelectedItem: {(_selectedItem != null ? _selectedItem.displayName : "NULL")}");
            
            if (_interactionState == InventoryInteractionState.UseButtonFocused)
            {
                // Segunda pulsación: Usar el item
                Debug.Log("[InventoryView] Segunda pulsación - Usando item");
                UseSelectedItem();
                return true;
            }

            if (_lastSelectedRow != null && _selectedItem != null)
            {
                // Primera pulsación: Enfocar botón de usar
                Debug.Log("[InventoryView] Primera pulsación - Enfocando botón de usar");
                HandleRowSubmit();
                Debug.Log($"[InventoryView] Después de HandleRowSubmit - Estado: {_interactionState}");
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
        
        [Header("Slots - Botón izquierdo (X)")]
        public Button leftSlotButton;
        public Text leftSlotLabel;

        [Header("Slots - Botón derecho (B)")]
        public Button rightSlotButton;
        public Text rightSlotLabel;

        [Header("Slots - Botón especial (Y)")]
        public Button specialSlotButton;
        public Text specialSlotLabel;
        
        [Header("Lista de hechizos")]
        public Transform rowsParent;
        public SpellRowWidget rowPrefab;
        public Text detailsText;
        
        [Header("Scroll (opcional - se busca automáticamente si no se asigna)")]
        [Tooltip("ScrollRect de hechizos. Si no se asigna, se busca automáticamente desde rowsParent.")]
        public ScrollRect scrollRect;
        
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
        static string Loc(string key, string fallback) =>
            LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, fallback) : fallback;

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
            
            // Intentar usar el ScrollRect asignado manualmente, o buscarlo automáticamente
            if (_ui.scrollRect != null)
            {
                _scrollRect = _ui.scrollRect;
                // Debug.Log($"[SpellView] ✅ ScrollRect asignado manualmente: {_scrollRect.name}");
            }
            else if (_ui.rowsParent != null)
            {
                _scrollRect = _ui.rowsParent.GetComponentInParent<ScrollRect>();
                if (_scrollRect != null)
                    Debug.Log($"[SpellView] ✅ ScrollRect encontrado automáticamente: {_scrollRect.name}");
                else
                    Debug.LogWarning($"[SpellView] ⚠️ ScrollRect NO encontrado. Asigna manualmente el ScrollRect en el Inspector (Spell UI → Scroll Rect) o verifica que '{_ui.rowsParent.name}' esté bajo un GameObject con ScrollRect.");
            }
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
            // No seleccionar ningún hechizo al abrir, limpiar detalles
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

            // Configurar auto-scroll al seleccionar este hechizo
            var rect = widget.GetComponent<RectTransform>();
            if (rect != null && _scrollRect != null)
            {
                var relay = widget.GetComponent<ScrollOnSelectRelay>();
                if (relay == null)
                    relay = widget.gameObject.AddComponent<ScrollOnSelectRelay>();
                relay.scrollRect = _scrollRect;
                relay.target = rect;
                Debug.Log($"[SpellView] ScrollOnSelectRelay configurado para hechizo '{ResolveName(spellId)}'");
            }
            else if (_scrollRect == null)
            {
                Debug.LogWarning($"[SpellView] ⚠️ No se puede añadir ScrollOnSelectRelay: ScrollRect es null");
            }

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
                description = Loc("SPELL_UNASSIGNED", "Sin asignar.");
            }
            else
            {
                var spell = GetSpellAsset(id);
                if (spell == null)
                {
                    description = Loc("SPELL_NO_INFO", "Hechizo sin información.");
                }
                else
                {
                    string damageLabel = string.Format(Loc("SPELL_DAMAGE_LABEL", "Daño: {0}"), spell.damage);
                    string manaLabel = string.Format(Loc("SPELL_MANA_COST_LABEL", "Coste de maná: {0}"), spell.manaCost);
                    string cooldownLabel = string.Format(Loc("SPELL_COOLDOWN_LABEL", "Cooldown: {0}s"), spell.cooldown.ToString("F2"));
                    description = $"{spell.GetLocalizedName()}\n{damageLabel}\n{manaLabel}\n{cooldownLabel}";
                }
            }

            switch (_assignmentMode)
            {
                case AssignmentMode.WaitingForSpellSelection:
                    description += "\n" + Loc("SPELL_SELECT_SPELL_HINT", "Selecciona un hechizo con A o cancela con B.");
                    break;
                case AssignmentMode.WaitingForSlotSelection:
                    description += "\n" + Loc("SPELL_SELECT_SLOT_HINT", "Selecciona un slot con A o cancela con B.");
                    break;
                default:
                    description += "\n" + Loc("SPELL_ASSIGN_HINT", "Pulsa A sobre un hechizo y luego escoge el slot al que asignarlo.");
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
            bool isFocused = slot == _focusedSlot;

            if (isWaitingForSlot && canAssignPending && isFocused)
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

        void HandleSlotFocused(Button button, MagicSlot slot)
        {
            _focusedSlot = slot;
            if (_assignmentMode == AssignmentMode.WaitingForSlotSelection)
                UpdateSlotButtonVisuals();
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
            if (id == SpellId.None) return Loc("SPELL_UNASSIGNED_SHORT", "Sin asignar");
            var spell = GetSpellAsset(id);
            return spell != null ? spell.GetLocalizedName() : id.ToString();
        }

        MagicSpellSO GetSpellAsset(SpellId id)
        {
            return _library != null ? _library.Get(id) : null;
        }

        void PlaySlotPulseFeedback(MagicSlot slot)
        {
            if (!_slotToButton.TryGetValue(slot, out var button) || button == null)
                return;

            // No añadir tween si ya está corriendo para este slot
            if (_slotFeedbackTweens.ContainsKey(button)) return;

            if (!_slotBaseScales.ContainsKey(button))
                _slotBaseScales[button] = button.transform.localScale;

            var baseScale = _slotBaseScales[button];
            var tween = button.transform
                .DOScale(baseScale * 1.12f, 0.35f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
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
            public Image icon;
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
        ModularAutoBuilder _builder;
        WardrobeInventory _wardrobe;
        WardrobeInventory _boundWardrobe;
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
            if (!value)
            {
                ResetAllHighlights();

                // FIX M10 (auditoría 2026-08-07): a diferencia de InventoryView, EquipmentView no
                // se desuscribía de OnWardrobeChanged al ocultarse — solo Dispose() lo hacía. Con
                // el canvas oculto (otra pestaña activa) HandleWardrobeChanged seguía disparándose
                // y refrescando la UI de esta vista sin que nadie la viera ("refrescos fantasma").
                // Al limpiar _boundWardrobe aquí, Refresh() vuelve a suscribirse solo la próxima
                // vez que la pestaña se muestre (ver el bloque `if (_activeTab == 2)` que llama a
                // Refresh() justo después de SetVisible(true)).
                if (_boundWardrobe != null)
                {
                    _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
                    _boundWardrobe = null;
                }
            }
            if (_ui.root != null)
                _ui.root.SetActive(value);
        }

        public void Refresh()
        {
            PlayerService.TryGetComponent(out _builder, includeInactive: true, allowSceneLookup: true);
            PlayerService.TryGetComponent(out _wardrobe, includeInactive: true, allowSceneLookup: true);

            // Debug.Log($"[EquipmentView.Refresh] Builder: {(_builder != null ? "Found" : "NULL")}, Wardrobe: {(_wardrobe != null ? "Found" : "NULL")}");

            if (_boundWardrobe != _wardrobe)
            {
                if (_boundWardrobe != null)
                {
                    _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
                    // Debug.Log($"[EquipmentView.Refresh] Desuscrito de OnWardrobeChanged del wardrobe anterior");
                }

                if (_wardrobe != null)
                {
                    _wardrobe.OnWardrobeChanged += HandleWardrobeChanged;
                    // Debug.Log($"[EquipmentView.Refresh] ✅ Suscrito exitosamente a OnWardrobeChanged");
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
                    var options = _wardrobe.GetUnlockedOptions(category);
                    hasOptions = options != null && options.Count > 0;
                    
                    // Log para depuración de todas las categorías
                    // Debug.Log($"[EquipmentView.Refresh] Categoría {category} tiene {options?.Count ?? 0} opciones desbloqueadas");
                    // if (hasOptions)
                    // {
                    //     foreach (var entry in options)
                    //     {
                    //         Debug.Log($"  [{category}] Item: {entry.partName} ({entry.displayName})");
                    //     }
                    // }
                }
                else
                {
                    // Debug.LogWarning($"[EquipmentView.Refresh] No hay wardrobe disponible para verificar categoría {category}");
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

            // Resetear highlights de otros botones para evitar que queden múltiples amarillos
            ResetOtherHighlights();

            bool changed = false;

            if (_wardrobe != null)
            {
                changed = TryCycleWithWardrobe(category, step);
                if (!changed)
                    return;
            }
            else
            {
                CycleBuilderPart(category, step);
                changed = true;
            }

            Snapshot();
            UpdateLabels();
        }

        void Clear(PartCategory category)
        {
            if (_builder == null) return;
            
            // Guardar referencia al botón previous/next de esta fila ANTES de hacer cambios
            Button fallbackButton = null;
            if (_rows.TryGetValue(category, out var row) && row != null)
            {
                if (row.previousButton != null && row.previousButton.IsInteractable())
                    fallbackButton = row.previousButton;
                else if (row.nextButton != null && row.nextButton.IsInteractable())
                    fallbackButton = row.nextButton;
            }
            
            // Resetear highlights de otros botones
            ResetOtherHighlights();
            
            SetBuilderPart(category, null);
            Snapshot();
            UpdateLabels();
            
            // Actualizar interactividad de los botones después de Clear
            UpdateRowInteractivity(category);
            
            // Forzar selección al botón previous/next de la misma fila
            if (fallbackButton != null && fallbackButton.IsInteractable())
            {
                Debug.Log($"[EquipmentView.Clear] Moviendo selección a {fallbackButton.name}");
                EventSystem.current?.SetSelectedGameObject(null);
                EventSystem.current?.SetSelectedGameObject(fallbackButton.gameObject);
                
                // Forzar el highlight visual
                var highlight = fallbackButton.GetComponent<ButtonHighlight>();
                if (highlight != null)
                {
                    highlight.OnSelect(null);
                }
            }
            else
            {
                // Buscar cualquier botón válido
                EnsureValidSelection(category);
            }
        }
        
        /// <summary>
        /// Actualiza la interactividad de una fila específica después de un cambio
        /// </summary>
        void UpdateRowInteractivity(PartCategory category)
        {
            if (!_rows.TryGetValue(category, out var row) || row == null) return;
            
            bool hasOptions = false;
            if (_wardrobe != null)
            {
                var options = _wardrobe.GetUnlockedOptions(category);
                hasOptions = options != null && options.Count > 0;
            }
            
            bool allowClear = _wardrobe == null ? _builder != null : hasOptions;
            
            // Verificar si hay algo seleccionado actualmente en esta categoría
            string currentSelection = GetSelectionFor(category);
            bool hasSomethingEquipped = !string.IsNullOrEmpty(currentSelection);
            
            // Solo permitir Clear si hay algo equipado
            allowClear = allowClear && hasSomethingEquipped;
            
            SetInteractable(row, _builder != null || hasOptions, allowClear);
        }
        
        /// <summary>
        /// Asegura que la selección actual sea un botón válido/interactuable
        /// </summary>
        void EnsureValidSelection(PartCategory category)
        {
            var currentSelected = EventSystem.current?.currentSelectedGameObject;
            if (currentSelected == null) return;
            
            // Verificar si el botón actual sigue siendo interactuable
            var currentButton = currentSelected.GetComponent<Button>();
            if (currentButton != null && currentButton.IsInteractable())
                return; // Todo bien, el botón actual es válido
            
            // El botón actual no es interactuable, buscar uno válido en la misma fila
            if (_rows.TryGetValue(category, out var row) && row != null)
            {
                // Intentar seleccionar previous o next primero (para poder seguir ciclando)
                Button newSelection = null;
                if (row.previousButton != null && row.previousButton.IsInteractable())
                    newSelection = row.previousButton;
                else if (row.nextButton != null && row.nextButton.IsInteractable())
                    newSelection = row.nextButton;
                
                if (newSelection != null)
                {
                    EventSystem.current.SetSelectedGameObject(newSelection.gameObject);
                    return;
                }
            }
            
            // Si no hay botón válido en la fila actual, buscar en otras filas
            foreach (var kvp in _rows)
            {
                var r = kvp.Value;
                if (r == null) continue;
                
                if (r.previousButton != null && r.previousButton.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(r.previousButton.gameObject);
                    return;
                }
                if (r.nextButton != null && r.nextButton.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(r.nextButton.gameObject);
                    return;
                }
                if (r.clearButton != null && r.clearButton.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(r.clearButton.gameObject);
                    return;
                }
            }
        }
        
        /// <summary>
        /// Resetea el highlight de todos los botones excepto el actualmente seleccionado
        /// </summary>
        void ResetOtherHighlights()
        {
            var currentSelected = EventSystem.current?.currentSelectedGameObject;
            foreach (var row in _rows.Values)
            {
                ResetButtonIfNotSelected(row?.previousButton, currentSelected);
                ResetButtonIfNotSelected(row?.nextButton, currentSelected);
                ResetButtonIfNotSelected(row?.clearButton, currentSelected);
            }
        }
        
        void ResetButtonIfNotSelected(Button button, GameObject currentSelected)
        {
            if (button == null) return;
            if (button.gameObject == currentSelected) return; // No resetear el seleccionado
            
            var highlight = button.GetComponent<ButtonHighlight>();
            if (highlight != null)
            {
                highlight.ResetColor();
            }
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
            var selection = _builder.GetSelection();

            foreach (var kvp in _rows)
            {
                var row = kvp.Value;
                if (row?.label == null) continue;

                string value = "Sin asignar";
                string partName = null;
                if (selection != null && selection.TryGetValue(kvp.Key, out var part) && !string.IsNullOrEmpty(part))
                {
                    partName = part;
                    value = ResolveDisplayName(kvp.Key, part);
                }

                row.label.text = $"{FormatCategory(kvp.Key)}: {value}";
                UpdateRowIcon(row, kvp.Key, partName);
            }
        }

        // Muestra el icono del item equipado en la fila (si el item tiene uno asignado en su WardrobeItemSO).
        // Items desbloqueados vía AutoUnlockAll() no tienen icono propio, así que se oculta el Image en ese caso.
        void UpdateRowIcon(EquipmentBindings.RowBinding row, PartCategory category, string partName)
        {
            if (row.icon == null) return;

            Sprite iconSprite = null;
            if (!string.IsNullOrEmpty(partName) && _wardrobe != null && _wardrobe.TryGetEntry(category, partName, out var entry))
                iconSprite = entry.icon;

            row.icon.sprite = iconSprite;
            row.icon.enabled = iconSprite != null;
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
            if (_wardrobe == null) return false;

            var options = _wardrobe.GetUnlockedOptions(category);
            if (options == null || options.Count == 0) return false;

            string current = GetSelectionFor(category);
            int currentIndex = -1;

            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].partName, current, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
                currentIndex = step > 0 ? 0 : options.Count - 1;

            int nextIndex = (currentIndex + step) % options.Count;
            if (nextIndex < 0) nextIndex += options.Count;

            var nextPartName = options[nextIndex].partName;
            if (string.IsNullOrEmpty(nextPartName)) return false;

            SetBuilderPart(category, nextPartName);
            return true;
        }

        void ClearSelection(PartCategory category)
        {
            if (_builder == null) return;
            SetBuilderPart(category, null);
        }

        string GetSelectionFor(PartCategory category)
        {
            if (_builder == null) return null;
            var selection = _builder.GetSelection();
            if (selection != null && selection.TryGetValue(category, out var part))
                return part;
            return null;
        }

        string ResolveDisplayName(PartCategory category, string partName)
        {
            if (string.IsNullOrEmpty(partName))
            {
                return LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get("SPELL_UNASSIGNED_SHORT", "Sin asignar")
                    : "Sin asignar";
            }
            if (_wardrobe != null && _wardrobe.TryGetEntry(category, partName, out var entry))
            {
                return string.IsNullOrEmpty(entry.displayName) ? partName : entry.displayName;
            }
            return partName;
        }

        void HandleWardrobeChanged()
        {
            Debug.Log("[EquipmentView] 📢 HandleWardrobeChanged - Evento recibido, refrescando opciones disponibles");
            
            // Log del wardrobe actual
            if (_wardrobe != null)
            {
                Debug.Log($"[EquipmentView] Wardrobe encontrado: {_wardrobe.GetType().Name}");
            }
            else
            {
                Debug.LogWarning("[EquipmentView] ⚠️ Wardrobe es NULL en HandleWardrobeChanged!");
            }
            
            Refresh();
            // Forzar actualización de la UI
            UpdateAllRowsUI();
        }

        void UpdateAllRowsUI()
        {
            Debug.Log("[EquipmentView] UpdateAllRowsUI - Actualizando todas las filas visualmente");
            UpdateLabels();
            
            // Forzar recálculo de interactividad de todos los botones
            foreach (var kvp in _rows)
            {
                var row = kvp.Value;
                var category = kvp.Key;
                
                bool hasOptions = false;
                if (_wardrobe != null)
                {
                    var options = _wardrobe.GetUnlockedOptions(category);
                    hasOptions = options != null && options.Count > 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[EquipmentView.UpdateAllRowsUI] Categoría {category}: {options?.Count ?? 0} opciones, hasOptions={hasOptions}");
#endif
                }

                bool allowClear = _wardrobe == null ? _builder != null : hasOptions;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[EquipmentView.UpdateAllRowsUI] Categoría {category}: Builder={(_builder != null)}, hasOptions={hasOptions}, allowClear={allowClear}");
#endif
                SetInteractable(row, _builder != null || hasOptions, allowClear);
            }
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
                _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
            }
        }

        string FormatCategory(PartCategory cat)
        {
            return cat switch
            {
                PartCategory.WeaponR => "Arma Mano Derecha",
                PartCategory.ShieldR => "Escudo Mano Izquierda",
                PartCategory.Bow => "Arco",
                PartCategory.Body => "Vestuario",
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

        // Llama al método Next/Prev del builder directamente
        void CycleBuilderPart(PartCategory category, int step)
        {
            if (_builder == null) return;
            _builder.Next(category, step);
        }

        void SetBuilderPart(PartCategory category, string nameOrNull)
        {
            if (_builder == null) return;
            _builder.SetByName(category, nameOrNull);
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

        class ButtonHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
        {
            public Graphic target;
            public Color highlightColor = Color.white; // ya no se usa para colorear, se mantiene por compatibilidad
            Vector3 _baseScale;
            bool _baseScaleCaptured;
            Tween _pulseTween;

            void Awake()
            {
                if (target == null)
                    target = GetComponent<Graphic>();
                if (target == null)
                    target = GetComponentInChildren<Graphic>();
            }

            public void Configure(Color color)
            {
                highlightColor = color;
                if (!_baseScaleCaptured)
                {
                    _baseScale = transform.localScale;
                    _baseScaleCaptured = true;
                }
            }

            public void OnSelect(BaseEventData eventData)
            {
                if (!_baseScaleCaptured)
                {
                    _baseScale = transform.localScale;
                    _baseScaleCaptured = true;
                }
                _pulseTween?.Kill();
                _pulseTween = transform
                    .DOScale(_baseScale * 1.12f, 0.35f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetUpdate(true);
            }

            public void OnDeselect(BaseEventData eventData)
            {
                StopPulse();
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                StartCoroutine(ResetAfterFrame());
            }

            public void OnSubmit(BaseEventData eventData)
            {
                StartCoroutine(ResetAfterFrame());
            }

            System.Collections.IEnumerator ResetAfterFrame()
            {
                yield return null;
                if (UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject != gameObject)
                    StopPulse();
            }

            public void ResetColor()
            {
                StopPulse();
            }

            void StopPulse()
            {
                _pulseTween?.Kill();
                _pulseTween = null;
                if (_baseScaleCaptured)
                    transform.localScale = _baseScale;
            }
        }
    }
}
