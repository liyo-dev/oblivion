using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Conversor por lotes a Quibli/Stylized Lit.
///
/// Parte de un CSV exportado por QuibliMaterialAuditor (el usuario decide qué lista de
/// materiales tocar — este script NO vuelve a decidir el alcance por su cuenta) y, para
/// cada material de esa lista, reasigna el shader a "Quibli/Stylized Lit" mapeando las
/// texturas/colores desde el shader de origen y fijando EXPLÍCITAMENTE cada propiedad
/// relevante (albedo, normal, emission, estado de blending/opacidad, toggles de Quibli
/// y la Gradient Ramp).
///
/// Por qué se fija todo explícitamente: reasignar el shader de un material YA EXISTENTE
/// no aplica los valores por defecto declarados en el ShaderLab del shader nuevo — las
/// propiedades que no existían antes quedan sin enlazar, y eso es lo que ha estado
/// causando materiales blancos/rotos en los intentos anteriores. Aquí no se deja nada
/// al azar: cada propiedad relevante se establece a un valor conocido, y la Gradient
/// Ramp se genera siempre (nunca se deja vacía), replicando el mismo formato que usa el
/// editor nativo de Quibli (MaterialGradientDrawer) para que siga siendo editable a mano
/// con su gradiente después.
///
/// Uso: menú El Sendero/Materiales/Convertir a Quibli (desde CSV).
/// Flujo: 1) Cargar CSV  2) Vista previa (dry-run, no toca nada)  3) Convertir.
/// </summary>
public class QuibliBatchConverter : EditorWindow
{
    private const string QUIBLI_SHADER_NAME = "Quibli/Stylized Lit";
    private const int RAMP_RESOLUTION = 256;

    private static readonly string[] AlbedoCandidates =
        { "_BaseMap", "_MainTex", "_BaseColorMap", "_AlbedoMap", "_Albedo", "_DiffuseMap", "_Diffuse", "_ColorMap" };
    private static readonly string[] BaseColorCandidates =
        { "_BaseColor", "_Color", "_TintColor", "_MainColor" };
    private static readonly string[] NormalCandidates =
        { "_BumpMap", "_NormalMap", "_Normal" };
    private static readonly string[] EmissionMapCandidates =
        { "_EmissionMap", "_EmissionTex" };
    private static readonly string[] EmissionColorCandidates =
        { "_EmissionColor" };

    private class CandidateRow
    {
        public string Path;
        public string Category;
        public string Usage;
        public string Special;
    }

    private class PreviewEntry
    {
        public string Path;
        public string OldShaderName;
        public string AlbedoSource;
        public string NormalSource;
        public string EmissionSource;
        public List<string> Warnings = new List<string>();
        public bool WillSkip;
        public string SkipReason;
    }

    private string _csvPath = "";
    private List<CandidateRow> _candidates = new List<CandidateRow>();
    private List<PreviewEntry> _preview = new List<PreviewEntry>();
    private bool _dryRunDone;
    private bool _backupConfirmed;
    private int _testLimit = 20;
    private Vector2 _scrollPos;

