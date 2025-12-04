using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryItemRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;

    void Awake()
    {
        // Asegurar que el row tiene un Button para navegación
        if (button == null)
            button = GetComponent<Button>();
        
        if (button == null)
            button = gameObject.AddComponent<Button>();
        
        // Configurar navegación explícita (se actualizará desde InventoryMenu)
        var nav = button.navigation;
        nav.mode = Navigation.Mode.Explicit;
        button.navigation = nav;
    }

    public Button GetButton() => button;

    public void Setup(ItemData item, int count)
    {
        if (item != null)
        {
            nameText.text = item.displayName;
            iconImage.sprite = item.icon;
        }
        else
        {
            nameText.text = "";
            iconImage.sprite = null;
        }

        countText.text = count.ToString();
    }
}

