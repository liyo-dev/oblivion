using UnityEngine;
using UnityEngine.SceneManagement;
using EasyTransition;
using System.Collections;

[DisallowMultipleComponent]
public class SceneTransitionLoader : MonoBehaviour
{
    // Singleton simple + persistente
    private static SceneTransitionLoader _inst;
    public static SceneTransitionLoader Instance => _inst;

    [Header("Valores por defecto")]
    public TransitionSettings defaultSettings;     // Asigna aquí tu Fade.asset (una sola vez)
    [Min(0)] public float defaultDelay = 0f;
    [Tooltip("Tiempo máximo a esperar a que exista TransitionManager (por si Start entra un frame después)")]
    [Min(0)] public float waitForManagerSec = 0.5f;

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        DontDestroyOnLoad(gameObject);
    }

    // -------- API estática (lo más cómodo desde cualquier script) --------

    /// Usa los valores por defecto del servicio (defaultSettings/delay).
    public static void Load(string sceneName)
    {
        if (_inst) _inst.StartCoroutine(_inst.LoadRoutine(sceneName, _inst.defaultSettings, _inst.defaultDelay));
        else SceneManager.LoadScene(sceneName); // fallback sin fade
    }

    /// Permite pasar settings/delay ad-hoc (por ejemplo desde el Menú).
    public static void Load(string sceneName, TransitionSettings settings, float delay = 0f)
    {
        if (_inst) _inst.StartCoroutine(_inst.LoadRoutine(sceneName, settings, delay));
        else SceneManager.LoadScene(sceneName); // fallback sin fade
    }

    // -------- Interna segura --------

    IEnumerator LoadRoutine(string sceneName, TransitionSettings settings, float delay)
    {
        // Si no hay plugin o settings, no llamamos al manager para evitar su error y cargamos directo.
        if (settings == null)
        {
            SceneManager.LoadScene(sceneName);
            yield break;
        }

        // Espera a que exista TransitionManager (por Start con auto-bootstrap)
        TransitionManager tm = null;
        float t = 0f;
        while (t < waitForManagerSec && (tm = FindTM()) == null)
        {
            yield return null;
            t += Time.unscaledDeltaTime;
        }
        if (tm == null) tm = FindTM();

        if (tm != null)
            tm.Transition(sceneName, settings, delay);
        else
            SceneManager.LoadScene(sceneName); // fallback
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
