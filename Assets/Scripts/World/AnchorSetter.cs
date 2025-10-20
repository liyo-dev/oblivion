using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AnchorSetter : MonoBehaviour
{
    public string anchorId;
    public bool saveAfter = true;

    private bool _pendingSet;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_pendingSet)
        {
            ApplyAnchorAndMaybeSave();
            _pendingSet = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameBootService.IsAvailable)
        {
            ApplyAnchorAndMaybeSave();
        }
        else
        {
            // Diferir hasta que el GameBootProfile esté listo
            _pendingSet = true;
        }
    }

    private void ApplyAnchorAndMaybeSave()
    {
        if (string.IsNullOrEmpty(anchorId)) return;

        SpawnManager.SetCurrentAnchor(anchorId);
        Debug.Log($"[AnchorSetter] Anchor establecido a: {anchorId}");

        if (!saveAfter) return;
        if (!GameBootService.IsAvailable) return;

        var profile = GameBootService.Profile;
        if (profile != null && !profile.allowAutoSaves)
        {
            Debug.Log("[AnchorSetter] Auto-guardado omitido (allowAutoSaves = false).");
            return;
        }

        // Guardar estado actualizando el lastSpawnAnchorId
        var data = PlayerSaveData.FromGameBootProfile();
        data.lastSpawnAnchorId = anchorId;

        var saveSystem = FindFirstObjectByType<SaveSystem>();
        if (saveSystem != null)
        {
            saveSystem.Save(data);
            Debug.Log("[AnchorSetter] Partida guardada tras cambiar de anchor");
        }
        else
        {
            Debug.LogWarning("[AnchorSetter] No se encontró SaveSystem para guardar");
        }
    }
}
