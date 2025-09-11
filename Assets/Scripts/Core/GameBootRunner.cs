using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameBootRunner : MonoBehaviour
{
    [Header("Perfil de arranque (SO)")]
    public GameBootProfile profile;

    [Header("Servicios")]
    public SaveSystem saveSystem;

    [Header("UI (opcional)")]
    public Button continueButton;

    void Awake()
    {
        if (!saveSystem) saveSystem = FindFirstObjectByType<SaveSystem>();
        if (continueButton) continueButton.interactable = saveSystem && saveSystem.HasSave();
    }

    public void StartNewGame()
    {
        var data = profile.BuildDefaultSave(); // <- método que añadimos al SO
        saveSystem?.Save(data);
        SceneManager.LoadScene(profile.sceneToLoad);
    }

    public void ContinueGame()
    {
        if (saveSystem && saveSystem.HasSave())
            SceneManager.LoadScene(profile.sceneToLoad);
        else
            StartNewGame();
    }

    public void DeleteSave()
    {
        saveSystem?.Delete();
        if (continueButton) continueButton.interactable = false;
    }
}