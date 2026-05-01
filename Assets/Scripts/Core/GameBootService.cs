using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Servicio simple que hace persistir el GameBootProfile entre escenas
/// Su única función es actuar como contenedor estático del ScriptableObject
/// </summary>
[DefaultExecutionOrder(100)] // ✅ Ejecutarse DESPUÉS de managers como QuestManager
public class GameBootService : MonoBehaviour
{
    [Header("Boot Profile")]
    [SerializeField] private GameBootProfile bootProfile;
    
    // Cache estático para acceso global
    private static GameBootProfile _profile;
    private static bool _isInitialized;
    private static bool _testingModeInitialized; // ✅ NUEVO: Evita resetear el runtime en cada escena en modo testeo
    private static GameBootService _instance; // ✅ Referencia al singleton
    
    // Evento para notificar cuando el profile está listo
    public static event System.Action OnProfileReady;
    
    // Propiedad pública para acceder al profile desde cualquier lugar
    public static GameBootProfile Profile 
    { 
        get 
        {
            // ✅ Registrar quién está accediendo al Profile para diagnóstico
            #if UNITY_EDITOR
            if (_profile != null)
            {
                var stackTrace = new System.Diagnostics.StackTrace(1, false);
                var frame = stackTrace.GetFrame(0);
                if (frame != null)
                {
                    var method = frame.GetMethod();
                    if (method != null && method.DeclaringType != null)
                    {
                        string callerType = method.DeclaringType.Name;
                        ProfileReadyDiagnostics.RegisterProfileAccess(callerType);
                    }
                }
            }
            #endif
            
            return _profile;
        }
    }

    /// <summary>
    /// Verifica si el servicio está disponible y inicializado.
    /// </summary>
    public static bool IsAvailable => _isInitialized && _profile != null;

    /// <summary>
    /// Indica si el perfil está configurado para forzar el boot desde el preset (modo test).
    /// </summary>
    public static bool IsPresetOverrideActive => _profile != null && _profile.ShouldBootFromPreset();
    
    #if UNITY_EDITOR
    /// <summary>
    /// Resetea las variables estáticas cuando se sale de PlayMode en el editor.
    /// Esto previene que valores de ejecuciones anteriores contaminen nuevas sesiones.
    /// </summary>
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetStaticsOnEnterPlayMode(UnityEditor.EnterPlayModeOptions options)
    {
        _isInitialized = false;
        _testingModeInitialized = false;
        _instance = null;
        _profile = null;
        OnProfileReady = null;
        Debug.Log("[GameBootService] 🔄 Variables estáticas reseteadas al entrar en PlayMode");
    }
    #endif
    
    void Awake()
    {
        // ✅ CRÍTICO: En editor, permitir reinicialización si se inicia desde otra escena
        // En build, el flujo siempre es Start → MainWorld, pero en editor se puede iniciar desde cualquier escena
        #if UNITY_EDITOR
        bool isReinitInEditor = _isInitialized && UnityEditor.EditorApplication.isPlaying;
        if (isReinitInEditor)
        {
            Debug.Log("[GameBootService] 🔄 Reinicializando desde editor (inicio desde escena diferente a Start)");
            // Destruir el GameObject anterior y permitir que este nuevo se inicialice
            if (_instance != null && _instance != this)
            {
                Destroy(_instance.gameObject);
            }
            _isInitialized = false;
            _instance = null;
            _profile = null;
        }
        #endif
        
        // Si ya tenemos el profile cacheado, destruir este GameObject (evita duplicados en build)
        if (_isInitialized)
        {
            Debug.Log("[GameBootService] Profile ya está inicializado. Destruyendo duplicado.");
            Destroy(gameObject);
            return;
        }
        
        // Validar que tenemos el profile asignado
        if (bootProfile == null)
        {
            Debug.LogError("[GameBootService] No se ha asignado GameBootProfile en el inspector!");
            Destroy(gameObject);
            return;
        }
        
        // Cachear el profile para acceso global
        _profile = bootProfile;
        _instance = this;
        _isInitialized = true;
        
        // Hacer que este GameObject persista entre escenas
        DontDestroyOnLoad(gameObject);
        
        // Suscribirse a eventos de carga de escena para reforzar el preset de testeo
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        Debug.Log($"[GameBootService] 🎮 GameBootProfile '{bootProfile.name}' cacheado - Preparando preset...");

        // Preparar el runtimePreset según reglas: preset de test -> save -> default
        PrepareActivePreset();
        
        // ✅ CRÍTICO: Diferir OnProfileReady un frame para permitir que todos los OnEnable() se ejecuten
        // Esto garantiza que BossArenaController, NPCManager, etc. estén suscritos antes de disparar el evento
        StartCoroutine(NotifyProfileReadyDelayed());
    }
    
