using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    public string sceneToLoad;
    [Min(0f)] public float delaySeconds = 0f;

    void Start()
    {
        // Solo ejecutar si estamos en la escena Start
        if (SceneManager.GetActiveScene().name == "Start")
        {
            float wait = Mathf.Max(0f, delaySeconds);
            if (wait <= 0f)
            {
                SceneManager.LoadScene(sceneToLoad);
            }
            else
            {
                StartCoroutine(LoadAfterDelay(wait));
            }
        }
    }

    IEnumerator LoadAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (!string.IsNullOrEmpty(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);
    }
}