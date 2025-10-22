using System;
using UnityEngine;

[Serializable]
public sealed class StartQuestNode : NarrativeNode
{
    public string questId;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        Debug.Log($"[StartQuestNode] Start {questId}");
        ctx.Signals.StartQuest(questId, null);
        ready?.Invoke();
    }

}