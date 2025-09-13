using UnityEngine;

[DisallowMultipleComponent]
public class FlightCameraFollower : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Tooltip("Altura del punto de enfoque respecto al suelo (si hay CapsuleCollider se auto-ajusta).")]
    public float focusHeight = 1.6f;

    [Header("Rig")]
    public float distance   = 6f;     // distancia ideal
    public float minDistance= 3.2f;   // ¡NUNCA más cerca que esto!
    public float maxDistance= 10f;
    public float height     = 2.2f;   // alza extra de la cámara respecto al focus
    public float pitch      = -10f;   // inclinación fija (negativo = mira hacia abajo)

    [Header("Smoothing")]
    public float posLerp = 14f;       // mayor = más pegado
    public float yawLerp = 16f;

    [Header("Collision")]
    public float collisionRadius = 0.3f;            // radio de la esfera para el cast
    public LayerMask collisionMask = ~0;            // por defecto todo; el script ignora el Player
    public float collisionShell = 0.08f;            // margen para no “besar” paredes

    // cache
    Transform _t;
    CapsuleCollider _capsule;
    int _playerLayer;
    Vector3 _vel; // no usado (por si quieres cambiar a SmoothDamp)
    
    void Awake()
    {
        _t = transform;
    }

    void OnEnable()
    {
        // Ajustar focusHeight si hay CapsuleCollider
        if (target != null)
        {
            _capsule = target.GetComponent<CapsuleCollider>();
            _playerLayer = target.gameObject.layer;

            if (_capsule != null)
            {
                // punto medio entre pecho y cabeza aprox
                focusHeight = Mathf.Clamp(_capsule.height * 0.6f, 1.2f, 2.2f);
                // minDistance acorde al tamaño del player
                minDistance = Mathf.Max(minDistance, _capsule.radius * 3f);
            }
        }
    }

    void LateUpdate()
    {
        if (!target) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 1) Punto de enfoque (pecho/cabeza)
        Vector3 focus = target.position + Vector3.up * focusHeight;

        // 2) Yaw del player = cámara detrás SIEMPRE
        float targetYaw = target.eulerAngles.y;
        Quaternion baseRot = Quaternion.Euler(0f, targetYaw, 0f);
        Quaternion desiredRot = Quaternion.Euler(pitch, targetYaw, 0f);

        // 3) Posición deseada (detrás + altura)
        Vector3 backDir = (baseRot * Vector3.back).normalized;     // solo yaw
        Vector3 desiredPosNoPitch = focus + Vector3.up * height + backDir * distance;

        // 4) Colisión: empuja la cámara hacia fuera si hay paredes/objetos por medio
        //    (y evita meterse dentro del propio Player)
        Vector3 from = focus;
        Vector3 to   = desiredPosNoPitch;
        Vector3 dir  = (to - from);
        float   len  = Mathf.Max(0.0001f, dir.magnitude);
        dir /= len;

        float safeDist = Mathf.Clamp(distance, minDistance, maxDistance);
        Vector3 safeTarget = focus + Vector3.up * height + backDir * safeDist;

        // SphereCast para evitar clipping con el entorno
        if (Physics.SphereCast(from, collisionRadius, dir, out RaycastHit hit, len, collisionMask, QueryTriggerInteraction.Ignore))
        {
            // Ignora impactos con el propio player
            if (hit.collider != null && hit.collider.transform.IsChildOf(target))
            {
                // forzar una distancia mínima detrás
                to = safeTarget;
            }
            else
            {
                // colisión con el mundo: coloca la cámara justo antes del obstáculo
                to = hit.point - dir * (collisionRadius + collisionShell);
                // asegura distancia mínima al player
                float distToFocus = Vector3.Distance(focus + Vector3.up * height, to);
                if (distToFocus < minDistance)
                    to = focus + Vector3.up * height + backDir * minDistance;
            }
        }
        else
        {
            // sin colisión: usa el objetivo seguro (clamp min/max)
            to = safeTarget;
        }

        // 5) Interpolaciones suaves (exponenciales)
        float kPos = 1f - Mathf.Exp(-posLerp * dt);
        float kYaw = 1f - Mathf.Exp(-yawLerp * dt);

        _t.position = Vector3.Lerp(_t.position, to, kPos);

        // mezcla el yaw hacia el del player y aplica pitch fijo
        Vector3 curEuler = _t.rotation.eulerAngles;
        float newYaw = Mathf.LerpAngle(curEuler.y, targetYaw, kYaw);
        _t.rotation = Quaternion.Euler(pitch, newYaw, 0f);

        // 6) Asegura tercera persona: si por cualquier razón hemos quedado demasiado cerca, empuja
        Vector3 camAnchor = focus + Vector3.up * height;
        Vector3 camDir = (_t.position - camAnchor);
        float camLen = camDir.magnitude;
        if (camLen < minDistance)
        {
            camDir = (camLen < 1e-4f) ? (baseRot * Vector3.back) : (camDir / camLen);
            _t.position = camAnchor + camDir * minDistance;
        }
    }
}
