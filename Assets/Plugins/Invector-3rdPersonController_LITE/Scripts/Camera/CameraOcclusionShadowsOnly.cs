using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraOcclusionShadowsOnly : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private LayerMask obstructionMask = ~0;
    [Tooltip("Radio del SphereCast. 0.2 cubre obstáculos finos sin falsas detecciones.")]
    [SerializeField] private float checkRadius = 0.2f;
    [Tooltip("Histéresis al liberar la oclusión (segundos).")]
    [SerializeField] private float releaseDelay = 0.06f;
    [Tooltip("Margen en metros antes del jugador. Hits dentro de esta distancia al jugador NO se ocluden, evitando que un edificio desaparezca sólo porque el jugador está tocando su pared.")]
    [SerializeField] private float playerProximityMargin = 0.8f;

    [Header("Dissolve")]
    [SerializeField] private Shader occlusionShader;
    [Tooltip("Fracción de superficie que se disuelve en oclusión máxima. 0.45 = estilo ghost translúcido.")]
    [SerializeField, Range(0f, 1f)] private float revealAmount = 0.45f;
    [Tooltip("Alpha mínimo de la superficie que queda visible. 0.3 permite ver el player detrás.")]
    [SerializeField, Range(0f, 1f)] private float minimumAlpha = 0.30f;
    [Tooltip("Frecuencia del ruido de dissolve. 18 da un punteado fino típico de juegos AAA.")]
    [SerializeField] private float noiseScale = 18f;
    [Tooltip("Anchura de la zona de transición del borde. Valores bajos = borde más nítido.")]
    [SerializeField] private float edgeWidth = 0.07f;

    [Header("Borde luminoso")]
    [Tooltip("Color del glow en el borde del dissolve.")]
    [SerializeField] private Color edgeColor = new Color(0.55f, 0.82f, 1f, 1f);
    [Tooltip("Intensidad del glow (multiplica el color del borde).")]
    [SerializeField] private float edgeGlow = 3.0f;

    [Header("Transición")]
    [Tooltip("Velocidad de aparición del efecto (respuesta inmediata al bloqueo).")]
    [SerializeField] private float fadeInSpeed = 14f;
    [Tooltip("Velocidad de restauración al liberar el bloqueo.")]
    [SerializeField] private float fadeOutSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool debugRays = false;

    // ─── Estado interno ────────────────────────────────────────────────────────

    private class Entry
    {
        public Renderer  Renderer;
        public Material[] OriginalMaterials;
        public Material[] PaintedMaterials;
        public float     LastSeen;
        public float     Progress; // 0 = normal, 1 = pintado
    }

    private readonly Dictionary<Renderer, Entry> _active   = new();
    private readonly List<Renderer>              _toRestore = new(32);
    private bool _shaderMissing;
    private static bool _loggedShaderLoadAttempt;

    // Buffer pre-alocado para el SphereCast — evita Physics.SphereCastAll (aloca cada frame) en
    // el caso normal. 32 impactos cubre con margen cualquier oclusión real; si algún día se
    // supera, Process() cae a un SphereCastAll de respaldo solo ese frame (ver más abajo) en vez
    // de perder oclusiones silenciosamente.
    private readonly RaycastHit[] _hitsBuffer = new RaycastHit[32];

    // Los personajes (jugador y NPCs) no tienen capa propia — viven en Default, igual que la
    // geometría (ver AGENTS.md/CLAUDE.md §2) — así que el filtrado por LayerMask no basta para
    // excluirlos. NPCSimpleAnimator vive en Assets/Scripts (Assembly-CSharp), que compila
    // DESPUÉS que Assets/Plugins (Assembly-CSharp-firstpass), así que no se puede referenciar el
    // tipo directamente: se resuelve por nombre (igual que el puente EnvironmentQuery) y se
    // cachea por Renderer para no repetir el lookup cada frame.
    private readonly Dictionary<Renderer, bool> _characterCache = new();

    // ─── Carga de shader ──────────────────────────────────────────────────────

    private Shader LoadOcclusionShader()
    {
        Shader shader;

        if (!_loggedShaderLoadAttempt)
        {
            _loggedShaderLoadAttempt = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CameraOcclusionShadowsOnly] Cargando shader de oclusión...");
#endif
        }

        if (occlusionShader != null) return occlusionShader;

        shader = Resources.Load<Shader>("Shaders/CameraOcclusionPaint");
        if (shader != null) return shader;

        var backupMat = Resources.Load<Material>("Shaders/CameraOcclusionPaint_Backup");
        if (backupMat != null && backupMat.shader != null) return backupMat.shader;

        shader = Shader.Find("Custom/CameraOcclusionPaint");
        if (shader != null) return shader;

        shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) { Debug.LogWarning("[CameraOcclusionShadowsOnly] Usando fallback URP/Unlit."); return shader; }

        Debug.LogError("[CameraOcclusionShadowsOnly] No se pudo cargar ningún shader.\n" +
                       "Añade 'Custom/CameraOcclusionPaint' a Edit > Project Settings > Graphics > Always Included Shaders.");
        return null;
    }

    // ─── API pública ──────────────────────────────────────────────────────────

    /// <summary>Llamado cada frame por vThirdPersonCamera con los puntos cámara → objetivo.</summary>
    public void Process(Vector3 from, Vector3 to)
    {
        float now = Time.time;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.0001f) return;

        if (!occlusionShader)
        {
            occlusionShader = LoadOcclusionShader();
            if (!occlusionShader) { _shaderMissing = true; return; }
            if (!occlusionShader.isSupported) { _shaderMissing = true; occlusionShader = null; return; }
        }
        if (_shaderMissing) return;

        if (debugRays) Debug.DrawLine(from, to, Color.magenta, 0f, false);

        dir /= dist;

        // Solo consideramos obstáculos que estén MÁS CERCA de la cámara que (dist - margen).
        // Evita que un edificio que el jugador está tocando desaparezca aunque no bloquee la vista.
        float maxOcclusionDist = dist - playerProximityMargin;
        if (maxOcclusionDist <= 0f)
        {
            UpdateEntries(now);
            return;
        }

        int hitCount = Physics.SphereCastNonAlloc(from, checkRadius, dir, _hitsBuffer, maxOcclusionDist, obstructionMask, QueryTriggerInteraction.Ignore);

        if (hitCount < _hitsBuffer.Length)
        {
            for (int i = 0; i < hitCount; i++)
                RegisterHit(_hitsBuffer[i].collider, now);
        }
        else
        {
            // El buffer se llenó (>= _hitsBuffer.Length impactos): SphereCastNonAlloc NO garantiza
            // que los impactos devueltos sean los más cercanos a la cámara ni que estén ordenados,
            // así que el obstáculo real que tapa al jugador puede quedar fuera del buffer y nunca
            // disolverse (visto en zonas con mucho attrezzo: columnas, rocas, vegetación con
            // collider). Repetimos con SphereCastAll — sí aloca, pero solo en este caso
            // excepcional — para no perder oclusiones reales.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[CameraOcclusionShadowsOnly] SphereCastNonAlloc llenó el buffer " +
                              $"({_hitsBuffer.Length} impactos) — puede haber objetos entre cámara y " +
                              "jugador sin disolver. Usando SphereCastAll de respaldo este frame.");