    private System.Collections.IEnumerator NotifyProfileReadyDelayed()
    {
        // Esperar un frame para que todos los OnEnable() se ejecuten
        yield return null;
        
        // Ahora notificar que el profile está listo
        Debug.Log($"[GameBootService] 📢 Disparando OnProfileReady (componentes listos para recibir)");
        OnProfileReady?.Invoke();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ✅ NUEVO COMPORTAMIENTO: En modo testeo, solo aplicar el preset la PRIMERA vez
        // Luego permitir que el runtime evolucione libremente entre escenas
        // Esto permite guardar el progreso en SavePoints y continuar acumulando avances
        if (_profile != null && _profile.ShouldBootFromPreset() && _profile.bootPreset != null)
        {
            // Solo resetear al bootPreset si es la primera inicialización
            // En cambios de escena subsecuentes, mantener el runtime evolucionado
            if (!_testingModeInitialized)
            {
                _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
                
                // Restaurar quest desde el preset de testeo (sobrescribe cualquier estado previo)
                var qm = QuestManager.Instance;
                if (qm != null)
                {
                    var preset = _profile.GetActivePresetResolved();
                    if (preset != null)
                    {
                        qm.RestoreFromProfileFlags(preset.flags);
                    }
                }
                
                _testingModeInitialized = true;
                Debug.Log($"[GameBootService] 🧪 Modo testeo inicializado desde bootPreset '{_profile.bootPreset.name}' - El runtime ahora evolucionará libremente");
            }
            else
            {
                Debug.Log($"[GameBootService] 🧪 Escena '{scene.name}' cargada → Manteniendo runtime evolucionado (modo testeo persistente)");
            }
        }
    }

