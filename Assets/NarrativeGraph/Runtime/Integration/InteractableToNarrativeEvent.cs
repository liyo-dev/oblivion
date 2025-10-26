using UnityEngine;

[DisallowMultipleComponent]
public class InteractableToNarrativeEvent : MonoBehaviour
{
    public string eventKey = "";
    public DefaultNarrativeSignals signals; // puede quedarse vacío

    public bool sendNow = false;

    void Awake()
    {
        if (signals == null) signals = DefaultNarrativeSignals.Instance
                                       ?? FindAnyObjectByType<DefaultNarrativeSignals>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// Start is called on the frame when a script is enabled just before
    /// any of the Update methods is called the first time.
    /// </summary>
    void Start()
    {
        if (sendNow) Send();
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