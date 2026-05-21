using UnityEngine;

/// <summary>
/// Cierra las puertas del castillo cuando el jugador cruza al interior.
/// Colocar un BoxCollider trigger en el umbral interior de las puertas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CastleDoorCloseTrigger : MonoBehaviour
{
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

    private void Close() => doorController?.CloseDoors();
}
