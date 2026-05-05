using UnityEngine;

/// <summary>
/// Cierra la puerta del castillo cuando el jugador cruza al interior.
/// Colocar en el mismo GameObject que CastleDoorController, con un
/// BoxCollider trigger posicionado en el umbral interior de la puerta.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CastleDoorCloseTrigger : MonoBehaviour
{
    [Tooltip("Referencia al CastleDoorController que controla la puerta")]
    [SerializeField] private CastleDoorController doorController;

    [Tooltip("Segundos de espera antes de cerrar (da tiempo al jugador a cruzar)")]
    [SerializeField] private float delay = 0.5f;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (doorController == null)
            doorController = GetComponent<CastleDoorController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (delay > 0f)
            Invoke(nameof(Close), delay);
        else
            Close();
    }

    private void Close() => doorController?.CloseDoor();
}
