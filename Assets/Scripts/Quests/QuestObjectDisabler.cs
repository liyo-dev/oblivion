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
    [SerializeField] private bool debugLogs;

    private bool _consumed;
    private bool _hasCheckedAfterDelay;

    void OnEnable()
    {
        var qm = QuestManager.Instance;
        if (qm)
        {
            qm.OnQuestsChanged += Check;
            if (debugLogs) Debug.Log($"[QuestObjectDisabler] Suscrito a OnQuestsChanged. questId='{questId}'");
        }
        
        // Verificación inicial inmediata
        Check();
        
        // Solo UNA verificación diferida para casos donde QuestManager se inicializa después
        // Esto cubre el caso de restauración de save sin hacer polling innecesario
        if (!_consumed && !_hasCheckedAfterDelay)
        {
            StartCoroutine(SingleDelayedCheck());
        }
    }

    void OnDisable()
    {
        var qm = QuestManager.Instance;
        if (qm)
        {
            qm.OnQuestsChanged -= Check;
            if (debugLogs) Debug.Log($"[QuestObjectDisabler] Desuscrito de OnQuestsChanged. questId='{questId}'");
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

    /// <summary>
    /// Una sola verificación diferida para cubrir el caso donde QuestManager se inicializa después del objeto.
    /// Mucho más eficiente que hacer 10 verificaciones.
    /// </summary>
    private System.Collections.IEnumerator SingleDelayedCheck()
    {
        // Esperar 1 frame para que QuestManager se inicialice si aún no lo está
        yield return null;
        
        // Verificar una sola vez
        if (!_consumed)
        {
            Check();
            _hasCheckedAfterDelay = true;
        }
    }
}
