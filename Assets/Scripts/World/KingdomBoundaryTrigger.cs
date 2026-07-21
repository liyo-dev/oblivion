using UnityEngine;

/// <summary>
/// Trigger de zona colocado en el límite visual del Reino (puente, arco, última casa
/// del pueblo). Al cruzarlo el jugador, emite un evento narrativo custom que el grafo
/// puede esperar con un WaitCustomEventNode para encadenar la transición de salida
/// (ver KingdomExitTransitionNode).
///
/// Sigue el mismo patrón que PortalTrigger/OnTriggerEnter_Event: Collider en modo
/// trigger + filtro por tag "Player" + emisión de señal narrativa.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class KingdomBoundaryTrigger : MonoBehaviour
{
    [Tooltip("Clave del evento narrativo a emitir al cruzar el límite (ej: EVT_REINO_EXIT_BOUNDARY).")]
    public string eventKey = "EVT_REINO_EXIT_BOUNDARY";

    [Tooltip("Si está marcado, el trigger solo se dispara una vez por sesión.")]
    public bool singleUse = true;

    bool _fired;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && singleUse) return;
        if (!other.CompareTag("Player")) return;

        _fired = true;

        var signals = DefaultNarrativeSignals.Instance ?? DefaultNarrativeSignals.EnsureInstance();
        signals.RaiseCustom(eventKey, $"[KingdomBoundaryTrigger] {name}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[KingdomBoundaryTrigger] Límite del Reino cruzado. Evento '{eventKey}' emitido.");
#endif
    }
}
