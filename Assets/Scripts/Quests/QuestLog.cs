// Scripts/Quests/QuestLog.cssing System.Collections.Generic;

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class QuestLog : MonoBehaviour
{
    Dictionary<string,(int stage, QuestState state)> _map = new();

    public void StartQuest(QuestSO q){
        if(!_map.ContainsKey(q.questId)) _map[q.questId]=(0,QuestState.Active);
    }
    public void Advance(QuestSO q, int add=1){
        if(!_map.TryGetValue(q.questId, out var s)) return;
        if (s.state!=QuestState.Active) return;
        s.stage += add;
        if (s.stage >= q.stages){ s.stage=q.stages; s.state=QuestState.Completed; }
        _map[q.questId]=s;
    }
    public bool IsActive(string id)=> _map.TryGetValue(id, out var s) && s.state==QuestState.Active;
    public bool IsCompleted(string id)=> _map.TryGetValue(id, out var s) && s.state==QuestState.Completed;
    public int  Stage(string id)=> _map.TryGetValue(id, out var s) ? s.stage : 0;

    // Save/Load bridge
    public List<QuestProgressEntry> Export(){
        var list=new List<QuestProgressEntry>();
        foreach(var kv in _map) list.Add(new QuestProgressEntry{questId=kv.Key, stage=kv.Value.stage, completed=kv.Value.state==QuestState.Completed});
        return list;
    }
    public void Import(List<QuestProgressEntry> data){
        _map.Clear();
        if(data==null) return;
        foreach(var e in data)
            _map[e.questId]=(e.stage, e.completed?QuestState.Completed:QuestState.Active);
    }
}