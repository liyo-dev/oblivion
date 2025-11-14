using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Permite ejecutar múltiples grafos narrativos (cada uno con su propio Blackboard) desde un único GameObject.
/// Útil para separar narrativa principal, misiones secundarias, etc.
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
        [Tooltip("Se ejecuta automáticamente al habilitar el Hub.")]
        public bool autoStart = true;
        [Tooltip("Vacía el blackboard antes de autoStart.")]
        public bool resetBlackboardOnStart = true;
        [Tooltip("Opcional: valores iniciales del blackboard.")]
        public List<SimpleBlackboard.Entry> initialBlackboardValues = new();
    }

    [SerializeField] private GraphSlot[] graphs;

    private readonly Dictionary<string, NarrativeRunner> _runnersByLabel = new();

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

            runner.Configure(
                slot.graph,
                blackboard,
                signals,
                runImmediately: slot.autoStart && Application.isPlaying,
                resetBlackboardBeforeRun: slot.resetBlackboardOnStart);

            // Mantiene la configuración por si se recrea durante la sesión
            runner.SetAutoStart(slot.autoStart, slot.resetBlackboardOnStart);

            if (!_runnersByLabel.ContainsKey(slot.label ?? string.Empty))
                _runnersByLabel.Add(slot.label ?? string.Empty, runner);
        }

        if (signals == null)
            Debug.LogWarning("[NarrativeGraphHub] No se encontró DefaultNarrativeSignals. Los nodos que dependan de señales no funcionarán.");
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public void RestartAll(bool resetBlackboard)
    {
        foreach (var runner in _runnersByLabel.Values)
        {
            if (runner == null) continue;
            runner.RestartFromStartNode(resetBlackboard);
        }
    }

    DefaultNarrativeSignals ResolveSignals() => DefaultNarrativeSignals.EnsureInstance();

    /// <summary>Devuelve el runner asociado a la etiqueta proporcionada.</summary>
    public NarrativeRunner GetRunner(string label)
    {
        if (string.IsNullOrEmpty(label)) return null;
        _runnersByLabel.TryGetValue(label, out var runner);
        return runner;
    }
}
