using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Tooltip("Catálogo opcional para arrancar quests por ID aunque no se hayan añadido antes.")]
    [SerializeField] private List<QuestData> questCatalog = new();

    // runtime: questId -> RuntimeQuest
    private readonly Dictionary<string, RuntimeQuest> _runtime = new(64);

    // visibilidad por quest (archivada vs visible)
    private readonly Dictionary<string, QuestVisibility> _visibility = new(StringComparer.Ordinal);
    // seguimiento ("seguir" misión en el tracker)
    private readonly HashSet<string> _followed = new(StringComparer.Ordinal);

    // índice: conditionId -> lista de (questId, stepIndex) para completar en O(1)
    private readonly Dictionary<string, List<StepRef>> _conditionIndex = new(64, StringComparer.Ordinal);

    // Eventos públicos para UI/lógica externa
    public event Action<string> OnQuestStarted;
    public event Action<string> OnQuestCompleted;
    public event Action<string, int> OnStepCompleted;
    public event Action OnQuestsChanged;
    public event Action<string, QuestVisibility> OnQuestVisibilityChanged;
    public event Action<string, bool> OnQuestFollowChanged;

    #region Unity
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region API básica
    public bool HasQuest(string questId) => _runtime.ContainsKey(questId);

    public QuestState GetState(string questId)
        => _runtime.TryGetValue(questId, out var rq) ? rq.State : QuestState.Inactive;

    public IEnumerable<RuntimeQuest> GetAll() => _runtime.Values;

    public QuestVisibility GetVisibility(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return QuestVisibility.Visible;
        if (_visibility.TryGetValue(questId, out var v)) return v;
        return QuestVisibility.Visible;
    }

    public void SetVisibility(string questId, QuestVisibility state)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (!_runtime.ContainsKey(questId)) return;

        var current = GetVisibility(questId);
        if (current == state) return;

        _visibility[questId] = state;
        OnQuestVisibilityChanged?.Invoke(questId, state);
        OnQuestsChanged?.Invoke();
    }

    public bool IsFollowed(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return false;
        return _followed.Contains(questId);
    }

    public void SetFollowed(string questId, bool followed)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (!_runtime.ContainsKey(questId)) return;

        bool changed;
        if (followed)
            changed = _followed.Add(questId);
        else
            changed = _followed.Remove(questId);

        if (changed)
        {
            OnQuestFollowChanged?.Invoke(questId, followed);
            OnQuestsChanged?.Invoke();
        }
    }

    public void AddQuest(QuestData data)
    {
        if (!data || string.IsNullOrEmpty(data.questId) || _runtime.ContainsKey(data.questId)) return;

        var rq = new RuntimeQuest(data);
        _runtime[data.questId] = rq;
        _visibility[data.questId] = QuestVisibility.Visible;
        _followed.Remove(data.questId);
        IndexQuestConditions(rq);
        OnQuestsChanged?.Invoke();
    }

    public void StartQuest(string questId)
    {
        if (!_runtime.TryGetValue(questId, out var rq))
        {
            var data = questCatalog.FirstOrDefault(q => q && q.questId == questId);
            if (!data) return;

            rq = new RuntimeQuest(data);
            _runtime[questId] = rq;
            _visibility[questId] = QuestVisibility.Visible;
            _followed.Remove(questId);
            IndexQuestConditions(rq);
        }

        if (rq.State == QuestState.Inactive)
        {
            rq.State = QuestState.Active;
            OnQuestStarted?.Invoke(questId);
            OnQuestsChanged?.Invoke();
        }
    }

    public void CompleteQuest(string questId)
    {
        if (!_runtime.TryGetValue(questId, out var rq)) return;
        if (rq.State == QuestState.Completed) return;

        rq.State = QuestState.Completed;
        OnQuestCompleted?.Invoke(questId);
        ArchiveCompletedQuest(questId);
        OnQuestsChanged?.Invoke();
    }

    public void MarkStepDone(string questId, int stepIndex)
    {
        if (!_runtime.TryGetValue(questId, out var rq)) return;
        if (rq.State != QuestState.Active) return;
        if ((uint)stepIndex >= (uint)rq.Steps.Length) return;

        var step = rq.Steps[stepIndex];
        if (step.completed) return;

        step.completed = true;
        OnStepCompleted?.Invoke(questId, stepIndex);

        if (AllStepsCompleted(rq))
        {
            rq.State = QuestState.Completed;
            OnQuestCompleted?.Invoke(questId);
            ArchiveCompletedQuest(questId);
        }

        OnQuestsChanged?.Invoke();
    }

    public bool IsStepCompleted(string questId, int stepIndex)
        => _runtime.TryGetValue(questId, out var rq)
           && (uint)stepIndex < (uint)rq.Steps.Length
           && rq.Steps[stepIndex].completed;

    public bool AreAllStepsCompleted(string questId)
        => _runtime.TryGetValue(questId, out var rq) && AllStepsCompleted(rq);

    public void CompleteByCondition(string conditionId)
    {
        if (string.IsNullOrEmpty(conditionId)) return;
        if (!_conditionIndex.TryGetValue(conditionId, out var list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            var sr = list[i];
            if (GetState(sr.questId) != QuestState.Active) continue;
            MarkStepDone(sr.questId, sr.stepIndex);
        }
    }

    void ArchiveCompletedQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        SetFollowed(questId, false);
        SetVisibility(questId, QuestVisibility.Hidden);
    }
    #endregion

    #region Persistencia vía flags (export/import)
    // Formato de flags:
    //   QUEST_COMPLETED:<questId>
    //   QUEST_ACTIVE:<questId>
    //   QUEST_STEP_DONE:<questId>:<stepIndex>
    //   QUEST_ARCHIVED:<questId>
    //   QUEST_FOLLOWED:<questId>

    private const string Q_COMPLETED = "QUEST_COMPLETED:";
    private const string Q_ACTIVE    = "QUEST_ACTIVE:";
    private const string Q_STEP_DONE = "QUEST_STEP_DONE:";
    private const string Q_ARCHIVED  = "QUEST_ARCHIVED:";
    private const string Q_FOLLOWED  = "QUEST_FOLLOWED:"; // legacy alias
    private const string Q_TRACKED   = "QUEST_TRACKED:";

    /// <summary>Reconstruye el estado a partir de flags del perfil.</summary>
    public void RestoreFromProfileFlags(IReadOnlyList<string> flags)
    {
        ResetAllQuests();

        if (flags == null || flags.Count == 0) return;

        var toActive = new HashSet<string>(StringComparer.Ordinal);
        var toArchived = new HashSet<string>(StringComparer.Ordinal);
        var toFollowed = new HashSet<string>(StringComparer.Ordinal);

        // 1) Marcar completadas / recopilar activas
        for (int i = 0; i < flags.Count; i++)
        {
            var f = flags[i];
            if (string.IsNullOrEmpty(f)) continue;

            if (f.StartsWith(Q_COMPLETED, StringComparison.Ordinal))
            {
                var qid = f.Substring(Q_COMPLETED.Length);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out var rq);
                rq.State = QuestState.Completed;
                _visibility[qid] = QuestVisibility.Hidden;
                // Marcar todos los pasos como completados si la misión está completada
                if (rq.Steps != null)
                {
                    for (int s = 0; s < rq.Steps.Length; s++)
                        rq.Steps[s].completed = true;
                }
            }
            else if (f.StartsWith(Q_ACTIVE, StringComparison.Ordinal))
            {
                var qid = f.Substring(Q_ACTIVE.Length);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out _);
                toActive.Add(qid);
            }
            else if (f.StartsWith(Q_ARCHIVED, StringComparison.Ordinal))
            {
                var qid = f.Substring(Q_ARCHIVED.Length);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out _);
                toArchived.Add(qid);
            }
            else if (f.StartsWith(Q_FOLLOWED, StringComparison.Ordinal) || f.StartsWith(Q_TRACKED, StringComparison.Ordinal))
            {
                var prefixLen = f.StartsWith(Q_FOLLOWED, StringComparison.Ordinal) ? Q_FOLLOWED.Length : Q_TRACKED.Length;
                var qid = f.Substring(prefixLen);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out _);
                toFollowed.Add(qid);
            }
        }

        foreach (var qid in toActive)
        {
            if (_runtime.TryGetValue(qid, out var rq) && rq.State != QuestState.Completed)
                rq.State = QuestState.Active;
        }

        // 2) Aplicar visibilidad archivada
        foreach (var qid in toArchived)
        {
            if (_runtime.ContainsKey(qid))
                _visibility[qid] = QuestVisibility.Hidden;
        }

        // 3) Aplicar seguimiento
        _followed.Clear();
        foreach (var qid in toFollowed)
        {
            if (_runtime.ContainsKey(qid))
            {
                _followed.Add(qid);
                // No sobrescribir "Hidden" con "Tracked": si está archivada, mantener Hidden
                if (GetVisibility(qid) != QuestVisibility.Hidden)
                    _visibility[qid] = QuestVisibility.Tracked;
            }
        }

        // 4b) Auto-archivar completadas y limpiar seguimiento
        foreach (var kvp in _runtime)
        {
            if (kvp.Value.State == QuestState.Completed)
            {
                _followed.Remove(kvp.Key);
                _visibility[kvp.Key] = QuestVisibility.Hidden;
            }
        }

        // 4) Marcar pasos completados
        for (int i = 0; i < flags.Count; i++)
        {
            var f = flags[i];
            if (string.IsNullOrEmpty(f)) continue;
            if (!f.StartsWith(Q_STEP_DONE, StringComparison.Ordinal)) continue;

            var rest = f.Substring(Q_STEP_DONE.Length);
            var sep = rest.LastIndexOf(':');
            if (sep <= 0) continue;

            var qid = rest.Substring(0, sep);
            var idxStr = rest.Substring(sep + 1);
            if (!int.TryParse(idxStr, out int stepIdx)) continue;

            EnsureRuntimeQuest(qid, out var rq2);
            if (rq2.State == QuestState.Inactive) rq2.State = QuestState.Active;
            if ((uint)stepIdx < (uint)rq2.Steps.Length)
                rq2.Steps[stepIdx].completed = true;
        }

        OnQuestsChanged?.Invoke();

        // helper local
        void EnsureRuntimeQuest(string questId, out RuntimeQuest rqOut)
        {
            if (!_runtime.TryGetValue(questId, out rqOut))
            {
                var data = questCatalog.FirstOrDefault(q => q && q.questId == questId);
                if (data != null)
                {
                    rqOut = new RuntimeQuest(data);
                    _runtime[questId] = rqOut;
                    IndexQuestConditions(rqOut);
                }
            }
        }
    }

    /// <summary>Vuelca el estado actual a una lista de flags (determinista).</summary>
    public void ExportFlags(List<string> outFlags)
    {
        if (outFlags == null) return;

        foreach (var rq in _runtime.Values)
        {
            var state = rq.State;
            if (state == QuestState.Completed)
            {
                outFlags.Add(Q_COMPLETED + rq.Id);
            }
            else if (state == QuestState.Active)
            {
                outFlags.Add(Q_ACTIVE + rq.Id);
                for (int i = 0; i < rq.Steps.Length; i++)
                    if (rq.Steps[i].completed)
                        outFlags.Add($"{Q_STEP_DONE}{rq.Id}:{i}");
            }

            // Visibilidad: si está archivada, emitir flag incluso cuando esté completada
            var vis = GetVisibility(rq.Id);
            if (vis == QuestVisibility.Hidden)
            {
                outFlags.Add(Q_ARCHIVED + rq.Id);
            }
            else if (IsFollowed(rq.Id) || vis == QuestVisibility.Tracked)
            {
                // Seguimiento: emitir solo si NO está archivada. Evita restaurar archivadas como "tracked".
                outFlags.Add(Q_TRACKED + rq.Id);
            }
        }
    }
    #endregion

    #region Internals
    private static bool AllStepsCompleted(RuntimeQuest rq)
    {
        var steps = rq.Steps;
        for (int i = 0; i < steps.Length; i++)
            if (!steps[i].completed) return false;
        return true;
    }

    private void IndexQuestConditions(RuntimeQuest rq)
    {
        var steps = rq.Steps;
        for (int i = 0; i < steps.Length; i++)
        {
            var cid = steps[i].conditionId;
            if (string.IsNullOrEmpty(cid)) continue;

            if (!_conditionIndex.TryGetValue(cid, out var lst))
            {
                lst = new List<StepRef>(2);
                _conditionIndex[cid] = lst;
            }
            lst.Add(new StepRef(rq.Id, i));
        }
    }

    private readonly struct StepRef
    {
        public readonly string questId;
        public readonly int stepIndex;
        public StepRef(string q, int i) { questId = q; stepIndex = i; }
    }

    // ===== Runtime model =====
    public class RuntimeQuest
    {
        public string Id => Data.questId;
        public QuestData Data { get; }
        public QuestState State { get; set; }
        public QuestStep[] Steps { get; }

        public RuntimeQuest(QuestData data)
        {
            Data = data;
            State = QuestState.Inactive;

            if (data.steps == null || data.steps.Length == 0)
            {
                Steps = Array.Empty<QuestStep>();
                return;
            }

            Steps = new QuestStep[data.steps.Length];
            for (int i = 0; i < data.steps.Length; i++)
            {
                var s = data.steps[i];
                Steps[i] = new QuestStep
                {
                    description = s.description,
                    conditionId = s.conditionId,
                    completed = false
                };
            }
        }
    }
    #endregion

    /// <summary>
    /// Elimina todas las misiones activas y su progreso. Útil para nueva partida.
    /// </summary>
    public void ResetAllQuests()
    {
        _runtime.Clear();
        _conditionIndex.Clear();
        _visibility.Clear();
        _followed.Clear();
        OnQuestsChanged?.Invoke();
    }
}
