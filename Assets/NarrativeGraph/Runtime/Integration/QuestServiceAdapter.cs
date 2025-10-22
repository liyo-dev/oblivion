using System;
using System.Collections.Generic;
using UnityEngine;

/// Adapta tu QuestManager clásico al IQuestService del grafo.
public class QuestServiceAdapter : MonoBehaviour, IQuestService
{
    [SerializeField] bool debugLogs = false;

    QuestManager QM => QuestManager.Instance;
    readonly Dictionary<string, List<Action>> waitingCompleted = new();
    bool subscribed;

    void OnEnable() { TrySubscribe(); }
    void OnDisable() { TryUnsubscribe(); waitingCompleted.Clear(); }
    
    public void StartQuest(string questId)
    {
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] StartQuest({questId})");
        var qm = QuestManager.Instance;
        if (qm == null) { Debug.LogWarning("[QuestServiceAdapter] QuestManager.Instance = null"); return; }
        qm.StartQuest(questId);
    }


    void TrySubscribe()
    {
        if (subscribed || QM == null) return;
        QM.OnQuestsChanged += HandleQuestsChanged;
        subscribed = true;
        if (debugLogs) Debug.Log("[QuestServiceAdapter] Subscribed");
    }
    void TryUnsubscribe()
    {
        if (!subscribed) return;
        if (QM != null) QM.OnQuestsChanged -= HandleQuestsChanged;
        subscribed = false;
    }

    void HandleQuestsChanged()
    {
        if (QM == null) return;
        var completedNow = new List<string>();
        foreach (var kv in waitingCompleted)
        {
            var qid = kv.Key;
            QuestState st;
            try { st = QM.GetState(qid); } catch { continue; }
            if (st == QuestState.Completed)
            {
                if (debugLogs) Debug.Log($"[QuestServiceAdapter] Completed → {qid}");
                foreach (var cb in kv.Value) { try { cb?.Invoke(); } catch (Exception e) { Debug.LogException(e); } }
                completedNow.Add(qid);
            }
        }
        foreach (var q in completedNow) waitingCompleted.Remove(q);
    }

    // ===== IQuestService =====
    public void Offer(string questId, object npcCtx)
    {
        var qm = QuestManager.Instance;
        if (qm == null || string.IsNullOrWhiteSpace(questId)) return;

        // Si tu QuestManager tiene oferta/diálogo propio, úsalo:
        var t = qm.GetType();
        var offerM =
            t.GetMethod("OfferQuest") ??
            t.GetMethod("ShowOffer") ??
            t.GetMethod("ShowQuestOffer");

        if (offerM != null)
        {
            // intenta pasar questId y (opcional) npcCtx si el método lo acepta
            var pars = offerM.GetParameters();
            if (pars.Length == 2) offerM.Invoke(qm, new object[] { questId, npcCtx });
            else                   offerM.Invoke(qm, new object[] { questId });
        }
        else
        {
            // Fallback: si no hay UI de oferta, inicia directo
            qm.StartQuest(questId);
        }
    }

    public bool IsCompleted(string questId)
    {
        if (QM == null) return false;
        try { return QM.GetState(questId) == QuestState.Completed; } catch { return false; }
    }

    public void OnCompleted(string questId, Action cb)
    {
        if (cb == null || string.IsNullOrEmpty(questId)) return;
        if (IsCompleted(questId)) { cb(); return; }
        if (!waitingCompleted.TryGetValue(questId, out var list))
        {
            list = new List<Action>();
            waitingCompleted[questId] = list;
        }
        list.Add(cb);
        TrySubscribe();
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] OnCompleted subscribed → {questId}");
    }

    public void OffCompleted(string questId, Action cb)
    {
        if (cb == null) return;
        if (waitingCompleted.TryGetValue(questId, out var list))
        {
            list.RemoveAll(a => a == cb);
            if (list.Count == 0) waitingCompleted.Remove(questId);
        }
    }

    public void Complete(string questId)
    {
        if (QM == null) return;
        var t = QM.GetType();
        var m = t.GetMethod("CompleteQuest") ?? t.GetMethod("Complete") ?? t.GetMethod("CompleteQuestById");
        if (m != null)
        {
            if (debugLogs) Debug.Log($"[QuestServiceAdapter] Complete → {m.Name}({questId})");
            try { m.Invoke(QM, new object[] { questId }); } catch (Exception e) { Debug.LogException(e); }
        }
        else if (debugLogs) Debug.Log("[QuestServiceAdapter] QuestManager no tiene método de completar (no-op).");
    }
}
