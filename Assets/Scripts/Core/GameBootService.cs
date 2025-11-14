using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Servicio simple que hace persistir el GameBootProfile entre escenas
/// Su única función es actuar como contenedor estático del ScriptableObject
/// </summary>
public class GameBootService : MonoBehaviour
{
    [Header("Boot Profile")]
    [SerializeField] private GameBootProfile bootProfile;
    
    // Cache estático para acceso global
    private static GameBootProfile _profile;
    private static bool _isInitialized;
    
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
        
        Debug.Log($"[GameBootService] GameBootProfile '{bootProfile.name}' cacheado y servicio persistente.");

        // Preparar el runtimePreset según reglas: preset de test -> save -> default
        PrepareActivePreset();
        
        // Notificar que el profile está listo
        OnProfileReady?.Invoke();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Si el checkbox de testeo está activado, SIEMPRE reforzar el preset de testeo completo
        // ignorando cualquier save existente (control absoluto del preset)
        if (_profile != null && _profile.ShouldBootFromPreset() && _profile.bootPreset != null)
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
            
            Debug.Log($"[GameBootService] Escena '{scene.name}' cargada → Reforzando bootPreset completo (modo testing - save ignorado)");
        }
    }

    private void PrepareActivePreset()
    {
        var profile = _profile;
        if (profile == null) return;

        // Intentar localizar un SaveSystem en escena (persistente)
        var saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        bool initialized = false;

        // 1) MODO TESTING: El preset de testeo tiene PRIORIDAD ABSOLUTA - ignora saves completamente
        if (profile.ShouldBootFromPreset())
        {
            profile.EnsureRuntimePresetFromTemplate(profile.bootPreset);
            Debug.Log("[GameBootService] Inicializado desde bootPreset (testing) - SAVE IGNORADO");
            initialized = true;
        }
        // 2) Intentar cargar partida si existe (SOLO si NO hay preset de testeo)
        else if (saveSystem != null && saveSystem.HasSave())
        {
            if (profile.LoadProfile(saveSystem))
            {
                Debug.Log("[GameBootService] Inicializado desde SAVE");
                initialized = true;
            }
        }

        // 3) Si no, usar preset por defecto
        if (!initialized)
        {
            if (profile.defaultPlayerPreset)
            {
                profile.EnsureRuntimePresetFromTemplate(profile.defaultPlayerPreset);
                Debug.Log("[GameBootService] Inicializado desde defaultPlayerPreset");
            }
            else
            {
                profile.EnsureRuntimePreset();
                Debug.LogWarning("[GameBootService] No hay defaultPlayerPreset. Se crea runtimePreset vacío.");
            }
        }

        // Log rápido de diagnóstico
        var p = profile.GetActivePresetResolved();
        if (p)
        {
            Debug.Log($"[GameBootService] RuntimePreset listo → Anchor: {p.spawnAnchorId}, HP: {p.currentHP}/{p.maxHP}, MP: {p.currentMP}/{p.maxMP}, Slots: L:{p.leftSpellId} R:{p.rightSpellId} S:{p.specialSpellId}");
        }
        
        // === reconstruir estados del QuestManager desde flags del perfil ===
        var qm = QuestManager.Instance;
        if (qm != null && p != null)
        {
            qm.RestoreFromProfileFlags(p.flags);
        }
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
            Debug.Log("[GameBootService] NewGameReset llamado con testing mode activo → Manteniendo bootPreset");
            return;
        }
        
        var save = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        _profile.NewGameReset(save);

        // Reiniciar todos los grafos narrativos persistentes para que arranquen desde cero
        NarrativeGraphHub.Instance?.RestartAll(resetBlackboard: true);
    }
}
