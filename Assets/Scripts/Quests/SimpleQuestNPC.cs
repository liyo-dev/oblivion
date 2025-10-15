using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Interactable))]
public class SimpleQuestNPC : MonoBehaviour
{
    // QuestCompletionMode moved to Identifiers.cs

    [System.Serializable]
    public class QuestChainEntry
    {
        [Tooltip("Quest de esta etapa")]
        public QuestData questData;

        [Tooltip("Modo de completado")]
        public QuestCompletionMode completionMode = QuestCompletionMode.Manual;

        [Header("Detección de Items")]
        public bool autoDetectItemDelivery = false;
        [Tooltip("Índice del paso a marcar al detectar el item")]
        public int itemDeliveryStepIndex = 1;
        [Tooltip("Tag del item a detectar")]
        public string itemTag = "Untagged";

        [Header("Diálogos")]
        public DialogueAsset dlgBefore;
        public DialogueAsset dlgInProgress;
        public DialogueAsset dlgTurnIn;
        public DialogueAsset dlgCompleted;

        [Header("Eventos")]
        public UnityEvent onQuestCompleted;
    }

    [Header("Cadena de Quests")]
    [SerializeField] private List<QuestChainEntry> questChain = new();

    [Header("Detección de Items")]
    [SerializeField, Min(0f)] private float detectionRadius = 3f;
    [SerializeField, Range(0, 180)] private float detectionAngle = 90f;
    [SerializeField, Tooltip("Capa de los items para detectar con física")]
    private LayerMask detectionLayer = ~0;
    [SerializeField, Tooltip("Intervalo de escaneo (segundos)")]
    private float detectionInterval = 0.33f;
    [SerializeField] private bool showDetectionGizmos = true;

    // buffers no alocantes
    private Collider[] _overlapBuffer = new Collider[16];
    private readonly HashSet<GameObject> _consumed = new();

    private Interactable _interactable;
    private Transform _t;
    private Transform _player;
    private Coroutine _scanCo;

    void Awake()
    {
        _t = transform;
        _interactable = GetComponent<Interactable>();
        if (!_interactable)
            Debug.LogError($"[{nameof(SimpleQuestNPC)}] Falta Interactable en {name}");
    }

    void Start()
    {
        var goPlayer = GameObject.FindGameObjectWithTag("Player");
        _player = goPlayer ? goPlayer.transform : null;
        StartItemScan();
    }

    void OnEnable()
    {
        if (_scanCo == null && isActiveAndEnabled) StartItemScan();
    }

    void OnDisable()
    {
        if (_scanCo != null) { StopCoroutine(_scanCo); _scanCo = null; }
    }

    public void Interact()
    {
        var qm = QuestManager.Instance;
        if (qm == null || questChain.Count == 0) return;

        int idx = FindActiveOrCompletedIndex(qm);
        if (idx >= 0)
        {
            var entry = questChain[idx];
            var questId = entry.questData?.questId;
            if (string.IsNullOrEmpty(questId)) return;

            var state = qm.GetState(questId);
            switch (state)
            {
                case QuestState.Inactive:
                    PlayDialogue(entry.dlgBefore);
                    break;
                case QuestState.Active:
                    HandleActive(entry, qm, questId, idx);
                    break;
                case QuestState.Completed:
                    PlayDialogue(entry.dlgCompleted, () => StartCoroutine(StartNextQuestAfterDialogue(qm, idx)));
                    break;
            }
        }
        else
        {
            // todavía no hay ninguna activa o completada → ofrecer la primera
            var e0 = questChain[0];
            PlayDialogue(e0.dlgBefore);
        }
    }

    // === Flujo activo
    private void HandleActive(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        switch (entry.completionMode)
        {
            case QuestCompletionMode.AutoCompleteOnTalk:
                CompleteAllStepsAndQuest(entry, qm, questId, currentIndex);
                break;

            case QuestCompletionMode.CompleteOnTalkIfStepsReady:
                if (qm.AreAllStepsCompleted(questId))
                    FinishQuestAndChain(entry, qm, questId, currentIndex);
                else
                    PlayDialogue(entry.dlgInProgress);
                break;

            default: // Manual
                if (qm.AreAllStepsCompleted(questId))
                    FinishQuestAndChain(entry, qm, questId, currentIndex);
                else
                    PlayDialogue(entry.dlgInProgress);
                break;
        }
    }

    private void CompleteAllStepsAndQuest(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        var rqEnum = qm.GetAll();
        foreach (var rq in rqEnum)
        {
            if (rq.Id != questId) continue;
            for (int i = 0; i < rq.Steps.Length; i++)
                if (!rq.Steps[i].completed) qm.MarkStepDone(questId, i);
            break;
        }

        FinishQuestAndChain(entry, qm, questId, currentIndex);
    }

    private void FinishQuestAndChain(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        qm.CompleteQuest(questId);
        PersistQuestCompletedFlag(questId);
        entry.onQuestCompleted?.Invoke();

        PlayDialogue(entry.dlgTurnIn, () =>
        {
            StartCoroutine(StartNextQuestAfterDialogue(qm, currentIndex));
        });
    }

    private IEnumerator StartNextQuestAfterDialogue(QuestManager qm, int currentIndex)
    {
        yield return null; // un frame para evitar carreras con cierre de diálogos

        int next = currentIndex + 1;
        while (next < questChain.Count)
        {
            var e = questChain[next];
            var id = e.questData ? e.questData.questId : null;

            if (string.IsNullOrEmpty(id)) { next++; continue; }

            var state = qm.GetState(id);
            if (state == QuestState.Completed) { next++; continue; }

            if (state == QuestState.Inactive)
            {
                qm.AddQuest(e.questData);
                qm.StartQuest(id);
                if (e.dlgBefore) PlayDialogue(e.dlgBefore);
            }
            else // Active
            {
                if (e.dlgInProgress) PlayDialogue(e.dlgInProgress);
            }
            yield break;
        }

        Debug.Log($"[{nameof(SimpleQuestNPC)}] Fin de la cadena en {name}.");
    }

