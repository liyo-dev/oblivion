using UnityEngine;
using UnityEngine.SceneManagement;
using EasyTransition;

[DisallowMultipleComponent]
public class TransitionPlayer : MonoBehaviour
{
    [Header("Efecto")]
    public TransitionSettings settings;   // Asigna p.ej. Fade.asset
    [Min(0f)] public float delay = 0f;

    [Header("Atajos (opcionales)")]
    public bool playOnStart = false;
    public KeyCode debugKey = KeyCode.None;  // p.ej. F9 para probar

    void Start()
    {
        if (playOnStart) Play();
    }

    void Update()
    {
        if (debugKey != KeyCode.None && Input.GetKeyDown(debugKey))
            Play();
    }

    /// <summary>Lanza la transición (sin cargar escena).</summary>
    public void Play()
    {
        var tm = TransitionManager.Instance();
        if (tm == null)
        {
            Debug.LogWarning("[TransitionPlayer] TransitionManager no encontrado. ¿Está Start cargada?");
            return;
        }
        tm.Transition(settings, delay);
    }

    /// <summary>Lanza la transición y carga una escena por nombre.</summary>
    public void PlayAndLoadScene(string sceneName)
    {
        var tm = TransitionManager.Instance();
        if (tm == null)
        {
            Debug.LogWarning("[TransitionPlayer] TransitionManager no encontrado. Cargo escena directa.");
            SceneManager.LoadScene(sceneName);
            return;
        }
        tm.Transition(sceneName, settings, delay);
    }

    /// <summary>Permite cambiar el efecto por código/Inspector y ejecutar.</summary>
    public void PlayWith(TransitionSettings newSettings)
    {
        settings = newSettings;
        Play();
    }
}