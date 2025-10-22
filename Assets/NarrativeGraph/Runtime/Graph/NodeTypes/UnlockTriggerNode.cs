// UnlockTriggerNode.cs
using System;

[Obsolete("UnlockTriggerNode está obsoleto. Usa UnlockAbilitiesNode u otros mecanismos para desbloquear triggers.")]
[Serializable]
public sealed class UnlockTriggerNode : NarrativeNode
{
    public string triggerKey;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Ejemplo: levantar un custom signal para el trigger
        ctx.Signals.RaiseCustom(triggerKey);
        onReadyToAdvance?.Invoke();
    }
}
