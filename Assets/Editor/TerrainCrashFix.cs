using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Script que desactiva automáticamente el Terrain en MainWorld para evitar el crash
/// Se ejecuta ANTES de que Unity intente procesar el Terrain
/// </summary>
[InitializeOnLoad]
public static class TerrainCrashFix
{
    static TerrainCrashFix()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        // Solo actuar en MainWorld
        if (!scene.name.Contains("MainWorld")) return;

        Debug.Log($"[TerrainCrashFix] Escena MainWorld detectada. Desactivando Terrain...");

        // Usar delayCall para ejecutar después de que la escena cargue completamente
        EditorApplication.delayCall += () =>
        {
            DisableTerrainInScene();
        };
    }

    private static void DisableTerrainInScene()
    {
        // Buscar TODOS los GameObjects en la escena
        var allObjects = Object.FindObjectsOfType<GameObject>(true); // incluye inactivos
        bool found = false;

        foreach (var go in allObjects)
        {
            // Buscar por componente Terrain O por nombre
            if (go.GetComponent<Terrain>() != null || go.name.Contains("TERRAIN") || go.name.Contains("Terrain"))
            {
                if (go.activeInHierarchy)
                {
                    go.SetActive(false);
                    Debug.LogWarning($"[TerrainCrashFix] ⚠️ Terrain '{go.name}' DESACTIVADO para evitar crash");
                    found = true;
                    
                    // Marcar escena como modificada
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
        }

        if (found)
        {
            Debug.LogWarning("[TerrainCrashFix] ⚠️ IMPORTANTE: Se desactivó el Terrain automáticamente");
            Debug.LogWarning("[TerrainCrashFix] El Terrain tiene un problema que causa crash");
            Debug.LogWarning("[TerrainCrashFix] Guarda la escena (Ctrl+S) y ahora podrás trabajar sin crash");
            
            // Mostrar diálogo
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog(
                    "Terrain Desactivado",
                    "El Terrain fue desactivado automáticamente porque causa un crash.\n\n" +
                    "Problema: TerrainCollider mal configurado con MeshCollider incompatible.\n\n" +
                    "¿Quieres guardar la escena ahora con el Terrain desactivado?",
                    "Sí, Guardar",
                    "Más tarde"))
                {
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    Debug.Log("[TerrainCrashFix] ✓ Escena guardada con Terrain desactivado");
                    Debug.Log("[TerrainCrashFix] Ahora puedes presionar Play sin crash");
                }
            };
        }
        else
        {
            Debug.Log("[TerrainCrashFix] No se encontró Terrain activo en la escena");
        }
    }

    // Método manual por si el automático no funciona
    [MenuItem("Tools/Fix MainWorld/Desactivar Terrain")]
    public static void DisableTerrainManual()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.name.Contains("MainWorld"))
        {
            EditorUtility.DisplayDialog("Error", "Esta herramienta solo funciona en la escena MainWorld", "OK");
            return;
        }

        DisableTerrainInScene();
    }
}

