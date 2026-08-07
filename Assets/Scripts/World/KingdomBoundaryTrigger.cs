using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger de zona colocado en el límite visual del Reino (puente, arco, última casa
/// del pueblo). Al cruzarlo el jugador, emite un evento narrativo custom que el grafo
/// puede esperar con un WaitCustomEventNode para encadenar la transición de salida
/// (ver KingdomExitTransitionNode).
///
/// Sigue el mismo patrón que PortalTrigger/OnTriggerEnter_Event: Collider en modo
/// trigger + filtro por tag "Player" + emisión de señal narrativa.
///
/// Bloqueo físico: igual que DayOnlyInspectionTrigger, adquiere el lock de movimiento
/// (PlayerLockService) justo antes de emitir el evento y lo libera un frame después,
/// como puente hasta que el sistema narrativo (diálogo/cinemática) tome el control
/// con su propio PushMode(ActionMode.Cinematic). Sin esto el jugador seguía moviéndose
/// libremente mientras el grafo procesaba el evento.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class KingdomBoundaryTrigger : MonoBehaviour
{
    [Tooltip("Clave del evento narrativo a emitir al cruzar el límite (ej: EVT_REINO_EXIT_BOUNDARY).")]
    public string eventKey = "EVT_REINO_EXIT_BOUNDARY";

    [Tooltip("Si está marcado, el trigger solo se dispara una vez por sesión.")]
    public bool singleUse = true;

    bool _fired;
    bool _lockAcquired;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnDisable()
    {
        ReleaseLock();
    }

    void OnDestroy()
    {
        ReleaseLock();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_fired && singleUse) return;
        if (!other.CompareTag("Player")) return;

        var signals = DefaultNarrativeSignals.Instance ?? DefaultNarrativeSignals.EnsureInstance();

        // Este evento representa un hecho físico instantáneo ("el jugador está cruzando
        // AHORA"), no un flag narrativo que deba recordarse para siempre. Si todavía no hay
        // nadie esperando este evento (p. ej. el grafo aún no ha llegado al WaitCustomEventNode
        // correspondiente porque la misión que lo precede no ha terminado), NO lo emitimos:
        // RaiseCustom lo bancaría en _pending/_raised y, cuando el grafo llegase más tarde a
        // ese punto, lo consumiría de inmediato como si el cruce acabara de ocurrir — aunque
        // el jugador ya esté en la otra punta del mundo. En su lugar dejamos el trigger sin
        // consumir para que el cruce cuente solo cuando de verdad haya alguien escuchando.
        if (!signals.HasCustomListener(eventKey))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[KingdomBoundaryTrigger] Cruce ignorado: nadie espera '{eventKey}' todavía (el grafo no ha llegado a ese punto).");
#endif
            return;
        }

        _fired = true;

        // Adquirir lock ANTES de emitir el evento: puente hasta que el sistema narrativo
        // (diálogo/cinemática) tome el control con su propio PushMode. Mismo patrón que
        // DayOnlyInspectionTrigger.OnTriggerEnter.
        var lockService = PlayerLockService.Instance;
        if (lockService != null)
        {
            lockService.Acquire(this);
            _lockAcquired = true;
        }

        signals.RaiseCustom(eventKey, $"[KingdomBoundaryTrigger] {name}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[KingdomBoundaryTrigger] Límite del Reino cruzado. Evento '{eventKey}' emitido.");
#endif

        StartCoroutine(ReleaseLockNextFrame());
    }

    IEnumerator ReleaseLockNextFrame()
    {
        yield return null;
        ReleaseLock();
    }

    void ReleaseLock()
    {
        if (_lockAcquired && PlayerLockService.HasInstance)
        {
            PlayerLockService.Instance.Release(this);
            _lockAcquired = false;
        }
    }
}
