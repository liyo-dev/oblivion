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
    DefaultNarrativeSignals _signals;
    QuestServiceAdapter _questService;
    NarrativeRunner _runner;

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

        _runner = GetComponent<NarrativeRunner>() ?? gameObject.AddComponent<NarrativeRunner>();
        _signals = GetComponent<DefaultNarrativeSignals>() ?? gameObject.AddComponent<DefaultNarrativeSignals>();
        _questService = GetComponent<QuestServiceAdapter>() ?? gameObject.AddComponent<QuestServiceAdapter>();

        if (!graph)
            Debug.LogWarning("[NarrativeAutoSetup] Graph no asignado. Asigna uno en el inspector.");
        _runner.graph = graph;

        _signals.questServiceProvider = _questService;

        var fi = typeof(NarrativeRunner).GetField("signalsProvider",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null) fi.SetValue(_runner, _signals);
        else Debug.LogWarning("[NarrativeAutoSetup] No se encontró 'signalsProvider' en NarrativeRunner.");

        // Snapshot narrativo eliminado; no hay pending que aplicar

        if (debugLogs)
            Debug.Log("[NarrativeAutoSetup] Listo: runner + signals + questService conectados.");
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        TryBootstrapQuestSignals();
    }

    void TryBootstrapQuestSignals()
    {
        if (_signals == null) return;
        var qm = QuestManager.Instance;
        if (qm == null) return;

        var questState = qm.GetState("ELDRAN_MISSION1");
        if (questState >= QuestState.Completed)
        {
            if (debugLogs) Debug.Log("[NarrativeAutoSetup] Quest ELDRAN_MISSION1 completed; raising LETTER_START");
            _signals.RaiseCustom("LETTER_START");
        }
    }

    public static void ResetForNewGame()
    {
        if (_instance == null) return;
        _instance.HandleReset("ResetForNewGame");
    }

    public static void ResetForLoadedProfile()
    {
        if (_instance == null) return;
        _instance.HandleReset("ResetForLoadedProfile", rebootstrapSignals: true);
    }

    void HandleReset(string reason, bool rebootstrapSignals = false)
    {
        if (debugLogs) Debug.Log($"[NarrativeAutoSetup] {reason}()");

        _signals?.ResetState();
        _questService?.ResetState();
        _runner?.RestartFromStartNode(resetBlackboard: true);

        if (rebootstrapSignals)
            TryBootstrapQuestSignals();
    }
}
