using System;
using UnityEngine;
using UnityEngine.UI;

public class SpellRowWidget : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;

    Action _onClick;

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

    void HandleClick()
    {
        _onClick?.Invoke();
    }
}
