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

        _saveSystem = FindFirstObjectByType<SaveSystem>();

        // 1) Modo PRESET (test): ignora el save
        if (bootProfile.ShouldBootFromPreset())
        {
            var anchor = bootProfile.GetStartAnchorOrDefault();
            SpawnManager.SetCurrentAnchor(anchor);

            StartCoroutine(WaitForPlayerAndTeleport(anchor));

            Debug.Log("[WorldBootstrap] Iniciado en modo PRESET");
            return;
        }

        // 2) Flujo normal: intentar cargar partida; si no, usar anchor del preset activo
        string anchorId = bootProfile.GetStartAnchorOrDefault();

        if (_saveSystem != null && _saveSystem.Load(out var data))
        {
            if (!string.IsNullOrEmpty(data.lastSpawnAnchorId))
                anchorId = data.lastSpawnAnchorId;

            // Actualizar runtimePreset con los datos del save
            var slotTemplate = bootProfile.bootPreset ? bootProfile.bootPreset : bootProfile.defaultPlayerPreset;
            bootProfile.SetRuntimePresetFromSave(data, slotTemplate);

            Debug.Log("[WorldBootstrap] Save cargado correctamente");
        }
        else
        {
            Debug.Log("[WorldBootstrap] Sin save disponible, usando configuración por defecto");
        }

        // 3) Colocar jugador (esperar a que esté disponible y activo)
        SpawnManager.SetCurrentAnchor(anchorId);
        StartCoroutine(WaitForPlayerAndTeleport(anchorId));
    }

    private System.Collections.IEnumerator WaitForPlayerAndTeleport(string anchorId)
    {
        GameObject player = null;
        int maxAttempts = 100;
        int attempts = 0;

        // Buscar al jugador (incluso si está desactivado)
        while (player == null && attempts < maxAttempts)
        {
            try
            {
                player = GameObject.FindWithTag("Player");
            }
            catch (UnityException) { }
            
            if (player == null)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    try
                    {
                        if (obj != null && obj.CompareTag("Player") && obj.scene.isLoaded && !string.IsNullOrEmpty(obj.scene.name))
                        {
                            player = obj;
                            break;
                        }
                    }
                    catch (UnityException) { }
                }
            }

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
