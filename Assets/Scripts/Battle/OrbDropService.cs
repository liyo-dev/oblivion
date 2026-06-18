using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Servicio singleton (Start scene, DontDestroyOnLoad).
/// En cada carga de escena inyecta OrbDropper en todos los objetos
/// con Damageable en la layer Enemy que no lo tengan ya.
/// </summary>
public class OrbDropService : MonoBehaviour
{
    public static OrbDropService Instance { get; private set; }

    [SerializeField] private OrbDropConfig _config;

    public OrbDropConfig Config => _config;

    private int _enemyLayer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _enemyLayer = LayerMask.NameToLayer("Enemy");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                InjectOrbDroppers(scene.GetRootGameObjects());
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        InjectOrbDroppers(scene.GetRootGameObjects());
    }

    /// <summary>
    /// Inyecta OrbDropper en todos los Damageable de la layer Enemy dentro de roots.
    /// Llamar tambien desde spawners que instancien enemigos en runtime.
    /// </summary>
    public void InjectOrbDroppers(GameObject[] roots)
    {
        if (_config == null) return;

        foreach (var root in roots)
        {
            foreach (var d in root.GetComponentsInChildren<Damageable>(true))
            {
                if (d.gameObject.layer != _enemyLayer) continue;
                if (!d.TryGetComponent<OrbDropper>(out _))
                    d.gameObject.AddComponent<OrbDropper>();
            }
        }
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif
}
