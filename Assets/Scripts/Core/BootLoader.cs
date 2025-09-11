using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    public GameBootProfile profile;

    void Awake()
    {
        // SaveSystem ya hace DontDestroyOnLoad en su Awake.
        // Aquí solo saltamos al menú.
        SceneManager.LoadScene(profile.sceneToLoad);
    }
}