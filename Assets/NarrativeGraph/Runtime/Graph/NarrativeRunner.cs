using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class NarrativeRunner : MonoBehaviour
{
    public NarrativeGraph graph;
    public SimpleBlackboard Blackboard = new SimpleBlackboard();

    [SerializeField] private DefaultNarrativeSignals signalsProvider;

    NarrativeContext _ctx;
    NarrativeNode _current;

    // Ya no hay auto-start. Los grafos se activan mediante señales/eventos.

    /// <summary>
    /// Inicia el grafo desde un nodo específico mediante su GUID.
    /// Útil cuando un evento/señal quiere activar el grafo.
    /// </summary>
    public void StartFromNode(string nodeGuid)
    {
        if (!TryBuildContext()) return;

        if (string.IsNullOrEmpty(nodeGuid))
        {
            Debug.LogError("[NarrativeRunner] StartFromNode: nodeGuid vacío.");
            return;
        }

        var node = graph.FindNode(nodeGuid);
        if (node == null)
        {
            Debug.LogError($"[NarrativeRunner] No existe nodo con guid={nodeGuid}");
            return;
        }

        Debug.Log($"[NarrativeRunner] Iniciando desde nodo: {node.GetType().Name}");
        GoTo(node);
    }

    /// <summary>
    /// Inicia el grafo desde el nodo marcado como Start.
    /// Si hay un nodo guardado en el blackboard, continúa desde ahí.
    /// </summary>
    public void StartFromStartNode()
    {
        if (!TryBuildContext()) return;

        // Verificar si hay un nodo guardado en el blackboard
        var savedNodeGuid = Blackboard.Get<string>("__currentNodeGuid", null);
        if (!string.IsNullOrEmpty(savedNodeGuid))
        {
            var savedNode = graph.FindNode(savedNodeGuid);
            if (savedNode != null)
            {
                Debug.Log($"[NarrativeRunner] Continuando desde nodo guardado: {savedNode.GetType().Name} (guid={savedNodeGuid})");
                GoTo(savedNode);
                return;
            }
            else
            {
                Debug.LogWarning($"[NarrativeRunner] Nodo guardado con guid={savedNodeGuid} no encontrado. Iniciando desde Start.");
            }
        }

        if (string.IsNullOrEmpty(graph.startNodeGuid))
        {
            Debug.LogError("[NarrativeRunner] startNodeGuid vacío. Añade un StartNode y márcalo como 'Set as Start' en el editor.");
            return;
        }

        var start = graph.FindNode(graph.startNodeGuid);
        if (start == null)
        {
            Debug.LogError($"[NarrativeRunner] No encuentro el StartNode guid={graph.startNodeGuid}");
            return;
        }

        Debug.Log($"[NarrativeRunner] Iniciando desde StartNode: {start.GetType().Name}");
        GoTo(start);
    }

    bool TryBuildContext()
    {
        if (graph == null)
        {
            Debug.LogError("[Narrative] Graph no asignado en NarrativeRunner.");
            return false;
        }

        if (Blackboard == null)
            Blackboard = new SimpleBlackboard();

        if (signalsProvider == null)
            signalsProvider = DefaultNarrativeSignals.EnsureInstance();

        _ctx = new NarrativeContext
        {
            Graph = graph,
            Runner = this,
            Blackboard = Blackboard,
            Exposed = new ExposedPropertyTable(),
            Signals = signalsProvider
        };
        return true;
    }

    public void GoTo(NarrativeNode node)
    {
        Debug.Log($"[Runner] GoTo → {node?.GetType().Name}");
        
        _current?.Exit(_ctx);
        _current = node;

        if (_current == null)
        {
            Debug.LogWarning("[Narrative] GoTo(null). Fin del flujo.");
            // Limpiar el nodo actual del blackboard
            Blackboard.Set("__currentNodeGuid", string.Empty);
            return;
        }

        // Guardar el GUID del nodo actual en el blackboard para persistencia
        Blackboard.Set("__currentNodeGuid", _current.guid);

        // El nodo llama a ready() cuando esté listo para avanzar
        _current.Enter(_ctx, Advance);
    }

    void Advance()
    {
        if (_current == null)
        {
            Debug.LogWarning("[Narrative] Advance() sin nodo actual.");
            return;
        }

        var outs = _current.outputs;
        if (outs == null || outs.Count == 0)
        {
            Debug.Log($"[Narrative] '{_current.GetType().Name}' no tiene salidas. Flujo detenido.");
            // Marcar que no hay nodo actual para evitar re-ejecutar acciones tras cargar partida
            Blackboard.Set("__currentNodeGuid", string.Empty);
            return;
        }

        // Si hay una sola salida, comportarse como antes
        if (outs.Count == 1)
        {
            var nextGuid = outs.FirstOrDefault(g => !string.IsNullOrEmpty(g));
            if (string.IsNullOrEmpty(nextGuid))
            {
                Debug.Log($"[Narrative] Salida vacía desde '{_current.GetType().Name}'. Flujo detenido.");
                return;
            }

            var next = graph.FindNode(nextGuid);
            if (next == null)
            {
                Debug.LogError($"[Narrative] No existe nodo con guid={nextGuid}. ¿Se borró sin actualizar edges?");
                return;
            }

            GoTo(next);
            return;
        }

        // Si hay múltiples salidas -> lanzar cada rama en paralelo mediante coroutines independientes
        Blackboard.Set("__currentNodeGuid", string.Empty);
        foreach (var guid in outs)
        {
            if (string.IsNullOrEmpty(guid)) continue;
            var node = graph.FindNode(guid);
            if (node == null)
            {
                Debug.LogError($"[Narrative] ForceBranch: no existe nodo guid={guid}.");
                continue;
            }
            StartCoroutine(RunSubGraph(node));
        }

        // El flujo principal no continúa con una sola 'current' cuando se lanzan ramas paralelas
        _current = null;
    }

    // Ejecuta una rama de nodos secuencialmente a partir de 'start'
    System.Collections.IEnumerator RunSubGraph(NarrativeNode start)
    {
        var node = start;
        while (node != null)
        {
            bool ready = false;
            try
            {
                node.Enter(_ctx, () => ready = true);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Narrative] RunSubGraph Enter error: {ex.Message}");
                yield break;
            }

            // Esperar a que el nodo invoque el callback
            yield return new WaitUntil(() => ready);

            // Obtener salidas
            var outs = node.outputs;
            if (outs == null || outs.Count == 0)
            {
                // fin de esta rama
                yield break;
            }

            if (outs.Count == 1)
            {
                var nextGuid = outs[0];
                if (string.IsNullOrEmpty(nextGuid)) yield break;
                var next = graph.FindNode(nextGuid);
                if (next == null) yield break;
                node = next;
                continue;
            }

            // Si hay múltiples salidas, disparar sub-ramas para cada salida y terminar esta rama
            foreach (var guid in outs)
            {
                if (string.IsNullOrEmpty(guid)) continue;
                var n = graph.FindNode(guid);
                if (n == null) continue;
                StartCoroutine(RunSubGraph(n));
            }
            yield break;
        }
    }

    public void ForceJumpToOutput(NarrativeNode from, int outputIndex)
    {
        if (from == null)
        {
            Debug.LogWarning("[Narrative] ForceJumpToOutput: 'from' es null.");
            return;
        }
        if (from.outputs == null || outputIndex < 0 || outputIndex >= from.outputs.Count)
        {
            Debug.LogWarning($"[Narrative] ForceJumpToOutput: índice {outputIndex} fuera de rango en {from.GetType().Name}.");
            return;
        }

        var guid = from.outputs[outputIndex];
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogWarning($"[Narrative] ForceJumpToOutput: salida {outputIndex} vacía en {from.GetType().Name}.");
            return;
        }

        var next = graph.FindNode(guid);
        if (next == null)
        {
            Debug.LogError($"[Narrative] ForceJumpToOutput: no existe nodo guid={guid}.");
            return;
        }

        GoTo(next);
    }

    // Snapshot export/import eliminado: la progresión depende de flags/misiones

    public void SetSignalsProvider(DefaultNarrativeSignals provider)
    {
        if (provider != null)
            signalsProvider = provider;
    }
}
