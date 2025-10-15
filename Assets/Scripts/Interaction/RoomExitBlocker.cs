using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Bloquea la salida hasta que se cumpla un requisito de misiones.
/// Permite exigir "alguna misión" o una/s misión/es concreta/s (iniciadas o completadas).
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoomExitBlocker : MonoBehaviour
{
    public enum RequirementMode
    {
        AnyQuestStartedOrCompleted, // deja pasar si hay al menos 1 misión activa o completada
        AnyQuestStarted,            // deja pasar si hay al menos 1 misión activa
        SpecificQuestsStarted,      // requiere que TODAS las indicadas estén activas
        SpecificQuestsCompleted     // requiere que TODAS las indicadas estén completadas
    }

    [Header("Requisito")]
    [SerializeField] private RequirementMode requirementMode = RequirementMode.AnyQuestStartedOrCompleted;

    [Tooltip("IDs de misiones requeridas (opcional si usas QuestDataRefs). Para SpecificQuests* TODAS deben cumplirse.")]
    [SerializeField] private List<string> requiredQuestIds = new();

    [Tooltip("Referencias a QuestData requeridas (opcional si usas Ids). Para SpecificQuests* TODAS deben cumplirse.")]
    [SerializeField] private List<QuestData> requiredQuestRefs = new();

    [Header("Mensaje")]
    [Tooltip("Clave de localización del mensaje de bloqueo (si está vacía, se usará defaultBlockedMessage).")]
    [SerializeField] private string blockedMessageKey = "ROOM_EXIT_BLOCKED";

    [Tooltip("Clave de localización con formato, p.ej.: 'Antes de salir, inicia: {0}'. Si está vacía, se intentará componer mensaje.")]
    [SerializeField] private string blockedMessageFormatKey = "ROOM_EXIT_NEEDS";

    [TextArea(3, 5)]
    [SerializeField] private string defaultBlockedMessage = "Debería revisar mi habitación antes de salir…";

    [TextArea(2, 4)]
    [SerializeField] private string defaultNeedsFormat = "Antes de salir, inicia/completa: {0}";

    [Tooltip("Separador para listar nombres de quests requeridas en el mensaje.")]
    [SerializeField] private string listSeparator = ", ";

    [Header("Diálogo")]
    [Tooltip("Nombre/ID del emisor (para el cuadro de diálogo).")]
    [SerializeField] private string messageSpeaker = "Pensamiento";

    [Header("Bloqueo Físico")]
    [Tooltip("Si está activado, el collider se vuelve sólido mientras esté bloqueado.")]
    [SerializeField] private bool useSolidBlocker = true;

    [Header("Cooldown / Debug")]
    [SerializeField] private float messageCooldown = 1.5f;
    [SerializeField] private bool debugLogs;

    // ---- estado ----
    private Collider _col;
    private bool _isBlocked = true;
    private float _lastMessageTime;
    private bool _subscribed;

    void Awake()
    {
        _col = GetComponent<Collider>();
        EnsureSubscription();
        EvaluateAndApplyState();
    }

    void OnEnable()
    {
        EnsureSubscription();
        EvaluateAndApplyState();
    }

    void OnDisable()
    {
        TryUnsubscribe();
    }

    void Start()
    {
        // Re-evaluar cuando todo está ya inicializado
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
            // Asegura estado del collider por si editor cambió cosas
            ApplyColliderState();
        }
    }

    private void ApplyColliderState()
    {
        if (!_col) _col = GetComponent<Collider>();
        if (!_col) return;

        if (useSolidBlocker)
        {
            // Bloqueado = sólido (no trigger). Desbloqueado = trigger (pasa pero detecta para mensaje si hiciera falta)
            _col.isTrigger = !_isBlocked;
        }
        else
        {
            // Modo no-sólido: siempre trigger; solo mostramos mensaje si está bloqueado.
            _col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // En modo trigger, solo mostramos mensaje si está bloqueado
        if (_isBlocked) TryShowMessage();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        // En modo sólido, el choque indica bloqueo
        if (_isBlocked) TryShowMessage();
    }

    private void TryShowMessage()
    {
        if (Time.time - _lastMessageTime < Mathf.Max(0.1f, messageCooldown)) return;
        _lastMessageTime = Time.time;

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

        if (debugLogs) Debug.Log($"[RoomExitBlocker] MOSTRAR: {msg}");
    }

    // ---------- LÓGICA DE REQUISITOS ----------

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
            {
                foreach (var rq in qm.GetAll())
                    if (rq.State == QuestState.Active || rq.State == QuestState.Completed)
                        return true;
                return false;
            }
            case RequirementMode.AnyQuestStarted:
            {
                foreach (var rq in qm.GetAll())
                    if (rq.State == QuestState.Active)
                        return true;
                return false;
            }
            case RequirementMode.SpecificQuestsStarted:
            {
                var ids = GetRequiredIds();
                if (ids.Count == 0) return false; // si no hay lista, bloquea (evita pasar por accidente)
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
            default:
                return false;
        }
    }

    private List<string> GetRequiredIds()
    {
        // Merge IDs + refs (sin duplicados)
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

    private string BuildBlockedMessage()
    {
        // 1) si el requisito es “alguna misión”, usa clave simple o por defecto
        if (requirementMode == RequirementMode.AnyQuestStartedOrCompleted ||
            requirementMode == RequirementMode.AnyQuestStarted)
        {
            string loc = TryGetLocalized(blockedMessageKey);
            return string.IsNullOrEmpty(loc) ? defaultBlockedMessage : loc;
        }

        // 2) si el requisito es “misiones concretas”, compón la lista de nombres
        var ids = GetRequiredIds();
        if (ids.Count == 0)
        {
            // no hay lista → cae al mensaje simple
            string fallback = TryGetLocalized(blockedMessageKey);
            return string.IsNullOrEmpty(fallback) ? defaultBlockedMessage : fallback;
        }

        // Nombres localizados (QuestData si está en catálogo o en refs)
        var names = new List<string>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            string display = id;

            // intenta desde refs
            var refMatch = requiredQuestRefs.FirstOrDefault(q => q != null && q.questId == id);
            if (refMatch != null)
            {
                var n = refMatch.GetLocalizedName();
                if (!string.IsNullOrEmpty(n)) display = n;
            }
            else
            {
                // intenta desde catálogo del QuestManager
                var qm = QuestManager.Instance;
                if (qm != null)
                {
                    // qm no expone catálogo público; alternativa: usa ID tal cual o agrega tu propio lookup si quieres
                    // aquí preferimos dejar el id si no tenemos el SO
                }
            }

            names.Add(display);
        }

        string joined = string.Join(listSeparator, names);

        // 3) intenta formato localizado con {0}
        string locFmt = TryGetLocalized(blockedMessageFormatKey);
        if (!string.IsNullOrEmpty(locFmt) && locFmt.Contains("{0}"))
            return string.Format(locFmt, joined);

        // 4) formato por defecto
        return string.Format(defaultNeedsFormat, joined);
    }

    private string TryGetLocalized(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (LocalizationManager.Instance == null) return null;

        // intenta sin fallback para detectar ausencia real
        var txt = LocalizationManager.Instance.Get(key, "");
        return string.IsNullOrEmpty(txt) ? null : txt;
    }

    // ---------- Gizmos ----------

    private void OnDrawGizmos()
    {
        // Color según bloqueo actual (en editor, si no está en play, evalúa rápido)
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

        // Flecha de “salida”
        Gizmos.color = Color.yellow;
        Vector3 a = transform.position;
        Vector3 b = transform.position - transform.forward * 2f;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(b, 0.08f);
    }

    private void OnValidate()
    {
        // Mantener coherencia en editor
        if (!_col) _col = GetComponent<Collider>();
        ApplyColliderState();
    }
}
