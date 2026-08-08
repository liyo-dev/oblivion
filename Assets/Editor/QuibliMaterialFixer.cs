using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta para migrar materiales al shader "Quibli/Stylized Lit".
///
/// Problema que resuelve:
/// Cuando cambiás el shader de un material desde el dropdown del Inspector, Unity
/// SOLO conserva los valores de propiedades que tienen el MISMO NOMBRE en ambos
/// shaders. Si el material anterior guardaba la textura de color en "_MainTex"
/// (Standard, muchos asset packs) y Quibli espera "_BaseMap", esa referencia se
/// pierde silenciosamente y el material queda con _BaseMap vacío. Como Quibli
/// además bloquea "_TextureImpact" a 0 cuando no hay Albedo asignado, el objeto
/// termina mostrando solo el tinte _BaseColor (blanco por defecto) => "se ve blanco".
///
/// Unity, sin embargo, NO borra esos valores viejos: quedan guardados dentro de
/// m_SavedProperties del .mat aunque el shader actual no los use. Esta herramienta
/// lee esos valores "huérfanos" vía SerializedObject y, si encuentra una textura
/// bajo alguno de los nombres típicos de otros shaders, la reasigna a "_BaseMap".
///
/// Solo toca materiales que:
///   - usan el shader "Quibli/Stylized Lit"
///   - tienen "_BaseMap" vacío
///   - tienen una textura recuperable bajo otro nombre
/// Nunca toca materiales que ya tienen Albedo asignado (los que "ya se ven bien").
///
/// Para el problema de Transparent/Cutout "planos": Surface Type y Alpha Clipping
/// (_Surface, _AlphaClip, _Blend) también son propiedades ocultas del shader y no
/// se migran si el shader anterior no las llamaba igual (por ej. el "_Mode" de
/// Standard no es lo mismo que "_Surface" de Quibli). Detectarlo automáticamente
/// es poco confiable porque distintos asset packs reutilizan esos nombres para
/// cosas distintas, así que esta herramienta ofrece un botón para aplicarlo a mano
/// sobre los materiales que vos selecciones en el Project (los que sabés que son
/// hojas/pelo/vallas recortadas, etc.).
/// </summary>
public class QuibliMaterialFixer : EditorWindow {
    private const string QuibliShaderName = "Quibli/Stylized Lit";

    // Nombres de textura "Albedo" usados por otros shaders comunes en el proyecto
    // (Standard, HDRP/URP Lit, UTS2, y variantes típicas de asset packs).
    private static readonly string[] LegacyAlbedoNames = {
        "_MainTex", "_AlbedoMap", "_Albedo", "_DiffuseTex", "_Diffuse",
        "_BaseColorMap", "_BaseColorTexture", "_ColorMap", "_Texture",
        "_BaseTex", "_DiffuseMap", "_Main_Texture", "_Tex"
    };

    private Vector2 _scroll;
    private List<string> _fixable = new List<string>();
    private List<string> _fixedNow = new List<string>();
    private List<string> _noTexture = new List<string>();
    private bool _scanned;

    [MenuItem("Tools/Quibli/Migrar materiales a Quibli")]
    static void Open() {
        GetWindow<QuibliMaterialFixer>("Quibli Material Fixer");
    }

    void OnGUI() {
        EditorGUILayout.HelpBox(
            "1) Escanear busca materiales con shader Quibli/Stylized Lit sin Albedo asignado.\n" +
            "2) Reparar recupera automáticamente la textura vieja (ej. _MainTex) y la pone en _BaseMap.\n" +
            "3) Para materiales Transparent/Cutout que quedaron 'planos', seleccionalos en el Project " +
            "y usá el botón de abajo para activar Alpha Clipping correctamente.",
            MessageType.Info);

        EditorGUILayout.Space();
        if (GUILayout.Button("1) Escanear proyecto")) Scan();

        using (new EditorGUI.DisabledScope(!_scanned || _fixable.Count == 0)) {
            if (GUILayout.Button($"2) Reparar automáticamente ({_fixable.Count})")) FixAll();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Activar Alpha Clipping (Cutout) en la selección actual")) {
            ApplyAlphaClipToSelection();
        }
        if (GUILayout.Button("Activar Surface Type = Transparent en la selección actual")) {
            ApplyTransparentToSelection();
        }

        EditorGUILayout.Space();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (_fixedNow.Count > 0) {
            EditorGUILayout.LabelField("Reparados en esta sesión:", EditorStyles.boldLabel);
            foreach (var s in _fixedNow) EditorGUILayout.LabelField(s);
            EditorGUILayout.Space();
        }

        if (_fixable.Count > 0) {
            EditorGUILayout.LabelField("Recuperables (textura vieja encontrada):", EditorStyles.boldLabel);
            foreach (var s in _fixable) EditorGUILayout.LabelField(s);
            EditorGUILayout.Space();
        }

        if (_noTexture.Count > 0) {
            EditorGUILayout.LabelField("Sin _BaseMap y sin textura recuperable (revisar a mano):", EditorStyles.boldLabel);
            foreach (var s in _noTexture) EditorGUILayout.LabelField(s);
        }

        EditorGUILayout.EndScrollView();
    }

