using System;
using UnityEngine;

/// <summary>
/// Requisito de misión serializable, embebible en cualquier MonoBehaviour o ScriptableObject.
/// Evalúa el estado de UNA quest concreta sin necesitar setup adicional.
///
/// Uso básico:
///   [SerializeField] private QuestRequirement questGate;
///   if (!questGate.IsSatisfied()) return;
///
/// Para comportamiento reactivo (reevaluar cuando cambia el estado de las quests),
/// el MonoBehaviour que lo contiene debe suscribirse a QuestManager.OnQuestsChanged.
/// </summary>
[Serializable]
public sealed class QuestRequirement
{
    public enum Mode
    {
        None,                    // Sin requisito — siempre satisfecho
        QuestActive,             // La misión debe estar activa (iniciada, no completada)
        QuestStartedOrCompleted, // La misión debe haberse iniciado (activa o completada)
        QuestCompleted,          // La misión debe estar completada
        QuestNotStarted          // La misión NO debe haberse iniciado todavía
    }

    [Tooltip("Tipo de requisito. 'None' desactiva la comprobación.")]
    public Mode mode = Mode.None;

    [Tooltip("Asset de la misión. Si se asigna, tiene prioridad sobre questId.")]
    public QuestData quest;

    [Tooltip("ID de la misión como texto (solo si no puedes arrastrar el asset).")]
    public string questId;

    /// <summary>
    /// Evalúa si el requisito se cumple en el estado actual del juego.
    /// - Mode.None → true siempre.
    /// - Sin ID configurado → true (sin restricción).
    /// - QuestManager no disponible → false (bloquea hasta que esté listo).
    /// </summary>
    public bool IsSatisfied()
    {
        if (mode == Mode.None) return true;

        string id = quest != null ? quest.questId : questId;
        if (string.IsNullOrEmpty(id)) return true;

        var qm = QuestManager.Instance;
        if (qm == null) return false;

        var state = qm.GetState(id);
        return mode switch
        {
            Mode.QuestActive             => state == QuestState.Active,
            Mode.QuestStartedOrCompleted => state == QuestState.Active || state == QuestState.Completed,
            Mode.QuestCompleted          => state == QuestState.Completed,
            Mode.QuestNotStarted         => state == QuestState.Inactive,
            _                            => true
        };
    }

    /// <summary>Devuelve el ID resuelto de la quest (SO tiene prioridad sobre string).</summary>
    public string ResolvedId => quest != null ? quest.questId : questId;

    /// <summary>Indica si hay algún requisito configurado (mode != None y hay ID).</summary>
    public bool IsConfigured => mode != Mode.None && !string.IsNullOrEmpty(ResolvedId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string DebugDescription()
    {
        if (!IsConfigured) return "Sin requisito";
        string id = ResolvedId;
        return mode switch
        {
            Mode.QuestActive             => $"Quest '{id}' activa",
            Mode.QuestStartedOrCompleted => $"Quest '{id}' iniciada/completada",
            Mode.QuestCompleted          => $"Quest '{id}' completada",
            Mode.QuestNotStarted         => $"Quest '{id}' no iniciada",
            _                            => "Sin requisito"
        };
    }
#endif
}
