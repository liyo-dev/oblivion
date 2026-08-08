using System.Collections.Generic;
using UnityEngine;

namespace Core.InputGlyphs.EditorTools
{
    /// <summary>
    /// Genera, para una familia de dispositivo dada, las 11 imágenes de botón PLACEHOLDER (mismos
    /// nombres que <see cref="InputGlyphNames"/>) dibujadas por código con
    /// <see cref="InputGlyphPlaceholderCanvas"/>. Solo la usa <see cref="InputGlyphAssetGeneratorWindow"/>
    /// para hornear PNG reales en Assets/Resources/InputGlyphs — esto ya NO corre en tiempo de juego.
    ///
    /// Es el mismo dibujado por código que antes hacía en runtime la vieja
    /// Core.InputGlyphs.InputGlyphAssetFactory (la "chapuza" original: generar las imágenes cada vez
    /// que arrancaba el juego en vez de tener archivos reales). Aquí se genera UNA VEZ, desde el
    /// Editor, y el resultado se guarda como PNG normal y corriente — así cualquiera puede abrirlo y
    /// sustituirlo por arte final sin tocar código.
    ///
    /// Xbox normalmente ni pasa por aquí (la ventana copia el arte real ya existente en
    /// Assets/Art/UI/Buttons), pero se deja implementado también por si hace falta como último
    /// recurso (arte Xbox no encontrado).
    /// </summary>
    internal static class InputGlyphPlaceholderFactory
    {
        const int TexSize = 96;

        static readonly Color ColXboxA = new Color(0.10f, 0.55f, 0.20f);
        static readonly Color ColXboxB = new Color(0.75f, 0.12f, 0.12f);
        static readonly Color ColXboxX = new Color(0.10f, 0.35f, 0.70f);
        static readonly Color ColXboxY = new Color(0.90f, 0.72f, 0.10f);
        static readonly Color ColDarkBody = new Color(0.16f, 0.17f, 0.20f);
        static readonly Color TextWhite = Color.white;
        static readonly Color TextDark = new Color(0.12f, 0.10f, 0.05f);

        static readonly Color ColPsCross = new Color(0.42f, 0.68f, 0.90f);
        static readonly Color ColPsCircle = new Color(0.88f, 0.25f, 0.30f);
        static readonly Color ColPsSquare = new Color(0.86f, 0.42f, 0.72f);
        static readonly Color ColPsTriangle = new Color(0.30f, 0.78f, 0.62f);

        static readonly Color ColSwitchBody = new Color(0.20f, 0.21f, 0.27f);

        static readonly Color KeycapBase = new Color(0.94f, 0.94f, 0.97f);
        static readonly Color KeycapBorder = new Color(0.55f, 0.55f, 0.62f);
        static readonly Color KeycapText = new Color(0.15f, 0.15f, 0.20f);
        static readonly Color MouseAccent = new Color(1f, 0.80f, 0.30f);

        /// <summary>Genera un único botón placeholder. Usada por la ventana para "Regenerar" una sola
        /// casilla sin tener que redibujar toda la familia.</summary>
        public static Texture2D BuildTexture(InputGlyphDeviceFamily family, string buttonName)
        {
            var all = BuildFamilyTextures(family);
            var result = all[buttonName];
            foreach (var kvp in all)
                if (kvp.Key != buttonName) Object.DestroyImmediate(kvp.Value);
            return result;
        }

