using UnityEngine;

/// <summary>
/// Escucha un evento narrativo custom y abre las puertas del castillo.
/// Colocar en el mismo GameObject que CastleDoorController o referenciarle.
/// </summary>
public class CastleNarrativeEventListener : MonoBehaviour
{
    [Tooltip("Clave del evento narrativo que dispara la apertura (debe coincidir con narrativeEventKey del NPC)")]
    [SerializeField] private string narrativeEventKey;

    [SerializeField] private CastleDoorController doorController;

    private System.Action _handler;

    void Start()
    {
        if (string.IsNullOrEmpty(narrativeEventKey)) return;

        _handler = OnNarrativeEvent;
        DefaultNarrativeSignals.OnAfterReset += ReSubscribe;
        TrySubscribe();
    }

    void OnDestroy()
    {
        DefaultNarrativeSignals.OnAfterReset -= ReSubscribe;
        DefaultNarrativeSignals.Instance?.OffCustom(narrativeEventKey, _handler);
    }

    private void TrySubscribe()
    {
        DefaultNarrativeSignals.Instance?.OnCustom(narrativeEventKey, _handler);
    }

    private void ReSubscribe() => TrySubscribe();

    private void OnNarrativeEvent() => doorController?.OpenDoors();
}
