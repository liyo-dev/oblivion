using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Refs")]
    public GameBootProfile profile;
    public SaveSystem saveSystem;
    public Button continueButton; // opcional

    [Header("World Scene")]
    public string worldScene = "World";

    void Start()
    {
        if (!saveSystem) saveSystem = FindFirstObjectByType<SaveSystem>();
        if (continueButton) continueButton.interactable = saveSystem && saveSystem.HasSave();
    }

    public void OnNewGame()
    {
        var data = profile.BuildDefaultSave(); // usa defaultAnchorId del SO
        saveSystem?.Save(data);
        SceneManager.LoadScene(worldScene);
    }

    public void OnContinue()
    {
        if (saveSystem && saveSystem.HasSave())
            SceneManager.LoadScene(worldScene);
        else
            OnNewGame();
    }
}