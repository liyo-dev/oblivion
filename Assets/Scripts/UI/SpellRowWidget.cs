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

    void HandleClick()
    {
        _onSelected?.Invoke();
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
