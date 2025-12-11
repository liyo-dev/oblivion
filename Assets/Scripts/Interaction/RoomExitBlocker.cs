using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Muestra el mensaje bloqueado usando la clave de localización y
/// mantiene la lógica mínima para desbloquear la salida cuando se
/// cumpla la condición de misiones.
/// </summary>
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
    [Tooltip("Clave del mensaje a mostrar cuando se bloquea.")]
    [SerializeField] private string blockedMessageKey = "ROOM_EXIT_BLOCKED";

    [Header("Diálogo")]
    [Tooltip("ID/nombre del emisor del mensaje.")]
    [SerializeField] private string messageSpeaker = "Pensamiento";

    [Header("Cooldown / Debug")]
    [SerializeField] private float messageCooldown = 1.5f;
    [SerializeField] private bool debugLogs;

    private float _lastMessageTime;
    private Collider _col;
    private bool _isBlocked = true;
    private bool _subscribed;

    void Awake()
    {
        _col = GetComponent<Collider>();
        StartCoroutine(WaitForQuestManagerAndSubscribe());
    }

    void OnEnable()
    {
        if (_subscribed)
            EvaluateAndApplyState();
        else
            StartCoroutine(WaitForQuestManagerAndSubscribe());

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        TryUnsubscribe();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void Start()
    {
        EvaluateAndApplyState();
    }

    private System.Collections.IEnumerator WaitForQuestManagerAndSubscribe()
    {
        while (QuestManager.Instance == null)
        {
            yield return null;
        }

        EnsureSubscription();
        EvaluateAndApplyState();
    }

    private void EnsureSubscription()
    {
        if (_subscribed) return;
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsChanged += HandleQuestsChanged;
            _subscribed = true;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EvaluateAndApplyState();
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed) return;
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestsChanged -= HandleQuestsChanged;
        _subscribed = false;
    }

    private void HandleQuestsChanged()
    {
        EvaluateAndApplyState();
    }

    private void EvaluateAndApplyState()
    {
        bool shouldBlock = !RequirementSatisfied();
        _isBlocked = shouldBlock;
        ApplyColliderState();
        if (debugLogs) Debug.Log($"[RoomExitBlocker] Estado → {(_isBlocked ? "BLOQUEADO" : "DESBLOQUEADO")}");
    }

    private void ApplyColliderState()
    {
        if (!_col) _col = GetComponent<Collider>();
        if (!_col) return;
        // Bloqueado = collider sólido; desbloqueado = trigger para dejar pasar
        _col.isTrigger = !_isBlocked;
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
        if (Time.time - _lastMessageTime < Mathf.Max(0.1f, messageCooldown)) return;
        _lastMessageTime = Time.time;

        // Texto final: traducir por clave
        string msg = BuildBlockedMessage();
        if (string.IsNullOrEmpty(msg))
        {
            if (debugLogs) Debug.LogWarning("[RoomExitBlocker] No hay mensaje que mostrar.");
            return;
        }

        if (DialogueManager.Instance == null)
        {
            if (debugLogs) Debug.LogWarning("[RoomExitBlocker] DialogueManager no disponible.");
            return;
        }

        // Diálogo temporal: usamos el campo 'text' directo
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

        // Llama al overload con Transform si existe; si no, al simple
        try
        {
            DialogueManager.Instance.StartDialogue(temp, transform, () =>
            {
                if (debugLogs) Debug.Log("[RoomExitBlocker] Mensaje cerrado (con transform).");
            });
        }
        catch
        {
            DialogueManager.Instance.StartDialogue(temp, () =>
            {
                if (debugLogs) Debug.Log("[RoomExitBlocker] Mensaje cerrado (sin transform).");
            });
        }
    }

    /// <summary>
    /// Construye el texto final usando solo la clave de localización.
    /// </summary>
    private string BuildBlockedMessage()
    {
        return TryGetLocalized(blockedMessageKey) ?? "Debería revisar mi habitación antes de salir…";
    }

    private string TryGetLocalized(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (LocalizationManager.Instance == null) return null;
        var txt = LocalizationManager.Instance.Get(key, "");
        return string.IsNullOrEmpty(txt) ? null : txt;
    }

    // ===================== LÓGICA DE REQUISITOS (mínima) =====================

    private bool RequirementSatisfied()
    {
        var qm = QuestManager.Instance;
        if (qm == null)
        {
            if (debugLogs) Debug.LogWarning("[RoomExitBlocker] QuestManager no disponible. Bloqueando por defecto.");
            return false;
        }

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
                var ids = GetRequiredIds();
                if (ids.Count == 0) return false;
                for (int i = 0; i < ids.Count; i++)
                    if (qm.GetState(ids[i]) != QuestState.Active)
                        return false;
                return true;
            }

            case RequirementMode.SpecificQuestsCompleted:
            {
                var ids = GetRequiredIds();
                if (ids.Count == 0) return false;
                for (int i = 0; i < ids.Count; i++)
                    if (qm.GetState(ids[i]) != QuestState.Completed)
                        return false;
                return true;
            }
        }
        return false;
    }

    private List<string> GetRequiredIds()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < requiredQuestIds.Count; i++)
        {
            var id = requiredQuestIds[i];
            if (!string.IsNullOrEmpty(id)) set.Add(id);
        }
        for (int i = 0; i < requiredQuestRefs.Count; i++)
        {
            var r = requiredQuestRefs[i];
            if (r != null && !string.IsNullOrEmpty(r.questId)) set.Add(r.questId);
        }
        return set.ToList();
    }
    private void OnDrawGizmos() { }
}
