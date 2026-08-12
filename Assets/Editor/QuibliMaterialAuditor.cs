using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditor de migración a Quibli.
///
/// Escanea TODOS los materiales del proyecto (solo lectura, no modifica nada) y los
/// clasifica según su shader actual:
///   - Ya en Quibli/Stylized Lit (marcando si les falta Albedo o Gradient Ramp, que es
///     la causa más común de que un material "convertido" se vea blanco: al reasignar
///     el shader de un material ya existente, Unity NO rellena las propiedades nuevas
///     con los valores por defecto del shader — una textura sin asignar se muestrea
///     como blanco en tiempo de ejecución).
///   - Toon Shader de Ciro Continisio (sistema de shading paralelo, pendiente de migrar).
///   - Shaders built-in de Unity, URP nativo, o de paquetes de terceros (agrupados
///     automáticamente por carpeta) — pendientes de convertir a Quibli.
///
/// Además, con "Analizar Uso" cruza cada material contra las dependencias reales de
/// todas las escenas y prefabs del proyecto, para distinguir lo que de verdad se ve en
/// el juego de lo que son piezas sueltas de un pack que nunca llegaron a colocarse
/// (relevante sobre todo para packs grandes con materiales embebidos en FBX, como
/// Fantasy_Kingdom_Pack).
///
/// Uso: menú El Sendero/Materiales/Auditor de Migración a Quibli.
/// Genera además un CSV exportable con el detalle completo para planificar la conversión.
/// </summary>
public class QuibliMaterialAuditor : EditorWindow
{
    private const string QUIBLI_PATH_PREFIX = "Assets/Plugins/Quibli/";
    private const string CIRO_TOON_PATH_PREFIX = "Assets/Plugins/CiroContinisio/";

    private const string USAGE_SCENE = "En escena";
    private const string USAGE_PREFAB = "En prefab (no visto en escena)";
    private const string USAGE_NONE = "Sin uso detectado";
    private const string USAGE_UNKNOWN = "(sin analizar)";

    private class MaterialInfo
    {
        public string Path;
        public string Name;
        public string ShaderName;
        public string Category;
        public List<string> Risks;
        public string TextureSummary;
        public string Usage = USAGE_UNKNOWN;
        public bool IsSpecial;
        public string SpecialReason;
    }

    private List<MaterialInfo> _results = new List<MaterialInfo>();
    private Dictionary<string, int> _categoryCounts = new Dictionary<string, int>();
    private bool _hasScanned;
    private bool _usageAnalyzed;
    private int _skippedDemoScenes;
    private int _skippedDemoPrefabs;
    private Vector2 _scrollPos;
    private string _filterText = "";
    private bool _onlyWithRisks = true;
    private bool _hideSpecial;
    private int _usageFilterIndex; // 0 = todos, 1 = solo con uso, 2 = solo sin uso

    private static readonly string[] UsageFilterOptions = { "Todos", "Solo usados", "Solo sin uso detectado" };

    private const int MAX_ROWS_DRAWN = 400;

    [MenuItem("El Sendero/Materiales/Auditor de Migración a Quibli")]
    public static void ShowWindow()
    {
        var window = GetWindow<QuibliMaterialAuditor>("Auditor Quibli");
        window.minSize = new Vector2(700, 560);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Auditor de Migración a Quibli", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Escanea todos los materiales del proyecto y los clasifica según su shader actual. " +
            "Es de SOLO LECTURA: no modifica ningún material, solo genera un informe para " +
            "planificar la conversión a Quibli/Stylized Lit.",
            MessageType.Info);

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1. Auditar Proyecto", GUILayout.Height(28)))
            {
                Scan();
            }

            GUI.enabled = _hasScanned;
            if (GUILayout.Button("2. Analizar Uso (Escenas/Prefabs)", GUILayout.Height(28)))
            {
                AnalyzeUsage();
            }

