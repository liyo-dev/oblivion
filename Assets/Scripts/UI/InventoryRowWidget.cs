using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryRowWidget : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private Image iconImage;

    ItemData _item;
    string _fallbackName = "Item";
    Action _onClick;
    Action _onSelected;
    ColorBlock _defaultColors;
    bool _colorsInitialized;
    bool _selectionCallbacksEnabled = true;

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
        
        if (button != null && !_colorsInitialized)
        {
            _defaultColors = button.colors;
            _colorsInitialized = true;
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
        if (button != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(ForceSelectionVisual());
        }
    }

    /// <summary>
    /// Resalta la fila con el color especificado (usado para navegación, igual que en hechizos)
    /// Usa el sistema de ColorBlock del Button, NO un Image separado
    /// </summary>
    public void SetHighlighted(bool highlighted, Color highlightColor)
    {
        if (button == null) return;
        
        if (!_colorsInitialized)
        {
            _defaultColors = button.colors;
            _colorsInitialized = true;
        }

        if (highlighted)
        {
            var colors = _defaultColors;
            colors.normalColor = highlightColor;
            colors.highlightedColor = highlightColor;
            colors.selectedColor = highlightColor;
            button.colors = colors;
        }
        else
        {
            button.colors = _defaultColors;
        }
    }
    
    System.Collections.IEnumerator ForceSelectionVisual()
    {
        // Esperar hasta el final del frame para que todo esté inicializado
        yield return new WaitForEndOfFrame();
        
        if (button != null)
        {
            // Esperar otro frame
            yield return null;
            
            // Forzar visualmente el estado de selección
            button.Select();
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
        if (!_selectionCallbacksEnabled) return;
        _onSelected?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_selectionCallbacksEnabled) return;
        _onSelected?.Invoke();
    }
}
