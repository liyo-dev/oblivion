using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para localizar renderers en la escena abierta que
/// todavía usan un shader que NO es de Quibli (es decir, candidatos a
/// convertir para que encajen con el "look" de Quibli).
///
/// Instalación: coloca este archivo en cualquier carpeta llamada "Editor"
/// dentro de Assets (por ejemplo Assets/Editor/FindNonQuibliMaterials.cs).
///
/// Uso: Tools > Quibli > Buscar materiales sin Quibli (en la escena)
/// El resultado aparece en la consola, y también se selecciona en la
/// Hierarchy cada GameObject afectado para que sea fácil de revisar.
/// </summary>
public static class FindNonQuibliMaterials
{
    [MenuItem("Tools/Quibli/Buscar materiales sin Quibli (en la escena)")]
    private static void FindInScene()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var offenders = new List<(GameObject go, Material mat, string shaderPath)>();
        var seenMats = new HashSet<Material>();

        foreach (var r in renderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || mat.shader == null) continue;
                if (!seenMats.Add(mat)) { /* sigue comprobando el GO igualmente */ }

                string shaderPath = AssetDatabase.GetAssetPath(mat.shader);
                bool isQuibli = shaderPath.Contains("/Quibli/") ||
                                 mat.shader.name.StartsWith("Quibli/") ||
                                 mat.shader.name.Contains("Quibli");

                if (!isQuibli)
                {
                    offenders.Add((r.gameObject, mat, string.IsNullOrEmpty(shaderPath) ? "(built-in)" : shaderPath));
                }
            }
        }

        if (offenders.Count == 0)
        {
            Debug.Log("[Quibli Check] No se encontró ningún renderer en la escena usando un shader que no sea de Quibli. Todo en orden.");
            return;
        }

        Debug.Log($"[Quibli Check] Se encontraron {offenders.Count} referencias (GameObject + material) que NO usan un shader de Quibli:");

        // Agrupar por material para un resumen más legible.
        var byMaterial = offenders.GroupBy(o => o.mat);
        foreach (var group in byMaterial.OrderBy(g => g.Key.name))
        {
            var shaderPath = group.First().shaderPath;
            var shaderName = group.Key.shader != null ? group.Key.shader.name : "(sin shader)";
            var gos = string.Join(", ", group.Select(o => o.go.name).Distinct().Take(10));
            Debug.Log($"  · Material '{group.Key.name}' — shader: {shaderName} ({shaderPath}) — usado en: {gos}" +
                      (group.Select(o => o.go.name).Distinct().Count() > 10 ? " ..." : ""));
        }

        // Seleccionar todos los GameObjects afectados en la Hierarchy.
        Selection.objects = offenders.Select(o => (Object)o.go).Distinct().ToArray();
    }
}
