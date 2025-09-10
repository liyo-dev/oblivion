using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public GameConfigSO config;
    public PlayerState  playerState;
    public QuestLog     questLog;

    // TeleportService usará esto para saber "dónde reaparecer" tras cambios
    public static string CurrentAnchorId { get; private set; }

    void Awake()
    {
        if (!playerState) playerState = FindFirstObjectByType<PlayerState>();
        if (!questLog)     questLog   = FindFirstObjectByType<QuestLog>();

        // 1) Cargar o crear save runtime
        PlayerSaveData save = SaveSystem.Load();
        if (save == null)
        {
            var preset = playerState.newGamePreset;
            string spawnId = config ? config.defaultSpawnAnchorId : "Bedroom";
            save = PlayerSaveData.FromPreset(preset, spawnId);
            if (preset==null) Debug.LogWarning("No PlayerPresetSO asignado. Usando valores por defecto.");
        }

        // 2) Aplicar al player
        playerState.LoadFromSave(save);
        if (questLog) questLog.Import(save.quests);

        // 3) Recolocar al anchor pedido (tu Player YA está en escena)
        string id = string.IsNullOrEmpty(save.lastSpawnAnchorId) ? (config?config.defaultSpawnAnchorId:"Bedroom") : save.lastSpawnAnchorId;
        TeleportService.PlaceAtAnchor(playerState.gameObject, id, immediate:true);
        CurrentAnchorId = id;
    }

    public void SaveNow()
    {
        var data = playerState.CreateSave(CurrentAnchorId, questLog);
        SaveSystem.Save(data);
    }

    public static void SetCurrentAnchor(string id){ CurrentAnchorId = id; }
}