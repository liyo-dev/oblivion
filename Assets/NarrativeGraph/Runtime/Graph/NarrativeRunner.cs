using System.Linq;
using UnityEngine;

public class NarrativeRunner : MonoBehaviour
{
    public NarrativeGraph graph;
    public SimpleBlackboard blackboard = new SimpleBlackboard();

    // ← asegúrate de tener este campo (lo rellena el AutoSetup o tú a mano)
    [SerializeField] private DefaultNarrativeSignals signalsProvider;

    NarrativeContext ctx;
    NarrativeNode current;

    void Start()
    {
        Debug.Log("[Runner] Start");
        
        if (graph == null)
        {
            Debug.LogError("[Narrative] Graph no asignado en NarrativeRunner.");
            return;
        }

        ctx = new NarrativeContext
        {
            Graph = graph,
            Runner = this,
            Blackboard = blackboard,
            Exposed = new ExposedPropertyTable(),
            Signals = signalsProvider // puede ser null; los nodos que lo usen deben comprobarlo
        };

        // Arranca en el StartNode del asset
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

    public void GoTo(NarrativeNode node)
    {
        Debug.Log($"[Runner] GoTo → {node?.GetType().Name}");
        
        current?.Exit(ctx);
        current = node;

        if (current == null)
        {
            Debug.LogWarning("[Narrative] GoTo(null). Fin del flujo.");
            return;
        }

        // El nodo llama a ready() cuando esté listo para avanzar
        current.Enter(ctx, Advance);
    }

    void Advance()
    {
        if (current == null)
        {
            Debug.LogWarning("[Narrative] Advance() sin nodo actual.");
            return;
        }

        var outs = current.outputs;
        if (outs == null || outs.Count == 0)
        {
            Debug.Log($"[Narrative] '{current.GetType().Name}' no tiene salidas. Flujo detenido.");
            return;
        }

        var nextGuid = outs.FirstOrDefault(g => !string.IsNullOrEmpty(g));
        if (string.IsNullOrEmpty(nextGuid))
        {
            Debug.Log($"[Narrative] Salida vacía desde '{current.GetType().Name}'. Flujo detenido.");
            return;
        }

        var next = graph.FindNode(nextGuid);
        if (next == null)
        {
            Debug.LogError($"[Narrative] No existe nodo con guid={nextGuid}. ¿Se borró sin actualizar edges?");
            return;
        }

        GoTo(next);
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
}
