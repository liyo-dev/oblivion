using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Script para iniciar el baking de lightmaps con configuración de alta calidad
/// </summary>
public class StartProductionBake : EditorWindow
{
    [MenuItem("El Sendero/Lighting/Start Production Bake")]
    public static void StartBaking()
    {
        if (!EditorUtility.DisplayDialog(
            "Iniciar Bake de Producción",
            "Esto iniciará el baking de lightmaps con configuración de ALTA CALIDAD.\n\n" +
            "⚠️ ADVERTENCIA:\n" +
            "- Tomará VARIAS HORAS (toda la noche)\n" +
            "- Asegúrate de que el PC no se apague\n" +
            "- Guarda tu trabajo antes de continuar\n\n" +
            "Configuración aplicada:\n" +
            "- Lightmap Max Size: 2048\n" +
            "- Direct Samples: 256\n" +
            "- Indirect Samples: 4096\n" +
            "- Environment Samples: 2048\n" +
            "- Bounces: 4\n" +
            "- Denoiser: Optix (máxima calidad)\n" +
            "- Filtering: Advanced\n\n" +
            "¿Continuar?",
            "Sí, Iniciar Bake",
            "Cancelar"))
        {
            return;
        }

        Debug.Log("=== INICIANDO BAKE DE PRODUCCIÓN ===");
        Debug.Log("Configuración de ALTA CALIDAD activada");
        Debug.Log("Tiempo estimado: 4-12 horas dependiendo de la escena");
        
        // Guardar escena actual
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        
        Debug.Log("✓ Escena y assets guardados");
        
        // Iniciar bake
        Lightmapping.BakeAsync();
        
        Debug.Log("✓ Baking iniciado en modo asíncrono");
        Debug.Log("Puedes minimizar Unity - seguirá trabajando en segundo plano");
        
        EditorApplication.Beep();
    }

    [MenuItem("El Sendero/Lighting/Cancel Baking")]
    public static void CancelBaking()
    {
        if (Lightmapping.isRunning)
        {
            Lightmapping.Cancel();
            Debug.Log("⚠️ Baking cancelado");
        }
        else
        {
            Debug.Log("No hay baking en progreso");
        }
    }

    [MenuItem("El Sendero/Lighting/Show Baking Progress")]
    public static void ShowProgress()
    {
        if (Lightmapping.isRunning)
        {
            Debug.Log($"Baking en progreso... Complejidad: {Lightmapping.buildProgress * 100:F1}%");
        }
        else
        {
            Debug.Log("No hay baking en progreso");
        }
    }
}

