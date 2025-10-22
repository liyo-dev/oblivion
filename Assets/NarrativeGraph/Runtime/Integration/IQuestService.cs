public interface IQuestService
{
    void Offer(string questId, object npcCtx);
    bool IsCompleted(string questId);
    void OnCompleted(string questId, System.Action cb);
    void OffCompleted(string questId, System.Action cb);
    void Complete(string questId);
    void StartQuest(string questId);
}