        public static Dictionary<string, Texture2D> BuildFamilyTextures(InputGlyphDeviceFamily family)
        {
            var result = new Dictionary<string, Texture2D>(11);

            switch (family)
            {
                case InputGlyphDeviceFamily.KeyboardMouse:
                    result[InputGlyphNames.South] = Keycap("E");
                    result[InputGlyphNames.North] = Keycap("Q");
                    result[InputGlyphNames.West] = MouseGlyph(highlightLeft: true, highlightRight: false, wheel: false, wheelUp: false);
                    result[InputGlyphNames.East] = MouseGlyph(highlightLeft: false, highlightRight: true, wheel: false, wheelUp: false);
                    result[InputGlyphNames.ShoulderLeft] = MouseGlyph(highlightLeft: false, highlightRight: false, wheel: true, wheelUp: false);
                    result[InputGlyphNames.ShoulderRight] = MouseGlyph(highlightLeft: false, highlightRight: false, wheel: true, wheelUp: true);
                    result[InputGlyphNames.TriggerLeft] = WideKeycap("CTRL");
                    result[InputGlyphNames.TriggerRight] = WideKeycap("CTRL");
                    result[InputGlyphNames.Dpad] = WideKeycap("WASD");
                    result[InputGlyphNames.Stick] = MouseGlyph(highlightLeft: false, highlightRight: false, wheel: false, wheelUp: false);
                    result[InputGlyphNames.Start] = WideKeycap("ESC");
                    // Atajo de teletransporte en un punto de guardado (SavePointTeleportTrigger lee la
                    // tecla T directamente) — NO comparte tecla con North/AttackMagicNorth (Q), a
                    // diferencia de mando, donde sí es el mismo botón físico. Ver InputGlyphNames.Teleport.
                    result[InputGlyphNames.Teleport] = Keycap("T");
                    break;

                case InputGlyphDeviceFamily.PlayStation:
                    result[InputGlyphNames.South] = FaceSymbol(c => c.FillDiagonalCross(TexSize * 0.5f, TexSize * 0.5f, TexSize * 0.34f, TexSize * 0.075f, ColPsCross));
                    result[InputGlyphNames.East] = FaceSymbol(c => c.FillRing(TexSize * 0.5f, TexSize * 0.5f, TexSize * 0.20f, TexSize * 0.075f, ColPsCircle));
                    result[InputGlyphNames.West] = FaceSymbol(c => c.FillRoundedRect(TexSize * 0.5f, TexSize * 0.5f, TexSize * 0.34f, TexSize * 0.34f, TexSize * 0.05f, ColPsSquare));
                    result[InputGlyphNames.North] = FaceSymbol(c => c.FillTriangle(TexSize * 0.5f, TexSize * 0.5f, TexSize * 0.40f, ColPsTriangle));
                    result[InputGlyphNames.ShoulderLeft] = ShoulderBar("L1");
                    result[InputGlyphNames.ShoulderRight] = ShoulderBar("R1");
                    result[InputGlyphNames.TriggerLeft] = ShoulderBar("L2");
                    result[InputGlyphNames.TriggerRight] = ShoulderBar("R2");
                    result[InputGlyphNames.Dpad] = DpadIcon();
                    result[InputGlyphNames.Stick] = StickIcon();
                    result[InputGlyphNames.Start] = HamburgerIcon();
                    // En mando, teletransporte es el mismo botón físico que North (△) — ver comentario
                    // en InputGlyphNames.Teleport. Se regenera en vez de reutilizar la misma instancia
                    // de Texture2D de North porque BuildTexture() destruye por clave todas las texturas
                    // no solicitadas al regenerar un único botón desde la ventana del Editor; compartir
                    // el objeto dejaría el otro nombre apuntando a una textura ya destruida.
                    result[InputGlyphNames.Teleport] = FaceSymbol(c => c.FillTriangle(TexSize * 0.5f, TexSize * 0.5f, TexSize * 0.40f, ColPsTriangle));
                    break;

                case InputGlyphDeviceFamily.Switch:
                    result[InputGlyphNames.South] = FaceButton(ColSwitchBody, "B", TextWhite);
                    result[InputGlyphNames.East] = FaceButton(ColSwitchBody, "A", TextWhite);
                    result[InputGlyphNames.West] = FaceButton(ColSwitchBody, "Y", TextWhite);
                    result[InputGlyphNames.North] = FaceButton(ColSwitchBody, "X", TextWhite);
                    result[InputGlyphNames.ShoulderLeft] = ShoulderBar("L");
                    result[InputGlyphNames.ShoulderRight] = ShoulderBar("R");
                    result[InputGlyphNames.TriggerLeft] = ShoulderBar("ZL");
                    result[InputGlyphNames.TriggerRight] = ShoulderBar("ZR");
                    result[InputGlyphNames.Dpad] = DpadIcon();
                    result[InputGlyphNames.Stick] = StickIcon();
                    result[InputGlyphNames.Start] = PlusIcon();
                    // Mismo botón físico que North (X) — ver comentario en el caso PlayStation de
                    // arriba sobre por qué se regenera en vez de reutilizar la instancia.
                    result[InputGlyphNames.Teleport] = FaceButton(ColSwitchBody, "X", TextWhite);
                    break;

                case InputGlyphDeviceFamily.Xbox:
                default:
                    result[InputGlyphNames.South] = FaceButton(ColXboxA, "A", TextWhite);
                    result[InputGlyphNames.East] = FaceButton(ColXboxB, "B", TextWhite);
                    result[InputGlyphNames.West] = FaceButton(ColXboxX, "X", TextWhite);
                    result[InputGlyphNames.North] = FaceButton(ColXboxY, "Y", TextDark);
                    result[InputGlyphNames.ShoulderLeft] = ShoulderBar("LB");
                    result[InputGlyphNames.ShoulderRight] = ShoulderBar("RB");
                    result[InputGlyphNames.TriggerLeft] = ShoulderBar("LT");
                    result[InputGlyphNames.TriggerRight] = ShoulderBar("RT");
                    result[InputGlyphNames.Dpad] = DpadIcon();
                    result[InputGlyphNames.Stick] = StickIcon();
                    result[InputGlyphNames.Start] = HamburgerIcon();
                    // Xbox normalmente no pasa por este placeholder (la ventana copia el arte real de
                    // Assets/Art/UI/Buttons, incluido interactable_teleport si existe ahí como copia de
                    // interactable_y.png) — esto es solo el último recurso si falta ese arte real.
                    result[InputGlyphNames.Teleport] = FaceButton(ColXboxY, "Y", TextDark);
                    break;
            }

            return result;
        }

