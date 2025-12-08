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
    [SerializeField] private bool debugLogs = false;

    private bool _consumed;
    private Coroutine _delayedCheckRoutine;

    void OnEnable()
    {
        var qm = QuestManager.Instance;
        if (qm)
        {
            qm.OnQuestsChanged += Check;
            if (debugLogs) Debug.Log($"[QuestObjectDisabler] Suscrito a OnQuestsChanged. questId='{questId}'");
        }
        Check();

        // Re-evaluación tardía para casos donde las quests se restauran después
        if (_delayedCheckRoutine == null)
            _delayedCheckRoutine = StartCoroutine(DelayedReadyChecks());
    }

    void OnDisable()
    {
        var qm = QuestManager.Instance;
        if (qm)
        {
            qm.OnQuestsChanged -= Check;
            if (debugLogs) Debug.Log($"[QuestObjectDisabler] Desuscrito de OnQuestsChanged. questId='{questId}'");
        }

        if (_delayedCheckRoutine != null)
        {
            StopCoroutine(_delayedCheckRoutine);
            _delayedCheckRoutine = null;
        }
    }

    private void Check()
    {
        if (_consumed) return;
        if (string.IsNullOrEmpty(questId)) return;

        var qm = QuestManager.Instance;
        if (qm == null)
        {
            if (debugLogs) Debug.LogWarning($"[QuestObjectDisabler] QuestManager.Instance es null. No se puede evaluar '{questId}'.");
            return;
        }

        var state = qm.GetState(questId);
        if (debugLogs) Debug.Log($"[QuestObjectDisabler] Estado de '{questId}': {state}");
        if (state != QuestState.Completed) return;

        var go = target ? target : gameObject;
        if (go == null) return;

        _consumed = true;
        if (destroyObject)
        {
            if (debugLogs) Debug.Log($"[QuestObjectDisabler] Destruyendo objeto '{go.name}' por quest completada '{questId}'.");
            Destroy(go);
        }
        else
        {
            if (debugLogs) Debug.Log($"[QuestObjectDisabler] Desactivando objeto '{go.name}' por quest completada '{questId}'.");
            go.SetActive(false);
        }
    }

    public void ForceCheck()
    {
        if (debugLogs) Debug.Log($"[QuestObjectDisabler] ForceCheck invocado para '{questId}'.");
        _consumed = false; // permitir re-evaluación en caso de reactivación manual
        Check();
    }

    System.Collections.IEnumerator DelayedReadyChecks()
    {
        // Intenta varias veces en los primeros segundos para cubrir restauraciones tardías
        const int attempts = 10;
        const float interval = 0.25f;
        for (int i = 0; i < attempts && !_consumed; i++)
        {
            yield return new WaitForSeconds(interval);
            if (debugLogs) Debug.Log($"[QuestObjectDisabler] DelayedReadyChecks intento {i+1}/{attempts} para '{questId}'.");
            Check();
        }
        _delayedCheckRoutine = null;
    }
}
