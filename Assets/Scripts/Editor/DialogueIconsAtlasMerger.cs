using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEditor;
using TMPro;

/// <summary>
/// Migra los iconos de diálogo que hoy solo existen como sub-assets de <c>fallbackSpriteAssets</c>
/// (algas, boots, interactable_b/dpad/Joystick/lb/lt/rb/rt/x/y, lifePotion, start...) a la tabla
/// PROPIA de DialogueIcons.asset, empaquetándolos junto con lo que ya hubiera (p.ej. interactable_A)
/// en un único atlas nuevo.
///
/// Por qué: TextMeshPro (com.unity.ugui) tiene un bug de motor sin fix oficial en
/// TMP_Text.SaveSpriteVertexInfo — NullReferenceException al generar el mesh de un
/// &lt;sprite name="X"&gt; cuando ese sprite se resuelve recorriendo fallbackSpriteAssets en vez
/// de estar en la tabla propia del sprite asset asignado al texto. Ver TDD.md § 13 (bug U1) y
/// el comentario grande junto a TryForceMeshUpdate() en DialogueManager.cs.
///
/// Automático a propósito — no hace falta mapear nombres, rutas ni tamaños a mano: el script
/// recorre fallbackSpriteAssets de DialogueIcons.asset (y lo que ya haya en su propia tabla) y
/// usa el nombre/textura/métricas que cada sub-asset ya tiene.
///
/// Qué hace exactamente:
///   1. Reúne todos los iconos (los ya migrados + cada fallback) como fuente.
///   2. Reescala cada uno (si hace falta) a MaxIconSize px de lado más largo — el atlas
///      no necesita la resolución nativa (hasta 1600px) para un icono inline de texto.
///   3. Los empaqueta en un atlas nuevo con Texture2D.PackTextures y lo guarda como PNG
///      importado (Assets/Art/UI/DialogueIcons/DialogueIcons_Atlas.png).
///   4. Reescribe spriteSheet, material, m_GlyphTable y m_SpriteCharacterTable de
///      DialogueIcons.asset para que apunten al atlas nuevo, con las métricas de cada icono
///      original escaladas proporcionalmente.
///   5. Vacía fallbackSpriteAssets (ya no hace falta ningún fallback: todo vive en la tabla
///      propia). Los .asset originales de cada icono NO se borran — quedan sin usar por si hay
///      que revertir (o puedes borrarlos a mano una vez verificado en el Editor).
///
/// Después de ejecutarlo: ábrelo en Play Mode y revisa unas líneas de diálogo con iconos antes
/// de hacer commit — el script no puede verificar el resultado visual por ti. Es normal tener
/// que retocar un poco el escalado/posición de algún icono en el Inspector de DialogueIcons.asset
/// (m_Scale por carácter) si no queda perfecto a simple vista; eso es ajuste visual, no
/// "mapeo a mano" de datos.
///
/// Uso: Tools &gt; El Sendero &gt; Diálogo &gt; Fusionar Iconos de Diálogo en un Atlas.
/// </summary>
public static class DialogueIconsAtlasMerger
{
    private const string MainAssetPath = "Assets/Art/UI/DialogueIcons/DialogueIcons.asset";
    private const string AtlasOutputPath = "Assets/Art/UI/DialogueIcons/DialogueIcons_Atlas.png";

    private const int Padding = 4;
    private const int MaxAtlasSize = 4096;

    // Los PNG de origen llegan a 1600px de lado — muchísimo más de lo que necesita un icono
    // inline de texto. Los reescalamos a esto antes de empaquetar (ajustable si hiciera falta
    // más nitidez): reduce muchísimo el tamaño del atlas final sin pérdida visible en pantalla.
    private const int MaxIconSize = 256;

    private class SourceIcon
    {
        public string name;
        public Texture2D texture;
        public TMP_SpriteGlyph glyph;
        public TMP_SpriteCharacter characterEntry;
    }

