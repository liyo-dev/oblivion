using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bloquea la salida hasta que se cumpla un requisito de misiones.
/// Puede exigir "alguna misión" o misiones concretas (iniciadas o completadas).
/// Muestra un diálogo con texto ya resuelto (sin depender de textId).
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoomExitBlocker : MonoBehaviour
{
    // RequirementMode moved to Core/Identifiers.cs (centralized)

    [Header("Requisito")]
    [SerializeField] private RequirementMode requirementMode = RequirementMode.AnyQuestStartedOrCompleted;

    [Tooltip("IDs de misiones requeridas (opcional si usas QuestDataRefs). Para SpecificQuests* TODAS deben cumplirse.")]
    [SerializeField] private List<string> requiredQuestIds = new();

    [Tooltip("Referencias a QuestData requeridas (opcional si usas Ids). Para SpecificQuests* TODAS deben cumplirse.")]
    [SerializeField] private List<QuestData> requiredQuestRefs = new();

    [Header("Mensajes (localización)")]
    [Tooltip("Clave genérica cuando el requisito es 'alguna misión' (AnyQuestStarted/AnyQuestStartedOrCompleted). No se usa si especificas quests concretas.")]
    [SerializeField] private string blockedMessageKey = "ROOM_EXIT_BLOCKED";

    [Tooltip("Prefijo del mensaje (ej: 'Antes de salir de la habitación necesitas:', 'Antes de entrar al agua necesitas:'). Si está vacío, usa 'Antes de salir necesitas:'")]
    [SerializeField] private string messagePrefix = "Antes de salir necesitas:";

    [Tooltip("Clave de localización que se traducirá (ej: 'WATER_ENTER_NEEDS'). Si está vacío, usa el nombre/ID de las quests requeridas. Si está relleno, ignora los nombres de las quests y usa esta traducción.")]
    [SerializeField] private string blockedMessageFormatKey = "";

    [Tooltip("Separador para listar nombres de quests requeridas en el mensaje.")]
    [SerializeField] private string listSeparator = ", ";

    [Header("Diálogo")]
    [Tooltip("ID/nombre del emisor del mensaje.")]
    [SerializeField] private string messageSpeaker = "Pensamiento";

    [Header("Bloqueo Físico")]
    [Tooltip("Bloqueado = collider sólido; desbloqueado = collider trigger.")]
    [SerializeField] private bool useSolidBlocker = true;

    [Header("Cooldown / Debug")]
    [SerializeField] private float messageCooldown = 1.5f;
    [SerializeField] private bool debugLogs;

    // ---- estado interno ----
    private Collider _col;
    private bool _isBlocked = true;
    private float _lastMessageTime;
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
    }

    void OnDisable()
    {
        TryUnsubscribe();
    }

    void Start()
    {
        EvaluateAndApplyState();
    }

    private System.Collections.IEnumerator WaitForQuestManagerAndSubscribe()
    {
        // Esperar hasta que QuestManager esté disponible (para soportar carga aditiva de Start)
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
        if (_isBlocked != shouldBlock)
        {
            _isBlocked = shouldBlock;
            ApplyColliderState();
            if (debugLogs) Debug.Log($"[RoomExitBlocker] Estado → {(_isBlocked ? "BLOQUEADO" : "DESBLOQUEADO")}");
        }
        else
        {
            ApplyColliderState();
        }
    }

    private void ApplyColliderState()
    {
        if (!_col) _col = GetComponent<Collider>();
        if (!_col) return;
        _col.isTrigger = useSolidBlocker ? !_isBlocked : true;
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

        // Texto final ya resuelto (incluye lista de misiones si procede)
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

    // ===================== LÓGICA DE REQUISITOS =====================

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

    /// <summary>
    /// Construye el texto final que se mostrará en el diálogo (con localización + lista de quests si toca).
    /// </summary>
    private string BuildBlockedMessage()
    {
        // Caso “alguna misión”
        if (requirementMode == RequirementMode.AnyQuestStarted ||
            requirementMode == RequirementMode.AnyQuestStartedOrCompleted)
        {
            return TryGetLocalized(blockedMessageKey) ?? "Debería revisar mi habitación antes de salir…";
        }

        // Caso “misiones concretas”
        var ids = GetRequiredIds();
        if (ids.Count == 0)
        {
            // Sin lista → usa genérico
            return TryGetLocalized(blockedMessageKey) ?? "Debería revisar mi habitación antes de salir…";
        }

        // Resolver nombres “bonitos” de las misiones usando los QuestData refs si están
        var prettyNames = new List<string>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            string display = id;

            var refMatch = requiredQuestRefs.FirstOrDefault(q => q != null && q.questId == id);
            if (refMatch != null)
            {
                var n = refMatch.GetLocalizedName();
                if (!string.IsNullOrEmpty(n)) display = n;
            }

            prettyNames.Add(display);
        }

        // Determinar el prefijo del mensaje (intentar traducir si parece una clave)
        string prefix = messagePrefix;
        if (string.IsNullOrEmpty(prefix))
        {
            prefix = "Antes de salir necesitas:";
        }
        else if (LooksLikeLocalizationKey(prefix))
        {
            // Si el prefijo parece una clave de localización, intentar traducirlo
            var translated = TryGetLocalized(prefix);
            if (!string.IsNullOrEmpty(translated))
                prefix = translated;
        }

        // Si blockedMessageFormatKey está relleno, traducirlo y usarlo
        if (!string.IsNullOrEmpty(blockedMessageFormatKey))
        {
            string translated = TryGetLocalized(blockedMessageFormatKey);
            if (!string.IsNullOrEmpty(translated))
            {
                return $"{prefix} {translated}";
            }
            // Fallback si no hay traducción: usar la clave directamente
            return $"{prefix} {blockedMessageFormatKey}";
        }

        // Si está vacío, usar la lista de nombres de quests
        string joined = string.Join(listSeparator, prettyNames);
        return $"{prefix} {joined}";
    }

    private bool LooksLikeLocalizationKey(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        // Detectar patrones tipo UPPER_CASE_WITH_UNDERSCORES
        return text.Length > 3 && 
               text == text.ToUpperInvariant() && 
               text.Contains('_');
    }

    private string TryGetLocalized(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (LocalizationManager.Instance == null) return null;
        var txt = LocalizationManager.Instance.Get(key, "");
        return string.IsNullOrEmpty(txt) ? null : txt;
    }

    // ===================== Gizmos =====================

    private void OnDrawGizmos()
    {
        bool blocked = Application.isPlaying ? _isBlocked : !RequirementSatisfied();
        Gizmos.color = blocked ? Color.red : Color.green;

        var c = GetComponent<Collider>();
        if (c is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            if (blocked)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                Gizmos.DrawCube(box.center, box.size);
            }
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }

        Gizmos.color = Color.yellow;
        var a = transform.position;
        var b = transform.position - transform.forward * 2f;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(b, 0.08f);
    }

    private void OnValidate()
    {
        if (!_col) _col = GetComponent<Collider>();
        ApplyColliderState();
    }
}
