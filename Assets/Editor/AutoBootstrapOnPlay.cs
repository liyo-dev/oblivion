// Editor/AutoBootstrapOnPlay.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AutoBootstrapOnPlay
{
    // Escenas en las que NO se debe cargar Start.unity de forma aditiva al entrar en Play.
    // Ojo: cualquier sistema que dependa de un manager de Start vía ServiceLocator
    // (PlayerInputManager, GameBootService, etc.) dejará de funcionar en estas escenas.
    private static readonly System.Collections.Generic.HashSet<string> ExcludedScenes =
        new System.Collections.Generic.HashSet<string>
    {
        "CharacterCreator",
    };

    static AutoBootstrapOnPlay()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            // Cargar Start ANTES de entrar en PlayMode para que los sistemas se inicialicen correctamente
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    var scene = EditorSceneManager.GetSceneAt(i);
                    if (ExcludedScenes.Contains(scene.name))
                    {
                        UnityEngine.Debug.Log($"[AutoBootstrapOnPlay] Escena '{scene.name}' está en la lista de exclusión — no se carga 'Start' aditivamente.");
                        return;
                    }
                }

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