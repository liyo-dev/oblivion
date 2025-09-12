using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTargeting : MonoBehaviour, ITargetProvider
{
    // ================== SCAN / TARGETING ==================
    [Header("Búsqueda")]
    [SerializeField] private float radius = 8f;
    [SerializeField] private LayerMask enemyMask;          // solo Layer Enemy
    [SerializeField] private float fovDegrees = 140f;      // campo de visión
    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private Transform aimOrigin;          // si null usa el player; puedes arrastrar la cámara
    [SerializeField] private float updatesPerSecond = 10f; // 0 = cada frame

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawRadius = true;
    [SerializeField] private bool drawFOV = true;
    [SerializeField] private bool drawTargetLine = true;
    [SerializeField] private Color radiusColor = new Color(0f, 0.7f, 1f, 0.35f);
    [SerializeField] private Color fovColor = new Color(0.2f, 1f, 0.4f, 0.25f);
    [SerializeField] private Color targetLineColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    // ================== FEEDBACK / MARKER ==================
    [Header("Feedback de Target (Opcional)")]
    [SerializeField] private bool enableMarker = true;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Vector3 markerOffset = new Vector3(0, 1.8f, 0);
    [SerializeField] private bool billboardToCamera = true;
    [Tooltip("Si true, el marcador se parenta al target (útil si se mueve mucho).")]
    [SerializeField] private bool parentMarkerToTarget = false;

    public Transform CurrentTarget { get; private set; }

    float _nextScan;
    Transform _marker;
    Collider _lastTargetCol;
    Camera _cam;

    // ================== UNITY ==================
    void Awake()
    {
        // Cámara para billboard (no es obligatorio)
        _cam = Camera.main;

        // Instancia del marker si se desea feedback
        if (enableMarker && markerPrefab)
        {
            var go = Instantiate(markerPrefab);
            go.SetActive(false);
            _marker = go.transform;
        }
    }

    void OnDestroy()
    {
        if (_marker) Destroy(_marker.gameObject);
    }

    void Update()
    {
        if (updatesPerSecond <= 0f || Time.time >= _nextScan)
        {
            Transform before = CurrentTarget;
            Scan();
            if (updatesPerSecond > 0f)
                _nextScan = Time.time + 1f / updatesPerSecond;

            if (before != CurrentTarget)
                OnTargetChanged(before, CurrentTarget);
        }
    }

    void LateUpdate()
    {
        UpdateMarker();
    }

    // ================== SCAN LOGIC ==================
    void Scan()
    {
        Vector3 origin = aimOrigin ? aimOrigin.position : transform.position + Vector3.up * 1f;
        Vector3 fwd    = aimOrigin ? aimOrigin.forward  : transform.forward;

        var hits = Physics.OverlapSphere(origin, radius, enemyMask, QueryTriggerInteraction.Collide);
        float bestScore = float.NegativeInfinity;
        Transform best = null;

        foreach (var h in hits)
        {
            if (!h) continue;

            Vector3 center = GetTargetCenter(h.transform);
            Vector3 to     = center - origin;
            float dist     = to.magnitude;
            if (dist < 0.01f) continue;

            Vector3 dir = to / dist;
            float ang   = Vector3.Angle(fwd, dir);
            if (ang > fovDegrees * 0.5f) continue;

            if (requireLineOfSight)
            {
                if (Physics.Raycast(origin, dir, out var rh, dist, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (rh.collider.transform.root != h.transform.root) continue;
                }
            }

            // score: favorece ángulo (dot) y distancia corta
            float score = Vector3.Dot(fwd, dir) * 1.0f - (dist / Mathf.Max(0.0001f, radius)) * 0.35f;
            if (score > bestScore) { bestScore = score; best = h.transform; }
        }

        CurrentTarget = best;
    }

    // ================== FEEDBACK LOGIC ==================
    void OnTargetChanged(Transform oldT, Transform newT)
    {
        if (!_marker) return;

        if (parentMarkerToTarget)
        {
            _marker.SetParent(newT, worldPositionStays: true);
        }

        if (!newT)
        {
            if (_marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
            _lastTargetCol = null;
        }
        else
        {
            if (!_marker.gameObject.activeSelf) _marker.gameObject.SetActive(true);
        }
    }

    void UpdateMarker()
    {
        if (!_marker || !enableMarker) return;

        var t = CurrentTarget;
        if (!t)
        {
            if (_marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
            return;
        }

        if (!_marker.gameObject.activeSelf) _marker.gameObject.SetActive(true);

        // Calcula posición sobre el bounds del target
        if (_lastTargetCol == null || _lastTargetCol.transform != t)
            _lastTargetCol = t.GetComponentInParent<Collider>();

        Vector3 pos = t.position + markerOffset;
        if (_lastTargetCol)
            pos = _lastTargetCol.bounds.center + new Vector3(0, _lastTargetCol.bounds.extents.y, 0) + markerOffset * 0.2f;

        if (!parentMarkerToTarget) _marker.position = pos;
        else _marker.localPosition = t.InverseTransformPoint(pos);

        if (billboardToCamera && (_cam || (_cam = Camera.main)))
        {
            _marker.forward = (_marker.position - _cam.transform.position).normalized;
        }
    }

    // ================== ITargetProvider ==================
    public bool TryGetTarget(out Transform t)
    {
        t = CurrentTarget;
        return t != null;
    }

    public Vector3 GetAimDirectionFrom(Transform origin, Vector3 fallbackForward)
    {
        if (CurrentTarget)
        {
            Vector3 center = GetTargetCenter(CurrentTarget);
            Vector3 dir = (center - origin.position);
            dir.y = 0f; // opcional: evita disparos altos/bajos
            if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        }
        return fallbackForward.normalized;
    }

    static Vector3 GetTargetCenter(Transform target)
    {
        var col = target.GetComponentInParent<Collider>();
        return col ? col.bounds.center : target.position + Vector3.up * 1.0f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = aimOrigin ? aimOrigin.position : transform.position + Vector3.up * 1f;
        Vector3 fwd    = aimOrigin ? aimOrigin.forward  : transform.forward;

        if (drawRadius)
        {
            Gizmos.color = radiusColor;
            Gizmos.DrawWireSphere(origin, radius);
        }

        if (drawFOV)
        {
            Gizmos.color = fovColor;
            float half = fovDegrees * 0.5f;
            Quaternion leftRot  = Quaternion.AngleAxis(-half, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(+half, Vector3.up);
            Vector3 leftDir  = leftRot * fwd;
            Vector3 rightDir = rightRot * fwd;
            Gizmos.DrawRay(origin, leftDir.normalized * radius);
            Gizmos.DrawRay(origin, rightDir.normalized * radius);
        }

        if (drawTargetLine && CurrentTarget)
        {
            Gizmos.color = targetLineColor;
            Gizmos.DrawLine(origin, GetTargetCenter(CurrentTarget));
        }
    }
#endif
}
