using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class InteractableToNarrativeEvent : MonoBehaviour
{
    [Tooltip("Clave del evento que escuchará el grafo")]
    public string eventKey = "";

    DefaultNarrativeSignals _signals;

    [Tooltip("Enviar automáticamente al iniciar")]
    public bool sendNow = false;

    [Header("Congelar al jugador (opcional)")]
    [Tooltip("Si se asigna, delega en TriggerPlayerStop cómo afecta este evento al jugador " +
             "(Parar = freeze total hasta que el grafo/diálogo tome el control; ver esa clase). " +
             "Dejar vacío para el comportamiento anterior: no toca al jugador, solo emite el evento.")]
    public TriggerPlayerStop playerStop;

    void Awake()
    {
        ResolveSignals();
    }

    void Start()
    {
        if (sendNow) StartCoroutine(SendWhenReady());
    }

    IEnumerator SendWhenReady()
    {
        // Espera breve a que los managers de Start se inicialicen
        float timeout = 2f; // segundos máx
        while (_signals == null && timeout > 0f)
        {
            ResolveSignals();
            if (_signals != null) break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_signals == null)
        {
            Debug.LogError("[InteractableToNarrativeEvent] No hay DefaultNarrativeSignals tras esperar.");
            yield break;
        }

        Send();
    }

    void ResolveSignals()
    {
        if (_signals != null) return;
        _signals = DefaultNarrativeSignals.EnsureInstance();
    }

    public void Send()
    {
        if (_signals == null) ResolveSignals();
        if (_signals == null)
        {
            Debug.LogError("[InteractableToNarrativeEvent] No hay DefaultNarrativeSignals.");
            return;
        }

        // Este componente representa un hecho físico/interactivo instantáneo ("el jugador
        // está aquí AHORA"), no un flag narrativo que deba recordarse para siempre. Si
        // todavía no hay nadie esperando esta key (p. ej. el grafo sigue bloqueado en un
        // WaitQuestCompleteNode previo), NO lo emitimos: RaiseCustom lo bancaría en
        // _pending/_raised y, cuando el grafo llegase más tarde a ese WaitCustomEventNode,
        // lo consumiría de inmediato como si el trigger acabara de dispararse — aunque en
        // realidad ocurrió antes de tiempo. Mismo patrón que KingdomBoundaryTrigger.
        if (!_signals.HasCustomListener(eventKey))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[InteractableToNarrativeEvent] Ignorado: nadie espera '{eventKey}' todavía (el grafo no ha llegado a ese punto).");
#endif
            return;
        }

        // Congelar al jugador ANTES de emitir, como puente hasta que el grafo/diálogo tome el
        // control con su propio PushMode(ActionMode.Cinematic). Sin efecto si playerStop es null
        // o su modo no incluye Parar. Mismo patrón que KingdomBoundaryTrigger/DayOnlyInspectionTrigger.
        playerStop?.IniciarParadaMomentanea();

        _signals.RaiseCustom(eventKey, name);
        Debug.Log($"[InteractableToNarrativeEvent] Emite '{eventKey}' → signals #{_signals.GetEntityId()}");
    }
}
