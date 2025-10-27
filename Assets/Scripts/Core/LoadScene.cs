using UnityEngine;

[DisallowMultipleComponent]
public class LoadScene : MonoBehaviour
{
    [Header("Target")]
    public string sceneName;
    public bool loadOnStart = false;

    [Header("Loading Screen (opcional)")]
    [Tooltip("Si está vacío, no se usa overlay y se hace un Load normal.")]
    public string loadingOverlayScene = "LoadingScreen";

    [Header("Fade override (opcional)")]
    public EasyTransition.TransitionSettings fadeOverride;
    [Min(0f)] public float fadeDelay = 0f;

    void Start()
    {
        if (loadOnStart && !string.IsNullOrEmpty(sceneName))
            LoadTargetScene();
    }

    [ContextMenu("Load Target Scene")]
    public void LoadTargetScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[LoadScene] No scene name specified!");
            return;
        }

        bool useOverlay = !string.IsNullOrEmpty(loadingOverlayScene);

        if (useOverlay)
        {
            if (fadeOverride != null)
                SceneTransitionLoader.LoadWithOverlay(sceneName, loadingOverlayScene, fadeOverride, fadeDelay);
            else
                SceneTransitionLoader.LoadWithOverlay(sceneName, loadingOverlayScene);
        }
        else
        {
            if (fadeOverride != null)
                SceneTransitionLoader.Load(sceneName, fadeOverride, fadeDelay);
            else
                SceneTransitionLoader.Load(sceneName); // usa el default del servicio
        }
    }
}