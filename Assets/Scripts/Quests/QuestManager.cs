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

    // índice: conditionId -> lista de (questId, stepIndex) para completar en O(1)
    private readonly Dictionary<string, List<StepRef>> _conditionIndex = new(64, StringComparer.Ordinal);

    // Eventos públicos para UI/lógica externa
    public event Action<string> OnQuestStarted;
    public event Action<string> OnQuestCompleted;
    public event Action<string, int> OnStepCompleted;
    public event Action OnQuestsChanged;

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

    public void AddQuest(QuestData data)
    {
        if (!data || string.IsNullOrEmpty(data.questId) || _runtime.ContainsKey(data.questId)) return;

        var rq = new RuntimeQuest(data);
        _runtime[data.questId] = rq;
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
    #endregion

    #region Persistencia vía flags (export/import)
    // Formato de flags:
    //   QUEST_COMPLETED:<questId>
    //   QUEST_ACTIVE:<questId>
    //   QUEST_STEP_DONE:<questId>:<stepIndex>

    private const string Q_COMPLETED = "QUEST_COMPLETED:";
    private const string Q_ACTIVE    = "QUEST_ACTIVE:";
    private const string Q_STEP_DONE = "QUEST_STEP_DONE:";

    /// <summary>Reconstruye el estado a partir de flags del perfil.</summary>
    public void RestoreFromProfileFlags(IReadOnlyList<string> flags)
    {
        ResetAllQuests();

        if (flags == null || flags.Count == 0) return;

        var toActive = new HashSet<string>(StringComparer.Ordinal);

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
        }

        foreach (var qid in toActive)
        {
            if (_runtime.TryGetValue(qid, out var rq) && rq.State != QuestState.Completed)
                rq.State = QuestState.Active;
        }

        // 2) Marcar pasos completados
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
            if (rq.State == QuestState.Completed)
            {
                outFlags.Add(Q_COMPLETED + rq.Id);
                continue; // no hacen falta ACTIVE ni STEP_DONE si ya está completada
            }

            if (rq.State == QuestState.Active)
            {
                outFlags.Add(Q_ACTIVE + rq.Id);
                for (int i = 0; i < rq.Steps.Length; i++)
                    if (rq.Steps[i].completed)
                        outFlags.Add($"{Q_STEP_DONE}{rq.Id}:{i}");
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
        OnQuestsChanged?.Invoke();
    }
}
