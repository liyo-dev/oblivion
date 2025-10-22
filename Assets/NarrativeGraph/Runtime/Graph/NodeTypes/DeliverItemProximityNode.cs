// DeliverItemProximityNode.cs
using System;
using UnityEngine;

[Serializable]
[Obsolete("DeliverItemProximityNode eliminado - ya no usar. Sustituye por nodos específicos.")]
public sealed class DeliverItemProximityNode : NarrativeNode
{
    // Nodo obsoleto: placeholder para mantener compatibilidad de serialización.
    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        onReadyToAdvance?.Invoke();
    }

    public override void Exit(NarrativeContext ctx) { }
}