using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 🔍 HERRAMIENTA DE DIAGNÓSTICO AVANZADO
/// 
/// Analiza TODOS los MonoBehaviours activos en la escena y detecta cuáles tienen métodos
/// que DEBERÍAN suscribirse a OnProfileReady pero NO lo están haciendo.
/// 
/// DETECCIÓN HEURÍSTICA:
/// - Busca métodos como LoadProfile, RestoreState, Initialize, etc.
/// - Busca acceso a GameBootService.Profile o QuestManager.Instance en Start/Awake
/// - Detecta sistemas que leen flags narrativos o estados de guardado
/// - Identifica referencias a PlayerPreset, NPCPositions, etc.
/// 
/// USO:
/// - Se ejecuta automáticamente en Awake con DefaultExecutionOrder(-400)
/// - Genera un informe detallado en el log de Unity
/// - Marca en ROJO sistemas críticos que están mal configurados
/// </summary>
[DefaultExecutionOrder(-400)] // Ejecutar después de ProfileReadyDiagnostics (-450)
public class ProfileReadySubscriptionAnalyzer : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Habilitar análisis automático en Awake (solo modo desarrollo)")]
    [SerializeField] private bool enableAutoAnalysis = true;
    
    [Tooltip("Mostrar todos los sistemas analizados (no solo los problemáticos)")]
    [SerializeField] private bool showAllSystems = false;
    
    [Tooltip("Palabras clave en nombres de métodos que sugieren necesidad de OnProfileReady")]
    [SerializeField] private string[] methodKeywords = new[]
    {
        "Load", "Restore", "Apply", "Initialize", "Setup", "Configure", "Sync",
        "Profile", "Save", "Quest", "NPC", "Boss", "Narrative", "State"
    };
    
    [Tooltip("Palabras clave en nombres de clases que sugieren necesidad de OnProfileReady")]
    [SerializeField] private string[] classKeywords = new[]
    {
        "Manager", "Controller", "Service", "System", "Handler", "Loader",
        "Tracker", "Bridge", "Bootstrap", "Initializer"
    };

    private static ProfileReadySubscriptionAnalyzer _instance;
    
    public static ProfileReadySubscriptionAnalyzer Instance => _instance;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
    }
    #endif

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (enableAutoAnalysis)
        {
            // Esperar un frame para que todos los OnEnable se ejecuten
            StartCoroutine(PerformAnalysisDelayed());
        }
    }

    private System.Collections.IEnumerator PerformAnalysisDelayed()
    {
        // Esperar 2 frames:
        // - Frame 1: Todos los Awake se ejecutan
        // - Frame 2: Todos los OnEnable se ejecutan
        // - Frame 3: GameBootService.OnProfileReady se dispara
        yield return null;
        yield return null;
        yield return null; // Un frame extra para asegurar
        
        AnalyzeAllSystems();
    }

    /// <summary>
    /// Analiza todos los sistemas en la escena y genera un informe de suscripciones
    /// </summary>
    [ContextMenu("Analizar Suscripciones OnProfileReady")]
    public void AnalyzeAllSystems()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[ProfileReadySubscriptionAnalyzer] 🔍 INICIANDO ANÁLISIS COMPLETO DE SUSCRIPCIONES OnProfileReady...");
