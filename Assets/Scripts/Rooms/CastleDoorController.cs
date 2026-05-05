using UnityEngine;

/// <summary>
/// Abre la puerta del castillo cuando el jugador entra en la zona trigger.
/// Trabaja junto a CastleDoorCloseTrigger (en el mismo GameObject) para
/// cerrar la puerta una vez el jugador ha cruzado.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CastleDoorController : MonoBehaviour
{
    [Tooltip("Puerta a controlar (GameObject con DoorGate o cualquier objeto a activar/desactivar)")]
    [SerializeField] private DoorGate door;

    [Tooltip("Si true, la puerta arranca cerrada al iniciar la escena")]
    [SerializeField] private bool startsLocked = true;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        if (door != null && startsLocked)
            door.Close();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (door != null)
            door.Open();
    }

    public void OpenDoor()  => door?.Open();
    public void CloseDoor() => door?.Close();
}
