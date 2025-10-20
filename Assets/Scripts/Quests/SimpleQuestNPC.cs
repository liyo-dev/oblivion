using System.Collections;
using System.Collections.Generic;
using Alex.NPC.Common;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Interactable))]
[System.Obsolete("Usa NPCBehaviourManager para gestionar NPCs con misiones.")]
public class SimpleQuestNPC : MonoBehaviour
{
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

    readonly Collider[] _overlapBuffer = new Collider[16];
    readonly HashSet<GameObject> _consumed = new();

    Interactable _interactable;
    Transform _cachedTransform;
    Transform _player;
    Coroutine _scanRoutine;

    QuestManager Manager => QuestManager.Instance;
    bool HasChain => questChain != null && questChain.Count > 0;

    void Awake()
    {
        _cachedTransform = transform;
        _interactable = GetComponent<Interactable>();
        if (_interactable == null)
            Debug.LogError($"[{nameof(SimpleQuestNPC)}] Falta Interactable en {name}");
    }

    void Start()
    {
        _player = PlayerLocator.ResolvePlayer();
        if (_player)
            PlayerService.RegisterComponent(_player, false);
        StartItemScan();
    }

    void OnEnable()
    {
        if (_scanRoutine == null && isActiveAndEnabled)
            StartItemScan();
    }

    void OnDisable()
    {
        if (_scanRoutine != null)
        {
            StopCoroutine(_scanRoutine);
            _scanRoutine = null;
        }
    }

    public void Interact()
    {
        var qm = Manager;
        if (qm == null || !HasChain)
            return;

        if (TryGetCurrentEntry(qm, out var entry, out var index))
        {
            var questId = entry.questData?.questId;
            if (string.IsNullOrEmpty(questId))
                return;

            switch (qm.GetState(questId))
            {
                case QuestState.Inactive:
                    PlayDialogue(entry.dlgBefore);
                    break;

                case QuestState.Active:
                    HandleActive(entry, qm, questId, index);
                    break;

                case QuestState.Completed:
                    PlayDialogue(entry.dlgCompleted, () => StartCoroutine(StartNextQuestAfterDialogue(qm, index)));
                    break;
            }
        }
        else
        {
            PlayDialogue(questChain[0].dlgBefore);
        }
    }

