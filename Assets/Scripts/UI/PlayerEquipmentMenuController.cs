using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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

    bool _isOpen;
    int _activeTab;
    float _savedTimeScale = 1f;
    float _lastDpadVertical;

    bool _warnedInventory;
    bool _warnedSpells;
    bool _warnedEquipment;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;

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
            return;
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

        HandleToggleInput();

        if (_isOpen)
        {
            HandleCloseInput();
            HandleTabNavigationInput();
            UpdatePlayerInfoPanel();
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
            CloseMenu();
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

        if (cancel && _activeTab == 1 && _spellView != null && _spellView.TryHandleCancel())
            return;

        if (cancel)
            CloseMenu();
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
        if (!EnsureViews()) return;

        EnsureEventSystem();

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
    }

    void CloseMenu()
    {
        SetCanvasState(false);
        _spellView?.CancelSlotSelection(true);
        Time.timeScale = _savedTimeScale;
        _isOpen = false;
        if (GameState.Is(GamePhase.Inventory)) GameState.Pop(GamePhase.Inventory);
        if (GameState.Is(GamePhase.Equipment)) GameState.Pop(GamePhase.Equipment);
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
        _activeTab = Mathf.Clamp(index, 0, 2);

        if (_spellView != null && _activeTab != 1)
            _spellView.CancelSlotSelection(true);

        if (_inventoryView != null)
        {
            _inventoryView.SetVisible(_activeTab == 0);
            if (_activeTab == 0) _inventoryView.Refresh(forceRebuild);
        }

        if (_spellView != null)
        {
            _spellView.SetVisible(_activeTab == 1);
            if (_activeTab == 1) _spellView.Refresh();
        }

        if (_equipmentView != null)
        {
            _equipmentView.SetVisible(_activeTab == 2);
            if (_activeTab == 2) _equipmentView.Refresh();
        }

        UpdateTabButtonStates();
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
        if (levelText == null && hpText == null && mpText == null) return;

        PlayerPresetSO preset = null;
        if (GameBootService.IsAvailable && GameBootService.Profile != null)
            preset = GameBootService.Profile.GetActivePresetResolved();

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

        var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(esGO);
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

            if (_selectedItem == null && _rows.Count > 0)
            {
                _rows[0].InvokeClick();
            }
            else
            {
                UpdateSelectedItemDetails();
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

                widget.RegisterClickHandler(() =>
                {
                    _selectedItem = entry.item;
                    UpdateSelectedItemDetails();
                });

                _rows.Add(widget);
            }

            if (_rows.Count == 0)
                UpdateEmptyState("Inventario vacío");
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
    }

    [Serializable]
    class SpellBindings
    {
        public GameObject root;
        public Button leftSlotButton;
        public Text leftSlotLabel;
        public Button rightSlotButton;
        public Text rightSlotLabel;
        public Button specialSlotButton;
        public Text specialSlotLabel;
        public Transform rowsParent;
        public SpellRowWidget rowPrefab;
        public Text detailsText;
        [Header("Feedback visual")]
        public Color slotSelectionColor = new Color(1f, 0.83f, 0.2f, 1f);

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
        readonly SpellBindings _ui;
        readonly List<RowEntry> _rows = new();
        readonly Dictionary<Button, ColorBlock> _slotDefaultColors = new();
        readonly Dictionary<Button, Navigation> _slotNavigation = new();

        PlayerPresetSO _preset;
        SpellLibrarySO _library;
        PlayerPresetService _presetService;
        SpellId _highlightedSpell = SpellId.None;
        RowEntry _highlightedRow;
        SpellId _pendingSpell = SpellId.None;
        bool _isSelectingSlot;
        GameObject _currentSlotSelection;

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

            SetSlotNavigationActive(false);
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
                return _ui.leftSlotButton != null ? _ui.leftSlotButton.gameObject : _ui.root;
            }
        }

        public void SetVisible(bool value)
        {
            if (!value)
                CancelSlotSelection(true);
            if (_ui.root != null)
                _ui.root.SetActive(value);
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

            UpdateSlotLabels();
            BuildSpellList();
            UpdateSlotButtonVisuals();
        }

        void UpdateSlotLabels()
        {
            if (_preset == null)
            {
                _ui.leftSlotLabel.text = "Izquierda: --";
                _ui.rightSlotLabel.text = "Derecha: --";
                _ui.specialSlotLabel.text = "Especial: --";
                return;
            }

            _ui.leftSlotLabel.text = $"Izquierda: {ResolveName(_preset.leftSpellId)}";
            _ui.rightSlotLabel.text = $"Derecha: {ResolveName(_preset.rightSpellId)}";
            _ui.specialSlotLabel.text = $"Especial: {ResolveName(_preset.specialSpellId)}";
        }

        void BuildSpellList()
        {
            var previous = _highlightedSpell;
            ClearList();

            if (_preset == null) return;

            var added = new HashSet<SpellId>();
            AddSpellRow(SpellId.None);
            added.Add(SpellId.None);

            if (_preset.unlockedSpells != null)
            {
                foreach (var id in _preset.unlockedSpells)
                {
                    if (!added.Add(id)) continue;
                    AddSpellRow(id);
                }
            }

            if (!SelectRow(previous))
            {
                if (!SelectRow(_preset.leftSpellId))
                    SelectFirstRow();
            }
        }

        void AddSpellRow(SpellId spellId)
        {
            var widget = UnityEngine.Object.Instantiate(_ui.rowPrefab, _ui.rowsParent);
            widget.SetLabel(ResolveName(spellId));
            var rowEntry = new RowEntry { spellId = spellId, widget = widget };
            widget.RegisterClickHandler(() => HandleRowClicked(rowEntry));
            widget.RegisterSelectedHandler(() => HandleRowSelected(rowEntry));

            _rows.Add(rowEntry);
        }

        void HandleRowSelected(RowEntry entry)
        {
            if (entry == null) return;
            if (_isSelectingSlot)
            {
                RestoreSlotFocus();
                return;
            }
            _highlightedSpell = entry.spellId;
            _highlightedRow = entry;
            ShowSpellDetails(entry.spellId);
        }

        void HandleRowClicked(RowEntry entry)
        {
            if (entry == null) return;
            if (_isSelectingSlot) return;

            BeginSlotSelection(entry.spellId);
        }

        void BeginSlotSelection(SpellId spellId)
        {
            _pendingSpell = spellId;
            _isSelectingSlot = true;
            SetSlotNavigationActive(true);
            UpdateSlotButtonVisuals();

            if (!FocusFirstAllowedSlot())
            {
                CancelSlotSelection(false);
                return;
            }

            ShowSpellDetails(spellId);
        }

        bool FocusFirstAllowedSlot()
        {
            if (TryFocusSlot(_ui.leftSlotButton, MagicSlot.Left)) return true;
            if (TryFocusSlot(_ui.rightSlotButton, MagicSlot.Right)) return true;
            if (TryFocusSlot(_ui.specialSlotButton, MagicSlot.Special)) return true;
            return false;
        }

        bool TryFocusSlot(Button button, MagicSlot slot)
        {
            if (button == null) return false;
            if (!CanAssign(slot, _pendingSpell)) return false;
            var es = EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(button.gameObject);
            _currentSlotSelection = button.gameObject;
            return true;
        }

        void HandleSlotButtonPressed(MagicSlot slot)
        {
            if (!_isSelectingSlot) return;
            if (!CanAssign(slot, _pendingSpell)) return;

            AssignSpellToSlot(slot, _pendingSpell);
            FinishSlotSelection();
        }

        void AssignSpellToSlot(MagicSlot slot, SpellId id)
        {
            if (_preset == null) return;

            switch (slot)
            {
                case MagicSlot.Left: _preset.leftSpellId = id; break;
                case MagicSlot.Right: _preset.rightSpellId = id; break;
                case MagicSlot.Special: _preset.specialSpellId = id; break;
            }

            _presetService?.ApplyCurrentPreset();
            UpdateSlotLabels();
        }

        void FinishSlotSelection()
        {
            _isSelectingSlot = false;
            _pendingSpell = SpellId.None;
            SetSlotNavigationActive(false);
            UpdateSlotButtonVisuals();
            _currentSlotSelection = null;
            _highlightedRow?.widget?.Focus();
            ShowSpellDetails(_highlightedSpell);
        }

        public void CancelSlotSelection(bool silent)
        {
            if (!_isSelectingSlot)
                return;

            _isSelectingSlot = false;
            _pendingSpell = SpellId.None;
            SetSlotNavigationActive(false);
            UpdateSlotButtonVisuals();
            _currentSlotSelection = null;
            if (!silent)
            {
                _highlightedRow?.widget?.Focus();
                ShowSpellDetails(_highlightedSpell);
            }
        }

        public bool TryHandleCancel()
        {
            if (!_isSelectingSlot) return false;
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

            if (_isSelectingSlot)
                description += "\nSelecciona un slot con A o cancela con B.";
            else if (id == SpellId.None)
                description += "\nPulsa A para limpiar un slot.";
            else
                description += "\nPulsa A para elegir en qué slot equipar.";

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
                entry.widget?.Focus();
                HandleRowSelected(entry);
                return true;
            }

            return false;
        }

        void SelectFirstRow()
        {
            if (_rows.Count == 0) return;
            var first = _rows[0];
            first?.widget?.Focus();
            HandleRowSelected(first);
        }

        void RestoreSlotFocus()
        {
            if (_currentSlotSelection == null) return;
            var es = EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(_currentSlotSelection);
        }

        void ConfigureSlotButton(Button button, MagicSlot slot)
        {
            if (button == null) return;

            button.onClick.AddListener(() => HandleSlotButtonPressed(slot));

            if (!_slotDefaultColors.ContainsKey(button))
                _slotDefaultColors[button] = button.colors;

            if (!_slotNavigation.ContainsKey(button))
                _slotNavigation[button] = button.navigation;
        }

        void SetSlotNavigationActive(bool active)
        {
            foreach (var kvp in _slotNavigation)
            {
                var button = kvp.Key;
                if (button == null) continue;
                button.navigation = active ? kvp.Value : new Navigation { mode = Navigation.Mode.None };
            }
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

            if (_isSelectingSlot && CanAssign(slot, _pendingSpell))
            {
                var highlighted = defaults;
                highlighted.normalColor = _ui.slotSelectionColor;
                highlighted.highlightedColor = _ui.slotSelectionColor;
                highlighted.selectedColor = _ui.slotSelectionColor;
                highlighted.pressedColor = _ui.slotSelectionColor;
                button.colors = highlighted;
            }
            else
            {
                button.colors = defaults;
            }

            button.interactable = !_isSelectingSlot || CanAssign(slot, _pendingSpell);
        }

        bool CanAssign(MagicSlot slot, SpellId spellId)
        {
            if (spellId == SpellId.None) return true;
            var spell = GetSpellAsset(spellId);
            if (spell == null) return true;

            if (slot == MagicSlot.Special)
                return spell.slotType == SpellSlotType.SpecialOnly || spell.slotType == SpellSlotType.Any;

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
                }
            }
        }

        public GameObject DefaultSelection
        {
            get
            {
                foreach (var row in _ui.rows)
                {
                    if (row?.previousButton != null) return row.previousButton.gameObject;
                    if (row?.nextButton != null) return row.nextButton.gameObject;
                }
                return null;
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
                SetInteractable(kvp.Value, hasOptions, true);
            }

            UpdateLabels();
        }

        void Cycle(PartCategory category, int step)
        {
            if (_builder == null) return;

            bool changed = false;

            if (_wardrobe != null)
                changed = TryCycleWithWardrobe(category, step);

            if (!changed)
                InvokeBuilderNextPrev(category, step);

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
            var method = type.GetMethod("Next", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method == null) method = type.GetMethod("Next", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null) return;
            var paramType = method.GetParameters()[0].ParameterType;
            try
            {
                var enumVal = System.Enum.Parse(paramType, category.ToString());
                method.Invoke(_builder, new object[] { enumVal, 1 });
                if (step < 0)
                {
                    // call Prev if exists
                    var prevMethod = type.GetMethod("Prev", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (prevMethod == null) prevMethod = type.GetMethod("Prev", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prevMethod != null)
                    {
                        prevMethod.Invoke(_builder, new object[] { enumVal });
                        return;
                    }
                    // fallback: call Next with negative step via a Prev-like approach
                    var nextMethod = method;
                    // try Next with step parameter if exists
                    var parms = method.GetParameters();
                    if (parms.Length == 2)
                    {
                        nextMethod.Invoke(_builder, new object[] { enumVal, step });
                    }
                }
            }
            catch (System.Exception) { }
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
    }
}
