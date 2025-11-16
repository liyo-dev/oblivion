// Editor/AutoBootstrapOnPlay.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AutoBootstrapOnPlay
{
    static AutoBootstrapOnPlay()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            // Cargar Start ANTES de entrar en PlayMode para que los sistemas se inicialicen correctamente
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                bool startLoaded = false;
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    var s = EditorSceneManager.GetSceneAt(i);
                    if (s.name == "Start") { startLoaded = true; break; }
                }
                
                if (!startLoaded)
                {
                    // Buscar la escena Start en el proyecto
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Scene Start");
                    if (guids.Length > 0)
                    {
                        string startPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        EditorSceneManager.OpenScene(startPath, OpenSceneMode.Additive);
                        UnityEngine.Debug.Log($"[AutoBootstrapOnPlay] Escena 'Start' cargada aditivamente desde: {startPath}");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[AutoBootstrapOnPlay] No se encontró la escena 'Start' en el proyecto.");
                    }
                }
            }
        };
    }
}
#endif