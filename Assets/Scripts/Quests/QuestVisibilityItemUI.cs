using System;
using System.Collections.Generic;
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
    private ScrollRect _scrollRect;

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
            questState.text = GetStateLabel(data.State);
        }

        if (showButton)
        {
            showButton.onClick.RemoveAllListeners();
            showButton.onClick.AddListener(() => NotifyChange(QuestVisibility.Visible));
            SetButtonState(showButton, visibility == QuestVisibility.Visible);
            EnsureTextHighlight(showButton);
        }

        if (hideButton)
        {
            hideButton.onClick.RemoveAllListeners();
            hideButton.onClick.AddListener(() => NotifyChange(QuestVisibility.Hidden));
            SetButtonState(hideButton, visibility == QuestVisibility.Hidden);
            EnsureTextHighlight(hideButton);
        }

        DisableUnusedButtons();
        UpdateInteractableStates();
    }

    public void ConfigureScrollRect(ScrollRect scrollRect)
    {
        _scrollRect = scrollRect;
        AttachScrollRelay(showButton);
        AttachScrollRelay(hideButton);
    }

    void NotifyChange(QuestVisibility visibility)
    {
        if (_data == null) return;
        if (visibility == _currentVisibility) return;

        Debug.Log($"QuestVisibilityItemUI: NotifyChange for '{_data.Id}' -> {visibility}");

        _currentVisibility = visibility;
        UpdateInteractableStates();
        _onChange?.Invoke(_data, visibility);
    }

    static void SetButtonState(Button btn, bool active)
    {
        if (btn == null) return;
        Debug.Log($"QuestVisibilityItemUI: SetButtonState '{btn.name}' active={active}");
    }

    void DisableUnusedButtons()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button == showButton || button == hideButton) continue;
            button.gameObject.SetActive(false);
        }
    }

    string GetStateLabel(QuestState state)
    {
        string fallback = state switch
        {
            QuestState.Inactive => "Inactiva",
            QuestState.Active => "Activa",
            QuestState.Completed => "Completada",
            _ => state.ToString()
        };

        if (LocalizationManager.Instance != null)
        {
            string key = state switch
            {
                QuestState.Inactive => "QUEST_STATE_INACTIVE",
                QuestState.Active => "QUEST_STATE_ACTIVE",
                QuestState.Completed => "QUEST_STATE_COMPLETED",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(key))
                fallback = LocalizationManager.Instance.Get(key, fallback);
        }

        return fallback;
    }

    void EnsureTextHighlight(Button button)
    {
        if (button == null) return;

        var tmpLabels = new List<TextMeshProUGUI>();
        button.GetComponentsInChildren(includeInactive: true, tmpLabels);
        foreach (var label in tmpLabels)
        {
            if (label == null) continue;
            var highlight = label.GetComponent<MenuTextHighlight>();
            if (highlight == null)
                highlight = label.gameObject.AddComponent<MenuTextHighlight>();

            highlight.selectionOwner = button.gameObject;
        }
    }

    void UpdateInteractableStates()
    {
        if (showButton)
            showButton.interactable = _currentVisibility != QuestVisibility.Visible;

        if (hideButton)
            hideButton.interactable = _currentVisibility != QuestVisibility.Hidden;
    }

    void AttachScrollRelay(Selectable selectable)
    {
        if (selectable == null || _scrollRect == null) return;
        var relay = selectable.gameObject.GetComponent<ScrollOnSelectRelay>();
        if (relay == null)
            relay = selectable.gameObject.AddComponent<ScrollOnSelectRelay>();
        relay.scrollRect = _scrollRect;
        relay.target = transform as RectTransform;
    }
}
