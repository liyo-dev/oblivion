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
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                bool startLoaded = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s.name == "Start") { startLoaded = true; break; }
                }
                if (!startLoaded)
                {
                    // Carga Start de forma aditiva cuando entras en Play
                    SceneManager.LoadScene("Start", LoadSceneMode.Additive);
                }
            }
        };
    }
}
#endif