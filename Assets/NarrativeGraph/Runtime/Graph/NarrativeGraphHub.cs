using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

/// <summary>
/// Hub simplificado para ejecutar múltiples grafos narrativos.
/// Todos los grafos están siempre activos y esperando eventos.
/// No hay auto-start: los grafos se activan mediante señales/eventos.
/// </summary>
[DefaultExecutionOrder(-450)]
public sealed class NarrativeGraphHub : MonoBehaviour
{
    static NarrativeGraphHub _instance;
    public static NarrativeGraphHub Instance => _instance;

    [Serializable]
    public class GraphSlot
    {
        public string label = "Graph";
        public NarrativeGraph graph;
        [Tooltip("Valores iniciales del blackboard (opcional).")]
        public List<SimpleBlackboard.Entry> initialBlackboardValues = new();
    }

    [SerializeField] private GraphSlot[] graphs;

    private readonly Dictionary<string, NarrativeRunner> _runnersByLabel = new();
    private readonly List<NarrativeRunner> _allRunners = new();
    private readonly HashSet<string> _validatedLabels = new();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (graphs == null || graphs.Length == 0)
            return;

        var signals = ResolveSignals();

        foreach (var slot in graphs)
        {
            if (slot == null || slot.graph == null)
                continue;

            var runnerGo = new GameObject(string.IsNullOrEmpty(slot.label) ? slot.graph.name : slot.label);
            runnerGo.transform.SetParent(transform, false);

            var runner = runnerGo.AddComponent<NarrativeRunner>();
            var blackboard = new SimpleBlackboard();
            blackboard.ImportFromSerializable(slot.initialBlackboardValues);

            // Configurar el runner SIN ejecutarlo automáticamente
            runner.graph = slot.graph;
            runner.Blackboard = blackboard;
            runner.SetSignalsProvider(signals);

            _allRunners.Add(runner);
            
            if (!_runnersByLabel.ContainsKey(slot.label ?? string.Empty))
                _runnersByLabel.Add(slot.label ?? string.Empty, runner);

            // Debug.Log($"[NarrativeGraphHub] Grafo '{slot.label}' registrado.");
        }

