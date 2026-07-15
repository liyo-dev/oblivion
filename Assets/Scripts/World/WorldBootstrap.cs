using UnityEngine;

/// <summary>
/// Inicializa el mundo cuando MainWorld se carga.
/// Se ejecuta DESPUÉS de SpawnManager para usar el anchor ya establecido.
/// </summary>
[DefaultExecutionOrder(200)]
public class WorldBootstrap : MonoBehaviour
{
    private bool _initialized;

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(WorldBootstrap));
        
        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
        else
        {
            // ✅ En editor, Start puede cargarse aditivamente - Esperar un momento antes de asumir que no existe
            #if UNITY_EDITOR
            Debug.Log("[WorldBootstrap] ⏳ GameBootService no disponible aún - Esperando por si Start se carga aditivamente...");
            StartCoroutine(WaitForGameBootServiceOrFallback());
            #else
            // En build, si no hay GameBootService es un error grave - no se puede continuar
            Debug.LogError("[WorldBootstrap] ❌ FATAL: GameBootService no disponible en build. La escena 'Start' debe cargarse primero.");
            #endif
        }
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// En editor, espera un tiempo razonable por si Start se está cargando aditivamente.
    /// Si después de esperar no hay GameBootService, usa valores por defecto.
    /// </summary>
    private System.Collections.IEnumerator WaitForGameBootServiceOrFallback()
    {
        int framesWaited = 0;
        
        // Esperar indefinidamente hasta que GameBootService esté disponible
        // (con timeout de seguridad de 30 segundos para detectar errores graves)
        while (!GameBootService.IsAvailable)
        {
            yield return null;
            framesWaited++;
            
            // Timeout de seguridad: 1800 frames = 30 segundos a 60 FPS
            if (framesWaited > 1800)
            {
                Debug.LogError($"[WorldBootstrap] ❌ FATAL: GameBootService no disponible después de 30 segundos ({framesWaited} frames)");
                Debug.LogError($"[WorldBootstrap] ❌ Verifica que la escena 'Start' esté en Build Settings y se haya cargado correctamente");
                Debug.LogError($"[WorldBootstrap] ❌ AutoBootstrapOnPlay debe haber cargado 'Start' aditivamente antes de PlayMode");
                yield break;
            }
        }
        
        Debug.Log($"[WorldBootstrap] ✅ GameBootService disponible después de {framesWaited} frame(s) - Inicializando normalmente");
        HandleProfileReady();
    }
    #endif

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
        Debug.Log($"[WorldBootstrap] 🌍 InitializeWorld() - Iniciando configuración del mundo");
        
        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
            Debug.LogError("[WorldBootstrap] ¡No se encontró GameBootProfile en GameBootService!");
            return;
        }

        Debug.Log($"[WorldBootstrap] Profile encontrado - ShouldBootFromPreset: {bootProfile.ShouldBootFromPreset()}");

        // 1) Modo PRESET (test): SIEMPRE tiene prioridad sobre saves
        if (bootProfile.ShouldBootFromPreset())
        {
            bootProfile.EnsureRuntimePresetFromTemplate(bootProfile.bootPreset);

            // ✅ El anchor ya fue establecido por SpawnManager.HandleProfileReady()
            var anchor = bootProfile.GetStartAnchorOrDefault();
            Debug.Log($"[WorldBootstrap] 📍 Modo PRESET - Anchor desde profile: '{anchor}', CurrentAnchorId: '{SpawnManager.CurrentAnchorId}'");

            var testPreset = bootProfile.GetActivePresetResolved();
            if (testPreset != null)
            {
                // Restaurar quests
                var qm = QuestManager.Instance;
                if (qm != null)
                {
                    qm.RestoreFromProfileFlags(testPreset.flags);
                }

                // ✅ CRÍTICO: Aplicar posiciones de NPCs AQUÍ (cuando MainWorld ya está cargada)
                // GameBootService.ApplyPresetAsLoadedGame() se ejecuta ANTES de que MainWorld cargue
                // En ese momento los NPCs no existen, por lo que NO se pueden posicionar
                // AHORA es el momento correcto porque MainWorld está cargada y los NPCs existen
                Debug.Log($"[WorldBootstrap] 🎯 Aplicando posiciones de {testPreset.npcPositions?.Count ?? 0} NPCs desde preset (modo testeo)");
                bootProfile.ApplyNpcPositionsToScene(testPreset);
            }

            // Aplicar entorno del anchor de spawn antes de esperar al jugador
            // Así el skybox/interior es correcto desde el primer frame visible
            ApplySpawnEnvironmentNow(anchor);
            StartCoroutine(WaitForPlayerAndTeleport(anchor));
            StartCoroutine(EnsureEnvironmentAfterCinematic(anchor));
            // Debug.Log("[WorldBootstrap] Iniciado en modo PRESET (testing)");
            return;
        }

        // 2) Flujo normal: El runtimePreset ya está configurado por GameBootService.PrepareActivePreset()
        // Simplemente aplicar el preset al jugador sin recargar el save
        string anchorId = bootProfile.GetStartAnchorOrDefault();
        Debug.Log($"[WorldBootstrap] 📍 Modo NORMAL - Anchor desde profile: '{anchorId}', CurrentAnchorId: '{SpawnManager.CurrentAnchorId}'");
        
        var runtimePreset = bootProfile.GetActivePresetResolved();
        if (runtimePreset != null)
        {
            // Aplicar posiciones de NPCs usando el método robusto de GameBootProfile
            bootProfile.ApplyNpcPositionsToScene(runtimePreset);
            
            // Aplicar el preset al jugador (incluye inventario y abilities)
            if (PlayerService.TryGetComponent<PlayerPresetService>(out var presetService, includeInactive: true, allowSceneLookup: true))
                presetService.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
            else
            {
                var svc = ServiceLocator.Get<PlayerPresetService>(false);
                if (svc != null) svc.ApplyCurrentPreset(includeInventory: true, includeAbilities: true);
            }
            
            // Aplicar estado de quests desde el preset
            var qm = QuestManager.Instance;
            if (qm != null) qm.RestoreFromProfileFlags(runtimePreset.flags);
            
            // Debug.Log($"[WorldBootstrap] Usando runtimePreset ya configurado → Anchor: '{anchorId}'");
        }

        // 3) Colocar jugador
        SpawnManager.SetCurrentAnchor(anchorId);
        // Aplicar entorno del anchor de spawn antes de esperar al jugador
        ApplySpawnEnvironmentNow(anchorId);
        StartCoroutine(WaitForPlayerAndTeleport(anchorId));
        StartCoroutine(EnsureEnvironmentAfterCinematic(anchorId));
    }

    private void ApplySpawnEnvironmentNow(string anchorId)
    {
        if (string.IsNullOrEmpty(anchorId)) return;
        var ec = EnvironmentController.Instance;
        if (ec == null) return;

        SpawnAnchor anchor = AnchorRegistry.Get(anchorId);
        if (anchor == null)
        {
            var all = FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var candidate in all)
            {
                if (candidate.anchorId == anchorId) { anchor = candidate; break; }
            }
        }

        if (anchor == null) { ec.ApplyExterior(); return; }

        var env = anchor.GetComponentInParent<AnchorEnvironment>(includeInactive: true);
        if (env != null && env.isInterior) ec.ApplyInterior(env);
        else ec.ApplyExterior();
    }


    // Si hay una cinemática aditiva en curso al cargar (ej: prólogo), su Unload() llama
    // SafeTeleportToCurrent() con el anchor de salida (exterior), lo que puede sobreescribir
    // el entorno interior del anchor guardado. Esta corrutina re-aplica el entorno correcto
    // un frame después de que la cinemática termine, mientras el fade-out aún cubre la pantalla.
    private System.Collections.IEnumerator EnsureEnvironmentAfterCinematic(string anchorId)
    {
        yield return null; // esperar a que AdditiveSceneCinematic.Start() inicialice su flag

        if (!AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying) yield break;

        while (AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying)
            yield return null;

        // Un frame extra para que SafeTeleportToCurrent() complete su ApplyExterior()
        yield return null;

        ApplySpawnEnvironmentNow(anchorId);
    }

    private System.Collections.IEnumerator WaitForPlayerAndTeleport(string anchorId)
    {
        GameObject player = null;
        int maxAttempts = 100;
        int attempts = 0;

        // Buscar al jugador usando PlayerService (con scene lookup para encontrarlo si empieza inactivo)
        while (player == null && attempts < maxAttempts)
        {
            PlayerService.TryGetPlayer(out player, allowSceneLookup: true);
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

