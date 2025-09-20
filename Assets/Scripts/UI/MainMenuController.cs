using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Refs")]
    private SaveSystem _saveSystem;
    public Button continueButton;

    [Header("World Scene")]
    public string worldScene = "MainWorld";

    [Header("Fade override (opcional)")]
    public EasyTransition.TransitionSettings fadeOverride; // si lo dejas vacío usa el default del servicio
    [Min(0)] public float fadeDelay = 0f;

    void Start()
    {
        _saveSystem = FindFirstObjectByType<SaveSystem>();
        if (continueButton != null)
            continueButton.interactable = (_saveSystem != null) && _saveSystem.HasSave();
    }

    public void OnNewGame()
    {
        _saveSystem?.Delete();
        Load("Prologo");
    }

    public void OnContinue()
    {
        if (_saveSystem != null && _saveSystem.HasSave())
            Load(worldScene);
        else
            OnNewGame();
    }

    void Load(string sceneName)
    {
        if (fadeOverride != null)
            SceneTransitionLoader.Load(sceneName, fadeOverride, fadeDelay);
        else
            SceneTransitionLoader.Load(sceneName); // usa default del servicio
    }
}