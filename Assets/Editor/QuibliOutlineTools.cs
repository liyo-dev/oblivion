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
///      que tengas seleccionados en la Hierarchy en ese momento (y sus hijos).
///   3. Soporta Undo (Ctrl+Z) como cualquier otra operación del Editor.
///
/// Uso:
///   1. Selecciona en la Hierarchy el/los GameObject(s) cuyo material ya esté
///      en Quibli/StylizedLit (raíz de un prefab, o varios a la vez).
///   2. Tools > Quibli > Outline > Activar outline en selección
///   3. Ajusta color/grosor a mano en el Inspector del material si el valor
///      por defecto no encaja (cada material puede tener su propio ancho).
///
/// Instalación: coloca este archivo en cualquier carpeta llamada "Editor"
/// dentro de Assets (ya vive en Assets/Editor/QuibliOutlineTools.cs).
/// </summary>
public static class QuibliOutlineTools
{
    private const string OutlineEnabledProp = "_OutlineEnabled";
    private const string OutlineColorProp = "_OutlineColor";
    private const string OutlineWidthProp = "_OutlineWidth";

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
