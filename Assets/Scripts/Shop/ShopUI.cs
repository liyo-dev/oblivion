using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// UI genérica para tiendas. Se conecta a un ShopController y muestra sus items.
/// Reutilizable para múltiples tiendas.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private ShopController shopController;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private Transform itemListContainer;
    [SerializeField] private GameObject itemCardPrefab;
    [SerializeField] private Text currencyText;
    [SerializeField] private Button closeButton;
    
    [Header("Item Detail Panel")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Image detailIcon;
    [SerializeField] private Text detailName;
    [SerializeField] private Text detailDescription;
    [SerializeField] private Text detailPrice;
    [SerializeField] private Text detailStock;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Text messageText;
    [Header("Confirmación de compra")]
    [SerializeField] private GameObject confirmPopupRoot;
    [SerializeField] private Text confirmPopupText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;
    [SerializeField] private string confirmMessage = "¿Estás seguro?";
    [SerializeField] private float buyButtonPulseScale = 1.08f;
    [SerializeField] private float buyButtonPulseDuration = 0.14f;
    [SerializeField] private Ease buyButtonPulseEase = Ease.OutBack;

    private List<ShopItemCard> _itemCards = new();
    private ShopController.ShopItemEntry _selectedEntry;
    private int _selectedIndex = -1;
    private bool _isOpen;
    private Inventory _playerInventory;
    private float _navCooldown;
    private const float NAV_REPEAT_DELAY = 0.18f;
    private Tween _buyButtonTween;
    private Vector3 _buyButtonBaseScale;
    private bool _buyButtonScaleCached;

    enum ShopState
    {
        Browsing,
        BuyButtonFocused,
        Confirming
    }

    // Public accessor for MenuManager / external callers
    public bool IsOpen => _isOpen;

    ShopState _state = ShopState.Browsing;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(HandleBuyButtonPressed);
        }

        if (sellButton != null)
            sellButton.onClick.AddListener(OnSellClicked);

        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.RemoveAllListeners();
            confirmYesButton.onClick.AddListener(ConfirmPurchase);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.RemoveAllListeners();
            confirmNoButton.onClick.AddListener(CancelConfirmation);
        }

        if (windowRoot != null)
            windowRoot.SetActive(false);

        if (detailPanel != null)
            detailPanel.SetActive(false);

        if (confirmPopupRoot != null)
            confirmPopupRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (shopController != null)
            shopController.OnStockChanged += RefreshUI;
    }

    void OnDisable()
    {
        if (shopController != null)
            shopController.OnStockChanged -= RefreshUI;
    }

    void Update()
    {
        if (!_isOpen) return;

        _navCooldown -= Time.unscaledDeltaTime;

        if (_navCooldown <= 0f && _state != ShopState.Confirming)
        {
            int move = ReadVerticalInput();
            if (move != 0)
            {
                NavigateItems(move);
                _navCooldown = NAV_REPEAT_DELAY;
            }

            int horizontal = ReadHorizontalInput();
            if (horizontal > 0)
                HandleSubmitInput();
            else if (horizontal < 0 && _state == ShopState.BuyButtonFocused)
                ReturnToItemList();
        }

        if (ReadSubmitInput())
            HandleSubmitInput();

        if (ReadCancelInput())
            HandleCancelInput();
    }
    
    int ReadVerticalInput()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.up.wasPressedThisFrame || gp.leftStick.up.wasPressedThisFrame) return -1;
            if (gp.dpad.down.wasPressedThisFrame || gp.leftStick.down.wasPressedThisFrame) return +1;
        }
#endif
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return -1;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return +1;
        return 0;
    }

    bool ReadSubmitInput()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null && gp.buttonSouth.wasPressedThisFrame) return true;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
    }

    bool ReadCancelInput()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
            return gp.buttonEast.wasPressedThisFrame || gp.startButton.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel");
    }

    int ReadHorizontalInput()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame) return 1;
            if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame) return -1;
        }
