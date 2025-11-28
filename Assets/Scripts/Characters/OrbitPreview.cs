using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitPreview : MonoBehaviour
{
    public Transform target;          // raíz del personaje
    public float distance = 4f;
    public float minDistance = 2f;
    public float maxDistance = 8f;
    public float yaw = 30f;
    public float pitch = 15f;
    public float rotateSpeed = 120f;
    public float zoomSpeed = 4f;
    public float autoSpinSpeed = 15f; // grados/seg si no hay input
    public Vector3 focusOffset = new Vector3(0, 1.1f, 0);

    Camera cam;
    float idleTimer;

    void Awake() => cam = GetComponent<Camera>();

    void Start()
    {
        if (target != null) FrameTarget();
    }

    void Update()
    {
        bool anyInput = false;

        var look = GamepadInputReader.CameraLook;
        if (look.sqrMagnitude > 0.0001f)
        {
            yaw   += look.x * rotateSpeed * Time.deltaTime;
            pitch -= look.y * rotateSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, -10f, 60f);
            anyInput = true;
        }

        // Auto-spin si no hay input
        if (!anyInput) yaw += autoSpinSpeed * Time.deltaTime;
        else idleTimer = 0f;

        // Colocación de cámara
        var center = GetFocusPoint();
        var rot = Quaternion.Euler(pitch, yaw, 0);
        var pos = center - rot * Vector3.forward * distance;

        transform.SetPositionAndRotation(pos, rot);
    }

    public void FrameTarget()
    {
        if (target == null) return;

        var b = ComputeBounds(target);
        // distancia para encajar bounding según FOV
        float radius = b.extents.magnitude;
        float fov = cam.fieldOfView * Mathf.Deg2Rad;
        distance = Mathf.Clamp(radius / Mathf.Sin(fov * 0.5f), minDistance, maxDistance);
        // centra en mitad superior del personaje
        focusOffset = new Vector3(0, b.center.y - target.position.y + b.extents.y * 0.2f, 0);
    }

    Bounds ComputeBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds b = new Bounds(root.position, Vector3.zero);
        bool init = false;
        foreach (var r in renderers)
        {
            if (!init) { b = r.bounds; init = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!init) b = new Bounds(root.position, Vector3.one);
        return b;
    }

    Vector3 GetFocusPoint()
    {
        return (target != null ? target.position : Vector3.zero) + focusOffset;
    }
}
