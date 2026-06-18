using UnityEngine;

/// <summary>
/// Singleton vacío — conservado para no romper la referencia en Start.unity.
/// La vida y la magia son únicas (pertenecen al player) y se comparten entre todos
/// los personajes. El intercambio de stats al cambiar personaje ha sido eliminado.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PartyStatsManager : MonoBehaviour
{
    public static PartyStatsManager Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
