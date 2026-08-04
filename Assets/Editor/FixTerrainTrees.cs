using UnityEngine;
using UnityEditor;

/// <summary>
/// Script para limpiar completamente los árboles del Terrain que causa crash
/// </summary>
public class FixTerrainTrees : EditorWindow
{
    [MenuItem("Tools/Fix MainWorld/Limpiar Árboles del Terrain")]
    public static void CleanTerrainTrees()
    {
        // Buscar todos los Terrains en la escena actual
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        
        if (terrains.Length == 0)
        {
            EditorUtility.DisplayDialog("No hay Terrains", "No se encontraron Terrains en la escena.", "OK");
            return;
        }

        int totalCleaned = 0;
        string report = "=== LIMPIEZA DE TERRAINS ===\n\n";

        foreach (Terrain terrain in terrains)
        {
            TerrainData data = terrain.terrainData;
            if (data == null) continue;

            int treeCount = data.treeInstanceCount;
            int prototypeCount = data.treePrototypes.Length;

            if (treeCount > 0 || prototypeCount > 0)
            {
                report += $"Terrain: {terrain.name}\n";
                report += $"  - Árboles instances: {treeCount}\n";
                report += $"  - Tree prototypes: {prototypeCount}\n";

                // Limpiar tree instances
                data.SetTreeInstances(new TreeInstance[0], true);
                
                // Limpiar tree prototypes
                data.treePrototypes = new TreePrototype[0];
                data.RefreshPrototypes();

                report += $"  ✓ Limpiado completamente\n\n";
                totalCleaned++;

                // Marcar el terrainData como sucio para que se guarde
                EditorUtility.SetDirty(data);
            }
        }

        if (totalCleaned > 0)
        {
            report += $"✓ Total de Terrains limpiados: {totalCleaned}\n";
            report += "\n⚠️ IMPORTANTE:\n";
            report += "1. Guarda la escena (Ctrl+S)\n";
            report += "2. Guarda el proyecto (Ctrl+Shift+S o File > Save Project)\n";
            report += "3. Cierra y reabre Unity\n";
            report += "4. Intenta Play de nuevo\n";

            Debug.Log(report);
            EditorUtility.DisplayDialog("Terrains Limpiados", report, "OK");

            // Marcar escena como modificada
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        else
        {
            EditorUtility.DisplayDialog("Sin cambios", "No se encontraron árboles para limpiar.", "OK");
        }
    }
}

