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

    // --- internos ---
    struct Fadable
    {
        public Renderer renderer;
        public MaterialPropertyBlock mpb;
        public float curAlpha;
        public float curDesat;
    }

    private readonly Dictionary<Renderer, Fadable> _active = new Dictionary<Renderer, Fadable>();
    private readonly HashSet<Renderer> _thisFrame = new HashSet<Renderer>();

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
                    mpb = new MaterialPropertyBlock(),
                    curAlpha = 1f,
                    curDesat = 0f
                };
                _active[rend] = f;
            }
        }

        // Actualizar todos los que tenemos registrados (fade in/out)
        var toRestore = new List<Renderer>();
        foreach (var kv in _active)
        {
            var rend = kv.Key;
            var f = kv.Value;

            bool occluding = _thisFrame.Contains(rend);
            float aTarget  = occluding ? Mathf.Clamp01(targetAlpha) : 1f;
            float dTarget  = occluding ? Mathf.Clamp01(targetDesaturation) : 0f;

            float spdA = occluding ? fadeInSpeed : fadeOutSpeed;
            float spdD = occluding ? fadeInSpeed : fadeOutSpeed;

            f.curAlpha = Mathf.Lerp(f.curAlpha, aTarget, Mathf.Clamp01(spdA * Time.deltaTime));
            f.curDesat = Mathf.Lerp(f.curDesat, dTarget, Mathf.Clamp01(spdD * Time.deltaTime));

            // Aplicar a MPB
            rend.GetPropertyBlock(f.mpb);
            var tint = new Color(tintWhenOccluded.r, tintWhenOccluded.g, tintWhenOccluded.b, f.curAlpha);
            f.mpb.SetColor(ID_TintColor, tint);
            f.mpb.SetFloat(ID_Desat, f.curDesat);
            rend.SetPropertyBlock(f.mpb);

            _active[rend] = f;

            // Si ya volvió “casi” a normal, limpiar
            if (!occluding && f.curAlpha > 0.995f && f.curDesat < 0.005f)
                toRestore.Add(rend);
        }

        // Restaurar y purgar
        for (int i = 0; i < toRestore.Count; i++)
        {
            var r = toRestore[i];
            if (!r) continue;
            r.SetPropertyBlock(null);
            _active.Remove(r);
        }
    }
}
