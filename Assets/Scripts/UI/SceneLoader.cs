using UnityEngine;

/// <summary>
/// Pequeña API estática para arrancar cargas de escena con pantalla de carga.
/// Llama internamente a LoadingScreenController.LoadScene.
/// Útil para llamar desde nodos, eventos o botones sin depender de instancia.
/// </summary>
public static class SceneLoader
{
    /// <summary>
    /// Inicia la carga de la escena usando la pantalla de carga si existe.
    /// </summary>
    public static void Load(string sceneName)
    {
        LoadingScreenController.LoadScene(sceneName);
    }

    /// <summary>
    /// Si necesitas un fallback que intente cargar sin la UI (síncrono), puedes usar esto.
    /// </summary>
    public static void LoadFallback(string sceneName)
    {
        if (LoadingScreenController.Instance != null)
        {
            LoadingScreenController.Instance.StartLoad(sceneName);
            return;
        }

        // Fallback básico
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}

