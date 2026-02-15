using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramientas de editor para diagnosticar y corregir problemas de suscripción a OnProfileReady.
/// </summary>
public class ProfileReadyDiagnosticsEditor : EditorWindow
{
    private Vector2 _scrollPosition;
    private bool _autoRefresh = true;
    private float _lastRefreshTime;
    private const float REFRESH_INTERVAL = 1f;

    [MenuItem("Tools/Sendero/Profile Ready Diagnostics")]
    public static void ShowWindow()
    {
        var window = GetWindow<ProfileReadyDiagnosticsEditor>("Profile Ready Diagnostics");
        window.minSize = new Vector2(600, 400);
        window.Show();
    }

    private void OnEnable()
    {
        _lastRefreshTime = Time.realtimeSinceStartup;
    }

    private void Update()
    {
        if (_autoRefresh && Application.isPlaying)
        {
            if (Time.realtimeSinceStartup - _lastRefreshTime > REFRESH_INTERVAL)
            {
                Repaint();
                _lastRefreshTime = Time.realtimeSinceStartup;
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        
        EditorGUILayout.LabelField("DIAGNÓSTICO DE SUSCRIPCIONES A OnProfileReady", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Este sistema detecta componentes que acceden a GameBootService.Profile sin suscribirse correctamente a OnProfileReady, " +
            "lo que puede causar comportamientos diferentes entre Start y MainWorld.",
            MessageType.Info
        );
        
        EditorGUILayout.Space(10);

        // Controles
        EditorGUILayout.BeginHorizontal();
        _autoRefresh = EditorGUILayout.Toggle("Auto-Refresh", _autoRefresh);
        
        if (GUILayout.Button("Refrescar Ahora", GUILayout.Width(150)))
        {
            Repaint();
        }
        
        if (GUILayout.Button("Informe Completo", GUILayout.Width(150)))
        {
            var diagnostics = FindFirstObjectByType<ProfileReadyDiagnostics>();
            if (diagnostics != null)
            {
                diagnostics.GenerateFullReport();
            }
            else if (Application.isPlaying)
            {
                Debug.LogWarning("[ProfileReadyDiagnostics] Sistema no encontrado en la escena");
            }
            else
            {
                Debug.LogWarning("[ProfileReadyDiagnostics] Debe estar en Play Mode para ver el informe");
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entra en Play Mode para ver los diagnósticos en tiempo real.", MessageType.Warning);
            return;
        }

        // Estado del sistema
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        
        DrawSystemStatus();
        
        EditorGUILayout.EndScrollView();
    }

    private void DrawSystemStatus()
    {
        EditorGUILayout.LabelField("Estado del Sistema", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Buscar el componente de diagnósticos
        var diagnostics = Object.FindObjectOfType<ProfileReadyDiagnostics>();
        if (diagnostics == null)
        {
            EditorGUILayout.HelpBox("ProfileReadyDiagnostics no encontrado en la escena.", MessageType.Error);
            return;
        }

        // Información básica
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⏱️ OnProfileReady Disparado:", GetProfileReadyStatus());
        EditorGUILayout.Space(5);
        
        // Acciones rápidas
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Escanear Escena"))
        {
            diagnostics.ScanScene();
        }
        if (GUILayout.Button("Analizar Suscripciones"))
        {
            diagnostics.AnalyzeSubscriptions();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Guía rápida
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📚 Guía Rápida de Corrección", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        EditorGUILayout.LabelField("Para sistemas que acceden al Profile sin suscribirse:");
        EditorGUILayout.LabelField("1. Añadir en OnEnable():", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(
            "GameBootService.OnProfileReady += HandleProfileReady;\n" +
            "ProfileReadyDiagnostics.RegisterSubscriber(nameof(TuClase));",
            EditorStyles.textArea,
            GUILayout.Height(40)
        );
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("2. Añadir en OnDisable():", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(
            "GameBootService.OnProfileReady -= HandleProfileReady;",
            EditorStyles.textArea,
            GUILayout.Height(20)
        );
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("3. Implementar el handler:", EditorStyles.miniBoldLabel);
        EditorGUILayout.SelectableLabel(
            "private void HandleProfileReady()\n" +
            "{\n" +
            "    // Inicializar usando GameBootService.Profile\n" +
            "    var preset = GameBootService.Profile?.GetActivePresetResolved();\n" +
            "    if (preset != null)\n" +
            "    {\n" +
            "        // Tu lógica de inicialización\n" +
            "    }\n" +
            "}",
            EditorStyles.textArea,
            GUILayout.Height(120)
        );
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(10);
        
        // Sistemas comunes que suelen necesitar suscripción
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚠️ Sistemas que DEBEN suscribirse:", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        string[] criticalSystems = new[]
        {
            "• BossArenaController - Restaurar estado de bosses derrotados",
            "• PlayerHealthSystem - Cargar HP/MP del preset",
            "• NPCManager - Restaurar estados de NPCs",
            "• WorldPickup - Cargar items ya recogidos",
            "• QuestManager - Restaurar quests activas",
            "• PartyManager - Restaurar miembros del party",
            "• Cualquier sistema que guarde/cargue estado en el Profile"
        };
        
        foreach (var system in criticalSystems)
        {
            EditorGUILayout.LabelField(system, EditorStyles.wordWrappedLabel);
        }
        
        EditorGUILayout.EndVertical();
    }

    private string GetProfileReadyStatus()
    {
        // Esto es solo una aproximación visual, el estado real está en ProfileReadyDiagnostics
        if (Application.isPlaying && Time.frameCount > 2)
        {
            return "Probablemente SÍ (ver Console para detalles)";
        }
        return "Esperando...";
    }
}