            GUI.enabled = _hasScanned;
            if (GUILayout.Button("Exportar CSV", GUILayout.Height(28)))
            {
                ExportCsv();
            }
            GUI.enabled = true;
        }

        if (!_hasScanned)
        {
            EditorGUILayout.HelpBox("Pulsa 'Auditar Proyecto' para empezar.", MessageType.None);
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Total de materiales analizados: {_results.Count}", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var kvp in _categoryCounts.OrderByDescending(k => k.Value))
        {
            EditorGUILayout.LabelField($"   •  {kvp.Key}: {kvp.Value}");
        }

        int riskCount = _results.Count(r => r.Risks.Count > 0);
        EditorGUILayout.Space(6);
        if (riskCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{riskCount} material(es) con avisos: sin Albedo, sin Gradient Ramp, " +
                "o pendientes de migrar desde otro shader.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("No se encontraron avisos.", MessageType.None);
        }

        int specialCount = _results.Count(r => r.IsSpecial);
        if (specialCount > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                $"{specialCount} material(es) especiales detectados (transparentes, cutout o con nombre de " +
                "agua/cristal). Suelen romperse con más facilidad al cambiar de shader — déjalos para el " +
                "final, fuera del primer lote de conversión.",
                MessageType.Warning);
        }

        if (_usageAnalyzed)
        {
            EditorGUILayout.Space(6);
            int enScena = _results.Count(r => r.Usage == USAGE_SCENE);
            int enPrefab = _results.Count(r => r.Usage == USAGE_PREFAB);
            int sinUso = _results.Count(r => r.Usage == USAGE_NONE);
            EditorGUILayout.LabelField("Uso real en el proyecto:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"   •  {USAGE_SCENE}: {enScena}");
            EditorGUILayout.LabelField($"   •  {USAGE_PREFAB}: {enPrefab}");
            EditorGUILayout.LabelField($"   •  {USAGE_NONE}: {sinUso}");
            EditorGUILayout.LabelField(
                $"   (excluidas de este análisis por ser demo/ejemplo de terceros: {_skippedDemoScenes} escena(s), {_skippedDemoPrefabs} prefab(s))",
                EditorStyles.miniLabel);

            // Resumen específico para packs grandes con muchos materiales embebidos
            // (p.ej. Fantasy_Kingdom_Pack), que es donde más importa saber cuánto es
            // realmente necesario tocar.
            var byTopPack = _results
                .GroupBy(r => GetPackageGroup(r.Path))
                .Where(g => g.Count() >= 30)
                .OrderByDescending(g => g.Count());

            bool printedHeader = false;
            foreach (var g in byTopPack)
            {
                if (!printedHeader)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Desglose de uso por carpeta/pack (≥30 materiales):", EditorStyles.boldLabel);
                    printedHeader = true;
                }

                int total = g.Count();
                int usados = g.Count(r => r.Usage == USAGE_SCENE || r.Usage == USAGE_PREFAB);
                int especiales = g.Count(r => r.IsSpecial);
                EditorGUILayout.LabelField(
                    $"   •  {g.Key}: {total} totales, {usados} usados, {total - usados} sin uso detectado, {especiales} especiales");
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Aún no se ha analizado el uso real. Pulsa '2. Analizar Uso' para cruzar cada " +
                "material contra las escenas y prefabs del proyecto (puede tardar unos segundos).",
                MessageType.None);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Detalle", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Filtro:", GUILayout.Width(40));
            _filterText = EditorGUILayout.TextField(_filterText);
            _onlyWithRisks = EditorGUILayout.ToggleLeft("Solo con avisos", _onlyWithRisks, GUILayout.Width(110));
            _hideSpecial = EditorGUILayout.ToggleLeft("Ocultar especiales", _hideSpecial, GUILayout.Width(130));
        }

        if (_usageAnalyzed)
        {
            _usageFilterIndex = EditorGUILayout.Popup("Filtro de uso", _usageFilterIndex, UsageFilterOptions);
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        int drawn = 0;
        foreach (var info in _results)
        {
            if (_onlyWithRisks && info.Risks.Count == 0) continue;
            if (_hideSpecial && info.IsSpecial) continue;

            if (_usageAnalyzed)
            {
                bool used = info.Usage == USAGE_SCENE || info.Usage == USAGE_PREFAB;
                if (_usageFilterIndex == 1 && !used) continue;
                if (_usageFilterIndex == 2 && used) continue;
            }

            if (!string.IsNullOrEmpty(_filterText) &&
                info.Path.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) < 0 &&
                info.Category.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) < 0 &&
                info.ShaderName.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            drawn++;
            if (drawn > MAX_ROWS_DRAWN)
            {
                EditorGUILayout.HelpBox(
                    $"Mostrando los primeros {MAX_ROWS_DRAWN} resultados. Afina el filtro para ver el resto.",
                    MessageType.None);
                break;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(info.Name, EditorStyles.boldLabel, GUILayout.Width(200));
                    EditorGUILayout.LabelField(info.Category, GUILayout.Width(260));
                    if (GUILayout.Button("Seleccionar", GUILayout.Width(90)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<Material>(info.Path);
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }

                EditorGUILayout.LabelField($"Shader: {info.ShaderName}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Ruta: {info.Path}", EditorStyles.miniLabel);

                if (_usageAnalyzed)
                {
                    EditorGUILayout.LabelField($"Uso: {info.Usage}", EditorStyles.miniLabel);
                }

                if (info.IsSpecial)
                {
                    EditorGUILayout.LabelField($"⚠ Especial ({info.SpecialReason}) — dejar para el final", EditorStyles.miniBoldLabel);
                }

                if (info.Risks.Count > 0)
                {
                    EditorGUILayout.LabelField("Avisos: " + string.Join("  ·  ", info.Risks), EditorStyles.miniBoldLabel);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        _results = new List<MaterialInfo>();
        _categoryCounts = new Dictionary<string, int>();
        _usageAnalyzed = false;

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int total = guids.Length;

        try
        {
            for (int i = 0; i < total; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (i % 20 == 0)
                {
                    bool cancel = EditorUtility.DisplayCancelableProgressBar(
                        "Auditando materiales de Quibli", path, (float)i / total);
                    if (cancel) break;
                }

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                List<string> risks;
                string category = ClassifyMaterial(mat, path, out risks);
                string texSummary = GetTextureSummary(mat);

                string specialReason;
                bool isSpecial = IsSpecialMaterial(mat, out specialReason);
                if (isSpecial && category.StartsWith("Fuera de alcance", StringComparison.Ordinal) == false)
                {
                    risks.Add($"Especial (transparente/agua) — dejar para el final ({specialReason})");
                }

                var info = new MaterialInfo
                {
                    Path = path,
                    Name = mat.name,
                    ShaderName = mat.shader != null ? mat.shader.name : "(sin shader)",
                    Category = category,
                    Risks = risks,
                    TextureSummary = texSummary,
                    IsSpecial = isSpecial,
                    SpecialReason = specialReason
                };

                _results.Add(info);

                if (!_categoryCounts.ContainsKey(category)) _categoryCounts[category] = 0;
                _categoryCounts[category]++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        _results.Sort((a, b) => string.Compare(a.Category + a.Path, b.Category + b.Path, StringComparison.Ordinal));
        _hasScanned = true;
    }

    /// <summary>
    /// Cruza cada material contra las dependencias reales (recursivas) de todas las
    /// escenas y todos los prefabs del proyecto. Un material que no aparece en ninguna
    /// de las dos listas es candidato a "no se ve en el juego" — típicamente piezas
    /// sueltas de un pack grande que nunca se llegaron a colocar.
    /// </summary>
    private void AnalyzeUsage()
    {
        var sceneDependencies = new HashSet<string>();
        var prefabDependencies = new HashSet<string>();
        int skippedScenes = 0;
        int skippedPrefabs = 0;

        try
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                EditorUtility.DisplayProgressBar("Analizando uso", $"Escena: {scenePath}", (float)i / sceneGuids.Length * 0.5f);

                // Las escenas/prefabs de demostración que vienen DENTRO de los propios
                // packs comprados (p.ej. Fantasy_Kingdom_Pack/Demo) suelen colocar
                // literalmente todas las piezas del pack solo para lucirlas — si las
                // contamos, casi todo sale "usado" aunque tú nunca lo hayas puesto en
                // el juego. Se excluyen de la detección de uso real.
                if (IsVendorDemoPath(scenePath)) { skippedScenes++; continue; }

                foreach (var dep in AssetDatabase.GetDependencies(scenePath, true))
                {
                    sceneDependencies.Add(dep);
                }
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                EditorUtility.DisplayProgressBar("Analizando uso", $"Prefab: {prefabPath}", 0.5f + (float)i / prefabGuids.Length * 0.5f);

                if (IsVendorDemoPath(prefabPath)) { skippedPrefabs++; continue; }

                foreach (var dep in AssetDatabase.GetDependencies(prefabPath, true))
                {
                    prefabDependencies.Add(dep);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        foreach (var info in _results)
        {
            if (sceneDependencies.Contains(info.Path)) info.Usage = USAGE_SCENE;
            else if (prefabDependencies.Contains(info.Path)) info.Usage = USAGE_PREFAB;
            else info.Usage = USAGE_NONE;
        }

        _usageAnalyzed = true;
        _skippedDemoScenes = skippedScenes;
        _skippedDemoPrefabs = skippedPrefabs;
        Debug.Log($"[QuibliMaterialAuditor] Análisis de uso completado. Excluidas por ser demo/ejemplo de un " +
                   $"pack de terceros: {skippedScenes} escena(s), {skippedPrefabs} prefab(s).");
    }

    /// <summary>
    /// Detecta si una ruta cuelga de una carpeta de demo/ejemplo típica de un asset
    /// comprado (Demo, Demos, Example, Examples, Sample, Samples, Showcase), para no
    /// contarla como "uso real" del juego.
    /// </summary>
    private static bool IsVendorDemoPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return false;

        string normalized = "/" + assetPath.Replace('\\', '/').ToLowerInvariant() + "/";
        string[] markers = { "/demo/", "/demos/", "/example/", "/examples/", "/sample/", "/samples/", "/showcase/" };

        foreach (var marker in markers)
        {
            if (normalized.Contains(marker)) return true;
        }

        return false;
    }

    private static string ClassifyMaterial(Material mat, string matPath, out List<string> risks)
    {
        risks = new List<string>();

        var shader = mat.shader;
        if (shader == null)
        {
            risks.Add("Material sin shader asignado");
            return "⚠ Sin shader";
        }

        string shaderPath = AssetDatabase.GetAssetPath(shader);

        // --- Materiales de Quibli ---
        if (shaderPath.StartsWith(QUIBLI_PATH_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            if (mat.HasProperty("_GradientRamp"))
            {
                // Stylized Lit (o una variante que comparte la propiedad de rampa).
                if (IsTextureEmpty(mat, "_BaseMap")) risks.Add("Sin Albedo (_BaseMap)");
                if (IsTextureEmpty(mat, "_GradientRamp")) risks.Add("Sin Gradient Ramp");
                return "Quibli — Stylized Lit";
            }

            return $"Quibli — otro shader ({shader.name})";
        }

        // --- Toon Shader de Ciro Continisio (a migrar según lo decidido) ---
        if (shaderPath.StartsWith(CIRO_TOON_PATH_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("Sistema de shading paralelo — pendiente de migrar a Quibli");
            return "Toon (Ciro Continisio) — a migrar";
        }

        // --- Shaders de partículas / VFX / skybox / UI: quedan fuera del alcance ---
        if (IsOutOfScopeShader(shader.name))
        {
            return $"Fuera de alcance — VFX/UI/Skybox ({shader.name})";
        }

        // --- Shaders built-in de Unity (Standard, etc.) ---
        if (string.IsNullOrEmpty(shaderPath) || shaderPath.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("Shader built-in de Unity — revisar si necesita migrarse");
            return $"Built-in de Unity ({shader.name})";
        }

        // --- URP nativo (Lit, Simple Lit, Unlit, Terrain Lit, etc.) ---
        if (shaderPath.StartsWith("Packages/com.unity.render-pipelines.universal/", StringComparison.OrdinalIgnoreCase))
        {
            risks.Add("URP nativo — pendiente de migrar a Quibli");
            return $"URP nativo ({shader.name})";
        }

        // --- Cualquier otro shader: agrupar automáticamente por paquete/carpeta ---
        string group = GetPackageGroup(shaderPath);
        risks.Add("Shader de terceros — pendiente de migrar a Quibli");
        return $"Otro — {group} ({shader.name})";
    }

    /// <summary>
    /// Shaders que por su naturaleza (partículas, skybox, texto/UI) no deben pasar por
    /// Quibli/Stylized Lit aunque sean shaders "normales" — se quedan tal cual están.
    /// </summary>
    private static bool IsOutOfScopeShader(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName)) return false;

        string[] outOfScopeMarkers =
        {
            "Particles/", "Legacy Shaders/Particles", "Mobile/Particles",
            "Skybox/", "FX/", "TextMeshPro", "GUI/Text", "UI/",
            "Universal Render Pipeline/Particles"
        };

        foreach (var marker in outOfScopeMarkers)
        {
            if (shaderName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    /// <summary>
    /// Detecta materiales "especiales" que suelen romperse con más facilidad al cambiar
    /// de shader: transparentes, cutout, o con nombre que sugiere agua/cristal. Se marcan
    /// aparte para dejarlos fuera del primer lote de conversión, tal como se decidió.
    /// </summary>
    private static bool IsSpecialMaterial(Material mat, out string reason)
    {
        reason = null;
        var motives = new List<string>();

        int queue = mat.renderQueue;
        if (queue >= 2450) motives.Add($"render queue {queue}");

        bool keywordAlpha = mat.IsKeywordEnabled("_ALPHATEST_ON")
                          || mat.IsKeywordEnabled("_ALPHABLEND_ON")
                          || mat.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")
                          || mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
        if (keywordAlpha) motives.Add("keyword de alpha activo");

        if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f)
            motives.Add("Surface = Transparent");

        if (mat.HasProperty("_Mode") && mat.GetFloat("_Mode") >= 2f)
            motives.Add("Mode = Fade/Transparent");

        string haystack = ((mat.name ?? "") + " " + (mat.shader != null ? mat.shader.name : "")).ToLowerInvariant();
        string[] markers = { "water", "agua", "glass", "cristal", "vidrio", "ocean", "river", "rio", "lago", "océano" };
        foreach (var marker in markers)
        {
            if (haystack.Contains(marker))
            {
                motives.Add($"nombre sugiere '{marker}'");
                break;
            }
        }

        if (motives.Count == 0) return false;

        reason = string.Join(", ", motives);
        return true;
    }

    private static string GetPackageGroup(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return "Desconocido";

        string[] parts = assetPath.Split('/');
        // parts[0] es "Assets" o "Packages"; usamos los dos segmentos siguientes como
        // nombre de grupo (por ejemplo "Art/Fantasy_Kingdom_Pack").
        if (parts.Length >= 3) return parts[1] + "/" + parts[2];
        if (parts.Length == 2) return parts[1];
        return assetPath;
    }

    private static bool IsTextureEmpty(Material mat, string propertyName)
    {
        if (!mat.HasProperty(propertyName)) return false;
        return mat.GetTexture(propertyName) == null;
    }

    private static string GetTextureSummary(Material mat)
    {
        var shader = mat.shader;
        if (shader == null) return "";

        var sb = new StringBuilder();
        int count = shader.GetPropertyCount();

        for (int i = 0; i < count; i++)
        {
            if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;

            string name = shader.GetPropertyName(i);
            if (name.StartsWith("unity_", StringComparison.OrdinalIgnoreCase)) continue;

            var tex = mat.GetTexture(name);
            sb.Append(name).Append(tex != null ? "=OK" : "=VACIO").Append("; ");
        }

        return sb.ToString();
    }

    private void ExportCsv()
    {
        string dir = Path.Combine(Application.dataPath, "..", "ReportesMateriales");
        Directory.CreateDirectory(dir);

        string fileName = $"auditoria_quibli_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string fullPath = Path.Combine(dir, fileName);

        using (var writer = new StreamWriter(fullPath, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("Ruta;Nombre;Shader;Categoria;Uso;Especial;Avisos;Texturas");

            foreach (var info in _results)
            {
                string risksJoined = string.Join(" | ", info.Risks);
                writer.WriteLine(string.Join(";",
                    EscapeCsv(info.Path),
                    EscapeCsv(info.Name),
                    EscapeCsv(info.ShaderName),
                    EscapeCsv(info.Category),
                    EscapeCsv(info.Usage),
                    EscapeCsv(info.IsSpecial ? info.SpecialReason : ""),
                    EscapeCsv(risksJoined),
                    EscapeCsv(info.TextureSummary)));
            }
        }

        Debug.Log($"[QuibliMaterialAuditor] Informe exportado a: {fullPath}");
        EditorUtility.RevealInFinder(fullPath);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (value.Contains(";") || value.Contains("\"") || value.Contains("\n"))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }
}
