using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpellRowWidget : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private Image icon;

    Action _onClick;
    Action _onSelected;
    bool _selectionCallbacksEnabled = true;
    ColorBlock _defaultColors;
    bool _colorsInitialized;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (label == null)
            label = GetComponentInChildren<Text>();
        if (icon == null)
        {
            var iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                icon = iconTransform.GetComponent<Image>();
        }
        
        if (button != null && !_colorsInitialized)
        {
            _defaultColors = button.colors;
            _colorsInitialized = true;
        }
    }

    public void SetLabel(string value)
    {
        if (label != null)
            label.text = value;
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

    public GameObject ButtonGameObject => button != null ? button.gameObject : gameObject;

    public Selectable Selectable => button != null ? button : GetComponent<Selectable>();

    public void Focus()
    {
        var es = EventSystem.current;
        if (es != null && ButtonGameObject != null)
            es.SetSelectedGameObject(ButtonGameObject);
    }

    public void SetSelectionCallbacksEnabled(bool enabled)
    {
        _selectionCallbacksEnabled = enabled;
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon == null) return;
        if (sprite == null)
        {
            icon.enabled = false;
            icon.sprite = null;
        }
        else
        {
            icon.enabled = true;
            icon.sprite = sprite;
        }
    }

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

    void HandleClick()
    {
        _onSelected?.Invoke();
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
