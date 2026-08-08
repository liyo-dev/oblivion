using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kit de arte procedural compartido para la UI "moderna" del juego (HUD, menús, inventario, tienda...).
/// Sigue exactamente el mismo patrón ya usado en <c>DreamBackgroundController</c>, <c>DreamSparkleOverlay</c>
/// e <c>InputGlyphCanvas</c>: se generan texturas en memoria (compositing por capas con alpha-blend) y se
/// convierten en <see cref="Sprite"/> normales. No depende de ningún archivo de imagen externo.
///
/// Objetivo de estilo: el mismo "onírico/pintado" de las secuencias de sueño (paneles suaves, brillo cálido,
/// acentos tipo chispa) para que HUD, menús, inventario y tienda compartan una única identidad visual.
/// Ver <see cref="Palette"/> para los colores base — son los MISMOS que usa DreamBackgroundController /
/// DreamSparkleOverlay, a propósito, para que todo el juego tire de la misma paleta.
///
/// Rendimiento: los sprites se cachean por parámetros (mismo tamaño/colores → mismo Sprite reutilizado).
/// Esto importa en cuanto esto se use en listas con muchas entradas iguales (inventario, tienda, misiones):
/// sin caché, cada fila/celda generaría su propia textura en su <c>Awake</c> aunque el resultado sea
/// pixel-por-pixel idéntico al de la fila de al lado. Con caché, el coste de generar la textura se paga
/// una sola vez por combinación de parámetros, no una vez por instancia visible en pantalla.
/// </summary>
public static class ProceduralUIKit
{
    static readonly Dictionary<(int, float, float, float, Color, Color, Color, bool), Sprite> _panelCache = new();
    static readonly Dictionary<(int, float, float, Color, Color), Sprite> _ringCache = new();
    static readonly Dictionary<(int, float), Sprite> _glowCache = new();
    static readonly Dictionary<(int, float, Color, Color, bool), Sprite> _circleCache = new();
    static readonly Dictionary<(Color, bool), Sprite> _fillCache = new();

    // Los sprites cacheados aquí viven mientras dure el dominio de C# (no se destruyen desde los
    // componentes que los consumen — ver ProceduralPanelSkin/ProceduralSlotFrameSkin). Sin este reset,
    // tras un domain reload desactivado (Enter Play Mode Options) la caché podría quedar apuntando a
    // texturas ya liberadas entre sesiones de PlayMode en el editor. Mismo patrón que exige CLAUDE.md
    // para cualquier estático con estado.
#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _panelCache.Clear();
        _ringCache.Clear();
        _glowCache.Clear();
        _circleCache.Clear();
        _fillCache.Clear();
    }
