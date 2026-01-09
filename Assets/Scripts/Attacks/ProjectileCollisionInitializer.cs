using UnityEngine;

/// <summary>
/// Servicio singleton que gestiona el sistema de colisión de proyectiles.
/// Persiste entre escenas y se registra en el ServiceLocator.
/// 
/// CONFIGURACIÓN:
/// 1. Añade este componente a un GameObject en la escena START
/// 2. Crea un ProjectileCollisionSettings (Create > Game > Projectile Collision Settings)
/// 3. Arrastra el settings al campo 'Settings' en el Inspector
/// 4. Configura el VFX de colisión en el settings
/// 
/// El servicio persistirá entre escenas y estará disponible globalmente.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class ProjectileCollisionService : MonoBehaviour
{
    public static ProjectileCollisionService Instance { get; private set; }
    
    [Header("Configuración")]
    [Tooltip("ScriptableObject con la configuración de colisiones de proyectiles")]
    [SerializeField] private ProjectileCollisionSettingsSO settings;
    
    [Header("Persistencia")]
    [Tooltip("Mantener este servicio entre cambios de escena")]
    [SerializeField] private bool persistAcrossScenes = true;
    
    private static bool _isShuttingDown;
    
    void Awake()
    {
        // Patrón Singleton con protección contra duplicados
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[ProjectileCollisionService] Instancia duplicada detectada en '{name}'. Se destruye el duplicado.");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        _isShuttingDown = false;
        
        // DontDestroyOnLoad para persistir entre escenas
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        // Registrar en ServiceLocator
        ServiceLocator.Register(this);
        
        // Inicializar el sistema de colisiones
        InitializeCollisionSystem();
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            _isShuttingDown = true;
            
            // Desregistrar del ServiceLocator
            ServiceLocator.Unregister(this);
        }
    }
    
    void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }
    
    private void InitializeCollisionSystem()
    {
        if (settings != null)
        {
            var config = settings.ToConfig();
            ProjectileCollisionHandler.Initialize(config);
            // Debug.Log("[ProjectileCollisionService] ✅ Sistema de colisión de proyectiles inicializado con settings");
            
            // Log de configuración para debugging
            if (settings.collisionVFX != null)
            {
                // Debug.Log($"[ProjectileCollisionService] VFX configurado: {settings.collisionVFX.name}");
            }
            else
            {
                Debug.LogWarning("[ProjectileCollisionService] ⚠️ No hay VFX de colisión asignado");
            }
        }
        else
        {
            Debug.LogWarning("[ProjectileCollisionService] ⚠️ No hay settings asignado - usando configuración por defecto");
            // El handler usará configuración por defecto automáticamente
        }
    }
    
    /// <summary>
    /// Permite actualizar la configuración en runtime
    /// </summary>
    public void UpdateSettings(ProjectileCollisionSettingsSO newSettings)
    {
        settings = newSettings;
        InitializeCollisionSystem();
        Debug.Log("[ProjectileCollisionService] Configuración actualizada en runtime");
    }
    
    /// <summary>
    /// Obtiene la configuración actual
    /// </summary>
    public ProjectileCollisionSettingsSO GetSettings()
    {
        return settings;
    }
}

