using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Huella de luz en forma de estrella de 5 puntas con bordes suaves que se desvanece. Gestionada por StarWorldFootprintPool.
/// El prefab necesita un MeshRenderer con material URP Lit, Surface=Transparent, Emission habilitada.
/// La textura se genera por código — no hace falta ningún asset externo.
/// </summary>
public class FootprintEffect : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private float _duration = 1.5f;
    [Tooltip("Multiplicador de emisión. 0 = sin brillo, 1 = color plano, 2 = muy brillante.")]
    [Range(0f, 3f)]
    [SerializeField] private float _emissionMultiplier = 1f;

    private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int _baseMapId   = Shader.PropertyToID("_BaseMap");

    // Textura compartida entre todas las instancias del pool
    private static Texture2D _sharedStarTexture;

    private MaterialPropertyBlock _mpb;
    private Coroutine _fadeCoroutine;
    private Action<FootprintEffect> _returnToPool;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        // Forzar material transparente en URP por código, sin depender de la configuración del prefab
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f);           // Surface Type = Transparent
        mat.SetFloat("_Blend", 0f);             // Blend Mode = Alpha
        mat.SetFloat("_SrcBlend", 5f);          // SrcAlpha
        mat.SetFloat("_DstBlend", 10f);         // OneMinusSrcAlpha
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        _renderer.material = mat;

        if (_sharedStarTexture == null)
            _sharedStarTexture = CreateSoftStarTexture(128);
    }

    public void Activate(Vector3 position, float scale, Color color, Action<FootprintEffect> returnToPool)
    {
        transform.position = position;
        transform.localScale = new Vector3(scale, 1f, scale);
        _returnToPool = returnToPool;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(color));
    }

    private IEnumerator FadeRoutine(Color color)
    {
        // _emissionMultiplier aumenta el brillo del color en URP Unlit (HDR)
        float r = color.r * _emissionMultiplier;
        float g = color.g * _emissionMultiplier;
        float b = color.b * _emissionMultiplier;
        float elapsed = 0f;

        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - elapsed / _duration;

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(_baseMapId, _sharedStarTexture);
            _mpb.SetColor(_baseColorId, new Color(r, g, b, t));
            _renderer.SetPropertyBlock(_mpb);

            yield return null;
        }

        _fadeCoroutine = null;
        gameObject.SetActive(false);
        _returnToPool?.Invoke(this);
    }

    // Estrella de 5 puntas: brillo máximo en el centro, desvanece hacia los bordes de cada punta
    private static Texture2D CreateSoftStarTexture(int resolution)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        float center = (resolution - 1) * 0.5f;

        const int numPoints = 5;
        const float innerRatio = 0.42f; // radio interior / radio exterior
        float outerRadius = center * 0.95f;
        float innerRadius = outerRadius * innerRatio;
        // Cada estrella de N puntas tiene 2N sectores de ángulo π/N
        float sectorAngle = Mathf.PI / numPoints;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Ángulo en coordenadas polares, rotado para que una punta apunte hacia arriba
                float angle = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;
                if (angle < 0f) angle += Mathf.PI * 2f;

                // Sector y fracción dentro del sector
                float sectorIndex = angle / sectorAngle;
                int sector = Mathf.FloorToInt(sectorIndex);
                float frac = sectorIndex - sector;

                // Sectores pares: punta exterior → valle interior; impares: valle → punta
                float starRadius = (sector % 2 == 0)
                    ? Mathf.Lerp(outerRadius, innerRadius, frac)
                    : Mathf.Lerp(innerRadius, outerRadius, frac);

                // Normalizar distancia respecto al borde de la estrella en este ángulo
                float t = 1f - Mathf.Clamp01(dist / starRadius);
                t = t * t * (3f - 2f * t); // smoothstep
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, t));
            }
        }

        tex.Apply();
        return tex;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        if (_sharedStarTexture != null)
        {
            Destroy(_sharedStarTexture);
            _sharedStarTexture = null;
        }
    }
#endif
}
