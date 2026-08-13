using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Componente para cada card de item en la lista de la tienda.
/// </summary>
public class ShopItemCard : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private TextMeshProUGUI stockText;
    [SerializeField] private Button button;
    [SerializeField] private Image background;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(1f, 0.82f, 0.16f, 1f);

    [Header("Rediseño visual - chip de stock (icono real, ver coin.png)")]
    [Tooltip("Fondo tipo 'chip' detrás de stockText. Solo se muestra cuando el item tiene stock limitado.")]
    [SerializeField] private Image stockChipBackground;
    [SerializeField] private Sprite stockChipSpriteAvailable;
    [SerializeField] private Sprite stockChipSpriteUnavailable;

    private System.Action _onSelect;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        
        if (button != null)
            button.onClick.AddListener(() => _onSelect?.Invoke());
    }

    public void Setup(ShopController.ShopItemEntry entry, int index, System.Action onSelect)
    {
        _onSelect = onSelect;
        
        if (entry == null || entry.item == null)
        {
            Debug.LogWarning("[ShopItemCard] Setup: entry o item es null");
            return;
        }
        
        var item = entry.item;
        int price = entry.GetBuyPrice();
        
        Debug.Log($"[ShopItemCard] Setup: {item.displayName}, precio={price}");
        
        if (iconImage != null)
            iconImage.sprite = item.icon;
        else
            Debug.LogWarning("[ShopItemCard] iconImage es null");
        
        if (nameText != null)
            nameText.text = item.GetLocalizedName();
        else
            Debug.LogWarning("[ShopItemCard] nameText es null");
        
        if (priceText != null)
        {
            // Ya no se usa el emoji 💰 literal: el icono de moneda es ahora el sprite
            // real "coin.png" (CoinIcon) mostrado junto a este texto en el prefab.
            priceText.text = $"{price}";
            Debug.Log($"[ShopItemCard] PriceText actualizado a: {priceText.text}");
        }
        else
            Debug.LogWarning("[ShopItemCard] priceText es NULL - no está asignado en el inspector");

        if (stockText != null)
        {
            if (entry.limitedStock)
            {
                string key = entry.HasStock ? "SHOP_ITEM_AVAILABLE" : "SHOP_STOCK_OUT";
                string fallback = entry.HasStock ? "Disponible" : "Agotado";
                stockText.text = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.Get(key, fallback)
                    : fallback;
            }
            else
                stockText.text = "";
        }

        // Chip visual detrás de stockText (rediseño "glass"): solo se muestra si el item
        // tiene stock limitado (igual criterio que el texto de arriba), y cambia de sprite
        // según haya o no stock disponible. Puramente visual, no toca la lógica de compra.
        if (stockChipBackground != null)
        {
            stockChipBackground.gameObject.SetActive(entry.limitedStock);
            if (entry.limitedStock)
            {
                var chipSprite = entry.HasStock ? stockChipSpriteAvailable : stockChipSpriteUnavailable;
                if (chipSprite != null)
                    stockChipBackground.sprite = chipSprite;
            }
        }

        if (button != null)
            button.interactable = entry.HasStock;
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
    
    public Button GetButton() => button;
}
