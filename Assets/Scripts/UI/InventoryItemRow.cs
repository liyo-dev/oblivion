using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryItemRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Image iconImage;

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

