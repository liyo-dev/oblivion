// ActivateGameObjectNode.cs
using System;
using UnityEngine;

[Serializable]
public sealed class ActivateGameObjectNode : NarrativeNode
{
    public ExposedReference<GameObject> target;
    public bool setActive = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var go = target.Resolve(ctx.Exposed);
        if (go) go.SetActive(setActive);
        onReadyToAdvance?.Invoke();
    }
}