        /// <summary>
        /// Dibuja una etiqueta de texto sobre <paramref name="canvas"/> (ya con el fondo/forma
        /// pintado) usando una fuente de verdad vía
        /// <see cref="InputGlyphPlaceholderTextRenderer"/> — se ve como una letra normal, no como el
        /// mosaico ilegible de la fuente de píxeles casera de antes. <paramref name="boxHeight"/> es
        /// la altura visual deseada en el icono; el ancho sale solo de la relación de aspecto del
        /// texto renderizado, así que "A" y "CTRL" quedan bien proporcionados sin tener que calcular
        /// nada a mano por etiqueta.
        ///
        /// Si el renderizado con fuente real falla por lo que sea (por ejemplo, algún entorno sin TMP
        /// bien configurado), cae de vuelta al dibujo por píxeles de siempre en vez de romper toda la
        /// generación — mejor una etiqueta fea que ninguna.
        /// </summary>
        static void DrawLabel(InputGlyphPlaceholderCanvas canvas, string label, float cx, float cy, float boxHeight, Color color)
        {
            try
            {
                var tex = InputGlyphPlaceholderTextRenderer.RenderText(label, out float aspect);
                if (tex != null)
                {
                    canvas.DrawTexture(tex, cx, cy, boxHeight * aspect, boxHeight, color);
                    Object.DestroyImmediate(tex);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[InputGlyphPlaceholderFactory] No se pudo renderizar '{label}' con " +
                                  $"fuente real, uso la fuente de píxeles de respaldo: {ex.Message}");
            }

            canvas.DrawText(label, cx, cy, boxHeight * 0.11f, color);
        }

        // ── Glyphs de mando ───────────────────────────────────────────────────

        static Texture2D FaceButton(Color fill, string label, Color textColor)
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillCircle(mid, mid, TexSize * 0.42f, fill);
            DrawLabel(c, label, mid, mid, TexSize * 0.40f, textColor);
            return c.ToTexture2D(label);
        }

