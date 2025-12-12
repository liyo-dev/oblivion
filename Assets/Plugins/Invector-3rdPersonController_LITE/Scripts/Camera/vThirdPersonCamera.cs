using Invector;
using UnityEngine;

/// <summary>
/// vThirdPersonCamera con opción de oclusión por "sombra": en vez de acercar la cámara
/// cuando hay un obstáculo entre cámara y jugador, convierte esos renderers a ShadowsOnly.
/// Requiere el componente auxiliar CameraOcclusionShadowsOnly en la misma cámara (se añade solo).
/// </summary>
public class vThirdPersonCamera : MonoBehaviour
{
    #region Inspector

    public Transform target;
    [Tooltip("Lerp speed entre estados de cámara")]
    public float smoothCameraRotation = 12f;
    [Tooltip("Capas que bloquean (para raycasts de oclusión/altura)")]
    public LayerMask cullingLayer = 1 << 0;
    [Tooltip("Fijar cámara detrás del personaje (debug/alineación)")]
    public bool lockCamera;

    [Header("Seguimiento básico")]
    public float rightOffset = 0f;
    public float defaultDistance = 2.5f;
    public float height = 1.4f;
    public float smoothFollow = 10f;
    public float xMouseSensitivity = 3f;
    public float yMouseSensitivity = 3f;
    public float yMinLimit = -40f;
    public float yMaxLimit = 80f;

    // Solo dejamos la opción de oclusión por sombras (ShadowsOnly)

    [Header("Oclusión → Sombras (alternativa)")]
    [Tooltip("No acercar cámara; poner obstáculos en ShadowsOnly para ver al player")]
    public bool useShadowsOnlyOcclusion = false;
    [Tooltip("Capas que se volverán 'sombras' cuando tapen al player (excluye al Player)")]
    public LayerMask obstructionMask = ~0;
    [Tooltip("Radio del SphereCast entre cámara y objetivo (línea de visión)")]
    public float obstructionCheckRadius = 0.15f;

    #endregion

    #region Estado oculto

    [HideInInspector] public int indexList, indexLookPoint;
    [HideInInspector] public float offSetPlayerPivot;
    [HideInInspector] public string currentStateName;
    [HideInInspector] public Transform currentTarget;
    [HideInInspector] public Vector2 movementSpeed;

    private Transform targetLookAt;
    private Vector3 currentTargetPos;
    private Vector3 current_cPos;
    private Camera _camera;

    private float distance = 5f;
    private float mouseY = 0f;
    private float mouseX = 0f;
    private float currentHeight;
    private float cullingDistance;
    private float checkHeightRadius = 0.4f;
    private float clipPlaneMargin = 0f;
    private float forward = -1f;
    private float xMinLimit = -360f;
    private float xMaxLimit = 360f;
    private float cullingHeight = 0.2f;
    private float cullingMinDist = 0.1f;

    private CameraOcclusionShadowsOnly _occlusionFader;

    #endregion

    void Start() => Init();

    public void Init()
    {
        if (target == null) return;

        _camera = GetComponent<Camera>();
        currentTarget = target;
        currentTargetPos = new Vector3(
            currentTarget.position.x,
            currentTarget.position.y + offSetPlayerPivot,
            currentTarget.position.z);

        if (targetLookAt == null)
        {
            targetLookAt = new GameObject("targetLookAt").transform;
            targetLookAt.hideFlags = HideFlags.HideInHierarchy;
        }
        targetLookAt.position = currentTarget.position;
        targetLookAt.rotation = currentTarget.rotation;

        mouseY = currentTarget.eulerAngles.x;
        mouseX = currentTarget.eulerAngles.y;

        distance = defaultDistance;
        currentHeight = height;

        // Solo oclusión por sombras

        _occlusionFader = GetComponent<CameraOcclusionShadowsOnly>();
        if (_occlusionFader == null) _occlusionFader = gameObject.AddComponent<CameraOcclusionShadowsOnly>();
        _occlusionFader.SetMask(obstructionMask);
        _occlusionFader.SetRadius(obstructionCheckRadius);
    }

    void FixedUpdate()
    {
        if (target == null || targetLookAt == null) return;
        CameraMovement();
    }

    /// <summary>Setea target secundario (seguimiento temporal)</summary>
    public void SetTarget(Transform newTarget)
    {
        currentTarget = newTarget ? newTarget : target;
    }

    /// <summary>Setea target principal y re-inicializa</summary>
    public void SetMainTarget(Transform newTarget)
    {
        target = newTarget;
        currentTarget = newTarget;
        mouseY = currentTarget.rotation.eulerAngles.x;
        mouseX = currentTarget.rotation.eulerAngles.y;
        Init();
    }

