using UnityEngine;

/// <summary>
/// Cierra las puertas del castillo cuando el jugador cruza al interior.
/// Si el requisito de quest configurado se cumple, el trigger se ignora y las puertas permanecen abiertas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CastleDoorCloseTrigger : MonoBehaviour
{
    [SerializeField] private CastleDoorController doorController;

    [Tooltip("Segundos de espera antes de cerrar (da tiempo al jugador a cruzar).")]
    [SerializeField] private float delay = 0.5f;

    [Header("Bloqueo por misión")]
    [Tooltip("Si el requisito se cumple, el trigger NO cerrará las puertas.")]
    [SerializeField] private QuestRequirement questGate;

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
        if (questGate.IsConfigured && questGate.IsSatisfied()) return;

        if (delay > 0f)
            Invoke(nameof(Close), delay);
        else
            Close();
    }

    private void Close()
    {
        if (questGate.IsConfigured && questGate.IsSatisfied()) return;
        doorController?.CloseDoors();
    }
}
