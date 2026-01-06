using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class PlayerTargeting : MonoBehaviour, ITargetProvider
{
    // ================== SCAN / TARGETING ==================
    [Header("Búsqueda")]
    [SerializeField] private float radius = 8f;
    [Tooltip("Radio máximo usado para detectar enemigos (debe ser >= los radios personalizados de los enemigos).")]
    [SerializeField] private float scanRadius = 12f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float fovDegrees = 140f;
    [SerializeField] private bool requireLineOfSight = true;     // <- ahora true por defecto
    [SerializeField] private Transform aimOrigin;                 // arrastra la cámara aquí
    [SerializeField] private float updatesPerSecond = 10f;

    [Header("Visibilidad en pantalla")]
    [SerializeField] private bool mustBeOnScreen = true;          // <- NUEVO
    [SerializeField, Range(0f, 0.2f)] private float screenEdgePadding = 0.03f;

    [Header("Targeting Automático")]
    [Tooltip("Si está activo, cualquier objeto en enemyMask será targeteable automáticamente sin necesidad del componente Targetable")]
    [SerializeField] private bool autoTargetByLayer = true;
    [Tooltip("Si autoTargetByLayer está activo, ¿requiere que el enemigo tenga Damageable y esté vivo?")]
    [SerializeField] private bool requireDamageableAlive = true;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawRadius = true;
    [SerializeField] private bool drawFOV = true;
    [SerializeField] private bool drawTargetLine = true;
    [SerializeField] private Color radiusColor = new Color(0f, 0.7f, 1f, 0.35f);
    [SerializeField] private Color scanRadiusColor = new Color(0f, 0.4f, 1f, 0.18f);
    [SerializeField] private Color fovColor = new Color(0.2f, 1f, 0.4f, 0.25f);
    [SerializeField] private Color targetLineColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    public Transform CurrentTarget { get; private set; }

    float _nextScan;
    Transform _marker;
    Collider _lastTargetCol;
    Camera _cam;
    // buffer reutilizable para evitar allocations en OverlapSphereNonAlloc
    private Collider[] _overlapBuffer = new Collider[64];
    
    // Referencia al Damageable del target actual para detectar muerte inmediata
    private Damageable _currentTargetDamageable;

    [Header("Feedback de Target (Opcional)")]
    [SerializeField] private bool enableMarker = true;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Vector3 markerOffset = new(0, 1.8f, 0);
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private bool parentMarkerToTarget;
    
    [Header("Animación del Marker")]
    [SerializeField] private float markerShowDuration = 0.25f;
    [SerializeField] private float markerHideDuration = 0.15f;
    
    private Vector3 _markerOriginalScale;
    private Tween _markerTween;
    private bool _markerVisible;

    void Awake()
    {
        _cam = Camera.main;
        
        // Debug: Verificar configuración del marker
        Debug.Log($"[PlayerTargeting] Awake - enableMarker={enableMarker}, markerPrefab={markerPrefab}, enemyMask={enemyMask.value}");
        
        if (enableMarker && markerPrefab)
        {
            var go = Instantiate(markerPrefab);
            go.SetActive(false);
            _marker = go.transform;
            _markerOriginalScale = _marker.localScale;
            _marker.localScale = Vector3.zero; // Empezar pequeño para animación
            Debug.Log($"[PlayerTargeting] ✅ Marker instanciado: {go.name}");
        }
        else
        {
            Debug.LogWarning($"[PlayerTargeting] ⚠️ Marker NO instanciado - enableMarker={enableMarker}, markerPrefab={(markerPrefab != null ? markerPrefab.name : "NULL")}");
        }
        
        if (!aimOrigin && _cam) aimOrigin = _cam.transform; // <- recomendable
    }

    void OnDestroy()
    {
        _markerTween?.Kill();
        if (_marker) Destroy(_marker.gameObject);
        
        // Desuscribirse del Damageable del target actual
        if (_currentTargetDamageable != null)
        {
            _currentTargetDamageable.OnDied -= OnCurrentTargetDied;
            _currentTargetDamageable = null;
        }
    }
    
    /// <summary>
    /// Callback cuando el target actual muere - limpia el marker inmediatamente
    /// </summary>
    private void OnCurrentTargetDied()
    {
        Debug.Log($"[PlayerTargeting] 💀 Target muerto, limpiando marker inmediatamente");
        
        // Desuscribirse
        if (_currentTargetDamageable != null)
        {
            _currentTargetDamageable.OnDied -= OnCurrentTargetDied;
            _currentTargetDamageable = null;
        }
        
        // Limpiar target y marker
        CurrentTarget = null;
        OnTargetChanged(null);
        
        // Forzar un scan inmediato para buscar nuevo target
        _nextScan = 0f;
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
                OnTargetChanged(CurrentTarget);
        }
    }

    void LateUpdate() => UpdateMarker();

    void Scan()
    {
        var origin = aimOrigin ? aimOrigin.position : transform.position + Vector3.up;
        var fwd    = aimOrigin ? aimOrigin.forward  : transform.forward;

        int hitCount = Physics.OverlapSphereNonAlloc(origin, /*radius*/ scanRadius, _overlapBuffer, enemyMask, QueryTriggerInteraction.Collide);
         float bestScore = float.NegativeInfinity;
         Transform best = null;

         for (int i = 0; i < hitCount; i++)
         {
             var h = _overlapBuffer[i];
             if (!h) continue;
             
             // Verificar que el collider y su gameObject estén activos
             if (!h.enabled || !h.gameObject.activeInHierarchy) continue;

             Vector3 center = GetTargetCenter(h.transform);
             Vector3 to = center - origin;
             float dist = to.magnitude;
             if (dist < 0.01f) continue;

            // Si el enemigo tiene un componente Targetable, verificar si está en combate activo
            var cfg = h.transform.GetComponentInParent<Targetable>();
            
            // Determinar si este enemigo es válido para targeting
            bool isValidTarget = false;
            float allowedRadius = radius;
            
            // ✅ SIEMPRE verificar si el enemigo está vivo antes de targetearlo
            if (requireDamageableAlive)
            {
                var damageable = h.transform.GetComponentInParent<Damageable>();
                if (damageable == null || !damageable.IsAlive) continue;
            }
            
            if (cfg != null && cfg.isInActiveCombat)
            {
                // Tiene Targetable con combate activo: usar su configuración
                isValidTarget = true;
                if (cfg.targetingRadius > 0f)
                    allowedRadius = cfg.targetingRadius;
            }
            else if (cfg != null && !cfg.isInActiveCombat)
            {
                // Tiene Targetable pero NO está en combate activo
                // Verificar si es un enemigo puro (sin sistema NPC narrativo) o un NPC en alerta
                var npcManager = h.transform.GetComponentInParent<Game.NPC.NPCBehaviourManagerV2>();
                
                if (npcManager != null)
                {
                    // Es un NPC con sistema narrativo: NO targetear si no está en combate activo
                    // (Esto incluye NPCs en diálogo de alerta pre-combate)
                    continue;
                }
                else
                {
                    // Es un enemigo puro sin sistema NPC: targeteable siempre (ej: demonio, arañas)
                    isValidTarget = true;
                    if (cfg.targetingRadius > 0f)
                        allowedRadius = cfg.targetingRadius;
                }
            }
            else if (autoTargetByLayer && cfg == null)
            {
                // NO tiene Targetable, usar autoTargetByLayer
                // (Esto es para enemigos simples como monstruos sin el sistema NPC)
                isValidTarget = true;
            }
            
            if (!isValidTarget) continue;
             
             // Si está fuera del radio permitido para ese enemigo, ignóralo
             if (dist > allowedRadius) continue;

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
            float score = Vector3.Dot(fwd, dir) * 1.0f - (dist / Mathf.Max(0.0001f, allowedRadius)) * 0.35f;
            if (score > bestScore) { bestScore = score; best = h.transform; }
        }

        CurrentTarget = best;
    }

    void OnTargetChanged(Transform newT)
    {
        Debug.Log($"[PlayerTargeting] 🎯 Target changed: {(newT != null ? newT.name : "NULL")}");
        
        // Desuscribirse del Damageable anterior
        if (_currentTargetDamageable != null)
        {
            _currentTargetDamageable.OnDied -= OnCurrentTargetDied;
            _currentTargetDamageable = null;
        }
        
        // Suscribirse al nuevo Damageable para detectar muerte inmediata
        if (newT != null)
        {
            _currentTargetDamageable = newT.GetComponentInParent<Damageable>();
            if (_currentTargetDamageable != null)
            {
                _currentTargetDamageable.OnDied += OnCurrentTargetDied;
            }
        }
        
        if (!_marker) return;

        if (parentMarkerToTarget)
            _marker.SetParent(newT, worldPositionStays: true);

        if (!newT)
        {
            // Ocultar marker con animación
            if (_markerVisible)
            {
                _markerVisible = false;
                _markerTween?.Kill();
                _markerTween = _marker.DOScale(Vector3.zero, markerHideDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        if (_marker != null)
                            _marker.gameObject.SetActive(false);
                    });
            }
            _lastTargetCol = null;
        }
        else
        {
            // Mostrar marker con animación
            if (!_markerVisible)
            {
                _markerVisible = true;
                _marker.gameObject.SetActive(true);
                _marker.localScale = Vector3.zero;
                _markerTween?.Kill();
                _markerTween = _marker.DOScale(_markerOriginalScale, markerShowDuration)
                    .SetEase(Ease.OutBack);
            }
        }
    }

    void UpdateMarker()
    {
        if (!_marker || !enableMarker) return;

        var t = CurrentTarget;
        if (!t)
        {
            // No desactivar aquí - OnTargetChanged maneja la animación
            return;
        }

        // No activar aquí si está en animación de ocultar
        if (!_markerVisible) return;

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
            // dibujar radio de targeting (radius)
            Gizmos.DrawWireSphere(origin, radius);
            // dibujar radio de scan (scanRadius) con color más sutil
            Gizmos.color = scanRadiusColor;
            Gizmos.DrawWireSphere(origin, scanRadius);
        }

        if (drawFOV)
        {
            Gizmos.color = fovColor;
            float half = fovDegrees * 0.5f;
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(-half, Vector3.up) * fwd * scanRadius);
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(+half, Vector3.up) * fwd * scanRadius);
        }

        if (drawTargetLine && CurrentTarget)
        {
            Gizmos.color = targetLineColor;
            Gizmos.DrawLine(origin, GetTargetCenter(CurrentTarget));
        }
    }
#endif
}
