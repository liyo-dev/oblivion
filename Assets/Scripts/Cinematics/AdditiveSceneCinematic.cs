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

    [Header("Return To Previous Position")]
    [Tooltip("Si está activo, al terminar la cinemática se restaurará la posición y la rotación previas del jugador, ignorando SpawnManager/Bootstrap.")]
    [SerializeField] private bool useLastPlayerPositionOnExit = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    AsyncOperation loadOp;
    PlayableDirector director;
    bool isUnloading = false;
    bool isPlaying = false;
    Coroutine watchdogCo;

    // Estado guardado del jugador
    Transform cachedPlayerTransform;
    Vector3 savedPlayerPosition;
    Quaternion savedPlayerRotation;
    bool hasSavedPlayerTransform = false;

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

        // Guardar posición/rotación actual del jugador si así se desea
        if (useLastPlayerPositionOnExit)
        {
            SavePlayerTransformIfPossible();
        }

        // Preparar destino oficial de salida (no mueve, solo estado) salvo que ignoremos SpawnManager
        if (!useLastPlayerPositionOnExit && setAsCurrentAnchor && !string.IsNullOrEmpty(exitAnchorId))
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

        // Posicionamiento de salida: o restauramos la posición previa del jugador o usamos SpawnManager
        if (useLastPlayerPositionOnExit && hasSavedPlayerTransform)
        {
            RestorePlayerTransformIfPossible();
        }
        else
        {
            // Teletransporte/posicionamiento de salida vía SpawnManager
            SafeTeleportToCurrent();
        }

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
            // Forzar teletransporte inmediato sin transición al salir de la cinemática
            SpawnManager.TeleportToCurrent(false);
            if (showDebugLogs) Debug.Log("[AdditiveSceneCinematic] Teleport via SpawnManager.TeleportToCurrent(false) (sin transición).");
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

    // Buscar el transform del jugador de forma segura
    Transform FindPlayerTransform()
    {
        if (cachedPlayerTransform != null) return cachedPlayerTransform;

        // Preferimos por Tag
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null)
        {
            // Fallback por nombre (menos fiable, pero útil si no hay tag configurado)
            // Obsoleto: FindObjectsOfType(true) -> usar FindObjectsByType con IncludeInactive
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t.name == "Player" || t.name.Contains("Player"))
                {
                    go = t.gameObject;
                    break;
                }
            }
        }

        if (go != null) cachedPlayerTransform = go.transform;
        return cachedPlayerTransform;
    }

    void SavePlayerTransformIfPossible()
    {
        var t = FindPlayerTransform();
        if (t != null)
        {
            savedPlayerPosition = t.position;
            savedPlayerRotation = t.rotation;
            hasSavedPlayerTransform = true;
            if (showDebugLogs)
                Debug.Log($"[AdditiveSceneCinematic] Guardada posición previa del jugador: {savedPlayerPosition} (rot: {savedPlayerRotation.eulerAngles}).");
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning("[AdditiveSceneCinematic] No se encontró jugador para guardar posición previa.");
        }
    }

    void RestorePlayerTransformIfPossible()
    {
        var t = FindPlayerTransform();
        if (t == null)
        {
            Debug.LogWarning("[AdditiveSceneCinematic] No se encontró jugador para restaurar posición previa.");
            return;
        }

        // Si hay CharacterController, conviene desactivarlo un frame para teleport limpio
        var cc = t.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        t.position = savedPlayerPosition;
        t.rotation = savedPlayerRotation; // siempre restauramos rotación

        // Reset de físicas si aplica
        var rb = t.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        var rb2d = t.GetComponent<Rigidbody2D>();
        if (rb2d != null) { rb2d.linearVelocity = Vector2.zero; rb2d.angularVelocity = 0f; }

        if (cc != null) cc.enabled = true;

        if (showDebugLogs)
            Debug.Log($"[AdditiveSceneCinematic] Restaurada posición y rotación previas del jugador: {savedPlayerPosition} / {savedPlayerRotation.eulerAngles}.");
    }
}
