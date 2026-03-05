// Assets/Scripts/Core/SceneTransitionLoader.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using EasyTransition; // TransitionSettings y (opcional) TransitionManager
using Sendero.Core.Feedback;

/// <summary>
/// Servicio estático centralizado para cargar escenas.
/// - Load(): carga sin overlay (puede usar EasyTransition si se pasa settings).
/// - LoadWithOverlay(): carga con overlay de progreso (NO usa EasyTransition para evitar conflictos).
/// 
/// Notas:
/// - La overlay se carga en aditivo, se busca LoadingScreenController, se marca DontDestroyOnLoad
///   para sobrevivir a la activación de la escena destino y se destruye al final.
/// - Las corrutinas de UI (fades) siempre se lanzan desde un runner persistente.
/// - Al finalizar la carga con overlay, se realiza un fade-in para una transición suave.
/// </summary>
public static class SceneTransitionLoader
{
    // Defaults configurable en runtime (p.ej., desde menús) para aplicar overlay/fade
    public static string DefaultOverlayScene = null; // si no es null/empty, Load() usará overlay
    public static TransitionSettings DefaultFade = null; // usado cuando NO hay overlay
    public static float DefaultFadeDelay = 0f;
    
    // Configuración del fade-in post-carga
    public static float PostLoadFadeDuration = 0.5f;
    public static Color PostLoadFadeColor = Color.black;
    // ====================== API PÚBLICA ======================

    /// <summary>Carga una escena por nombre, sin overlay de progreso.</summary>
    public static void Load(string targetScene,
                            TransitionSettings fade = null,
                            float fadeDelay = 0f)
    {
        // Si hay overlay por defecto configurado, redirigir a LoadWithOverlay
        if (!string.IsNullOrEmpty(DefaultOverlayScene))
        {
            EnsureRunner().StartCoroutine(LoadRoutine(targetScene, DefaultOverlayScene, DefaultFade ?? fade, DefaultFadeDelay > 0f ? DefaultFadeDelay : fadeDelay));
            return;
        }
        EnsureRunner().StartCoroutine(LoadRoutine(targetScene, overlayScene: null, fade ?? DefaultFade, (fadeDelay > 0f) ? fadeDelay : DefaultFadeDelay));
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
                ui = ServiceLocator.Get<LoadingScreenController>(false);

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

                // Poner pantalla en negro ANTES de activar la escena para evitar el parpadeo:
                // la overlay de carga sigue encima en este punto, así que no se ve nada raro
                // mientras se crea el canvas. Cuando la escena destino active, ya estará cubierta.
                if (hasOverlay && PostLoadFadeDuration > 0f)
                    FeedbackService.SetScreenFadeImmediate(PostLoadFadeColor);

                op.allowSceneActivation = true;
            }

            yield return null;
        }

        // Llegados aquí, la escena destino está activa
        loadingUI?.SetProgress(1f);

        // Seguridad: asegurar que timeScale esté a 1 al cargar una escena
        Time.timeScale = 1f;

        // 4) La pantalla ya está en negro (se puso antes de activar la escena).
        //    Pasamos directamente a apagar la overlay.

        // 5) Apagar overlay con fade-out y limpieza
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

        // 6) Fade-out suave desde negro (revela la escena)
        if (hasOverlay && PostLoadFadeDuration > 0f)
        {
            // Pequeña pausa para que la escena se estabilice
            yield return new WaitForSecondsRealtime(0.15f);
            
            // Fade-out desde negro (revela la escena)
            yield return EnsureRunner().StartCoroutine(
                FeedbackService.ScreenFadeAsync(PostLoadFadeColor, PostLoadFadeDuration, fadeIn: false));
        }

        yield break;
    }

    /// <summary>
    /// Intenta disparar transición externa (EasyTransition) si hay TransitionManager en escena.
    /// Solo se usa cuando NO hay overlay.
    /// </summary>
    private static void TryPlayExternalTransition(TransitionSettings settings, float delay)
    {
        if (settings == null) return;

        var mgr = ServiceLocator.Get<TransitionManager>(false);
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
