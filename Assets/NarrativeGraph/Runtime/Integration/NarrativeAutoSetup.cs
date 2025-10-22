using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class NarrativeAutoSetup : MonoBehaviour
{
    // ÚNICO que debes asignar en el inspector
    [Header("Config obligatoria")]
    public NarrativeGraph graph;

    [Header("Debug opcional")]
    public bool debugLogs;

    // Singleton duro para persistir entre escenas
    private static NarrativeAutoSetup _instance;

    void Awake()
    {
        // Singleton + persistencia IMPERATIVA
        if (_instance != null && _instance != this)
        {
            if (debugLogs) Debug.Log("[NarrativeAutoSetup] Duplicado detectado. Destruyendo este.");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Asegura y conecta componentes
        var runner  = GetComponent<NarrativeRunner>()        ?? gameObject.AddComponent<NarrativeRunner>();
        var signals = GetComponent<DefaultNarrativeSignals>() ?? gameObject.AddComponent<DefaultNarrativeSignals>();
        var qs      = GetComponent<QuestServiceAdapter>()    ?? gameObject.AddComponent<QuestServiceAdapter>();

        // Asigna el asset de grafo
        if (!graph)
            Debug.LogWarning("[NarrativeAutoSetup] Graph no asignado. Asígnalo en el inspector.");
        runner.graph = graph;

        // Señales → servicio de quests
        signals.questServiceProvider = qs;

        // Runner → provider de señales (campo público o privado)
        var fi = typeof(NarrativeRunner).GetField("signalsProvider",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null) fi.SetValue(runner, signals);
        else Debug.LogWarning("[NarrativeAutoSetup] No se encontró 'signalsProvider' en NarrativeRunner.");

        // Aplicar snapshot pendiente del GameBootProfile (si existe)
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
            Debug.Log("[NarrativeAutoSetup] Listo: persistente, runner+signals+adapter conectados.");
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}