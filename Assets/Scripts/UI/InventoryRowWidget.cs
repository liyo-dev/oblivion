using System;
using UnityEngine;
using UnityEngine.UI;

public class InventoryRowWidget : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private Image iconImage;

    ItemData _item;
    string _fallbackName = "Item";
    Action _onClick;

    public GameObject ButtonGameObject => button != null ? button.gameObject : gameObject;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (label == null)
            label = GetComponentInChildren<Text>();
        if (iconImage == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                iconImage = iconTransform.GetComponent<Image>();
        }
    }

    public void Configure(ItemData item)
    {
        _item = item;
        if (_item != null && !string.IsNullOrEmpty(_item.displayName))
            _fallbackName = _item.displayName;
        UpdateIcon();
    }

    public void RefreshLabel(Inventory inventory)
    {
        if (label == null) return;

        int count = (inventory != null && _item != null)
            ? inventory.Count(_item.itemId)
            : 0;

        string name = _item != null && !string.IsNullOrEmpty(_item.displayName)
            ? _item.displayName
            : _fallbackName;

        label.text = $"{name} x{count}";
    }

    public void RegisterClickHandler(Action onClick)
    {
        _onClick = onClick;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    public void InvokeClick()
    {
        if (button != null)
            button.onClick.Invoke();
        else
            HandleClick();
    }

    void UpdateIcon()
    {
        if (iconImage == null) return;

        if (_item != null && _item.icon != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = _item.icon;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    void HandleClick()
    {
        _onClick?.Invoke();
    }
}
