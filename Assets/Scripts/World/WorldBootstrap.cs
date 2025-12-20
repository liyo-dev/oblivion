using UnityEngine;
using Game.NPC; // NPCBehaviourManagerV2

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
        // Si el checkbox de testeo está marcado, ignoramos cualquier save existente
        if (bootProfile.ShouldBootFromPreset())
        {
            // Reforzar que el runtimePreset sea el bootPreset (por si se modificó)
            bootProfile.EnsureRuntimePresetFromTemplate(bootProfile.bootPreset);
            
            var anchor = bootProfile.GetStartAnchorOrDefault();
            SpawnManager.SetCurrentAnchor(anchor);

            // IMPORTANTE: Resetear quests para que usen SOLO los flags del preset de testeo
            var qm = QuestManager.Instance;
            if (qm != null)
            {
                var preset = bootProfile.GetActivePresetResolved();
                if (preset != null)
                {
                    qm.RestoreFromProfileFlags(preset.flags);
                    Debug.Log($"[WorldBootstrap] Quest restauradas desde preset de testeo (flags count: {preset.flags?.Count ?? 0})");
                }
            }

            StartCoroutine(WaitForPlayerAndTeleport(anchor));

            Debug.Log("[WorldBootstrap] Iniciado en modo PRESET (testing) - Se ignora cualquier save existente");
            return;
        }

        // 2) Flujo normal: intentar cargar partida; si no, usar anchor del preset activo
        string anchorId = bootProfile.GetStartAnchorOrDefault();

        if (_saveSystem != null && _saveSystem.Load(out var data))
        {
            if (!string.IsNullOrEmpty(data.lastSpawnAnchorId))
                anchorId = data.lastSpawnAnchorId;

            // Actualizar runtimePreset con los datos del save
            bootProfile.SetRuntimePresetFromSave(data);

            // Reubicar NPCs desde el SO si hay entradas persistidas
            TryApplyNpcPositionsFromPreset(bootProfile);

            // Aplicar el preset recién cargado al jugador (incluye inventario y apariencia)
            // para evitar que queden valores por defecto hasta el próximo cambio de escena.
            if (PlayerService.TryGetComponent<PlayerPresetService>(out var presetService, includeInactive: true, allowSceneLookup: true))
            {
                presetService.ApplyCurrentPreset(includeInventory: true);
            }
            else
            {
                // Fallback: obtener desde el ServiceLocator
                var svc = ServiceLocator.Get<PlayerPresetService>(false);
                if (svc != null)
                    svc.ApplyCurrentPreset(includeInventory: true);
            }

            Debug.Log("[WorldBootstrap] Save cargado correctamente");
        }
        else
        {
            Debug.Log("[WorldBootstrap] Sin save disponible, usando configuración por defecto");
            // Nueva partida efectiva: asegurar estado limpio de quests
            var qm = QuestManager.Instance; if (qm != null) qm.ResetAllQuests();
        }

        // 3) Colocar jugador (esperar a que esté disponible y activo)
        SpawnManager.SetCurrentAnchor(anchorId);
        StartCoroutine(WaitForPlayerAndTeleport(anchorId));
    }

    void TryApplyNpcPositionsFromPreset(GameBootProfile bootProfile)
    {
        if (bootProfile == null) return;
        var preset = bootProfile.GetActivePresetResolved();
        if (preset == null || preset.npcPositions == null || preset.npcPositions.Count == 0) return;

        for (int i = 0; i < preset.npcPositions.Count; i++)
        {
            var entry = preset.npcPositions[i];
            if (string.IsNullOrWhiteSpace(entry.npcId)) continue;

            GameObject go = null;
            try { go = GameObject.Find(entry.npcId); } catch { }
            if (go == null) continue;

            var mgr = go.GetComponent<NPCBehaviourManagerV2>();
            if (mgr == null) continue;
            
            // Aplicar estado activo/inactivo solo si se guardó explícitamente
            if (entry.hasActiveState && !entry.isActive)
            {
                go.SetActive(false);
                Debug.Log($"[WorldBootstrap] NPC '{entry.npcId}' desactivado según preset");
                continue; // No aplicar posición si está desactivado
            }
            
            // Solo aplicar posición si el NPC tiene persistencia habilitada
            if (!mgr.persistLastPosition) continue;

            mgr.lastPosition = entry.position;
            if (mgr.isActiveAndEnabled)
            {
                mgr.ApplyLastPositionIfNeeded();
            }
        }
    }

    private System.Collections.IEnumerator WaitForPlayerAndTeleport(string anchorId)
    {
        GameObject player = null;
        int maxAttempts = 100;
        int attempts = 0;

        // Buscar al jugador (incluso si está desactivado)
        while (player == null && attempts < maxAttempts)
        {
            // Intentar obtener el objeto Player desde el ServiceLocator
            player = ServiceLocator.Get<GameObject>(false);
            if (player == null)
            {
                yield return new WaitForSeconds(0.05f);
                attempts++;
            }
        }

        if (player == null)
        {
            Debug.LogError("[WorldBootstrap] No se encontró el jugador con tag 'Player'.");
            yield break;
        }

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
            SpawnManager.TeleportTo(anchorId, false);
        }
        else
        {
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

