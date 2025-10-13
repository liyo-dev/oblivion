using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using System.Collections;

public class AdditiveSceneCinematic : MonoBehaviour
{
    [Header("Cinematic Scene")]
    [SerializeField] private string cinematicSceneName = "";   // usado en runtime

#if UNITY_EDITOR
    [SerializeField] private UnityEditor.SceneAsset cinematicSceneAsset; // solo para drag&drop en editor
    void OnValidate()
    {
        if (cinematicSceneAsset != null)
            cinematicSceneName = cinematicSceneAsset.name; // copiamos el nombre real de la escena
    }
#endif

    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool directorInAdditive = true;

    [Header("Gameplay Lock")]
    [SerializeField] private GameObject[] toDisableDuringCinematic;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    AsyncOperation loadOp;
    PlayableDirector director;
    bool isUnloading = false;
    bool isPlaying = false;

    IEnumerator Start()
    {
        if (!playOnStart) yield break;
        yield return Play();
    }

    public IEnumerator Play()
    {
        if (string.IsNullOrEmpty(cinematicSceneName))
        {
            Debug.LogWarning("[AdditiveSceneCinematic] No hay escena asignada.");
            yield break;
        }

        isUnloading = false;
        isPlaying = false;

        if (showDebugLogs)
            Debug.Log($"[AdditiveSceneCinematic] Desactivando gameplay y cargando escena: {cinematicSceneName}");

        foreach (var go in toDisableDuringCinematic) if (go) go.SetActive(false);

        loadOp = SceneManager.LoadSceneAsync(cinematicSceneName, LoadSceneMode.Additive);
        yield return loadOp;

        if (directorInAdditive)
        {
            var scn = SceneManager.GetSceneByName(cinematicSceneName);
            foreach (var root in scn.GetRootGameObjects())
            {
                director = director ?? root.GetComponentInChildren<PlayableDirector>(true);
                if (director) break;
            }
        }
        else
        {
            director = GetComponentInChildren<PlayableDirector>(true);
        }

        if (director)
        {
            // Asegurarse de que no haya suscripciones previas
            director.stopped -= OnDirectorStopped;
            director.stopped += OnDirectorStopped;
            
            if (showDebugLogs)
                Debug.Log($"[AdditiveSceneCinematic] PlayableDirector encontrado. Duración: {director.duration}s. Iniciando reproducción...");
            
            director.Play();
            isPlaying = true;
        }
        else
        {
            Debug.LogWarning("[AdditiveSceneCinematic] No se encontró PlayableDirector; descargando escena.");
            yield return Unload();
        }
    }

    void Update()
    {
        // Verificación adicional: si el director terminó de reproducir pero no se disparó el evento
        if (isPlaying && director != null && !isUnloading)
        {
            // Si el tiempo actual es mayor o igual a la duración Y no está reproduciendo
            if (director.time >= director.duration && director.state != PlayState.Playing)
            {
                if (showDebugLogs)
                    Debug.Log($"[AdditiveSceneCinematic] Timeline terminó detectado en Update. Tiempo: {director.time:F2}s / {director.duration:F2}s");
                
                isPlaying = false;
                director.stopped -= OnDirectorStopped;
                StartCoroutine(Unload());
            }
        }
    }

    void OnDirectorStopped(PlayableDirector d)
    {
        if (isUnloading)
        {
            if (showDebugLogs)
                Debug.Log("[AdditiveSceneCinematic] Ya está descargando, ignorando llamada duplicada.");
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[AdditiveSceneCinematic] Director detenido (tiempo: {d.time:F2}s / {d.duration:F2}s). Iniciando descarga...");

        isPlaying = false;
        d.stopped -= OnDirectorStopped;
        StartCoroutine(Unload());
    }

    IEnumerator Unload()
    {
        if (isUnloading)
        {
            if (showDebugLogs)
                Debug.Log("[AdditiveSceneCinematic] Ya está descargando.");
            yield break;
        }

        isUnloading = true;
        isPlaying = false;

        if (showDebugLogs)
            Debug.Log($"[AdditiveSceneCinematic] Descargando escena: {cinematicSceneName}");

        var scn = SceneManager.GetSceneByName(cinematicSceneName);
        if (scn.isLoaded)
            yield return SceneManager.UnloadSceneAsync(scn);

        if (showDebugLogs)
            Debug.Log("[AdditiveSceneCinematic] Reactivando gameplay.");

        foreach (var go in toDisableDuringCinematic) if (go) go.SetActive(true);
        
        director = null;
        isUnloading = false;
    }

    void OnDestroy()
    {
        // Limpieza de eventos al destruir el objeto
        if (director != null)
        {
            director.stopped -= OnDirectorStopped;
        }
    }
}
