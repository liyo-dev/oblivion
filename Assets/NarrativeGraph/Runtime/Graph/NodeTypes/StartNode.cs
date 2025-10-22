// StartNode.cs
using System;

[Serializable]
public sealed class StartNode : NarrativeNode
{
    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        onReadyToAdvance?.Invoke(); // pasa inmediatamente al siguiente
    }
}