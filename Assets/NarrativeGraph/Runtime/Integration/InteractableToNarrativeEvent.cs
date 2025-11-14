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

        _signals.RaiseCustom(eventKey);
        Debug.Log($"[InteractableToNarrativeEvent] Emite '{eventKey}' → signals #{_signals.GetInstanceID()}");
    }
}
