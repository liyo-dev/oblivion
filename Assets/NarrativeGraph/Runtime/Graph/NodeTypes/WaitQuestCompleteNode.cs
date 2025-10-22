// WaitQuestCompleteNode.cs
using System;

[Serializable]
public sealed class WaitQuestCompleteNode : NarrativeNode
{
    public string questId;
    Action _cb;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (ctx.Signals.IsQuestCompleted(questId)) { onReadyToAdvance?.Invoke(); return; }
        _cb = () => onReadyToAdvance?.Invoke();
        ctx.Signals.OnQuestCompleted(questId, _cb);
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_cb != null) ctx.Signals.OffQuestCompleted(questId, _cb);
        _cb = null;
    }
}