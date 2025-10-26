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

        try
        {
            var profile = GameBootService.Profile;
            if (profile != null)
            {
                var pending = profile.PopPendingNarrativeSnapshot();
                if (pending != null)
                {
                    try
                    {
                        runner.RestoreFromSnapshot(pending);
                        if (debugLogs) Debug.Log("[NarrativeAutoSetup] Applied pending narrative snapshot to runner.");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[NarrativeAutoSetup] Error applying pending narrative snapshot: {ex.Message}");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[NarrativeAutoSetup] Error checking pending snapshot: {ex.Message}");
        }

        if (debugLogs)
            Debug.Log("[NarrativeAutoSetup] Listo: runner + signals + questService conectados.");
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
