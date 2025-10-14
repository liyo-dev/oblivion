using UnityEngine;

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
        
        Debug.Log($"[GameBootService] GameBootProfile '{bootProfile.name}' cacheado y servicio persistente.");

        // Preparar el runtimePreset según reglas: preset de test -> save -> default
        PrepareActivePreset();
        
        // Notificar que el profile está listo
        OnProfileReady?.Invoke();
    }

    private void PrepareActivePreset()
    {
        var profile = _profile;
        if (profile == null) return;

        // Intentar localizar un SaveSystem en escena (persistente)
        SaveSystem saveSystem;
#if UNITY_2022_3_OR_NEWER
        saveSystem = Object.FindFirstObjectByType<SaveSystem>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        saveSystem = FindObjectOfType<SaveSystem>(true);
#pragma warning restore 618
#endif

        bool initialized = false;

        // 1) Forzar preset de test si está configurado
        if (profile.ShouldBootFromPreset())
        {
            profile.EnsureRuntimePresetFromTemplate(profile.bootPreset);
            Debug.Log("[GameBootService] Inicializado desde bootPreset (testing)");
            initialized = true;
        }
        // 2) Intentar cargar partida si existe
        else if (saveSystem && saveSystem.HasSave())
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
    }
    
    /// <summary>
    /// Verifica si el GameBootProfile está disponible
    /// </summary>
    public static bool IsAvailable => _profile != null && _isInitialized;

    // === NUEVO: API estática para Nueva Partida ===
    /// <summary>
    /// Borra el save y restablece el runtimePreset al defaultPlayerPreset.
    /// Llamar desde menú antes de cargar la escena inicial de juego.
    /// </summary>
    public static void NewGameReset()
    {
        if (!IsAvailable) return;
        SaveSystem save;
#if UNITY_2022_3_OR_NEWER
        save = Object.FindFirstObjectByType<SaveSystem>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        save = Object.FindObjectOfType<SaveSystem>(true);
#pragma warning restore 618
#endif
        _profile.NewGameReset(save);
    }
}
