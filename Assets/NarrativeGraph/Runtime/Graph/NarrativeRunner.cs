using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class NarrativeRunner : MonoBehaviour
{
    public NarrativeGraph graph;
    public SimpleBlackboard Blackboard = new SimpleBlackboard();

    // ← asegúrate de tener este campo (lo rellena el AutoSetup o tú a mano)
    [SerializeField] private DefaultNarrativeSignals signalsProvider;

    NarrativeContext _ctx;
    NarrativeNode _current;

    // Pending snapshot si RestoreFromSnapshot se invoca antes de Start()
    PlayerSaveData.NarrativeSnapshot _pendingSnapshot;

    void Start()
    {
        Debug.Log("[Runner] Start");
        
        if (graph == null)
        {
            Debug.LogError("[Narrative] Graph no asignado en NarrativeRunner.");
            return;
        }

        _ctx = new NarrativeContext
        {
            Graph = graph,
            Runner = this,
            Blackboard = Blackboard,
            Exposed = new ExposedPropertyTable(),
            Signals = signalsProvider // puede ser null; los nodos que lo usen deben comprobarlo
        };

        // Si hay snapshot pendiente, restaurarla y no arrancar desde el StartNode por defecto
        if (_pendingSnapshot != null)
        {
            var snap = _pendingSnapshot;
            _pendingSnapshot = null;
            RestoreFromSnapshot(snap);
            return;
        }

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

    // === NUEVO: Snapshot export/import ===
    public PlayerSaveData.NarrativeSnapshot ExportSnapshot()
    {
        var snap = new PlayerSaveData.NarrativeSnapshot();
        try
        {
            snap.currentNodeGuid = _current != null ? _current.guid : string.Empty;

            var sbEntries = Blackboard?.ExportToSerializable();
            if (sbEntries != null && sbEntries.Count > 0)
            {
                snap.entries = new List<PlayerSaveData.NarrativeBlackboardEntry>(sbEntries.Count);
                foreach (var e in sbEntries)
                {
                    var ne = new PlayerSaveData.NarrativeBlackboardEntry
                    {
                        key = e.key,
                        type = e.type,
                        value = e.value
                    };
                    snap.entries.Add(ne);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Narrative] ExportSnapshot falló: {ex.Message}");
        }
        return snap;
    }

    public void RestoreFromSnapshot(PlayerSaveData.NarrativeSnapshot snapshot)
    {
        if (snapshot == null) return;

        // Si el runner no ha inicializado el contexto aún, guardar el snapshot para aplicarlo en Start()
        if (_ctx == null)
        {
            _pendingSnapshot = snapshot;
            return;
        }

        try
        {
            // Restaurar blackboard
            if (snapshot.entries != null && snapshot.entries.Count > 0)
            {
                var sbList = new List<SimpleBlackboard.Entry>(snapshot.entries.Count);
                foreach (var e in snapshot.entries)
                {
                    var se = new SimpleBlackboard.Entry
                    {
                        key = e.key,
                        type = e.type,
                        value = e.value
                    };
                    sbList.Add(se);
                }
                Blackboard.ImportFromSerializable(sbList);
            }

            // Restaurar nodo actual (si existe)
            if (!string.IsNullOrEmpty(snapshot.currentNodeGuid) && graph != null)
            {
                var node = graph.FindNode(snapshot.currentNodeGuid);
                if (node != null)
                {
                    GoTo(node);
                }
                else
                {
                    Debug.LogWarning($"[Narrative] RestoreFromSnapshot: no existe el nodo guid={snapshot.currentNodeGuid} en el grafo.");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Narrative] RestoreFromSnapshot falló: {ex.Message}");
        }
    }
}
