using UnityEngine;
using UnityEngine.SceneManagement;
using EasyTransition;
using System.Collections;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class SceneTransitionLoader : MonoBehaviour
{
    private static SceneTransitionLoader _inst;
    public static SceneTransitionLoader Instance => _inst;

    [Header("Valores por defecto")]
    public TransitionSettings defaultSettings;     // ← asigna tu Fade.asset aquí
    [Min(0)] public float defaultDelay = 0f;
    [Min(0)] public float waitForManagerSec = 0.5f;

    // Evita arranques concurrentes
    private bool _isLoading = false;

    void Awake()
    {
        if (_inst == null)
        {
            _inst = this;
            DontDestroyOnLoad(gameObject);
            return;
        }
        if (_inst != this)
        {
            // Prefiere la instancia que tenga defaultSettings asignado
            bool prevOk = _inst.defaultSettings != null;
            bool thisOk = this.defaultSettings != null;
            if (!prevOk && thisOk)
            {
                Destroy(_inst.gameObject);
                _inst = this;
                DontDestroyOnLoad(gameObject);
            }
            else Destroy(gameObject);
        }
    }

    // APIs públicas
    public static void Load(string sceneName)
    {
        if (_inst)
        {
            Debug.Log($"SceneTransitionLoader.Load requested: {sceneName}");
            if (_inst._isLoading)
            {
                Debug.LogWarning("SceneTransitionLoader: Load already in progress; ignoring request.");
                return;
            }
            // marcamos inmediatamente para evitar reentradas mientras la coroutine no haya comenzado aún
            _inst._isLoading = true;
            _inst.StartCoroutine(_inst.LoadRoutine(sceneName, _inst.defaultSettings, _inst.defaultDelay));
        }
        else
        {
            Debug.Log("SceneTransitionLoader: No instance found, loading scene directly.");
            SceneManager.LoadScene(sceneName);
        }
    }

    public static void Load(string sceneName, TransitionSettings settings, float delay = 0f)
    {
        if (_inst)
        {
            Debug.Log($"SceneTransitionLoader.Load requested (with settings): {sceneName}");
            if (_inst._isLoading)
            {
                Debug.LogWarning("SceneTransitionLoader: Load already in progress; ignoring request.");
                return;
            }
            // marcamos inmediatamente para evitar reentradas mientras la coroutine no haya comenzado aún
            _inst._isLoading = true;
            _inst.StartCoroutine(_inst.LoadRoutine(sceneName, settings ?? _inst.defaultSettings, delay));
        }
        else
        {
            Debug.Log("SceneTransitionLoader: No instance found, loading scene directly (settings provided).");
            SceneManager.LoadScene(sceneName);
        }
    }

    // Núcleo: transición SIN cambio de escena + carga en el "cut"
    IEnumerator LoadRoutine(string sceneName, TransitionSettings settings, float delay)
    {
        Debug.Log($"SceneTransitionLoader.LoadRoutine start for {sceneName}");
        // marcamos que estamos en proceso
        _isLoading = true;
        TransitionManager tm = null;
        UnityAction onCut = null;
        UnityAction onEnd = null;

        try
        {
            // Si seguimos sin settings, no llames al plugin (evita su error)
            if (settings == null)
            {
                Debug.LogWarning($"SceneTransitionLoader: No TransitionSettings provided for {sceneName}, loading directly.");
                SceneManager.LoadScene(sceneName);
                yield break;
            }

            // Espera a que exista el TransitionManager (Start entra aditivo)
            float t = 0f;
            while (t < waitForManagerSec && (tm = FindTM()) == null)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            if (tm == null) tm = FindTM();

            if (tm == null)
            {
                Debug.LogWarning($"SceneTransitionLoader: TransitionManager not found for {sceneName}, loading directly.");
                SceneManager.LoadScene(sceneName); // fallback seguro
                yield break;
            }

            // Si los settings no contienen prefabs válidos, intentar usar defaultSettings o hacer fallback
            if (settings != null && !(settings.transitionIn != null || settings.transitionOut != null))
            {
                if (this.defaultSettings != null && (this.defaultSettings.transitionIn != null || this.defaultSettings.transitionOut != null))
                {
                    settings = this.defaultSettings;
                    Debug.LogWarning($"SceneTransitionLoader: Provided settings invalid for {sceneName}, using defaultSettings.");
                }
                else
                {
                    // No hay transición válida disponible: carga directa y salimos
                    Debug.LogWarning($"SceneTransitionLoader: No valid transition prefabs found for {sceneName}, loading directly.");
                    SceneManager.LoadScene(sceneName);
                    yield break;
                }
            }

            // Evitar llamar a tm.Transition si el TransitionManager ya está en medio de otra transición.
            bool isRunning = false;
            try
            {
                var fi = typeof(TransitionManager).GetField("runningTransition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fi != null)
                    isRunning = (bool)fi.GetValue(tm);
            }
            catch (System.Exception ex) { Debug.LogException(ex); isRunning = false; }

            if (isRunning)
            {
                // Espera hasta un timeout razonable a que termine la transición actual
                float waitTimeout = 3f; // aumentar timeout para reducir false fallbacks
                float waited = 0f;
                while (waited < waitTimeout)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                    try
                    {
                        var fi = typeof(TransitionManager).GetField("runningTransition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (fi == null) { isRunning = false; break; }
                        isRunning = (bool)fi.GetValue(tm);
                        if (!isRunning) break;
                    }
                    catch (System.Exception ex) { Debug.LogException(ex); isRunning = false; break; }
                }

                if (isRunning)
                {
                    // Tras timeout, evitamos bloquear la experiencia: cargar sin transición
                    Debug.LogWarning($"SceneTransitionLoader: TransitionManager busy; loading scene {sceneName} without transition to avoid conflict.");
                    SceneManager.LoadScene(sceneName);
                    yield break;
                }
            }

            // Ahora que el manager está libre, suscribimos los handlers locales y lanzamos la transición.
            // Preparar control de finalización y carga única de escena
            bool completed = false;
            bool sceneLoaded = false;

            // Redefinimos los handlers para usar las banderas locales de control
            onCut = () =>
            {
                Debug.Log($"SceneTransitionLoader: onCut invoked for {sceneName}");
                // Dejar que el TransitionManager haga la carga de escena en su Timer(string,...)
                sceneLoaded = true; // marcar para evitar fallback manual
                Debug.Log($"SceneTransitionLoader: onCut - plugin will load the scene: {sceneName}");
                completed = true;

                try { if (tm != null) tm.onTransitionCutPointReached -= onCut; } catch (System.Exception ex) { Debug.LogException(ex); }
            };

            onEnd = () =>
            {
                Debug.Log($"SceneTransitionLoader: onEnd invoked for {sceneName}");
                completed = true;
                try { if (tm != null) tm.onTransitionEnd -= onEnd; } catch (System.Exception ex) { Debug.LogException(ex); }
            };

            // Suscribimos una vez con las implementaciones finales
            try
            {
                if (tm != null)
                {
                    tm.onTransitionCutPointReached += onCut;
                    tm.onTransitionEnd += onEnd;
                }
            }
            catch (System.Exception ex) { Debug.LogException(ex); }

            // Esperamos activamente a que la transición complete su flujo (cut o end)

            try
            {
                Debug.Log($"SceneTransitionLoader: Requesting Transition for {sceneName} (delay={delay})");
                // Usar la sobrecarga que carga la escena internamente para evitar duplicar la carga
                tm.Transition(sceneName, settings, delay);
            }
            catch (System.Exception ex)
            {
                // Si por alguna razón la llamada a Transition falla, hacemos fallback seguro
                Debug.LogWarning("SceneTransitionLoader: Transition failed; loading scene directly.");
                Debug.LogException(ex);
                // nos aseguramos de desuscribir handlers que pudimos haber añadido
                try
                {
                    if (tm != null)
                    {
                        if (onCut != null) tm.onTransitionCutPointReached -= onCut;
                        if (onEnd != null) tm.onTransitionEnd -= onEnd;
                    }
                }
                catch (System.Exception ex2) { Debug.LogException(ex2); }

                if (!sceneLoaded)
                {
                    Debug.Log($"SceneTransitionLoader: Loading scene (fallback after exception): {sceneName}");
                    SceneManager.LoadScene(sceneName);
                    sceneLoaded = true;
                }

                yield break;
            }

            // Esperamos a que la transición termine (cut o end). Esto evita que _isLoading se libere
            // inmediatamente y que se inicien transiciones dobles.
            float waitForTransitionTimeout = 10f; // segundos máximos de espera
            float waitAcc = 0f;
            while (!completed && waitAcc < waitForTransitionTimeout)
            {
                yield return null;
                waitAcc += Time.unscaledDeltaTime;
            }

            if (!completed)
            {
                Debug.LogWarning("SceneTransitionLoader: Transition did not complete within timeout; forcing scene load.");
                if (!sceneLoaded)
                {
                    SceneManager.LoadScene(sceneName);
                    sceneLoaded = true;
                }
            }

        }
        finally
        {
            // limpieza segura: desuscribimos por si queda alguno (es seguro hacer -= si no está suscrito)
            try
            {
                if (tm != null)
                {
                    if (onCut != null) tm.onTransitionCutPointReached -= onCut;
                    if (onEnd != null) tm.onTransitionEnd -= onEnd;
                }
            }
            catch { }
            _isLoading = false;
        }
    }

    static TransitionManager FindTM()
    {
    #if UNITY_2022_3_OR_NEWER
        return Object.FindFirstObjectByType<TransitionManager>(FindObjectsInactive.Include);
    #else
        return Object.FindObjectOfType<TransitionManager>(true);
    #endif
    }
}
