using System;

public interface INarrativeSignals
{
    // QUEST
    void OfferQuest(string questId, object npcContext);
    bool IsQuestCompleted(string questId);
    void OnQuestCompleted(string questId, Action cb);
    void OffQuestCompleted(string questId, Action cb);
    void StartQuest(string questId, object npcContext);
    void CompleteQuest(string questId);

    // BATTLE
    void OnBattleWon(object arena, Action cb);
    void OffBattleWon(object arena, Action cb);

    // CUSTOM EVENTS 
    void RaiseCustom(string key);
    void OnCustom(string key, Action cb);
    void OffCustom(string key, Action cb);
}