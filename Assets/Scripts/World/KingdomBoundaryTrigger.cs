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
/// (PlayerLockService) justo antes de emitir el evento, como puente hasta que el sistema
/// narrativo (diálogo/cinemática) tome el control con su propio PushMode(ActionMode.Cinematic).
/// Sin esto el jugador seguía moviéndose libremente mientras el grafo procesaba el evento.
///
/// FIX (Agosto 2026): el puente ya NO se libera a los "un frame después" a ciegas — eso dejaba
/// una ventana en la que el freeze se soltaba antes de que el grafo llegara de verdad a empujar
/// ActionMode.Cinematic (NarrativeRunner.RunSubGraph cede como mínimo 1 frame por cada nodo
/// intermedio entre el WaitCustomEventNode que consume este evento y el nodo que realmente
/// bloquea, aunque cada uno resuelva al instante). Ahora usa
/// PlayerLockService.AcquireBridgeUntilCinematic(), que espera a que Cinematic esté realmente
/// activo (con un timeout de seguridad) desde una corrutina alojada en el propio
/// PlayerLockService (persistente), no en este trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class KingdomBoundaryTrigger : MonoBehaviour
{
    [Tooltip("Clave del evento narrativo a emitir al cruzar el límite (ej: EVT_REINO_EXIT_BOUNDARY).")]
    public string eventKey = "EVT_REINO_EXIT_BOUNDARY";

    [Tooltip("Si está marcado, el trigger solo se dispara una vez por sesión.")]
    public bool singleUse = true;

    [Tooltip("Si se asigna, delega el freeze en TriggerPlayerStop (modo Parar) en vez de la " +
             "implementación propia de este script. Dejar vacío para el comportamiento anterior.")]
    public TriggerPlayerStop playerStop;

    bool _fired;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
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

        if (playerStop != null)
        {
            // Delega en el sistema central (modo Parar) el mismo puente: freeze ahora, liberado
            // en cuanto ActionMode.Cinematic esté realmente activo (ver TriggerPlayerStop.
            // IniciarParadaMomentanea / PlayerLockService.AcquireBridgeUntilCinematic).
            playerStop.IniciarParadaMomentanea();
        }
        else
        {
            // Comportamiento anterior: adquirir lock ANTES de emitir el evento, puente hasta que
            // el sistema narrativo (diálogo/cinemática) tome el control con su propio PushMode.
            // Mismo patrón que DayOnlyInspectionTrigger.OnTriggerEnter. La espera/liberación vive
            // en PlayerLockService (persistente), no en este componente — ver comentario de clase.
            var lockService = PlayerLockService.Instance;
            lockService?.AcquireBridgeUntilCinematic(this);
        }

        signals.RaiseCustom(eventKey, $"[KingdomBoundaryTrigger] {name}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[KingdomBoundaryTrigger] Límite del Reino cruzado. Evento '{eventKey}' emitido.");
#endif
    }
}
