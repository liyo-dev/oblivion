using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpellRowWidget : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;

    Action _onClick;
    Action _onSelected;
    bool _selectionCallbacksEnabled = true;

    void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (label == null)
            label = GetComponentInChildren<Text>();
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
