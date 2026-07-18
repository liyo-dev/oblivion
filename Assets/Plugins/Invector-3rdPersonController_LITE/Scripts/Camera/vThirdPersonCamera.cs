using Invector;
using UnityEngine;

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

    [Header("Oclusión → Sombras (alternativa)")]
    public bool useShadowsOnlyOcclusion = false;
    public LayerMask obstructionMask = ~0;
    public float obstructionCheckRadius = 0.15f;
    
    [Header("Anulación de Lock-On")]
    public float overrideTargetHeightOffset = 1.5f;

    #endregion

    #region Estado oculto
    
    [HideInInspector] public Transform currentTarget;
    
    // Variable privada para el target de bloqueo
    [SerializeField]
    private Transform _lockTarget;
    public Transform LockTarget => _lockTarget;
    
    private Transform targetLookAt;
    private Camera _camera;
    private float distance;
    private float mouseY;
    private float mouseX;
    private float currentHeight;
    private float forward = -1f;
    private CameraOcclusionShadowsOnly _occlusionFader;
    public static bool lockCameraForCinematic = false;

    // Override de zona ambiental
    [HideInInspector] public bool zoneRotationLocked;
    private float _zoneLockedMouseX;
    private float _zoneLockedMouseY;

    // Suavizado de posición al re-activar tras cinemáticas/sueño
    private bool _doSmoothSnap;
    private Vector3 _snapVelocity;
    private bool _wasCinematicLocked;
    private const float SnapSmoothTime = 0.15f;

    #endregion

    void Start() => Init();

    void OnEnable()
    {
        // Si ya estaba inicializado (re-activación tras dormir u otra desactivación),
        // activar suavizado para no dar el golpe seco de posición.
        if (targetLookAt != null)
        {
            _doSmoothSnap = true;
            _snapVelocity = Vector3.zero;
        }
    }

    public void Init()
    {
        if (target == null) return;

        _camera = GetComponent<Camera>();
        currentTarget = target;

        if (targetLookAt == null)
        {
            targetLookAt = new GameObject("targetLookAt").transform;
            targetLookAt.hideFlags = HideFlags.HideInHierarchy;
        }

        mouseY = currentTarget.eulerAngles.x;
        mouseX = currentTarget.eulerAngles.y;
        distance = defaultDistance;
        currentHeight = height;

        _occlusionFader = GetComponent<CameraOcclusionShadowsOnly>();
        if (_occlusionFader == null) _occlusionFader = gameObject.AddComponent<CameraOcclusionShadowsOnly>();
        _occlusionFader.SetMask(obstructionMask);
        _occlusionFader.SetRadius(obstructionCheckRadius);

        // Cambio de target implica teleport de cámara; no suavizar.
        _doSmoothSnap = false;
    }

    void FixedUpdate()
    {
        if (target == null) return;

        if (lockCameraForCinematic)
        {
            _wasCinematicLocked = true;
            return;
        }

        // Detectar salida de bloqueo cinematic para aplicar suavizado
        if (_wasCinematicLocked)
        {
            _wasCinematicLocked = false;
            if (targetLookAt != null)
            {
                _doSmoothSnap = true;
                _snapVelocity = Vector3.zero;
            }
        }

        CameraMovement();
    }

    // ===================================================================================
    // API PÚBLICA PARA LOCK-ON
    // ===================================================================================

    public void SetLockTarget(Transform target)
    {
        // Debug.Log($"[vThirdPersonCamera] SetLockTarget LLAMADO en '{this.gameObject.name}'. Nuevo target: {(target != null ? target.name : "NULL")}");
        _lockTarget = target;
    }

    public void ClearLockTarget()
    {
        // if (_lockTarget != null) Debug.Log($"[vThirdPersonCamera] ClearLockTarget LLAMADO en '{this.gameObject.name}'. Liberando target: {_lockTarget.name}");
        _lockTarget = null;
    }

    /// <summary>Bloquea la cámara en el ángulo indicado; la zona ambiental lo llama al entrar.</summary>
    public void SetZoneRotation(float lockedMouseX, float lockedMouseY)
    {
        _zoneLockedMouseX = lockedMouseX;
        _zoneLockedMouseY = lockedMouseY;
        zoneRotationLocked = true;
    }

    public void ClearZoneRotation()
    {
        zoneRotationLocked = false;
    }

    public void SetMainTarget(Transform newTarget)
    {
        target = newTarget;
        currentTarget = newTarget;
        mouseY = currentTarget.rotation.eulerAngles.x;
        mouseX = currentTarget.rotation.eulerAngles.y;
        Init();
    }

    /// <summary>Fuerza los ángulos de órbita de la cámara (mouseX = horizontal, mouseY = vertical).
    /// Usar antes de re-habilitar la cámara tras una cinemática para evitar que aparezca detrás de una pared.</summary>
    public void SetAngles(float mouseXVal, float mouseYVal)
    {
        mouseX = mouseXVal;
        mouseY = vExtensions.ClampAngle(mouseYVal, yMinLimit, yMaxLimit);
        if (targetLookAt != null)
            targetLookAt.rotation = Quaternion.Euler(mouseY, mouseX, 0);
    }

    public void RotateCamera(float x, float y)
    {
        if (lockCameraForCinematic || _lockTarget != null || zoneRotationLocked) return;

        mouseX += x * xMouseSensitivity;
        mouseY -= y * yMouseSensitivity;
        mouseY = vExtensions.ClampAngle(mouseY, yMinLimit, yMaxLimit);
    }

    void CameraMovement()
    {
        if (currentTarget == null) return;

        distance = Mathf.Lerp(distance, defaultDistance, smoothFollow * Time.deltaTime);
        currentHeight = height;
        
        Vector3 targetPos = new Vector3(currentTarget.position.x, currentTarget.position.y, currentTarget.position.z);
        Vector3 current_cPos = targetPos + new Vector3(0, currentHeight, 0);
        
        Quaternion newRot;

        if (_lockTarget != null)
        {
            // --- MODO LOCK-ON ---
            Vector3 lookAtPoint = _lockTarget.position + new Vector3(0, overrideTargetHeightOffset, 0);
            Vector3 dirToTarget = lookAtPoint - current_cPos;

            if (dirToTarget.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
                
                // Asignar directamente los ángulos objetivo.
                // La suavidad vendrá del Slerp final de targetLookAt.rotation.
                mouseX = targetRot.eulerAngles.y;
                mouseY = targetRot.eulerAngles.x;
            }
            
            // Asegurar que mouseY respete los límites incluso en lock-on
            // (opcional, pero evita giros extraños si el enemigo está muy arriba/abajo)
            // mouseY = vExtensions.ClampAngle(mouseY, yMinLimit, yMaxLimit); 
            
            newRot = Quaternion.Euler(mouseY, mouseX, 0);
        }
        else
        {
            // --- MODO LIBRE ---
            if (zoneRotationLocked)
            {
                mouseX = Mathf.LerpAngle(mouseX, _zoneLockedMouseX, smoothCameraRotation * Time.fixedDeltaTime);
                mouseY = Mathf.Lerp(mouseY, _zoneLockedMouseY, smoothCameraRotation * Time.fixedDeltaTime);
            }
            var camDir = (forward * targetLookAt.forward) + (rightOffset * targetLookAt.right);
            camDir = camDir.normalized;
            newRot = Quaternion.Euler(mouseY, mouseX, 0);
        }

        // Aplicar rotación al pivote
        targetLookAt.position = current_cPos;
        targetLookAt.rotation = Quaternion.Slerp(targetLookAt.rotation, newRot, smoothCameraRotation * Time.fixedDeltaTime);
        
        // Calcular posición final de la cámara
        var finalCamDir = (forward * targetLookAt.forward) + (rightOffset * targetLookAt.right);
        finalCamDir = finalCamDir.normalized;
        
        Vector3 camPos = current_cPos + (finalCamDir * distance);

        if (_doSmoothSnap)
        {
            var p = Vector3.SmoothDamp(transform.position, camPos, ref _snapVelocity, SnapSmoothTime, float.MaxValue, Time.fixedDeltaTime);
            if ((p - camPos).sqrMagnitude < 0.0001f) { _doSmoothSnap = false; p = camPos; }
            transform.position = p;
        }
        else
        {
            transform.position = camPos;
        }
        
        // Mirar siempre al pivote
        var lookPoint = current_cPos + targetLookAt.forward * 2f;
        lookPoint += (targetLookAt.right * Vector3.Dot(finalCamDir * (distance), targetLookAt.right));
        transform.rotation = Quaternion.LookRotation((lookPoint) - transform.position);

        // Oclusión
        if (_occlusionFader != null)
        {
            _occlusionFader.Process(transform.position, targetPos + new Vector3(0, currentHeight, 0));
        }
    }
}
