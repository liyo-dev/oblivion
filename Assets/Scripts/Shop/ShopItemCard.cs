using UnityEngine;
using UnityEngine.UI;

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
            nameText.text = item.displayName;
        else
            Debug.LogWarning("[ShopItemCard] nameText es null");
        
        if (priceText != null)
        {
            priceText.text = $"{price} 💰";
            Debug.Log($"[ShopItemCard] PriceText actualizado a: {priceText.text}");
        }
        else
            Debug.LogWarning("[ShopItemCard] priceText es NULL - no está asignado en el inspector");
        
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
    
    public Button GetButton() => button;
}
