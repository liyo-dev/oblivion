using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla la compra/venta de items para una tienda concreta. No dibuja UI;
/// expone métodos para que un menú pueda consultar stock, comprar y vender.
/// </summary>
public class ShopController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private ItemData currencyItem;

    public ItemData CurrencyItem => currencyItem;

    [Header("Stock (por tienda)")]
    [SerializeField] private List<ShopItemEntry> stock = new();

    public IReadOnlyList<ShopItemEntry> Stock => stock;

    public event Action OnStockChanged;
    private Inventory playerInventory;
    private WardrobeInventory wardrobeInventory;

    void Awake()
    {
        if (playerInventory == null)
            PlayerService.TryGetComponent(out playerInventory, includeInactive: true, allowSceneLookup: true);
        if (wardrobeInventory == null)
            PlayerService.TryGetComponent(out wardrobeInventory, includeInactive: true, allowSceneLookup: true);

        for (int i = 0; i < stock.Count; i++)
            stock[i]?.ResetRuntime();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Establecer valores por defecto cuando se añaden nuevos items
        foreach (var entry in stock)
        {
            if (entry != null && entry.buyPriceOverride == 0 && entry.sellPriceOverride == 0)
            {
                entry.buyPriceOverride = -1;
                entry.sellPriceOverride = -1;
            }
        }
    }
#endif

    public bool TryBuy(int index, out string message)
    {
        message = null;
        if (!IsValidIndex(index))
        {
            message = "Selección inválida.";
            return false;
        }

        var entry = stock[index];
        if (entry == null || entry.item == null)
        {
            message = "Item no disponible.";
            return false;
        }

        if (!entry.HasStock)
        {
            message = "Producto agotado.";
            return false;
        }

        if (!HasInventoryReferences(out message))
            return false;

        int price = entry.GetBuyPrice();
        Debug.Log($"[ShopController] TryBuy: item={entry.item.displayName}, precio={price}, buyPriceOverride={entry.buyPriceOverride}, item.buyPrice={entry.item.buyPrice}");
        
        if (price > 0 && !HasCurrency(price))
        {
            message = "No tienes suficientes monedas.";
            return false;
        }

        if (price > 0)
            SpendCurrency(price);

        GrantItem(entry.item);

        entry.ConsumeOne();
        OnStockChanged?.Invoke();
        return true;
    }

    public bool TrySell(ItemData item, int amount, out string message)
    {
        message = null;
        if (item == null)
        {
            message = "Item inválido.";
            return false;
        }

        if (amount <= 0) amount = 1;

        if (!HasInventoryReferences(out message))
            return false;

        if (!playerInventory.TryConsume(item, amount, true))
        {
            message = "No tienes suficiente cantidad.";
            return false;
        }

        int payout = Mathf.Max(0, item.sellValue) * amount;
        if (payout > 0)
            playerInventory.Add(currencyItem, payout);

        OnStockChanged?.Invoke();
        return true;
    }

    bool HasInventoryReferences(out string message)
    {
        // Intentar obtener referencias si son null
        if (playerInventory == null)
            PlayerService.TryGetComponent(out playerInventory, includeInactive: true, allowSceneLookup: true);
        
        if (wardrobeInventory == null)
            PlayerService.TryGetComponent(out wardrobeInventory, includeInactive: true, allowSceneLookup: true);
        
        if (playerInventory == null)
        {
            message = "No hay inventario configurado.";
            Debug.LogError("[ShopController] No se pudo encontrar Inventory del jugador");
            return false;
        }
        if (currencyItem == null)
        {
            message = "No se ha asignado item de monedas.";
            Debug.LogError("[ShopController] currencyItem no está asignado en el inspector");
            return false;
        }
        message = null;
        return true;
    }

    bool IsValidIndex(int index) => index >= 0 && index < stock.Count;

    bool HasCurrency(int amount)
    {
        if (amount <= 0) return true;
        if (currencyItem == null || playerInventory == null) return false;
        return playerInventory.Count(currencyItem.itemId) >= amount;
    }

    void SpendCurrency(int amount)
    {
        if (amount <= 0 || currencyItem == null) return;
        playerInventory.TryConsume(currencyItem, amount, true);
    }

    void GrantItem(ItemData item)
    {
        if (item == null || playerInventory == null) return;

        switch (item.usageKind)
        {
            case ItemData.ItemUsageKind.Equipment:
                if (item.wardrobeUnlock != null && wardrobeInventory != null)
                {
                    wardrobeInventory.Unlock(item.wardrobeUnlock);
                }
                else
                {
                    playerInventory.Add(item, 1);
                }
                break;
            default:
                playerInventory.Add(item, 1);
                break;
        }
    }

    [Serializable]
    public class ShopItemEntry
    {
        public ItemData item;
        [Tooltip("Override del precio de compra. -1 usa el valor del item.")]
        public int buyPriceOverride = -1;
        [Tooltip("Override del precio de venta. -1 usa el valor del item.")]
        public int sellPriceOverride = -1;
        public bool limitedStock;
        [Min(0)] public int startingStock = 1;
        int _runtimeStock;

        public bool HasStock => !limitedStock || _runtimeStock > 0;
        public int GetBuyPrice() => buyPriceOverride >= 0 ? buyPriceOverride : (item != null ? Mathf.Max(0, item.buyPrice) : 0);
        public int GetSellPrice() => sellPriceOverride >= 0 ? sellPriceOverride : (item != null ? Mathf.Max(0, item.sellValue) : 0);

        public void ResetRuntime()
        {
            _runtimeStock = startingStock;
        }

        public void ConsumeOne()
        {
            if (!limitedStock) return;
            if (_runtimeStock > 0)
                _runtimeStock--;
        }
    }
}
