using System;
using UnityEngine;

[Serializable]
public sealed class StartQuestNode : NarrativeNode
{
    public string questId;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        // Usar GUID del nodo para hacer el flag específico a esta instancia
        var questStartedKey = $"__quest_{guid}_{questId}_started";
        if (ctx.Blackboard.Get<bool>(questStartedKey, false))
        {
            Debug.Log($"[StartQuestNode:{guid}] Quest {questId} ya fue iniciada previamente por este nodo → saltando inicio");
            ready?.Invoke();
            return;
        }

        Debug.Log($"[StartQuestNode:{guid}] Start {questId}");
        ctx.Signals.StartQuest(questId, null);
        
        // Marcar la quest como iniciada por este nodo específico
        ctx.Blackboard.Set(questStartedKey, true);
        
        ready?.Invoke();
    }

}