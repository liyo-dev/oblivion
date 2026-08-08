using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Overlay de chispas 2D generadas de forma procedural (sin texturas externas).
/// Debe ser hijo del Canvas de DramaticTextOverlayUI, entre el fondo y el texto.
/// Referenciarlo en DramaticTextOverlayUI._dreamSparkles.
/// </summary>
[DisallowMultipleComponent]
public class DreamSparkleOverlay : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField, Range(5, 30)] int _poolSize = 18;

    [Header("Ritmo")]
    [SerializeField] float _spawnMin  = 0.12f;
    [SerializeField] float _spawnMax  = 0.45f;

    [Header("Vida")]
    [SerializeField] float _lifetimeMin = 1.6f;
    [SerializeField] float _lifetimeMax = 3.8f;

    [Header("Movimiento")]
    [Tooltip("Píxeles que asciende cada chispa durante su vida.")]
    [SerializeField] float _driftY     = 85f;
    [Tooltip("Rango horizontal de desviación aleatoria (píxeles).")]
    [SerializeField] float _driftXMax  = 22f;

    [Header("Tamaño")]
    [SerializeField] float _sizeMin = 5f;
    [SerializeField] float _sizeMax = 20f;

    [Header("Color")]
    [SerializeField] Color _colorA = new Color(1f,   0.95f, 0.70f, 0.88f); // dorado cálido
    [SerializeField] Color _colorB = new Color(0.55f, 0.82f, 1f,   0.88f); // azul frío

    // Sprites procedurales compartidos por todo el pool
    Sprite _glowSprite;
    Sprite _starSprite;

    RectTransform _rect;
    Image[]       _pool;
    Coroutine     _spawnLoop;
    bool          _running;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _rect        = GetComponent<RectTransform>();
        _glowSprite  = BuildGlowSprite(64);
        _starSprite  = BuildStarSprite(64);

        _pool = new Image[_poolSize];
        for (int i = 0; i < _poolSize; i++)
        {
            var go = new GameObject($"Sp_{i}") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = Vector2.one * 14f;

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = Color.clear;
            go.SetActive(false);
            _pool[i] = img;
        }

        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_glowSprite != null) Destroy(_glowSprite.texture);
        if (_starSprite != null) Destroy(_starSprite.texture);
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reconfigura el ritmo/tamaño del pool ANTES de que se ejecute <see cref="Awake"/> — hay que llamarlo
    /// justo después de <c>AddComponent&lt;DreamSparkleOverlay&gt;()</c> con el GameObject aún inactivo
    /// (si no, Awake ya habrá construido el pool con los valores del Inspector). Pensado para reutilizar
    /// este mismo generador con una cadencia más suave en overlays de ambiente permanentes de UI
    /// (ver <c>ProceduralAmbientSparkles</c>), sin duplicar el código de sprites/pool.
    /// </summary>
    public void ConfigurePace(int poolSize, float spawnMin, float spawnMax)
    {
        _poolSize = Mathf.Clamp(poolSize, 5, 30);
        _spawnMin = spawnMin;
        _spawnMax = spawnMax;
    }

    public void StartSparkles()
    {
        gameObject.SetActive(true);
        _running = true;
        if (_spawnLoop != null) StopCoroutine(_spawnLoop);
        _spawnLoop = StartCoroutine(Co_SpawnLoop());
    }

    public void StopSparkles()
    {
        _running = false;
        if (_spawnLoop != null)
        {
            StopCoroutine(_spawnLoop);
            _spawnLoop = null;
        }
        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].gameObject.activeSelf) continue;
            DOTween.Kill(_pool[i]);
            DOTween.Kill((RectTransform)_pool[i].transform);
            _pool[i].gameObject.SetActive(false);
        }
        gameObject.SetActive(false);
    }

    // ── Bucle de spawn ────────────────────────────────────────────────────────

    IEnumerator Co_SpawnLoop()
    {
        while (_running)
        {
            SpawnOne();
            yield return new WaitForSecondsRealtime(Random.Range(_spawnMin, _spawnMax));
        }
    }

    void SpawnOne()
    {
        Image sparkle = null;
        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].gameObject.activeSelf) { sparkle = _pool[i]; break; }
        }
        if (sparkle == null) return;

        Vector2 half     = _rect.rect.size * 0.5f;
        Vector2 pos      = new Vector2(
            Random.Range(-half.x * 0.88f,  half.x * 0.88f),
            Random.Range(-half.y * 0.42f,  half.y * 0.33f)
        );

        float sz       = Random.Range(_sizeMin, _sizeMax);
        float lifetime = Random.Range(_lifetimeMin, _lifetimeMax);
        float fadeIn   = lifetime * 0.28f;
        float fadeOut  = lifetime * 0.32f;
        Color col      = Color.Lerp(_colorA, _colorB, Random.value);

        // Alternar forma: 70% círculo suave (estrella lejana), 30% estrella de 4 puntas
        sparkle.sprite = (Random.value < 0.70f) ? _glowSprite : _starSprite;

        var rt = (RectTransform)sparkle.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = Vector2.one * sz;
        rt.localRotation    = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        sparkle.color       = Color.clear;
        sparkle.gameObject.SetActive(true);

        // Fade in
        sparkle.DOColor(col, fadeIn).SetUpdate(true);

        // Deriva: sube y se desvía levemente
        Vector2 dest = pos + new Vector2(Random.Range(-_driftXMax, _driftXMax), _driftY);
        rt.DOAnchorPos(dest, lifetime).SetEase(Ease.OutSine).SetUpdate(true);

        // Rotación suave
        rt.DOLocalRotate(
            new Vector3(0f, 0f, rt.localEulerAngles.z + Random.Range(-50f, 50f)),
            lifetime, RotateMode.FastBeyond360
        ).SetEase(Ease.InOutSine).SetUpdate(true);

        // Fade out al final
        Image captured = sparkle;
        DOTween.Sequence()
            .SetUpdate(true)
            .AppendInterval(lifetime - fadeOut)
            .Append(captured.DOColor(Color.clear, fadeOut).SetUpdate(true))
            .OnComplete(() => { if (captured != null) captured.gameObject.SetActive(false); });
    }

    // ── Generación procedural de sprites ──────────────────────────────────────

    // Círculo con falloff suave (glow de estrella lejana).
    static Sprite BuildGlowSprite(int size)
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
                float t = 1f - Mathf.Clamp01(d);
                float a = t * t * (3f - 2f * t); // smoothstep → borde suave
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }

    // Estrella de 4 puntas con brillo central suave.
    static Sprite BuildStarSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x - c) / c;
                float ny = (y - c) / c;
                float dist  = Mathf.Sqrt(nx * nx + ny * ny);
                float angle = Mathf.Atan2(ny, nx);

                // Radio de la estrella: picos cada 90° (cos de 2·angle da 4 picos)
                float starR  = 0.28f + 0.72f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * angle)), 5f);
                float inside = Mathf.Clamp01(1f - dist / starR);
                float a      = inside * inside;

                // Añadir un halo suave alrededor de la estrella
                float glow = Mathf.Clamp01(1f - dist * 1.4f);
                glow = glow * glow * 0.35f;
                a = Mathf.Clamp01(a + glow);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
    }
}