    private void PrepareActivePreset()
    {
        Debug.Log($"[GameBootService] 🚀 PrepareActivePreset() iniciado");
        
        var profile = _profile;
        if (profile == null) return;

        // Intentar localizar un SaveSystem en escena (persistente)
        var saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        
        Debug.Log($"[GameBootService] 🔍 SaveSystem: {(saveSystem != null ? "Disponible" : "NULL")}");
        Debug.Log($"[GameBootService] 🔍 SaveSystem.HasSave(): {(saveSystem?.HasSave() ?? false)}");
        Debug.Log($"[GameBootService] 🔍 Profile.ShouldBootFromPreset(): {profile.ShouldBootFromPreset()}");
        Debug.Log($"[GameBootService] 🔍 Profile.bootPreset: {(profile.bootPreset != null ? profile.bootPreset.name : "NULL")}");

        bool initialized = false;

        // 1) MODO TESTING: El preset de testeo actúa COMO SI FUERA una partida cargada
        // Aplica TODOS los sistemas (quests, NPCs, blackboards, etc.) como LoadProfile()
        if (profile.ShouldBootFromPreset())
        {
            Debug.Log($"[GameBootService] 📋 MODO TESTING - Usando bootPreset: '{profile.bootPreset.name}'");
            profile.EnsureRuntimePresetFromTemplate(profile.bootPreset);

            // Si existe un save, restaurar progresión narrativa para evitar re-ejecutar
            // contenido ya visto (CameraFocusNode, LorePopupNode, etc.).
            // El estado de mundo (HP, anchor, party, flags) sigue viniendo del bootPreset.
            if (saveSystem != null && saveSystem.HasSave() && saveSystem.Load(out var savedProgress))
            {
                var rtp = profile.GetActivePresetResolved();
                if (rtp != null)
                {
                    if (savedProgress.seenLorePopupIds?.Count > 0)
                    {
                        rtp.seenLorePopupIds = new List<string>(savedProgress.seenLorePopupIds);
                        Debug.Log($"[GameBootService] 🧪 Popups de lore vistos cargados desde save: {rtp.seenLorePopupIds.Count}");
                    }
                    if (savedProgress.narrativeBlackboards?.Count > 0)
                    {
                        rtp.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>(savedProgress.narrativeBlackboards);
                        Debug.Log($"[GameBootService] 🧪 Blackboards narrativos cargados desde save: {rtp.narrativeBlackboards.Count}");
                    }
                    if (savedProgress.completedInteractiveNarratives?.Count > 0)
                    {
                        rtp.completedInteractiveNarratives = new List<string>(savedProgress.completedInteractiveNarratives);
                        Debug.Log($"[GameBootService] 🧪 Narrativas completadas cargadas desde save: {rtp.completedInteractiveNarratives.Count}");
                    }
                }
            }

            // ✅ CRÍTICO: Aplicar el preset de testeo usando la misma lógica que LoadProfile
            // Esto asegura que TODOS los sistemas se inicialicen correctamente
            ApplyPresetAsLoadedGame(profile);
            
            // Marcar como inicializado para que OnSceneLoaded no resetee
            _testingModeInitialized = true;
            
            // Solo log en modo testing (útil para debug)
            Debug.Log("[GameBootService] ✅ Inicializado desde bootPreset (testing mode) - Aplicados todos los sistemas como si fuera una partida cargada");
            initialized = true;
        }
        // 2) Intentar cargar partida si existe (SOLO si NO hay preset de testeo)
        else if (saveSystem != null && saveSystem.HasSave())
        {
            if (profile.LoadProfile(saveSystem))
            {
                // Log eliminado - carga normal no necesita log
                _testingModeInitialized = false; // ✅ En modo normal, resetear flag
                initialized = true;
            }
        }

        // 3) Si no, usar preset por defecto
        if (!initialized)
        {
            if (profile.defaultPlayerPreset)
            {
                profile.EnsureRuntimePresetFromTemplate(profile.defaultPlayerPreset);
                // Log eliminado - inicialización normal no necesita log
            }
            else
            {
                profile.EnsureRuntimePreset();
                Debug.LogWarning("[GameBootService] No hay defaultPlayerPreset. Se crea runtimePreset vacío.");
            }
            _testingModeInitialized = false; // ✅ En modo normal, resetear flag
        }

        // Log rápido de diagnóstico
        var p = profile.GetActivePresetResolved();
        if (p)
        {
            // Debug.Log($"[GameBootService] RuntimePreset listo → Anchor: {p.spawnAnchorId}, HP: {p.currentHP}/{p.maxHP}, MP: {p.currentMP}/{p.maxMP}, Slots: L:{p.leftSpellId} R:{p.rightSpellId} S:{p.specialSpellId}");
        }
        
        // === reconstruir estados del QuestManager desde flags del perfil ===
        // NOTA: Esto ya se hace en ApplyPresetAsLoadedGame() para modo testing,
        // pero lo dejamos aquí para el caso de defaultPlayerPreset
        if (!profile.ShouldBootFromPreset())
        {
            var qm = QuestManager.Instance;
            if (qm != null && p != null)
            {
                qm.RestoreFromProfileFlags(p.flags);
                Debug.Log($"[GameBootService] ✅ Quests restauradas (modo normal) desde {p.flags?.Count ?? 0} flags");
            }
            else if (p != null && p.flags != null && p.flags.Count > 0)
            {
                Debug.LogWarning($"[GameBootService] ⚠️ QuestManager.Instance es NULL en modo normal - Restaurando cuando esté listo");
                StartCoroutine(RestoreQuestsWhenReady(p.flags));
            }
        }
    }
    
