using UnityEngine;

[DisallowMultipleComponent]
public class QuestStarterOnInteract : MonoBehaviour
{
    [SerializeField] private string questIdToStart = string.Empty;
    [SerializeField] private bool onlyOnce = true;
    [SerializeField] private GameObject targetToHide;

    bool used;

    void Awake()
    {
        if (!targetToHide)
            targetToHide = gameObject;
    }

    void OnEnable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged += HandleQuestsChanged;

        EvaluateVisibility();
    }

    void OnDisable()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged -= HandleQuestsChanged;
    }

    void HandleQuestsChanged()
    {
        EvaluateVisibility();
    }

    void EvaluateVisibility()
    {
        if (!onlyOnce) return;
        if (QuestManager.Instance == null) return;
        if (string.IsNullOrEmpty(questIdToStart)) return;

        var state = QuestManager.Instance.GetState(questIdToStart);
        if (state != QuestState.Inactive)
        {
            used = true;
            if (targetToHide && targetToHide.activeSelf)
                targetToHide.SetActive(false);
        }
    }

    // Llama a este método desde el OnFinished del Interactable (o desde un UnityEvent del propio diálogo)
    public void StartQuestNow()
    {
        if (used) return;

        QuestManager.Instance?.StartQuest(questIdToStart);
        used = onlyOnce;

        if (used && targetToHide)
            targetToHide.SetActive(false);
    }
}