        static Texture2D FaceSymbol(System.Action<InputGlyphPlaceholderCanvas> drawSymbol)
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillCircle(mid, mid, TexSize * 0.42f, ColDarkBody);
            drawSymbol(c);
            return c.ToTexture2D("psSymbol");
        }

        static Texture2D PlusIcon()
        {
            // Símbolo "+" del botón Plus/Start de Switch — es una forma, no una letra, así que se
            // dibuja como vector (dos barras cruzadas) igual que el D-pad, no como texto.
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillCircle(mid, mid, TexSize * 0.42f, ColSwitchBody);
            c.FillCross(mid, mid, TexSize * 0.32f, TexSize * 0.075f, TexSize * 0.02f, TextWhite);
            return c.ToTexture2D("start");
        }

        static Texture2D ShoulderBar(string label)
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillRoundedRect(mid, mid, TexSize * 0.82f, TexSize * 0.42f, TexSize * 0.16f, ColDarkBody);
            DrawLabel(c, label, mid, mid, TexSize * 0.28f, TextWhite);
            return c.ToTexture2D(label);
        }

        static Texture2D DpadIcon()
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillCross(mid, mid, TexSize * 0.68f, TexSize * 0.26f, TexSize * 0.06f, ColDarkBody);
            return c.ToTexture2D("dpad");
        }

        static Texture2D StickIcon()
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillCircle(mid, mid, TexSize * 0.40f, ColDarkBody);
            c.FillCircle(mid, mid, TexSize * 0.16f, MouseAccent);
            return c.ToTexture2D("stick");
        }

        static Texture2D HamburgerIcon()
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillRoundedRect(mid, mid, TexSize * 0.72f, TexSize * 0.72f, TexSize * 0.12f, ColDarkBody);
            c.FillHamburger(mid, mid, TexSize * 0.42f, TexSize * 0.07f, TexSize * 0.16f, TextWhite);
            return c.ToTexture2D("start");
        }

        // ── Glyphs de teclado/ratón ───────────────────────────────────────────

        static Texture2D Keycap(string label)
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillRoundedRect(mid, mid, TexSize * 0.74f, TexSize * 0.74f, TexSize * 0.14f, KeycapBorder);
            c.FillRoundedRect(mid, mid, TexSize * 0.64f, TexSize * 0.64f, TexSize * 0.11f, KeycapBase);
            DrawLabel(c, label, mid, mid, TexSize * 0.36f, KeycapText);
            return c.ToTexture2D(label);
        }

        static Texture2D WideKeycap(string label)
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            c.FillRoundedRect(mid, mid, TexSize * 0.92f, TexSize * 0.5f, TexSize * 0.12f, KeycapBorder);
            c.FillRoundedRect(mid, mid, TexSize * 0.86f, TexSize * 0.40f, TexSize * 0.09f, KeycapBase);
            // La textura de la etiqueta escala su ancho con la longitud del texto (ver
            // InputGlyphPlaceholderTextRenderer), así que "CTRL"/"WASD" no se salen del tecla ancha
            // sin necesidad de calcular a mano una escala reducida como antes.
            DrawLabel(c, label, mid, mid, TexSize * 0.24f, KeycapText);
            return c.ToTexture2D(label);
        }

        /// <summary>Silueta simple de ratón. Botón izq/der resaltado, o rueda central (con flecha arriba/abajo).</summary>
        static Texture2D MouseGlyph(bool highlightLeft, bool highlightRight, bool wheel, bool wheelUp)
        {
            var c = new InputGlyphPlaceholderCanvas(TexSize);
            float mid = TexSize * 0.5f;
            float bodyW = TexSize * 0.5f, bodyH = TexSize * 0.78f;
            float top = mid - bodyH * 0.5f;

            c.FillRoundedRect(mid, mid, bodyW, bodyH, bodyW * 0.5f, KeycapBorder);
            c.FillRoundedRect(mid, mid + 1f, bodyW - 3f, bodyH - 3f, (bodyW - 3f) * 0.5f, KeycapBase);

            float buttonZoneBottom = top + bodyH * 0.42f;
            c.FillLine(mid, top + 4f, mid, buttonZoneBottom, 1.5f, KeycapBorder);

            if (highlightLeft)
                c.FillRoundedRect(mid - bodyW * 0.25f, top + bodyH * 0.20f, bodyW * 0.46f, bodyH * 0.32f, TexSize * 0.05f, MouseAccent);
            if (highlightRight)
                c.FillRoundedRect(mid + bodyW * 0.25f, top + bodyH * 0.20f, bodyW * 0.46f, bodyH * 0.32f, TexSize * 0.05f, MouseAccent);

            if (wheel)
            {
                c.FillRoundedRect(mid, top + bodyH * 0.30f, bodyW * 0.22f, bodyH * 0.22f, TexSize * 0.03f, MouseAccent);
                float arrowY = wheelUp ? top + bodyH * 0.30f - bodyH * 0.20f : top + bodyH * 0.30f + bodyH * 0.20f;
                float dir = wheelUp ? -1f : 1f;
                c.FillLine(mid - 5f, arrowY - dir * 3f, mid, arrowY + dir * 3f, 2f, MouseAccent);
                c.FillLine(mid + 5f, arrowY - dir * 3f, mid, arrowY + dir * 3f, 2f, MouseAccent);
            }

            return c.ToTexture2D("mouse");
        }
    }
}
