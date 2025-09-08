public enum LoadMode { Single, Additive }

public interface ISceneLoader : IGameService
{
    void LoadScene(string sceneName, LoadMode mode);
}