using System;
using System.Collections.Generic;
using UnityEngine;
using QuestState = QuesStateEnum;

[Serializable]
public struct QuestProgressEntry
{
    public string questId;
    public int stage;
    public bool completed;
}

[DisallowMultipleComponent]
public class QuestLog : MonoBehaviour
{
    // Mapa: questId -> (stage actual, estado)
    private readonly Dictionary<string, (int stage, QuestState state)> _map = new();

    public void StartQuest(QuestSO q)
    {
        if (q == null || string.IsNullOrEmpty(q.questId)) return;
        if (!_map.ContainsKey(q.questId))
            _map[q.questId] = (0, QuestState.Active);
    }

    public void Advance(QuestSO q, int add = 1)
    {
        if (q == null || string.IsNullOrEmpty(q.questId)) return;
        if (!_map.TryGetValue(q.questId, out var s)) return;
        if (s.state != QuestState.Active) return;

        s.stage += add;
        if (s.stage >= q.stages)
        {
            s.stage = q.stages;
            s.state = QuestState.Completed;
        }
        _map[q.questId] = s;
    }

    public bool IsActive(string id) =>
        _map.TryGetValue(id, out var s) && s.state == QuestState.Active;

    public bool IsCompleted(string id) =>
        _map.TryGetValue(id, out var s) && s.state == QuestState.Completed;

    public int Stage(string id) =>
        _map.TryGetValue(id, out var s) ? s.stage : 0;

    // --------- Save/Load bridge ---------

    public List<QuestProgressEntry> Export()
    {
        var list = new List<QuestProgressEntry>(_map.Count);
        foreach (var kv in _map)
        {
            list.Add(new QuestProgressEntry
            {
                questId = kv.Key,
                stage = kv.Value.stage,
                completed = kv.Value.state == QuestState.Completed
            });
        }
        return list;
    }

    public void Import(List<QuestProgressEntry> data)
    {
        _map.Clear();
        if (data == null) return;

        foreach (var e in data)
        {
            if (string.IsNullOrEmpty(e.questId)) continue;
            var state = e.completed ? QuestState.Completed : QuestState.Active;
            _map[e.questId] = (Mathf.Max(0, e.stage), state);
        }
    }
}
