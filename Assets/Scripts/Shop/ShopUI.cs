using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
            PlayerService.TryGetComponent(out shopController, includeInactive: true, allowSceneLookup: true);
        RefreshUI();
        SelectFirstItem();
        
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        
        if (windowRoot != null)
            windowRoot.SetActive(false);
        
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
        
        if (shopController == null || itemCardPrefab == null || itemListContainer == null)
            return;
        
        // Crear cards para cada item en stock
        for (int i = 0; i < shopController.Stock.Count; i++)
        {
            var entry = shopController.Stock[i];
            if (entry == null || entry.item == null)
                continue;
            
            var cardObj = Instantiate(itemCardPrefab, itemListContainer);
            var card = cardObj.GetComponent<ShopItemCard>();
            
            if (card != null)
            {
                int index = i;
                card.Setup(entry, index, () => SelectItem(index));
                _itemCards.Add(card);
            }
        }
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
        if (shopController != null && shopController.Stock.Count > 0)
        {
            SelectItem(0);
            
            // Auto-seleccionar la primera card
            if (_itemCards.Count > 0 && _itemCards[0] != null)
            {
                var selectable = _itemCards[0].GetComponent<Selectable>();
                if (selectable != null)
                {
                    EventSystem.current?.SetSelectedGameObject(selectable.gameObject);
                    selectable.Select();
                }
            }
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

    void Update()
    {
        if (!_isOpen) return;
        
        // Cerrar con ESC o botón B del gamepad
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel"))
        {
            Close();
        }
    }
}

/// <summary>
/// Componente para cada card de item en la lista de la tienda.
/// </summary>
public class ShopItemCard : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text priceText;
    [SerializeField] private Text stockText;
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.82f, 0.16f, 1f);
    
    private System.Action _onSelect;

    void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => _onSelect?.Invoke());
    }

    public void Setup(ShopController.ShopItemEntry entry, int index, System.Action onSelect)
    {
        _onSelect = onSelect;
        
        if (entry == null || entry.item == null)
            return;
        
        var item = entry.item;
        
        if (iconImage != null)
            iconImage.sprite = item.icon;
        
        if (nameText != null)
            nameText.text = item.displayName;
        
        if (priceText != null)
            priceText.text = $"{entry.GetBuyPrice()} 💰";
        
        if (stockText != null)
        {
            if (entry.limitedStock)
                stockText.text = entry.HasStock ? "Disponible" : "Agotado";
            else
                stockText.text = "";
        }
        
        if (button != null)
            button.interactable = entry.HasStock;
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}
