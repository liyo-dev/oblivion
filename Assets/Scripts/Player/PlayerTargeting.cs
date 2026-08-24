using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PlayerTargeting : MonoBehaviour, ITargetProvider
{
    [Header("Búsqueda")]
    [SerializeField] private float radius = 8f;
    [SerializeField] private float scanRadius = 12f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float fovDegrees = 140f;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float updatesPerSecond = 10f;

    [Header("Visibilidad en pantalla")]
    [SerializeField] private bool mustBeOnScreen = true;
    [SerializeField, Range(0f, 0.2f)] private float screenEdgePadding = 0.03f;

    [Header("Targeting Automático")]
    [SerializeField] private bool requireDamageableAlive = true;

    [Header("Feedback de Target")]
    [SerializeField] private bool enableMarker = true;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Vector3 markerOffset = new(0, 1.8f, 0);
    [SerializeField] private bool billboardToCamera = true;
    
    [Header("Animación del Marker")]
    [SerializeField] private float markerShowDuration = 0.25f;
    [SerializeField] private float markerHideDuration = 0.15f;
    [SerializeField] private int markerSortingOrder = 500;
    [SerializeField] private bool forceUnlitForSpriteMarker = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;
#if UNITY_EDITOR
    [SerializeField] private bool drawRadius = true;
    [SerializeField] private bool drawFOV = true;
    [SerializeField] private bool drawTargetLine = true;
#endif
    
    public Transform CurrentTarget { get; private set; }
    public bool IsManualTargetActive => _isManualTargetActive;

    private bool _isManualTargetActive;
    // Cuando está activo, se ignora el auto-scan (Scan()) por completo: un sistema externo
    // (ej. CombatCameraTargeting durante el lock-on de cámara) tiene control exclusivo del
    // target/marker. Sin esto, el auto-scan reengancha el marker usando el FOV de la cámara
    // (que durante el lock-on SIEMPRE apunta al enemigo), pisando el gate de "¿el jugador
    // realmente está mirando al enemigo?" que hace el sistema de cámara.
    private bool _autoScanSuppressed;
    private float _nextScan;
    private Transform _marker;
    private Camera _cam;
    private Collider[] _overlapBuffer = new Collider[64];
    private Damageable _currentTargetDamageable;
    private Vector3 _markerOriginalScale;
    private Tween _markerTween;
    private bool _markerVisible;
    private bool _hiddenByDialogue;
    private readonly List<Material> _runtimeMarkerMaterials = new();
    // Centro local del collider del target actual, cacheado para evitar bounds.center en LateUpdate
    private Vector3 _cachedTargetLocalCenter = Vector3.up;

    void Awake()
    {
        _cam = Camera.main;

        if (enableMarker && markerPrefab)
        {
            var go = Instantiate(markerPrefab);
            go.SetActive(false);
            _marker = go.transform;
            _markerOriginalScale = _marker.localScale;
            _marker.localScale = Vector3.zero;
            ConfigureMarkerVisuals(go);
        }

        if (!aimOrigin && _cam) aimOrigin = _cam.transform;
    }

    void OnEnable()
    {
        DialogueManager.OnDialogueStarted += HandleDialogueStarted;
        DialogueManager.OnDialogueClosed += HandleDialogueClosed;
    }

    void OnDisable()
    {
        DialogueManager.OnDialogueStarted -= HandleDialogueStarted;
        DialogueManager.OnDialogueClosed -= HandleDialogueClosed;
    }

    void HandleDialogueStarted(Transform _)
    {
        if (!_marker) return;
        _hiddenByDialogue = true;
        if (!_markerVisible) return;
        _markerTween?.Kill();
        _markerTween = _marker.DOScale(Vector3.zero, markerHideDuration).SetEase(Ease.InBack)
            .OnComplete(() => _marker.gameObject.SetActive(false));
    }

    void HandleDialogueClosed(Transform _)
    {
        if (!_hiddenByDialogue) return;
        _hiddenByDialogue = false;
        if (_marker && _markerVisible && CurrentTarget != null)
        {
            _markerTween?.Kill();
            _marker.gameObject.SetActive(true);
            _marker.localScale = Vector3.zero;
            _markerTween = _marker.DOScale(_markerOriginalScale, markerShowDuration).SetEase(Ease.OutBack);
        }
    }

    void OnDestroy()
    {
        _markerTween?.Kill();
        if (_marker) Destroy(_marker.gameObject);
        foreach (var material in _runtimeMarkerMaterials)
        {
            if (material != null) Destroy(material);
        }
        _runtimeMarkerMaterials.Clear();
        
        if (_currentTargetDamageable != null)
        {
            _currentTargetDamageable.OnDied -= OnCurrentTargetDied;
        }
    }
    
    private void OnCurrentTargetDied()
    {
        if (verboseLogging) Debug.Log($"[PlayerTargeting] Target '{_currentTargetDamageable.name}' muerto, limpiando.");
        if (_isManualTargetActive)
        {
            ClearManualTarget();
            return;
        }

        CurrentTarget = null;
        OnTargetChanged(null);
        _nextScan = 0f;
    }
    
    public void SetManualTarget(Transform target)
    {
        if (target == null)
        {
            ClearManualTarget();
            return;
        }

        _isManualTargetActive = (target != null);
        
        bool changed = CurrentTarget != target;
        if (changed)
        {
            CurrentTarget = target;
            OnTargetChanged(target);
        }
        else if (_marker != null && (!_markerVisible || !_marker.gameObject.activeSelf))
        {
            // Reforzar el estado visual si otro sistema desactivó el marker.
            OnTargetChanged(target);
        }

        UpdateMarker();
        
        if (verboseLogging) Debug.Log($"[PlayerTargeting] Target MANUAL establecido: {(target ? target.name : "NULL")}");
    }

    void ConfigureMarkerVisuals(GameObject markerRoot)
    {
        if (markerRoot == null) return;

        var spriteRenderers = markerRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null) continue;
            spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, markerSortingOrder);
            spriteRenderer.color = Color.white;

            if (!forceUnlitForSpriteMarker) continue;

            var sharedMaterial = spriteRenderer.sharedMaterial;
            bool shouldApplyUnlitFallback = sharedMaterial == null;
            if (!shouldApplyUnlitFallback && sharedMaterial.shader != null)
            {
                string shaderName = sharedMaterial.shader.name;
                shouldApplyUnlitFallback =
                    shaderName.IndexOf("Sprite-Lit", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (shaderName.IndexOf("Sprite", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                     shaderName.IndexOf("Lit", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!shouldApplyUnlitFallback) continue;

            Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) continue;

            var runtimeMaterial = new Material(unlitShader)
            {
                name = $"{spriteRenderer.gameObject.name}_MarkerRuntime"
            };

            if (spriteRenderer.sprite != null)
            {
                var texture = spriteRenderer.sprite.texture;
                if (runtimeMaterial.HasProperty("_MainTex")) runtimeMaterial.SetTexture("_MainTex", texture);
                if (runtimeMaterial.HasProperty("_BaseMap")) runtimeMaterial.SetTexture("_BaseMap", texture);
            }

            if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", Color.white);
            if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", Color.white);
            runtimeMaterial.renderQueue = 3000;

            spriteRenderer.material = runtimeMaterial;
            _runtimeMarkerMaterials.Add(runtimeMaterial);
        }
    }
    
    public void ClearManualTarget()
    {
        if (!_isManualTargetActive) return;
        
        _isManualTargetActive = false;
        if (CurrentTarget != null)
        {
            CurrentTarget = null;
            OnTargetChanged(null);
        }
        if (verboseLogging) Debug.Log($"[PlayerTargeting] Target manual liberado.");
    }

    public void ForceVisualRefresh()
    {
        OnTargetChanged(CurrentTarget);
        UpdateMarker();
    }

    /// <summary>
    /// Suprime/reactiva el auto-scan (Scan()). Lo usa CombatCameraTargeting mientras el lock-on
    /// de cámara está activo, para que el target/marker dependa únicamente de si el jugador está
    /// mirando (de cuerpo) hacia el enemigo, y no del auto-scan reenganchándolo por el FOV de cámara.
    /// </summary>
    public void SetAutoScanSuppressed(bool suppressed)
    {
        if (_autoScanSuppressed == suppressed) return;
        _autoScanSuppressed = suppressed;
        if (verboseLogging) Debug.Log($"[PlayerTargeting] Auto-scan suprimido: {suppressed}");
    }

    void Update()
    {
        if (_isManualTargetActive)
        {
            if (CurrentTarget == null || !CurrentTarget.gameObject.activeInHierarchy || (_currentTargetDamageable != null && !_currentTargetDamageable.IsAlive))
            {
                if (verboseLogging) Debug.Log($"[PlayerTargeting] Target manual inválido, limpiando.");
                ClearManualTarget();
            }
            return;
        }

        if (_autoScanSuppressed) return;

        if (updatesPerSecond <= 0f || Time.time >= _nextScan)
        {
            var before = CurrentTarget;
            Scan();
            if (before != CurrentTarget)
                OnTargetChanged(CurrentTarget);
            
            if (updatesPerSecond > 0f)
                _nextScan = Time.time + 1f / updatesPerSecond;
        }
    }

    void LateUpdate() => UpdateMarker();

    void Scan()
    {
        var origin = aimOrigin ? aimOrigin.position : transform.position + Vector3.up;
        var fwd = aimOrigin ? aimOrigin.forward : transform.forward;

        int hitCount = Physics.OverlapSphereNonAlloc(origin, scanRadius, _overlapBuffer, enemyMask, QueryTriggerInteraction.Collide);
        float bestScore = float.NegativeInfinity;
        Transform best = null;

        for (int i = 0; i < hitCount; i++)
        {
            var h = _overlapBuffer[i];
            if (!h || !h.enabled || !h.gameObject.activeInHierarchy) continue;

            if (requireDamageableAlive)
            {
                var damageable = h.GetComponentInParent<Damageable>();
                if (damageable == null || !damageable.IsAlive)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[DEBUG-AIM] '{h.gameObject.name}' descartado: sin Damageable vivo (damageable={(damageable == null ? "NULL" : "existe")}, alive={(damageable != null && damageable.IsAlive)})");
#endif
                    continue;
                }
            }

            Vector3 center = GetTargetCenter(h.transform);
            Vector3 to = center - origin;
            float dist = to.magnitude;
            if (dist < 0.01f) continue;

            var cfg = h.GetComponentInParent<Targetable>();
            if (cfg && !cfg.isInActiveCombat)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DEBUG-AIM] '{h.gameObject.name}' descartado: Targetable.isInActiveCombat=false");
#endif
                continue;
            }
            float allowedRadius = (cfg && cfg.targetingRadius > 0) ? cfg.targetingRadius : radius;
            if (dist > allowedRadius)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DEBUG-AIM] '{h.gameObject.name}' descartado: dist={dist:F2} > allowedRadius={allowedRadius:F2}");
