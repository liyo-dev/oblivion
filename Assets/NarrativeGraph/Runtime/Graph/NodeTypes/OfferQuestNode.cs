// OfferQuestNode.cs
using System;

[Obsolete("OfferQuestNode está obsoleto. Usa Signals.StartQuest/OfferQuest directamente desde otros nodos o adapta el flujo.")]
[Serializable]
public sealed class OfferQuestNode : NarrativeNode
{
    public string questId;
    public string npcName; // opcional, por UX en editor
    public object npcContext; // puedes dejar null; sirve para tu sistema si lo usas

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        ctx.Signals.OfferQuest(questId, npcContext);
        onReadyToAdvance?.Invoke();
    }
}