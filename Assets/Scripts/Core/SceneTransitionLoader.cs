// Assets/Scripts/Core/SceneTransitionLoader.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using EasyTransition; // TransitionSettings y (opcional) TransitionManager

/// <summary>
/// Servicio estático centralizado para cargar escenas.
/// - Load(): carga sin overlay (puede usar EasyTransition si se pasa settings).
/// - LoadWithOverlay(): carga con overlay de progreso (NO usa EasyTransition para evitar conflictos).
/// 
/// Notas:
/// - La overlay se carga en aditivo, se busca LoadingScreenController, se marca DontDestroyOnLoad
///   para sobrevivir a la activación de la escena destino y se destruye al final.
/// - Las corrutinas de UI (fades) siempre se lanzan desde un runner persistente.
/// </summary>
public static class SceneTransitionLoader
{
    // ====================== API PÚBLICA ======================

    /// <summary>Carga una escena por nombre, sin overlay de progreso.</summary>
    public static void Load(string targetScene,
                            TransitionSettings fade = null,
                            float fadeDelay = 0f)
    {
        EnsureRunner().StartCoroutine(LoadRoutine(targetScene, overlayScene: null, fade, fadeDelay));
    }

    /// <summary>
    /// Carga una escena por nombre mostrando overlay de carga/progreso.
    /// IMPORTANTE: al usar overlay, NO se dispara EasyTransition para evitar que su GameObject
    /// muera a mitad de transición. El fade visual lo realiza la propia overlay.
    /// </summary>
    public static void LoadWithOverlay(string targetScene,
                                       string overlayScene,
                                       TransitionSettings fade = null,
                                       float fadeDelay = 0f)
    {
        EnsureRunner().StartCoroutine(LoadRoutine(targetScene, overlayScene, fade, fadeDelay));
    }

    // ====================== Núcleo ======================

    private static LoaderRunner _runner;

    private static LoaderRunner EnsureRunner()
    {
        if (_runner != null) return _runner;

        var go = new GameObject("_SceneTransitionLoader");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<LoaderRunner>();
        return _runner;
    }

    private static IEnumerator LoadRoutine(string targetScene,
                                           string overlayScene, // null o vacío => sin overlay
                                           TransitionSettings fade,
                                           float fadeDelay)
    {
        bool hasOverlay = !string.IsNullOrEmpty(overlayScene);

        // 1) Transición externa (SOLO si NO hay overlay)
        if (!hasOverlay)
        {
            TryPlayExternalTransition(fade, fadeDelay);
        }

        // 2) Preparar overlay si procede
        LoadingScreenController ui = null;
        ILoadingUI loadingUI = null;
        Scene overlayLoaded = default;

        if (hasOverlay)
        {
            // Cargar overlay aditiva si no estaba ya cargada
            var existing = SceneManager.GetSceneByName(overlayScene);
            if (!existing.IsValid() || !existing.isLoaded)
            {
                var loadOverlay = SceneManager.LoadSceneAsync(overlayScene, LoadSceneMode.Additive);
                while (loadOverlay != null && !loadOverlay.isDone) yield return null;
                existing = SceneManager.GetSceneByName(overlayScene);
            }
            overlayLoaded = existing;

            // Buscar la UI en la overlay
            foreach (var root in overlayLoaded.GetRootGameObjects())
            {
                ui = root.GetComponentInChildren<LoadingScreenController>(true);
                if (ui) break;
            }
            if (!ui)
                ui = Object.FindFirstObjectByType<LoadingScreenController>(FindObjectsInactive.Include);

            if (!ui)
            {
                Debug.LogWarning($"[SceneTransitionLoader] No se encontró LoadingScreenController en '{overlayScene}'. Progreso no visible.");
            }
            else
            {
                // Mantener viva la UI durante el cambio de escena
                Object.DontDestroyOnLoad(ui.gameObject);

                loadingUI = ui;
                ui.ShowImmediate();
                // Fade-in de la UI desde el runner persistente
                yield return EnsureRunner().StartCoroutine(ui.Fade(0f, 1f));
            }
        }

        // 3) Carga asíncrona de la escena destino
        var op = SceneManager.LoadSceneAsync(targetScene);
        if (op == null)
        {
            Debug.LogError($"[SceneTransitionLoader] No se pudo iniciar la carga de '{targetScene}'");

            // Apagar overlay si estaba
            if (ui != null)
            {
                yield return EnsureRunner().StartCoroutine(ui.Fade(1f, 0f));
                ui.HideImmediate();
                Object.Destroy(ui.gameObject);
            }
            yield break;
        }

        op.allowSceneActivation = false;

        float shownAt = Time.unscaledTime;

        while (!op.isDone)
        {
            // Unity reporta 0..0.9 durante la carga; 1 al activar
            float raw = op.progress;                       // 0..0.9
            float normalized = (raw < 0.9f) ? raw / 0.9f : 1f;
            loadingUI?.SetProgress(normalized);

            if (raw >= 0.9f)
            {
                // Respetar tiempo mínimo visible de la overlay
                if (loadingUI != null)
                {
                    float elapsedShown = Time.unscaledTime - shownAt;
                    float remaining = Mathf.Max(0f, loadingUI.MinVisibleTime - elapsedShown);
                    if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
                }

                // Pequeño respiro antes de activar
                yield return new WaitForSecondsRealtime(0.05f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        // Llegados aquí, la escena destino está activa
        loadingUI?.SetProgress(1f);

        // 4) Apagar overlay con fade-out y limpieza
        if (ui != null)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            yield return EnsureRunner().StartCoroutine(ui.Fade(1f, 0f));
            ui.HideImmediate();
            Object.Destroy(ui.gameObject);
        }

        // Descargar la escena overlay si sigue cargada (por si acaso)
        if (hasOverlay && overlayLoaded.IsValid() && overlayLoaded.isLoaded)
        {
            var unload = SceneManager.UnloadSceneAsync(overlayLoaded);
            while (unload != null && !unload.isDone) yield return null;
        }

        // 5) (Opcional) Transición externa post (si quisieras una “entrada”),
        //     podrías lanzar aquí SIEMPRE QUE el TransitionManager sea persistente.
        //     Por defecto lo omitimos para evitar conflictos.
        yield break;
    }

    /// <summary>
    /// Intenta disparar transición externa (EasyTransition) si hay TransitionManager en escena.
    /// Solo se usa cuando NO hay overlay.
    /// </summary>
    private static void TryPlayExternalTransition(TransitionSettings settings, float delay)
    {
        if (settings == null) return;

        var mgr = Object.FindFirstObjectByType<TransitionManager>(FindObjectsInactive.Include);
        if (!mgr) return;

        try
        {
            // OJO: si el TransitionManager vive en una escena que se va a descargar,
            // también puede morir. Idealmente, colócalo bajo un objeto persistente (Start/CoreSystems)
            // con DontDestroyOnLoad para máxima seguridad.
            mgr.Transition(settings, delay);
        }
        catch (System.MissingMethodException)
        {
            Debug.LogWarning("[SceneTransitionLoader] TransitionManager encontrado, pero no tiene método Transition(TransitionSettings, float). Ajusta TryPlayExternalTransition().");
        }
    }

    /// <summary>Runner persistente (host de coroutines) para no depender de objetos que se destruyen al cambiar de escena.</summary>
    private sealed class LoaderRunner : MonoBehaviour { }
}
