using UnityEngine;

namespace Core.InputGlyphs.EditorTools
{
    /// <summary>
    /// Lienzo procedural para dibujar un glyph de botón (círculo, barra redondeada, cruz, texto...)
    /// píxel a píxel en memoria. Solo lo usa <see cref="InputGlyphPlaceholderFactory"/>, desde la
    /// herramienta de Editor <see cref="InputGlyphAssetGeneratorWindow"/>, para generar PLACEHOLDERS
    /// (PlayStation/Switch/Teclado&amp;Ratón, mientras no haya arte final) que se hornean una vez a PNG
    /// real en Assets/Resources/InputGlyphs — esto ya NO se ejecuta en tiempo de juego.
    ///
    /// Idéntico en su lógica de dibujo a la vieja Core.InputGlyphs.InputGlyphCanvas (que generaba
    /// estas mismas texturas en runtime, la "chapuza" original); solo cambia <see cref="ToTexture2D"/>
    /// en vez de envolver directamente en un <see cref="Sprite"/>, porque aquí hace falta el
    /// <see cref="Texture2D"/> crudo para poder guardarlo como PNG con <c>EncodeToPNG()</c>.
    /// </summary>
    internal sealed class InputGlyphPlaceholderCanvas
    {
        public readonly int Size;
        readonly Color[] _pixels;

        public InputGlyphPlaceholderCanvas(int size)
        {
            Size = size;
            _pixels = new Color[size * size];
        }

        // ── Compositing ──────────────────────────────────────────────────────

        void Blend(int x, int y, Color src)
        {
            if ((uint)x >= (uint)Size || (uint)y >= (uint)Size || src.a <= 0f) return;
            int i = y * Size + x;
            Color dst = _pixels[i];
            float outA = src.a + dst.a * (1f - src.a);
            if (outA <= 0.0001f) { _pixels[i] = default; return; }
            Color rgb = src * src.a + dst * (dst.a * (1f - src.a));
            _pixels[i] = new Color(rgb.r / outA, rgb.g / outA, rgb.b / outA, outA);
        }

        // ── Formas ────────────────────────────────────────────────────────────

