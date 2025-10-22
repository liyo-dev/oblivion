// DeliverQuestCompleteNode.cs
using System;

[Obsolete("DeliverQuestCompleteNode está obsoleto. Usa la lógica de QuestService/Signals directamente o nodos alternativos.")]
[Serializable]
public sealed class DeliverQuestCompleteNode : NarrativeNode
{
    public string questId;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        ctx.Signals.CompleteQuest(questId);
        onReadyToAdvance?.Invoke();
    }
}
