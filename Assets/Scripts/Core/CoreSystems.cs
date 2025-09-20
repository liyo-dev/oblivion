using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class CoreSystems : MonoBehaviour
{
    private static CoreSystems _instance;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}