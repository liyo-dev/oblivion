using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class InteractableToNarrativeEvent : MonoBehaviour
{
    [Tooltip("Clave del evento que escuchará el grafo")]
    public string eventKey = "";

    [Tooltip("Referencia opcional. Si está vacía, se resuelve sola.")]
    public DefaultNarrativeSignals signals; // opcional

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
        while (signals == null && timeout > 0f)
        {
            ResolveSignals();
            if (signals != null) break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (signals == null)
        {
            Debug.LogError("[InteractableToNarrativeEvent] No hay DefaultNarrativeSignals tras esperar.");
            yield break;
        }

        Send();
    }

    void ResolveSignals()
    {
        if (signals != null) return;

        signals = DefaultNarrativeSignals.Instance
                  ?? FindAnyObjectByType<DefaultNarrativeSignals>(FindObjectsInactive.Include);
    }

    public void Send()
    {
        if (signals == null) ResolveSignals();
        if (signals == null)
        {
            Debug.LogError("[InteractableToNarrativeEvent] No hay DefaultNarrativeSignals.");
            return;
        }

        signals.RaiseCustom(eventKey);
        Debug.Log($"[InteractableToNarrativeEvent] Emite '{eventKey}' → signals #{signals.GetInstanceID()}");
    }
}
