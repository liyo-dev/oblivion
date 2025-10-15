using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class CameraOcclusionShadowsOnly : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private LayerMask obstructionMask = ~0;   // capas que pueden tapar
    [SerializeField] private float checkRadius = 0.15f;        // radio del spherecast
    [SerializeField] private float releaseDelay = 0.2f;        // histéresis al soltar

    [Header("Debug")]
    [SerializeField] private bool debugRays = false;

    // estado
    private readonly Dictionary<Renderer, float> _active = new(); // renderer -> lastSeenTime
    private readonly List<Renderer> _toRestore = new(32);
    private Transform _cam;

    void Awake() { _cam = transform; }

    /// <summary>Llama cada frame con los puntos cámara→objetivo.</summary>
    public void Process(Vector3 from, Vector3 to)
    {
        float now = Time.time;
        Vector3 dir = (to - from);
        float dist = dir.magnitude;
        if (dist < 0.0001f) return;
        dir /= dist;

        if (debugRays) Debug.DrawLine(from, to, Color.magenta, 0f, false);

        // detecta todo lo que hay ENTRE cámara y objetivo
        var hits = Physics.SphereCastAll(from, checkRadius, dir, dist, obstructionMask, QueryTriggerInteraction.Ignore);

        // marca / aplica ShadowsOnly a nuevos hits
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            var r = h.collider.GetComponentInParent<Renderer>();
            if (!r) continue;

            // ignora si ya está en ShadowsOnly por diseño
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
            {
                _active[r] = now;
                continue;
            }

            // pon en sombrita
            _active[r] = now;
            r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        // recolecta para restaurar los que no hemos visto hace un ratito
        _toRestore.Clear();
        foreach (var kv in _active)
        {
            if (now - kv.Value > releaseDelay) _toRestore.Add(kv.Key);
        }

        // restaura a On
        for (int i = 0; i < _toRestore.Count; i++)
        {
            var r = _toRestore[i];
            if (r) r.shadowCastingMode = ShadowCastingMode.On;
            _active.Remove(r);
        }
    }

    /// <summary>Restaura todo inmediatamente (p.ej. OnDisable).</summary>
    public void RestoreAll()
    {
        foreach (var r in _active.Keys)
            if (r) r.shadowCastingMode = ShadowCastingMode.On;
        _active.Clear();
    }

    void OnDisable() => RestoreAll();

    // Exponer setters si lo quieres tocar desde otro script
    public void SetMask(LayerMask m) => obstructionMask = m;
    public void SetRadius(float r) => checkRadius = Mathf.Max(0f, r);
    public void SetReleaseDelay(float d) => releaseDelay = Mathf.Max(0f, d);
}
