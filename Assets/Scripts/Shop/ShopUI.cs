using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
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
    [SerializeField] private float buyButtonPulseScale = 1.08f;
    [SerializeField] private float buyButtonPulseDuration = 0.14f;
    [SerializeField] private Ease buyButtonPulseEase = Ease.OutBack;
    [SerializeField] private Color successMessageColor = new Color(0f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color errorMessageColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private float currencyPunchScale = 1.08f;
    [SerializeField] private float currencyPunchDuration = 0.18f;
    [Header("Input")]
    [Tooltip("When the shop opens, ignore player input (submit/cancel/navigation) for this many seconds to avoid the 'close dialogue' button press from affecting the shop.")]
    [SerializeField, Min(0f)] private float openIgnoreInputDuration = 0.15f;

    private const float MinOpenInputBlock = 0.25f;
    private const float NavRepeatDelay = 0.18f;
    private static readonly Color ActiveBuyButtonColor = new Color(0.05f, 0.75f, 0.2f, 1f);

    private List<ShopItemCard> _itemCards = new();
    private ShopController.ShopItemEntry _selectedEntry;
    private int _selectedIndex = -1;
    private bool _isOpen;
    private Inventory _playerInventory;
    private float _nextNavTime;
    private float _ignoreInputUntil;
    private Tween _buyButtonTween;
    private Vector3 _buyButtonBaseScale;
    private bool _buyButtonScaleCached;
    private bool _buyButtonColorsCached;
    private ColorBlock _buyButtonDefaultColors;
    private Tween _currencyTween;

    // Variables de control de mensajes y estado
    private bool _skipMessageClearOnce;
    private bool _messageLocked;
    private bool _justFocusedBuyButton;
    
    enum ShopState
    {
        Browsing,
        BuyButtonFocused
    }

    // Public accessor for MenuManager / external callers
    public bool IsOpen => _isOpen;

    ShopState _state = ShopState.Browsing;

    void Awake()
    {
        EnsurePlayerInventory();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(HandleBuyButtonPressed);
            // Deshabilitar el botón de compra por defecto
            buyButton.interactable = false;
        }

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

        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        if (shopController != null)
            shopController.OnStockChanged -= RefreshUI;

        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    private void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (!_isOpen) return;
        if (Time.unscaledTime < _ignoreInputUntil) return;

        switch (input.Type)
        {
            case GamepadInputReader.InputEventType.Submit when input.Phase == InputActionPhase.Performed:
                HandleSubmitInput();
                break;
            case GamepadInputReader.InputEventType.Cancel when input.Phase == InputActionPhase.Performed:
            case GamepadInputReader.InputEventType.Start when input.Phase == InputActionPhase.Performed:
                HandleCancelInput();
                break;
            case GamepadInputReader.InputEventType.DpadUp:
                HandleVerticalNavigation(-1);
                break;
            case GamepadInputReader.InputEventType.DpadDown:
                HandleVerticalNavigation(+1);
                break;
            case GamepadInputReader.InputEventType.DpadLeft:
                HandleHorizontalNavigation(-1);
                break;
            case GamepadInputReader.InputEventType.DpadRight:
                HandleHorizontalNavigation(+1);
                break;
            case GamepadInputReader.InputEventType.Navigate when input.Phase == InputActionPhase.Performed:
                HandleNavigateVector(input.Value);
                break;
        }
    }

    private bool CanNavigate() => Time.unscaledTime >= _nextNavTime;

    private void HandleNavigateVector(Vector2 value)
    {
        if (!CanNavigate()) return;

        if (value.y > 0.6f) HandleVerticalNavigation(-1);
        else if (value.y < -0.6f) HandleVerticalNavigation(+1);

        if (value.x > 0.6f) HandleHorizontalNavigation(1);
        else if (value.x < -0.6f) HandleHorizontalNavigation(-1);
    }

    private void HandleVerticalNavigation(int direction)
    {
        if (!CanNavigate()) return;

        NavigateItems(direction);
        _nextNavTime = Time.unscaledTime + NavRepeatDelay;
    }

    private void HandleHorizontalNavigation(int direction)
    {
        if (!CanNavigate()) return;

        if (direction > 0)
            HandleSubmitInput();
        else if (direction < 0 && _state == ShopState.BuyButtonFocused)
            ReturnToItemList();

        _nextNavTime = Time.unscaledTime + NavRepeatDelay;
    }

    void HandleSubmitInput()
    {
        // Si acabamos de enfocar el botón, ignorar este input
        if (_justFocusedBuyButton)
        {
            return;
        }

        switch (_state)
        {
            case ShopState.Browsing:
                // Solo permitir enfocar el botón si hay un ítem seleccionado y tiene stock
                if (_selectedEntry != null && _selectedEntry.HasStock)
                {
                    FocusBuyButton();
                }
                break;
            case ShopState.BuyButtonFocused:
                AttemptPurchase();
                break;
        }
    }

    void HandleCancelInput()
    {
        if (_state == ShopState.BuyButtonFocused)
        {
            ReturnToItemList();
            // Evitar que la misma pulsación cierre la tienda inmediatamente
            _ignoreInputUntil = Time.unscaledTime + 0.12f;
            return;
        }

        // Reproducir sonido de cancelar/cerrar menú
        GamepadInputReader.PlayUISound("UI_Cancel");
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
        EnsurePlayerInventory();
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
        _justFocusedBuyButton = false;
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

        // Ignore input for a short moment so the button used to close dialogue doesn't propagate into the shop UI.
        float ignoreDuration = Mathf.Max(openIgnoreInputDuration, MinOpenInputBlock);
        _ignoreInputUntil = Time.unscaledTime + ignoreDuration;

        GameState.Push(GamePhase.Shop);
        Time.timeScale = 0f;
        
        // IMPORTANTE: Suprimir inputs de gameplay mientras la tienda está abierta
        // Pero permitir navegación UI (D-Pad, joystick, submit, cancel)
        GamepadInputReader.PushGameplaySuppression(this);
        GamepadInputReader.PushUiNavigationScope();
        Debug.Log("[ShopUI] Gameplay suprimido - Solo inputs UI permitidos");
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        
        GameState.Pop(GamePhase.Shop);
        Time.timeScale = 1f;
        ResetBuyButtonFeedback();
        
        // Unregister from central manager
        MenuManager.Close(MenuKind.Shop);
        
        // Desactivar el windowRoot inmediatamente
        if (windowRoot != null)
            windowRoot.SetActive(false);
        
        // CRÍTICO: Ignorar el botón Cancel/B durante 0.3 segundos para evitar que se procese en gameplay
        GamepadInputReader.IgnoreCancelButton(0.3f);
        
        // Restaurar inputs de gameplay con un pequeño retraso
        Invoke(nameof(RestoreGameplayInputs), 0.2f);
    }

    private void RestoreGameplayInputs()
    {
        // Restaurar los inputs de gameplay después del retraso
        GamepadInputReader.PopUiNavigationScope();
        GamepadInputReader.PopGameplaySuppression(this);
        Debug.Log("[ShopUI] Gameplay restaurado - Inputs de gameplay desbloqueados (con retraso)");
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
        EnsurePlayerInventory();

        if (currencyText == null || _playerInventory == null || shopController == null)
            return;
        
        var currencyItem = shopController.CurrencyItem;
        if (currencyItem != null)
        {
            int amount = _playerInventory.Count(currencyItem.itemId);
            currencyText.text = $"💰 {amount}";
            return;
        }

        currencyText.text = "💰 0";
    }

    void EnsurePlayerInventory()
    {
        if (_playerInventory == null)
            PlayerService.TryGetComponent(out _playerInventory, includeInactive: true, allowSceneLookup: true);
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

        // Limpiar mensaje anterior a menos que se indique lo contrario
        if (!_skipMessageClearOnce)
        {
            ClearMessage(force: true);
        }
        _skipMessageClearOnce = false;

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

    void FocusSelectedCard()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _itemCards.Count) return;
        var button = _itemCards[_selectedIndex]?.GetButton();
        if (button == null) return;
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
        
        // El botón permanece deshabilitado hasta que se haga focus en él con Submit
        if (buyButton != null)
            buyButton.interactable = false;
        
        // Por ahora deshabilitamos venta (se puede implementar después)
        if (sellButton != null)
            sellButton.gameObject.SetActive(false);
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

        // Deshabilitar el botón de compra cuando no hay selección
        if (buyButton != null)
            buyButton.interactable = false;

        ClearMessage();
    }


    void OnSellClicked()
    {
        // TODO: Implementar venta de items del inventario
        if (messageText != null)
            messageText.text = "Función de venta no implementada aún.";
    }

    void ClearMessage(bool force = false)
    {
        if (!force && _messageLocked)
            return;
        _messageLocked = false;
        if (messageText != null)
            messageText.text = string.Empty;
    }

    void HandleBuyButtonPressed()
    {
        // Si acabamos de enfocar el botón, ignorar el onClick del botón
        if (_justFocusedBuyButton)
        {
            return;
        }

        if (_state == ShopState.Browsing)
        {
            _messageLocked = false;
            // Solo permitir si hay ítem seleccionado y tiene stock
            if (_selectedEntry != null && _selectedEntry.HasStock)
            {
                FocusBuyButton();
            }
            return;
        }

        if (_state == ShopState.BuyButtonFocused)
        {
            AttemptPurchase();
        }
    }

    void FocusBuyButton()
    {
        if (buyButton == null) return;
        if (_selectedEntry == null || !_selectedEntry.HasStock) return;

        _state = ShopState.BuyButtonFocused;
        
        // Habilitar el botón de compra
        buyButton.interactable = true;
        
        buyButton.Select();
        PlayBuyButtonFeedback();
        
        // Reproducir sonido de selección/confirmación
        GamepadInputReader.PlayUISound("UI_Select");
        
        // Prevenir que la misma pulsación de Submit compre el producto inmediatamente
        _justFocusedBuyButton = true;
        _ignoreInputUntil = Time.unscaledTime + 0.3f;
        
        // Resetear la bandera después de procesarse todos los eventos del frame actual
        StartCoroutine(ResetBuyButtonFocusFlag());
    }

    private System.Collections.IEnumerator ResetBuyButtonFocusFlag()
    {
        // Esperar 2 frames completos para asegurar que todos los eventos se procesen
        yield return null;
        yield return null;
        _justFocusedBuyButton = false;
    }

    void ReturnToItemList()
    {
        _state = ShopState.Browsing;
        ResetBuyButtonFeedback();
        
        // Deshabilitar el botón de compra al volver a la lista
        if (buyButton != null)
            buyButton.interactable = false;
        
        // Resetear bandera de focus
        _justFocusedBuyButton = false;
        
        FocusSelectedCard();
        _messageLocked = false;
    }

    void PlayBuyButtonFeedback()
    {
        if (buyButton == null) return;

        if (!_buyButtonColorsCached)
        {
            _buyButtonDefaultColors = buyButton.colors;
            _buyButtonColorsCached = true;
        }

        if (!_buyButtonScaleCached)
        {
            _buyButtonBaseScale = buyButton.transform.localScale;
            _buyButtonScaleCached = true;
        }

        // Matar animación previa si existe
        _buyButtonTween?.Kill();

        // Cambiar color a verde activo
        var colors = buyButton.colors;
        colors.normalColor = ActiveBuyButtonColor;
        colors.highlightedColor = ActiveBuyButtonColor * 1.05f;
        colors.selectedColor = ActiveBuyButtonColor * 1.05f;
        colors.pressedColor = ActiveBuyButtonColor * 0.9f;
        buyButton.colors = colors;

        // Animación de punch/rebote para dar feedback visual
        buyButton.transform.localScale = _buyButtonBaseScale;
        _buyButtonTween = buyButton.transform
            .DOPunchScale(Vector3.one * (buyButtonPulseScale - 1f), buyButtonPulseDuration, vibrato: 6, elasticity: 0.5f)
            .SetEase(buyButtonPulseEase)
            .SetUpdate(true)
            .OnComplete(() => {
                if (buyButton != null)
                    buyButton.transform.localScale = _buyButtonBaseScale * buyButtonPulseScale;
            });
    }

    void ResetBuyButtonFeedback()
    {
        _buyButtonTween?.Kill();
        _buyButtonTween = null;
        if (buyButton != null && _buyButtonScaleCached)
            buyButton.transform.localScale = _buyButtonBaseScale;

        if (buyButton != null && _buyButtonColorsCached)
            buyButton.colors = _buyButtonDefaultColors;
    }

    void AttemptPurchase()
    {
        if (_selectedIndex < 0 || shopController == null)
            return;

        var success = shopController.TryBuy(_selectedIndex, out string message);

        if (success)
            ShowMessage(string.IsNullOrEmpty(message) ? "¡Comprado!" : message, successMessageColor, pulseCurrency: true);
        else
            ShowMessage(string.IsNullOrEmpty(message) ? "No se pudo comprar." : message, errorMessageColor);

        if (success)
        {
            HandleSuccessfulPurchase();
        }
        else
        {
            // Mantener foco en el botón de compra para que el jugador pueda reintentar o cancelar con B
            _state = ShopState.BuyButtonFocused;
            FocusBuyButton();
        }
    }

    void HandleSuccessfulPurchase()
    {
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

    void ShowMessage(string text, Color color, bool pulseCurrency = false)
    {
        if (messageText != null)
        {
            messageText.text = text ?? string.Empty;
            messageText.color = color;
        }
        _messageLocked = true;

        if (pulseCurrency)
            PulseCurrencyDisplay();

        if (_state == ShopState.BuyButtonFocused)
            PlayBuyButtonFeedback();
    }

    void PulseCurrencyDisplay()
    {
        if (currencyText == null) return;
        _currencyTween?.Kill();
        var baseScale = currencyText.transform.localScale;
        _currencyTween = currencyText.transform
            .DOPunchScale(Vector3.one * (currencyPunchScale - 1f), currencyPunchDuration, vibrato: 6, elasticity: 0.4f)
            .SetUpdate(true)
            .OnComplete(() => currencyText.transform.localScale = baseScale);
    }
}
