using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

/// <summary>
/// Controlador simple de pantalla de carga.
/// - Mantenerlo como singleton (DontDestroyOnLoad).
/// - Exponer una coroutine pública LoadSceneRoutine(string) que muestra la UI, carga la escena y la oculta.
/// - Requiere asignar en el inspector: Panel (CanvasGroup), ProgressBar (Slider) y ProgressText (Text).
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    public static LoadingScreenController Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("CanvasGroup del panel de la pantalla de carga (para fade in/out).")]
    public CanvasGroup panel;
    [Tooltip("Slider que muestra el progreso (0..1).")]
    public Slider progressBar;
    [Tooltip("Texto que muestra porcentaje (opcional)")]
    public Text progressText;

    [Header("Timing")]
    public float fadeDuration = 0.25f;
    public float minVisibleTime = 0.5f;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else if (Instance != this) { Destroy(gameObject); return; }

        if (panel != null)
        {
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Safe show: if panel not assigned, logs a warning and does nothing.
    /// </summary>
    public void ShowImmediate()
    {
        if (panel == null) { Debug.LogWarning("[LoadingScreen] Panel not assigned."); return; }
        panel.gameObject.SetActive(true);
        panel.alpha = 1f;
        SetProgress(0f);
    }

    public void HideImmediate()
    {
        if (panel == null) return;
        panel.alpha = 0f;
        panel.gameObject.SetActive(false);
    }

    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (progressBar != null) progressBar.value = t;
        if (progressText != null) progressText.text = Mathf.RoundToInt(t * 100f) + "%";
    }

    IEnumerator Fade(float from, float to)
    {
        if (panel == null) yield break;
        float elapsed = 0f;
        panel.alpha = from;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        panel.alpha = to;
    }

    /// <summary>
    /// Coroutine pública que carga una escena mostrando la UI de carga.
    /// - Si el AsyncOperation.progress llega a 0.9, espera un pequeño retardo y activa la escena.
    /// - Normaliza el progreso para mostrar 0..1 (0..0.9 -> 0..1 during load).
    /// </summary>
    public IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (panel == null)
        {
            Debug.LogWarning("[LoadingScreen] Panel no asignado. Cargando escena sin pantalla de carga.");
            var fallback = SceneManager.LoadSceneAsync(sceneName);
            yield return fallback;
            yield break;
        }

        panel.gameObject.SetActive(true);
        // Fade in
        yield return StartCoroutine(Fade(0f, 1f));

        // Ensure minimal visible time
        float shownAt = Time.unscaledTime;

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[LoadingScreen] No se pudo iniciar la carga de escena '{sceneName}'");
            yield return StartCoroutine(Fade(1f, 0f));
            panel.gameObject.SetActive(false);
            yield break;
        }

        // Control activation so we can show full progress
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            // Unity reports progress 0..0.9 while loading, and jumps to 1 when activated
            float raw = op.progress; // 0..0.9
            float normalized = (raw < 0.9f) ? (raw / 0.9f) : 1f;
            SetProgress(normalized);

            // When we reached 0.9 we can finalize
            if (raw >= 0.9f)
            {
                // Guarantee minimum visible time
                float elapsedShown = Time.unscaledTime - shownAt;
                float remaining = Mathf.Max(0f, minVisibleTime - elapsedShown);
                if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);

                // short delay to let last frame render
                yield return new WaitForSecondsRealtime(0.1f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        // Make sure progress is 100%
        SetProgress(1f);

        // small pause then fade out
        yield return new WaitForSecondsRealtime(0.15f);
        yield return StartCoroutine(Fade(1f, 0f));
        panel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Helper to start the load routine from other code without needing a runner.
    /// </summary>
    public void StartLoad(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    // Convenience: static helper that ensures existence and starts loading
    public static void LoadScene(string sceneName)
    {
        if (Instance == null)
        {
            var go = new GameObject("_LoadingScreenController");
            Instance = go.AddComponent<LoadingScreenController>();
            DontDestroyOnLoad(go);
            Debug.LogWarning("[LoadingScreen] Instantiated LoadingScreenController at runtime - assign UI refs in inspector for visual feedback.");
        }
        Instance.StartLoad(sceneName);
    }
}

