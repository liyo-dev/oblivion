using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class NarrativeRunner : MonoBehaviour
{
    public NarrativeGraph graph;
    public SimpleBlackboard Blackboard = new SimpleBlackboard();

    // ← asegúrate de tener este campo (lo rellena el AutoSetup o tú a mano)
    [SerializeField] private DefaultNarrativeSignals signalsProvider;
    [SerializeField] private bool autoStartOnPlay = true;
    [SerializeField] private bool resetBlackboardOnAutoStart = true;

    NarrativeContext _ctx;
    NarrativeNode _current;

    void Start()
    {
        if (autoStartOnPlay)
            RestartFromStartNode(resetBlackboardOnAutoStart);
    }

    /// <summary>
    /// Reinicia el grafo desde el nodo inicial. Útil al arrancar o al comenzar nueva partida.
    /// </summary>
    public void RestartFromStartNode(bool resetBlackboard)
    {
        Debug.Log("[Runner] RestartFromStartNode");

        StopAllCoroutines();

        if (resetBlackboard)
        {
            if (Blackboard == null) Blackboard = new SimpleBlackboard();
            else Blackboard.Clear();
        }
        else if (Blackboard == null)
        {
            Blackboard = new SimpleBlackboard();
        }

        if (!TryBuildContext()) return;

        _current = null; // evita llamar Exit con un contexto nuevo

        if (string.IsNullOrEmpty(graph.startNodeGuid))
        {
            Debug.LogError("[Narrative] startNodeGuid vacío. Marca un nodo como 'Set as Start' en el editor.");
            return;
        }

        var start = graph.FindNode(graph.startNodeGuid);
        if (start == null)
        {
            Debug.LogError($"[Narrative] No encuentro el StartNode guid={graph.startNodeGuid}. ¿Se borró?");
            return;
        }

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
            return;
        }

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

    public void SetAutoStart(bool autoStart, bool resetBlackboard)
    {
        autoStartOnPlay = autoStart;
        resetBlackboardOnAutoStart = resetBlackboard;
    }

    public void Configure(NarrativeGraph newGraph, SimpleBlackboard blackboard, DefaultNarrativeSignals provider, bool runImmediately, bool resetBlackboardBeforeRun)
    {
        if (newGraph != null)
            graph = newGraph;
        if (blackboard != null)
            Blackboard = blackboard;
        if (provider != null)
            signalsProvider = provider;

        if (runImmediately)
            RestartFromStartNode(resetBlackboardBeforeRun);
    }
}
