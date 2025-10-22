// ActivateGameObjectNode.cs
using System;
using UnityEngine;

[Obsolete("ActivateGameObjectNode eliminado - ya no usar. Usa UnlockAbilitiesNode o PlayCinematicNode según corresponda.")]
public sealed class ActivateGameObjectNode : NarrativeNode
{
    public string targetName;
    public bool activate = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var go = GameObject.Find(targetName);
        if (go) go.SetActive(activate);
        onReadyToAdvance?.Invoke();
    }
}
