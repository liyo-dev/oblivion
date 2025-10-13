using System.Collections;
using UnityEngine;

/// <summary>
/// Barrera del área de boss: transparente, con “scan” vertical y borde de energía.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class BossArenaBarrier : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color barrierColor = new Color(0.3f, 0.5f, 1f, 0.15f);
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField, Range(0f, 1f)] private float pulseIntensity = 0.3f;
    [SerializeField] private bool showOnAwake = false;

    [Header("Scan / Rim")]
    [SerializeField] private Color bandColor = Color.white;
    [SerializeField, Min(0.001f)] private float bandWidth = 0.35f;
    [SerializeField] private float bandSpeed = 1.5f; // unidades de altura por segundo
    [SerializeField, Range(0f, 3f)] private float rimIntensity = 0.6f;
    [SerializeField, Range(0.1f, 8f)] private float rimPower = 2.2f;

    [Header("Activación")]
    [SerializeField, Min(0f)] private float activationDuration = 0.5f;

    private MeshRenderer _renderer;
    private Material _material;
    private Color _baseColor;
    private bool _isActive;
    private float _currentAlpha;

    // Property IDs (evita strings repetidas)
    static readonly int BASECOLOR = Shader.PropertyToID("_BaseColor");
    static readonly int BANDCOLOR = Shader.PropertyToID("_BandColor");
    static readonly int BANDWIDTH = Shader.PropertyToID("_BandWidth");
    static readonly int BANDSPEED = Shader.PropertyToID("_BandSpeed");
    static readonly int RIMINT    = Shader.PropertyToID("_RimIntensity");
    static readonly int RIMPOW    = Shader.PropertyToID("_RimPower");
    static readonly int MINY      = Shader.PropertyToID("_MinY");
    static readonly int MAXY      = Shader.PropertyToID("_MaxY");
    static readonly int EMISSION  = Shader.PropertyToID("_EmissionColor"); // por si usas post/emisión

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        CreateBarrierMaterial();

        if (!showOnAwake)
        {
            _renderer.enabled = false;
        }
        else
        {
            Show();
        }
    }

    void Update()
    {
        if (!_isActive || !_material) return;

        // Pulso de alfa (respira)
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        float alpha = Mathf.Clamp01(_baseColor.a + pulse);
        var newColor = new Color(_baseColor.r, _baseColor.g, _baseColor.b, alpha);

        _material.SetColor(BASECOLOR, newColor);
        _material.SetColor(EMISSION, newColor * 0.5f); // opcional si usas Bloom
    }

    void LateUpdate()
    {
        // Si el objeto se mueve/escala, mantener el recorrido completo del scan
        if (_material && _renderer)
        {
            var b = _renderer.bounds;
            _material.SetFloat(MINY, b.min.y);
            _material.SetFloat(MAXY, b.max.y);
        }
    }

    private void CreateBarrierMaterial()
    {
        // 1) Shader custom con scan + fresnel
        var scanShader = Shader.Find("Liyo/BarrierScanURP");
        if (scanShader != null)
        {
            _material = new Material(scanShader);
            _renderer.material = _material;

            _baseColor = barrierColor;

            _material.SetColor(BASECOLOR, _baseColor);
            _material.SetColor(BANDCOLOR, bandColor);
            _material.SetFloat(BANDWIDTH, bandWidth);
            _material.SetFloat(BANDSPEED, bandSpeed);
            _material.SetFloat(RIMINT, rimIntensity);
            _material.SetFloat(RIMPOW, rimPower);

            // Cola transparente
            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Rango inicial del scan
            var b = _renderer.bounds;
            _material.SetFloat(MINY, b.min.y);
            _material.SetFloat(MAXY, b.max.y);

            // Renderer
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.allowOcclusionWhenDynamic = true;
            return;
        }

        // 2) Fallback URP/Simple Lit transparente si el shader no existe
        var urpSimpleLit = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (urpSimpleLit != null)
        {
            _material = new Material(urpSimpleLit);
            _renderer.material = _material;

            _material.SetFloat("_Surface", 1f); // Transparent
            _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _material.SetFloat("_Blend", 0f);   // Alpha
            _material.SetFloat("_AlphaClip", 0f);
            _material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
            _material.SetFloat("_ZWriteControl", 2f); // ForceDisable
            _material.SetFloat("_SpecularHighlights", 0f);
            _material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

            _baseColor = barrierColor;
            _material.SetColor("_BaseColor", _baseColor);
            _material.EnableKeyword("_EMISSION");
            _material.SetColor("_EmissionColor", _baseColor * 0.5f);
            _material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;

            _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            return;
        }

        // 3) Último recurso: Standard transparente (Built-in)
        _material = new Material(Shader.Find("Standard"));
        _renderer.material = _material;

        _material.SetFloat("_Mode", 3); // Transparent
        _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _material.SetInt("_ZWrite", 0);
        _material.DisableKeyword("_ALPHATEST_ON");
        _material.EnableKeyword("_ALPHABLEND_ON");
        _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _material.renderQueue = 3000;

        _baseColor = barrierColor;
        _material.SetColor("_Color", _baseColor);
        _material.EnableKeyword("_EMISSION");
        _material.SetColor("_EmissionColor", _baseColor * 0.5f);

        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    public void Show()
    {
        _isActive = true;
        _renderer.enabled = true;
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeIn()
    {
        float elapsed = 0f;
        _currentAlpha = 0f;

        while (elapsed < activationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / activationDuration);

            _currentAlpha = Mathf.Lerp(0f, barrierColor.a, t);
            var c = new Color(barrierColor.r, barrierColor.g, barrierColor.b, _currentAlpha);
            _baseColor = c;

            if (_material) _material.SetColor(BASECOLOR, c);
            yield return null;
        }
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float startAlpha = _currentAlpha;

        while (elapsed < activationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / activationDuration);

            _currentAlpha = Mathf.Lerp(startAlpha, 0f, t);
            var c = new Color(barrierColor.r, barrierColor.g, barrierColor.b, _currentAlpha);
            _baseColor = c;

            if (_material) _material.SetColor(BASECOLOR, c);
            yield return null;
        }

        _isActive = false;
        _renderer.enabled = false;
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
