using UnityEngine;

public class WorldBootstrap : MonoBehaviour
{
    [Header("Opcional (solo para fallback)")]
    public GameBootProfile profile;  // si quieres tomar defaultAnchorId desde aquí
    public GameConfigSO gameConfig;  // si prefieres usar su Default Spawn Anchor Id

    SaveSystem saveSystem;

    void Start()
    {
        saveSystem = FindFirstObjectByType<SaveSystem>();

        // 1) Resolver anchor por defecto (si no hay save)
        string fallbackAnchor = ResolveFallbackAnchorId(); // "start" / "Bedroom" según tengas

        // 2) Intentar cargar partida
        string anchorId = fallbackAnchor;
        if (saveSystem != null && saveSystem.Load(out var data) && !string.IsNullOrEmpty(data.lastSpawnAnchorId))
        {
            anchorId = data.lastSpawnAnchorId;

            // Restaura stats/flags/abilities al Player
            var ps = FindFirstObjectByType<PlayerState>();
            if (ps) data.ApplyTo(ps);
        }

        // 3) Colocar al jugador en el anchor resuelto
        var player = FindFirstObjectByType<PlayerState>()?.gameObject;
        if (player)
        {
            TeleportService.PlaceAtAnchor(player, anchorId, immediate: true);
            // PlaceAtAnchor ya hace SpawnManager.SetCurrentAnchor(anchorId).
        }
        else
        {
            Debug.LogWarning("[WorldBootstrap] No se encontró PlayerState en la escena.");
        }
    }

    string ResolveFallbackAnchorId()
    {
        // Prioridad: GameConfigSO > GameBootProfile > "start"
        if (gameConfig && !string.IsNullOrEmpty(gameConfig.defaultSpawnAnchorId))
            return gameConfig.defaultSpawnAnchorId;

        if (profile && !string.IsNullOrEmpty(profile.defaultAnchorId))
            return profile.defaultAnchorId;

        return "start";
    }
}