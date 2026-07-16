using UnityEngine;
using UnityEngine.InputSystem;

/// Herramienta de desarrollo para previsualizar planos cinemáticos durante el Play.
/// Asigna el driver y los shot points en el Inspector.
/// Teclas en Play: [ / ] para ciclar planos, \ para activar/desactivar.
[AddComponentMenu("Debug/Cinematic Shot Preview")]
[DisallowMultipleComponent]
public class CinematicShotPreview : MonoBehaviour
{
    [SerializeField] private CinematicCameraDriver driver;
    [SerializeField] private Transform[]           shots;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int  _current = -1;
    private bool _active;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit3Key.wasPressedThisFrame)
        {
            if (_active) StopPreview();
            else         StartPreview();
            return;
        }

        if (!_active || shots == null || shots.Length == 0) return;

        if (kb.digit2Key.wasPressedThisFrame) ShowShot((_current + 1) % shots.Length);
        if (kb.digit1Key.wasPressedThisFrame) ShowShot((_current - 1 + shots.Length) % shots.Length);
    }

    void StartPreview()
    {
        if (driver == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[ShotPreview] Asigna un CinematicCameraDriver en el Inspector.");
#endif
            return;
        }
        driver.Activate();
        _active = true;
        ShowShot(0);
    }

    void ShowShot(int index)
    {
        if (shots == null || index < 0 || index >= shots.Length || shots[index] == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[ShotPreview] Shot {index} es null o fuera de rango.");
#endif
            return;
        }
        _current = index;
        driver.Cut(shots[index]);
        Debug.Log($"[ShotPreview] [{_current}/{shots.Length - 1}] {shots[index].name}");
#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = shots[index].gameObject;
        UnityEditor.EditorApplication.isPaused = true;
#endif
    }

    void StopPreview()
    {
        driver?.Deactivate();
        _active  = false;
        _current = -1;
        Debug.Log("[ShotPreview] Desactivado — control devuelto al juego.");
    }

    void OnDisable() { if (_active) StopPreview(); }
#endif
}
