using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    [Header("Contenedores UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Objeto raíz del contenido del menú (se activa/desactiva al abrir/cerrar).")]
    [SerializeField] private GameObject windowRoot;

    [Header("Pestañas")]
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button spellsTabButton;
    [SerializeField] private Button equipmentTabButton;

    [Header("Panel de jugador")]
    [SerializeField] private Text levelText;
    [SerializeField] private Text hpText;
    [SerializeField] private Text mpText;

    [Header("Habilidades")]
    [SerializeField] private GameObject abilitiesRoot;
    [SerializeField] private AbilityEntryReferences abilityEntries = new();

    [Header("Selección inicial")]
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
    [Header("Cámara de equipamiento")]
    [SerializeField] private Camera equipmentPreviewCamera;
    [SerializeField] private float equipmentCameraDistance = 3f;
    [SerializeField] private float equipmentCameraHeight = 1.7f;
    [SerializeField] private Vector3 equipmentCameraLookOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private float equipmentCameraHorizontalOffset = -1.2f;
    [SerializeField] private float previewOrbitSpeed = 120f;
    [Header("Navegación UI")]
    [SerializeField, Min(0f)] private float uiNavRepeatDelay = 0.18f;
    [SerializeField, Range(0f, 1f)] private float uiNavDeadzone = 0.45f;
    bool _equipmentCameraActive;
    Transform _playerPreviewTarget;
    Quaternion _storedPlayerRotation;
    Vector3 _previewBaseForward = Vector3.forward;
    float _previewPlayerYaw;
    PlayerActionManager _actionManager;
    float _uiNavCooldown;
    Vector2 _lastUiNav;
    bool _actionModeActive;

    bool _isOpen;
    int _activeTab;
    float _savedTimeScale = 1f;
    float _lastDpadVertical;

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
        // Pequeño delay para asegurar que Start está cargada si se carga aditivamente
        UnityEngine.Object.FindFirstObjectByType<PlayerEquipmentMenuController>()?.StartCoroutine(DelayedBootstrap());
    }

    static System.Collections.IEnumerator DelayedBootstrap()
    {
        yield return null; // Esperar un frame para que Start termine de cargar

        if (_instance != null) yield break;

#if UNITY_2022_3_OR_NEWER
        var existing = FindFirstObjectByType<PlayerEquipmentMenuController>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        var existing = FindObjectOfType<PlayerEquipmentMenuController>(true);
#pragma warning restore 618
#endif
        if (existing != null)
        {
            _instance = existing;
            // Asegurar persistencia si el usuario colocó el objeto en la escena inicial
            try
            {
                if (existing.transform.root != null)
                    DontDestroyOnLoad(existing.transform.root.gameObject);
                else
                    DontDestroyOnLoad(existing.gameObject);
            }
            catch { }
            yield break;
        }

        var go = new GameObject(nameof(PlayerEquipmentMenuController));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PlayerEquipmentMenuController>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (dontDestroyOnLoad && transform.parent == null)
            DontDestroyOnLoad(gameObject);

        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>(true);
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (windowRoot == null && canvas != null)
            windowRoot = canvas.gameObject;

        SetCanvasState(false);

        RegisterTabButtons();
        EnsureViews();
        SetEquipmentCameraActive(false);
        // Unregister from MenuManager
        MenuManager.Close(MenuKind.Equipment);
    }

    void OnDestroy()
    {
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

        if (PauseMenuController.IsOpen)
        {
            if (_isOpen) CloseMenu();
            return;
        }

        if (GameOverManager.Instance != null && GameOverManager.Instance.IsShown)
        {
            if (_isOpen) CloseMenu();
            return;
        }

        // Si el menú ya está abierto, evita leer el input de apertura para que el D-Pad
        // no interfiera con la navegación UI (el toggle se maneja al cerrarse).
        if (!_isOpen)
        {
            HandleToggleInput();
        }
        else
        {
            HandleCloseInput();
            HandleTabNavigationInput();
            HandleUiNavigationInput();
            UpdatePlayerInfoPanel();
            if (_activeTab == 1)
                _spellView?.HandleInput();
        }
    }

    void RegisterTabButtons()
    {
        if (inventoryTabButton != null)
        {
            inventoryTabButton.onClick.AddListener(() => ShowTab(0));
            _tabButtons.Add(inventoryTabButton);
        }
        if (spellsTabButton != null)
        {
            spellsTabButton.onClick.AddListener(() => ShowTab(1));
            _tabButtons.Add(spellsTabButton);
        }
        if (equipmentTabButton != null)
        {
            equipmentTabButton.onClick.AddListener(() => ShowTab(2));
            _tabButtons.Add(equipmentTabButton);
        }
    }

    void HandleToggleInput()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
            pressed = Gamepad.current.dpad.down.wasPressedThisFrame;
