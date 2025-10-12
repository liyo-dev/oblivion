using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

public class QuestLogItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI questName;
    [SerializeField] private TextMeshProUGUI firstStepDesc; // Descripción del primer paso activo
    [SerializeField] private TextMeshProUGUI statePillText;
    [SerializeField] private Image statePillBg;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image progressFill;      // barra de progreso (Fill)
    [SerializeField] private Transform stepsRoot;     // contenedor de steps
    [SerializeField] private QuestStepItemUI stepPrefab;
    [SerializeField] private GameObject stepsContainer; // el GameObject completo para show/hide

    [Header("Estilos")]
    [SerializeField] private Color colorInactive = new Color(0.6f,0.6f,0.6f);
    [SerializeField] private Color colorActive = new Color(0.2f,0.6f,1f);
    [SerializeField] private Color colorCompleted = new Color(0.3f,0.8f,0.4f);

    private bool _isExpanded = false;

    public void Bind(QuestManager.RuntimeQuest data)
    {
        // Título de la quest (localizado)
        string display = data.Data.GetLocalizedName();
        if (string.IsNullOrEmpty(display)) display = data.Id;
        if (questName) questName.text = display;

        // Obtener steps una sola vez para todo el método
        var steps = data.Steps ?? Array.Empty<QuestStep>();

        // Descripción del primer paso no completado (localizado)
        if (firstStepDesc)
        {
            string stepDescription = "";
            int stepIndex = -1;
            
            // Buscar el primer paso no completado
            for (int i = 0; i < steps.Length; i++)
            {
                if (!steps[i].completed)
                {
                    stepIndex = i;
                    break;
                }
            }
            
            // Si todos están completados, mostrar el último paso
            if (stepIndex == -1 && steps.Length > 0)
            {
                stepIndex = steps.Length - 1;
            }
            
            // Obtener descripción localizada
            if (stepIndex >= 0)
            {
                stepDescription = data.Data.GetLocalizedStepDescription(stepIndex);
            }
            
            firstStepDesc.text = stepDescription;
        }

        switch (data.State)
        {
            case QuestState.Inactive:
                // Localizar estado
                string inactiveText = "Inactiva";
                if (LocalizationManager.Instance != null)
                    inactiveText = LocalizationManager.Instance.Get("QUEST_STATE_INACTIVE", inactiveText);
                if (statePillText) statePillText.text = inactiveText;
                if (statePillBg) statePillBg.color = colorInactive;
                break;
            case QuestState.Active:
                string activeText = "Activa";
                if (LocalizationManager.Instance != null)
                    activeText = LocalizationManager.Instance.Get("QUEST_STATE_ACTIVE", activeText);
                if (statePillText) statePillText.text = activeText;
                if (statePillBg) statePillBg.color = colorActive;
                break;
            case QuestState.Completed:
                string completedText = "Completada";
                if (LocalizationManager.Instance != null)
                    completedText = LocalizationManager.Instance.Get("QUEST_STATE_COMPLETED", completedText);
                if (statePillText) statePillText.text = completedText;
                if (statePillBg) statePillBg.color = colorCompleted;
                break;
        }

        // Calcular progreso
        int done = 0;
        foreach (var s in steps) if (s.completed) done++;
        float pct = (steps.Length == 0) ? 1f : (float)done / steps.Length;

        if (progressFill) progressFill.fillAmount = pct;
        if (progressText) progressText.text = steps.Length > 0 ? $"{done}/{steps.Length}" : "—";

        if (stepsRoot && stepPrefab)
        {
            for (int i = stepsRoot.childCount - 1; i >= 0; i--)
                GameObject.Destroy(stepsRoot.GetChild(i).gameObject);

            for (int i = 0; i < steps.Length; i++)
            {
                var item = GameObject.Instantiate(stepPrefab, stepsRoot);
                // Usar el método Bind con localización
                item.Bind(steps[i], data.Data, i);
            }
        }

        // Inicialmente colapsado
        SetExpanded(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ToggleExpanded();
    }

    public void ToggleExpanded()
    {
        SetExpanded(!_isExpanded);
    }

    public void SetExpanded(bool expanded)
    {
        _isExpanded = expanded;
        if (stepsContainer)
        {
            stepsContainer.SetActive(_isExpanded);
        }
    }
}
