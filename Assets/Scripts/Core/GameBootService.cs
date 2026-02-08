using UnityEngine;
using UnityEngine.SceneManagement;

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
    
    // Evento para notificar cuando el profile está listo
    public static event System.Action OnProfileReady;
    
    // Propiedad pública para acceder al profile desde cualquier lugar
    public static GameBootProfile Profile => _profile;

    /// <summary>
    /// Indica si el perfil está configurado para forzar el boot desde el preset (modo test).
    /// </summary>
    public static bool IsPresetOverrideActive => _profile != null && _profile.ShouldBootFromPreset();
    
    void Awake()
    {
        // Si ya tenemos el profile cacheado, destruir este GameObject (evita duplicados)
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
        _isInitialized = true;
        
        // Hacer que este GameObject persista entre escenas
        DontDestroyOnLoad(gameObject);
        
        // Suscribirse a eventos de carga de escena para reforzar el preset de testeo
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Debug.Log($"[GameBootService] GameBootProfile '{bootProfile.name}' cacheado y servicio persistente.");

        // Preparar el runtimePreset según reglas: preset de test -> save -> default
        PrepareActivePreset();
        
        // Notificar que el profile está listo
        // Los servicios suscritos leerán del runtimePreset y actuarán en consecuencia
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
        var profile = _profile;
        if (profile == null) return;

        // Intentar localizar un SaveSystem en escena (persistente)
        var saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        bool initialized = false;

        // 1) MODO TESTING: El preset de testeo actúa COMO SI FUERA una partida cargada
        // Aplica TODOS los sistemas (quests, NPCs, blackboards, etc.) como LoadProfile()
        if (profile.ShouldBootFromPreset())
        {
            profile.EnsureRuntimePresetFromTemplate(profile.bootPreset);
            
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
        if (!string.IsNullOrEmpty(preset.spawnAnchorId))
        {
            SpawnManager.SetCurrentAnchor(preset.spawnAnchorId);
            Debug.Log($"[GameBootService]   ✅ Spawn anchor: {preset.spawnAnchorId}");
        }
        
        // 2. Restaurar progreso de bosses
        if (BossProgressTracker.TryGetInstance(out var tracker))
        {
            tracker.LoadFromSnapshot(preset.defeatedBossIds);
            Debug.Log($"[GameBootService]   ✅ Bosses derrotados: {preset.defeatedBossIds?.Count ?? 0}");
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
        profile.ApplyNpcPositionsToScene(preset);
        Debug.Log($"[GameBootService]   ✅ Posiciones de NPCs: {preset.npcPositions?.Count ?? 0}");
        
        // 5. Resetear sistema de narrativas para el perfil cargado
        NarrativeAutoSetup.ResetForLoadedProfile();
        
        // 6. Limpiar registro de narrativas interactivas para que se re-registren
        Game.NPC.Modules.NPCInteractiveNarrativeRegistry.Clear();
        Debug.Log($"[GameBootService]   ✅ NPCInteractiveNarrativeRegistry limpiado");
        
        // 7. Restaurar blackboards narrativos si existen
        if (preset.narrativeBlackboards != null && preset.narrativeBlackboards.Count > 0)
        {
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
                Debug.LogWarning($"[GameBootService]   ⚠️ NarrativeGraphHub.Instance es NULL - no se pueden restaurar {preset.narrativeBlackboards.Count} blackboards");
            }
        }
        else
        {
            Debug.Log("[GameBootService]   ℹ️ No hay blackboards narrativos en el preset para restaurar");
        }
        
        Debug.Log($"[GameBootService] 🎮 Preset de testeo aplicado como partida cargada - Sistema completo inicializado");
        
        // ✅ NUEVO: Limpiar locks del PlayerLockService al entrar en modo testeo
        // Esto previene que el player quede bloqueado si viene de una sesión anterior
        if (PlayerLockService.HasInstance && PlayerLockService.Instance.IsLocked)
        {
            Debug.LogWarning("[GameBootService] 🔓 Limpiando locks residuales del PlayerLockService en modo testeo");
            PlayerLockService.Instance.ForceUnlock();
        }
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
    
    /// <summary>
    /// Verifica si el GameBootProfile está disponible
    /// </summary>
    public static bool IsAvailable => _profile != null && _isInitialized;

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

        // Limpiar blackboards de todos los grafos narrativos para nueva partida
        // Los grafos siguen esperando eventos, pero con estado limpio
        NarrativeGraphHub.Instance?.ClearAllBlackboards();
    }
}
