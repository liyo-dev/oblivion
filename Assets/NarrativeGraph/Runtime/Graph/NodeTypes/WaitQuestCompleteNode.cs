// WaitQuestCompleteNode.cs
using System;

[Serializable]
[SavePoint("Seguro guardar mientras espera quests")]
public sealed class WaitQuestCompleteNode : NarrativeNode
{
    public string questId;
    Action _cb;
    public bool debugLogs = false;

    void Log(string msg)
    {
        if (debugLogs)
            UnityEngine.Debug.Log($"[WaitQuestCompleteNode] {msg}");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (ctx.Signals.IsQuestCompleted(questId))
        {
            Log($"Quest '{questId}' already completed → advancing");
            onReadyToAdvance?.Invoke();
            return;
        }
        _cb = () =>
        {
            Log($"Quest '{questId}' completion received → advancing");
            onReadyToAdvance?.Invoke();
        };
        Log($"Waiting quest '{questId}' completion");
        ctx.Signals.OnQuestCompleted(questId, _cb);
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_cb != null)
        {
            ctx.Signals.OffQuestCompleted(questId, _cb);
            Log($"Unsubscribed from quest '{questId}' completion");
        }
        _cb = null;
    }
}
