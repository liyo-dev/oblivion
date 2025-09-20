using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    public string sceneToLoad;

    void Awake()
    {
        // Solo ejecutar si estamos en la escena Start
        if (SceneManager.GetActiveScene().name == "Start")
        {
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}