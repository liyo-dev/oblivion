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

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool directorInAdditive = true;

    [Header("Gameplay Lock")]
    [SerializeField] private GameObject[] toDisableDuringCinematic;

    [Header("Exit Positioning")]
    [Tooltip("Id del SpawnAnchor al que debe ir el jugador al terminar la cinemática (por fin natural o skip).")]
    [SerializeField] private string exitAnchorId = "";
    [Tooltip("Si true, registra este anchor como el 'actual' en el SpawnManager ANTES de reproducir.")]
    [SerializeField] private bool setAsCurrentAnchor = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    AsyncOperation loadOp;
    PlayableDirector director;
    bool isUnloading = false;
    bool isPlaying = false;
    Coroutine watchdogCo;

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

        // Preparar destino oficial de salida (no mueve, solo estado)
        if (setAsCurrentAnchor && !string.IsNullOrEmpty(exitAnchorId))
        {
            SafeSetCurrentAnchor(exitAnchorId);
            if (showDebugLogs)
                Debug.Log($"[AdditiveSceneCinematic] CurrentAnchor preparado: '{exitAnchorId}'.");
        }

        isUnloading = false;
        isPlaying = false;

        if (showDebugLogs)
            Debug.Log($"[AdditiveSceneCinematic] Desactivando gameplay y cargando escena: {cinematicSceneName}");

        if (toDisableDuringCinematic != null)
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
            // Limpieza previa + subscribir
            director.stopped -= OnDirectorStopped;
            director.stopped += OnDirectorStopped;

            // Clave: forzar finalización real (emite 'stopped')
            director.extrapolationMode = DirectorWrapMode.None;

            if (showDebugLogs)
                Debug.Log($"[AdditiveSceneCinematic] PlayableDirector encontrado. Duración: {director.duration:F2}s. Iniciando reproducción...");

            director.Play();
            isPlaying = true;

            // Watchdog: si no llega 'stopped', forzamos Stop()
            if (watchdogCo != null) StopCoroutine(watchdogCo);
            watchdogCo = StartCoroutine(WatchDirectorEnd(director));
        }
        else
        {
            Debug.LogWarning("[AdditiveSceneCinematic] No se encontró PlayableDirector; descargando escena.");
            yield return Unload();
        }
    }

    IEnumerator WatchDirectorEnd(PlayableDirector d)
    {
        float Elapsed() => d.timeUpdateMode == DirectorUpdateMode.GameTime ? Time.deltaTime : Time.unscaledDeltaTime;

        var target = Mathf.Max(0.05f, (float)d.duration) + 0.25f;
        float t = 0f;

        while (d != null && !isUnloading && d.state == PlayState.Playing)
        {
            t += Elapsed();

            if (d.time >= d.duration - 0.001f)
                break;

            if (t >= target)
                break;

            yield return null;
        }

        if (d != null && !isUnloading && d.state == PlayState.Playing)
        {
            if (showDebugLogs)
                Debug.Log("[AdditiveSceneCinematic] Watchdog forzando Stop() de la Timeline.");
            d.Stop(); // disparará OnDirectorStopped
        }
    }

    void Update()
    {
        // Respaldo por si el wrapMode se tocó en runtime
        if (isPlaying && director != null && !isUnloading)
        {
            if (director.time >= director.duration && director.state != PlayState.Playing)
            {
                if (showDebugLogs)
                    Debug.Log($"[AdditiveSceneCinematic] Timeline terminó detectado en Update. {director.time:F2}s / {director.duration:F2}s");
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
            Debug.Log($"[AdditiveSceneCinematic] Director detenido ({d.time:F2}s / {d.duration:F2}s). Iniciando descarga...");

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

        if (watchdogCo != null) { StopCoroutine(watchdogCo); watchdogCo = null; }

        if (showDebugLogs)
            Debug.Log($"[AdditiveSceneCinematic] Descargando escena: {cinematicSceneName}");

        var scn = SceneManager.GetSceneByName(cinematicSceneName);
        if (scn.isLoaded)
            yield return SceneManager.UnloadSceneAsync(scn);

        if (showDebugLogs)
            Debug.Log("[AdditiveSceneCinematic] Reactivando gameplay.");

        if (toDisableDuringCinematic != null)
            foreach (var go in toDisableDuringCinematic) if (go) go.SetActive(true);

        // Teletransporte/posicionamiento de salida
        SafeTeleportToCurrent();

        director = null;
        isUnloading = false;
    }

    void OnDestroy()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;
        if (watchdogCo != null)
            StopCoroutine(watchdogCo);
    }

    // -------- Helpers --------

    // Siempre delega en SpawnManager para ir al anchor actual
    void SafeTeleportToCurrent()
    {
        try
        {
            SpawnManager.TeleportToCurrent();
            if (showDebugLogs) Debug.Log("[AdditiveSceneCinematic] Teleport via SpawnManager.TeleportToCurrent().");
        }
        catch
        {
            Debug.LogWarning("[AdditiveSceneCinematic] No se pudo teletransportar al CurrentAnchor. Revisa SpawnManager/anchors.");
        }
    }

    void SafeSetCurrentAnchor(string anchorId)
    {
        try
        {
            SpawnManager.SetCurrentAnchor(anchorId);
        }
        catch
        {
            Debug.LogWarning($"[AdditiveSceneCinematic] No se pudo SetCurrentAnchor('{anchorId}'). ¿Existe ese SpawnAnchor en MainWorld?");
        }
    }
}