    void Scan() {
        _fixable.Clear();
        _noTexture.Clear();
        _fixedNow.Clear();

        foreach (var guid in AssetDatabase.FindAssets("t:Material")) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (mat.shader.name != QuibliShaderName) continue;
            if (!mat.HasProperty("_BaseMap")) continue;
            if (mat.GetTexture("_BaseMap") != null) continue; // ya tiene Albedo, no tocar

            var recovered = FindLegacyTexture(mat, out var fromName);
            if (recovered != null) {
                _fixable.Add($"{path}   (recuperable de \"{fromName}\")");
            } else {
                _noTexture.Add(path);
            }
        }

        _scanned = true;
        Debug.Log($"[Quibli Fixer] Escaneo completo. Recuperables: {_fixable.Count} | Sin textura: {_noTexture.Count}");
    }

    void FixAll() {
        foreach (var entry in _fixable.ToList()) {
            var path = entry.Split(' ')[0];
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            var recovered = FindLegacyTexture(mat, out var fromName, out var scale, out var offset);
            if (recovered == null) continue;

            Undo.RecordObject(mat, "Fix Quibli BaseMap");
            mat.SetTexture("_BaseMap", recovered);
            mat.SetTextureScale("_BaseMap", scale);
            mat.SetTextureOffset("_BaseMap", offset);

            // Si estaba bloqueado en 0 por falta de textura, restaurarlo a 1 (impacto normal).
            if (mat.HasProperty("_TextureImpact")) mat.SetFloat("_TextureImpact", 1f);

            EditorUtility.SetDirty(mat);
            _fixedNow.Add($"{path}  <- {fromName}");
        }

        AssetDatabase.SaveAssets();
        _fixable.RemoveAll(e => _fixedNow.Any(f => f.StartsWith(e.Split(' ')[0])));
        Debug.Log($"[Quibli Fixer] Reparados {_fixedNow.Count} materiales.");
    }

    static Texture FindLegacyTexture(Material mat, out string fromName) {
        return FindLegacyTexture(mat, out fromName, out _, out _);
    }

    static Texture FindLegacyTexture(Material mat, out string fromName, out Vector2 scale, out Vector2 offset) {
        fromName = null;
        scale = Vector2.one;
        offset = Vector2.zero;

        var so = new SerializedObject(mat);
        var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (texEnvs == null) return null;

        for (int i = 0; i < texEnvs.arraySize; i++) {
            var entry = texEnvs.GetArrayElementAtIndex(i);
            var name = entry.FindPropertyRelative("first").stringValue;
            if (!LegacyAlbedoNames.Contains(name)) continue;

            var second = entry.FindPropertyRelative("second");
            var texProp = second.FindPropertyRelative("m_Texture");
            var tex = texProp != null ? texProp.objectReferenceValue as Texture : null;
            if (tex == null) continue;

            fromName = name;
            var scaleProp = second.FindPropertyRelative("m_Scale");
            var offsetProp = second.FindPropertyRelative("m_Offset");
            if (scaleProp != null) scale = scaleProp.vector2Value;
            if (offsetProp != null) offset = offsetProp.vector2Value;
            return tex;
        }

        return null;
    }

    // Replica la lógica de Quibli.QuibliEditor.HandleUrpSettings para el caso
    // "Opaque + Alpha Clipping" (recorte tipo hojas/vallas/pelo con silueta dura).
    static void ApplyAlphaClipToSelection() {
        int count = 0;
        foreach (var obj in Selection.objects) {
            var mat = obj as Material;
            if (mat == null || mat.shader == null || mat.shader.name != QuibliShaderName) continue;

            Undo.RecordObject(mat, "Quibli Alpha Clip");
            mat.SetFloat("_Surface", 0f); // Opaque
            mat.SetFloat("_AlphaClip", 1f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.SetShaderPassEnabled("ShadowCaster", true);
            if (!mat.HasProperty("_Cutoff") || mat.GetFloat("_Cutoff") <= 0f) mat.SetFloat("_Cutoff", 0.5f);

            EditorUtility.SetDirty(mat);
            count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Quibli Fixer] Alpha Clipping activado en {count} material(es).");
    }

    // Replica el caso "Transparent / Blend Alpha" (cristal, humo, telas translúcidas).
    static void ApplyTransparentToSelection() {
        int count = 0;
        foreach (var obj in Selection.objects) {
            var mat = obj as Material;
            if (mat == null || mat.shader == null || mat.shader.name != QuibliShaderName) continue;

            Undo.RecordObject(mat, "Quibli Transparent");
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetShaderPassEnabled("ShadowCaster", false);

            EditorUtility.SetDirty(mat);
            count++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Quibli Fixer] Surface Type = Transparent activado en {count} material(es).");
    }
}
