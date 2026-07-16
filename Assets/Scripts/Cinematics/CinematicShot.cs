using UnityEngine;

/// Punto de plano cinemático. Añade este componente a un GO vacío.
/// La Camera adjunta se usa solo como referencia visual en el editor (nunca renderiza).
/// En la Scene View selecciona el GO y verás el frustum y el panel de preview de Unity.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Cinematics/Cinematic Shot")]
[DisallowMultipleComponent]
public class CinematicShot : MonoBehaviour
{
    [Tooltip("Nombre del plano, aparece en el inspector de CinematicShotPreview y en los gizmos.")]
    public string label;

    private Camera _cam;

    void OnEnable()
    {
        _cam = GetComponent<Camera>();
        _cam.enabled = false;
    }

    void OnValidate()
    {
        if (TryGetComponent(out Camera c))
            c.enabled = false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        string text = string.IsNullOrEmpty(label) ? gameObject.name : label;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, text);
    }
#endif
}
