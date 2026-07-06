using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RequirementMode
{
    AnyQuestStartedOrCompleted,
    AnyQuestStarted,
    SpecificQuestsStarted,
    SpecificQuestsCompleted
}

[RequireComponent(typeof(Collider))]
public class RoomExitBlocker : MonoBehaviour
{
    [Header("Requisito")]
    [SerializeField] private RequirementMode requirementMode = RequirementMode.AnyQuestStartedOrCompleted;

    [Tooltip("IDs de misiones requeridas (opcional si usas QuestDataRefs). Para SpecificQuests* TODAS deben cumplirse.")]
    [SerializeField] private List<string> requiredQuestIds = new();

    [Tooltip("Referencias a QuestData requeridas (opcional si usas Ids). Para SpecificQuests* TODAS deben cumplirse.")]
    [SerializeField] private List<QuestData> requiredQuestRefs = new();

    [Header("Mensajes (localización)")]
    [SerializeField] private string blockedMessageKey = "ROOM_EXIT_BLOCKED";

    [Header("Diálogo")]
    [SerializeField] private string messageSpeaker = "Pensamiento";

    [Header("Cooldown / Debug")]
    [SerializeField] private float messageCooldown = 1.5f;
    [SerializeField] private bool debugLogs;

    private float _lastMessageTime;
    private Collider _col;
    private bool _isBlocked = true;
    private bool _subscribed;
    private bool _isShowingMessage;
    private Coroutine _waitCoroutine;

    void Awake()
    {
        _col = GetComponent<Collider>();
    }

    void OnEnable()
    {
        var qm = QuestManager.Instance;
        if (qm != null)
        {
            qm.OnQuestsChanged += OnQuestsChanged;
            _subscribed = true;
            EvaluateAndApplyState();
        }
        else
        {
            _waitCoroutine = StartCoroutine(WaitAndSubscribe());
        }
    }

    void OnDisable()
    {
        if (_waitCoroutine != null)
        {
            StopCoroutine(_waitCoroutine);
            _waitCoroutine = null;
        }

        if (_subscribed)
        {
            var qm = QuestManager.Instance;
            if (qm != null)
                qm.OnQuestsChanged -= OnQuestsChanged;
            _subscribed = false;
        }
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (QuestManager.Instance == null)
            yield return null;

        _waitCoroutine = null;
        var qm = QuestManager.Instance;
        qm.OnQuestsChanged += OnQuestsChanged;
        _subscribed = true;
        EvaluateAndApplyState();
    }

    private void OnQuestsChanged() => EvaluateAndApplyState();

    public void ForceReevaluate() => EvaluateAndApplyState();

    private void EvaluateAndApplyState()
    {
        _isBlocked = !RequirementSatisfied();
        ApplyColliderState();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugLogs)
            Debug.Log($"[RoomExitBlocker:{gameObject.name}] → {(_isBlocked ? "BLOQUEADO" : "DESBLOQUEADO")}");
#endif
    }

    private void ApplyColliderState()
    {
        if (!_col) _col = GetComponent<Collider>();
        if (!_col) return;

        bool shouldBeTrigger = !_isBlocked;
        if (_col.isTrigger != shouldBeTrigger)
            _col.isTrigger = shouldBeTrigger;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_isBlocked) TryShowMessage();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (_isBlocked) TryShowMessage();
    }

    private void TryShowMessage()
    {
        if (_isShowingMessage) return;
        if (Time.time - _lastMessageTime < Mathf.Max(0.1f, messageCooldown)) return;

        _lastMessageTime = Time.time;

        string msg = TryGetLocalized(blockedMessageKey) ?? "Debería revisar mi habitación antes de salir…";
        if (string.IsNullOrEmpty(msg)) return;
        if (DialogueManager.Instance == null) return;

        _isShowingMessage = true;

        var temp = ScriptableObject.CreateInstance<DialogueAsset>();
        temp.lines = new[]
        {
            new DialogueLine
            {
                speakerNameId = messageSpeaker,
                textId = null,
                text = msg,
                portrait = null
            }
        };

        try
        {
            DialogueManager.Instance.StartDialogue(temp, transform, () => _isShowingMessage = false);
        }
        catch
        {
            DialogueManager.Instance.StartDialogue(temp, () => _isShowingMessage = false);
        }
    }

    private bool RequirementSatisfied()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return false;

        switch (requirementMode)
        {
            case RequirementMode.AnyQuestStartedOrCompleted:
                foreach (var rq in qm.GetAll())
                    if (rq.State == QuestState.Active || rq.State == QuestState.Completed)
                        return true;
                return false;

            case RequirementMode.AnyQuestStarted:
                foreach (var rq in qm.GetAll())
                    if (rq.State == QuestState.Active)
                        return true;
                return false;

            case RequirementMode.SpecificQuestsStarted:
            {
                int configuredCount = 0;
                int satisfiedCount = 0;

                for (int i = 0; i < requiredQuestIds.Count; i++)
                {
                    var id = requiredQuestIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    configuredCount++;
                    var state = qm.GetState(id);
                    if (state == QuestState.Active || state == QuestState.Completed) satisfiedCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugLogs) Debug.Log($"[RoomExitBlocker:{gameObject.name}] ID '{id}' → {state}");
#endif
                }

                for (int i = 0; i < requiredQuestRefs.Count; i++)
                {
                    var r = requiredQuestRefs[i];
                    if (r == null || string.IsNullOrEmpty(r.questId)) continue;
                    configuredCount++;
                    var state = qm.GetState(r.questId);
                    if (state == QuestState.Active || state == QuestState.Completed) satisfiedCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (debugLogs) Debug.Log($"[RoomExitBlocker:{gameObject.name}] Ref '{r.questId}' → {state}");
#endif
                }

                if (configuredCount == 0) return false;
                return satisfiedCount >= configuredCount;
            }

            case RequirementMode.SpecificQuestsCompleted:
            {
                int configuredCount = 0;
                int satisfiedCount = 0;

                for (int i = 0; i < requiredQuestIds.Count; i++)
                {
                    var id = requiredQuestIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    configuredCount++;
                    if (qm.GetState(id) == QuestState.Completed) satisfiedCount++;
                }

                for (int i = 0; i < requiredQuestRefs.Count; i++)
                {
                    var r = requiredQuestRefs[i];
                    if (r == null || string.IsNullOrEmpty(r.questId)) continue;
                    configuredCount++;
                    if (qm.GetState(r.questId) == QuestState.Completed) satisfiedCount++;
                }

                if (configuredCount == 0) return false;
                return satisfiedCount >= configuredCount;
            }
        }

        return false;
    }

    private string TryGetLocalized(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (LocalizationManager.Instance == null) return null;
        var txt = LocalizationManager.Instance.Get(key, "");
        return string.IsNullOrEmpty(txt) ? null : txt;
    }
}
