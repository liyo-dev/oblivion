using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTargeting : MonoBehaviour, ITargetProvider
{
    // ================== SCAN / TARGETING ==================
    [Header("Búsqueda")]
    [SerializeField] private float radius = 8f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float fovDegrees = 140f;
    [SerializeField] private bool requireLineOfSight = true;     // <- ahora true por defecto
    [SerializeField] private Transform aimOrigin;                 // arrastra la cámara aquí
    [SerializeField] private float updatesPerSecond = 10f;

    [Header("Visibilidad en pantalla")]
    [SerializeField] private bool mustBeOnScreen = true;          // <- NUEVO
    [SerializeField, Range(0f, 0.2f)] private float screenEdgePadding = 0.03f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawRadius = true;
    [SerializeField] private bool drawFOV = true;
    [SerializeField] private bool drawTargetLine = true;
    [SerializeField] private Color radiusColor = new Color(0f, 0.7f, 1f, 0.35f);
    [SerializeField] private Color fovColor = new Color(0.2f, 1f, 0.4f, 0.25f);
    [SerializeField] private Color targetLineColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    public Transform CurrentTarget { get; private set; }

    float _nextScan;
    Transform _marker;
    Collider _lastTargetCol;
    Camera _cam;

    [Header("Feedback de Target (Opcional)")]
    [SerializeField] private bool enableMarker = true;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Vector3 markerOffset = new(0, 1.8f, 0);
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private bool parentMarkerToTarget = false;

    void Awake()
    {
        _cam = Camera.main;
        if (enableMarker && markerPrefab)
        {
            var go = Instantiate(markerPrefab);
            go.SetActive(false);
            _marker = go.transform;
        }
        if (!aimOrigin && _cam) aimOrigin = _cam.transform; // <- recomendable
    }

    void OnDestroy()
    {
        if (_marker) Destroy(_marker.gameObject);
    }

    void Update()
    {
        if (updatesPerSecond <= 0f || Time.time >= _nextScan)
        {
            var before = CurrentTarget;
            Scan();
            if (updatesPerSecond > 0f)
                _nextScan = Time.time + 1f / updatesPerSecond;

            if (before != CurrentTarget)
                OnTargetChanged(before, CurrentTarget);
        }
    }

    void LateUpdate() => UpdateMarker();

    void Scan()
    {
        var origin = aimOrigin ? aimOrigin.position : transform.position + Vector3.up;
        var fwd    = aimOrigin ? aimOrigin.forward  : transform.forward;

        var hits = Physics.OverlapSphere(origin, radius, enemyMask, QueryTriggerInteraction.Collide);
        float bestScore = float.NegativeInfinity;
        Transform best = null;

        foreach (var h in hits)
        {
            if (!h) continue;

            Vector3 center = GetTargetCenter(h.transform);
            Vector3 to = center - origin;
            float dist = to.magnitude;
            if (dist < 0.01f) continue;

            Vector3 dir = to / dist;

            // FOV respecto al aim (cámara si la arrastras a aimOrigin)
            float ang = Vector3.Angle(fwd, dir);
            if (ang > fovDegrees * 0.5f) continue;

            // En pantalla (si se exige)
            if (mustBeOnScreen && (_cam || (_cam = Camera.main)))
            {
                Vector3 vp = _cam.WorldToViewportPoint(center);
                if (vp.z <= 0f) continue; // detrás de la cámara
                float pad = screenEdgePadding;
                if (vp.x < pad || vp.x > 1f - pad || vp.y < pad || vp.y > 1f - pad) continue;
            }

            // Línea de visión
            if (requireLineOfSight)
            {
                if (Physics.Raycast(origin, dir, out var rh, dist, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (rh.collider.transform.root != h.transform.root) continue;
                }
            }

            // score: favorece estar centrado y más cerca
            float score = Vector3.Dot(fwd, dir) * 1.0f - (dist / Mathf.Max(0.0001f, radius)) * 0.35f;
            if (score > bestScore) { bestScore = score; best = h.transform; }
        }

        CurrentTarget = best;
    }

    void OnTargetChanged(Transform oldT, Transform newT)
    {
        if (!_marker) return;

        if (parentMarkerToTarget)
            _marker.SetParent(newT, worldPositionStays: true);

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

        if (_lastTargetCol == null || _lastTargetCol.transform != t)
            _lastTargetCol = t.GetComponentInParent<Collider>();

        Vector3 pos = t.position + markerOffset;
        if (_lastTargetCol)
            pos = _lastTargetCol.bounds.center + new Vector3(0, _lastTargetCol.bounds.extents.y, 0) + markerOffset * 0.2f;

        if (!parentMarkerToTarget) _marker.position = pos;
        else _marker.localPosition = t.InverseTransformPoint(pos);

        if (billboardToCamera && (_cam || (_cam = Camera.main)))
            _marker.forward = (_marker.position - _cam.transform.position).normalized;
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
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;              // <- ya NO aplano en Y aquí
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
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(-half, Vector3.up) * fwd * radius);
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(+half, Vector3.up) * fwd * radius);
        }

        if (drawTargetLine && CurrentTarget)
        {
            Gizmos.color = targetLineColor;
            Gizmos.DrawLine(origin, GetTargetCenter(CurrentTarget));
        }
    }
#endif
}
