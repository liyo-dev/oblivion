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
            CloseMenu();
        else
            OpenMenu();
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
            CloseMenu();
    }

    void OpenMenu()
    {
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
        SelectInitial();
    }

    void CloseMenu()
    {
        SetCanvasState(false);
        Time.timeScale = _savedTimeScale;
        _isOpen = false;
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

            if (!InventoryUseUtility.TryUseItem(_inventory, _selectedItem, _collector, out var reason, out var consumed))
            {
                if (_ui.feedbackText != null)
                    _ui.feedbackText.text = reason;
                return;
            }

            if (_ui.feedbackText != null)
                _ui.feedbackText.text = "Usado correctamente.";

            if (consumed && _inventory.Count(_selectedItem.itemId) == 0)
                _selectedItem = null;

            Refresh(true);
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
        readonly List<SpellRowWidget> _rows = new();
        MagicSlot _activeSlot = MagicSlot.Left;

        PlayerPresetSO _preset;
        SpellLibrarySO _library;
        PlayerPresetService _presetService;

        public SpellView(SpellBindings bindings)
        {
            _ui = bindings;
            _ui.root?.SetActive(false);

            _ui.leftSlotButton.onClick.AddListener(() => SelectSlot(MagicSlot.Left));
            _ui.rightSlotButton.onClick.AddListener(() => SelectSlot(MagicSlot.Right));
            _ui.specialSlotButton.onClick.AddListener(() => SelectSlot(MagicSlot.Special));
        }

        public GameObject DefaultSelection => _ui.leftSlotButton != null ? _ui.leftSlotButton.gameObject : null;

        public void SetVisible(bool value)
        {
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
                ClearList();
                UpdateSlotLabels();
                return;
            }

            _preset = GameBootService.Profile.GetActivePresetResolved();
            PlayerService.TryGetComponent(out _presetService, includeInactive: true, allowSceneLookup: true);
            _library = _presetService != null ? _presetService.SpellLibrary : null;

            UpdateSlotLabels();
            BuildSpellList();
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

        void SelectSlot(MagicSlot slot)
        {
            _activeSlot = slot;
            BuildSpellList();
        }

        void BuildSpellList()
        {
            ClearList();

            if (_preset == null) return;

            AddSpellRow(SpellId.None);

            if (_preset.unlockedSpells != null)
            {
                foreach (var id in _preset.unlockedSpells)
                    AddSpellRow(id);
            }

            SpellId current = _activeSlot switch
            {
                MagicSlot.Left => _preset.leftSpellId,
                MagicSlot.Right => _preset.rightSpellId,
                MagicSlot.Special => _preset.specialSpellId,
                _ => SpellId.None
            };
            ShowSpellDetails(current);
        }

        void AddSpellRow(SpellId spellId)
        {
            if (!IsAllowed(spellId)) return;

            var widget = UnityEngine.Object.Instantiate(_ui.rowPrefab, _ui.rowsParent);
            widget.SetLabel(ResolveName(spellId));
            widget.RegisterClickHandler(() =>
            {
                AssignSpell(spellId);
                ShowSpellDetails(spellId);
            });

            _rows.Add(widget);
        }

        void AssignSpell(SpellId id)
        {
            if (_preset == null) return;

            switch (_activeSlot)
            {
                case MagicSlot.Left: _preset.leftSpellId = id; break;
                case MagicSlot.Right: _preset.rightSpellId = id; break;
                case MagicSlot.Special: _preset.specialSpellId = id; break;
            }

            _presetService?.ApplyCurrentPreset();
            UpdateSlotLabels();
        }

        void ShowSpellDetails(SpellId id)
        {
            if (_ui.detailsText == null) return;

            if (id == SpellId.None)
            {
                _ui.detailsText.text = "Sin asignar.";
                return;
            }

            var spell = GetSpellAsset(id);
            if (spell == null)
            {
                _ui.detailsText.text = "Hechizo sin información.";
                return;
            }

            _ui.detailsText.text = $"{spell.displayName}\nDaño: {spell.damage}\nCoste de maná: {spell.manaCost}\nCooldown: {spell.cooldown:F2}s";
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

        bool IsAllowed(SpellId id)
        {
            if (id == SpellId.None) return true;
            var spell = GetSpellAsset(id);
            if (spell == null) return true;

            return _activeSlot == MagicSlot.Special
                ? spell.slotType == SpellSlotType.SpecialOnly || spell.slotType == SpellSlotType.Any
                : spell.slotType != SpellSlotType.SpecialOnly;
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
            public PartCat category;
            public Text label;
            public Button previousButton;
            public Button nextButton;
            public Button clearButton;
        }
    }

    class EquipmentView
    {
        readonly EquipmentBindings _ui;
        readonly Dictionary<PartCat, EquipmentBindings.RowBinding> _rows = new();

        ModularAutoBuilder _builder;
        PlayerPresetService _presetService;

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

                    if (row.previousButton != null)
                        row.previousButton.onClick.AddListener(() => Cycle(row.category, -1));
                    if (row.nextButton != null)
                        row.nextButton.onClick.AddListener(() => Cycle(row.category, +1));
                    if (row.clearButton != null)
                        row.clearButton.onClick.AddListener(() => Clear(row.category));
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

            if (_builder == null)
            {
                foreach (var row in _rows.Values)
                {
                    if (row?.label != null)
                        row.label.text = $"{FormatCategory(row.category)}: (sin builder)";
                    SetInteractable(row, false);
                }
                return;
            }

            foreach (var row in _rows.Values)
                SetInteractable(row, true);

            UpdateLabels();
        }

        void Cycle(PartCat category, int step)
        {
            if (_builder == null) return;

            if (step >= 0)
                _builder.Next(category, 1);
            else
                _builder.Prev(category);

            Snapshot();
            UpdateLabels();
        }

        void Clear(PartCat category)
        {
            if (_builder == null) return;
            _builder.SetByName(category, null);
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

                string value = selection != null && selection.TryGetValue(kvp.Key, out var part)
                    ? part
                    : "Sin asignar";

                row.label.text = $"{FormatCategory(kvp.Key)}: {value}";
            }
        }

        void SetInteractable(EquipmentBindings.RowBinding row, bool value)
        {
            if (row == null) return;
            if (row.previousButton != null) row.previousButton.interactable = value;
            if (row.nextButton != null) row.nextButton.interactable = value;
            if (row.clearButton != null) row.clearButton.interactable = value;
        }

        string FormatCategory(PartCat cat)
        {
            return cat switch
            {
                PartCat.OHS => "Arma Mano Derecha",
                PartCat.Shield => "Escudo Mano Izquierda",
                PartCat.Bow => "Arco",
                PartCat.Body => "Cuerpo",
                PartCat.Cloak => "Capa",
                PartCat.Head => "Cabeza",
                PartCat.Hair => "Pelo",
                PartCat.Eyes => "Ojos",
                PartCat.Mouth => "Boca",
                PartCat.Hat => "Casco",
                PartCat.Eyebrow => "Ceja",
                PartCat.Accessory => "Accesorio",
                _ => cat.ToString()
            };
        }
    }
}