    /// <summary>
    /// Aplica un preset como si fuera una partida cargada, inicializando TODOS los sistemas.
    /// Esto incluye: quests, NPCs, blackboards, bosses, spawn anchor, etc.
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
            Debug.LogWarning($"[GameBootService]   ⚠️ QuestManager.Instance es NULL - Las quests se restaurarán cuando QuestManager esté disponible");
            // ✅ CRÍTICO: Restaurar quests cuando QuestManager esté listo
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
                // ✅ CRÍTICO: Si el Hub no está disponible aún, diferir la restauración
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
    
    /// <summary>
    /// Espera a que NarrativeGraphHub esté listo y luego restaura los blackboards.
    /// </summary>
    private System.Collections.IEnumerator RestoreBlackboardsWhenHubReady(List<PlayerSaveData.NarrativeBlackboardSnapshot> blackboards)
    {
        int attempts = 0;
        const int maxAttempts = 100; // 5 segundos máximo (50ms * 100)
        
        while (attempts < maxAttempts)
        {
            var hub = NarrativeGraphHub.Instance;
            if (hub != null)
            {
                var runners = hub.GetAllRunners();
                if (runners != null && runners.Count > 0)
                {
                    Debug.Log($"[GameBootService] ✅ NarrativeGraphHub listo con {runners.Count} runners - restaurando {blackboards.Count} blackboards");
                    hub.RestoreBlackboards(blackboards);
                    yield break;
                }
            }
            
            attempts++;
            yield return new WaitForSeconds(0.05f);
        }
        
        Debug.LogError($"[GameBootService] ❌ Timeout esperando NarrativeGraphHub - NO se restauraron {blackboards.Count} blackboards");
    }
    
    /// <summary>
    /// Corrutina que espera a que QuestManager esté disponible y luego restaura las quests.
    /// </summary>
    private System.Collections.IEnumerator RestoreQuestsWhenReady(System.Collections.Generic.List<string> flags)
    {
        Debug.Log("[GameBootService] ⏳ Esperando a que QuestManager esté disponible...");
        
        // Esperar hasta que QuestManager.Instance no sea null
        while (QuestManager.Instance == null)
        {
            yield return null;
        }
        
        Debug.Log($"[GameBootService] ✅ QuestManager disponible - Restaurando {flags?.Count ?? 0} flags");
        QuestManager.Instance.RestoreFromProfileFlags(flags);
    }

    // === NUEVO: API estática para Nueva Partida ===
    /// <summary>
    /// Borra el save y restablece el runtimePreset al defaultPlayerPreset.
    /// Llamar desde menú antes de cargar la escena inicial de juego.
    /// IMPORTANTE: Si el modo testing está activo, respeta el bootPreset en lugar de resetear.
    /// </summary>
    public static void NewGameReset()
    {
        if (!IsAvailable) return;
        
        // Si el modo testing está activo, no resetear - mantener el bootPreset
        if (_profile.ShouldBootFromPreset() && _profile.bootPreset != null)
        {
            _profile.EnsureRuntimePresetFromTemplate(_profile.bootPreset);
            _testingModeInitialized = true; // ✅ Marcar como inicializado
            Debug.Log("[GameBootService] NewGameReset llamado con testing mode activo → Manteniendo bootPreset");
            return;
        }
        
        var save = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        _profile.NewGameReset(save);
        _testingModeInitialized = false; // ✅ Resetear flag para modo normal

        // Limpiar estado runtime persistente (DontDestroyOnLoad) que puede arrastrarse
        // cuando se inicia "Nueva Partida" sin reiniciar la aplicación.
        if (Game.NPC.PlayerParty.HasInstance)
        {
            Game.NPC.PlayerParty.Instance.ResetForNewGame();
        }

        // Limpiar blackboards de todos los grafos narrativos para nueva partida
        // Los grafos siguen esperando eventos, pero con estado limpio
        NarrativeGraphHub.Instance?.ClearAllBlackboards();
    }
}
