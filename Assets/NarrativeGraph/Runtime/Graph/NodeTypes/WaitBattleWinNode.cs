// WaitBattleWinNode.cs
using System;

[Obsolete("WaitBattleWinNode está obsoleto. Usa StartBattleNode que combina Start+Wait, o usa señales directamente.")]
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

// Este nodo está marcado como obsoleto porque StartBattleNode ahora combina inicio y espera de victoria.
// Puedes eliminar el archivo si confirmas que no lo necesitas.
