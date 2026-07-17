using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Fondo nebulosa procedural para secuencias de sueño.
/// Crea blobs suaves de colores (azul/púrpura/teal) que pulsan y flotan.
/// Debe ser hijo del Canvas de DramaticTextOverlayUI, encima del fondo sólido y debajo de las chispas.
/// Referenciarlo en DramaticTextOverlayUI._dreamBackground.
/// </summary>
[DisallowMultipleComponent]
public class DreamBackgroundController : MonoBehaviour
{
    [Header("Nebulosa")]
    [SerializeField, Range(3, 7)] int _blobCount = 5;
    [SerializeField] float _sizeMin = 200f;
    [SerializeField] float _sizeMax = 560f;
    [SerializeField] float _alphaMin = 0.20f;
    [SerializeField] float _alphaMax = 0.40f;
    [Tooltip("Velocidad del ciclo de pulso (segundos por semiciclo).")]
    [SerializeField] float _pulseSpeedMin = 3.5f;
    [SerializeField] float _pulseSpeedMax = 8.0f;
    [Tooltip("Rango de drift (fracción del tamaño del canvas).")]
    [SerializeField] float _driftFraction = 0.35f;
    [SerializeField] float _driftSpeedMin = 5f;
    [SerializeField] float _driftSpeedMax = 13f;

    static readonly Color[] _palette =
    {
        new Color(0.08f, 0.02f, 0.42f, 0f),  // violeta profundo
        new Color(0.00f, 0.07f, 0.48f, 0f),  // azul marino
        new Color(0.20f, 0.00f, 0.42f, 0f),  // púrpura
        new Color(0.00f, 0.18f, 0.42f, 0f),  // teal oscuro
        new Color(0.30f, 0.00f, 0.35f, 0f),  // magenta oscuro
    };

    Sprite    _blobSprite;
    Image[]   _blobs;
    RectTransform _rect;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _rect      = GetComponent<RectTransform>();
        _blobSprite = BuildBlobSprite(256);
        _blobs      = new Image[_blobCount];

        for (int i = 0; i < _blobCount; i++)
        {
            var go = new GameObject($"Blob_{i}") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.sprite       = _blobSprite;
            img.raycastTarget = false;
            img.color        = Color.clear;
            img.material     = null;

            go.SetActive(false);
            _blobs[i] = img;
        }

        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_blobSprite != null) Destroy(_blobSprite.texture);
    }

    // ── API pública ───────────────────────────────────────────────────────────

    public void StartDream()
    {
        gameObject.SetActive(true);
        Vector2 half = _rect.rect.size * 0.5f;

        for (int i = 0; i < _blobCount; i++)
        {
            var img = _blobs[i];
            var rt  = (RectTransform)img.transform;
            img.gameObject.SetActive(true);

            float sz    = Random.Range(_sizeMin, _sizeMax);
            rt.sizeDelta = Vector2.one * sz;

            Vector2 startPos = new Vector2(
                Random.Range(-half.x * 0.85f, half.x * 0.85f),
                Random.Range(-half.y * 0.85f, half.y * 0.85f)
            );
            rt.anchoredPosition = startPos;

            Color dark = _palette[i % _palette.Length];
            Color peak = dark;
            peak.a = Random.Range(_alphaMin, _alphaMax);

            img.color = dark;

            // Pulso: fade in al pico y ciclo Yoyo infinito
            float pulseSpeed = Random.Range(_pulseSpeedMin, _pulseSpeedMax);
            float startDelay  = Random.Range(0f, pulseSpeed);

            img.DOColor(peak, pulseSpeed)
               .SetDelay(startDelay)
               .SetEase(Ease.InOutSine)
               .SetLoops(-1, LoopType.Yoyo)
               .SetUpdate(true);

            // Drift: movimiento suave entre dos posiciones aleatorias
            Vector2 destPos = new Vector2(
                Random.Range(-half.x * _driftFraction, half.x * _driftFraction),
                Random.Range(-half.y * _driftFraction, half.y * _driftFraction)
            );
            float driftSpeed = Random.Range(_driftSpeedMin, _driftSpeedMax);

            rt.DOAnchorPos(destPos, driftSpeed)
              .SetEase(Ease.InOutSine)
              .SetLoops(-1, LoopType.Yoyo)
              .SetUpdate(true);
        }
    }

    public void StopDream()
    {
        for (int i = 0; i < _blobs.Length; i++)
        {
            DOTween.Kill(_blobs[i]);
            DOTween.Kill((RectTransform)_blobs[i].transform);
            _blobs[i].gameObject.SetActive(false);
        }
        gameObject.SetActive(false);
    }

    // ── Sprite procedural ─────────────────────────────────────────────────────

    // Gaussiana suave: más difusa que el glow de las chispas, ideal para nubes/nebulosas.
    static Sprite BuildBlobSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Exp(-d * d * 2.8f); // falloff gaussiano
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }
}
