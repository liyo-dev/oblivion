using System;
using System.Collections.Generic;
using UnityEngine;

public class DefaultNarrativeSignals : MonoBehaviour, INarrativeSignals
{
    public static DefaultNarrativeSignals Instance { get; private set; }

    [Tooltip("Componente que implementa IQuestService (p.ej., QuestServiceAdapter)")]
    public MonoBehaviour questServiceProvider;

    IQuestService _qs;

    // Suscriptores por clave
    readonly Dictionary<string, Action> _custom = new();
    // Eventos que llegaron antes de que hubiera oyentes (se consumen al suscribirse)
    readonly HashSet<string> _pending = new();

    void Awake()
    {
        Instance = this; 
    }

    IQuestService QS
    {
        get
        {
            if (_qs != null) return _qs;
            _qs = questServiceProvider as IQuestService
                  ?? GetComponent<IQuestService>()
                  ?? FindAnyObjectByType<QuestServiceAdapter>(FindObjectsInactive.Include);
            return _qs;
        }
    }
    
    // ===================== QUEST =====================
    public void OfferQuest(string questId, object npcContext)
    {
        Debug.Log($"[Signals] OfferQuest {questId} (svc={(QS!=null?QS.GetType().Name:"NULL")})");
        QS?.Offer(questId, npcContext);
    }

    public bool IsQuestCompleted(string questId) => QS != null && QS.IsCompleted(questId);
    public void OnQuestCompleted(string questId, Action cb) => QS?.OnCompleted(questId, cb);
    public void OffQuestCompleted(string questId, Action cb) => QS?.OffCompleted(questId, cb);

    public void StartQuest(string questId, object npcContext)
    {
        Debug.Log($"[Signals] StartQuest {questId} (svc={(QS!=null?QS.GetType().Name:"NULL")})");
        QS?.StartQuest(questId);
    }

    public void CompleteQuest(string questId) => QS?.Complete(questId);

    // ============= CUSTOM (sticky) =============
    public void RaiseCustom(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        if (_custom.TryGetValue(key, out var a) && a != null)
        {
            Debug.Log($"[Signals] Custom: {key}");
            try { a.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }
        else
        {
            // nadie suscrito → lo dejamos pendiente
            _pending.Add(key);
            Debug.Log($"[Signals] Custom: {key} (sin oyentes → pendiente)");
        }
    }

    public void OnCustom(string key, Action cb)
    {
        if (string.IsNullOrWhiteSpace(key) || cb == null) return;

        // Si estaba pendiente, lo consumimos inmediatamente (una sola vez) y NO lo guardamos
        if (_pending.Remove(key))
        {
            try { cb(); } catch (Exception e) { Debug.LogException(e); }
            return;
        }

        if (_custom.TryGetValue(key, out var a)) _custom[key] = a + cb;
        else _custom[key] = cb;
    }

    public void OffCustom(string key, Action cb)
    {
        if (string.IsNullOrWhiteSpace(key) || cb == null) return;
        if (_custom.TryGetValue(key, out var a))
        {
            a -= cb;
            if (a == null) _custom.Remove(key);
            else _custom[key] = a;
        }
    }

    // ====== BATTLE (placeholder) ======
    public void OnBattleWon(object arena, Action cb) { }
    public void OffBattleWon(object arena, Action cb) { }
}