#endif
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return 1;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return -1;
        return 0;
    }

    void HandleSubmitInput()
    {
        switch (_state)
        {
            case ShopState.Browsing:
                FocusBuyButton();
                break;
            case ShopState.BuyButtonFocused:
                ShowConfirmation();
                break;
            case ShopState.Confirming:
                ConfirmPurchase();
                break;
        }
    }

    void HandleCancelInput()
    {
        if (_state == ShopState.Confirming)
        {
            CancelConfirmation();
            return;
        }

        if (_state == ShopState.BuyButtonFocused)
        {
            ReturnToItemList();
            return;
        }

        Close();
    }
    
    void NavigateItems(int direction)
    {
        if (_itemCards.Count == 0) return;
        if (_state != ShopState.Browsing) return;

        int newIndex = _selectedIndex + direction;

        // Wrap around
        if (newIndex < 0) newIndex = _itemCards.Count - 1;
        if (newIndex >= _itemCards.Count) newIndex = 0;

        SelectItem(newIndex);
    }

    void Start()
    {
        if (_playerInventory == null)
            PlayerService.TryGetComponent(out _playerInventory, includeInactive: true, allowSceneLookup: true);
    }

    public void BindController(ShopController controller)
    {
        if (shopController == controller) return;
        if (shopController != null)
            shopController.OnStockChanged -= RefreshUI;
        shopController = controller;
        if (shopController != null)
            shopController.OnStockChanged += RefreshUI;
        RefreshUI();
    }

    public void Open()
    {
        if (_isOpen) return;
        // Ask central MenuManager for permission
        if (!MenuManager.TryOpen(MenuKind.Shop))
        {
            Debug.Log("[ShopUI] Apertura denegada por MenuManager");
            return;
        }
        _isOpen = true;
        _state = ShopState.Browsing;
        HideConfirmationVisuals();
        ResetBuyButtonFeedback();

        if (windowRoot != null)
            windowRoot.SetActive(true);
        
        if (shopController == null)
        {
            shopController = GetComponent<ShopController>();
            if (shopController == null)
                shopController = GetComponentInChildren<ShopController>();
        }
        
        if (shopController == null)
        {
            Debug.LogError("[ShopUI] No se encontró ShopController en este GameObject ni en sus hijos.");
        }
        
        RefreshUI();
        // No seleccionar ningún producto al abrir
        ClearSelection();

        GameState.Push(GamePhase.Shop);
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        
        if (windowRoot != null)
            windowRoot.SetActive(false);

        GameState.Pop(GamePhase.Shop);
        Time.timeScale = 1f;
        ResetBuyButtonFeedback();
        HideConfirmationVisuals();

        // Unregister from central manager
        MenuManager.Close(MenuKind.Shop);
    }

    void RefreshUI()
    {
        if (shopController == null)
        {
            ClearSelection();
            UpdateCurrencyDisplay();
            return;
        }

        UpdateCurrencyDisplay();
        RebuildItemList();
        
        if (_selectedIndex >= 0 && _selectedIndex < shopController.Stock.Count)
            SelectItem(_selectedIndex);
        else
            ClearSelection();
    }

    void UpdateCurrencyDisplay()
    {
        if (currencyText == null || _playerInventory == null || shopController == null)
            return;
        
        // Asume que el ShopController tiene referencia a currencyItem
        var currencyItemField = shopController.GetType().GetField("currencyItem", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (currencyItemField != null)
        {
            var currencyItem = currencyItemField.GetValue(shopController) as ItemData;
            if (currencyItem != null)
            {
                int amount = _playerInventory.Count(currencyItem.itemId);
                currencyText.text = $"💰 {amount}";
                return;
            }
        }
        
        currencyText.text = "💰 0";
    }

    void RebuildItemList()
    {
        // Limpiar cards existentes
        foreach (var card in _itemCards)
        {
            if (card != null && card.gameObject != null)
                Destroy(card.gameObject);
        }
        _itemCards.Clear();
        
        if (shopController == null)
        {
            Debug.LogError("[ShopUI] RebuildItemList: shopController es NULL");
            return;
        }
        
        if (itemCardPrefab == null)
        {
            Debug.LogError("[ShopUI] RebuildItemList: itemCardPrefab es NULL");
            return;
        }
        
        if (itemListContainer == null)
        {
            Debug.LogError("[ShopUI] RebuildItemList: itemListContainer es NULL");
            return;
        }
        
        Debug.Log($"[ShopUI] RebuildItemList: Stock tiene {shopController.Stock.Count} items");
        
        // Crear cards para cada item en stock
        for (int i = 0; i < shopController.Stock.Count; i++)
        {
            var entry = shopController.Stock[i];
            if (entry == null || entry.item == null)
            {
                Debug.LogWarning($"[ShopUI] Item en índice {i} es null o no tiene ItemData");
                continue;
            }
            
            Debug.Log($"[ShopUI] Creando card para item: {entry.item.displayName}");
            var cardObj = Instantiate(itemCardPrefab, itemListContainer);
            var card = cardObj.GetComponent<ShopItemCard>();
            
            if (card != null)
            {
                int index = i;
                card.Setup(entry, index, () => SelectItem(index));
                _itemCards.Add(card);
            }
            else
            {
                Debug.LogError($"[ShopUI] itemCardPrefab no tiene componente ShopItemCard");
            }
        }
        
        Debug.Log($"[ShopUI] Se crearon {_itemCards.Count} cards en total");
    }

    void SelectItem(int index)
    {
        if (index < 0 || index >= shopController.Stock.Count)
            return;

        _selectedIndex = index;
        _selectedEntry = shopController.Stock[index];
        
        if (detailPanel != null)
            detailPanel.SetActive(true);
        
        UpdateDetailPanel();

        // Resaltar card seleccionada
        for (int i = 0; i < _itemCards.Count; i++)
        {
            if (_itemCards[i] != null)
                _itemCards[i].SetSelected(i == index);
        }

        if (_state == ShopState.Browsing)
            FocusSelectedCard();
    }

    void SelectFirstItem()
    {
        if (shopController != null && shopController.Stock.Count > 0 && _itemCards.Count > 0)
        {
            SelectItem(0);
        }
    }

    void FocusSelectedCard()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _itemCards.Count) return;
        var button = _itemCards[_selectedIndex]?.GetButton();
        if (button == null) return;
        var es = EventSystem.current;
        if (es != null)
        {
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(button.gameObject);
        }
        button.Select();
    }

    void UpdateDetailPanel()
    {
        if (_selectedEntry == null || _selectedEntry.item == null)
        {
            ClearSelection();
            return;
        }
        
        var item = _selectedEntry.item;
        
        if (detailIcon != null)
            detailIcon.sprite = item.icon;
        
        if (detailName != null)
            detailName.text = item.displayName;
        
        if (detailDescription != null)
            detailDescription.text = item.useDescription;
        
        if (detailPrice != null)
        {
            int price = _selectedEntry.GetBuyPrice();
            detailPrice.text = $"Precio: {price} 💰";
        }
        
        if (detailStock != null)
        {
            if (_selectedEntry.limitedStock)
                detailStock.text = _selectedEntry.HasStock ? "En stock" : "Agotado";
            else
                detailStock.text = "Stock ilimitado";
        }
        
        if (buyButton != null)
            buyButton.interactable = _selectedEntry.HasStock;
        
        // Por ahora deshabilitamos venta (se puede implementar después)
        if (sellButton != null)
            sellButton.gameObject.SetActive(false);
        
        if (messageText != null)
            messageText.text = "";
    }

    void ClearSelection()
    {
        _selectedIndex = -1;
        _selectedEntry = null;
        
        if (detailPanel != null)
            detailPanel.SetActive(false);
        
        foreach (var card in _itemCards)
        {
            if (card != null)
                card.SetSelected(false);
        }
    }

    void OnBuyClicked()
    {
        if (_selectedIndex < 0 || shopController == null)
            return;
        
        bool success = shopController.TryBuy(_selectedIndex, out string message);
        
        if (messageText != null)
        {
            messageText.text = message ?? (success ? "¡Comprado!" : "Error");
            messageText.color = success ? Color.green : Color.red;
        }
        
        if (success)
        {
            RefreshUI();
        }
    }

    void OnSellClicked()
    {
        // TODO: Implementar venta de items del inventario
        if (messageText != null)
            messageText.text = "Función de venta no implementada aún.";
    }

    void HandleBuyButtonPressed()
    {
        if (_state == ShopState.Browsing)
        {
            FocusBuyButton();
            return;
        }

        if (_state == ShopState.BuyButtonFocused)
        {
            ShowConfirmation();
        }
    }

    void FocusBuyButton()
    {
        if (buyButton == null) return;
        _state = ShopState.BuyButtonFocused;
        var es = EventSystem.current;
        if (es != null)
        {
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(buyButton.gameObject);
        }
        buyButton.Select();
        PlayBuyButtonFeedback();
    }

    void ReturnToItemList()
    {
        _state = ShopState.Browsing;
        ResetBuyButtonFeedback();
        FocusSelectedCard();
    }

    void PlayBuyButtonFeedback()
    {
        if (buyButton == null) return;

        if (!_buyButtonScaleCached)
        {
            _buyButtonBaseScale = buyButton.transform.localScale;
            _buyButtonScaleCached = true;
        }

        // Solo escalar el botón una vez y mantener color verde
        buyButton.transform.localScale = _buyButtonBaseScale * buyButtonPulseScale;
        var colors = buyButton.colors;
        colors.normalColor = Color.green;
        colors.highlightedColor = Color.green;
        colors.pressedColor = Color.green;
        buyButton.colors = colors;
    }

    void ResetBuyButtonFeedback()
    {
        _buyButtonTween?.Kill();
        _buyButtonTween = null;
        if (buyButton != null && _buyButtonScaleCached)
            buyButton.transform.localScale = _buyButtonBaseScale;
    }

    void ShowConfirmation()
    {
        if (_selectedEntry == null || _selectedEntry.item == null)
            return;

        _state = ShopState.Confirming;
        ResetBuyButtonFeedback();

        string itemName = _selectedEntry.item.displayName;
        int price = _selectedEntry.GetBuyPrice();
        string message = $"{confirmMessage}\nComprar {itemName} por {price} 💰?";

        if (confirmPopupText != null)
            confirmPopupText.text = message;

        if (confirmPopupRoot != null)
            confirmPopupRoot.SetActive(true);

        if (confirmYesButton != null)
        {
            var es = EventSystem.current;
            if (es != null)
                es.SetSelectedGameObject(confirmYesButton.gameObject);
            confirmYesButton.Select();
        }
    }

    void HideConfirmationVisuals()
    {
        if (confirmPopupRoot != null)
            confirmPopupRoot.SetActive(false);
    }

    void CancelConfirmation()
    {
        HideConfirmationVisuals();
        _state = ShopState.BuyButtonFocused;
        FocusBuyButton();
    }

    void ConfirmPurchase()
    {
        if (_selectedIndex < 0 || shopController == null)
        {
            CancelConfirmation();
            return;
        }

        HideConfirmationVisuals();
        var success = shopController.TryBuy(_selectedIndex, out string message);

        if (messageText != null)
        {
            messageText.text = message ?? (success ? "¡Comprado!" : "Error");
            messageText.color = success ? Color.green : Color.red;
        }

        _state = ShopState.Browsing;
        RefreshUI();
        if (_itemCards.Count == 0)
        {
            _selectedIndex = -1;
        }
        else
        {
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _itemCards.Count - 1);
            FocusSelectedCard();
        }
    }
}