#endif

        if (!pressed)
        {
            try { pressed = Input.GetButtonDown("DPadDown"); } catch { }
            if (!pressed) pressed = Input.GetKeyDown(KeyCode.DownArrow);

            if (!pressed)
            {
                float axis = 0f;
                try { axis = Input.GetAxis("7th axis"); } catch { }
                if (axis < -0.5f && _lastDpadVertical >= -0.5f)
                    pressed = true;
                _lastDpadVertical = axis;
            }
            else
            {
                _lastDpadVertical = -1f;
            }
        }

        if (!pressed) return;

        if (_isOpen)
        {
            // D-Pad abajo solo abre el menú; el cierre se hace con B o Start.
            return;
        }
        else
        {
            if (!GameState.CanOpenInventory) return;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
            OpenMenu();
        }
    }

    void HandleCloseInput()
    {
        bool cancel = false;

#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
            cancel = Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame;
#endif

        if (!cancel)
        {
            cancel = Input.GetKeyDown(KeyCode.Escape) ||
                     Input.GetKeyDown(KeyCode.Backspace) ||
                     Input.GetKeyDown(KeyCode.JoystickButton1);
        }

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

    void HandleTabNavigationInput()
    {
        int delta = 0;

#if ENABLE_INPUT_SYSTEM
        var pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.leftShoulder.wasPressedThisFrame) delta = -1;
            else if (pad.rightShoulder.wasPressedThisFrame) delta = 1;
        }
#endif

        if (delta == 0)
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.PageUp)) delta = -1;
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.PageDown)) delta = 1;
        }

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

    void HandleUiNavigationInput()
    {
        if (!_isOpen) return;

        if (_uiNavCooldown > 0f)
        {
            _uiNavCooldown -= Time.unscaledDeltaTime;
            if (_uiNavCooldown < 0f) _uiNavCooldown = 0f;
        }

        var es = EventSystem.current;
        if (es == null)
            return;

        Vector2 nav = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        try
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                var d = pad.dpad.ReadValue();
                if (Mathf.Abs(d.x) > uiNavDeadzone || Mathf.Abs(d.y) > uiNavDeadzone)
                    nav = d;
                else
                {
                    var s = pad.leftStick.ReadValue();
                    if (Mathf.Abs(s.x) > uiNavDeadzone || Mathf.Abs(s.y) > uiNavDeadzone)
                        nav = s;
                }
            }
            else
            {
                var js = Joystick.current;
                if (js != null)
                {
                    var s = js.stick.ReadValue();
                    if (Mathf.Abs(s.x) > uiNavDeadzone || Mathf.Abs(s.y) > uiNavDeadzone)
                        nav = s;
                }
            }
        }
        catch { }
