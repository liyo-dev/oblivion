using UnityEngine;

/// <summary>
/// DEPRECATED: Este componente emitía eventos automáticamente basado en el estado de quests.
/// Esto causaba emisiones duplicadas y comportamiento impredecible.
/// Los eventos deben ser emitidos explícitamente por:
/// - Interactables (InteractableToNarrativeEvent)
/// - Nodos del grafo (UnlockTriggerNode, etc.)
/// - NPCs mediante código específico
/// NO elimines este archivo porque podría estar referenciado en escenas, pero no se ejecutará.
/// </summary>
[System.Obsolete("Este componente está deprecado. Los eventos deben ser emitidos explícitamente por Interactables o nodos del grafo.")]
[DefaultExecutionOrder(100)]
public sealed class QuestEventBootstrap : MonoBehaviour
{
    // Campos sin uso a propósito: el componente está deprecado y su lógica (TryFire) está
    // comentada más abajo. Se conservan para no perder los valores ya serializados en escenas.
#pragma warning disable 414
    [SerializeField] private string questId;
    [SerializeField] private QuestState minimumState = QuestState.Completed;
    [SerializeField] private string eventKey;
    [SerializeField] private bool runOnce = true;
#pragma warning restore 414

    bool _fired;

    void Start()
    {
        Debug.LogWarning($"[QuestEventBootstrap] COMPONENTE DEPRECADO en '{gameObject.name}'. " +
                        $"Este componente ya no emite eventos automáticamente. " +
                        $"Usa InteractableToNarrativeEvent o nodos del grafo para emitir '{eventKey}'.");
        // TryFire(); // DESHABILITADO - Causaba emisiones automáticas no deseadas
    }

    void OnEnable()
    {
        // if (!runOnce) TryFire(); // DESHABILITADO
    }

    void TryFire()
    {
        // MÉTODO DESHABILITADO - Causaba emisiones automáticas de eventos basadas en estado de quests
        // Los eventos deben ser emitidos explícitamente por Interactables o nodos del grafo
        
        /*
        if (_fired && runOnce) return;
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(eventKey)) return;
        var qm = QuestManager.Instance;
        if (qm == null) return;
        var state = qm.GetState(questId);
        if ((int)state < (int)minimumState) return;
        var signals = DefaultNarrativeSignals.Instance;
        if (signals == null) return;
        Debug.Log($"[QuestEventBootstrap] Raising '{eventKey}' because quest '{questId}' is {state}");
        signals.RaiseCustom(eventKey);
        if (runOnce) _fired = true;
        */
    }
}
