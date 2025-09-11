using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SavePoint : MonoBehaviour
{
    [Header("Config")]
    public string anchorIdToSet;        // si lo dejas vacío, conserva el actual
    public bool healOnSave = true;
    public bool teleportAfterSave;
    public string teleportAnchorId;

    [Header("Interacción")]
    public KeyCode interactKey = KeyCode.E;
    public string prompt = "Guardar partida (E)";

    CanvasGroup _promptCg;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        ShowPrompt(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        ShowPrompt(false);
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (Input.GetKeyDown(interactKey)) DoSave(other);
    }

    void DoSave(Collider playerCol)
    {
        var ps = playerCol.GetComponent<PlayerState>() ?? playerCol.GetComponentInParent<PlayerState>();
        if (!ps) return;

        if (!string.IsNullOrEmpty(anchorIdToSet))
            SpawnManager.SetCurrentAnchor(anchorIdToSet);

        if (healOnSave)
        {
            ps.SetHealth(ps.MaxHealth);
            ps.SetMana(ps.MaxMana);
        }

        var save = FindFirstObjectByType<SaveSystem>();
        if (save) save.Save(PlayerSaveData.From(ps));
        
        if (teleportAfterSave && !string.IsNullOrEmpty(teleportAnchorId))
            TeleportService.TeleportToAnchor(ps.gameObject, teleportAnchorId);
    }

    void ShowPrompt(bool show)
    {
        // opcional: si tienes un Canvas local con CanvasGroup para el prompt
        if (!_promptCg) _promptCg = GetComponentInChildren<CanvasGroup>(true);
        if (_promptCg){ _promptCg.alpha = show ? 1f : 0f; _promptCg.blocksRaycasts = show; }
        // si no, pon aquí tu llamada a la UI global (TextMeshPro).
        if (show) Debug.Log(prompt);
    }
}