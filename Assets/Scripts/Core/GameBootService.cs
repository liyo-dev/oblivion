using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orquesta el arranque del juego, la carga de saves y la gestión del perfil de jugador.
///
/// Flujo de arranque:
/// 1. Awake() se ejecuta al iniciar el juego (desde la escena 'Start').
/// 2. Carga el GameBootProfile desde Resources.
/// 3. Llama a PrepareActivePreset() para decidir qué datos usar:
///    - Si está en modo test (usePresetInsteadOfSave=true), usa el 'bootPreset'.
///    - Si no, intenta cargar el save JSON.
///    - Si no hay save, usa el 'defaultPlayerPreset'.
/// 4. Dispara el evento OnProfileReady para que otros sistemas se inicialicen.
/// 5. Carga la escena principal del juego (ej: 'MainWorld').
///
/// El GameBootService persiste entre escenas.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GameBootService : MonoBehaviour
{
    #region Singleton y State
    private static GameBootService _instance;
    public static bool IsAvailable => _instance != null;

    [SerializeField] private GameBootProfile _bootProfileAsset;

    private static GameBootProfile _profile;
    public static GameBootProfile Profile => _profile;

    private static SaveSystem _saveSystem;
    public static SaveSystem SaveSystem => _saveSystem;

    public static event System.Action OnProfileReady;
    
    private bool _isInitialized;
    #endregion

    #region Editor
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Game/Boot/Force Reload Test Preset")]
    public static void ForceReloadTestPreset()
    {
        if (!Application.isPlaying || _instance == null)
        {
            Debug.LogWarning("Esta opción solo funciona en PlayMode.");
            return;
        }
        ReloadTestPreset();
    }
    
    [UnityEditor.MenuItem("Game/Boot/Force New Game Reset")]
    public static void ForceNewGameReset()
    {
        if (!Application.isPlaying || _instance == null)
        {
            Debug.LogWarning("Esta opción solo funciona en PlayMode.");
            return;
        }
        NewGameReset();
    }
#endif
    #endregion

    #region Static Lifecycle
    /// <summary>
    /// Garantiza que el GameBootService exista al iniciar el juego.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        // Con BeforeSceneLoad no hay escena cargada aún, FindFirstObjectByType siempre retorna null.
        // NO crear instancia dinámica: carecería del _bootProfileAsset serializado, rompiendo builds.
        // Start.unity siempre contiene el componente con el perfil asignado; su Awake() lo registra.
        var existing = FindFirstObjectByType<GameBootService>();
        if (existing)
        {
            _instance = existing;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // Si el juego está en PlayMode y se recargan los scripts,
        // es posible que las referencias estáticas se pierdan.
        if (Application.isPlaying && _instance == null)
        {
            _instance = FindFirstObjectByType<GameBootService>();
            if (_instance)
            {
                Debug.Log("[GameBootService] 🔄 Referencia estática restaurada tras recarga de scripts.");
            }
        }
    }
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnEnterPlayMode()
    {
        Debug.Log("[GameBootService] 🔄 Variables estáticas reseteadas al entrar en PlayMode");
        _instance = null;
        _profile = null;
        _saveSystem = null;
        OnProfileReady = null;
    }
#endif
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (_isInitialized) return;
        _isInitialized = true;

        // Cargar el perfil de arranque desde referencia directa o fallback de editor
        _profile = _bootProfileAsset;
#if UNITY_EDITOR
        if (_profile == null)
            _profile = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBootProfile>(
                "Assets/_BootProfile/GameBootProfile.asset");
#endif
        if (_profile == null)
        {
            Debug.LogError("[GameBootService] GameBootProfile no encontrado. Asígnalo en el Inspector del componente GameBootService en la escena 'Start'.");
            return;
        }

        // Inicializar el sistema de guardado
        _saveSystem = FindFirstObjectByType<SaveSystem>();
        if (_saveSystem == null)
        {
            var saveGo = new GameObject("[SaveSystem]");
            _saveSystem = saveGo.AddComponent<SaveSystem>();
        }

        Debug.Log($"[GameBootService] 🎮 GameBootProfile '{_profile.name}' cacheado - Preparando preset...");
        
        // Preparar el runtimePreset según reglas: preset de test -> save -> default
        PrepareActivePreset();
        
        // ✅ CRÍTICO: Diferir OnProfileReady un frame para permitir que todos los OnEnable() se ejecuten
        StartCoroutine(NotifyProfileReadyDelayed());
        
        // Suscribirse a eventos de carga de escena para reforzar el preset de testeo
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }
    }

    private IEnumerator NotifyProfileReadyDelayed()
    {
        yield return null; // Esperar un frame
        Debug.Log($"[GameBootService] 📢 Disparando OnProfileReady (componentes listos para recibir)");
        OnProfileReady?.Invoke();
        
        var diagnostics = FindFirstObjectByType<ProfileReadyDiagnostics>();
        if (diagnostics != null)
        {
            diagnostics.HandleProfileReadyForDiagnostics();
        }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Indica si el perfil está configurado para forzar el boot desde el preset (modo test).
    /// </summary>
    public static bool IsPresetOverrideActive => _profile != null && _profile.ShouldBootFromPreset();
    #endregion

    #region Scene Load Handling
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ✅ NUEVO COMPORTAMIENTO: En modo testeo, solo aplicar el preset la PRIMERA vez
        if (_profile != null && _profile.ShouldBootFromPreset() && _profile.bootPreset != null)
        {
            // Solo resetear al bootPreset si es la primera inicialización
            if (scene.name == "Start")
            {
                _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
                
                // Restaurar quest desde el preset de testeo (sobrescribe cualquier estado previo)
                var qm = QuestManager.Instance;
                if (qm != null)
                {
                    var preset = _profile.GetActivePresetResolved();
                    if (preset != null)
                        qm.RestoreFromProfileFlags(preset.flags);
                }
                
                Debug.Log($"[GameBootService] 🧪 Modo testeo inicializado desde bootPreset '{_profile.bootPreset.name}' - El runtime ahora evolucionará libremente");
            }
            else
            {
                Debug.Log($"[GameBootService] 🧪 Escena '{scene.name}' cargada → Manteniendo runtime evolucionado (modo testeo persistente)");
            }
        }
    }
    #endregion

    #region Preset and Save Loading
    private void PrepareActivePreset()
    {
        Debug.Log($"[GameBootService] 🚀 PrepareActivePreset() iniciado");
        
        if (_profile == null)
        {
            Debug.LogError("[GameBootService] _profile es null. No se puede preparar preset.");
            return;
        }
        
        Debug.Log($"[GameBootService] 🔍 SaveSystem: {(_saveSystem != null ? "Disponible" : "NO Disponible")}");
        Debug.Log($"[GameBootService] 🔍 SaveSystem.HasSave(): {_saveSystem?.HasSave()}");
        Debug.Log($"[GameBootService] 🔍 Profile.ShouldBootFromPreset(): {_profile.ShouldBootFromPreset()}");
        Debug.Log($"[GameBootService] 🔍 Profile.bootPreset: {(_profile.bootPreset != null ? _profile.bootPreset.name : "NULL")}");

        // 1) MODO TESTING: El preset de testeo actúa COMO SI FUERA una partida cargada
        if (_profile.ShouldBootFromPreset())
        {
            Debug.Log($"[GameBootService] 📋 MODO TESTING - Usando bootPreset: '{_profile.bootPreset.name}'");
            _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
            
            // En modo testeo el preset define el estado exacto del juego.
            // No se lee ni mezcla nada del save JSON: "se vuelca al preset runtime lo que tenga el preset de testeo".
            
            // ✅ CRÍTICO: Aplicar el preset de testeo usando la misma lógica que LoadProfile
            ApplyPresetAsLoadedGame(_profile);
            Debug.Log("[GameBootService] ✅ Inicializado desde bootPreset (testing mode) - Aplicados todos los sistemas como si fuera una partida cargada");
        }
        // 2) Intentar cargar partida si existe (SOLO si NO hay preset de testeo)
        else if (_saveSystem != null && _saveSystem.HasSave())
        {
            Debug.Log("[GameBootService] 💾 Cargando partida desde save JSON...");
            _profile.LoadProfile(_saveSystem);
        }
        // 3) Si no, usar preset por defecto
        else
        {
            Debug.Log("[GameBootService] 📄 No hay save - Usando preset por defecto");
            if (_profile.defaultPlayerPreset)
            {
                _profile.EnsureRuntimePresetFromTemplate(_profile.defaultPlayerPreset);
            }
            else
            {
                _profile.EnsureRuntimePreset();
                Debug.LogWarning("[GameBootService] No hay defaultPlayerPreset. Se crea runtimePreset vacío.");
            }
        }

        var p = _profile.GetActivePresetResolved();
        if (p != null)
        {
            // Debug.Log($"[GameBootService] RuntimePreset listo → Anchor: {p.spawnAnchorId}, HP: {p.currentHP}/{p.maxHP}, MP: {p.currentMP}/{p.maxMP}, Slots: L:{p.leftSpellId} R:{p.rightSpellId} S:{p.specialSpellId}");
        }
        
        // NOTA: Esto ya se hace en ApplyPresetAsLoadedGame() para modo testing,
        // pero lo dejamos aquí para el caso de defaultPlayerPreset
        if (!_profile.ShouldBootFromPreset())
        {
            // Aplicar el preset por defecto al jugador
            var playerPresetService = FindFirstObjectByType<PlayerPresetService>();
            if (playerPresetService != null)
            {
                playerPresetService.ApplyCurrentPreset();
            }
        }
    }

    /// <summary>
    /// Aplica un preset como si fuera una partida cargada, inicializando TODOS los sistemas.
    /// </summary>
    private void ApplyPresetAsLoadedGame(GameBootProfile profile)
    {
        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;
        
        Debug.Log($"[GameBootService] 🎮 Aplicando preset de testeo como partida cargada...");
        
        // 1. Restaurar anchor de spawn
        // ✅ MOVIDO: SpawnManager.HandleProfileReady() se encarga de esto ahora
        // El anchor se establece DESPUÉS cuando SpawnManager recibe OnProfileReady
        Debug.Log($"[GameBootService]   ⏭️ Spawn anchor se establecerá por SpawnManager: {preset.spawnAnchorId}");
        
        // 2. Restaurar progreso de bosses
        // ⚠️ NOTA: NO cargar aquí porque BossArenaController.OnEnable() aún no se ha ejecutado
        // Los BossArenaController no están suscritos al evento OnProgressRestored todavía
        // BossProgressPersistenceBridge.HandleProfileReady() lo hará cuando todos estén listos
        if (BossProgressTracker.TryGetInstance(out var tracker))
        {
            // tracker.LoadFromSnapshot(preset.defeatedBossIds); // ← COMENTADO
            Debug.Log($"[GameBootService]   ⏭️ Boss progress se cargará por BossProgressPersistenceBridge (cuando componentes estén listos)");
        }
        
        // 3. Restaurar estado de quests desde flags
        var questManager = QuestManager.Instance;
        if (questManager != null)
        {
            questManager.RestoreFromProfileFlags(preset.flags);
            Debug.Log($"[GameBootService]   ✅ Quests restauradas desde {preset.flags?.Count ?? 0} flags");
        }
        else
        {
            // Si QuestManager no está listo, diferir la restauración
            StartCoroutine(RestoreQuestsWhenReady(preset.flags));
        }

        // 4. Aplicar posiciones de NPCs
        // ⚠️ NOTA: En modo testeo con preset, esto se ejecuta ANTES de que MainWorld cargue
        // Los NPCs no existen todavía, por lo que WorldBootstrap lo hará cuando MainWorld esté cargada
        // En modo normal (cargar save), MainWorld ya está cargada, así que funciona correctamente
        if (!profile.ShouldBootFromPreset())
        {
            profile.ApplyNpcPositionsToScene(preset);
            Debug.Log($"[GameBootService]   ✅ Posiciones de NPCs aplicadas: {preset.npcPositions?.Count ?? 0}");
        }
        else
        {
            Debug.Log($"[GameBootService]   ⏭️ Posiciones de NPCs se aplicarán por WorldBootstrap (modo preset)");
        }
        
        // 5. Resetear sistema de narrativas para el perfil cargado
        NarrativeAutoSetup.ResetForLoadedProfile();
        
        // 6. Limpiar registro de narrativas interactivas para que se re-registren
        Game.NPC.Modules.NPCInteractiveNarrativeRegistry.Clear();
        Debug.Log($"[GameBootService]   ✅ NPCInteractiveNarrativeRegistry limpiado");
        
        // 7. Restaurar puntos de teletransporte desbloqueados
        TeleportRegistry.LoadFromSaveData(preset.unlockedTeleportPoints);
        Debug.Log($"[GameBootService]   ✅ Teleport points restaurados desde preset: {preset.unlockedTeleportPoints?.Count ?? 0}");

        // 8. Restaurar blackboards narrativos si existen
        if (preset.narrativeBlackboards != null && preset.narrativeBlackboards.Count > 0)
        {
            Debug.Log($"[GameBootService] 📖 Intentando restaurar {preset.narrativeBlackboards.Count} blackboards narrativos...");
            
            // Intentar restauración inmediata
            var hub = NarrativeGraphHub.Instance;
            if (hub != null)
            {
                var runners = hub.GetAllRunners();
                Debug.Log($"[GameBootService] 🔍 NarrativeGraphHub disponible con {runners?.Count ?? 0} runners");
                
                hub.RestoreBlackboards(preset.narrativeBlackboards);
                Debug.Log($"[GameBootService]   ✅ Blackboards narrativos restaurados: {preset.narrativeBlackboards.Count}");
            }
            else
            {
                Debug.LogWarning($"[GameBootService]   ⏳ NarrativeGraphHub.Instance es NULL - diferiendo restauración de {preset.narrativeBlackboards.Count} blackboards");
                StartCoroutine(RestoreBlackboardsWhenHubReady(preset.narrativeBlackboards));
            }
        }
        else
        {
            Debug.Log("[GameBootService]   ℹ️ No hay blackboards narrativos en el preset para restaurar");
        }
        
        Debug.Log($"[GameBootService] 🎮 Preset de testeo aplicado como partida cargada - Sistema completo inicializado");
    }

    private IEnumerator RestoreQuestsWhenReady(IReadOnlyList<string> flags)
    {
        var wait = new WaitForSeconds(0.1f);
        for (int i = 0; i < 10; i++) // 1 segundo de timeout
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.RestoreFromProfileFlags(flags);
                Debug.Log($"[GameBootService]   ✅ (Diferido) Quests restauradas desde {flags?.Count ?? 0} flags");
                yield break;
            }
            yield return wait;
        }
        Debug.LogError("[GameBootService] Timeout esperando a QuestManager.Instance");
    }

    private IEnumerator RestoreBlackboardsWhenHubReady(List<PlayerSaveData.NarrativeBlackboardSnapshot> blackboards)
    {
        var wait = new WaitForSeconds(0.1f);
        for (int i = 0; i < 10; i++) // 1 segundo de timeout
        {
            if (NarrativeGraphHub.Instance != null)
            {
                NarrativeGraphHub.Instance.RestoreBlackboards(blackboards);
                Debug.Log($"[GameBootService]   ✅ (Diferido) Blackboards narrativos restaurados: {blackboards.Count}");
                yield break;
            }
            yield return wait;
        }
        Debug.LogError("[GameBootService] Timeout esperando a NarrativeGraphHub.Instance");
    }
    #endregion

    #region Public API para Testing y Resets
    // ==== API pública para recargar preset de testeo ====
    /// <summary>
    /// En modo testeo, descarta los avances de la sesión actual y vuelca el bootPreset
    /// desde cero: detiene runners huérfanos, resetea el runtimePreset y re-aplica todos
    /// los sistemas como si fuera la primera vez que se carga.
    /// </summary>
    public static void ReloadTestPreset()
    {
        if (_instance == null || !IsAvailable || !_profile.ShouldBootFromPreset()) return;
        _instance.ReloadTestPresetInternal();
    }

    private void ReloadTestPresetInternal()
    {
        Debug.Log("[GameBootService] 🔄 ReloadTestPreset — descartando sesión anterior y recargando bootPreset...");
        
        // 1. Detener todos los grafos narrativos para evitar runners huérfanos
        if (NarrativeGraphHub.Instance != null)
        {
            NarrativeGraphHub.Instance.StopAllRunners();
            // Limpiar blackboards en memoria: fork keys y branch-done markers de la sesión
            // anterior quedarían marcados como __DONE__ y los nodos terminales (p.ej. ShowLorePopupNode)
            // serían saltados silenciosamente por RelaunchForkBranches en el próximo run.
            NarrativeGraphHub.Instance.ClearAllBlackboards();
        }

        // 2. Volcar el bootPreset al runtimePreset (borra avances)
        _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
        
        // 3. Re-aplicar todos los sistemas como si fuera una carga de partida
        ApplyPresetAsLoadedGame(_profile);
        
        // 4. Forzar que todos los sistemas suscritos a OnProfileReady se re-inicialicen
        OnProfileReady?.Invoke();
        
        Debug.Log("[GameBootService] ✅ Test preset recargado — sistema reiniciado como primera carga");
    }

    /// <summary>
    /// Borra el save y restablece el runtimePreset al defaultPlayerPreset.
    /// IMPORTANTE: Si el modo testing está activo, respeta el bootPreset en lugar de resetear.
    /// </summary>
    public static void NewGameReset()
    {
        if (_instance == null || !IsAvailable) return;

        // Si el modo testing está activo, no resetear - mantener el bootPreset
        if (_profile.ShouldBootFromPreset() && _profile.bootPreset != null)
        {
            _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
            Debug.Log("[GameBootService] NewGameReset llamado con testing mode activo → Manteniendo bootPreset");
        }
        else
        {
            _profile.NewGameReset(_saveSystem);
        }
        
        // Forzar re-inicialización de sistemas
        OnProfileReady?.Invoke();
    }
    #endregion
}