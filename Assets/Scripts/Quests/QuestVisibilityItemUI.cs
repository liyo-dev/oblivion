using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestVisibilityItemUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshProUGUI questName;
    [SerializeField] private TextMeshProUGUI questState;
    [SerializeField] private TextMeshProUGUI questDescription;
    [SerializeField] private Button showButton;
    [SerializeField] private Button hideButton;

    private QuestManager.RuntimeQuest _data;
    private Action<QuestManager.RuntimeQuest, QuestVisibility> _onChange;
    private QuestVisibility _currentVisibility;

    public void Bind(
        QuestManager.RuntimeQuest data,
        QuestVisibility visibility,
        Action<QuestManager.RuntimeQuest, QuestVisibility> onChange)
    {
        _data = data;
        _onChange = onChange;
        _currentVisibility = visibility;

        if (questName)
        {
            var display = data.Data.GetLocalizedName();
            questName.text = string.IsNullOrEmpty(display) ? data.Id : display;
        }

        if (questDescription)
        {
            var description = data.Data.GetLocalizedDescription();
            questDescription.text = string.IsNullOrEmpty(description) ? string.Empty : description;
        }

        if (questState)
        {
            var stateLabel = visibility switch
            {
                QuestVisibility.Hidden => "Oculta",
                _ => "Visible"
            };
            questState.text = stateLabel;
        }

        if (showButton)
        {
            showButton.onClick.RemoveAllListeners();
            showButton.onClick.AddListener(() => NotifyChange(QuestVisibility.Visible));
            SetButtonState(showButton, visibility == QuestVisibility.Visible);
        }

        if (hideButton)
        {
            hideButton.onClick.RemoveAllListeners();
            hideButton.onClick.AddListener(() => NotifyChange(QuestVisibility.Hidden));
            SetButtonState(hideButton, visibility == QuestVisibility.Hidden);
        }

        DisableUnusedButtons();
        UpdateInteractableStates();
    }

    void NotifyChange(QuestVisibility visibility)
    {
        if (_data == null) return;
        if (visibility == _currentVisibility) return;

        _currentVisibility = visibility;
        UpdateInteractableStates();
        _onChange?.Invoke(_data, visibility);
    }

    static void SetButtonState(Button btn, bool active)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.colorMultiplier = active ? 1.2f : 1f;
        btn.colors = colors;
    }

    void DisableUnusedButtons()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button == showButton || button == hideButton) continue;
            button.gameObject.SetActive(false);
        }
    }

    void UpdateInteractableStates()
    {
        if (showButton)
            showButton.interactable = _currentVisibility != QuestVisibility.Visible;

        if (hideButton)
            hideButton.interactable = _currentVisibility != QuestVisibility.Hidden;
    }
}
