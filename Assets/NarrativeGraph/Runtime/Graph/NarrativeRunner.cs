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
    /// Detiene toda ejecución en curso: para corrutinas huérfanas y limpia el nodo actual.
    /// Llamar antes de iniciar una nueva sesión de grafo sobre el mismo runner.
    /// </summary>
    public void StopExecution()
    {
        StopAllCoroutines();
        if (_current != null && _ctx != null)
        {
            try { _current.Exit(_ctx); }
            catch (System.Exception ex) { Debug.LogError($"[NarrativeRunner] StopExecution Exit error: {ex.Message}"); }
        }
        _current = null;
    }

    /// <summary>
    /// Inicia el grafo desde un nodo específico mediante su GUID.
    /// Útil cuando un evento/señal quiere activar el grafo.
    /// </summary>
    public void StartFromNode(string nodeGuid)
    {
        StopExecution();
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

        // Debug.Log($"[NarrativeRunner] Iniciando desde nodo: {node.GetType().Name}");
        GoTo(node);
    }

    /// <summary>
    /// Inicia el grafo desde el nodo marcado como Start.
    /// Si hay un nodo guardado en el blackboard, continúa desde ahí.
    /// </summary>
    public void StartFromStartNode()
    {
        StopExecution();
        if (!TryBuildContext()) return;

        // Verificar si hay un nodo guardado en el blackboard
        var savedNodeGuid = Blackboard.Get<string>("__currentNodeGuid", null);
        Debug.Log($"[NarrativeRunner] StartFromStartNode() - savedNodeGuid='{savedNodeGuid ?? "NULL"}'");

        if (!string.IsNullOrEmpty(savedNodeGuid))
        {
            var savedNode = graph.FindNode(savedNodeGuid);
            if (savedNode != null)
            {
                Debug.Log($"[NarrativeRunner] ✅ Continuando desde nodo guardado: {savedNode.GetType().Name} (guid={savedNodeGuid})");
                GoTo(savedNode);
                return;
            }
            else
            {
                Debug.LogWarning($"[NarrativeRunner] Nodo guardado con guid={savedNodeGuid} no encontrado. Iniciando desde Start.");
            }
        }
        else
        {
            Debug.Log($"[NarrativeRunner] ⚠️ No hay nodo guardado - iniciando desde StartNode");
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

        // Debug.Log($"[NarrativeRunner] Iniciando desde StartNode: {start.GetType().Name}");
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
        // Debug.Log($"[Runner] GoTo → {node?.GetType().Name}");

        _current?.Exit(_ctx);
        _current = node;

        if (_current == null)
        {
            Debug.LogWarning("[Narrative] GoTo(null). Fin del flujo.");
            Blackboard.Set("__currentNodeGuid", string.Empty);
            return;
        }

        Blackboard.Set("__currentNodeGuid", _current.guid);
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

        // Fork: múltiples salidas
        var forkNode = _current;
        var forkGuid = forkNode.guid;
        var forkKey = $"__forked_{forkGuid}";

        // Al entrar en fork, __currentNodeGuid apunta al nodo fork (puesto por GoTo).
        // En el path de primera vez lo limpiamos; en reload lo dejamos apuntando al fork
        // para que las recargas subsecuentes retomen directamente desde aquí.

        _current = null;

        if (Blackboard.Get<bool>(forkKey, false))
        {
            // Fork ya ejecutado en sesión anterior — reanudar ramas en progreso
            Debug.Log($"[NarrativeRunner] Fork '{forkGuid}' ya ejecutado — reanudando ramas persistidas.");
            RelaunchForkBranches(forkGuid, outs);
            return;
        }

        // Primera vez: limpiar currentNodeGuid y marcar fork activo
        Blackboard.Set("__currentNodeGuid", string.Empty);
        Blackboard.Set(forkKey, true);

        for (int i = 0; i < outs.Count; i++)
        {
            var guid = outs[i];
            if (string.IsNullOrEmpty(guid)) continue;
            var node = graph.FindNode(guid);
            if (node == null)
            {
                Debug.LogError($"[Narrative] Fork: no existe nodo guid={guid}.");
                continue;
            }
            StartCoroutine(RunSubGraph(node, forkGuid, i));
        }
    }

    /// <summary>
    /// Reanuda las ramas de un fork que ya fue ejecutado, usando el estado guardado en el blackboard.
    /// Si una rama no tiene estado guardado (save antiguo) se relanza desde el inicio de esa rama.
    /// </summary>
    void RelaunchForkBranches(string forkGuid, List<string> branchStartGuids)
    {
        for (int i = 0; i < branchStartGuids.Count; i++)
        {
            var branchStartGuid = branchStartGuids[i];
            if (string.IsNullOrEmpty(branchStartGuid)) continue;

            var branchKey = $"__fork_{forkGuid}_{i}_node";
            var savedNodeGuid = Blackboard.Get<string>(branchKey, null);

            if (savedNodeGuid == "__DONE__")
            {
                Debug.Log($"[NarrativeRunner] Fork '{forkGuid}' rama {i}: completada, omitiendo.");
                continue;
            }

            // Si no hay estado guardado (save anterior al fix), relanzar desde el inicio de la rama
            string resumeGuid = !string.IsNullOrEmpty(savedNodeGuid) ? savedNodeGuid : branchStartGuid;
            var resumeNode = graph.FindNode(resumeGuid);

            if (resumeNode == null)
            {
                Debug.LogWarning($"[NarrativeRunner] Fork '{forkGuid}' rama {i}: nodo '{resumeGuid}' no encontrado. Saltando.");
                continue;
            }

            Debug.Log($"[NarrativeRunner] Fork '{forkGuid}' rama {i}: reanudando desde {resumeNode.GetType().Name} ({resumeGuid}).");
            StartCoroutine(RunSubGraph(resumeNode, forkGuid, i));
        }
    }

    /// <summary>
    /// Ejecuta una rama de nodos secuencialmente a partir de 'start'.
    /// Rastrea el progreso en el blackboard para permitir guardar y reanudar.
    /// </summary>
    System.Collections.IEnumerator RunSubGraph(NarrativeNode start, string forkGuid, int branchIndex)
    {
        bool track = !string.IsNullOrEmpty(forkGuid) && branchIndex >= 0;
        var node = start;

        if (track)
            Blackboard.Set($"__fork_{forkGuid}_{branchIndex}_node", node.guid);

        while (node != null)
        {
            // Detectar fork anidado ya ejecutado ANTES de hacer Enter para evitar re-ejecutar el nodo
            if (node.outputs != null && node.outputs.Count > 1)
            {
                var nestedForkKey = $"__forked_{node.guid}";
                if (Blackboard.Get<bool>(nestedForkKey, false))
                {
                    Debug.Log($"[NarrativeRunner] Fork anidado '{node.guid}' en rama {branchIndex} — reanudando subramas sin re-ejecutar nodo.");
                    if (track)
                        Blackboard.Set($"__fork_{forkGuid}_{branchIndex}_node", node.guid);
                    RelaunchForkBranches(node.guid, node.outputs);
                    yield break;
                }
            }

            bool ready = false;
            try
            {
                node.Enter(_ctx, () => ready = true);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Narrative] RunSubGraph Enter error en {node.GetType().Name}: {ex.Message}");
                yield break;
            }

            yield return new WaitUntil(() => ready);

            var outs = node.outputs;
            if (outs == null || outs.Count == 0)
            {
                if (track) Blackboard.Set($"__fork_{forkGuid}_{branchIndex}_node", "__DONE__");
                yield break;
            }

            if (outs.Count == 1)
            {
                var nextGuid = outs[0];
                if (string.IsNullOrEmpty(nextGuid))
                {
                    if (track) Blackboard.Set($"__fork_{forkGuid}_{branchIndex}_node", "__DONE__");
                    yield break;
                }
                var next = graph.FindNode(nextGuid);
                if (next == null)
                {
                    if (track) Blackboard.Set($"__fork_{forkGuid}_{branchIndex}_node", "__DONE__");
                    yield break;
                }
                node = next;
                if (track) Blackboard.Set($"__fork_{forkGuid}_{branchIndex}_node", node.guid);
                continue;
            }

            // Fork anidado — primera vez
            var nfKey = $"__forked_{node.guid}";
            Blackboard.Set(nfKey, true);
            // Mantener puntero al nodo fork para poder reanudar en recargas futuras
            if (track) Blackboard.Set($"__fork_{forkGuid}_{branchIndex}_node", node.guid);
            for (int ni = 0; ni < outs.Count; ni++)
            {
                var gid = outs[ni];
                if (string.IsNullOrEmpty(gid)) continue;
                var n = graph.FindNode(gid);
                if (n != null) StartCoroutine(RunSubGraph(n, node.guid, ni));
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
