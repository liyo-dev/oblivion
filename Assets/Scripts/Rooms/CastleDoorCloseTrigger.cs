using UnityEngine;

/// <summary>
/// Cierra las puertas del castillo cuando el jugador cruza al interior.
/// Colocar un BoxCollider trigger en el umbral interior de las puertas.
/// Si la misión asociada está completada, el trigger se ignora y las puertas permanecen abiertas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CastleDoorCloseTrigger : MonoBehaviour
{
    [SerializeField] private CastleDoorController doorController;

    [Tooltip("Segundos de espera antes de cerrar (da tiempo al jugador a cruzar)")]
    [SerializeField] private float delay = 0.5f;

    [Header("Bloqueo por misión")]
    [Tooltip("Si esta misión está activa o completada, el trigger no cerrará las puertas.")]
    [SerializeField] private QuestData blockingQuest;

    private Collider _col;

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col != null && !_col.isTrigger)
            _col.isTrigger = true;

        if (doorController == null)
            doorController = GetComponent<CastleDoorController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (IsQuestCompleted()) return;

        if (delay > 0f)
            Invoke(nameof(Close), delay);
        else
            Close();
    }

    private void Close()
    {
        if (IsQuestCompleted()) return;
        doorController?.CloseDoors();
    }

    private bool IsQuestCompleted()
    {
        if (blockingQuest == null || QuestManager.Instance == null) return false;
        return QuestManager.Instance.GetState(blockingQuest.questId) != QuestState.Inactive;
    }
}
