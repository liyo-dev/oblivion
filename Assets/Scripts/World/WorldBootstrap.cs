using UnityEngine;

public class WorldBootstrap : MonoBehaviour
{
    private SaveSystem _saveSystem;
    private bool _initialized;

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;
        // Usar corutina para dar tiempo a que los SpawnAnchor se registren en OnEnable
        StartCoroutine(InitializeWorldDelayed());
        _initialized = true;
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private System.Collections.IEnumerator InitializeWorldDelayed()
    {
        // Esperar un frame para que todos los OnEnable de los SpawnAnchor se ejecuten
        yield return null;
        InitializeWorld();
    }

    private void InitializeWorld()
    {
        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
            Debug.LogError("[WorldBootstrap] ¡No se encontró GameBootProfile en GameBootService!");
            return;
        }

        _saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        // 1) Modo PRESET (test): SIEMPRE tiene prioridad sobre saves
        if (bootProfile.ShouldBootFromPreset())
        {
            bootProfile.EnsureRuntimePresetFromTemplate(bootProfile.bootPreset);
            
            var anchor = bootProfile.GetStartAnchorOrDefault();
            SpawnManager.SetCurrentAnchor(anchor);

            var qm = QuestManager.Instance;
            if (qm != null)
            {
                var preset = bootProfile.GetActivePresetResolved();
                if (preset != null)
                {
                    qm.RestoreFromProfileFlags(preset.flags);
                }
            }

            // IMPORTANTE: NO aplicar posiciones de NPCs aquí cuando viene de modo preset
            // GameBootService.ApplyPresetAsLoadedGame() ya las aplicó correctamente
            // Si las aplicamos de nuevo aquí, causamos conflictos (doble aplicación)
            // Debug.Log("[WorldBootstrap] Modo PRESET - Posiciones de NPCs ya aplicadas por GameBootService");

            StartCoroutine(WaitForPlayerAndTeleport(anchor));
            // Debug.Log("[WorldBootstrap] Iniciado en modo PRESET (testing)");
            return;
        }

        // 2) Flujo normal: si hay save → cargarlo; si no → usar defaultPreset
        string anchorId = bootProfile.GetStartAnchorOrDefault();

        if (_saveSystem != null && _saveSystem.Load(out var data))
        {
            // HAY SAVE → usar anchor del save
            if (!string.IsNullOrEmpty(data.lastSpawnAnchorId))
                anchorId = data.lastSpawnAnchorId;

            bootProfile.SetRuntimePresetFromSave(data);
            
            // Aplicar posiciones de NPCs usando el método robusto de GameBootProfile
            // Esto asegura consistencia con el modo testing
            var preset = bootProfile.GetActivePresetResolved();
            if (preset != null)
            {
                bootProfile.ApplyNpcPositionsToScene(preset);
            }

            if (PlayerService.TryGetComponent<PlayerPresetService>(out var presetService, includeInactive: true, allowSceneLookup: true))
                presetService.ApplyCurrentPreset(includeInventory: true);
            else
            {
                var svc = ServiceLocator.Get<PlayerPresetService>(false);
                if (svc != null) svc.ApplyCurrentPreset(includeInventory: true);
            }

            // Debug.Log($"[WorldBootstrap] Save cargado → Anchor: '{anchorId}'");
        }
        else
        {
            // NO HAY SAVE → usar anchor del defaultPreset (ya en runtimePreset)
            // Debug.Log($"[WorldBootstrap] Sin save → Anchor del preset: '{anchorId}'");
            
            var qm = QuestManager.Instance;
            if (qm != null) qm.ResetAllQuests();
            
            if (PlayerService.TryGetComponent<PlayerPresetService>(out var presetService, includeInactive: true, allowSceneLookup: true))
                presetService.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
            else
            {
                var svc = ServiceLocator.Get<PlayerPresetService>(false);
                if (svc != null) svc.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
            }
            
            // Aplicar posiciones de NPCs usando el método robusto de GameBootProfile
            var preset = bootProfile.GetActivePresetResolved();
            if (preset != null)
            {
                bootProfile.ApplyNpcPositionsToScene(preset);
            }
        }

        // 3) Colocar jugador
        SpawnManager.SetCurrentAnchor(anchorId);
        StartCoroutine(WaitForPlayerAndTeleport(anchorId));
    }


    private System.Collections.IEnumerator WaitForPlayerAndTeleport(string anchorId)
    {
        GameObject player = null;
        int maxAttempts = 100;
        int attempts = 0;

        // Buscar al jugador usando PlayerService
        while (player == null && attempts < maxAttempts)
        {
            player = PlayerService.Player;
            if (player == null)
            {
                yield return new WaitForSeconds(0.05f);
                attempts++;
            }
        }

        if (player == null)
        {
            Debug.LogError("[WorldBootstrap] No se encontró el jugador via PlayerService.");
            yield break;
        }

        // Debug.Log($"[WorldBootstrap] 🎮 Jugador encontrado: {player.name}, teletransportando a '{anchorId}'");

        // Esperar a que el jugador esté activo
        attempts = 0;
        while (!player.activeInHierarchy && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.05f);
            attempts++;
        }

        // Teleportar al jugador
        if (player.activeInHierarchy)
        {
            // Debug.Log($"[WorldBootstrap] ✅ Ejecutando TeleportTo('{anchorId}')");
            SpawnManager.TeleportTo(anchorId, false);
        }
        else
        {
            Debug.LogWarning($"[WorldBootstrap] ⚠️ Jugador no activo, programando teleport diferido a '{anchorId}'");
            SpawnManager.SetCurrentAnchor(anchorId);
            StartCoroutine(TeleportWhenActive(player, anchorId));
        }
    }

    private System.Collections.IEnumerator TeleportWhenActive(GameObject player, string anchorId)
    {
        int maxAttempts = 200;
        int attempts = 0;

        while (player != null && !player.activeInHierarchy && attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.05f);
            attempts++;
        }

        if (player != null && player.activeInHierarchy)
        {
            SpawnManager.TeleportTo(anchorId, false);
        }
    }
}

