using UnityEngine;

public class PlayerTargeting : MonoBehaviour, ITargetProvider
{
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

    public Transform CurrentTarget { get; private set; }

    float _nextScan;

    void Update()
    {
        if (updatesPerSecond <= 0f || Time.time >= _nextScan)
        {
            Scan();
            if (updatesPerSecond > 0f)
                _nextScan = Time.time + 1f / updatesPerSecond;
        }
    }

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
