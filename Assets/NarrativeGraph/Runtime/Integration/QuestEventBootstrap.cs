using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class QuestEventBootstrap : MonoBehaviour
{
    [SerializeField] private string questId;
    [SerializeField] private QuestState minimumState = QuestState.Completed;
    [SerializeField] private string eventKey;
    [SerializeField] private bool runOnce = true;

    bool _fired;

    void Start()
    {
        TryFire();
    }

    void OnEnable()
    {
        if (!runOnce) TryFire();
    }

    void TryFire()
    {
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
    }
}
