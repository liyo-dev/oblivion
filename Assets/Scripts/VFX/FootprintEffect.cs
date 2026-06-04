using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Huella de luz circular con bordes suaves que se desvanece. Gestionada por StarWorldFootprintPool.
/// El prefab necesita un MeshRenderer con material URP Lit, Surface=Transparent, Emission habilitada.
/// La textura circular se genera por código — no hace falta ningún asset externo.
/// </summary>
public class FootprintEffect : MonoBehaviour
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private float _duration = 1.5f;
    [Tooltip("Multiplicador de emisión. 0 = sin brillo, 1 = color plano, 2 = muy brillante.")]
    [Range(0f, 3f)]
    [SerializeField] private float _emissionMultiplier = 1f;

    private static readonly int _baseColorId    = Shader.PropertyToID("_BaseColor");
    private static readonly int _emissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int _baseMapId       = Shader.PropertyToID("_BaseMap");

    // Textura compartida entre todas las instancias del pool
    private static Texture2D _sharedCircleTexture;

    private MaterialPropertyBlock _mpb;
    private Coroutine _fadeCoroutine;
    private Action<FootprintEffect> _returnToPool;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        if (_sharedCircleTexture == null)
            _sharedCircleTexture = CreateSoftCircleTexture(64);
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
        Color emission = color * _emissionMultiplier;
        float elapsed = 0f;

        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - elapsed / _duration;

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetTexture(_baseMapId, _sharedCircleTexture);
            _mpb.SetColor(_baseColorId, new Color(color.r, color.g, color.b, t));
            _mpb.SetColor(_emissionColorId, emission * t);
            _renderer.SetPropertyBlock(_mpb);

            yield return null;
        }

        _fadeCoroutine = null;
        gameObject.SetActive(false);
        _returnToPool?.Invoke(this);
    }

    // Gradiente radial: blanco opaco en el centro, transparente en el borde
    private static Texture2D CreateSoftCircleTexture(int resolution)
    {
        var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        float center = (resolution - 1) * 0.5f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float t = 1f - Mathf.Clamp01(dist / center);
                // Suavizado tipo smoothstep para bordes más difuminados
                t = t * t * (3f - 2f * t);
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
        if (_sharedCircleTexture != null)
        {
            Destroy(_sharedCircleTexture);
            _sharedCircleTexture = null;
        }
    }
#endif
}
