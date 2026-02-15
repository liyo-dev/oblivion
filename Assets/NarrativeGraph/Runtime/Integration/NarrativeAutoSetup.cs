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
        // NO iniciar el grafo aquí.
        // El NarrativeGraphStarter en la escena se encargará de:
        // 1. Restaurar blackboards desde el runtimePreset (sea de JSON o preset de testeo)
        // 2. Iniciar el grafo, que verificará __currentNodeGuid y continuará desde ahí
        //
        // Esto garantiza que el sistema es agnóstico al origen de los datos.
        if (debugLogs) Debug.Log("[NarrativeAutoSetup] Start() - grafo NO iniciado aquí (lo hará NarrativeGraphStarter)");
    }

    void TryBootstrapQuestSignals()
    {
        // MÉTODO DESHABILITADO - Causaba emisiones duplicadas de eventos
        // Los eventos deben ser emitidos por los Interactables cuando corresponda
        
        /*
        if (_signals == null) return;
        var qm = QuestManager.Instance;
        if (qm == null) return;

        var questState = qm.GetState("ELDRAN_MISSION1");
        if (questState >= QuestState.Completed)
        {
            if (debugLogs) Debug.Log("[NarrativeAutoSetup] Quest ELDRAN_MISSION1 completed; raising LETTER_START");
            _signals.RaiseCustom("LETTER_START");
        }
        */
    }

    public static void ResetForNewGame()
    {
        if (_instance == null) return;
        _instance.HandleReset("ResetForNewGame", clearBlackboard: true, restartGraph: true);
    }

    public static void ResetForLoadedProfile()
    {
        if (_instance == null) return;
        // Al cargar partida, NO limpiamos el blackboard ni reiniciamos el grafo
        // El estado del grafo se restaurará desde los flags del preset
        // Solo reseteamos los servicios de señales y quests para que se re-sincronicen
        // IMPORTANTE: Preservamos eventos pendientes porque el trigger puede activarse antes que el grafo
        _instance.HandleReset("ResetForLoadedProfile", clearBlackboard: false, restartGraph: false, preservePending: true);
    }

    void HandleReset(string reason, bool clearBlackboard = false, bool restartGraph = false, bool preservePending = false)
    {
        if (debugLogs) Debug.Log($"[NarrativeAutoSetup] {reason}() - clearBlackboard={clearBlackboard}, restartGraph={restartGraph}, preservePending={preservePending}");

        _signals?.ResetState(preservePending);
        _questService?.ResetState();
        
        if (_runner != null)
        {
            if (clearBlackboard)
            {
                _runner.Blackboard?.Clear();
            }
            
            if (restartGraph)
            {
                _runner.StartFromStartNode();
            }
        }
    }
}
