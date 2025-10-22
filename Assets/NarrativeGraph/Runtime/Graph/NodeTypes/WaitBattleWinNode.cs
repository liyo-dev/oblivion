// WaitBattleWinNode.cs
using System;

[Serializable]
public sealed class WaitBattleWinNode : NarrativeNode
{
    public object arenaContext; // si quieres pasar una referencia/ID
    Action _cb;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        _cb = () => onReadyToAdvance?.Invoke();
        ctx.Signals.OnBattleWon(arenaContext, _cb);
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_cb != null) ctx.Signals.OffBattleWon(arenaContext, _cb);
        _cb = null;
    }
}