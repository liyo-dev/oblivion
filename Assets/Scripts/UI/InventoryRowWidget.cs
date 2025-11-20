using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryRowWidget : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private Image iconImage;
    [Header("Feedback visual")]
    [SerializeField] private Color selectionColor = new Color(1f, 0.82f, 0.16f, 1f);

    ItemData _item;
    string _fallbackName = "Item";
    Action _onClick;
    Action _onSelected;
    ColorBlock _originalColors;
    bool _hasCachedColors;

    public GameObject ButtonGameObject => button != null ? button.gameObject : gameObject;
    public ItemData Item => _item;

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

        CacheAndApplySelectionColors();
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

    public void RegisterSelectedHandler(Action onSelected)
    {
        _onSelected = onSelected;
    }

    public void InvokeClick()
    {
        if (button != null)
            button.onClick.Invoke();
        else
            HandleClick();
    }

    public void Focus()
    {
        if (button != null)
        {
            StartCoroutine(ForceSelectionVisual());
        }
    }
    
    System.Collections.IEnumerator ForceSelectionVisual()
    {
        // Esperar hasta el final del frame para que todo esté inicializado
        yield return new WaitForEndOfFrame();
        
        var es = EventSystem.current;
        if (es != null && ButtonGameObject != null && button != null)
        {
            // Seleccionar en el EventSystem
            es.SetSelectedGameObject(ButtonGameObject);
            
            // Esperar otro frame
            yield return null;
            
            // Forzar visualmente el estado de selección
            if (es.currentSelectedGameObject == ButtonGameObject)
            {
                // Forzar el estado pressed y luego selected para activar la transición visual
                button.Select();
            }
        }
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

    public void OnSelect(BaseEventData eventData)
    {
        _onSelected?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _onSelected?.Invoke();
    }

    void CacheAndApplySelectionColors()
    {
        if (button == null || _hasCachedColors)
            return;

        _originalColors = button.colors;
        _hasCachedColors = true;

        var colors = _originalColors;
        colors.highlightedColor = selectionColor;
        colors.selectedColor = selectionColor;
        button.colors = colors;
    }
}
