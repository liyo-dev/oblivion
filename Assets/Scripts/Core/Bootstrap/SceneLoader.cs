using UnityEngine.SceneManagement;

public class SceneLoader : ISceneLoader
{
    public void Initialize() { }
    public void Dispose() { }

    public void LoadScene(string sceneName, LoadMode mode)
    {
        if (mode == LoadMode.Single)
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }
}