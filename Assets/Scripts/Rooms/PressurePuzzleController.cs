using UnityEngine;

public class PressurePuzzleController : MonoBehaviour
{
    public PressurePlate[] plates;
    public RoomGoal roomGoal;
    public DoorGate exitDoor;

    [Header("Audio")]
    [Tooltip("Clave SFX que se reproduce al completar el puzzle. Vacío = sin sonido.")]
    [SerializeField] private string solvedSfxKey = "puzzle_done";

    void Awake()
    {
        if (plates == null || plates.Length == 0)
            plates = GetComponentsInChildren<PressurePlate>(true);
        // Sin SFX en el chequeo inicial: si la sala ya estaba resuelta al cargar, no debe sonar
        CheckSolved(playSfx: false);
    }

    // Llamado por las placas vía SendMessageUpwards
    void OnPlateStateChanged(PressurePlate _)
    {
        CheckSolved(playSfx: true);
    }

    void CheckSolved(bool playSfx)
    {
        foreach (var p in plates)
            if (p && !p.isPressed) return;

        // todas pulsadas
        if (playSfx && !string.IsNullOrEmpty(solvedSfxKey))
            AudioService.Instance?.PlaySFX(solvedSfxKey, worldPosition: transform.position);
        roomGoal?.MarkCleared();
        exitDoor?.Open();
        enabled = false;
    }
}