    [MenuItem("El Sendero/Materiales/Convertir a Quibli (desde CSV)")]
    public static void ShowWindow()
    {
        var window = GetWindow<QuibliBatchConverter>("Convertir a Quibli");
        window.minSize = new Vector2(700, 560);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Conversor por lotes a Quibli/Stylized Lit", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1) Carga el CSV del Auditor de Migración a Quibli (con las filas ya revisadas). " +
            "2) Genera la vista previa (no modifica nada). 3) Convierte. Se recomienda hacer commit " +
            "en Git antes de convertir, y probar primero con un límite pequeño.",
            MessageType.Info);

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("CSV:", GUILayout.Width(35));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(_csvPath) ? "(ninguno cargado)" : _csvPath, EditorStyles.miniLabel);
            if (GUILayout.Button("Cargar CSV de auditoría...", GUILayout.Width(200)))
            {
                LoadCsv();
            }
        }

        if (_candidates.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Carga un CSV exportado por el Auditor de Migración a Quibli. Se incluyen solo las filas " +
                "que NO estén marcadas como 'Fuera de alcance', NO estén ya en Quibli, NO tengan nada en " +
                "la columna 'Especial', y su 'Uso' no sea 'Sin uso detectado'.",
                MessageType.None);
            return;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField($"Candidatos cargados del CSV: {_candidates.Count}", EditorStyles.boldLabel);

        EditorGUILayout.Space(6);
        _testLimit = EditorGUILayout.IntField("Límite de prueba (0 = todos)", _testLimit);
        EditorGUILayout.HelpBox(
            "Prueba primero con un número pequeño (p.ej. 20), revisa el resultado en Unity, y si todo " +
            "va bien vuelve a lanzar con 0 para el resto.",
            MessageType.None);

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Generar Vista Previa (dry-run, no modifica nada)", GUILayout.Height(28)))
        {
            RunDryRun();
        }

        if (!_dryRunDone)
        {
            EditorGUILayout.HelpBox("Genera la vista previa antes de convertir.", MessageType.None);
            return;
        }

        int withAlbedo = _preview.Count(p => !p.WillSkip && p.AlbedoSource != null);
        int withoutAlbedo = _preview.Count(p => !p.WillSkip && p.AlbedoSource == null);
        int withNormal = _preview.Count(p => !p.WillSkip && p.NormalSource != null);
        int withEmission = _preview.Count(p => !p.WillSkip && p.EmissionSource != null);
        int skipped = _preview.Count(p => p.WillSkip);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Resumen de la vista previa:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"   •  Se convertirán: {_preview.Count - skipped}");
        EditorGUILayout.LabelField($"   •  Con Albedo mapeado: {withAlbedo}   |   SIN Albedo detectado: {withoutAlbedo}");
        EditorGUILayout.LabelField($"   •  Con Normal Map: {withNormal}   |   Con Emission: {withEmission}");
        if (skipped > 0)
        {
            EditorGUILayout.HelpBox(
                $"{skipped} material(es) se omitirán por seguridad (ya están en Quibli, no se encontraron, " +
                "o al re-comprobar parecen transparentes/agua aunque el CSV no los marcara como especiales).",
                MessageType.Warning);
        }
        if (withoutAlbedo > 0)
        {
            EditorGUILayout.HelpBox(
                $"{withoutAlbedo} material(es) no tienen ninguna textura de albedo reconocible en su shader " +
                "de origen — se convertirán igualmente (con el color base que tuvieran) pero conviene " +
                "revisarlos a mano después.",
                MessageType.None);
        }

        EditorGUILayout.Space(10);
        _backupConfirmed = EditorGUILayout.ToggleLeft(
            "He hecho commit/backup de los cambios pendientes en Git antes de continuar.", _backupConfirmed);

        GUI.enabled = _backupConfirmed;
        if (GUILayout.Button("CONVERTIR AHORA (modifica materiales)", GUILayout.Height(32)))
        {
            int toConvert = _preview.Count(p => !p.WillSkip);
            int limited = _testLimit > 0 ? Mathf.Min(_testLimit, toConvert) : toConvert;
            bool confirmed = EditorUtility.DisplayDialog(
                "Confirmar conversión",
                $"Se van a convertir {limited} material(es) a {QUIBLI_SHADER_NAME}.\n\n" +
                "Esta acción modifica archivos .mat directamente. Asegúrate de tener los cambios " +
                "anteriores commiteados en Git para poder revertir si algo no se ve bien.\n\n¿Continuar?",
                "Sí, convertir", "Cancelar");
            if (confirmed)
            {
                RunConversion();
            }
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Detalle de la vista previa", EditorStyles.boldLabel);
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        int shown = 0;
        foreach (var entry in _preview)
        {
            shown++;
            if (shown > 300)
            {
                EditorGUILayout.HelpBox("Mostrando los primeros 300. El resto se procesará igual al convertir.", MessageType.None);
                break;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField(entry.Path, EditorStyles.miniBoldLabel);
                if (entry.WillSkip)
                {
                    EditorGUILayout.LabelField($"OMITIDO: {entry.SkipReason}", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        $"Shader anterior: {entry.OldShaderName}  →  {QUIBLI_SHADER_NAME}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(
                        $"Albedo: {entry.AlbedoSource ?? "(no encontrado)"}   " +
                        $"Normal: {entry.NormalSource ?? "-"}   " +
                        $"Emission: {entry.EmissionSource ?? "-"}", EditorStyles.miniLabel);
                    if (entry.Warnings.Count > 0)
                    {
                        EditorGUILayout.LabelField("Avisos: " + string.Join(" · ", entry.Warnings), EditorStyles.miniLabel);
                    }
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------
    // Carga de CSV
    // ------------------------------------------------------------------

    private void LoadCsv()
    {
        string startDir = Path.Combine(Application.dataPath, "..", "ReportesMateriales");
        string path = EditorUtility.OpenFilePanel("Seleccionar CSV del Auditor de Quibli", startDir, "csv");
        if (string.IsNullOrEmpty(path)) return;

        string text = File.ReadAllText(path, Encoding.UTF8);
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);

        string[] headers;
        var rows = ParseCsv(text, out headers);

        _candidates = new List<CandidateRow>();
        foreach (var row in rows)
        {
            string category = row.TryGetValue("Categoria", out var c) ? c : "";
            string usage = row.TryGetValue("Uso", out var u) ? u : "";
            string special = row.TryGetValue("Especial", out var s) ? s : "";
            string matPath = row.TryGetValue("Ruta", out var p) ? p : "";

            if (string.IsNullOrEmpty(matPath)) continue;
            if (category.StartsWith("Fuera de alcance", StringComparison.Ordinal)) continue;
            if (category.StartsWith("Quibli", StringComparison.Ordinal)) continue;
            if (category.StartsWith("⚠", StringComparison.Ordinal)) continue;
            if (!string.IsNullOrEmpty(special)) continue;
            if (usage == "Sin uso detectado") continue;

            _candidates.Add(new CandidateRow { Path = matPath, Category = category, Usage = usage, Special = special });
        }

        _csvPath = path;
        _dryRunDone = false;
        _preview.Clear();

        Debug.Log($"[QuibliBatchConverter] CSV cargado: {_candidates.Count} material(es) candidato(s) de {rows.Count} filas totales.");
    }

    private static List<Dictionary<string, string>> ParseCsv(string text, out string[] headers)
    {
        var records = new List<List<string>>();
        var field = new StringBuilder();
        var record = new List<string>();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
                continue;
            }

            if (ch == '"') { inQuotes = true; continue; }
            if (ch == ';') { record.Add(field.ToString()); field.Clear(); continue; }
            if (ch == '\r') continue;
            if (ch == '\n')
            {
                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                record = new List<string>();
                continue;
            }

            field.Append(ch);
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        headers = records.Count > 0 ? records[0].ToArray() : new string[0];

        var result = new List<Dictionary<string, string>>();
        for (int r = 1; r < records.Count; r++)
        {
            if (records[r].Count == 1 && records[r][0].Length == 0) continue; // línea vacía final
            var dict = new Dictionary<string, string>();
            for (int c = 0; c < headers.Length && c < records[r].Count; c++) dict[headers[c]] = records[r][c];
            result.Add(dict);
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Vista previa (dry-run)
    // ------------------------------------------------------------------

    private void RunDryRun()
    {
        _preview = new List<PreviewEntry>();

        try
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                var cand = _candidates[i];
                if (i % 20 == 0)
                {
                    EditorUtility.DisplayProgressBar("Generando vista previa", cand.Path, (float)i / _candidates.Count);
                }

                var entry = new PreviewEntry { Path = cand.Path };

                var mat = AssetDatabase.LoadAssetAtPath<Material>(cand.Path);
                if (mat == null)
                {
                    entry.WillSkip = true;
                    entry.SkipReason = "No se encontró el material en esa ruta (¿se movió o se borró?).";
                    _preview.Add(entry);
                    continue;
                }

                entry.OldShaderName = mat.shader != null ? mat.shader.name : "(sin shader)";

                if (mat.shader != null && mat.shader.name == QUIBLI_SHADER_NAME)
                {
                    entry.WillSkip = true;
                    entry.SkipReason = "Ya está en Quibli/Stylized Lit.";
                    _preview.Add(entry);
                    continue;
                }

                if (IsLikelySpecial(mat, out string specialReason))
                {
                    entry.WillSkip = true;
                    entry.SkipReason = $"Parece transparente/agua ({specialReason}) — revisar aparte.";
                    _preview.Add(entry);
                    continue;
                }

                var albedoTex = FindFirstTexture(mat, AlbedoCandidates, out string albedoProp);
                entry.AlbedoSource = albedoTex != null ? albedoProp : null;
                if (albedoTex == null) entry.Warnings.Add("Sin textura de Albedo detectada en el shader original");

                var normalTex = FindFirstTexture(mat, NormalCandidates, out string normalProp);
                entry.NormalSource = normalTex != null ? normalProp : null;

                var emissionTex = FindFirstTexture(mat, EmissionMapCandidates, out string emissionProp);
                var emissionColor = FindFirstColor(mat, EmissionColorCandidates, out _);
                bool hasEmission = emissionTex != null || (emissionColor.HasValue && ColorHasEnergy(emissionColor.Value));
                entry.EmissionSource = hasEmission ? (emissionProp ?? "_EmissionColor") : null;

                _preview.Add(entry);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        _dryRunDone = true;
    }

    // ------------------------------------------------------------------
    // Conversión real
    // ------------------------------------------------------------------

    private void RunConversion()
    {
        var quibliShader = Shader.Find(QUIBLI_SHADER_NAME);
        if (quibliShader == null)
        {
            EditorUtility.DisplayDialog("Error", $"No se encontró el shader '{QUIBLI_SHADER_NAME}'.", "OK");
            return;
        }

        var toConvert = _preview.Where(p => !p.WillSkip).ToList();
        int limit = _testLimit > 0 ? Mathf.Min(_testLimit, toConvert.Count) : toConvert.Count;

        var logLines = new List<string> { "Ruta;ShaderAnterior;Albedo;Normal;Emission;Advertencias" };
        int converted = 0;
        int errors = 0;

        try
        {
            for (int i = 0; i < limit; i++)
            {
                var entry = toConvert[i];
                EditorUtility.DisplayProgressBar("Convirtiendo a Quibli", entry.Path, (float)i / limit);

                try
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(entry.Path);
                    if (mat == null) { errors++; continue; }

                    var warnings = new List<string>();
                    ConvertMaterial(mat, quibliShader, warnings);

                    logLines.Add(string.Join(";",
                        entry.Path, entry.OldShaderName, entry.AlbedoSource ?? "", entry.NormalSource ?? "",
                        entry.EmissionSource ?? "", string.Join(" | ", warnings)));

                    converted++;
                }
                catch (Exception ex)
                {
                    errors++;
                    Debug.LogError($"[QuibliBatchConverter] Error convirtiendo '{entry.Path}': {ex}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string dir = Path.Combine(Application.dataPath, "..", "ReportesMateriales");
        Directory.CreateDirectory(dir);
        string logPath = Path.Combine(dir, $"conversion_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllLines(logPath, logLines, new UTF8Encoding(true));

        Debug.Log($"[QuibliBatchConverter] Conversión completada: {converted} convertido(s), {errors} error(es). Log: {logPath}");
        EditorUtility.DisplayDialog("Conversión completada",
            $"{converted} material(es) convertido(s), {errors} error(es).\n\nLog: {logPath}", "OK");
        EditorUtility.RevealInFinder(logPath);
    }

    /// <summary>
    /// Convierte un único material a Quibli/Stylized Lit. Lee todos los valores del
    /// shader ANTERIOR primero, y solo después reasigna el shader — así los datos de
    /// origen no se pierden al cambiar de shader.
    /// </summary>
    private static void ConvertMaterial(Material mat, Shader quibliShader, List<string> warnings)
    {
        var albedoTex = FindFirstTexture(mat, AlbedoCandidates, out _);
        var baseColor = FindFirstColor(mat, BaseColorCandidates, out _) ?? Color.white;
        var normalTex = FindFirstTexture(mat, NormalCandidates, out _);
        var emissionTex = FindFirstTexture(mat, EmissionMapCandidates, out _);
        var emissionColor = FindFirstColor(mat, EmissionColorCandidates, out _) ?? Color.black;
        bool hasEmission = emissionTex != null || ColorHasEnergy(emissionColor);

        if (albedoTex == null) warnings.Add("Sin Albedo de origen — revisar a mano");

        // --- Reasignar shader ---
        mat.shader = quibliShader;

        // --- Albedo / color base ---
        if (mat.HasProperty("_BaseMap") && albedoTex != null) mat.SetTexture("_BaseMap", albedoTex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);

        // --- Normal map ---
        if (mat.HasProperty("_BumpMap"))
        {
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.DisableKeyword("_NORMALMAP");
            }
        }

        // --- Emission ---
        if (mat.HasProperty("_EmissionColor"))
        {
            if (hasEmission)
            {
                if (emissionTex != null && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", emissionTex);
                mat.SetColor("_EmissionColor", emissionColor);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
        }

        // --- Estado de blending/opacidad: SIEMPRE opaco (los especiales se excluyen antes) ---
        SetFloatIfPresent(mat, "_Surface", 0f);
        SetFloatIfPresent(mat, "_Blend", 0f);
        SetFloatIfPresent(mat, "_AlphaClip", 0f);
        SetFloatIfPresent(mat, "_SrcBlend", (float)BlendMode.One);
        SetFloatIfPresent(mat, "_DstBlend", (float)BlendMode.Zero);
        SetFloatIfPresent(mat, "_ZWrite", 1f);
        SetFloatIfPresent(mat, "_Cull", (float)CullMode.Back);
        SetFloatIfPresent(mat, "_Cutoff", 0.5f);
        SetFloatIfPresent(mat, "_QueueOffset", 0f);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Geometry;

        // --- Toggles de Quibli: todos apagados por defecto, punto de partida predecible ---
        SetFloatIfPresent(mat, "_SpecularEnabled", 0f); mat.DisableKeyword("DR_SPECULAR_ON");
        SetFloatIfPresent(mat, "_RimEnabled", 0f); mat.DisableKeyword("DR_RIM_ON");
        SetFloatIfPresent(mat, "_GradientEnabled", 0f); mat.DisableKeyword("DR_GRADIENT_ON");
        SetFloatIfPresent(mat, "_VertexColorsEnabled", 0f); mat.DisableKeyword("DR_VERTEX_COLORS_ON");
        SetFloatIfPresent(mat, "_DecalPaintOver", 0f); mat.DisableKeyword("DR_DECAL_PAINT_OVER");
        SetFloatIfPresent(mat, "_OverrideLightAttenuation", 0f); mat.DisableKeyword("DR_LIGHT_ATTENUATION");
        SetFloatIfPresent(mat, "_OverrideBakedGi", 0f); mat.DisableKeyword("DR_BAKED_GI");
        SetFloatIfPresent(mat, "_OverrideLightmapDir", 0f); mat.DisableKeyword("DR_ENABLE_LIGHTMAP_DIR");
        SetFloatIfPresent(mat, "_ReceiveShadows", 0f); mat.DisableKeyword("_RECEIVE_SHADOWS_OFF");
        SetFloatIfPresent(mat, "_UnityShadowOcclusion", 0f); mat.DisableKeyword("_UNITYSHADOW_OCCLUSION");
        SetFloatIfPresent(mat, "_SelfShadingSize", 0f);
        SetFloatIfPresent(mat, "_TextureImpact", 1f);
        SetFloatIfPresent(mat, "_DetailMapImpact", 0f); // deja el detail map inerte, no hace falta mapearlo
        SetFloatIfPresent(mat, "_LightContribution", 1f);
        SetFloatIfPresent(mat, "_BaseMapPremultiply", 0f); mat.DisableKeyword("_BASEMAP_PREMULTIPLY");
        SetFloatIfPresent(mat, "_TextureBlendingMode", 0f);
        mat.EnableKeyword("_TEXTUREBLENDINGMODE_MULTIPLY");
        mat.DisableKeyword("_TEXTUREBLENDINGMODE_ADD");

        // --- Gradient Ramp: SIEMPRE se crea, nunca se deja sin enlazar (la causa del blanco) ---
        AssignDefaultGradientRamp(mat);

        EditorUtility.SetDirty(mat);
    }

    private static void SetFloatIfPresent(Material mat, string property, float value)
    {
        if (mat.HasProperty(property)) mat.SetFloat(property, value);
    }

    private static bool ColorHasEnergy(Color c) => c.r > 0.01f || c.g > 0.01f || c.b > 0.01f;

    private static Texture FindFirstTexture(Material mat, string[] candidates, out string usedProperty)
    {
        foreach (var name in candidates)
        {
            if (!mat.HasProperty(name)) continue;
            var tex = mat.GetTexture(name);
            if (tex != null) { usedProperty = name; return tex; }
        }
        usedProperty = null;
        return null;
    }

    private static Color? FindFirstColor(Material mat, string[] candidates, out string usedProperty)
    {
        foreach (var name in candidates)
        {
            if (!mat.HasProperty(name)) continue;
            usedProperty = name;
            return mat.GetColor(name);
        }
        usedProperty = null;
        return null;
    }

    /// <summary>
    /// Segunda comprobación de seguridad (independiente de la que ya hizo el auditor al
    /// generar el CSV) antes de tocar un material transparente/de agua por error.
    /// </summary>
    private static bool IsLikelySpecial(Material mat, out string reason)
    {
        reason = null;
        var motives = new List<string>();

        if (mat.renderQueue >= 2450) motives.Add($"render queue {mat.renderQueue}");

        if (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f) motives.Add("Surface = Transparent");
        if (mat.HasProperty("_Mode") && mat.GetFloat("_Mode") >= 2f) motives.Add("Mode = Fade/Transparent");

        if (mat.IsKeywordEnabled("_ALPHABLEND_ON") || mat.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"))
            motives.Add("keyword de alpha blend activo");

        if (motives.Count == 0) return false;
        reason = string.Join(", ", motives);
        return true;
    }

    /// <summary>
    /// Genera y asigna una Gradient Ramp neutra (banda de sombra gris suave → blanco),
    /// horneada en el mismo formato (256x1, ARGB32, Clamp, Bilinear) y con el mismo
    /// esquema de nombre que usa el editor nativo de Quibli (MaterialGradientDrawer),
    /// incrustada como sub-asset del propio material — así sigue siendo editable con el
    /// selector de gradiente nativo de Quibli más adelante.
    /// </summary>
    private static void AssignDefaultGradientRamp(Material mat)
    {
        const string propName = "_GradientRamp";
        if (!mat.HasProperty(propName)) return;

        string matPath = AssetDatabase.GetAssetPath(mat);
        if (string.IsNullOrEmpty(matPath)) return;

        var gradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 0f),
                new GradientColorKey(new Color(0.55f, 0.55f, 0.55f), 0.45f),
                new GradientColorKey(Color.white, 0.55f),
                new GradientColorKey(Color.white, 1f)
            },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
            mode = GradientMode.Blend
        };

        string textureBaseName = $"z_{propName}Tex";
        string fullAssetName = textureBaseName + EncodeGradient(gradient);

        var tex = new Texture2D(RAMP_RESOLUTION, 1, TextureFormat.ARGB32, false)
        {
            name = fullAssetName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int x = 0; x < tex.width; x++)
        {
            Color c = gradient.Evaluate((float)x / (tex.width - 1));
            for (int y = 0; y < tex.height; y++) tex.SetPixel(x, y, c);
        }
        tex.Apply();

        AssetDatabase.AddObjectToAsset(tex, matPath);
        mat.SetTexture(propName, tex);
    }

    /// <summary>
    /// Mismo formato de codificación JSON que usa MaterialGradientDrawer.Encode, para que
    /// Quibli pueda decodificar el degradado si el usuario lo edita más tarde.
    /// </summary>
    private static string EncodeGradient(Gradient gradient)
    {
        var rep = new GradientRepresentation(gradient);
        return JsonUtility.ToJson(rep);
    }

    [Serializable]
    private class GradientRepresentation
    {
        public GradientMode mode;
        public ColorKey[] colorKeys;
        public AlphaKey[] alphaKeys;

        public GradientRepresentation(Gradient source)
        {
            mode = source.mode;
            colorKeys = source.colorKeys.Select(k => new ColorKey { color = k.color, time = k.time }).ToArray();
            alphaKeys = source.alphaKeys.Select(k => new AlphaKey { alpha = k.alpha, time = k.time }).ToArray();
        }

        [Serializable]
        public struct ColorKey { public Color color; public float time; }

        [Serializable]
        public struct AlphaKey { public float alpha; public float time; }
    }
}
