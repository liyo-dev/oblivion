using UnityEngine;

public class WorldBootstrap : MonoBehaviour
{
    [Header("Perfil de arranque (SO)")]
    public GameBootProfile profile;

    private SaveSystem saveSystem;

    private void Start()
    {
        saveSystem = FindFirstObjectByType<SaveSystem>();

        // 1) Modo PRESET (test): ignora el save
        if (profile && profile.ShouldBootFromPreset())
        {
            var ps = FindFirstObjectByType<PlayerState>();
            if (ps) profile.ApplyBootPreset(ps);

            var anchor = profile.GetStartAnchorOrDefault();
            SpawnManager.SetCurrentAnchor(anchor);

            var playerGO = ps ? ps.gameObject : FindFirstObjectByType<PlayerState>()?.gameObject;
            if (playerGO) TeleportService.PlaceAtAnchor(playerGO, anchor, immediate: true);

            // PRESET forzado: no necesitamos runtimePreset, el servicio leerá bootPreset.
            return;
        }

        // 2) Flujo normal: intentar cargar partida
        string anchorId = profile ? profile.defaultAnchorId : "Bedroom";
        if (string.IsNullOrEmpty(anchorId)) anchorId = "Bedroom";

        if (saveSystem != null && saveSystem.Load(out var data))
        {
            if (!string.IsNullOrEmpty(data.lastSpawnAnchorId))
                anchorId = data.lastSpawnAnchorId;

            var ps = FindFirstObjectByType<PlayerState>();
            if (ps) ps.LoadFromSave(data);

            // ==== NUEVO: reflejar save en runtimePreset (para que los servicios lo lean) ====
            if (profile)
            {
                var slotTemplate = profile.bootPreset ? profile.bootPreset : profile.defaultPlayerPreset;
                profile.SetRuntimePresetFromSave(data, slotTemplate);
            }
        }

        // 3) Colocar jugador
        var player = FindFirstObjectByType<PlayerState>()?.gameObject;
        if (player)
        {
            TeleportService.PlaceAtAnchor(player, anchorId, immediate: true);
        }
        else
        {
            Debug.LogWarning("[WorldBootstrap] No se encontró PlayerState en la escena.");
        }
    }
}
