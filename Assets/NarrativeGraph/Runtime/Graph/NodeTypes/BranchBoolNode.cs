using System;

[Serializable]
public sealed class BranchBoolNode : NarrativeNode
{
    public string variableName;
    public bool invert = false;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        bool val = false;
        try { if (ctx?.Blackboard != null) val = ctx.Blackboard.Get<bool>(variableName, false); } catch { }
        if (invert) val = !val;
        // Original implementation no-ops with val; keep behavior: simply advance
        onReadyToAdvance?.Invoke();
    }
}
