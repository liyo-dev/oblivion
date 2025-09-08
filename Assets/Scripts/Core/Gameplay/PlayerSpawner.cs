// PlayerSpawner.cs (en World_Open)
using UnityEngine;

public class PlayerSpawner : MonoBehaviour, IGameService
{
    [SerializeField] GameObject playerPrefab;
    [SerializeField] string defaultSpawnId = "village_gate";

    public void Initialize()
    {
        var save = ServiceLocator.Get<ISaveManager>();
        string id = save.HasSave ? save.GetLastSpawnId() : defaultSpawnId;

        var target = FindSpawn(id);
        var player = Object.Instantiate(playerPrefab, target.position, target.rotation);
        // opcional: registrar referencia global del Player si la necesitas
    }

    public void Dispose() { }

    Transform FindSpawn(string id)
    {
        foreach (var sp in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (sp.spawnId == id) return sp.transform;

        // fallback: primero
        return FindFirstObjectByType<SpawnPoint>().transform;
    }
}