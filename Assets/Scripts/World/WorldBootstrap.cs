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

            // IMPORTANTE: NO tocamos la magia aquí. Los slots se configuran en World
            // (MagicLoadout del Player) de forma manual o por prefab.
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
            if (ps) data.ApplyTo(ps);
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

        // Slots de magia: se dejan tal cual estén en la escena/prefab (MagicLoadout).
    }
}