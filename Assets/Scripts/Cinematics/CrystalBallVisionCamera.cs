using UnityEngine;

/// Cámara secundaria que sigue al jugador y renderiza a un RenderTexture.
/// Asigna ese RT como Target Texture en el componente Camera (Inspector).
/// El material de la bola de cristal debe tener Emission ON y _EmissionMap = mismo RT.
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class CrystalBallVisionCamera : MonoBehaviour
{
    [Tooltip("Offset en espacio local del jugador (x=lateral, y=altura, z=distancia trasera negativa).")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.8f, -2.8f);
    [Tooltip("Altura sobre la posición del jugador hacia la que mira la cámara.")]
    [SerializeField] private float lookAtHeight = 1.1f;
    [Tooltip("Velocidad de seguimiento (lerp). Bajo = más suave y cinematográfico.")]
    [SerializeField] private float followSpeed = 3f;

    private Camera _cam;
    private Transform _target;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.enabled = false;
    }

    /// Activa la cámara posicionándola instantáneamente detrás del jugador.
    public void Activate(Transform playerTransform)
    {
        _target = playerTransform;
        if (_target != null)
        {
            transform.position = _target.position + _target.TransformDirection(followOffset);
            transform.LookAt(_target.position + Vector3.up * lookAtHeight);
        }
        _cam.enabled = true;
    }

    public void Deactivate()
    {
        _cam.enabled = false;
        _target = null;
    }

    void LateUpdate()
    {
        if (!_cam.enabled || _target == null) return;
        var desired = _target.position + _target.TransformDirection(followOffset);
        transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
        transform.LookAt(_target.position + Vector3.up * lookAtHeight);
    }
}
