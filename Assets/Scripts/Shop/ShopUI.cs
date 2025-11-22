using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
    
    private List<ShopItemCard> _itemCards = new();
    private ShopController.ShopItemEntry _selectedEntry;
    private int _selectedIndex = -1;
    private bool _isOpen;
    private Inventory _playerInventory;
    private float _navCooldown;
    private const float NAV_REPEAT_DELAY = 0.18f;

    void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuyClicked);
        
        if (sellButton != null)
            sellButton.onClick.AddListener(OnSellClicked);
        
        if (windowRoot != null)
            windowRoot.SetActive(false);
        
        if (detailPanel != null)
            detailPanel.SetActive(false);
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
        
        if (_navCooldown <= 0f)
        {
            int move = ReadVerticalInput();
            if (move != 0)
            {
                NavigateItems(move);
                _navCooldown = NAV_REPEAT_DELAY;
            }
        }
        
        if (ReadSubmitInput())
        {
            OnBuyClicked();
        }
        
        // Cerrar con ESC o botón B del gamepad
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel"))
        {
            Close();
        }
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
    
    void NavigateItems(int direction)
    {
        if (_itemCards.Count == 0) return;
        
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
        _isOpen = true;
        
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
        SelectFirstItem();
        
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
    }

    void SelectFirstItem()
    {
        if (shopController != null && shopController.Stock.Count > 0 && _itemCards.Count > 0)
        {
            SelectItem(0);
        }
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
}