#endif

    // ── Paleta compartida (idéntica a la de las secuencias de sueño) ────────────
    public static class Palette
    {
        public static readonly Color DeepViolet = new Color(0.08f, 0.02f, 0.42f, 1f);
        public static readonly Color NavyBlue   = new Color(0.00f, 0.07f, 0.48f, 1f);
        public static readonly Color Purple     = new Color(0.20f, 0.00f, 0.42f, 1f);
        public static readonly Color DarkTeal   = new Color(0.00f, 0.18f, 0.42f, 1f);
        public static readonly Color Magenta    = new Color(0.30f, 0.00f, 0.35f, 1f);

        public static readonly Color WarmGold = new Color(1f,    0.95f, 0.70f, 1f);
        public static readonly Color CoolBlue = new Color(0.55f, 0.82f, 1f,   1f);

        /// <summary>Fondo de panel por defecto: azul noche translúcido, igual de familia que el backdrop de diálogo.</summary>
        public static readonly Color PanelFill   = new Color(0.05f, 0.06f, 0.16f, 0.86f);
        /// <summary>Borde por defecto: dorado cálido atenuado, mismo tono que las chispas.</summary>
        public static readonly Color PanelBorder = new Color(0.86f, 0.74f, 0.42f, 0.95f);
        /// <summary>Halo exterior por defecto: azul frío, mismo tono que las chispas.</summary>
        public static readonly Color PanelGlow   = new Color(0.55f, 0.82f, 1f, 0.35f);
    }

    // ── Compositing (alpha-blend "source over", igual que InputGlyphCanvas.Blend) ──

    static void Blend(Color[] pixels, int size, int x, int y, Color src)
    {
        if ((uint)x >= (uint)size || (uint)y >= (uint)size || src.a <= 0f) return;
        int i = y * size + x;
        Color dst = pixels[i];
        float outA = src.a + dst.a * (1f - src.a);
        if (outA <= 0.0001f) { pixels[i] = default; return; }
        Color rgb = src * src.a + dst * (dst.a * (1f - src.a));
        pixels[i] = new Color(rgb.r / outA, rgb.g / outA, rgb.b / outA, outA);
    }

    // Distancia con signo a un rectángulo redondeado centrado en (0,0): negativo dentro, positivo fuera.
    static float RoundedRectSdf(float px, float py, float halfW, float halfH, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(halfW, halfH));
        float dx = Mathf.Abs(px) - (halfW - radius);
        float dy = Mathf.Abs(py) - (halfH - radius);
        float ox = Mathf.Max(dx, 0f);
        float oy = Mathf.Max(dy, 0f);
        float outside = Mathf.Sqrt(ox * ox + oy * oy) - radius;
        float inside  = Mathf.Min(Mathf.Max(dx, dy), 0f);
        return outside + inside;
    }

    /// <summary>
    /// Panel base de la UI: relleno + borde + halo exterior suave, con esquinas redondeadas.
    /// Devuelve un Sprite listo para usar con <c>Image.type = Image.Type.Sliced</c> (9-slice), así que
    /// se puede estirar a cualquier tamaño de panel sin deformar ni las esquinas ni el borde.
    /// </summary>
    public static Sprite BuildPanelSprite(
        int size = 128,
        float cornerRadius = 26f,
        float borderThickness = 4f,
        float glowRange = 14f,
        Color? fill = null,
        Color? border = null,
        Color? glow = null,
        bool rimHighlight = true)
    {
        Color fillColor   = fill   ?? Palette.PanelFill;
        Color borderColor = border ?? Palette.PanelBorder;
        Color glowColor   = glow   ?? Palette.PanelGlow;

        var key = (size, cornerRadius, borderThickness, glowRange, fillColor, borderColor, glowColor, rimHighlight);
        if (_panelCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

        var pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            float py = (y + 0.5f) - half;
            for (int x = 0; x < size; x++)
            {
                float px = (x + 0.5f) - half;
                float d = RoundedRectSdf(px, py, half, half, cornerRadius);

                // 1) Halo exterior: se apaga exponencialmente más allá del borde del panel.
                if (d > 0f)
                {
                    float g = Mathf.Clamp01(1f - d / glowRange);
                    g = g * g;
                    if (g > 0f) Blend(pixels, size, x, y, new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * g));
                    continue;
                }

                // 2) Silueta exterior del panel (borde), con anti-aliasing en d≈0.
                float covBorder = Mathf.Clamp01(0.5f - d);
                Blend(pixels, size, x, y, new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * covBorder));

                // 3) Relleno interior, inset por el grosor del borde, con anti-aliasing en d≈-borderThickness.
                float dInner = d + borderThickness;
                float covFill = Mathf.Clamp01(0.5f - dInner);
                if (covFill > 0f)
                {
                    Color fc = fillColor;
                    if (rimHighlight)
                    {
                        // Brillo tenue cerca del borde interior superior — sensación de "pintado"/pulido,
                        // acorde al toon pintado a mano del resto del arte (Quibli).
                        float topFade = Mathf.Clamp01((py + half) / size); // 0 arriba, 1 abajo
                        float rim = Mathf.Clamp01(1f - Mathf.Abs(dInner) / (cornerRadius * 0.6f)) * (1f - topFade);
                        fc = Color.Lerp(fillColor, Color.white, rim * 0.10f);
                    }
                    Blend(pixels, size, x, y, new Color(fc.r, fc.g, fc.b, fc.a * covFill));
                }
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name = "ProceduralPanelTex"
        };
        tex.SetPixels(pixels);
        tex.Apply();

        float m = cornerRadius + borderThickness + glowRange * 0.4f;
        var border9 = new Vector4(m, m, m, m);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, border9);
        sprite.name = "ProceduralPanel";
        _panelCache[key] = sprite;
        return sprite;
    }

    /// <summary>Anillo brillante (para marcos de slots de habilidad/magia, iconos circulares, avatares...).</summary>
    public static Sprite BuildRingFrameSprite(
        int size = 96,
        float thickness = 5f,
        float glowRange = 8f,
        Color? ringColor = null,
        Color? glowColor = null)
    {
        Color ring = ringColor ?? Palette.PanelBorder;
        Color glow = glowColor ?? Palette.PanelGlow;

        var key = (size, thickness, glowRange, ring, glow);
        if (_ringCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

        var pixels = new Color[size * size];
        float c = size * 0.5f;
        float radius = c - thickness - glowRange * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) - c;
                float dy = (y + 0.5f) - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius; // <0 dentro del anillo grueso, hacia el centro

                float distToRing = Mathf.Abs(d) - thickness * 0.5f;
                float covRing = Mathf.Clamp01(0.5f - distToRing);
                if (covRing > 0f) Blend(pixels, size, x, y, new Color(ring.r, ring.g, ring.b, ring.a * covRing));

                if (distToRing > 0f)
                {
                    float g = Mathf.Clamp01(1f - distToRing / glowRange);
                    g = g * g;
                    if (g > 0f) Blend(pixels, size, x, y, new Color(glow.r, glow.g, glow.b, glow.a * g));
                }
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name = "ProceduralRingTex"
        };
        tex.SetPixels(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        sprite.name = "ProceduralRingFrame";
        _ringCache[key] = sprite;
        return sprite;
    }

    /// <summary>Glow radial suave (gaussiano) — mismo look que el usado en DreamBackground/DreamSparkle.</summary>
    public static Sprite BuildSoftGlowSprite(int size = 64, float falloff = 2.8f)
    {
        var key = (size, falloff);
        if (_glowCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name = "ProceduralGlowTex"
        };
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Exp(-d * d * falloff);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        sprite.name = "ProceduralGlow";
        _glowCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// Disco sólido (círculo relleno) con halo exterior suave — fondo de slots circulares, base del
    /// overlay de cooldown radial, avatares, etc. Con <paramref name="glowRange"/> a 0 no lleva halo
    /// (útil para el overlay de cooldown, donde no queremos que "brille" por fuera del círculo).
    /// </summary>
    public static Sprite BuildFilledCircleSprite(
        int size = 96,
        float glowRange = 8f,
        Color? fill = null,
        Color? glow = null,
        bool rimHighlight = true)
    {
        Color fillColor = fill ?? Palette.PanelFill;
        Color glowColor = glow ?? Palette.PanelGlow;

        var key = (size, glowRange, fillColor, glowColor, rimHighlight);
        if (_circleCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

        var pixels = new Color[size * size];
        float c = size * 0.5f;
        float radius = c - glowRange * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) - c;
                float dy = (y + 0.5f) - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius; // <0 dentro del disco

                if (d > 0f)
                {
                    if (glowRange > 0f)
                    {
                        float g = Mathf.Clamp01(1f - d / glowRange);
                        g = g * g;
                        if (g > 0f) Blend(pixels, size, x, y, new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * g));
                    }
                    continue;
                }

                float cov = Mathf.Clamp01(0.5f - d);
                Color fc = fillColor;
                if (rimHighlight)
                {
                    float topFade = Mathf.Clamp01((y + 0.5f) / size); // 0 arriba, 1 abajo
                    float rim = Mathf.Clamp01(1f + d / Mathf.Max(radius * 0.5f, 0.001f)) * (1f - topFade);
                    fc = Color.Lerp(fillColor, Color.white, rim * 0.12f);
                }
                Blend(pixels, size, x, y, new Color(fc.r, fc.g, fc.b, fc.a * cov));
            }
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name = "ProceduralCircleTex"
        };
        tex.SetPixels(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        sprite.name = "ProceduralFilledCircle";
        _circleCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// Rectángulo plano de color sólido (con un leve brillo superior) para usar como relleno de una barra
    /// con <c>Image.Type.Filled</c>. Sin borde a propósito: el marco visual lo aporta el panel (generado con
    /// <see cref="BuildPanelSprite"/>) que va detrás — sus esquinas redondeadas enmarcan los extremos rectos
    /// de este relleno si se deja un margen pequeño entre ambos.
    /// </summary>
    public static Sprite BuildFlatFillSprite(Color color, bool topHighlight = true)
    {
        var key = (color, topHighlight);
        if (_fillCache.TryGetValue(key, out var cachedSprite) && cachedSprite != null) return cachedSprite;

        const int w = 32, h = 32;
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = topHighlight ? 1f - (float)y / (h - 1) : 0f; // 1 arriba, 0 abajo
            Color row = topHighlight ? Color.Lerp(color, Color.white, t * t * 0.22f) : color;
            for (int x = 0; x < w; x++) pixels[y * w + x] = row;
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            name = "ProceduralFillTex"
        };
        tex.SetPixels(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f);
        sprite.name = "ProceduralFlatFill";
        _fillCache[key] = sprite;
        return sprite;
    }
}
