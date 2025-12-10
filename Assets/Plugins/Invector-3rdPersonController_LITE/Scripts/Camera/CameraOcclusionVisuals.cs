using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
public class CameraOcclusionVisuals : MonoBehaviour
{
    [Header("Targets")]
    public Transform cameraTransform;   // si lo dejas vacío, usa Camera.main
    public Transform target;            // tu player / cabeza del player

    [Header("Detección")]
    public LayerMask fadeLayers;        // qué capas pueden “desaparecer suave”
    public float castRadius = 0.15f;    // 0 = raycast fino; 0.15-0.25 va bien
    public float maxDistancePadding = 0.05f; // margen para el cast

    [Header("Efecto")]
    [Range(0,1f)] public float targetAlpha = 0.25f;     // cuánta transparencia al ocluir
    [Range(0,1f)] public float targetDesaturation = 0.8f; // B/N al ocluir (0..1)
    public Color tintWhenOccluded = Color.white;         // tinte adicional
    public float fadeInSpeed = 10f;                      // hacia el estado ocluido
    public float fadeOutSpeed = 6f;                      // volver a normal

    [Header("Deformación (opcional)")]
    public bool deformWhenOccluded = true;               // si true, se aplasta suavemente
    [Range(0f, 1f)] public float squashAmount = 0.18f;    // cuánto se reduce la escala
    public Vector3 squashAxis = new Vector3(0.15f, 0.8f, 0.15f); // peso por eje
    public float squashInSpeed = 8f;
    public float squashOutSpeed = 6f;

    // --- internos ---
    struct Fadable
    {
        public Renderer renderer;
        public Transform transform;
        public MaterialPropertyBlock mpb;
        public float curAlpha;
        public float curDesat;
        public float curSquash;
        public Vector3 baseScale;
    }

    private readonly Dictionary<Renderer, Fadable> _active = new Dictionary<Renderer, Fadable>();
    private readonly HashSet<Renderer> _thisFrame = new HashSet<Renderer>();
    private readonly List<Renderer> _activeKeysCache = new List<Renderer>();

    static readonly int ID_TintColor = Shader.PropertyToID("_TintColor");
    static readonly int ID_Desat     = Shader.PropertyToID("_Desat");

    void Reset()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (!cameraTransform)
        {
            var cam = Camera.main;
            if (cam) cameraTransform = cam.transform;
        }
        if (!cameraTransform || !target) return;

        // Limpiar set de “vistos este frame”
        _thisFrame.Clear();

        // SphereCast a lo largo del segmento cámara → target
        Vector3 origin = cameraTransform.position;
        Vector3 dest   = target.position;
        Vector3 dir    = (dest - origin);
        float dist     = dir.magnitude + maxDistancePadding;
        if (dist <= 0.0001f) return;
        dir /= dist;

        var hits = Physics.SphereCastAll(origin, castRadius, dir, dist, fadeLayers, QueryTriggerInteraction.Ignore);

        // Marcar ocluidores este frame
        for (int i = 0; i < hits.Length; i++)
        {
            var rend = hits[i].collider.GetComponentInParent<Renderer>();
            if (!rend) continue;
            _thisFrame.Add(rend);

            if (!_active.TryGetValue(rend, out var f))
            {
                f = new Fadable
                {
                    renderer = rend,
                    transform = rend.transform,
                    mpb = new MaterialPropertyBlock(),
                    curAlpha = 1f,
                    curDesat = 0f,
                    curSquash = 0f,
                    baseScale = rend.transform.localScale
                };
                _active[rend] = f;
            }
        }

        // Actualizar todos los que tenemos registrados (fade in/out)
        var toRestore = new List<Renderer>();
        _activeKeysCache.Clear();
        foreach (var kv in _active)
        {
            _activeKeysCache.Add(kv.Key);
        }

        for (int i = 0; i < _activeKeysCache.Count; i++)
        {
            var rend = _activeKeysCache[i];
            if (!_active.TryGetValue(rend, out var f))
                continue;

            bool occluding = _thisFrame.Contains(rend);
            float aTarget  = occluding ? Mathf.Clamp01(targetAlpha) : 1f;
            float dTarget  = occluding ? Mathf.Clamp01(targetDesaturation) : 0f;
            float sTarget  = occluding && deformWhenOccluded ? Mathf.Clamp01(squashAmount) : 0f;

            float spdA = occluding ? fadeInSpeed : fadeOutSpeed;
            float spdD = occluding ? fadeInSpeed : fadeOutSpeed;
            float spdS = occluding ? squashInSpeed : squashOutSpeed;

            f.curAlpha = Mathf.Lerp(f.curAlpha, aTarget, Mathf.Clamp01(spdA * Time.deltaTime));
            f.curDesat = Mathf.Lerp(f.curDesat, dTarget, Mathf.Clamp01(spdD * Time.deltaTime));
            f.curSquash = Mathf.Lerp(f.curSquash, sTarget, Mathf.Clamp01(spdS * Time.deltaTime));

            // Aplicar a MPB
            rend.GetPropertyBlock(f.mpb);
            var tint = new Color(tintWhenOccluded.r, tintWhenOccluded.g, tintWhenOccluded.b, f.curAlpha);
            f.mpb.SetColor(ID_TintColor, tint);
            f.mpb.SetFloat(ID_Desat, f.curDesat);
            rend.SetPropertyBlock(f.mpb);

            // Deformar (aplastar) ligeramente para que parezca máscara que se pega
            if (f.transform && deformWhenOccluded)
            {
                var squashVec = new Vector3(
                    1f - (squashAxis.x * f.curSquash),
                    1f - (squashAxis.y * f.curSquash),
                    1f - (squashAxis.z * f.curSquash));
                f.transform.localScale = Vector3.Scale(f.baseScale, squashVec);
            }

            _active[rend] = f;

            // Si ya volvió “casi” a normal, limpiar
            if (!occluding && f.curAlpha > 0.995f && f.curDesat < 0.005f && f.curSquash < 0.005f)
                toRestore.Add(rend);
        }

        // Restaurar y purgar
        for (int i = 0; i < toRestore.Count; i++)
        {
            var r = toRestore[i];
            if (!r) continue;
            if (_active.TryGetValue(r, out var f))
            {
                if (f.transform) f.transform.localScale = f.baseScale;
            }
            r.SetPropertyBlock(null);
            _active.Remove(r);
        }
    }

    void OnDisable()
    {
        foreach (var kv in _active)
        {
            var f = kv.Value;
            if (f.renderer) f.renderer.SetPropertyBlock(null);
            if (f.transform) f.transform.localScale = f.baseScale;
        }
        _active.Clear();
        _thisFrame.Clear();
        _activeKeysCache.Clear();
    }
}