#endif
                continue;
            }

            Vector3 dir = to / dist;
            float angle = Vector3.Angle(fwd, dir);
            if (angle > fovDegrees * 0.5f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DEBUG-AIM] '{h.gameObject.name}' descartado: fuera de FOV (angle={angle:F1} > {fovDegrees * 0.5f:F1}) fwd={fwd} dir={dir}");
#endif
                continue;
            }

            if (mustBeOnScreen && _cam)
            {
                Vector3 vp = _cam.WorldToViewportPoint(center);
                if (vp.z <= 0f || vp.x < screenEdgePadding || vp.x > 1f - screenEdgePadding || vp.y < screenEdgePadding || vp.y > 1f - screenEdgePadding)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[DEBUG-AIM] '{h.gameObject.name}' descartado: fuera de pantalla (viewport={vp})");
#endif
                    continue;
                }
            }

            if (requireLineOfSight && Physics.Raycast(origin, dir, out var rh, dist, ~0, QueryTriggerInteraction.Ignore) && rh.collider.transform.root != h.transform.root)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DEBUG-AIM] '{h.gameObject.name}' descartado: línea de visión bloqueada por '{rh.collider.gameObject.name}' (root='{rh.collider.transform.root.name}') a {rh.distance:F2}m de {dist:F2}m totales");