        public void FillCircle(float cx, float cy, float r, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + r + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + r + 1));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float coverage = Mathf.Clamp01(r - d + 0.5f);
                if (coverage > 0f) Blend(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
            }
        }

        public void FillRoundedRect(float cx, float cy, float w, float h, float radius, Color color)
        {
            float halfW = w * 0.5f, halfH = h * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(halfW, halfH));
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - halfW - 1));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + halfW + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - halfH - 1));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + halfH + 1));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = Mathf.Abs(px - cx) - (halfW - radius);
                float dy = Mathf.Abs(py - cy) - (halfH - radius);
                float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
                float outsideDist = Mathf.Sqrt(ox * ox + oy * oy) - radius;
                float insideDist = Mathf.Min(Mathf.Max(dx, dy), 0f);
                float d = outsideDist + insideDist; // <0 dentro, >0 fuera
                float coverage = Mathf.Clamp01(0.5f - d);
                if (coverage > 0f) Blend(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
            }
        }

        /// <summary>Cruz/plus formada por dos barras redondeadas perpendiculares (D-pad, icono "+").</summary>
        public void FillCross(float cx, float cy, float armLength, float armThickness, float radius, Color color)
        {
            FillRoundedRect(cx, cy, armLength, armThickness, radius, color);
            FillRoundedRect(cx, cy, armThickness, armLength, radius, color);
        }

        /// <summary>Tres barras horizontales apiladas (icono de menú/hamburguesa).</summary>
        public void FillHamburger(float cx, float cy, float w, float barH, float gap, Color color)
        {
            FillRoundedRect(cx, cy - gap, w, barH, barH * 0.5f, color);
            FillRoundedRect(cx, cy, w, barH, barH * 0.5f, color);
            FillRoundedRect(cx, cy + gap, w, barH, barH * 0.5f, color);
        }

        /// <summary>Anillo (círculo hueco) — símbolo "○" de PlayStation.</summary>
        public void FillRing(float cx, float cy, float radius, float thickness, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - thickness - 1));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + radius + thickness + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - thickness - 1));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + radius + thickness + 1));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float dist = Mathf.Abs(d - radius) - thickness * 0.5f;
                float coverage = Mathf.Clamp01(0.5f - dist);
                if (coverage > 0f) Blend(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
            }
        }

        /// <summary>Cruz diagonal ("✕") — símbolo "Cross" de PlayStation. Dos líneas a 45°/135°.</summary>
        public void FillDiagonalCross(float cx, float cy, float armLength, float thickness, Color color)
        {
            float half = armLength * 0.5f * 0.70710678f; // proyección de cada brazo sobre cada eje a 45°
            FillLine(cx - half, cy - half, cx + half, cy + half, thickness, color);
            FillLine(cx - half, cy + half, cx + half, cy - half, thickness, color);
        }

        /// <summary>Triángulo equilátero relleno, vértice hacia arriba — símbolo "Triangle" de PlayStation.</summary>
        public void FillTriangle(float cx, float cy, float size, Color color)
        {
            float h = size * 0.86602540f; // altura de un triángulo equilátero de lado "size"
            Vector2 p0 = new(cx, cy - h * 0.6f);
            Vector2 p1 = new(cx - size * 0.5f, cy + h * 0.4f);
            Vector2 p2 = new(cx + size * 0.5f, cy + h * 0.4f);

            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x)) - 1));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)) + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y)) - 1));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)) + 1));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d0 = Cross2D(p1 - p0, p - p0);
                float d1 = Cross2D(p2 - p1, p - p1);
                float d2 = Cross2D(p0 - p2, p - p2);
                bool inside = (d0 >= 0f && d1 >= 0f && d2 >= 0f) || (d0 <= 0f && d1 <= 0f && d2 <= 0f);
                if (inside) Blend(x, y, color);
            }
        }

        static float Cross2D(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public void FillLine(float x0, float y0, float x1, float y1, float thickness, Color color)
        {
            float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float angle = Mathf.Atan2(y1 - y0, x1 - x0);
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - len));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + len));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - len));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + len));
            float cos = Mathf.Cos(-angle), sin = Mathf.Sin(-angle);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float rx = x + 0.5f - cx, ry = y + 0.5f - cy;
                float localX = rx * cos - ry * sin;
                float localY = rx * sin + ry * cos;
                float dx = Mathf.Abs(localX) - len * 0.5f;
                float dy = Mathf.Abs(localY) - thickness * 0.5f;
                float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
                float coverage = Mathf.Clamp01(0.5f - d);
                if (coverage > 0f) Blend(x, y, new Color(color.r, color.g, color.b, color.a * coverage));
            }
        }

        // ── Texto (fuente de píxeles propia) ─────────────────────────────────

        /// <summary>Dibuja texto centrado en (cx, cy). <paramref name="pixelScale"/> = px de pantalla por píxel de fuente.</summary>
        public void DrawText(string text, float cx, float cy, float pixelScale, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            int charW = InputGlyphPlaceholderFont.GlyphWidth;
            int charH = InputGlyphPlaceholderFont.GlyphHeight;
            float spacing = pixelScale; // 1 columna en blanco entre letras
            float totalW = text.Length * charW * pixelScale + Mathf.Max(0, text.Length - 1) * spacing;
            float startX = cx - totalW * 0.5f;
            float top = cy - charH * pixelScale * 0.5f;

            float penX = startX;
            foreach (char c in text)
            {
                if (c != ' ' && InputGlyphPlaceholderFont.TryGetGlyph(c, out var rows))
                {
                    for (int row = 0; row < charH; row++)
                    for (int col = 0; col < charW; col++)
                    {
                        if (rows[row][col] != '1') continue;
                        float px = penX + col * pixelScale;
                        float py = top + row * pixelScale;
                        FillRoundedRect(px + pixelScale * 0.5f, py + pixelScale * 0.5f, pixelScale, pixelScale, 0f, color);
                    }
                }
                penX += charW * pixelScale + spacing;
            }
        }

        public static float MeasureTextWidth(string text, float pixelScale)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            int charW = InputGlyphPlaceholderFont.GlyphWidth;
            float spacing = pixelScale;
            return text.Length * charW * pixelScale + Mathf.Max(0, text.Length - 1) * spacing;
        }

        /// <summary>
        /// Compone una textura ya renderizada aparte (normalmente una etiqueta de texto horneada con
        /// una fuente de verdad vía <see cref="InputGlyphPlaceholderTextRenderer"/>, blanca sobre
        /// negro) centrada en (cx,cy), escalada a (w,h), multiplicando por <paramref name="tint"/>.
        /// Así el texto se renderiza una vez en blanco y se puede teñir a cualquier color sin volver
        /// a renderizarlo.
        ///
        /// La cobertura se calcula a partir del BRILLO del píxel de origen (blanco = letra, negro =
        /// fondo), no de su canal alfa — <see cref="InputGlyphPlaceholderTextRenderer"/> lo renderiza
        /// así a propósito porque el alfa que devuelve no es fiable (ver comentario ahí). No usar esto
        /// para componer una textura de origen cualquiera con alfa real; está pensado específicamente
        /// para las etiquetas de texto que genera esa clase.
        /// </summary>
        public void DrawTexture(Texture2D source, float cx, float cy, float w, float h, Color tint)
        {
            if (source == null || w <= 0f || h <= 0f) return;

            float left = cx - w * 0.5f, top = cy - h * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(left));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(left + w));
            int minY = Mathf.Max(0, Mathf.FloorToInt(top));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(top + h));

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float u = (x + 0.5f - left) / w;
                float v = (y + 0.5f - top) / h;
                if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

                Color sample = source.GetPixelBilinear(u, v);
                float coverage = (sample.r + sample.g + sample.b) / 3f; // brillo = cobertura de la letra
                if (coverage <= 0.02f) continue;
                Blend(x, y, new Color(tint.r, tint.g, tint.b, tint.a * coverage));
            }
        }

        // ── Salida ────────────────────────────────────────────────────────────

        public Texture2D ToTexture2D(string name)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            tex.SetPixels(_pixels);
            tex.Apply();
            return tex;
        }
    }
}
