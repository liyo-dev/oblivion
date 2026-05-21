using UnityEngine;

public class CastleDoorController : MonoBehaviour
{
    [SerializeField] private DoorGate leftDoor;
    [SerializeField] private DoorGate rightDoor;

    [Tooltip("Si true, las puertas arrancan cerradas al iniciar la escena")]
    [SerializeField] private bool startsLocked = true;

    void Awake()
    {
        if (startsLocked)
        {
            leftDoor?.Close();
            rightDoor?.Close();
        }
    }

    public void OpenDoors()
    {
        leftDoor?.Open();
        rightDoor?.Open();
    }

    public void CloseDoors()
    {
        leftDoor?.Close();
        rightDoor?.Close();
    }
}
