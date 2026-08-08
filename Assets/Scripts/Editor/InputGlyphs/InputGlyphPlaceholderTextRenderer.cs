using TMPro;
using UnityEditor;
using UnityEngine;

namespace Core.InputGlyphs.EditorTools
{
    /// <summary>
    /// Renderiza una etiqueta de texto con una fuente de verdad (TextMeshPro — la misma que usa el
    /// resto del proyecto para diálogos) a una textura aparte, en blanco sobre NEGRO (no
    /// transparente — ver por qué debajo), para que
    /// <see cref="InputGlyphPlaceholderCanvas.DrawTexture"/> la componga (y tiña del color que toque)
    /// encima del icono ya dibujado, usando el brillo de cada píxel como cobertura en vez del canal
    /// alfa.
    ///
    /// Sustituye la fuente de píxeles 5x7 dibujada a mano (<see cref="InputGlyphPlaceholderFont"/>)
    /// para el TEXTO de las etiquetas ("A", "LB", "CTRL"...) — esa fuente casera se veía ilegible al
    /// tamaño real de un icono de botón. Las FORMAS (círculos, barras, símbolos de PlayStation...) no
    /// son texto y ya se veían bien, así que siguen dibujándose igual que antes con
    /// <see cref="InputGlyphPlaceholderCanvas"/> (FillCircle, FillRing, FillTriangle...).
    ///
    /// Usa <see cref="PreviewRenderUtility"/>, la misma utilidad con la que el propio Editor de Unity
    /// genera las miniaturas de preview de prefabs/materiales: monta una escena de preview aislada
    /// (no toca ni se ve afectada por la escena abierta en el Editor), renderiza un frame a una
    /// textura y la destruye. Solo corre desde la herramienta de Editor, nunca en el juego.
    ///
    /// NOTA sobre el fondo negro en vez de transparente: la primera versión limpiaba la cámara a
    /// negro CON alfa 0 (transparente) y componía usando ese canal alfa — pero
    /// <see cref="PreviewRenderUtility"/> devuelve el alfa a 1 en toda la imagen pase lo que pase (es
    /// una utilidad pensada para miniaturas opacas, no para recortes con transparencia), así que el
    /// resultado era un bloque sólido del color de la etiqueta tapando todo el icono en vez de solo
    /// las letras. Ahora limpiamos a negro OPACO y usamos el brillo (blanco=letra, negro=fondo) como
    /// cobertura al componer.
    ///
    /// NOTA sobre el encuadre de la cámara: la primera versión usaba un tamaño de cámara ortográfica
    /// FIJO (200 unidades) con un fontSize fijo adivinado (130) asumiendo cierta relación entre
    /// "fontSize" y unidades de mundo — esa relación depende de cómo esté generada la fuente del
    /// proyecto y no es la misma en todos los proyectos, así que el texto salía minúsculo dentro de
    /// ese encuadre (invisible al componerlo). Ahora, en vez de adivinar, se mide el tamaño REAL ya
    /// renderizado del texto (<c>Renderer.bounds</c>, después de <c>ForceMeshUpdate()</c>) y la
    /// cámara se ajusta a ese tamaño exacto — funciona sea cual sea la relación fontSize/unidades de
    /// la fuente del proyecto, porque nunca se asume, se mide.
    /// </summary>
    internal static class InputGlyphPlaceholderTextRenderer
    {
        const int TargetPixelHeight = 200;
        const float FontSize = 36f; // valor arbitrario razonable — el encuadre se ajusta solo al tamaño real medido, no depende de este número
        // Margen alrededor del texto medido: con poco margen (probado con 1.18) el negrita synthetic
        // de FontStyles.Bold sobre una fuente SDF sin variante bold dedicada dilata el trazo y lo deja
        // pegado al borde del encuadre — resultado: letra gigante y como "hinchada"/ilegible en vez de
        // una letra normal. Con más margen y sin negrita synthetic (ver más abajo) queda una letra
        // limpia con aire alrededor, como cualquier etiqueta normal.
        const float FramingPadding = 1.8f;

        /// <summary>
        /// Renderiza <paramref name="text"/> y devuelve la textura (blanca sobre negro) junto con su
        /// relación de aspecto ancho/alto real, para poder componerla en
        /// <see cref="InputGlyphPlaceholderCanvas.DrawTexture"/> sin deformar las letras. Devuelve
        /// <c>null</c> si el texto no produjo ninguna geometría visible (por ejemplo una cadena vacía)
        /// — el llamador debe tratarlo igual que cualquier otro fallo y usar su respaldo.
        /// El llamador es responsable de destruir la textura devuelta cuando termine con ella.
        /// </summary>
        public static Texture2D RenderText(string text, out float aspect)
        {
            aspect = 1f;
            if (string.IsNullOrEmpty(text)) return null;

            var preview = new PreviewRenderUtility();
            try
            {
                var textGo = new GameObject("InputGlyphLabel");
                var tmp = textGo.AddComponent<TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = FontSize;
                // Normal, no Bold: la mayoría de fuentes no traen una variante bold dedicada en su
                // Font Asset, así que TMP simularía el negrita dilatando el SDF — eso es lo que
                // hinchaba las letras hasta verse gigantes/borrosas (ver comentario de FramingPadding).
                tmp.fontStyle = FontStyles.Normal;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.enableWordWrapping = false;
                tmp.ForceMeshUpdate();

                var meshRenderer = textGo.GetComponent<MeshRenderer>();
                if (meshRenderer == null) return null;

                Bounds bounds = meshRenderer.bounds;
                if (bounds.extents.x <= 0.0001f || bounds.extents.y <= 0.0001f) return null;

                preview.AddSingleGO(textGo);

                float halfHeight = bounds.extents.y * FramingPadding;
                float halfWidth = bounds.extents.x * FramingPadding;
                aspect = halfWidth / halfHeight;

                int width = Mathf.Clamp(Mathf.RoundToInt(TargetPixelHeight * aspect), 32, 1024);

                preview.camera.orthographic = true;
                preview.camera.orthographicSize = halfHeight;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = Color.black; // opaco a propósito, ver comentario de clase
                preview.camera.nearClipPlane = 0.05f;
                preview.camera.farClipPlane = Mathf.Max(20f, bounds.size.magnitude * 4f);
                preview.camera.transform.position = bounds.center + new Vector3(0f, 0f, -10f);
                preview.camera.transform.rotation = Quaternion.identity;

                preview.BeginStaticPreview(new Rect(0, 0, width, TargetPixelHeight));
                preview.camera.Render();
                return preview.EndStaticPreview();
            }
            finally
            {
                preview.Cleanup();
            }
        }
    }
}