#endif
            var allHits = Physics.SphereCastAll(from, checkRadius, dir, maxOcclusionDist, obstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < allHits.Length; i++)
                RegisterHit(allHits[i].collider, now);
        }

        UpdateEntries(now);
    }

    private void RegisterHit(Collider collider, float now)
    {
        var renderer = collider.GetComponentInParent<Renderer>();
        if (!renderer) return;
        if (IsCharacter(renderer)) return; // jugador/NPCs nunca se disuelven aunque compartan capa con la geometría

        var entry = GetOrCreateEntry(renderer);
        entry.LastSeen = now;
    }

    private bool IsCharacter(Renderer renderer)
    {
        if (_characterCache.TryGetValue(renderer, out var cached)) return cached;

        bool isCharacter = renderer.transform.root.GetComponent("NPCSimpleAnimator") != null;
        _characterCache[renderer] = isCharacter;
        return isCharacter;
    }

    public void RestoreAll()
    {
        foreach (var entry in _active.Values)
            RestoreEntry(entry);
        _active.Clear();
    }

    // Setters para configurar desde vThirdPersonCamera o desde código externo
    public void SetMask(LayerMask m)                => obstructionMask = m;
    public void SetRadius(float r)                  => checkRadius = Mathf.Max(0f, r);
    public void SetReleaseDelay(float d)            => releaseDelay = Mathf.Max(0f, d);
    public void SetPlayerProximityMargin(float m)   => playerProximityMargin = Mathf.Max(0f, m);

    // ─── Lógica interna ───────────────────────────────────────────────────────

    void OnDisable() => RestoreAll();

    private void UpdateEntries(float now)
    {
        _toRestore.Clear();

        foreach (var kvp in _active)
        {
            var entry = kvp.Value;
            bool shouldOcclude = now - entry.LastSeen <= releaseDelay;
            float target = shouldOcclude ? 1f : 0f;
            float speed  = shouldOcclude ? fadeInSpeed : fadeOutSpeed;

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
            Renderer         = renderer,
            OriginalMaterials = renderer.sharedMaterials,
            PaintedMaterials  = BuildPaintedMaterials(renderer)
        };

        _active[renderer] = entry;
        return entry;
    }

    private Material[] BuildPaintedMaterials(Renderer renderer)
    {
        var originalMats = renderer.sharedMaterials;
        var painted      = new Material[originalMats.Length];

        for (int i = 0; i < originalMats.Length; i++)
        {
            var src = originalMats[i];
            var dst = new Material(occlusionShader);

            if (src)
            {
                if      (src.HasProperty("_BaseMap"))  dst.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                else if (src.HasProperty("_MainTex"))  dst.SetTexture("_BaseMap", src.GetTexture("_MainTex"));

                if      (src.HasProperty("_BaseColor")) dst.SetColor("_BaseColor", src.GetColor("_BaseColor"));
                else if (src.HasProperty("_Color"))     dst.SetColor("_BaseColor", src.GetColor("_Color"));
            }

            dst.SetFloat("_NoiseScale", noiseScale);
            dst.SetFloat("_EdgeWidth",  edgeWidth);
            dst.SetColor("_EdgeColor",  edgeColor);
            dst.SetFloat("_EdgeGlow",   edgeGlow);
            painted[i] = dst;
        }

        return painted;
    }

    private void ApplyMaterial(Entry entry, float progress)
    {
        if (!entry.Renderer) return;

        float reveal    = Mathf.Lerp(0f, revealAmount,  progress);
        float holdAlpha = Mathf.Lerp(1f, minimumAlpha, progress);

        entry.Renderer.sharedMaterials = progress > 0f ? entry.PaintedMaterials : entry.OriginalMaterials;

        if (progress > 0f)
        {
            for (int i = 0; i < entry.PaintedMaterials.Length; i++)
            {
                var m = entry.PaintedMaterials[i];
                if (!m) continue;
                m.SetFloat("_Reveal", reveal);
                var c = m.GetColor("_BaseColor");
                c.a = holdAlpha;
                m.SetColor("_BaseColor", c);
            }
        }
    }

    private void RestoreEntry(Entry entry)
    {
        if (entry.Renderer)
            entry.Renderer.sharedMaterials = entry.OriginalMaterials;
        entry.Progress = 0f;
    }
}
