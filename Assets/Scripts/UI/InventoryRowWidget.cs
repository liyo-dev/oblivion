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
    [SerializeField] private Graphic highlightGraphic;

    ItemData _item;
    string _fallbackName = "Item";
    Action _onClick;
    Action _onSelected;
    Color _defaultLabelColor = Color.white;
    Color _defaultIconColor = Color.white;
    Color _defaultHighlightColor = Color.white;

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
        if (label != null) _defaultLabelColor = label.color;
        if (iconImage != null) _defaultIconColor = iconImage.color;
        if (highlightGraphic == null)
        {
            var bg = GetComponent<Image>();
            if (bg != null && bg != iconImage)
                highlightGraphic = bg;
            else if (button != null)
            {
                var candidate = button.GetComponent<Image>();
                if (candidate != null && candidate != iconImage)
                    highlightGraphic = candidate;
                else if (button.targetGraphic != null && button.targetGraphic != label && button.targetGraphic != iconImage)
                    highlightGraphic = button.targetGraphic;
            }
        }
        if (highlightGraphic != null)
            _defaultHighlightColor = highlightGraphic.color;
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

    public void SetSelectedState(bool selected)
    {
        if (label != null)
            label.color = _defaultLabelColor;
        if (iconImage != null)
            iconImage.color = _defaultIconColor;
        if (highlightGraphic != null)
            highlightGraphic.color = selected ? selectionColor : _defaultHighlightColor;
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

}
