using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca uno o varios pasos de una quest como completados y, opcionalmente, cierra la quest.
/// Útil cuando un NPC otorga una recompensa que también debe avanzar la misión.
/// Ahora soporta completar steps por Condition ID (recomendado) o por índice.
/// </summary>
[Serializable]
public sealed class CompleteQuestStepsNode : NarrativeNode
{
    [Header("Quest")]
    [Tooltip("ID de la quest que se actualizará.")]
    public string questId;

    [Header("Completar Steps por Condition ID (Recomendado)")]
    [Tooltip("Lista de Step Condition IDs que se marcarán como completados. Ej: 'QUEST_ELDRAN_MISSION5_STEP_00'")]
    public List<string> stepConditionIds = new();

    [Header("Completar Steps por Índice (Legacy)")]
    [Tooltip("Lista de índices de pasos que se marcarán como completados. Solo se usa si stepConditionIds está vacío.")]
    public List<int> steps = new();

    [Header("Opciones")]
    [Tooltip("Cuando está activo, completará toda la quest tras procesar los pasos.")]
    public bool completeQuest;

    [Tooltip("Muestra advertencias cuando los datos son inválidos.")]
    public bool logWarnings = true;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            if (logWarnings)
                Debug.LogWarning("[CompleteQuestStepsNode] questId vacío.");
            ready?.Invoke();
            return;
        }

        bool anyAction = false;
        bool warnedNoSteps = false;

        // Prioridad 1: Completar por Condition ID (arquitectura correcta)
        if (stepConditionIds != null && stepConditionIds.Count > 0)
        {
            var processed = new HashSet<string>();
            foreach (var conditionId in stepConditionIds)
            {
                if (string.IsNullOrWhiteSpace(conditionId))
                {
                    if (logWarnings)
                        Debug.LogWarning($"[CompleteQuestStepsNode] Step Condition ID vacío para quest '{questId}'.");
                    continue;
                }

                if (!processed.Add(conditionId))
                    continue; // evitar repetir llamadas para el mismo ID

                // Usar el Singleton de QuestManager
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.CompleteQuestStepByConditionId(questId, conditionId);
                    anyAction = true;
                    Debug.Log($"[CompleteQuestStepsNode] ✅ Completando step con conditionId '{conditionId}' en quest '{questId}'");
                }
                else
                {
                    if (logWarnings)
                        Debug.LogWarning($"[CompleteQuestStepsNode] QuestManager.Instance es null - No se puede completar step '{conditionId}'");
                }
            }
        }
        // Prioridad 2: Completar por índice (fallback legacy)
        else if (steps != null && steps.Count > 0)
        {
            var processed = new HashSet<int>();
            foreach (var stepIndex in steps)
            {
                if (stepIndex < 0)
                {
                    if (logWarnings)
                        Debug.LogWarning($"[CompleteQuestStepsNode] Índice de paso inválido ({stepIndex}) para quest '{questId}'.");
                    continue;
                }
                if (!processed.Add(stepIndex))
                    continue; // evitar repetir llamadas para el mismo índice

                ctx.Signals?.CompleteQuestStep(questId, stepIndex);
                anyAction = true;
                Debug.Log($"[CompleteQuestStepsNode] ✅ Completando step índice {stepIndex} en quest '{questId}'");
            }
        }
        else if (!completeQuest && logWarnings)
        {
            Debug.LogWarning($"[CompleteQuestStepsNode] No hay pasos configurados (ni por conditionId ni por índice) para quest '{questId}' y 'completeQuest' está desactivado.");
            warnedNoSteps = true;
        }

        if (completeQuest)
        {
            ctx.Signals?.CompleteQuest(questId);
            anyAction = true;
            Debug.Log($"[CompleteQuestStepsNode] ✅ Completando quest '{questId}'");
        }
        else if (!anyAction && logWarnings && !warnedNoSteps)
        {
            Debug.LogWarning($"[CompleteQuestStepsNode] Nada que hacer para quest '{questId}'.");
        }

        ready?.Invoke();
    }
}
