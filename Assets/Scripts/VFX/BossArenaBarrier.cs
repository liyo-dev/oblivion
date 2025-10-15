using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class BossArenaBarrier : MonoBehaviour
{
    MeshRenderer _r;
    Material _sharedMat;                 
    MaterialPropertyBlock _mpb;

    // Parámetros del efecto
    Color  _baseColor      = new(0.3f, 0.5f, 1f, 0.25f);
    float  _pulseSpeed     = 2f;
    float  _pulseIntensity = 0.3f;
    float  _rimPower       = 2f;
    float  _rimStrength    = 1.5f;
    Vector2 _noiseTiling   = new(2, 2);
    Vector2 _noiseSpeed    = new(0.2f, 0f);
    float  _noiseStrength  = 1f;
    float  _alpha          = 1f;
    bool   _show;

    // IDs shader
    static readonly int ID_BaseColor      = Shader.PropertyToID("_BaseColor");
    static readonly int ID_Color          = Shader.PropertyToID("_Color"); 
    static readonly int ID_Alpha          = Shader.PropertyToID("_Alpha");
    static readonly int ID_PulseSpeed     = Shader.PropertyToID("_PulseSpeed");
    static readonly int ID_PulseIntensity = Shader.PropertyToID("_PulseIntensity");
    static readonly int ID_RimPower       = Shader.PropertyToID("_RimPower");
    static readonly int ID_RimStrength    = Shader.PropertyToID("_RimStrength");
    static readonly int ID_NoiseTiling    = Shader.PropertyToID("_NoiseTiling");
    static readonly int ID_NoiseSpeed     = Shader.PropertyToID("_NoiseSpeed");
    static readonly int ID_NoiseStrength  = Shader.PropertyToID("_NoiseStrength");
    // >>> añadidos para la banda
    static readonly int ID_MinY           = Shader.PropertyToID("_MinY");
    static readonly int ID_MaxY           = Shader.PropertyToID("_MaxY");

    public void Setup(
        Material sourceMat,
        Color color,
        float pulseSpeed,
        float pulseIntensity,
        float rimPower = 2f,
        float rimStrength = 1.5f,
        Vector2? noiseTiling = null,
        Vector2? noiseSpeed  = null,
        float noiseStrength  = 1f,
        float alpha          = 1f
    )
    {
        // 1) Renderer
        if (!_r && !TryGetComponent(out _r))
        {
            Debug.LogError("[BossArenaBarrier] No hay MeshRenderer en " + name);
            enabled = false;
            return;
        }

        // 2) Material asset
        if (!sourceMat)
        {
            Debug.LogError("[BossArenaBarrier] Falta barrierMaterial (asigna el .mat en BossArenaController).");
            enabled = false;
            return;
        }

        _sharedMat = sourceMat;
        // Asignamos el asset compartido; no instanciamos para ahorrar memoria
        _r.sharedMaterial = _sharedMat;
        if (_r.sharedMaterial != null)
            _r.sharedMaterial.renderQueue = Mathf.Max(_r.sharedMaterial.renderQueue, 3000);

        // 3) Parametría local
        _baseColor      = color;
        _pulseSpeed     = pulseSpeed;
        _pulseIntensity = pulseIntensity;
        _rimPower       = rimPower;
        _rimStrength    = rimStrength;
        _noiseTiling    = noiseTiling ?? new Vector2(2, 2);
        _noiseSpeed     = noiseSpeed  ?? new Vector2(0.2f, 0f);
        _noiseStrength  = noiseStrength;
        _alpha          = Mathf.Clamp01(alpha);

        // 4) PropertyBlock listo
        _mpb ??= new MaterialPropertyBlock();
        _r.GetPropertyBlock(_mpb);

        // 5) Subimos MinY/MaxY con los bounds actuales del muro (ya tiene escala/pos)
        var b = _r.bounds; // en mundo
        _mpb.SetFloat(ID_MinY, b.min.y);
        _mpb.SetFloat(ID_MaxY, b.max.y);

        // 6) Propiedades estáticas del shader
        if (_r.sharedMaterial && _r.sharedMaterial.HasProperty(ID_BaseColor))
            _mpb.SetColor(ID_BaseColor, _baseColor);
        else
            _mpb.SetColor(ID_Color, _baseColor);

        _mpb.SetFloat(ID_PulseSpeed,     _pulseSpeed);
        _mpb.SetFloat(ID_PulseIntensity, _pulseIntensity);
        _mpb.SetFloat(ID_RimPower,       _rimPower);
        _mpb.SetFloat(ID_RimStrength,    _rimStrength);
        _mpb.SetVector(ID_NoiseTiling,   new Vector4(_noiseTiling.x, _noiseTiling.y, 0, 0));
        _mpb.SetVector(ID_NoiseSpeed,    new Vector4(_noiseSpeed.x,  _noiseSpeed.y,  0, 0));
        _mpb.SetFloat(ID_NoiseStrength,  _noiseStrength);
        _mpb.SetFloat(ID_Alpha,          0f); // empieza oculto

        _r.SetPropertyBlock(_mpb);

        // 7) Estado visible/oculto
        _show = false;
        _r.enabled = false;
    }

    public void Show()
    {
        if (!_r) return;
        _show = true;
        _r.enabled = true;
        _r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(ID_Alpha, _alpha);
        _r.SetPropertyBlock(_mpb);
    }

    public void Hide()
    {
        if (!_r) return;
        _show = false;
        _r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(ID_Alpha, 0f);
        _r.SetPropertyBlock(_mpb);
        _r.enabled = false;
    }
}
