// DeliverItemProximityNode.cs
using System;
using UnityEngine;

[Serializable]
public sealed class DeliverItemProximityNode : NarrativeNode
{
    public string questIdToComplete;
    public ExposedReference<Transform> item;
    public ExposedReference<Transform> npc;
    public float radius = 2.0f;

    bool _running;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        _running = true;
        ctx.Runner.StartCoroutine(WaitRoutine(ctx, onReadyToAdvance));
    }

    System.Collections.IEnumerator WaitRoutine(NarrativeContext ctx, Action done)
    {
        var i = item.Resolve(ctx.Exposed);
        var n = npc.Resolve(ctx.Exposed);
        while (_running && i && n)
        {
            if (Vector3.Distance(i.position, n.position) <= radius)
            {
                // Completar misión mediante tus señales de quest:
                ctx.Signals.OfferQuest(questIdToComplete, null); // o llama a Complete según tu integración real
                done?.Invoke();
                yield break;
            }
            yield return null;
        }
    }

    public override void Exit(NarrativeContext ctx) { _running = false; }
}