using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Sistema de diagnóstico para detectar sistemas que NO se suscriben a OnProfileReady
/// cuando deberían hacerlo, causando comportamientos diferentes entre Start y MainWorld.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class ProfileReadyDiagnostics : MonoBehaviour
{
    private static ProfileReadyDiagnostics _instance;
    private static readonly HashSet<string> _expectedSubscribers = new HashSet<string>
    {
        // ✅ Sistemas de persistencia
        "BossProgressPersistenceBridge",
        "QuestPersistenceBridge",
        
        // ✅ Sistemas del jugador
        "PlayerPresetService",
        "PlayerHealthSystem",
        "WardrobeInventory",
        "PlayerAbilitiesUI",
        "PlayerEquipmentMenuController",
        
        // ✅ Managers de mundo
        "BossArenaController",
        "WorldPickup",
        "SavePoint",
        "PortalTrigger",
        "AnchorSetter",
        "SpawnManager",
        "WorldBootstrap",
        
        // ✅ UI y feedback
        "AbilityUnlockPopupUI",
        
        // ✅ Sistemas de NPC
        "PlayerParty",
        "NPCInteractiveNarrativeExecutor",
        
        // ✅ Triggers y unlocks
        "UnlockTrigger",
        
        // Añadir aquí cualquier otro sistema que DEBA suscribirse
    };

    /// <summary>
    /// Sistemas que acceden al perfil pero NO necesitan suscribirse porque lo hacen de forma diferida o condicional.
    /// </summary>
    private static readonly HashSet<string> _exemptSystems = new HashSet<string>
    {
        "NPCNarrativeStateManager", // Estático, acceso bajo demanda
        "UnlockService",             // Acceso estático
        "TeleportRegistry",          // Singleton con acceso bajo demanda
        "TeleportService",           // Singleton con acceso bajo demanda
        "TeleportUI",                // No accede al Profile, usa TeleportRegistry
        "Interactable",              // Acceso condicional bajo demanda
        "GameBootService",           // Es el que dispara el evento
        "ProfileReadyDiagnostics",   // Sistema de diagnóstico
        "NarrativeGraphStarter",     // Acceso condicional para restaurar blackboards
        "PlayerHUDV2",               // No accede al Profile, usa ServiceLocator
        "SpawnManager",              // ✅ Tiene métodos estáticos que acceden al Profile, pero está correctamente suscrito
        "MainMenuController",        // Acceso bajo demanda desde menú principal
    };

    private static readonly HashSet<string> _actualSubscribers = new HashSet<string>();
    private static readonly HashSet<string> _profileAccessors = new HashSet<string>(); // ✅ Nuevo: rastrear quién accede al Profile
    private static bool _profileReadyFired = false;
    private static float _profileReadyTime = -1f;
    private static bool _trackingEnabled = true; // ✅ Nuevo: habilitar/deshabilitar tracking

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject(nameof(ProfileReadyDiagnostics));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ProfileReadyDiagnostics>();
    }

    private void OnEnable()
    {
        // Suscribirse ANTES que cualquier otro sistema
        GameBootService.OnProfileReady += HandleProfileReadyForDiagnostics;
    }

    private void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReadyForDiagnostics;
    }

    /// <summary>
    /// Los sistemas llaman esto en su OnEnable() cuando se suscriben a OnProfileReady.
    /// </summary>
    public static void RegisterSubscriber(string systemName)
    {
        if (_actualSubscribers.Add(systemName))
        {
            Debug.Log($"[ProfileReadyDiagnostics] ✅ Sistema suscrito: {systemName} (Total: {_actualSubscribers.Count}/{_expectedSubscribers.Count}) - Time: {Time.time:F3}s");
        }
        else
        {
            Debug.Log($"[ProfileReadyDiagnostics] ℹ️ Sistema ya estaba suscrito: {systemName} - Time: {Time.time:F3}s");
        }

        // Si OnProfileReady ya se disparó, advertir que este sistema se suscribió tarde
        if (_profileReadyFired)
        {
            float delay = Time.time - _profileReadyTime;
            Debug.LogWarning($"[ProfileReadyDiagnostics] ⚠️ TARDE: {systemName} se suscribió {delay:F3}s DESPUÉS de OnProfileReady - Puede perder el evento!");
        }
    }

    /// <summary>
    /// Registra que un sistema está accediendo al Profile.
    /// Esto ayuda a detectar sistemas que acceden sin suscribirse primero.
    /// </summary>
    public static void RegisterProfileAccess(string systemName)
    {
        if (!_trackingEnabled) return;

        if (_profileAccessors.Add(systemName))
        {
            // Si el sistema accede al Profile ANTES de OnProfileReady y NO está suscrito, es un problema
            if (!_profileReadyFired && !_actualSubscribers.Contains(systemName) && !_exemptSystems.Contains(systemName))
            {
                Debug.LogWarning($"[ProfileReadyDiagnostics] ⚠️ {systemName} está accediendo al Profile ANTES de OnProfileReady sin estar suscrito!");
            }
            
            // Si el sistema accede al Profile DESPUÉS de OnProfileReady y NO está suscrito, también es sospechoso
            if (_profileReadyFired && !_actualSubscribers.Contains(systemName) && !_exemptSystems.Contains(systemName))
            {
                float delay = Time.time - _profileReadyTime;
                Debug.LogWarning($"[ProfileReadyDiagnostics] ⚠️ {systemName} accede al Profile {delay:F3}s después de OnProfileReady sin suscribirse - Puede tener datos obsoletos!");
            }
        }
    }

    public void HandleProfileReadyForDiagnostics()
    {
        _profileReadyFired = true;
        _profileReadyTime = Time.time;

        Debug.Log($"[ProfileReadyDiagnostics] 🔔 OnProfileReady disparado en t={Time.time:F3}s - Analizando suscripciones...");

        // ✅ MEJORADO: Detectar qué sistemas están realmente presentes en las escenas cargadas
        var presentSystems = FindPresentExpectedSystems();
        var presentExpectedCount = presentSystems.Count;
        
        // Verificar qué sistemas esperados están presentes pero NO se suscribieron
        var missing = presentSystems.Except(_actualSubscribers).ToList();
        
        // Sistemas que se suscribieron pero no están en la lista de esperados
        var extra = _actualSubscribers.Except(_expectedSubscribers).ToList();
        
        // ✅ NUEVO: Detectar sistemas que accedieron al Profile sin suscribirse
        var unsafeAccessors = _profileAccessors.Except(_actualSubscribers).Except(_exemptSystems).ToList();

        // REPORTE CONSOLIDADO
        var report = new System.Text.StringBuilder();
        report.AppendLine($"[ProfileReadyDiagnostics] 📊 Suscripciones: {_actualSubscribers.Count}/{presentExpectedCount} presentes esperados (de {_expectedSubscribers.Count} totales)");
        
        if (missing.Count > 0)
        {
            report.AppendLine($"  ❌ FALTANTES ({missing.Count}): {string.Join(", ", missing.OrderBy(x => x))}");
            report.AppendLine("     ⚠️ Estos sistemas ESTÁN en la escena pero NO se suscribieron - ¡COMPORTAMIENTO INCORRECTO!");
        }
        else
        {
            report.AppendLine("  ✅ Todos los sistemas presentes están suscritos correctamente");
        }

        if (extra.Count > 0)
        {
            report.AppendLine($"  ℹ️  Sistemas adicionales ({extra.Count}): {string.Join(", ", extra.OrderBy(x => x))}");
        }
        
        // ✅ NUEVO: Reportar accesos inseguros
        if (unsafeAccessors.Count > 0)
        {
            report.AppendLine($"  ⚠️  ACCESOS SIN SUSCRIPCIÓN ({unsafeAccessors.Count}): {string.Join(", ", unsafeAccessors.OrderBy(x => x))}");
            report.AppendLine("     🔥 Estos sistemas acceden al Profile pero NO están suscritos a OnProfileReady - POSIBLE BUG!");
        }

        // Log con nivel apropiado
        if (missing.Count > 0 || unsafeAccessors.Count > 0)
        {
            Debug.LogError(report.ToString());
        }
        else
        {
            Debug.Log(report.ToString());
        }

        // Esperar un frame y verificar si algún sistema se suscribió tarde
        StartCoroutine(CheckLateSubscriptions());
    }
    
    /// <summary>
    /// Encuentra qué sistemas de la lista de esperados están realmente presentes en las escenas cargadas.
    /// Solo considera "presentes" los que están en GameObjects ACTIVOS, porque los inactivos
    /// no habrán ejecutado OnEnable() todavía (y por lo tanto no se habrán registrado).
    /// </summary>
    private HashSet<string> FindPresentExpectedSystems()
    {
        var presentSystems = new HashSet<string>();
        
        // Buscar todos los MonoBehaviour activos E INACTIVOS en todas las escenas cargadas
        var allMonoBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            UnityEngine.FindObjectsInactive.Include
        );
        
        Debug.Log($"[ProfileReadyDiagnostics] Escaneando {allMonoBehaviours.Length} MonoBehaviours...");
        
        foreach (var mb in allMonoBehaviours)
        {
            if (mb == null) continue;
            
            var typeName = mb.GetType().Name;
            
            // Si el tipo está en la lista de esperados, verificar si debe considerarse "presente"
            if (_expectedSubscribers.Contains(typeName))
            {
                bool isActive = mb.gameObject.activeInHierarchy;
                
                // ✅ CLAVE: Solo considerar "presente" si el GameObject está ACTIVO
                // Los GameObjects inactivos no habrán ejecutado OnEnable() todavía
                if (isActive)
                {
                    presentSystems.Add(typeName);
                    Debug.Log($"[ProfileReadyDiagnostics] ✅ Presente y activo: {typeName}");
                }
                else
                {
                    Debug.Log($"[ProfileReadyDiagnostics] 💤 Encontrado pero INACTIVO: {typeName} - No se esperará su registro");
                }
            }
        }
        
        Debug.Log($"[ProfileReadyDiagnostics] Total sistemas esperados ACTIVOS: {presentSystems.Count}");
        
        return presentSystems;
    }

    private System.Collections.IEnumerator CheckLateSubscriptions()
    {
        yield return new WaitForSeconds(0.5f);

        int countBefore = _actualSubscribers.Count;
        yield return new WaitForSeconds(1f);
        int countAfter = _actualSubscribers.Count;

        if (countAfter > countBefore)
        {
            Debug.LogWarning($"[ProfileReadyDiagnostics] ⚠️ {countAfter - countBefore} sistema(s) se suscribieron DESPUÉS de OnProfileReady");
        }
    }

    /// <summary>
    /// Forzar un análisis manual (útil para debugging en runtime).
    /// </summary>
    [ContextMenu("Analizar Suscripciones")]
    public void AnalyzeSubscriptions()
    {
        Debug.Log($"[ProfileReadyDiagnostics] 📊 Análisis Manual:");
        Debug.Log($"  - OnProfileReady disparado: {_profileReadyFired}");
        Debug.Log($"  - Sistemas esperados: {_expectedSubscribers.Count}");
        Debug.Log($"  - Sistemas suscritos: {_actualSubscribers.Count}");
        Debug.Log($"  - Suscritos: {string.Join(", ", _actualSubscribers)}");

        var missing = _expectedSubscribers.Except(_actualSubscribers).ToList();
        if (missing.Count > 0)
        {
            Debug.LogError($"  - ❌ Faltantes: {string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// Escanea TODOS los MonoBehaviours activos en la escena para detectar posibles sistemas
    /// que accedan al perfil pero no estén suscritos.
    /// </summary>
    [ContextMenu("Escanear Escena Completa")]
    public void ScanScene()
    {
        Debug.Log($"[ProfileReadyDiagnostics] 🔍 Escaneando escena en busca de sistemas problemáticos...");

        var allMonoBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            UnityEngine.FindObjectsInactive.Include
        );
        var potentialIssues = new List<string>();

        foreach (var mb in allMonoBehaviours)
        {
            if (mb == null) continue;
            
            var typeName = mb.GetType().Name;
            
            // Ignorar sistemas exentos
            if (_exemptSystems.Contains(typeName)) continue;
            
            // Ignorar sistemas que ya están suscritos
            if (_actualSubscribers.Contains(typeName)) continue;
            
            // Verificar si el tipo del componente está en la lista esperada pero no suscrito
            if (_expectedSubscribers.Contains(typeName))
            {
                potentialIssues.Add($"{typeName} (en {mb.gameObject.name})");
            }
        }

        if (potentialIssues.Count > 0)
        {
            Debug.LogWarning($"[ProfileReadyDiagnostics] ⚠️ Sistemas esperados pero NO suscritos en escena:\n  - {string.Join("\n  - ", potentialIssues)}");
        }
        else
        {
            Debug.Log($"[ProfileReadyDiagnostics] ✅ No se detectaron sistemas problemáticos en la escena");
        }

        Debug.Log($"[ProfileReadyDiagnostics] 📋 Total MonoBehaviours escaneados: {allMonoBehaviours.Length}");
    }

    /// <summary>
    /// Genera un informe detallado del estado actual del sistema de suscripciones.
    /// </summary>
    [ContextMenu("Generar Informe Completo")]
    public void GenerateFullReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("========================================");
        report.AppendLine("   INFORME DE SUSCRIPCIONES OnProfileReady");
        report.AppendLine("========================================");
        report.AppendLine();
        
        report.AppendLine($"⏱️  OnProfileReady disparado: {(_profileReadyFired ? $"SÍ (hace {Time.time - _profileReadyTime:F2}s)" : "NO")}");
        report.AppendLine();
        
        report.AppendLine($"📊 Estadísticas:");
        report.AppendLine($"  - Sistemas esperados: {_expectedSubscribers.Count}");
        report.AppendLine($"  - Sistemas suscritos: {_actualSubscribers.Count}");
        report.AppendLine($"  - Sistemas exentos: {_exemptSystems.Count}");
        report.AppendLine($"  - Sistemas que accedieron al Profile: {_profileAccessors.Count}");
        report.AppendLine();
        
        var missing = _expectedSubscribers.Except(_actualSubscribers).ToList();
        var extra = _actualSubscribers.Except(_expectedSubscribers).ToList();
        var unsafeAccessors = _profileAccessors.Except(_actualSubscribers).Except(_exemptSystems).ToList();
        
        if (missing.Count > 0)
        {
            report.AppendLine($"❌ SISTEMAS FALTANTES ({missing.Count}):");
            foreach (var sys in missing.OrderBy(x => x))
            {
                report.AppendLine($"  - {sys}");
            }
            report.AppendLine();
        }
        else
        {
            report.AppendLine("✅ Todos los sistemas esperados están suscritos");
            report.AppendLine();
        }
        
        // ✅ NUEVO: Reportar accesos sin suscripción
        if (unsafeAccessors.Count > 0)
        {
            report.AppendLine($"🔥 ACCESOS SIN SUSCRIPCIÓN ({unsafeAccessors.Count}):");
            report.AppendLine("   Estos sistemas acceden al Profile pero NO están suscritos a OnProfileReady:");
            foreach (var sys in unsafeAccessors.OrderBy(x => x))
            {
                report.AppendLine($"  - {sys}");
            }
            report.AppendLine("   ⚠️  Esto puede causar comportamientos diferentes entre Start y MainWorld!");
            report.AppendLine();
        }
        
        if (extra.Count > 0)
        {
            report.AppendLine($"ℹ️  SISTEMAS ADICIONALES ({extra.Count}):");
            foreach (var sys in extra.OrderBy(x => x))
            {
                report.AppendLine($"  - {sys}");
            }
            report.AppendLine();
        }
        
        report.AppendLine($"✅ SISTEMAS SUSCRITOS CORRECTAMENTE ({_actualSubscribers.Count}):");
        foreach (var sys in _actualSubscribers.OrderBy(x => x))
        {
            var isExpected = _expectedSubscribers.Contains(sys);
            var hasAccessed = _profileAccessors.Contains(sys);
            string accessMark = hasAccessed ? " [accedió]" : "";
            report.AppendLine($"  - {sys}{(isExpected ? "" : " (adicional)")}{accessMark}");
        }
        report.AppendLine();
        
        report.AppendLine($"🔓 SISTEMAS EXENTOS ({_exemptSystems.Count}):");
        foreach (var sys in _exemptSystems.OrderBy(x => x))
        {
            var hasAccessed = _profileAccessors.Contains(sys);
            string accessMark = hasAccessed ? " [accedió]" : "";
            report.AppendLine($"  - {sys}{accessMark}");
        }
        report.AppendLine();
        
        report.AppendLine($"📝 TODOS LOS ACCESOS AL PROFILE ({_profileAccessors.Count}):");
        foreach (var sys in _profileAccessors.OrderBy(x => x))
        {
            bool isSubscribed = _actualSubscribers.Contains(sys);
            bool isExempt = _exemptSystems.Contains(sys);
            string status = isSubscribed ? "✅ suscrito" : isExempt ? "🔓 exento" : "❌ SIN SUSCRIPCIÓN";
            report.AppendLine($"  - {sys} ({status})");
        }
        report.AppendLine();
        
        report.AppendLine("========================================");
        
        Debug.Log(report.ToString());
    }

    /// <summary>
    /// Reset para testing (llamar antes de cambiar de escena en tests).
    /// </summary>
    public static void Reset()
    {
        _actualSubscribers.Clear();
        _profileAccessors.Clear();
        _profileReadyFired = false;
        _profileReadyTime = -1f;
        Debug.Log("[ProfileReadyDiagnostics] 🔄 Estado reseteado");
    }
}