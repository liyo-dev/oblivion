using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraOcclusionShadowsOnly : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private LayerMask obstructionMask = ~0;   // capas que pueden tapar
    [SerializeField] private float checkRadius = 0.15f;        // radio del spherecast
    [SerializeField] private float releaseDelay = 0.2f;        // histéresis al soltar

    [Header("Efecto de pintada")]
    [SerializeField] private Shader occlusionShader;
    [SerializeField, Range(0f, 1f)] private float revealAmount = 0.9f; // cuánto se "come" la pintada
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.15f; // nunca desaparecer del todo
    [SerializeField] private float fadeInSpeed = 6f;                    // velocidad al aplicar
    [SerializeField] private float fadeOutSpeed = 3f;                   // velocidad al restaurar
    [SerializeField] private float noiseScale = 5f;                     // deformación de la salpicadura
    [SerializeField] private float edgeWidth = 0.2f;                    // borde suave de la salpicadura

    [Header("Debug")]
    [SerializeField] private bool debugRays = false;

    private class Entry
    {
        public Renderer Renderer;
        public Material[] OriginalMaterials;
        public Material[] PaintedMaterials;
        public float LastSeen;
        public float Progress; // 0 = normal, 1 = pintado
    }

    private readonly Dictionary<Renderer, Entry> _active = new();
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

        if (!occlusionShader)
            occlusionShader = Shader.Find("Custom/CameraOcclusionPaint");

        if (debugRays) Debug.DrawLine(from, to, Color.magenta, 0f, false);

        var hits = Physics.SphereCastAll(from, checkRadius, dir, dist, obstructionMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            var renderer = hits[i].collider.GetComponentInParent<Renderer>();
            if (!renderer) continue;

            var entry = GetOrCreateEntry(renderer);
            entry.LastSeen = now;
        }

        UpdateEntries(now);
    }

    private void UpdateEntries(float now)
    {
        _toRestore.Clear();

        foreach (var kvp in _active)
        {
            var entry = kvp.Value;
            bool shouldOcclude = now - entry.LastSeen <= releaseDelay;
            float speed = shouldOcclude ? fadeInSpeed : fadeOutSpeed;
            float target = shouldOcclude ? 1f : 0f;

            entry.Progress = Mathf.MoveTowards(entry.Progress, target, speed * Time.deltaTime);

            ApplyMaterial(entry, entry.Progress);

            if (!shouldOcclude && Mathf.Approximately(entry.Progress, 0f))
                _toRestore.Add(entry.Renderer);
        }

        for (int i = 0; i < _toRestore.Count; i++)
        {
            var renderer = _toRestore[i];
            if (renderer && _active.TryGetValue(renderer, out var entry))
                RestoreEntry(entry);
            _active.Remove(renderer);
        }
    }

    private Entry GetOrCreateEntry(Renderer renderer)
    {
        if (_active.TryGetValue(renderer, out var existing))
            return existing;

        var entry = new Entry
        {
            Renderer = renderer,
            OriginalMaterials = renderer.sharedMaterials,
            PaintedMaterials = BuildPaintedMaterials(renderer)
        };

        _active[renderer] = entry;
        return entry;
    }

    private Material[] BuildPaintedMaterials(Renderer renderer)
    {
        var originalMats = renderer.sharedMaterials;
        var painted = new Material[originalMats.Length];

        for (int i = 0; i < originalMats.Length; i++)
        {
            var src = originalMats[i];
            var dst = new Material(occlusionShader);

            if (src)
            {
                if (src.HasProperty("_BaseMap")) dst.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                else if (src.HasProperty("_MainTex")) dst.SetTexture("_BaseMap", src.GetTexture("_MainTex"));

                if (src.HasProperty("_BaseColor")) dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                else if (src.HasProperty("_Color")) dst.SetColor("_BaseColor", src.GetColor("_Color"));
            }

            dst.SetFloat("_NoiseScale", noiseScale);
            dst.SetFloat("_EdgeWidth", edgeWidth);
            painted[i] = dst;
        }

        return painted;
    }

    private void ApplyMaterial(Entry entry, float progress)
    {
        if (!entry.Renderer) return;

        float reveal = Mathf.Lerp(0f, revealAmount, progress);
        float holdAlpha = Mathf.Lerp(1f, minimumAlpha, progress);

        var mats = progress > 0f ? entry.PaintedMaterials : entry.OriginalMaterials;
        entry.Renderer.sharedMaterials = mats;

        if (progress > 0f)
        {
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!m) continue;
                m.SetFloat("_Reveal", reveal);
                var color = m.GetColor("_BaseColor");
                color.a = holdAlpha;
                m.SetColor("_BaseColor", color);
            }
        }
    }

    private void RestoreEntry(Entry entry)
    {
        if (entry.Renderer)
            entry.Renderer.sharedMaterials = entry.OriginalMaterials;
        entry.Progress = 0f;
    }

    /// <summary>Restaura todo inmediatamente (p.ej. OnDisable).</summary>
    public void RestoreAll()
    {
        foreach (var entry in _active.Values)
            RestoreEntry(entry);
        _active.Clear();
    }

    void OnDisable() => RestoreAll();

    // Exponer setters si lo quieres tocar desde otro script
    public void SetMask(LayerMask m) => obstructionMask = m;
    public void SetRadius(float r) => checkRadius = Mathf.Max(0f, r);
    public void SetReleaseDelay(float d) => releaseDelay = Mathf.Max(0f, d);
}