#endif

        if (nav == Vector2.zero)
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > uiNavDeadzone || Mathf.Abs(v) > uiNavDeadzone)
                nav = new Vector2(h, v);
        }

        if (nav == Vector2.zero)
            return;

        Vector2 dir = nav.normalized;
        if (_uiNavCooldown > 0f && Vector2.Dot(dir, _lastUiNav) > 0.9f)
            return;

        _lastUiNav = dir;
        _uiNavCooldown = uiNavRepeatDelay;

        if (es.currentSelectedGameObject == null)
            SelectInitial();

        var axisEvent = new AxisEventData(es)
        {
            moveVector = dir,
            moveDir = ToMoveDirection(dir)
        };

        var target = es.currentSelectedGameObject;
        if (target != null)
            ExecuteEvents.Execute(target, axisEvent, ExecuteEvents.moveHandler);
    }

    static MoveDirection ToMoveDirection(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x > 0 ? MoveDirection.Right : MoveDirection.Left;
        if (Mathf.Abs(dir.y) > 0f)
            return dir.y > 0 ? MoveDirection.Up : MoveDirection.Down;
        return MoveDirection.None;
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
        if (!GameState.CanOpenInventory) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;

        // Ask central manager for permission to open
        if (!MenuManager.TryOpen(MenuKind.Equipment))
        {
            Debug.Log("[PlayerEquipmentMenuController] Apertura denegada por MenuManager");
            return;
        }

        if (!EnsureViews())
        {
            MenuManager.Close(MenuKind.Equipment);
            return;
        }

        EnsureEventSystem();
        EnsureActionManager();
        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Inventory);
            _actionModeActive = true;
        }

        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        SetCanvasState(true);

        int defaultTab = GetDefaultTab();
        bool forceRebuild = defaultTab == 0;
        ShowTab(defaultTab, forceRebuild);
        UpdatePlayerInfoPanel();

        _isOpen = true;
        GameState.Push(GamePhase.Inventory);
        GameState.Push(GamePhase.Equipment);
        SelectInitial();
        SetEquipmentCameraActive(_activeTab == 2);
    }

    void CloseMenu()
    {
        SetCanvasState(false);
        _spellView?.CancelSlotSelection(true);
        Time.timeScale = _savedTimeScale;
        _isOpen = false;
        if (_actionModeActive && _actionManager != null)
        {
            _actionManager.PopMode(ActionMode.Inventory);
            _actionModeActive = false;
        }
        Input.ResetInputAxes();
        if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
        if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
        SetEquipmentCameraActive(false);
        MenuManager.Close(MenuKind.Equipment);
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
                _inventoryView.EnsureSelection();
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
        SetEquipmentCameraActive(_isOpen && _activeTab == 2);
    }

    void EnsureActionManager()
    {
        if (_actionManager != null) return;
        PlayerService.TryGetComponent(out _actionManager, includeInactive: true, allowSceneLookup: true);
    }

    void LateUpdate()
    {
        if (!_equipmentCameraActive || equipmentPreviewCamera == null) return;
        UpdateEquipmentCamera();
    }

    void UpdateEquipmentCamera()
    {
        if (_playerPreviewTarget == null)
        {
            if (!TrySetupPreviewTarget())
                return;
        }

        float rotateInput = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
            rotateInput += Gamepad.current.rightStick.ReadValue().x;
#endif
        try { rotateInput += Input.GetAxis("RightStickHorizontal"); } catch { }
        try { rotateInput += Input.GetAxis("CameraHorizontal"); } catch { }

        if (Mathf.Abs(rotateInput) > 0.01f)
            _previewPlayerYaw += rotateInput * previewOrbitSpeed * Time.unscaledDeltaTime;

        var cameraForward = _previewBaseForward;
        if (cameraForward.sqrMagnitude < 0.001f) cameraForward = Vector3.forward;
        cameraForward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;

        Vector3 targetPos = _playerPreviewTarget.position + equipmentCameraLookOffset;
        Vector3 cameraPos = targetPos
                            - cameraForward * equipmentCameraDistance
                            + Vector3.up * equipmentCameraHeight
                            - cameraRight * equipmentCameraHorizontalOffset;

        equipmentPreviewCamera.transform.position = cameraPos;
        equipmentPreviewCamera.transform.rotation = Quaternion.LookRotation((targetPos - cameraPos).normalized, Vector3.up);

        var lookDir = Quaternion.Euler(0f, _previewPlayerYaw, 0f) * cameraForward;
        var previewRotation = Quaternion.LookRotation(-lookDir, Vector3.up);
        _playerPreviewTarget.rotation = previewRotation;
    }

    bool TrySetupPreviewTarget()
    {
        if (!PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
        {
            if (equipmentPreviewCamera != null)
                equipmentPreviewCamera.enabled = false;
            return false;
        }

        _playerPreviewTarget = player.transform;
        _storedPlayerRotation = _playerPreviewTarget.rotation;
        var forwardSource = Camera.main != null ? Camera.main.transform.forward : _playerPreviewTarget.forward;
        _previewBaseForward = Vector3.ProjectOnPlane(forwardSource, Vector3.up);
        if (_previewBaseForward.sqrMagnitude < 0.001f)
            _previewBaseForward = Vector3.forward;
        _previewBaseForward.Normalize();
        _previewPlayerYaw = 0f;
        if (equipmentPreviewCamera != null)
            equipmentPreviewCamera.enabled = true;
        return true;
    }

    void SetEquipmentCameraActive(bool value)
    {
        if (equipmentPreviewCamera == null) return;
        if (_equipmentCameraActive == value) return;
        _equipmentCameraActive = value;
        equipmentPreviewCamera.gameObject.SetActive(value);
        if (_equipmentCameraActive)
        {
            TrySetupPreviewTarget();
        }
        else
        {
            if (_playerPreviewTarget != null)
                _playerPreviewTarget.rotation = _storedPlayerRotation;
            _playerPreviewTarget = null;
            equipmentPreviewCamera.enabled = false;
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
        GameObject target = initialSelectionOverride;

        if (target == null)
        {
            if (_activeTab == 0)
                target = _inventoryView?.DefaultSelection;
            else if (_activeTab == 1)
                target = _spellView?.DefaultSelection;
            else
                target = _equipmentView?.DefaultSelection;
        }

        if (target == null && inventoryTabButton != null)
            target = inventoryTabButton.gameObject;

        if (target != null)
            StartCoroutine(SelectOnNextFrame(target));
    }

    System.Collections.IEnumerator SelectOnNextFrame(GameObject target)
    {
        yield return null;
        var es = EventSystem.current;
        if (es != null && target != null)
        {
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(target);
        }
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
            if (levelText != null)
                levelText.text = preset != null ? $"Nivel: {preset.level}" : "Nivel: ?";

            if (hpText != null)
            {
                if (PlayerService.TryGetComponent<PlayerHealthSystem>(out var health, includeInactive: true, allowSceneLookup: true))
                    hpText.text = $"Salud: {Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.CeilToInt(health.MaxHealth)}";
                else if (preset != null)
                    hpText.text = $"Salud: {Mathf.CeilToInt(preset.currentHP)} / {Mathf.CeilToInt(preset.maxHP)}";
                else
                    hpText.text = "Salud: ?";
            }

            if (mpText != null)
            {
                if (PlayerService.TryGetComponent<ManaPool>(out var mana, includeInactive: true, allowSceneLookup: true))
                    mpText.text = $"Magia: {Mathf.CeilToInt(mana.Current)} / {Mathf.CeilToInt(mana.Max)}";
                else if (preset != null)
                    mpText.text = $"Magia: {Mathf.CeilToInt(preset.currentMP)} / {Mathf.CeilToInt(preset.maxMP)}";
                else
                    mpText.text = "Magia: ?";
            }
        }

        UpdateAbilitiesPanel(preset);
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

    bool EnsureViews()
    {
        bool anyViewConfigured = false;

        if (_inventoryView == null)
        {
            if (inventoryUI.IsConfigured)
            {
                _inventoryView = new InventoryView(inventoryUI);
                anyViewConfigured = true;
            }
            else if (!_warnedInventory)
            {
                Debug.LogWarning("[PlayerEquipmentMenuController] Inventario no configurado: asigna root, contenedor y prefab de filas.");
                _warnedInventory = true;
            }
        }
        else
        {
            anyViewConfigured = true;
        }

        if (_spellView == null)
        {
            if (spellUI.IsConfigured)
            {
                _spellView = new SpellView(spellUI);
                anyViewConfigured = true;
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
        }

        if (_equipmentView == null)
        {
            if (equipmentUI.IsConfigured)
            {
                _equipmentView = new EquipmentView(equipmentUI);
                anyViewConfigured = true;
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

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var esGO = new GameObject("EventSystem", typeof(EventSystem)
#if ENABLE_INPUT_SYSTEM
            , typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)
#else
            , typeof(StandaloneInputModule)
#endif
        );
        DontDestroyOnLoad(esGO);
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

        public InventoryView(InventoryBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            if (_ui.useButton != null)
                _ui.useButton.onClick.AddListener(UseSelectedItem);
        }

        public GameObject DefaultSelection => _rows.Count > 0 ? _rows[0].ButtonGameObject : null;

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

            // No seleccionar automáticamente ningún item al abrir el menú
            // El usuario puede navegar y seleccionar manualmente
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
                widget.SetSelectedState(false);

                var capturedWidget = widget;
                var capturedItem = entry.item;
                widget.RegisterClickHandler(() => HandleRowActivated(capturedWidget, capturedItem, true));
                widget.RegisterSelectedHandler(() => HandleRowActivated(capturedWidget, capturedItem, false));

                _rows.Add(widget);
            }

            if (_rows.Count == 0)
                UpdateEmptyState("Inventario vacío");
        }

        void HandleRowActivated(InventoryRowWidget widget, ItemData item, bool focus)
        {
            _selectedItem = item;
            if (_lastSelectedRow != null && _lastSelectedRow != widget)
                _lastSelectedRow.SetSelectedState(false);
            _lastSelectedRow = widget;
            _lastSelectedRow?.SetSelectedState(true);
            FocusRow(widget, focus);
            UpdateSelectedItemDetails();
        }

        void FocusRow(InventoryRowWidget widget, bool forceFocus)
        {
            if (widget == null) return;

            if (forceFocus || EventSystem.current == null || EventSystem.current.currentSelectedGameObject != widget.ButtonGameObject)
                widget.Focus();
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
                _ui.itemDescription.text = string.IsNullOrEmpty(_selectedItem.useDescription) ? "Sin descripción." : _selectedItem.useDescription;

            if (_ui.itemCount != null)
            {
                int count = _inventory != null ? _inventory.Count(_selectedItem.itemId) : 0;
                _ui.itemCount.text = $"Cantidad: {count}";
            }

            if (_ui.useButton != null)
                _ui.useButton.interactable = _selectedItem.usableFromInventory && _inventory.Count(_selectedItem.itemId) > 0;

            if (_ui.feedbackText != null)
                _ui.feedbackText.text = string.Empty;
        }

        void UpdateEmptyState(string message)
        {
            if (_ui.itemName != null) _ui.itemName.text = message;
            if (_ui.itemDescription != null) _ui.itemDescription.text = string.Empty;
            if (_ui.itemCount != null) _ui.itemCount.text = string.Empty;
            if (_ui.feedbackText != null) _ui.feedbackText.text = string.Empty;
            if (_ui.useButton != null) _ui.useButton.interactable = false;
        }

        void UseSelectedItem()
        {
            if (_inventory == null || _selectedItem == null) return;

            var context = new InventoryItemUseContext(_inventory, _selectedItem, _collector);
            var result = DispatchInventoryUseRequest(context);

            if (!result.handled)
            {
                if (!InventoryUseUtility.TryUseItem(_inventory, _selectedItem, _collector, out var reason, out var consumed))
                {
                    if (_ui.feedbackText != null)
                        _ui.feedbackText.text = string.IsNullOrEmpty(reason) ? "No se pudo usar." : reason;
                    return;
                }

                result.handled = true;
                result.consumed = consumed;
            }

            if (result.consumed && _inventory.Count(_selectedItem.itemId) == 0)
                _selectedItem = null;

            Refresh(true);

            if (_ui.feedbackText != null)
            {
                if (string.IsNullOrEmpty(result.message))
                    result.message = "Usado correctamente.";
                _ui.feedbackText.text = result.message;
            }
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

            return false;
        }

        public bool TryHandleCancel() => false;
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
                var es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject != entry.widget?.ButtonGameObject)
                    entry.widget?.Focus();
            }

            if (fromUser)
                _focusArea = FocusArea.SpellList;
            UpdateRowVisuals();
            ShowSpellDetails(entry.spellId);
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
            _presetService?.ApplyCurrentPreset(includeInventory: false);
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
                    description = "Hechizo sin información.";
                }
                else
                {
                    description = $"{spell.displayName}\nDaño: {spell.damage}\nCoste de maná: {spell.manaCost}\nCooldown: {spell.cooldown:F2}s";
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

        void ConfigureRowNavigation()
        {
            var leftTarget = GetPrimarySlotSelectable();
            for (int i = 0; i < _rows.Count; i++)
            {
                var selectable = _rows[i]?.widget?.Selectable;
                if (selectable == null) continue;
                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = leftTarget;
                nav.selectOnRight = null;
                nav.selectOnUp = i > 0 ? _rows[i - 1]?.widget?.Selectable : selectable;
                nav.selectOnDown = i < _rows.Count - 1 ? _rows[i + 1]?.widget?.Selectable : selectable;
                selectable.navigation = nav;
            }
        }

        Selectable GetFirstRowSelectable()
        {
            if (_rows.Count == 0) return null;
            return _rows[0]?.widget?.Selectable;
        }

        Selectable GetPrimarySlotSelectable()
        {
            if (_ui.leftSlotButton != null) return _ui.leftSlotButton;
            if (_ui.rightSlotButton != null) return _ui.rightSlotButton;
            if (_ui.specialSlotButton != null) return _ui.specialSlotButton;
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

            var defaults = _slotDefaultColors.TryGetValue(button, out var colors)
                ? colors
                : button.colors;

            bool highlight = false;
            if (_assignmentMode == AssignmentMode.WaitingForSpellSelection)
                highlight = slot == _pendingSlot;
            else if (_assignmentMode == AssignmentMode.WaitingForSlotSelection)
                highlight = slot == _focusedSlot;

            if (highlight)
            {
                var highlighted = defaults;
                var focusColor = _ui.slotSelectionColor;
                highlighted.normalColor = focusColor;
                highlighted.highlightedColor = focusColor;
                highlighted.selectedColor = focusColor;
                highlighted.pressedColor = focusColor;
                button.colors = highlighted;
            }
            else
            {
                button.colors = defaults;
            }

            if (_assignmentMode == AssignmentMode.WaitingForSpellSelection)
                button.interactable = slot == _pendingSlot;
            else if (_assignmentMode == AssignmentMode.WaitingForSlotSelection)
                button.interactable = CanAssign(slot, _pendingSpell);
            else
                button.interactable = true;

            PlaySlotFeedback(button, highlight && button.interactable);
        }

        void PlaySlotFeedback(Button button, bool active)
        {
            if (button == null) return;

            if (!_slotBaseScales.ContainsKey(button))
                _slotBaseScales[button] = button.transform.localScale;

            if (_slotFeedbackTweens.TryGetValue(button, out var tween) && tween != null)
            {
                tween.Kill();
                _slotFeedbackTweens.Remove(button);
            }

            if (!_slotBaseScales.TryGetValue(button, out var baseScale))
                baseScale = button.transform.localScale;

            var targetScale = active ? baseScale * 1.08f : baseScale;
            var duration = active ? 0.15f : 0.12f;

            var scaleTween = button.transform
                .DOScale(targetScale, duration)
                .SetEase(active ? Ease.OutBack : Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() => _slotFeedbackTweens.Remove(button));

            _slotFeedbackTweens[button] = scaleTween;
        }

        void KillAllSlotFeedback()
        {
            foreach (var kvp in _slotFeedbackTweens)
            {
                kvp.Value?.Kill();
                if (kvp.Key != null && _slotBaseScales.TryGetValue(kvp.Key, out var baseScale))
                    kvp.Key.transform.localScale = baseScale;
            }
            _slotFeedbackTweens.Clear();
        }

        void PlaySlotConfirmFeedback(MagicSlot slot)
        {
            if (!_slotToButton.TryGetValue(slot, out var button) || button == null)
                return;

            var graphic = button.targetGraphic ?? button.GetComponentInChildren<Graphic>();
            if (graphic == null) return;

            var original = graphic.color;
            var minAlpha = Mathf.Clamp01(original.a * 0.45f);

            DOTween.Kill(graphic);
            DOTween.Sequence()
                .Append(graphic.DOFade(minAlpha, 0.08f).SetUpdate(true))
                .Append(graphic.DOFade(original.a, 0.08f).SetUpdate(true))
                .SetLoops(2, LoopType.Yoyo)
                .OnComplete(() => graphic.color = original)
                .SetTarget(graphic);
        }

        void HandleSlotFocused(Button button, MagicSlot slot)
        {
            if (button == null) return;
            _focusedSlot = slot;
            _focusArea = FocusArea.Slots;
            if (_assignmentMode == AssignmentMode.WaitingForSpellSelection)
                _pendingSlot = slot;
            UpdateSlotButtonVisuals();
        }

        void EnforcePresetSlotRules()
        {
            if (_preset == null || _library == null) return;

            bool changed = false;

            bool IsSpecialOnly(SpellId id)
            {
                var spell = _library.Get(id);
                return spell != null && spell.slotType == SpellSlotType.SpecialOnly;
            }

            var left = _preset.leftSpellId;
            var right = _preset.rightSpellId;
            var special = _preset.specialSpellId;

            if (left != SpellId.None && IsSpecialOnly(left)) { left = SpellId.None; changed = true; }
            if (right != SpellId.None && IsSpecialOnly(right)) { right = SpellId.None; changed = true; }
            if (special != SpellId.None && !IsSpecialOnly(special)) { special = SpellId.None; changed = true; }

            if (left != SpellId.None && right == left) { left = SpellId.None; changed = true; }
            if (left != SpellId.None && special == left) { left = SpellId.None; changed = true; }
            if (right != SpellId.None && special == right) { right = SpellId.None; changed = true; }

            if (changed)
            {
                _preset.leftSpellId = left;
                _preset.rightSpellId = right;
                _preset.specialSpellId = special;
                // No restaurar inventario al limpiar duplicados (solo actualizar spells)
                _presetService?.ApplyCurrentPreset(includeInventory: false);
            }
        }

        class SlotSelectListener : MonoBehaviour, ISelectHandler
        {
            public Action onSelect;

            public void OnSelect(BaseEventData eventData)
            {
                onSelect?.Invoke();
            }
        }

        bool CanAssign(MagicSlot slot, SpellId spellId)
        {
            if (spellId == SpellId.None) return true;
            var spell = GetSpellAsset(spellId);
            if (spell == null) return true;

            if (slot == MagicSlot.Special)
                return spell.slotType == SpellSlotType.SpecialOnly;

            return spell.slotType != SpellSlotType.SpecialOnly;
        }

        string ResolveName(SpellId id)
        {
            if (id == SpellId.None) return "Sin asignar";
            var spell = GetSpellAsset(id);
            return spell != null ? spell.displayName : id.ToString();
        }

        MagicSpellSO GetSpellAsset(SpellId id)
        {
            if (_library == null) return null;
            return _library.Get(id);
        }
    }
    [Serializable]
    class EquipmentBindings
    {
        public GameObject root;
        public List<RowBinding> rows = new();
        public Color rowSelectionColor = new Color(1f, 0.82f, 0.16f, 1f);

        public bool IsConfigured
        {
            get
            {
                if (root == null) return false;
                if (rows == null || rows.Count == 0) return false;
                foreach (var row in rows)
                {
                    if (row == null || row.label == null) return false;
                }
                return true;
            }
        }

        [Serializable]
        public class RowBinding
        {
            public PartCategory category;
            [Tooltip("Si está desactivado, esta categoría no se mostrará en el menú de equipo.")]
            public bool enabled = true;
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

        ModularAutoBuilder _builder;
        PlayerPresetService _presetService;
        WardrobeInventory _wardrobe;
        WardrobeInventory _boundWardrobe;

        public EquipmentView(EquipmentBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            if (_ui.rows != null)
            {
                foreach (var row in _ui.rows)
                {
                    if (row == null) continue;
                    _rows[row.category] = row;

                    var capturedCategory = row.category;
                    if (row.previousButton != null)
                        row.previousButton.onClick.AddListener(() => Cycle(capturedCategory, -1));
                    if (row.nextButton != null)
                        row.nextButton.onClick.AddListener(() => Cycle(capturedCategory, +1));
                    if (row.clearButton != null)
                        row.clearButton.onClick.AddListener(() => Clear(capturedCategory));
                    
                    // Ocultar fila si está deshabilitada
                    if (!row.enabled && row.label != null && row.label.transform.parent != null)
                        row.label.transform.parent.gameObject.SetActive(false);
                }
            }

            ConfigureNavigation();
        }

        public GameObject DefaultSelection
        {
            get
            {
                var ordered = GetOrderedRows();
                for (int i = 0; i < ordered.Count; i++)
                {
                    var row = ordered[i];
                    var button = GetDefaultButton(row);
                    if (button != null)
                        return button.gameObject;
                }
                return null;
            }
        }

        public void SetVisible(bool value)
        {
            if (_ui.root != null)
                _ui.root.SetActive(value);
            
            // Ocultar filas deshabilitadas
            if (value)
            {
                foreach (var row in _ui.rows)
                {
                    if (row == null) continue;
                    bool visible = row.enabled;
                    if (row.label != null && row.label.transform.parent != null)
                        row.label.transform.parent.gameObject.SetActive(visible);
                }
            }
        }

        public void Refresh()
        {
            PlayerService.TryGetComponent(out _builder, includeInactive: true, allowSceneLookup: true);
            PlayerService.TryGetComponent(out _presetService, includeInactive: true, allowSceneLookup: true);
            PlayerService.TryGetComponent(out _wardrobe, includeInactive: true, allowSceneLookup: true);

            if (_boundWardrobe != _wardrobe)
            {
                if (_boundWardrobe != null)
                    _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
                if (_wardrobe != null)
                    _wardrobe.OnWardrobeChanged += HandleWardrobeChanged;
                _boundWardrobe = _wardrobe;
            }

            if (_builder == null)
            {
                foreach (var row in _rows.Values)
                {
                    if (row?.label != null)
                        row.label.text = $"{FormatCategory(row.category)}: (sin builder)";
                    SetInteractable(row, false, false);
                }
                return;
            }

            foreach (var kvp in _rows)
            {
                bool hasOptions = _wardrobe == null || _wardrobe.HasOptions(kvp.Key);
                bool canCycle = _wardrobe == null ? _builder != null : hasOptions;
                bool allowClear = _wardrobe == null ? _builder != null : hasOptions;
                SetInteractable(kvp.Value, canCycle, allowClear);
            }

            UpdateLabels();
            _rowOrderDirty = true;
            ConfigureNavigation();
            // Limpiar selección visual en filas de vestuario
            foreach (var row in _rows.Values)
            {
                if (row?.label != null)
                    row.label.color = Color.white;
            }
        }

        void ConfigureNavigation()
        {
            var ordered = GetOrderedRows();
            if (ordered.Count == 0) return;

            for (int rowIndex = 0; rowIndex < ordered.Count; rowIndex++)
            {
                var row = ordered[rowIndex];
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
            _presetService?.ApplyCurrentPreset(includeInventory: false);
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
            var options = _wardrobe?.GetUnlockedOptions(category);
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

            if (options.Count == 0) return false;

            int nextIndex = (currentIndex + step) % options.Count;
            if (nextIndex < 0) nextIndex += options.Count;

            var entry = options[nextIndex];
            if (string.IsNullOrEmpty(entry.partName)) return false;

            InvokeBuilderSetByName(category, entry.partName);
            return true;
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
            if (string.IsNullOrEmpty(partName)) return "Sin asignar";
            if (_wardrobe != null && _wardrobe.TryGetEntry(category, partName, out var entry))
                return string.IsNullOrEmpty(entry.displayName) ? partName : entry.displayName;
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
                _boundWardrobe.OnWardrobeChanged -= HandleWardrobeChanged;
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

        // Invoca Next/Prev en el builder usando reflexión para evitar usar PartCat directamente
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
            return t != null ? t.position.y : float.MinValue;
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
        }
    }
}
