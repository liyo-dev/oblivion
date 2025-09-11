using System.Collections;
using System.IO;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [System.Serializable]
    public class SpawnAnchor
    {
        public string id;
        public Transform transform;
    }
    
    [SerializeField] GameBootProfile bootProfile;
    [SerializeField] PlayerState playerState;
    [SerializeField] GameConfigSO config;
    [SerializeField] SpawnAnchor[] spawnAnchors;

    public static string CurrentAnchorId { get; private set; }
    public static void SetCurrentAnchor(string id){ CurrentAnchorId = id; }

    void Awake()
    {
        if (!playerState) playerState = FindFirstObjectByType<PlayerState>();
        if (!config)      config      = Resources.Load<GameConfigSO>("GameConfig");
    }

    void Start() { StartCoroutine(Co_Boot()); }

    IEnumerator Co_Boot()
    {
        if (bootProfile && bootProfile.ShouldBootFromPreset())
        {
            var preset = bootProfile.GetPreset();
            var anchor = bootProfile.GetStartAnchorId();
            if (preset) playerState.ApplyPreset(preset);
            PlaceAtAnchor(playerState.gameObject, string.IsNullOrEmpty(anchor) ? GetDefaultSpawnAnchor() : anchor, true);
            yield break;
        }

        var save = LoadGameData();
        if (save != null)
        {
            save.ApplyTo(playerState);
            var anchor = string.IsNullOrEmpty(save.lastSpawnAnchorId) ? GetDefaultSpawnAnchor() : save.lastSpawnAnchorId;
            PlaceAtAnchor(playerState.gameObject, anchor, true);
            yield break;
        }

        // fallback: preset runtime o default
        var rp = bootProfile ? bootProfile.runtimePreset : null;
        if (rp) playerState.ApplyPreset(rp);
        PlaceAtAnchor(playerState.gameObject, GetDefaultSpawnAnchor(), true);
    }

    public void SaveNow()
    {
        var data = PlayerSaveData.From(playerState);
        SaveGameData(data);
    }

    public Transform GetAnchor(string anchorId)
    {
        if (spawnAnchors != null)
        {
            foreach (var anchor in spawnAnchors)
            {
                if (anchor.id == anchorId)
                    return anchor.transform;
            }
        }
        return null;
    }

    public void PlaceAtAnchor(GameObject player, string anchorId, bool setAsCurrent = false)
    {
        var anchor = GetAnchor(anchorId);
        if (anchor != null)
        {
            player.transform.position = anchor.position;
            player.transform.rotation = anchor.rotation;
        }
        else
        {
            // Fallback a posición por defecto
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            Debug.LogWarning($"No se encontró anchor '{anchorId}', usando posición por defecto");
        }
        
        if (setAsCurrent)
        {
            SetCurrentAnchor(anchorId);
        }
        
        Debug.Log($"Jugador colocado en: {anchorId}");
    }

    private string GetDefaultSpawnAnchor()
    {
        // Usar valor por defecto si config no está disponible
        return config != null ? "Bedroom" : "Bedroom";
    }

    // Sistema de guardado integrado
    private void SaveGameData(PlayerSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(Application.persistentDataPath, "savegame.json");
            File.WriteAllText(path, json);
            Debug.Log($"Juego guardado en: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al guardar: {e.Message}");
        }
    }

    private PlayerSaveData LoadGameData()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "savegame.json");
            if (!File.Exists(path))
            {
                Debug.Log("No se encontró archivo de guardado");
                return null;
            }

            string json = File.ReadAllText(path);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);
            Debug.Log("Juego cargado exitosamente");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al cargar: {e.Message}");
            return null;
        }
    }
}