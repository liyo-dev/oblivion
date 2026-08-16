using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para activar/desactivar el outline (borde negro tipo
/// cel-shading) que ya trae de fábrica el shader Quibli/StylizedLit
/// (propiedades _OutlineEnabled / _OutlineColor / _OutlineWidth / _OutlineScale).
///
/// Es el mismo look que se ve en la demo de las tazas de Quibli
/// (Assets/Plugins/Quibli/Demos/Mugs/[Demo] Mugs.unity, material
/// "Material_Mug_1" con _OutlineEnabled = 1).
///
/// IMPORTANTE — leer antes de usar (contexto: TDD.md § 16, nota del 11 ago 2026):
/// este proyecto ya sufrió una vez daño colateral por una migración masiva de
/// materiales al shading de Quibli (30 materiales ajenos migrados sin querer,
/// hubo que revertir). Para no repetir esa clase de problema, esta herramienta:
///   1. NUNCA cambia el shader de un material. Solo actúa sobre materiales que
///      YA usan un shader de Quibli con la propiedad _OutlineEnabled expuesta
///      (se comprueba con Material.HasProperty antes de tocar nada).
///   2. NUNCA recorre toda la escena sola. Solo actúa sobre los GameObjects
///      que tengas seleccionados en la Hierarchy en ese momento (y sus hijos) —
///      excepto la opción explícita de sincronización de todo el proyecto,
///      pensada para reparar desincronizaciones ya existentes (ver abajo).
///   3. Soporta Undo (Ctrl+Z) como cualquier otra operación del Editor.
///
/// Uso:
///   1. Selecciona en la Hierarchy el/los GameObject(s) cuyo material ya esté
///      en Quibli/StylizedLit (raíz de un prefab, o varios a la vez).
///   2. Tools > Quibli > Outline > Activar outline en selección
///   3. Ajusta color/grosor a mano en el Inspector del material si el valor
///      por defecto no encaja (cada material puede tener su propio ancho).
///
/// FIX (16 ago 2026 — coste de rendimiento fantasma): la versión original de
/// esta herramienta solo tocaba el FLOAT `_OutlineEnabled` con `SetFloat`.
/// Ese float es solo la etiqueta de UI del atributo `[Toggle(DR_OUTLINE_ON)]`
/// del shader — cambiarlo por script NO enciende/apaga sola la keyword
/// `DR_OUTLINE_ON` (eso solo ocurre si se toca el toggle a mano desde el
/// Inspector). Y el propio shader (`StylizedLit.shader`, pase "Outline",
/// `Tags {"LightMode"="SRPDefaultUnlit"}`) decide si dibuja geometría real
/// SOLO mirando la keyword (`#if defined(DR_OUTLINE_ON)`), no el float.
/// Además, quien de verdad evita que ese pase se envíe a la GPU como draw
/// call — `Material.SetShaderPassEnabled("SRPDefaultUnlit", ...)` — solo se
/// llama dentro de `QuibliEditor.OnGUI` (`Assets/Plugins/Quibli/Scripts/Editor/QuibliEditor.cs`,
/// línea ~302), que es código de Inspector: no corre en build, y ni siquiera
/// corre en el Editor a menos que alguien abra el Inspector de ese material
/// en concreto. Resultado real: al activar/desactivar outline en bloque con
/// la versión anterior de esta herramienta, el float cambiaba pero el pase
/// "Outline" seguía enviándose como draw call extra en cada objeto, activado
/// o no — coste de rendimiento fantasma (no visible, pero sí en el profiler:
/// duplica las llamadas a `SRPBRender.ApplyShader`/`SRPBatcher.Flush` en
/// `DrawOpaqueObjects`). Ahora `SetOutlineOnSelection` sincroniza los tres
/// estados a la vez (float + keyword + pase), y hay un nuevo comando de
/// menú para reparar materiales que ya quedaron desincronizados por el uso
/// anterior de la herramienta (ver `SyncOutlineStateProjectWide`).
///
/// Instalación: coloca este archivo en cualquier carpeta llamada "Editor"
/// dentro de Assets (ya vive en Assets/Editor/QuibliOutlineTools.cs).
/// </summary>
public static class QuibliOutlineTools
{
    private const string OutlineEnabledProp = "_OutlineEnabled";
    private const string OutlineColorProp = "_OutlineColor";
    private const string OutlineWidthProp = "_OutlineWidth";

    // Keyword real que lee el shader (#if defined(DR_OUTLINE_ON)) y nombre
    // del pase ("Name \"Outline\"", Tags LightMode=SRPDefaultUnlit) que hay
    // que desactivar de verdad para que no se envíe el draw call extra.
    private const string OutlineKeyword = "DR_OUTLINE_ON";
    private const string OutlinePassName = "SRPDefaultUnlit";

    private static readonly Color DefaultOutlineColor = Color.black;
    private const float DefaultOutlineWidth = 1.2f;

    [MenuItem("Tools/Quibli/Outline/Activar outline en selección")]
    private static void EnableOutlineOnSelection()
    {
        SetOutlineOnSelection(true);
    }

    [MenuItem("Tools/Quibli/Outline/Desactivar outline en selección")]
    private static void DisableOutlineOnSelection()
    {
        SetOutlineOnSelection(false);
    }

    [MenuItem("Tools/Quibli/Outline/Desactivar outline en TODO el proyecto (decisión final — no reactivar sin preguntar)")]
    private static void DisableOutlineProjectWideForGood()
    {
        // DECISIÓN (16 ago 2026): el outline de Quibli fue una prueba visual,
        // no se queda en el juego. Este comando apaga los tres estados a la
        // vez (float + keyword + pase "Outline") en TODOS los materiales del
        // proyecto que exponen _OutlineEnabled, sin importar en qué estado
        // estuvieran — es la forma de garantizar que no queda ni un draw call
        // extra del pase "Outline" en ningún material, no solo los 71 que se
        // activaron a mano el 12 de agosto. Ver TDD.md § 19.4.7 / § 21 para
        // el contexto de por qué se activó y por qué se revirtió.
        var guids = AssetDatabase.FindAssets("t:Material");
        var toFix = new List<Material>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (!mat.HasProperty(OutlineEnabledProp)) continue;

            bool floatOn = mat.GetFloat(OutlineEnabledProp) > 0f;
            bool keywordOn = mat.IsKeywordEnabled(OutlineKeyword);
            bool passOn = mat.GetShaderPassEnabled(OutlinePassName);

            if (!floatOn && !keywordOn && !passOn) continue; // ya apagado del todo, no tocar

            toFix.Add(mat);
        }

        if (toFix.Count == 0)
        {
            Debug.Log("[Quibli Outline] El outline ya está apagado (float + keyword + pase \"Outline\") " +
                      "en todos los materiales del proyecto. Cero draw calls extra por outline. Nada que hacer.");
            return;
        }

        Undo.RecordObjects(toFix.ToArray(), "Desactivar outline Quibli (proyecto completo, decisión final)");

        foreach (var mat in toFix)
        {
            mat.SetFloat(OutlineEnabledProp, 0f);
            mat.DisableKeyword(OutlineKeyword);
            mat.SetShaderPassEnabled(OutlinePassName, false);
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[Quibli Outline] Outline apagado del todo (float + keyword + pase, cero draw calls extra) " +
                   $"en {toFix.Count} material(es):\n  · " +
                   string.Join("\n  · ", toFix.Select(m => AssetDatabase.GetAssetPath(m)).OrderBy(p => p)));
    }

    [MenuItem("Tools/Quibli/Outline/Sincronizar outline en TODO el proyecto (arregla draw calls fantasma)")]
    private static void SyncOutlineStateProjectWide()
    {
        var guids = AssetDatabase.FindAssets("t:Material");
        var toFix = new List<Material>();

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (!mat.HasProperty(OutlineEnabledProp)) continue;

            bool shouldBeOn = mat.GetFloat(OutlineEnabledProp) > 0f;
            bool keywordOn = mat.IsKeywordEnabled(OutlineKeyword);
            bool passOn = mat.GetShaderPassEnabled(OutlinePassName);

            // Ya sincronizado: nada que hacer (evita ensuciar el asset sin motivo).
            if (keywordOn == shouldBeOn && passOn == shouldBeOn) continue;

            toFix.Add(mat);
        }

        if (toFix.Count == 0)
        {
            Debug.Log("[Quibli Outline] Todos los materiales del proyecto con _OutlineEnabled ya tienen " +
                      "la keyword DR_OUTLINE_ON y el pase \"Outline\" sincronizados con su valor actual. Nada que reparar.");
            return;
        }

        Undo.RecordObjects(toFix.ToArray(), "Sincronizar outline Quibli (proyecto completo)");

        foreach (var mat in toFix)
        {
            bool shouldBeOn = mat.GetFloat(OutlineEnabledProp) > 0f;

            if (shouldBeOn) mat.EnableKeyword(OutlineKeyword);
            else mat.DisableKeyword(OutlineKeyword);

            mat.SetShaderPassEnabled(OutlinePassName, shouldBeOn);

            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[Quibli Outline] Reparados {toFix.Count} material(es) desincronizados (keyword/pase no coincidía " +
                   $"con _OutlineEnabled) — el pase \"Outline\" ya no se dibuja de más en los que lo tenían apagado:\n  · " +
                   string.Join("\n  · ", toFix.Select(m => AssetDatabase.GetAssetPath(m)).OrderBy(p => p)));
    }

    [MenuItem("Tools/Quibli/Outline/Buscar materiales Quibli sin outline (en selección)")]
    private static void ReportMissingOutlineOnSelection()
    {
        var materials = CollectQuibliOutlineCapableMaterials(includeDisabledToo: true);

        if (materials.Count == 0)
        {
            Debug.Log("[Quibli Outline] Ningún material con soporte de outline (_OutlineEnabled) encontrado en la selección actual. " +
                      "Recuerda: si el modelo todavía usa un shader que no es de Quibli, primero hay que migrarlo " +
                      "(ver Tools/Quibli/Buscar materiales sin Quibli, y QuibliMaterialFixer si sigue en el proyecto).");
            return;
        }

        var withoutOutline = materials.Where(m => m.GetFloat(OutlineEnabledProp) <= 0f).ToList();
        var withOutline = materials.Count - withoutOutline.Count;

        Debug.Log($"[Quibli Outline] {materials.Count} material(es) con soporte de outline en la selección — " +
                  $"{withOutline} ya lo tienen activado, {withoutOutline.Count} no:");
        foreach (var mat in withoutOutline.OrderBy(m => m.name))
        {
            Debug.Log($"  · {mat.name}");
        }
    }

    private static void SetOutlineOnSelection(bool enable)
    {
        var materials = CollectQuibliOutlineCapableMaterials(includeDisabledToo: true);

        if (materials.Count == 0)
        {
            Debug.LogWarning("[Quibli Outline] No se ha encontrado ningún material con la propiedad _OutlineEnabled " +
                              "en la selección actual (ni en sus hijos). Esta herramienta no toca materiales que no " +
                              "sean ya de Quibli/StylizedLit — selecciona un objeto cuyo material ya esté migrado.");
            return;
        }

        Undo.RecordObjects(materials.ToArray(), enable ? "Activar outline Quibli" : "Desactivar outline Quibli");

        foreach (var mat in materials)
        {
            mat.SetFloat(OutlineEnabledProp, enable ? 1f : 0f);

            // Sincronizar también la keyword real que lee el shader y el pase
            // "Outline" que la emite como draw call — SetFloat por sí solo no
            // toca ninguna de las dos (ver nota FIX de la cabecera del archivo).
            if (enable) mat.EnableKeyword(OutlineKeyword);
            else mat.DisableKeyword(OutlineKeyword);
            mat.SetShaderPassEnabled(OutlinePassName, enable);

            if (enable)
            {
                // Solo se rellenan color/grosor si el material no tenía ya un
                // outline configurado a mano (evita pisar un ajuste previo).
                if (mat.HasProperty(OutlineColorProp) && mat.GetColor(OutlineColorProp) == Color.white)
                {
                    mat.SetColor(OutlineColorProp, DefaultOutlineColor);
                }

                if (mat.HasProperty(OutlineWidthProp) && Mathf.Approximately(mat.GetFloat(OutlineWidthProp), 1f))
                {
                    mat.SetFloat(OutlineWidthProp, DefaultOutlineWidth);
                }
            }

            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[Quibli Outline] Outline {(enable ? "activado" : "desactivado")} en {materials.Count} material(es): " +
                   string.Join(", ", materials.Select(m => m.name).Distinct()));
    }

    /// <summary>
    /// Recorre los GameObjects seleccionados (y todos sus hijos) buscando
    /// Renderers, y devuelve el conjunto (sin duplicados) de materiales que
    /// exponen _OutlineEnabled — es decir, materiales que ya usan un shader
    /// de Quibli compatible con outline. Cualquier material que no tenga esa
    /// propiedad se ignora por completo (nunca se toca su shader).
    /// </summary>
    private static List<Material> CollectQuibliOutlineCapableMaterials(bool includeDisabledToo)
    {
        var seen = new HashSet<Material>();
        var result = new List<Material>();

        foreach (var go in Selection.gameObjects)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null || mat.shader == null) continue;
                    if (!mat.HasProperty(OutlineEnabledProp)) continue;
                    if (seen.Add(mat)) result.Add(mat);
                }
            }
        }

        return result;
    }
}