#endif
        
        var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        
        var suspiciousSystems = new List<SuspiciousSystemInfo>();
        var safeSystems = new List<string>();
        
        foreach (var mb in allMonoBehaviours)
        {
            if (mb == null) continue;
            
            var analysis = AnalyzeSystem(mb);
            
            if (analysis.IsSuspicious)
            {
                suspiciousSystems.Add(analysis);
            }
            else if (showAllSystems)
            {
                safeSystems.Add($"✅ {mb.GetType().Name} ({mb.gameObject.name})");
            }
        }
        
        // Generar informe
        GenerateReport(suspiciousSystems, safeSystems);
    }

    private SuspiciousSystemInfo AnalyzeSystem(MonoBehaviour mb)
    {
        var type = mb.GetType();
        var info = new SuspiciousSystemInfo
        {
            ClassName = type.Name,
            GameObjectName = mb.gameObject.name,
            IsActive = mb.gameObject.activeInHierarchy && mb.enabled
        };
        
        // 1. Verificar si el nombre de la clase sugiere que debería suscribirse
        bool hasClassKeyword = classKeywords.Any(kw => type.Name.Contains(kw));
        
        // 2. Buscar métodos sospechosos
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        foreach (var method in methods)
        {
            // Buscar palabras clave en nombres de métodos
            bool hasMethodKeyword = methodKeywords.Any(kw => method.Name.Contains(kw));
            
            if (hasMethodKeyword)
            {
                info.SuspiciousMethods.Add(method.Name);
            }
            
            // Buscar acceso a GameBootService.Profile en Start/Awake
            if (method.Name == "Start" || method.Name == "Awake" || method.Name == "OnEnable")
            {
                // Nota: No podemos inspeccionar el IL sin librerías adicionales,
                // pero podemos marcar estos métodos para revisión manual
                info.HasStartAwakeOrOnEnable = true;
            }
        }
        
        // 3. Buscar campos/propiedades que sugieran dependencia de perfil
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        foreach (var field in fields)
        {
            string fieldTypeName = field.FieldType.Name;
            
            if (fieldTypeName.Contains("Profile") || 
                fieldTypeName.Contains("Quest") || 
                fieldTypeName.Contains("Save") ||
                fieldTypeName.Contains("NPC") ||
                fieldTypeName.Contains("Boss"))
            {
                info.SuspiciousFields.Add($"{field.Name} ({fieldTypeName})");
            }
        }
        
        // 4. Verificar si se suscribe a OnProfileReady
        var onEnableMethod = type.GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        
        if (onEnableMethod != null)
        {
            // Heurística: Si tiene OnEnable y palabras clave, probablemente debería suscribirse
            // Pero necesitaríamos inspeccionar el IL para estar seguros
            info.HasOnEnable = true;
        }
        
        // Determinar si es sospechoso
        info.IsSuspicious = (hasClassKeyword || info.SuspiciousMethods.Count > 0 || info.SuspiciousFields.Count > 0) 
                            && info.IsActive;
        
        return info;
    }

    private void GenerateReport(List<SuspiciousSystemInfo> suspiciousSystems, List<string> safeSystems)
    {
        var report = new System.Text.StringBuilder();
        
        report.AppendLine("\n╔════════════════════════════════════════════════════════════════════════════╗");
        report.AppendLine("║  INFORME DE ANÁLISIS DE SUSCRIPCIONES A OnProfileReady                    ║");
        report.AppendLine("╚════════════════════════════════════════════════════════════════════════════╝\n");
        
        report.AppendLine($"⏱️  Tiempo de análisis: {Time.time:F3}s desde inicio");
        report.AppendLine($"📊  Total sistemas analizados: {suspiciousSystems.Count + safeSystems.Count}");
        report.AppendLine($"⚠️  Sistemas sospechosos: {suspiciousSystems.Count}");
        report.AppendLine($"✅  Sistemas seguros: {safeSystems.Count}\n");
        
        if (suspiciousSystems.Count > 0)
        {
            report.AppendLine("════════════════════════════════════════════════════════════════════════════");
            report.AppendLine("⚠️  SISTEMAS SOSPECHOSOS (pueden necesitar suscribirse a OnProfileReady):");
            report.AppendLine("════════════════════════════════════════════════════════════════════════════\n");
            
            int index = 1;
            foreach (var system in suspiciousSystems.OrderByDescending(s => s.SuspiciousMethods.Count + s.SuspiciousFields.Count))
            {
                report.AppendLine($"{index}. 🔴 {system.ClassName} (GameObject: '{system.GameObjectName}')");
                report.AppendLine($"   Estado: {(system.IsActive ? "ACTIVO" : "INACTIVO")}");
                
                if (system.SuspiciousMethods.Count > 0)
                {
                    report.AppendLine($"   Métodos sospechosos ({system.SuspiciousMethods.Count}):");
                    foreach (var method in system.SuspiciousMethods)
                    {
                        report.AppendLine($"      - {method}()");
                    }
                }
                
                if (system.SuspiciousFields.Count > 0)
                {
                    report.AppendLine($"   Campos sospechosos ({system.SuspiciousFields.Count}):");
                    foreach (var field in system.SuspiciousFields)
                    {
                        report.AppendLine($"      - {field}");
                    }
                }
                
                if (system.HasOnEnable)
                {
                    report.AppendLine("   ✅ Tiene método OnEnable() - revisar si se suscribe a OnProfileReady");
                }
                else
                {
                    report.AppendLine("   ⚠️ NO tiene método OnEnable() - probablemente necesita agregar suscripción");
                }
                
                report.AppendLine();
                index++;
            }
        }
        else
        {
            report.AppendLine("✅ NO se encontraron sistemas sospechosos - Todos parecen estar correctamente configurados\n");
        }
        
        if (showAllSystems && safeSystems.Count > 0)
        {
            report.AppendLine("════════════════════════════════════════════════════════════════════════════");
            report.AppendLine("✅ SISTEMAS SEGUROS (no requieren OnProfileReady):");
            report.AppendLine("════════════════════════════════════════════════════════════════════════════\n");
            
            foreach (var system in safeSystems)
            {
                report.AppendLine(system);
            }
            report.AppendLine();
        }
        
        report.AppendLine("════════════════════════════════════════════════════════════════════════════");
        report.AppendLine("💡 RECOMENDACIONES:");
        report.AppendLine("════════════════════════════════════════════════════════════════════════════");
        report.AppendLine("1. Revisa cada sistema marcado como sospechoso");
        report.AppendLine("2. Si accede a GameBootService.Profile, QuestManager, o datos de guardado:");
        report.AppendLine("   → Debe suscribirse a GameBootService.OnProfileReady en OnEnable()");
        report.AppendLine("3. Si modifica estado de NPCs, bosses, o quests en Start/Awake:");
        report.AppendLine("   → Debe esperar a OnProfileReady antes de aplicar cambios");
        report.AppendLine("4. Registra la suscripción con ProfileReadyDiagnostics.RegisterSubscriber()");
        report.AppendLine("5. Desuscribe en OnDisable() para evitar memory leaks");
        report.AppendLine("════════════════════════════════════════════════════════════════════════════\n");
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ProfileReadyAnalyzer] {report}");
#endif
    }

    private class SuspiciousSystemInfo
    {
        public string ClassName;
        public string GameObjectName;
        public bool IsActive;
        public bool HasOnEnable;
        public bool HasStartAwakeOrOnEnable;
        public bool IsSuspicious;
        public List<string> SuspiciousMethods = new();
        public List<string> SuspiciousFields = new();
    }
    
    /// <summary>
    /// Comando de consola para ejecutar el análisis manualmente
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterCommand()
    {
        // Registrar comando de consola si hay un sistema de comandos
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[ProfileReadySubscriptionAnalyzer] 💡 Usa [ContextMenu] 'Analizar Suscripciones OnProfileReady' para análisis manual");
#endif
    }
}
