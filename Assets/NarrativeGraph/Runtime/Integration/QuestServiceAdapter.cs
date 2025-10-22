using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// Adapta tu QuestManager clásico al IQuestService del grafo.
public class QuestServiceAdapter : MonoBehaviour, IQuestService
{
    [SerializeField] bool debugLogs;

    QuestManager Qm => QuestManager.Instance;
    readonly Dictionary<string, List<Action>> _waitingCompleted = new();
    bool _subscribed;

    void OnEnable() { TrySubscribe(); }
    void OnDisable() { TryUnsubscribe(); _waitingCompleted.Clear(); }
    
    public void StartQuest(string questId)
    {
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] StartQuest({questId})");
        var qm = QuestManager.Instance;
        if (qm == null) { Debug.LogWarning("[QuestServiceAdapter] QuestManager.Instance = null"); return; }
        qm.StartQuest(questId);
    }


    void TrySubscribe()
    {
        if (_subscribed || Qm == null) return;
        Qm.OnQuestsChanged += HandleQuestsChanged;
        _subscribed = true;
        if (debugLogs) Debug.Log("[QuestServiceAdapter] Subscribed");
    }
    void TryUnsubscribe()
    {
        if (!_subscribed) return;
        if (Qm != null) Qm.OnQuestsChanged -= HandleQuestsChanged;
        _subscribed = false;
    }

    void HandleQuestsChanged()
    {
        if (Qm == null) return;
        var completedNow = new List<string>();

        // Iterar sobre una copia de las entradas para permitir modificaciones durante callbacks
        var entries = _waitingCompleted.ToList();
        foreach (var kv in entries)
        {
            var qid = kv.Key;
            QuestState st;
            try { st = Qm.GetState(qid); } catch { continue; }
            if (st == QuestState.Completed)
            {
                if (debugLogs) Debug.Log($"[QuestServiceAdapter] Completed → {qid}");

                // Iterar sobre una copia de la lista de callbacks porque los callbacks pueden modificarla
                var callbacks = kv.Value?.ToArray();
                if (callbacks != null)
                {
                    foreach (var cb in callbacks)
                    {
                        try { cb?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                    }
                }
                completedNow.Add(qid);
            }
        }

        // Remover fuera del bucle principal
        foreach (var q in completedNow) _waitingCompleted.Remove(q);

        // Si ya no hay callbacks pendientes, podemos desuscribirnos para ahorrar trabajo
        if (_waitingCompleted.Count == 0)
        {
            TryUnsubscribe();
            if (debugLogs) Debug.Log("[QuestServiceAdapter] No pending completions; unsubscribed.");
        }
    }

    // Helper para construir siempre un object[] sin warnings de inferencia/cast
    static object[] MakeArgs(params object[] args) => args;

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
            if (pars.Length == 2) offerM.Invoke(qm, MakeArgs(questId, npcCtx));
            else                   offerM.Invoke(qm, MakeArgs(questId));
        }
        else
        {
            // Fallback: si no hay UI de oferta, inicia directo
            qm.StartQuest(questId);
        }
    }

    public bool IsCompleted(string questId)
    {
        if (Qm == null) return false;
        try { return Qm.GetState(questId) == QuestState.Completed; } catch { return false; }
    }

    public void OnCompleted(string questId, Action cb)
    {
        if (cb == null || string.IsNullOrEmpty(questId)) return;
        if (IsCompleted(questId)) { cb(); return; }
        if (!_waitingCompleted.TryGetValue(questId, out var list))
        {
            list = new List<Action>();
            _waitingCompleted[questId] = list;
        }
        list.Add(cb);
        TrySubscribe();
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] OnCompleted subscribed → {questId}");
    }

    public void OffCompleted(string questId, Action cb)
    {
        if (cb == null) return;
        if (_waitingCompleted.TryGetValue(questId, out var list))
        {
            list.RemoveAll(a => a == cb);
            if (list.Count == 0) _waitingCompleted.Remove(questId);
        }
    }

    public void Complete(string questId)
    {
        if (Qm == null) return;
        var t = Qm.GetType();
        var m = t.GetMethod("CompleteQuest") ?? t.GetMethod("Complete") ?? t.GetMethod("CompleteQuestById");
        if (m != null)
        {
            if (debugLogs) Debug.Log($"[QuestServiceAdapter] Complete → {m.Name}({questId})");
            try { m.Invoke(Qm, MakeArgs(questId)); } catch (Exception e) { Debug.LogException(e); }
        }
        else if (debugLogs) Debug.Log("[QuestServiceAdapter] QuestManager no tiene método de completar (no-op).");
    }
}
