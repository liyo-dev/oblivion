using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using System.Collections;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

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
        // Autocompletar id por defecto si está vacío
        if (string.IsNullOrEmpty(singlePlayId) && cinematicSceneAsset != null)
            singlePlayId = cinematicSceneAsset.name;
    }
#endif

    // Added: permitir lectura/escritura del nombre de la escena desde código
    public string CinematicSceneName { get => cinematicSceneName; set => cinematicSceneName = value; }

    // Added: evento público que se dispara cuando la cinemática finaliza
    public event Action OnCinematicFinished;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool directorInAdditive = true;

    [Header("Playback Policy")]
    [Tooltip("Si está activo, esta cinemática (por escena) solo se reproducirá una vez por perfil.")]
    [SerializeField] private bool playOnlyOnce;
    [Tooltip("Identificador único para marcar la cinemática como vista. Si está vacío, se usa el nombre de la escena.")]
    [SerializeField] private string singlePlayId = "";

    [Header("Gameplay Lock")]
    [SerializeField] private GameObject[] toDisableDuringCinematic;

    [Header("Exit Positioning")]
    [Tooltip("Id del SpawnAnchor al que debe ir el jugador al terminar la cinemática (por fin natural o skip).")]
    [SerializeField] private string exitAnchorId = "";
    [Tooltip("Si true, registra este anchor como el 'actual' en el SpawnManager ANTES de reproducir.")]
    [SerializeField] private bool setAsCurrentAnchor = true;

    [Header("Return To Previous Position")]
    [Tooltip("Si está activo, al terminar la cinemática se restaurará la posición y la rotación previas del jugador, ignorando SpawnManager/Bootstrap.")]
    [SerializeField] private bool useLastPlayerPositionOnExit;

    [Header("Events")] 
    [Tooltip("Se dispara justo cuando la Timeline termina (natural o forzada) antes de descargar la escena.")]
    [SerializeField] private UnityEvent onCinematicFinished;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    AsyncOperation loadOp;
    PlayableDirector director;
    bool isUnloading;
    bool isPlaying;
    Coroutine watchdogCo;

    // Estado guardado del jugador
    Transform cachedPlayerTransform;
    Vector3 savedPlayerPosition;
    Quaternion savedPlayerRotation;
    bool hasSavedPlayerTransform;

    // Para no invocar doble las acciones de fin
    bool finishActionsInvoked;

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

        // Determinar ID único
        string id = GetSinglePlayId();

        // Preparar destino oficial de salida (no mueve, solo estado) salvo que ignoremos SpawnManager
        if (!useLastPlayerPositionOnExit && setAsCurrentAnchor && !string.IsNullOrEmpty(exitAnchorId))
        {
            SafeSetCurrentAnchor(exitAnchorId);
            if (showDebugLogs)
                Debug.Log($"[AdditiveSceneCinematic] CurrentAnchor preparado: '{exitAnchorId}'.");
        }

        // Si es solo una vez y ya se vio, finalizar sin reproducir
        if (playOnlyOnce && IsCinematicSeen(id))
        {
            if (showDebugLogs)
                Debug.Log($"[AdditiveSceneCinematic] Saltando cinemática ya vista: {id}");

            // Acciones de finalización equivalentes (sin cargar escena)
            TryInvokeSkipLikeActions(); // dispara eventos de fin si hay listeners
            // Rehabilitar gameplay explícitamente por si se desactivó algo previamente
            if (toDisableDuringCinematic != null)
                foreach (var go in toDisableDuringCinematic) if (go) go.SetActive(true);

            if (useLastPlayerPositionOnExit && hasSavedPlayerTransform)
            {
                RestorePlayerTransformIfPossible();
            }
            else
            {
                SafeTeleportToCurrent();
            }
            yield break;
        }

        // Guardar posición/rotación actual del jugador si así se desea (solo si vamos a reproducir)
        if (useLastPlayerPositionOnExit)
        {
            SavePlayerTransformIfPossible();
        }

        isUnloading = false;
        isPlaying = false;
        finishActionsInvoked = false;

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

            // Monitor robusto del fin de la timeline
            if (watchdogCo != null) StopCoroutine(watchdogCo);
            watchdogCo = StartCoroutine(MonitorDirector(director));
        }
        else
        {
            Debug.LogWarning("[AdditiveSceneCinematic] No se encontró PlayableDirector; descargando escena.");
            yield return Unload();
        }
    }

    IEnumerator MonitorDirector(PlayableDirector d)
    {
        // Timeout suave: duración + margen o valor fijo si duración no es válida
        float dur = (float)(double.IsNaN(d.duration) ? 0f : d.duration);
        bool hasValidDuration = dur > 0f && !float.IsInfinity(dur);
        float softTimeout = hasValidDuration ? dur + 0.5f : 30f;

        float elapsed = 0f;
        while (d != null && !isUnloading)
        {
            // 1) Fin del gráfico
            if (d.playableGraph.IsValid() && d.playableGraph.IsDone())
            {
                if (showDebugLogs) Debug.Log("[AdditiveSceneCinematic] PlayableGraph.IsDone -> finalizar cinemática.");
                TryInvokeSkipLikeActions();
                StartCoroutine(Unload());
                yield break;
            }

            // 2) Fin por tiempo
            if (hasValidDuration && d.time >= dur - 0.001f)
            {
                if (showDebugLogs) Debug.Log("[AdditiveSceneCinematic] Se alcanzó el final por tiempo -> Stop y finalizar.");
                d.Stop(); // dispara OnDirectorStopped, que a su vez finalizará
                yield break;
            }

            // 3) Timeout suave
            float dt = d.timeUpdateMode == DirectorUpdateMode.GameTime ? Time.deltaTime : Time.unscaledDeltaTime;
            elapsed += dt;
            if (elapsed >= softTimeout)
            {
                if (showDebugLogs) Debug.LogWarning("[AdditiveSceneCinematic] Timeout de cinemática. Forzando finalización.");
                // Si sigue en Play, detener para asegurar OnDirectorStopped
                if (d.state == PlayState.Playing) d.Stop();
                else { TryInvokeSkipLikeActions(); StartCoroutine(Unload()); }
                yield break;
            }

            yield return null;
        }
    }

    void Update()
    {
        // Respaldo adicional por si algo cambia en runtime
        if (isPlaying && director != null && !isUnloading)
        {
            bool graphDone = director.playableGraph.IsValid() && director.playableGraph.IsDone();
            bool byTime = director.duration > 0 && !double.IsInfinity(director.duration) && director.time >= director.duration - 0.001f;
            if (graphDone)
            {
                if (showDebugLogs)
                    Debug.Log("[AdditiveSceneCinematic] Detección en Update: PlayableGraph.IsDone.");
                isPlaying = false;
                director.stopped -= OnDirectorStopped;
                TryInvokeSkipLikeActions();
                StartCoroutine(Unload());
            }
            else if (byTime && director.state != PlayState.Playing)
            {
                if (showDebugLogs)
                    Debug.Log($"[AdditiveSceneCinematic] Timeline terminó detectado en Update. {director.time:F2}s / {director.duration:F2}s");
                isPlaying = false;
                director.stopped -= OnDirectorStopped;
                TryInvokeSkipLikeActions();
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
        // Invocar mismas acciones que 'hold A' antes de descargar
        TryInvokeSkipLikeActions();
        StartCoroutine(Unload());
    }

    void TryInvokeSkipLikeActions()
    {
        if (finishActionsInvoked) return;
        finishActionsInvoked = true;

        // 1) Evento configurable en el inspector
        try
        {
            onCinematicFinished?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdditiveSceneCinematic] onCinematicFinished lanzó excepción: {ex.Message}");
        }

        // 2) Compatibilidad: si hay un HoldToSkipUI activo, disparar su OnSkipCompleted
        try
        {
            var hold = FindFirstObjectByType<HoldToSkipUI>();
            if (hold != null)
            {
                if (hold.OnSkipCompleted != null)
                {
                    if (showDebugLogs) Debug.Log("[AdditiveSceneCinematic] Disparando HoldToSkipUI.OnSkipCompleted por fin automático.");
                    hold.OnSkipCompleted.Invoke();
                }
            }
        }
        catch { /* ignorar si no existe la clase o no hay instancia */ }

        // 3) Marcar como vista si aplica
        if (playOnlyOnce)
        {
            MarkCinematicSeen(GetSinglePlayId());
        }

        // Added: disparar el evento público de finalización para que callers puedan suscribirse
        try
        {
            OnCinematicFinished?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdditiveSceneCinematic] OnCinematicFinished lanzó excepción: {ex.Message}");
        }
    }

    // --- Single-play helpers ---
    string GetSinglePlayId()
    {
        if (!string.IsNullOrEmpty(singlePlayId)) return singlePlayId;
        return cinematicSceneName;
    }

    bool IsCinematicSeen(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var profile = GameBootService.Profile;
        if (profile == null) return false;
        var preset = profile.GetActivePresetResolved();
        if (preset == null) return false;
        if (preset.flags == null) return false;
        string flag = $"CINEMATIC_SEEN:{id}";
        return preset.flags.Contains(flag);
    }

    void MarkCinematicSeen(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var profile = GameBootService.Profile;
        if (profile == null) return;
        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;
        if (preset.flags == null) preset.flags = new List<string>();
        string flag = $"CINEMATIC_SEEN:{id}";
        if (!preset.flags.Contains(flag)) preset.flags.Add(flag);

        var saveSystem = FindFirstObjectByType<SaveSystem>();
        if (saveSystem != null)
        {
            profile.SaveCurrentGameState(saveSystem, SaveRequestContext.Auto);
        }
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

    // Added: método público que permite reproducir la cinemática y esperar hasta que finalice
    public IEnumerator PlayAndBlock()
    {
        bool finished = false;
        Action cb = () => finished = true;
        OnCinematicFinished += cb;

        // Iniciar la reproducción (Play() es IEnumerator y se encarga de cargar la escena y disparar la lógica de reproducción)
        StartCoroutine(Play());

        // Esperar hasta que TryInvokeSkipLikeActions invoque OnCinematicFinished
        yield return new WaitUntil(() => finished);

        OnCinematicFinished -= cb;
    }
}
