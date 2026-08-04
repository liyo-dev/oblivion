using UnityEngine;

/// <summary>
/// Trigger que detecta cuando el jugador cruza la zona de salida del minijuego.
/// Coloca en un BoxCollider trigger en el borde del área del reino.
/// Llama a TagMinigameController.PlayerExitedKingdom() para completar la huida.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MinigameExitTrigger : MonoBehaviour
{
    [Tooltip("ID del minijuego (coincide con TagMinigameController.minigameId)")]
    [SerializeField] private string minigameId = "TAG_MINIGAME_01";

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var controller = FindMinigameController();
        if (controller != null)
            controller.PlayerExitedKingdom();
    }

    private TagMinigameController FindMinigameController()
    {
        var all = FindObjectsByType<TagMinigameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c.MinigameId == minigameId)
                return c;
        }
        Debug.LogWarning($"[MinigameExitTrigger] No se encontró TagMinigameController con id='{minigameId}'");
        return null;
    }
}
