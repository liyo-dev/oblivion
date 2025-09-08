using UnityEngine;

public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private ProjectConfig projectConfig;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 60;

        // Limpia (por si vienes del editor en play)
        ServiceLocator.Clear();

        // Installer registra servicios y los Initialize()
        var systems = new SystemsInstaller(projectConfig);
        systems.InstallAll();

        // Ir al título
        var sceneLoader = ServiceLocator.Get<ISceneLoader>();
        sceneLoader.LoadScene(projectConfig.titleScene, LoadMode.Single);
    }
}