    [MenuItem("Tools/El Sendero/Diálogo/Fusionar Iconos de Diálogo en un Atlas")]
    public static void Run()
    {
        var main = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(MainAssetPath);
        if (main == null)
        {
            Debug.LogError($"[DialogueIconsAtlasMerger] No se encontró {MainAssetPath}");
            return;
        }

        bool hasFallbacks = main.fallbackSpriteAssets != null && main.fallbackSpriteAssets.Count > 0;
        if (!hasFallbacks)
        {
            Debug.Log("[DialogueIconsAtlasMerger] fallbackSpriteAssets ya está vacío — nada que fusionar " +
                      "(¿ya se ejecutó antes esta herramienta?).");
            return;
        }

        // 1) Reunir fuentes: lo que ya haya en la tabla propia de DialogueIcons.asset (p.ej.
        //    interactable_A, ya migrado) + el/los glyph(s) de cada sub-asset de fallback.
        var sources = new List<SourceIcon>();
        var seenNames = new HashSet<string>();

        void CollectFrom(TMP_SpriteAsset asset)
        {
            if (asset == null || asset.spriteCharacterTable == null) return;

            foreach (var ch in asset.spriteCharacterTable)
            {
                if (ch == null || string.IsNullOrEmpty(ch.name)) continue;
                if (!seenNames.Add(ch.name)) continue; // ya cubierto (duplicado entre fallbacks)

                var glyph = asset.spriteGlyphTable?.FirstOrDefault(g => g.index == ch.glyphIndex);
                if (glyph == null || glyph.sprite == null || glyph.sprite.texture == null)
                {
                    Debug.LogWarning($"[DialogueIconsAtlasMerger] '{asset.name}' → carácter '{ch.name}' " +
                                      "sin glyph/sprite/textura válidos, se omite.");
                    continue;
                }

                sources.Add(new SourceIcon
                {
                    name = ch.name,
                    texture = glyph.sprite.texture as Texture2D,
                    glyph = glyph,
                    characterEntry = ch
                });
            }
        }

        CollectFrom(main);
        foreach (var fallback in main.fallbackSpriteAssets)
            CollectFrom(fallback);

        if (sources.Count == 0)
        {
            Debug.LogWarning("[DialogueIconsAtlasMerger] No se encontró ningún icono válido para fusionar. Nada cambiado.");
            return;
        }

        // 2) Copia reescalada y legible de cada textura de origen (no toca el import setting
        //    original: se muestrea por GPU vía Graphics.Blit, no requiere Read/Write Enabled).
        var scaledCopies = new Texture2D[sources.Count];
        var scaleFactors = new float[sources.Count];
        for (int i = 0; i < sources.Count; i++)
            scaledCopies[i] = CreateScaledReadableCopy(sources[i].texture, MaxIconSize, out scaleFactors[i]);

        // 3) Empaquetar en un atlas nuevo.
        var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Rect[] uvRects = atlas.PackTextures(scaledCopies, Padding, MaxAtlasSize, false);

        for (int i = 0; i < scaledCopies.Length; i++)
            Object.DestroyImmediate(scaledCopies[i]);

        if (uvRects == null)
        {
            Object.DestroyImmediate(atlas);
            Debug.LogError("[DialogueIconsAtlasMerger] PackTextures ha fallado (¿demasiados iconos para " +
                            $"MaxAtlasSize={MaxAtlasSize}?). Nada cambiado.");
            return;
        }

        // 4) Guardar el atlas como PNG e importarlo como Texture2D del proyecto.
        byte[] pngBytes = atlas.EncodeToPNG();
        int atlasW = atlas.width, atlasH = atlas.height;
        Object.DestroyImmediate(atlas);

        Directory.CreateDirectory(Path.GetDirectoryName(AtlasOutputPath));
        File.WriteAllBytes(AtlasOutputPath, pngBytes);
        AssetDatabase.ImportAsset(AtlasOutputPath, ImportAssetOptions.ForceUpdate);

        var atlasImporter = (TextureImporter)AssetImporter.GetAtPath(AtlasOutputPath);
        atlasImporter.textureType = TextureImporterType.Sprite;
        atlasImporter.spriteImportMode = SpriteImportMode.Single;
        atlasImporter.alphaIsTransparency = true;
        atlasImporter.mipmapEnabled = false;
        atlasImporter.filterMode = FilterMode.Bilinear;
        atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
        atlasImporter.SaveAndReimport();

        var atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasOutputPath);
        var atlasSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AtlasOutputPath);
        if (atlasTexture == null || atlasSprite == null)
        {
            Debug.LogError($"[DialogueIconsAtlasMerger] No se pudo (re)importar {AtlasOutputPath} como Texture2D/Sprite.");
            return;
        }

        // 5) Material nuevo para DialogueIcons.asset apuntando al atlas.
        ShaderUtilities.GetShaderPropertyIDs(); // asegura que ID_MainTex esté inicializado
        Shader shader = main.material != null ? main.material.shader : Shader.Find("TextMeshPro/Sprite");
        var newMaterial = new Material(shader) { name = "DialogueIcons Atlas Material" };
        newMaterial.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
        AssetDatabase.AddObjectToAsset(newMaterial, main);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(main));

        // 6) Reconstruir las tablas del asset principal con las coordenadas del atlas nuevo,
        //    escalando las métricas originales de cada icono por el mismo factor que su
        //    reescalado (para no perder las proporciones que ya tenía).
        var newGlyphTable = new List<TMP_SpriteGlyph>(sources.Count);
        var newCharacterTable = new List<TMP_SpriteCharacter>(sources.Count);
        var report = new System.Text.StringBuilder();

        for (int i = 0; i < sources.Count; i++)
        {
            var s = sources[i];
            Rect uv = uvRects[i];
            float scale = scaleFactors[i];

            var pixelRect = new GlyphRect(
                Mathf.RoundToInt(uv.x * atlasW),
                Mathf.RoundToInt(uv.y * atlasH),
                Mathf.RoundToInt(uv.width * atlasW),
                Mathf.RoundToInt(uv.height * atlasH));

            var oldMetrics = s.glyph.metrics;
            var metrics = new GlyphMetrics(
                pixelRect.width,
                pixelRect.height,
                oldMetrics.horizontalBearingX * scale,
                oldMetrics.horizontalBearingY * scale,
                oldMetrics.horizontalAdvance * scale);

            var newGlyph = new TMP_SpriteGlyph((uint)i, metrics, pixelRect, 1f, 0, atlasSprite);
            newGlyphTable.Add(newGlyph);

            var newChar = new TMP_SpriteCharacter(0xFFFE, main, newGlyph)
            {
                name = s.name,
                scale = s.characterEntry.scale
            };
            newCharacterTable.Add(newChar);

            report.AppendLine($"  - {s.name}: {s.texture.width}x{s.texture.height} → " +
                               $"{pixelRect.width}x{pixelRect.height} en el atlas (x{scale:F2})");
        }

        main.spriteSheet = atlasTexture;
        main.material = newMaterial;

        main.spriteGlyphTable.Clear();
        main.spriteGlyphTable.AddRange(newGlyphTable);

        main.spriteCharacterTable.Clear();
        main.spriteCharacterTable.AddRange(newCharacterTable);

        int fallbacksCleared = main.fallbackSpriteAssets.Count;
        main.fallbackSpriteAssets.Clear();

        main.UpdateLookupTables();

        EditorUtility.SetDirty(main);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.objects = new Object[] { main, atlasTexture };
        EditorGUIUtility.PingObject(main);

        Debug.Log(
            $"[DialogueIconsAtlasMerger] Fusionados {sources.Count} iconos en un atlas de {atlasW}x{atlasH}px " +
            $"({AtlasOutputPath}). fallbackSpriteAssets vaciado ({fallbacksCleared} entradas). " +
            "Los .asset de icono originales NO se han borrado (quedan sin usar).\n" +
            report +
            "\nSiguiente paso: abre una escena con diálogo, entra en Play Mode y revisa que cada icono " +
            "se vea bien (tamaño/posición) antes de hacer commit. Si alguno queda desalineado, ajusta su " +
            "'Scale' en el Inspector de DialogueIcons.asset (pestaña Sprite Character Table).");
    }

    /// <summary>
    /// Crea una copia legible (CPU) de <paramref name="src"/>, reescalada si su lado más largo
    /// supera <paramref name="maxSize"/>. Usa Graphics.Blit (GPU), así que no requiere que
    /// <paramref name="src"/> tenga Read/Write Enabled ni toca su import setting original.
    /// </summary>
    private static Texture2D CreateScaledReadableCopy(Texture2D src, int maxSize, out float scale)
    {
        int srcW = src.width, srcH = src.height;
        int longestSide = Mathf.Max(srcW, srcH);
        scale = longestSide > maxSize ? (float)maxSize / longestSide : 1f;

        int dstW = Mathf.Max(1, Mathf.RoundToInt(srcW * scale));
        int dstH = Mathf.Max(1, Mathf.RoundToInt(srcH * scale));

        RenderTexture rt = RenderTexture.GetTemporary(dstW, dstH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        RenderTexture prevActive = RenderTexture.active;

        Graphics.Blit(src, rt);
        RenderTexture.active = rt;

        var result = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, dstW, dstH), 0, 0);
        result.Apply(false, false);

        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }
}