#endif
                continue;
            }

            float score = Vector3.Dot(fwd, dir) - (dist / allowedRadius) * 0.35f;
            if (score > bestScore) { bestScore = score; best = h.transform; }
        }
        CurrentTarget = best;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (hitCount > 0)
            Debug.Log($"[DEBUG-AIM] Scan() candidatos={hitCount} CurrentTarget={(best ? best.name : "NULL")}");
#endif
    }

    void OnTargetChanged(Transform newT)
    {
        if (verboseLogging) Debug.Log($"[PlayerTargeting] OnTargetChanged: {(newT ? newT.name : "NULL")}");

        if (_currentTargetDamageable != null) _currentTargetDamageable.OnDied -= OnCurrentTargetDied;

        if (newT != null)
        {
            _currentTargetDamageable = newT.GetComponentInParent<Damageable>();
            if (_currentTargetDamageable != null) _currentTargetDamageable.OnDied += OnCurrentTargetDied;

            // Cachear el centro local del collider una sola vez para no leer bounds.center cada frame.
            // bounds.center se actualiza en FixedUpdate y causa temblor si se lee en LateUpdate.
            var col = newT.GetComponentInParent<Collider>();
            _cachedTargetLocalCenter = col
                ? newT.InverseTransformPoint(col.bounds.center)
                : Vector3.up;
        }
        else
        {
            _cachedTargetLocalCenter = Vector3.up;
        }
        
        if (!_marker) return;

        if (newT)
        {
            if (!_markerVisible)
            {
                _markerVisible = true;
                if (!_hiddenByDialogue)
                {
                    _markerTween?.Kill();
                    _marker.gameObject.SetActive(true);
                    _marker.localScale = Vector3.zero;
                    _markerTween = _marker.DOScale(_markerOriginalScale, markerShowDuration).SetEase(Ease.OutBack);
                }
            }
        }
        else
        {
            if (_markerVisible)
            {
                _markerVisible = false;
                _markerTween?.Kill();
                _markerTween = _marker.DOScale(Vector3.zero, markerHideDuration).SetEase(Ease.InBack).OnComplete(() => _marker.gameObject.SetActive(false));
            }
        }
    }

    void UpdateMarker()
    {
        if (!_marker) return;

        if (_hiddenByDialogue) return;

        if (CurrentTarget == null)
        {
            if (_marker.gameObject.activeSelf)
                _marker.gameObject.SetActive(false);
            return;
        }

        if (!_marker.gameObject.activeSelf)
            _marker.gameObject.SetActive(true);

        // Usar el centro local cacheado con TransformPoint para seguir al transform interpolado,
        // en lugar de bounds.center que salta al ritmo de FixedUpdate y causa temblor.
        Vector3 pos = CurrentTarget.TransformPoint(_cachedTargetLocalCenter) + markerOffset;
        _marker.position = pos;
        
        // DEBUG: Verificar posición del marcador
        if (verboseLogging)
        {
            // Solo loguear cada 60 frames para no saturar
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[PlayerTargeting] UpdateMarker: Target={CurrentTarget.name}, Pos={pos}, MarkerPos={_marker.position}");
            }
        }

        if (billboardToCamera && _cam)
            _marker.forward = (_marker.position - _cam.transform.position).normalized;
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
            Vector3 targetPos = GetTargetCenter(CurrentTarget) + markerOffset;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DEBUG-AIM] GetAimDirectionFrom: usando CurrentTarget='{CurrentTarget.name}' targetPos={targetPos} dir={(targetPos - origin.position).normalized}");
#endif
            return (targetPos - origin.position).normalized;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[DEBUG-AIM] GetAimDirectionFrom: CurrentTarget=NULL, usando fallbackForward={fallbackForward}");
#endif
        return fallbackForward;
    }

    static Vector3 GetTargetCenter(Transform target)
    {
        var col = target.GetComponentInParent<Collider>();
        return col ? col.bounds.center : target.position + Vector3.up;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = aimOrigin ? aimOrigin.position : transform.position + Vector3.up;
        Vector3 fwd = aimOrigin ? aimOrigin.forward : transform.forward;

        if (drawRadius)
        {
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.color = new Color(0f, 0.4f, 1f, 0.18f);
            Gizmos.DrawWireSphere(origin, scanRadius);
        }

        if (drawFOV)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
            float half = fovDegrees * 0.5f;
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(-half, Vector3.up) * fwd * scanRadius);
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(+half, Vector3.up) * fwd * scanRadius);
        }

        if (drawTargetLine && CurrentTarget)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawLine(origin, GetTargetCenter(CurrentTarget));
        }
    }
#endif
}