        if (signals == null)
            Debug.LogWarning("[NarrativeGraphHub] No se encontró DefaultNarrativeSignals. Los nodos que dependan de señales no funcionarán.");

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ValidateGraphsForScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (_instance == this)
            _instance = null;
    }

    DefaultNarrativeSignals ResolveSignals() => DefaultNarrativeSignals.EnsureInstance();

    /// <summary>Devuelve el runner asociado a la etiqueta proporcionada.</summary>
    public NarrativeRunner GetRunner(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        _runnersByLabel.TryGetValue(label, out var runner);
        return runner;
    }

    /// <summary>Devuelve todos los runners registrados.</summary>
    public List<NarrativeRunner> GetAllRunners() => _allRunners;

    void OnActiveSceneChanged(Scene current, Scene next)
    {
        ValidateGraphsForScene(next.name);
    }

    void ValidateGraphsForScene(string sceneName)
    {
        if (graphs == null || graphs.Length == 0)
            return;

        foreach (var slot in graphs)
        {
            if (slot == null || slot.graph == null)
                continue;

            var label = slot.label ?? slot.graph.name;
            if (_validatedLabels.Contains(label))
                continue;

            if (!GraphAppliesToScene(slot.graph, sceneName))
            {
                // Debug.Log($"[NarrativeGraphHub] Validación de '{label}' omitida: escena '{sceneName}' fuera del alcance configurado.");
                continue;
            }

            var validation = NarrativeGraphValidator.ValidateGraph(slot.graph);
            validation.LogResults(label);
            _validatedLabels.Add(label);
        }
    }

    static bool GraphAppliesToScene(NarrativeGraph graph, string sceneName)
    {
        if (graph == null) return false;
        if (graph.applicableSceneNames == null || graph.applicableSceneNames.Count == 0)
            return true;

        foreach (var scene in graph.applicableSceneNames)
        {
            if (string.Equals(scene, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Limpia todos los blackboards. Útil al iniciar nueva partida.
    /// Los grafos se deben reiniciar manualmente usando NarrativeGraphStarter en cada escena.
    /// </summary>
    public void ClearAllBlackboards()
    {
        foreach (var runner in _allRunners)
        {
            if (runner == null) continue;
            if (runner.Blackboard != null)
            {
                runner.Blackboard.Clear();
                // Debug.Log($"[NarrativeGraphHub] Blackboard limpiado: {runner.name}");
            }
        }
    }
    
    /// <summary>
    /// Inicia un grafo específico por su etiqueta.
    /// </summary>
    public void StartGraph(string label)
    {
        var runner = GetRunner(label);
        if (runner == null)
        {
            Debug.LogWarning($"[NarrativeGraphHub] No se encontró grafo con etiqueta '{label}'");
            return;
        }
        
        runner.StartFromStartNode();
        // Debug.Log($"[NarrativeGraphHub] Grafo '{label}' iniciado.");
    }
    
    /// <summary>
    /// Inicia múltiples grafos por sus etiquetas.
    /// </summary>
    public void StartGraphs(params string[] labels)
    {
        foreach (var label in labels)
        {
            StartGraph(label);
        }
    }
    
    /// <summary>
    /// Captura el estado actual de todos los blackboards para guardado.
    /// </summary>
    public List<PlayerSaveData.NarrativeBlackboardSnapshot> CaptureBlackboards()
    {
        var snapshots = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        
        // Debug.Log($"[NarrativeGraphHub] CaptureBlackboards - revisando {_runnersByLabel.Count} runners registrados");
        
        foreach (var kvp in _runnersByLabel)
        {
            var label = kvp.Key;
            var runner = kvp.Value;
            
            if (runner == null || runner.Blackboard == null)
            {
                // Debug.LogWarning($"[NarrativeGraphHub] Runner '{label}' o su Blackboard es null - saltando");
                continue;
            }
            
            var blackboardData = runner.Blackboard.ExportToSerializable();
            
            // Log detallado: mostrar nodo actual y algunas entradas clave
            var currentNodeGuid = runner.Blackboard.Get<string>("__currentNodeGuid", null);
            var currentNodeName = "N/A";
            if (!string.IsNullOrEmpty(currentNodeGuid))
            {
                var node = runner.graph?.FindNode(currentNodeGuid);
                if (node != null)
                    currentNodeName = $"{node.GetType().Name}";
            }

            // Debug.Log($"[NarrativeGraphHub] Runner '{label}' - blackboard tiene {(blackboardData != null ? blackboardData.Count : 0)} entradas | Nodo actual: {currentNodeName} ({ShortGuid(currentNodeGuid)}...)");
            
            // Solo guardar si hay datos en el blackboard
            if (blackboardData != null && blackboardData.Count > 0)
            {
                snapshots.Add(new PlayerSaveData.NarrativeBlackboardSnapshot
                {
                    graphLabel = label,
                    blackboardData = blackboardData
                });
                
                // Debug.Log($"[NarrativeGraphHub] ✅ Snapshot añadido para '{label}' (nodo: {currentNodeName})");
            }
        }
        
        // Debug.Log($"[NarrativeGraphHub] Capturados {snapshots.Count} blackboards con estado");
        return snapshots;
    }
    
    /// <summary>
    /// Restaura los blackboards desde un snapshot guardado.
    /// </summary>
    public void RestoreBlackboards(List<PlayerSaveData.NarrativeBlackboardSnapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            // Debug.Log("[NarrativeGraphHub] No hay blackboards guardados para restaurar");
            return;
        }
        
        // Debug.Log($"[NarrativeGraphHub] RestoreBlackboards llamado con {snapshots.Count} snapshot(s)");
        
        int restored = 0;
        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrEmpty(snapshot.graphLabel))
            {
                Debug.LogWarning("[NarrativeGraphHub] Snapshot con graphLabel vacío - saltando");
                continue;
            }
            
            // Debug.Log($"[NarrativeGraphHub] Intentando restaurar '{snapshot.graphLabel}' con {(snapshot.blackboardData != null ? snapshot.blackboardData.Count : 0)} entradas");
            
            var runner = GetRunner(snapshot.graphLabel);
            if (runner == null)
            {
                Debug.LogWarning($"[NarrativeGraphHub] No se encontró runner para grafo '{snapshot.graphLabel}'");
                continue;
            }
            
            if (runner.Blackboard == null)
            {
                Debug.LogWarning($"[NarrativeGraphHub] Runner '{snapshot.graphLabel}' no tiene blackboard");
                continue;
            }
            
            runner.Blackboard.ImportFromSerializable(snapshot.blackboardData);
            restored++;
            
            // Log detallado: mostrar nodo guardado
            var currentNodeGuid = runner.Blackboard.Get<string>("__currentNodeGuid", null);
            var currentNodeName = "N/A";
            if (!string.IsNullOrEmpty(currentNodeGuid))
            {
                var node = runner.graph?.FindNode(currentNodeGuid);
                if (node != null)
                    currentNodeName = $"{node.GetType().Name}";
            }

            // Debug.Log($"[NarrativeGraphHub] ✅ Blackboard restaurado para grafo '{snapshot.graphLabel}' | Nodo guardado: {currentNodeName} ({ShortGuid(currentNodeGuid)}...)");
        }

        // Debug.Log($"[NarrativeGraphHub] Restaurados {restored}/{snapshots.Count} blackboards");
    }

    static string ShortGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return "null";

        const int previewLength = 8;
        return guid.Length <= previewLength ? guid : guid.Substring(0, previewLength);
    }
}
