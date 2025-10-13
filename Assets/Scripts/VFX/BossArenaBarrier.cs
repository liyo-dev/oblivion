using UnityEngine;

/// <summary>
/// Efecto visual para la barrera del área de boss.
/// Se activa automáticamente y muestra un efecto de energía en los bordes.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class BossArenaBarrier : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color barrierColor = new Color(0.3f, 0.5f, 1f, 0.15f); // Azul semitransparente con menos alfa
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;
    [SerializeField] private bool showOnAwake = false;

    [Header("Efecto de activación")]
    [SerializeField] private float activationDuration = 0.5f;
    
    private MeshRenderer _renderer;
    private Material _material;
    private Color _baseColor;
    private bool _isActive = false;
    private float _currentAlpha = 0f;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        
        // Crear material con shader transparente
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

        // Efecto de pulso
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        float alpha = _baseColor.a + pulse;
        
        Color newColor = _baseColor;
        newColor.a = Mathf.Clamp01(alpha);
        _material.SetColor("_Color", newColor);
        
        // Efecto de emisión
        _material.SetColor("_EmissionColor", newColor * 0.5f);
    }

    private void CreateBarrierMaterial()
    {
        // Crear material con shader Standard (Transparent)
        _material = new Material(Shader.Find("Standard"));
        _material.SetFloat("_Mode", 3); // Transparent mode
        _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _material.SetInt("_ZWrite", 0);
        _material.DisableKeyword("_ALPHATEST_ON");
        _material.EnableKeyword("_ALPHABLEND_ON");
        _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _material.renderQueue = 3000;
        
        // Configurar colores
        _baseColor = barrierColor;
        _material.SetColor("_Color", _baseColor);
        
        // Añadir emisión para el efecto brillante
        _material.EnableKeyword("_EMISSION");
        _material.SetColor("_EmissionColor", _baseColor * 0.5f);
        
        // Aplicar material
        _renderer.material = _material;
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

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        Color startColor = _baseColor;
        startColor.a = 0f;

        while (elapsed < activationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / activationDuration;
            
            _currentAlpha = Mathf.Lerp(0f, _baseColor.a, t);
            Color newColor = _baseColor;
            newColor.a = _currentAlpha;
            
            if (_material)
            {
                _material.SetColor("_Color", newColor);
            }
            
            yield return null;
        }

        _currentAlpha = _baseColor.a;
    }

    private System.Collections.IEnumerator FadeOut()
    {
        float elapsed = 0f;
        float startAlpha = _currentAlpha;

        while (elapsed < activationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / activationDuration;
            
            _currentAlpha = Mathf.Lerp(startAlpha, 0f, t);
            Color newColor = _baseColor;
            newColor.a = _currentAlpha;
            
            if (_material)
            {
                _material.SetColor("_Color", newColor);
            }
            
            yield return null;
        }

        _isActive = false;
        _renderer.enabled = false;
    }

    void OnDestroy()
    {
        // Limpiar material creado
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}
