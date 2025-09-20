using UnityEngine;

[DisallowMultipleComponent]
public class PersistOnLoad : MonoBehaviour
{
    private static PersistOnLoad _instance;

    void Awake()
    {
        // Evita duplicados si por algún motivo se vuelve a crear otro TransitionSystem
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        DontDestroyOnLoad(gameObject); // <-- persiste TODO el árbol (Manager + Template hijo)
    }
}