using UnityEngine;

public class PuzzleTorchGateController : MonoBehaviour
{
    [Header("Refs")]
    public TorchInteract[] torches;
    public RoomGoal roomGoal;          // el de la sala
    public DoorGate exitDoor;          // puerta Este a abrir (opcional si escuchas RoomGoal fuera)

    [Header("Lógica")]
    public int requiredLit = 2;        // cuántas encendidas para abrir

    [Header("Audio")]
    [Tooltip("Clave SFX que se reproduce al completar el puzzle. Vacío = sin sonido.")]
    [SerializeField] private string solvedSfxKey = "puzzle_done";

    int currentLit;

    void Awake()
    {
        currentLit = 0;
        foreach (var t in torches)
        {
            if (!t) continue;
            if (t.isLit) currentLit++;
            t.onTorchToggled += OnTorchToggled;
        }
        // Sin SFX en el chequeo inicial: si la sala ya estaba resuelta al cargar, no debe sonar
        CheckSolved(playSfx: false);
    }

    void OnDestroy()
    {
        foreach (var t in torches) if (t) t.onTorchToggled -= OnTorchToggled;
    }

    void OnTorchToggled(bool nowLit)
    {
        currentLit += nowLit ? 1 : -1;
        CheckSolved(playSfx: true);
    }

    void CheckSolved(bool playSfx)
    {
        if (currentLit >= requiredLit)
        {
            if (playSfx && !string.IsNullOrEmpty(solvedSfxKey))
                AudioService.Instance?.PlaySFX(solvedSfxKey, worldPosition: transform.position);
            roomGoal?.MarkCleared();
            if (exitDoor) exitDoor.Open();
            enabled = false;
        }
    }
}
