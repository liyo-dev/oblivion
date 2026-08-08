using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Core.InputGlyphs.EditorTools
{
    /// <summary>
    /// Herramienta de Editor (Tools/Input Glyphs/Generar Assets de Botones) que genera, como archivos
    /// PNG reales dentro de Assets/Resources/InputGlyphs/&lt;Familia&gt;/&lt;nombre&gt;.png, los 11
    /// iconos de botón que Core.InputGlyphs.InputGlyphService carga en tiempo de ejecución según el
    /// mando/teclado conectado (ver InputGlyphNames para los 11 nombres y su significado).
    ///
    /// - Xbox: se COPIA directamente del arte real que ya existía en Assets/Art/UI/Buttons (mismos
    ///   nombres de archivo).
    /// - PlayStation / Switch / Teclado&amp;Ratón: no había arte final para estas, así que se genera un
    ///   PLACEHOLDER dibujado por código (InputGlyphPlaceholderFactory) — sirve para jugar y probar ya
    ///   mismo. Sustitúyelo cuando tengas arte final arrastrando el PNG nuevo encima, en la misma ruta
    ///   (Assets/Resources/InputGlyphs/&lt;Familia&gt;/&lt;nombre&gt;.png) — el juego lo recoge solo, no
    ///   hace falta tocar código ni volver a abrir esta ventana.
    ///
    /// Un PNG que ya exista NUNCA se sobreescribe salvo que actives "Forzar regenerar", así que
    /// volver a abrir esta ventana (o correr "Generar todo lo que falte") no destruye arte final que
    /// ya hayas colocado a mano.
    /// </summary>
    public class InputGlyphAssetGeneratorWindow : EditorWindow
    {
        const string ResourcesRoot = "Assets/Resources/InputGlyphs";
        const string XboxSourceFolder = "Assets/Art/UI/Buttons";

        Vector2 _scroll;
        readonly Dictionary<(InputGlyphDeviceFamily, string), Texture2D> _previewCache = new();

        [MenuItem("Tools/Input Glyphs/Generar Assets de Botones...")]
        public static void Open()
        {
            var window = GetWindow<InputGlyphAssetGeneratorWindow>("Input Glyphs");
            window.minSize = new Vector2(540, 420);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Genera los sprites de botón por mando/teclado en:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(ResourcesRoot + "/<Familia>/<nombre>.png", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Xbox se copia del arte real en " + XboxSourceFolder + ".\n" +
                "PlayStation, Switch y Teclado&Ratón se generan como placeholder (dibujado por código) " +
                "hasta que los sustituyas por arte final — basta con machacar el PNG en su carpeta, " +
                "el juego lo recoge solo.\n\n" +
                "Un archivo ya existente no se toca salvo que fuerces la regeneración.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generar todo lo que falte", GUILayout.Height(28)))
                    GenerateAll(forceOverwrite: false);

                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.45f);
                if (GUILayout.Button("Forzar regenerar TODO", GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("Forzar regenerar TODO",
                        "Esto sobreescribe TODOS los PNG existentes en " + ResourcesRoot + ", incluido " +
                        "cualquier arte final que ya hayas colocado a mano. ¿Seguro?",
                        "Sí, sobreescribir todo", "Cancelar"))
                    {
                        GenerateAll(forceOverwrite: true);
                    }
                }
                GUI.backgroundColor = prevColor;
            }

            EditorGUILayout.Space(10);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (InputGlyphDeviceFamily family in Enum.GetValues(typeof(InputGlyphDeviceFamily)))
                DrawFamilySection(family);

            EditorGUILayout.EndScrollView();
        }

        void DrawFamilySection(InputGlyphDeviceFamily family)
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(family.ToString(), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Generar familia", GUILayout.Width(140)))
                        GenerateFamily(family, forceOverwrite: false);
                }

                foreach (var buttonName in InputGlyphNames.All)
                {
                    string assetPath = AssetPathFor(family, buttonName);
                    bool exists = File.Exists(AbsoluteFromAssetPath(assetPath));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var preview = LoadPreview(family, buttonName, exists);
                        var rect = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(28), GUILayout.Height(28));
                        if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                        else EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));

                        EditorGUILayout.LabelField(buttonName, GUILayout.Width(180));
                        EditorGUILayout.LabelField(exists ? "generado" : "falta",
                            exists ? EditorStyles.miniLabel : EditorStyles.miniBoldLabel, GUILayout.Width(70));

                        if (GUILayout.Button("Regenerar", GUILayout.Width(90)))
                        {
                            GenerateOne(family, buttonName, forceOverwrite: true);
                            _previewCache.Remove((family, buttonName));
                        }
                    }
                }
            }
        }

        Texture2D LoadPreview(InputGlyphDeviceFamily family, string buttonName, bool exists)
        {
            if (!exists) return null;
            var key = (family, buttonName);
            if (_previewCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPathFor(family, buttonName));
            _previewCache[key] = tex;
            return tex;
        }

        // ── Generación ───────────────────────────────────────────────────────

        void GenerateAll(bool forceOverwrite)
        {
            foreach (InputGlyphDeviceFamily family in Enum.GetValues(typeof(InputGlyphDeviceFamily)))
                GenerateFamily(family, forceOverwrite);

            _previewCache.Clear();
            Debug.Log("[InputGlyphAssetGeneratorWindow] Generación completa.");
            Repaint();
        }

        void GenerateFamily(InputGlyphDeviceFamily family, bool forceOverwrite)
        {
            foreach (var buttonName in InputGlyphNames.All)
                GenerateOne(family, buttonName, forceOverwrite);

            _previewCache.Clear();
            Repaint();
        }

        void GenerateOne(InputGlyphDeviceFamily family, string buttonName, bool forceOverwrite)
        {
            string assetPath = AssetPathFor(family, buttonName);
            string destAbsolute = AbsoluteFromAssetPath(assetPath);

            if (File.Exists(destAbsolute) && !forceOverwrite)
                return;

            EnsureFolder($"{ResourcesRoot}/{family}");

            bool wroteFromSource = family == InputGlyphDeviceFamily.Xbox && TryCopyXboxSource(buttonName, destAbsolute);
            if (!wroteFromSource)
            {
                var tex = InputGlyphPlaceholderFactory.BuildTexture(family, buttonName);
                File.WriteAllBytes(destAbsolute, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }

            AssetDatabase.SaveAssets();
            ImportAsSprite(assetPath);
        }

        bool TryCopyXboxSource(string buttonName, string destAbsolute)
        {
            string sourceAssetPath = $"{XboxSourceFolder}/{buttonName}.png";
            string sourceAbsolute = AbsoluteFromAssetPath(sourceAssetPath);

            if (!File.Exists(sourceAbsolute))
            {
                Debug.LogWarning($"[InputGlyphAssetGeneratorWindow] No se encontró arte Xbox real para " +
                                  $"'{buttonName}' en {XboxSourceFolder}. Genero un placeholder en su lugar.");
                return false;
            }

            File.Copy(sourceAbsolute, destAbsolute, true);
            return true;
        }

        void ImportAsSprite(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }

        static string AssetPathFor(InputGlyphDeviceFamily family, string buttonName) =>
            $"{ResourcesRoot}/{family}/{buttonName}.png";

        static string AbsoluteFromAssetPath(string assetPath)
        {
            // assetPath siempre empieza por "Assets/" (rutas de proyecto de Unity).
            string relative = assetPath.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, relative);
        }

        /// <summary>
        /// Crea (si hace falta) todas las carpetas intermedias de <paramref name="assetFolderPath"/>
        /// usando AssetDatabase.CreateFolder — a diferencia de Directory.CreateDirectory, esto deja la
        /// carpeta nueva ya registrada en Unity al instante, así que el AssetDatabase.ImportAsset()
        /// que viene justo después no falla por no reconocer todavía la carpeta que la contiene.
        /// </summary>
        static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            int slash = assetFolderPath.LastIndexOf('/');
            string parent = assetFolderPath.Substring(0, slash);
            string leaf = assetFolderPath.Substring(slash + 1);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
