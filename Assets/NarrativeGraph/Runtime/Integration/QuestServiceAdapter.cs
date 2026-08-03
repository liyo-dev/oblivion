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

    void OnEnable() 
    { 
        if (!_subscribed)
            StartCoroutine(WaitForQuestManagerAndSubscribe());
    }
    
    void OnDisable() { TryUnsubscribe(); _waitingCompleted.Clear(); }

    /// <summary>
    /// Espera a que QuestManager esté disponible antes de suscribirse.
    /// Esto soporta la carga aditiva de la escena Start.
    /// </summary>
    private System.Collections.IEnumerator WaitForQuestManagerAndSubscribe()
    {
        while (QuestManager.Instance == null)
        {
            yield return null;
        }
        
        TrySubscribe();
    }

    public void ResetState()
    {
        TryUnsubscribe();
        _waitingCompleted.Clear();
    }

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
        Qm.OnQuestCompleted += HandleQuestCompleted; // suscripción directa para disparo inmediato
        _subscribed = true;
        if (debugLogs) Debug.Log("[QuestServiceAdapter] Subscribed");
    }
    void TryUnsubscribe()
    {
        if (!_subscribed) return;
        if (Qm != null)
        {
            Qm.OnQuestsChanged -= HandleQuestsChanged;
            Qm.OnQuestCompleted -= HandleQuestCompleted;
        }
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

    // ===== IQuestService =====
    public void Offer(string questId, object npcCtx)
    {
        var qm = QuestManager.Instance;
        if (qm == null || string.IsNullOrWhiteSpace(questId)) return;

        // QuestManager no tiene un paso de "oferta"/diálogo propio separado del inicio
        // (no expone OfferQuest/ShowOffer/ShowQuestOffer) — iniciar directamente es el
        // comportamiento real, no un fallback. npcCtx queda sin usar hasta que exista
        // una UI de oferta explícita en QuestManager.
        qm.StartQuest(questId);
    }

    public bool IsCompleted(string questId)
    {
        if (Qm == null) return false;
        try { return Qm.GetState(questId) == QuestState.Completed; } catch { return false; }
    }

    public void OnCompleted(string questId, Action cb)
    {
        if (cb == null || string.IsNullOrEmpty(questId)) return;
        if (IsCompleted(questId))
        {
            if (debugLogs) Debug.Log($"[QuestServiceAdapter] Quest {questId} already completed; invoking callback now");
            cb();
            return;
        }
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
        if (Qm == null || string.IsNullOrWhiteSpace(questId)) return;
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] Complete → CompleteQuest({questId})");
        try { Qm.CompleteQuest(questId); } catch (Exception e) { Debug.LogException(e); }
    }

    public void CompleteStep(string questId, int stepIndex)
    {
        if (Qm == null) return;
        if (string.IsNullOrEmpty(questId) || stepIndex < 0) return;
        try
        {
            Qm.MarkStepDone(questId, stepIndex);
            if (debugLogs) Debug.Log($"[QuestServiceAdapter] CompleteStep {questId} -> {stepIndex}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void CompleteStepByConditionId(string questId, string stepConditionId)
    {
        if (Qm == null)
        {
            Debug.LogWarning($"[QuestServiceAdapter] QuestManager.Instance es null - No se puede completar step '{stepConditionId}'");
            return;
        }
        if (string.IsNullOrWhiteSpace(questId) || string.IsNullOrWhiteSpace(stepConditionId)) return;

        Qm.CompleteQuestStepByConditionId(questId, stepConditionId);
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] CompleteStepByConditionId {questId} -> {stepConditionId}");
    }

    // Disparo diferido cuando QuestManager anuncia completada
    // Se ejecuta en el siguiente frame para evitar conflictos con diálogos activos
    void HandleQuestCompleted(string questId)
    {
        if (!_waitingCompleted.TryGetValue(questId, out var list) || list == null || list.Count == 0)
            return;
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] HandleQuestCompleted → {questId} (callbacks={list.Count}) - Diferiendo al siguiente frame");
        
        // Copiar callbacks y remover de la lista antes de diferir
        var callbacks = list.ToArray();
        _waitingCompleted.Remove(questId);

        // Ejecutar callbacks en el siguiente frame para evitar conflictos
        StartCoroutine(InvokeCallbacksNextFrame(questId, callbacks));
    }

    private System.Collections.IEnumerator InvokeCallbacksNextFrame(string questId, Action[] callbacks)
    {
        // Esperar al siguiente frame
        yield return null;
        
        if (debugLogs) Debug.Log($"[QuestServiceAdapter] Ejecutando callbacks diferidos para {questId}");
        
        foreach (var cb in callbacks)
        {
            try { cb?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        if (_waitingCompleted.Count == 0)
            TryUnsubscribe();
    }
}
