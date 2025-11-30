using UnityEngine;

/// <summary>
/// Destruye o desactiva un objeto cuando la quest indicada ya fue completada.
/// Útil para evitar que pickups reaparezcan en partidas donde la misión terminó.
/// </summary>
public class QuestObjectDisabler : MonoBehaviour
{
    [SerializeField] private string questId = string.Empty;
    [SerializeField] private GameObject target;
    [SerializeField] private bool destroyObject = true;

    private bool _consumed;

    void OnEnable()
    {
        QuestManager.Instance?.OnQuestsChanged += Check;
        Check();
    }

    void OnDisable()
    {
        if (QuestManager.Instance)
            QuestManager.Instance.OnQuestsChanged -= Check;
    }

    private void Check()
    {
        if (_consumed) return;
        if (string.IsNullOrEmpty(questId)) return;

        var qm = QuestManager.Instance;
        if (qm == null) return;
        if (qm.GetState(questId) != QuestState.Completed) return;

        var go = target ? target : gameObject;
        if (go == null) return;

        _consumed = true;
        if (destroyObject)
            Destroy(go);
        else
            go.SetActive(false);
    }
}
