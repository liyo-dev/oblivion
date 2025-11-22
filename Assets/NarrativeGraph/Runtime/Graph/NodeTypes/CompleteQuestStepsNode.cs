using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca uno o varios pasos de una quest como completados y, opcionalmente, cierra la quest.
/// Útil cuando un NPC otorga una recompensa que también debe avanzar la misión.
/// </summary>
[Serializable]
public sealed class CompleteQuestStepsNode : NarrativeNode
{
    [Tooltip("ID de la quest que se actualizará.")]
    public string questId;

    [Tooltip("Lista de índices de pasos que se marcarán como completados al entrar en el nodo.")]
    public List<int> steps = new();

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
        if (steps != null && steps.Count > 0)
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
            }
        }
        else if (!completeQuest && logWarnings)
        {
            Debug.LogWarning($"[CompleteQuestStepsNode] No hay pasos configurados para quest '{questId}' y 'completeQuest' está desactivado.");
            warnedNoSteps = true;
        }

        if (completeQuest)
        {
            ctx.Signals?.CompleteQuest(questId);
            anyAction = true;
        }
        else if (!anyAction && logWarnings && !warnedNoSteps)
        {
            Debug.LogWarning($"[CompleteQuestStepsNode] Nada que hacer para quest '{questId}'.");
        }

        ready?.Invoke();
    }
}
