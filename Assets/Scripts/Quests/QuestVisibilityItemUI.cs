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
    [SerializeField] private Image statePillBg;
    
    [Header("Buttons")]
    [Tooltip("Botón para archivar la misión (se muestra solo en panel de visibles)")]
    [SerializeField] private Button archiveButton;
    [Tooltip("Botón para activar/seguir la misión (se muestra solo en panel de archivados)")]
    [SerializeField] private Button activateButton;
    
    [Header("Sprites State Pill")]
    [Tooltip("Sprite del state pill cuando la quest está activa (azul)")]
    [SerializeField] private Sprite statePillSpriteActive;
    [Tooltip("Sprite del state pill cuando la quest está completada (verde)")]
    [SerializeField] private Sprite statePillSpriteCompleted;

    private QuestManager.RuntimeQuest _data;
    private Action<QuestManager.RuntimeQuest, QuestVisibility> _onChange;
    private QuestVisibility _currentVisibility;
    private ScrollRect _scrollRect;
    
    public Button GetArchiveButton() => archiveButton;
    public Button GetActivateButton() => activateButton;

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
            // Panel de detalle de misión: usa la descripción detallada (guía completa,
            // con todos los pasos explicados) en vez de la descripción corta que ya
            // se muestra en el menú rápido de misiones.
            var description = data.Data.GetLocalizedDetailedDescription();
            questDescription.text = string.IsNullOrEmpty(description) ? string.Empty : description;
        }

        if (questState)
        {
            questState.text = GetStateLabel(data.State);
        }

        // Aplicar sprite del state pill según el estado
        if (statePillBg != null)
        {
            switch (data.State)
            {
                case QuestState.Active:
                    if (statePillSpriteActive != null)
                        statePillBg.sprite = statePillSpriteActive;
                    break;
                
                case QuestState.Completed:
                    if (statePillSpriteCompleted != null)
                        statePillBg.sprite = statePillSpriteCompleted;
                    break;
            }
        }

        // Configurar botones según el panel actual
        if (visibility == QuestVisibility.Visible)
        {
            // Panel de visibles → Solo mostrar botón "Archivar"
            ConfigureButton(archiveButton, QuestVisibility.Hidden, "Archivar");
            if (activateButton) activateButton.gameObject.SetActive(false);
        }
        else // QuestVisibility.Hidden
        {
            // Panel de archivados → Solo mostrar botón "Activar"
            ConfigureButton(activateButton, QuestVisibility.Visible, "Activar");
            if (archiveButton) archiveButton.gameObject.SetActive(false);
        }

        DisableUnusedButtons();
    }

    public void ConfigureScrollRect(ScrollRect scrollRect)
    {
        _scrollRect = scrollRect;
        AttachScrollRelay(archiveButton);
        AttachScrollRelay(activateButton);
    }

    /// <summary>
    /// Configura un botón para cambiar la visibilidad de la quest
    /// </summary>
    void ConfigureButton(Button button, QuestVisibility targetVisibility, string actionName)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => NotifyChange(targetVisibility));
        button.interactable = true;
        
        EnsureTextHighlight(button);
        

    }

    void NotifyChange(QuestVisibility visibility)
    {
        if (_data == null) return;
        if (visibility == _currentVisibility) return;

        Debug.Log($"QuestVisibilityItemUI: NotifyChange for '{_data.Id}' -> {visibility}");

        _currentVisibility = visibility;
        _onChange?.Invoke(_data, visibility);
    }

    void DisableUnusedButtons()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button == archiveButton || button == activateButton) continue;
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