    private int FindActiveOrCompletedIndex(QuestManager qm)
    {
        // desde el final hacia atrás priorizando la última activa/completada
        for (int i = questChain.Count - 1; i >= 0; i--)
        {
            var e = questChain[i];
            if (!e.questData) continue;
            var s = qm.GetState(e.questData.questId);
            if (s == QuestState.Active || s == QuestState.Completed) return i;
        }
        return -1;
    }

    private void PlayDialogue(DialogueAsset dlg, System.Action onComplete = null)
    {
        if (!dlg)
        {
            onComplete?.Invoke();
            return;
        }

        if (_interactable)
        {
            _interactable.SetDialogue(dlg);
            _interactable.OnStarted?.Invoke();

            var dm = DialogueManager.Instance;
            if (dm != null)
            {
                dm.StartDialogue(dlg, _t, () =>
                {
                    _interactable.OnFinished?.Invoke();
                    onComplete?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning("[SimpleQuestNPC] DialogueManager no disponible, saltando diálogo.");
                _interactable.OnFinished?.Invoke();
                onComplete?.Invoke();
            }
        }
        else
        {
            DialogueManager.Instance?.StartDialogue(dlg, _t, onComplete);
        }
    }

    private void PersistQuestCompletedFlag(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        var profile = GameBootService.Profile;
        var saveSystem = FindFirstObjectByType<SaveSystem>();
        if (profile == null || saveSystem == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;
        preset.flags ??= new List<string>();

        string flag = $"QUEST_COMPLETED:{questId}";
        if (!preset.flags.Contains(flag)) preset.flags.Add(flag);

        profile.SaveCurrentGameState(saveSystem);
    }

    // ==== Detección de ítems (no alocante, sin Find*)
    private void StartItemScan()
    {
        if (_scanCo != null) StopCoroutine(_scanCo);
        _scanCo = StartCoroutine(ScanRoutine());
    }

    private IEnumerator ScanRoutine()
    {
        var wait = new WaitForSeconds(detectionInterval);
        while (isActiveAndEnabled)
        {
            TryDetectItems();
            yield return wait;
        }
    }

    private void TryDetectItems()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        int idx = FindActiveOrCompletedIndex(qm);
        if (idx < 0) return;

        var entry = questChain[idx];
        if (!entry.autoDetectItemDelivery || entry.questData == null) return;
        if (qm.GetState(entry.questData.questId) != QuestState.Active) return;

        int count = Physics.OverlapSphereNonAlloc(_t.position, detectionRadius, _overlapBuffer, detectionLayer, QueryTriggerInteraction.Collide);
        if (count <= 0) return;

        Vector3 fwd = _t.forward;
        float half = detectionAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuffer[i];
            if (!col) continue;

            var go = col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;
            if (_consumed.Contains(go)) continue;

            // Tag check (rápido)
            if (!string.IsNullOrEmpty(entry.itemTag) && !go.CompareTag(entry.itemTag)) continue;

            // ángulo
            Vector3 dir = go.transform.position - _t.position;
            if (dir.sqrMagnitude > detectionRadius * detectionRadius) continue;
            float ang = Vector3.Angle(fwd, dir);
            if (ang > half) continue;

            // Si lo lleva el player, no lo detectes aún
            if (IsHeldByPlayer(go)) continue;

            OnItemDetected(go, entry, qm);
            // no lo seguimos procesando este frame
        }
    }

    private bool IsHeldByPlayer(GameObject item)
    {
        if (_player == null) return false;
        Transform p = item.transform.parent;
        while (p != null)
        {
            if (p == _player) return true;
            p = p.parent;
        }
        return false;
    }
    
    private void OnItemDetected(GameObject item, QuestChainEntry entry, QuestManager qm)
    {
        _consumed.Add(item);
        Destroy(item); // o devuelve al pool si usas pooling

        var qid = entry.questData.questId;
        int stepsCount = GetStepsCount(qm, qid);

        if (stepsCount == 0)
        {
            // Sin pasos: completar directamente
            FinishQuestAndChain(entry, qm, qid, FindActiveOrCompletedIndex(qm));
            return;
        }

        int step = Mathf.Clamp(entry.itemDeliveryStepIndex, 0, stepsCount - 1);
        qm.MarkStepDone(qid, step);

        if (qm.AreAllStepsCompleted(qid))
        {
            FinishQuestAndChain(entry, qm, qid, FindActiveOrCompletedIndex(qm));
        }
    }

    private int GetStepsCount(QuestManager qm, string questId)
    {
        foreach (var rq in qm.GetAll())
        {
            if (rq.Id == questId)
                return rq.Steps != null ? rq.Steps.Length : 0;
        }
        return 0;
    }

    // ==== Gizmos
    void OnDrawGizmosSelected()
    {
        if (!showDetectionGizmos) return;

        Gizmos.color = new Color(1f, 0.8f, 0f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        var forward = transform.forward;
        float half = detectionAngle * 0.5f;
        Vector3 left = Quaternion.Euler(0, -half, 0) * forward * detectionRadius;
        Vector3 right = Quaternion.Euler(0, half, 0) * forward * detectionRadius;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + left);
        Gizmos.DrawLine(transform.position, transform.position + right);
    }
}