    public Ray ScreenPointToRay(Vector3 point)
    {
        return GetComponent<Camera>().ScreenPointToRay(point);
    }

    public void RotateCamera(float x, float y)
    {
        mouseX += x * xMouseSensitivity;
        mouseY -= y * yMouseSensitivity;

        movementSpeed.x = x;
        movementSpeed.y = -y;

        if (!lockCamera)
        {
            mouseY = vExtensions.ClampAngle(mouseY, yMinLimit, yMaxLimit);
            mouseX = vExtensions.ClampAngle(mouseX, xMinLimit, xMaxLimit);
        }
        else
        {
            mouseY = currentTarget.root.localEulerAngles.x;
            mouseX = currentTarget.root.localEulerAngles.y;
        }
    }

    void CameraMovement()
    {
        if (currentTarget == null) return;

        // direcciones base
        var camDir = (forward * targetLookAt.forward) + (rightOffset * targetLookAt.right);
        camDir = camDir.normalized;

        // posiciones objetivo
        var targetPos = new Vector3(currentTarget.position.x, currentTarget.position.y + offSetPlayerPivot, currentTarget.position.z);
        currentTargetPos = targetPos;

        var desired_cPos = targetPos + new Vector3(0, height, 0);
        current_cPos = currentTargetPos + new Vector3(0, currentHeight, 0);

        RaycastHit hitInfo;
        var planePoints = _camera.NearClipPlanePoints(current_cPos + (camDir * (distance)), clipPlaneMargin);
        var oldPoints   = _camera.NearClipPlanePoints(desired_cPos + (camDir * distance), clipPlaneMargin);

        // Altura: si hay techo cerca, ajustar hacia cullingHeight
        if (Physics.SphereCast(targetPos, checkHeightRadius, Vector3.up, out hitInfo, cullingHeight + 0.2f, cullingLayer))
        {
            var t = hitInfo.distance - 0.2f;
            t -= height;
            t /= (cullingHeight - height);
            cullingHeight = Mathf.Lerp(height, cullingHeight, Mathf.Clamp(t, 0.0f, 1.0f));
        }

        // Solo oclusión por sombras: no tocamos la distancia, nos quedamos en defaultDistance con un leve lerp para estabilidad
        distance = Mathf.Lerp(distance, defaultDistance, smoothFollow * Time.deltaTime);
        currentHeight = height;
        current_cPos = currentTargetPos + new Vector3(0, currentHeight, 0);

        // Apuntar lookAt y rotación
        var lookPoint = current_cPos + targetLookAt.forward * 2f;
        lookPoint += (targetLookAt.right * Vector3.Dot(camDir * (distance), targetLookAt.right));
        targetLookAt.position = current_cPos;

        Quaternion newRot = Quaternion.Euler(mouseY, mouseX, 0);
        targetLookAt.rotation = Quaternion.Slerp(targetLookAt.rotation, newRot, smoothCameraRotation * Time.deltaTime);

        // Colocar cámara
        Vector3 camPos = current_cPos + (camDir * (distance));
        transform.position = camPos;
        transform.rotation = Quaternion.LookRotation((lookPoint) - transform.position);

        // Línea de visión → convertir obstáculos a ShadowsOnly
        if (_occlusionFader != null)
        {
            Vector3 from = transform.position;
            Vector3 to   = currentTargetPos + new Vector3(0, currentHeight, 0);
            _occlusionFader.Process(from, to);
        }

        movementSpeed = Vector2.zero;
    }

    /// <summary>Raycasts contra los 4 puntos del near clip.</summary>
    bool CullingRayCast(Vector3 from, ClipPlanePoints to, out RaycastHit hitInfo, float dist, LayerMask layer, Color debug)
    {
        bool hitAny = false;
        hitInfo = default;

        if (Physics.Raycast(from, to.LowerLeft - from, out var h1, dist, layer)) { hitAny = true; hitInfo = h1; cullingDistance = h1.distance; }
        if (Physics.Raycast(from, to.LowerRight - from, out var h2, dist, layer))
        {
            if (!hitAny || h2.distance < cullingDistance) { hitInfo = h2; cullingDistance = h2.distance; }
            hitAny = true;
        }
        if (Physics.Raycast(from, to.UpperLeft - from, out var h3, dist, layer))
        {
            if (!hitAny || h3.distance < cullingDistance) { hitInfo = h3; cullingDistance = h3.distance; }
            hitAny = true;
        }
        if (Physics.Raycast(from, to.UpperRight - from, out var h4, dist, layer))
        {
            if (!hitAny || h4.distance < cullingDistance) { hitInfo = h4; cullingDistance = h4.distance; }
            hitAny = true;
        }

        return hitAny && hitInfo.collider;
    }
}
    