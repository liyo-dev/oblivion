using UnityEngine;

/// <summary>
/// Marca un paso de misión cuando el objeto se recoge mediante un Interactable.
/// Se autoconecta al UnityEvent del Interactable para evitar configurar listeners manualmente.
/// </summary>
public class SimpleQuestPickup : MonoBehaviour
{
    [SerializeField] string questId;
    [SerializeField] int stepIndex;
    [SerializeField] bool completeOnPickup = false;
    [SerializeField] bool debugLogs = false;

    Interactable _interactable;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
    }

    void OnEnable()
    {
        if (_interactable != null)
            _interactable.OnInteract.AddListener(OnInteract);
    }

    void OnDisable()
    {
        if (_interactable != null)
            _interactable.OnInteract.RemoveListener(OnInteract);
    }

    void OnInteract(GameObject _)
    {
        Pick();
    }

    public void Pick()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;
        var state = qm.GetState(questId);
        if (debugLogs)
            Debug.Log($"[SimpleQuestPickup] Pick quest={questId} state={state}");

        if (!completeOnPickup)
            return;

        if (state == QuestState.Active)
            qm.MarkStepDone(questId, stepIndex);
    }
}
