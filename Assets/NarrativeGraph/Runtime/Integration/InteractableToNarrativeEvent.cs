using UnityEngine;

[DisallowMultipleComponent]
public class InteractableToNarrativeEvent : MonoBehaviour
{
    public string eventKey = "";
    public DefaultNarrativeSignals signals; // puede quedarse vacío

    void Awake()
    {
        if (signals == null) signals = DefaultNarrativeSignals.Instance
                                       ?? FindAnyObjectByType<DefaultNarrativeSignals>(FindObjectsInactive.Include);
    }

    public void Send()
    {
        if (signals == null)
            signals = DefaultNarrativeSignals.Instance
                      ?? FindAnyObjectByType<DefaultNarrativeSignals>(FindObjectsInactive.Include);

        if (signals == null) { Debug.LogError("[InteractableToNarrativeEvent] No hay DefaultNarrativeSignals."); return; }

        signals.RaiseCustom(eventKey);
        Debug.Log($"[InteractableToNarrativeEvent] Emite '{eventKey}' → signals #{signals.GetInstanceID()}");
    }

}