using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class NarrativeAutoSetup : MonoBehaviour
{
    [Header("Config obligatoria")]
    public NarrativeGraph graph;

    [Header("Debug opcional")]
    public bool debugLogs;

    private static NarrativeAutoSetup _instance;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (debugLogs) Debug.Log("[NarrativeAutoSetup] Duplicado detectado. Destruyendo este.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        var runner = GetComponent<NarrativeRunner>() ?? gameObject.AddComponent<NarrativeRunner>();
        var signals = GetComponent<DefaultNarrativeSignals>() ?? gameObject.AddComponent<DefaultNarrativeSignals>();
        var questService = GetComponent<QuestServiceAdapter>() ?? gameObject.AddComponent<QuestServiceAdapter>();

        if (!graph)
            Debug.LogWarning("[NarrativeAutoSetup] Graph no asignado. Asigna uno en el inspector.");
        runner.graph = graph;

        signals.questServiceProvider = questService;

        var fi = typeof(NarrativeRunner).GetField("signalsProvider",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null) fi.SetValue(runner, signals);
        else Debug.LogWarning("[NarrativeAutoSetup] No se encontró 'signalsProvider' en NarrativeRunner.");

        // Snapshot narrativo eliminado; no hay pending que aplicar

        if (debugLogs)
            Debug.Log("[NarrativeAutoSetup] Listo: runner + signals + questService conectados.");
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