    void HandleActive(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
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

            default:
                if (qm.AreAllStepsCompleted(questId))
                    FinishQuestAndChain(entry, qm, questId, currentIndex);
                else
                    PlayDialogue(entry.dlgInProgress);
                break;
        }
    }

    void CompleteAllStepsAndQuest(QuestChainEntry entry, QuestManager qm, string questId, int index)
    {
        foreach (var rq in qm.GetAll())
        {
            if (rq.Id != questId)
                continue;

            var steps = rq.Steps;
            if (steps != null)
            {
                for (int i = 0; i < steps.Length; i++)
                    if (!steps[i].completed)
                        qm.MarkStepDone(questId, i);
            }
            break;
        }

        FinishQuestAndChain(entry, qm, questId, index);
    }

    void FinishQuestAndChain(QuestChainEntry entry, QuestManager qm, string questId, int index)
    {
        qm.CompleteQuest(questId);
        PersistQuestCompletedFlag(questId);
        entry.onQuestCompleted?.Invoke();

        PlayDialogue(entry.dlgTurnIn, () =>
        {
            StartCoroutine(StartNextQuestAfterDialogue(qm, index));
        });
    }

    IEnumerator StartNextQuestAfterDialogue(QuestManager qm, int currentIndex)
    {
        yield return null; // esperar un frame para evitar carreras con el cierre del diálogo

        int next = currentIndex + 1;
        while (next < questChain.Count)
        {
            var nextEntry = questChain[next];
            var nextId = nextEntry.questData ? nextEntry.questData.questId : null;
            if (string.IsNullOrEmpty(nextId)) { next++; continue; }

            var state = qm.GetState(nextId);
            if (state == QuestState.Completed) { next++; continue; }

            if (state == QuestState.Inactive)
            {
                qm.AddQuest(nextEntry.questData);
                qm.StartQuest(nextId);
                if (nextEntry.dlgBefore) PlayDialogue(nextEntry.dlgBefore);
            }
            else
            {
                if (nextEntry.dlgInProgress) PlayDialogue(nextEntry.dlgInProgress);
            }
            yield break;
        }

        Debug.Log($"[{nameof(SimpleQuestNPC)}] Fin de la cadena en {name}.");
    }

    void PlayDialogue(DialogueAsset dlg, System.Action onComplete = null)
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
                dm.StartDialogue(dlg, _cachedTransform, () =>
                {
                    _interactable.OnFinished?.Invoke();
                    onComplete?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning($"[{nameof(SimpleQuestNPC)}] DialogueManager no disponible, saltando diálogo.");
                _interactable.OnFinished?.Invoke();
                onComplete?.Invoke();
            }
        }
        else
        {
            DialogueManager.Instance?.StartDialogue(dlg, _cachedTransform, onComplete);
        }
    }

    void PersistQuestCompletedFlag(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return;

        var profile = GameBootService.Profile;
        var saveSystem = FindFirstObjectByType<SaveSystem>();
        if (profile == null || saveSystem == null)
            return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null)
            return;

        preset.flags ??= new List<string>();
        string flag = $"QUEST_COMPLETED:{questId}";
        if (!preset.flags.Contains(flag))
            preset.flags.Add(flag);

        profile.SaveCurrentGameState(saveSystem, SaveRequestContext.Auto);
    }

    bool TryGetCurrentEntry(QuestManager qm, out QuestChainEntry entry, out int index)
    {
        index = FindActiveOrCompletedIndex(qm);
        if (index >= 0)
        {
            entry = questChain[index];
            return true;
        }

        entry = null;
        return false;
    }

    int FindActiveOrCompletedIndex(QuestManager qm)
    {
        for (int i = questChain.Count - 1; i >= 0; i--)
        {
            var entry = questChain[i];
            if (!entry.questData) continue;

            var state = qm.GetState(entry.questData.questId);
            if (state == QuestState.Active || state == QuestState.Completed)
                return i;
        }

        return -1;
    }

    // ==== Detección de ítems ====
    void StartItemScan()
    {
        if (_scanRoutine != null)
            StopCoroutine(_scanRoutine);

        _scanRoutine = StartCoroutine(ScanRoutine());
    }

    IEnumerator ScanRoutine()
    {
        var wait = new WaitForSeconds(detectionInterval);
        while (isActiveAndEnabled)
        {
            TryDetectItems();
            yield return wait;
        }
    }

    void TryDetectItems()
    {
        var qm = Manager;
        if (qm == null)
            return;

        if (!TryGetCurrentEntry(qm, out var entry, out var index))
            return;

        if (!entry.autoDetectItemDelivery || entry.questData == null)
            return;

        if (qm.GetState(entry.questData.questId) != QuestState.Active)
            return;

        int hits = Physics.OverlapSphereNonAlloc(_cachedTransform.position, detectionRadius, _overlapBuffer, detectionLayer, QueryTriggerInteraction.Collide);
        if (hits <= 0)
            return;

        Vector3 origin = _cachedTransform.position;
        Vector3 forward = _cachedTransform.forward;
        float halfAngle = detectionAngle * 0.5f;
        float radiusSqr = detectionRadius * detectionRadius;

        for (int i = 0; i < hits; i++)
        {
            var collider = _overlapBuffer[i];
            if (!collider) continue;

            var go = collider.attachedRigidbody ? collider.attachedRigidbody.gameObject : collider.gameObject;
            if (!go || _consumed.Contains(go))
                continue;

            if (!string.IsNullOrEmpty(entry.itemTag) && !go.CompareTag(entry.itemTag))
                continue;

            Vector3 dir = go.transform.position - origin;
            if (dir.sqrMagnitude > radiusSqr)
                continue;

            if (Vector3.Angle(forward, dir) > halfAngle)
                continue;

            if (IsHeldByPlayer(go))
                continue;

            OnItemDetected(go, entry, qm, index);
        }
    }

    bool IsHeldByPlayer(GameObject item)
    {
        if (_player == null)
            return false;

        Transform parent = item.transform.parent;
        while (parent != null)
        {
            if (parent == _player)
                return true;
            parent = parent.parent;
        }
        return false;
    }

    void OnItemDetected(GameObject item, QuestChainEntry entry, QuestManager qm, int currentIndex)
    {
        _consumed.Add(item);
        Destroy(item);

        string questId = entry.questData.questId;
        int stepsCount = GetStepsCount(qm, questId);

        if (stepsCount == 0)
        {
            FinishQuestAndChain(entry, qm, questId, currentIndex);
            return;
        }

        int stepIndex = Mathf.Clamp(entry.itemDeliveryStepIndex, 0, stepsCount - 1);
        qm.MarkStepDone(questId, stepIndex);

        if (qm.AreAllStepsCompleted(questId))
            FinishQuestAndChain(entry, qm, questId, currentIndex);
    }

    int GetStepsCount(QuestManager qm, string questId)
    {
        foreach (var request in qm.GetAll())
        {
            if (request.Id == questId)
                return request.Steps?.Length ?? 0;
        }
        return 0;
    }

    void OnDrawGizmosSelected()
    {
        if (!showDetectionGizmos)
            return;

        Vector3 pos = transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.75f);
        Gizmos.DrawWireSphere(pos, detectionRadius);

        float half = detectionAngle * 0.5f;
        Vector3 forward = transform.forward;
        Vector3 left = Quaternion.Euler(0f, -half, 0f) * forward * detectionRadius;
        Vector3 right = Quaternion.Euler(0f, half, 0f) * forward * detectionRadius;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(pos, pos + left);
        Gizmos.DrawLine(pos, pos + right);
    }
}
