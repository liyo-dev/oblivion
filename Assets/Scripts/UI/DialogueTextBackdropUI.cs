using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fondo difuminado para el área de texto del diálogo.
/// Genera un gradiente procedural: opaco en el centro, transparente en los bordes.
/// Colocar como hijo del panel de diálogo, entre DreamBackground y el texto.
/// Requiere un componente Image en el mismo GameObject.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class DialogueTextBackdropUI : MonoBehaviour
{
    [Tooltip("Color del difuminado. El alpha es la opacidad máxima en el centro.")]
    [SerializeField] Color _color = new Color(0.02f, 0.04f, 0.15f, 0.82f);

    [Tooltip("Fracción del ancho donde empieza el desvanecimiento lateral (0=sin fade lateral, 0.5=fade en la mitad exterior).")]
    [SerializeField, Range(0f, 0.8f)] float _softH = 0.22f;

    [Tooltip("Fracción del alto donde empieza el desvanecimiento vertical (0=sin fade vertical, 0.8=fade casi desde el centro).")]
    [SerializeField, Range(0f, 0.95f)] float _softV = 0.55f;

    Sprite _sprite;

    void Awake()
    {
        _sprite = BuildSprite(256, 64);
        var img = GetComponent<Image>();
        img.sprite         = _sprite;
        img.color          = _color;
        img.type           = Image.Type.Simple;
        img.preserveAspect = false;
        img.raycastTarget  = false;
    }

    void OnDestroy()
    {
        if (_sprite != null) Destroy(_sprite.texture);
    }

    // Gradiente: completamente opaco en el centro, desvanece suavemente en los bordes.
    // Horizontal: fade solo en los últimos _softH del ancho (mantiene máxima opacidad en el centro).
    // Vertical:   fade en los últimos _softV del alto (el texto flota sobre un lecho oscuro).
    Sprite BuildSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - cx) / cx; // 0 = centro, 1 = borde lateral
                float dy = Mathf.Abs(y - cy) / cy; // 0 = centro, 1 = borde vertical

                float ax = FadeEdge(dx, _softH);
                float ay = FadeEdge(dy, _softV);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, ax * ay));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f);
    }

    // Devuelve 1 cuando d < (1 - soft), y baja suavemente a 0 en el tramo final.
    static float FadeEdge(float d, float soft)
    {
        if (soft < 0.001f) return 1f;
        float threshold = 1f - soft;
        if (d <= threshold) return 1f;
        float t = (d - threshold) / soft;
        t = Mathf.Clamp01(t);
        t = t * t * (3f - 2f * t); // smoothstep
        return 1f - t;
    }
}
