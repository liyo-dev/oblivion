using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script para iniciar grafos narrativos específicos en una escena de gameplay.
/// Coloca este script en un GameObject de la escena y configura qué grafos deben iniciarse.
/// </summary>
public class NarrativeGraphStarter : MonoBehaviour
{
    [Header("Grafos a Iniciar")]
    [Tooltip("Etiquetas de los grafos del NarrativeGraphHub que se iniciarán al cargar esta escena.")]
    [SerializeField] private string[] graphLabels = new string[] { "Historia Principal" };

    [Header("Configuración")]
    [Tooltip("Si está activado, los grafos se inician en Start(). Si no, debes llamar a StartGraphs() manualmente.")]
    [SerializeField] private bool startOnAwake = true;
    [Tooltip("Si está activo, solo se inician los grafos cuando la escena que contiene este componente es la activa.")]
    [SerializeField] private bool requireActiveScene = true;

    [Header("Debug")]
    [SerializeField] private bool logDebug = true;

    void Start()
    {
        if (startOnAwake)
        {
            StartCoroutine(WaitForHubAndStart());
        }
    }

    /// <summary>
    /// Espera a que NarrativeGraphHub esté disponible antes de iniciar los grafos.
    /// Esto soporta la carga aditiva de la escena Start en el editor.
    /// </summary>
    private System.Collections.IEnumerator WaitForHubAndStart()
    {
        // Esperar al menos un frame para que Start() de todos los componentes se complete.
        // Esto garantiza que NPCInteractiveNarrativeExecutor.Start() → RestoreState() corra
        // antes de que el runner empiece a ejecutar StartQuestNode y dispare OnQuestStarted.
        yield return null;

        // Esperar hasta que el Hub esté disponible
        while (NarrativeGraphHub.Instance == null)
        {
            yield return null;
        }

        // Evitar iniciar grafos si la escena aún no es la activa (p.ej. Start cargada aditivamente en el editor)
        if (requireActiveScene)
        {
            while (gameObject != null
                && gameObject.scene.IsValid()
                && gameObject.scene.isLoaded
                && SceneManager.GetActiveScene() != gameObject.scene)
            {
                yield return null;
            }

            if (gameObject == null || !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                if (logDebug)
                    Debug.LogWarning("[NarrativeGraphStarter] Escena no válida o destruida antes de iniciar grafos.");
                yield break;
            }
        }

        StartGraphs();
    }

    /// <summary>
    /// Inicia todos los grafos configurados en la lista.
    /// Si hay un estado guardado previo, lo restaura primero.
    /// </summary>
    public void StartGraphs()
    {
        if (requireActiveScene && SceneManager.GetActiveScene() != gameObject.scene)
        {
            // if (logDebug)
            //     Debug.Log($"[NarrativeGraphStarter] Escena '{gameObject.scene.name}' no es la activa. Inicio de grafos omitido.");
            return;
        }

        if (graphLabels == null || graphLabels.Length == 0)
        {
            // if (logDebug)
            //     Debug.LogWarning("[NarrativeGraphStarter] No hay grafos configurados para iniciar.");
            return;
        }

        var hub = NarrativeGraphHub.Instance;
        if (hub == null)
        {
            Debug.LogError("[NarrativeGraphStarter] NarrativeGraphHub no encontrado. Asegúrate de que existe en la escena.");
            return;
        }

        // Restaurar blackboards guardados si existen (desde el preset)
        RestoreBlackboardsFromPreset(hub);

        // if (logDebug)
        //     Debug.Log($"[NarrativeGraphStarter] Iniciando {graphLabels.Length} grafo(s) en escena '{gameObject.scene.name}'");

        foreach (var label in graphLabels)
        {
            if (string.IsNullOrEmpty(label))
            {
                // if (logDebug)
                //     Debug.LogWarning("[NarrativeGraphStarter] Etiqueta vacía en la lista, saltando...");
                continue;
            }

            // El runner se encargará de verificar si hay estado guardado
            // y continuará desde el nodo guardado automáticamente
            hub.StartGraph(label);
        }
    }

    /// <summary>
    /// Restaura los blackboards desde el preset guardado.
    /// </summary>
    private void RestoreBlackboardsFromPreset(NarrativeGraphHub hub)
    {
        if (!GameBootService.IsAvailable)
        {
            Debug.LogWarning("[NarrativeGraphStarter] ⚠️ GameBootService no disponible - no hay blackboards para restaurar");
            return;
        }

        var profile = GameBootService.Profile;
        if (profile == null)
        {
            Debug.LogWarning("[NarrativeGraphStarter] ⚠️ GameBootProfile es null");
            return;
        }

        var preset = profile.GetActivePresetResolved();
        if (preset == null)
        {
            Debug.LogWarning("[NarrativeGraphStarter] ⚠️ Preset es null");
            return;
        }

        if (preset.narrativeBlackboards == null)
        {
            Debug.LogWarning($"[NarrativeGraphStarter] ⚠️ preset.narrativeBlackboards es null para preset '{preset.name}' - Los grafos iniciarán desde el nodo Start");
            return;
        }

        if (preset.narrativeBlackboards.Count == 0)
        {
            Debug.LogWarning($"[NarrativeGraphStarter] ⚠️ preset.narrativeBlackboards está vacío para preset '{preset.name}' - Los grafos iniciarán desde el nodo Start");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[NarrativeGraphStarter] 🔄 Restaurando blackboards desde preset '{preset.name}' ({preset.narrativeBlackboards.Count} grafos)");
#endif

        hub.RestoreBlackboards(preset.narrativeBlackboards);
    }

    /// <summary>
    /// Inicia un grafo específico por su etiqueta.
    /// </summary>
    public void StartGraph(string label)
    {
        var hub = NarrativeGraphHub.Instance;
        if (hub == null)
        {
            Debug.LogError("[NarrativeGraphStarter] NarrativeGraphHub no encontrado.");
            return;
        }

        hub.StartGraph(label);
    }
}
