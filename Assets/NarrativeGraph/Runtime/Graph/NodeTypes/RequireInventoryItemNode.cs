using System;
using UnityEngine;

/// <summary>
/// Verifica que el jugador tenga un item en inventario, opcionalmente lo consume y actualiza una quest/step.
/// Puede derivar el flujo a otra salida cuando falta el item.
/// </summary>
[Serializable]
public sealed class RequireInventoryItemNode : NarrativeNode
{
    [Header("Item requerido")]
    public ItemData item;
    [Min(1)] public int requiredAmount = 1;
    public bool consumeOnSuccess = true;
    public bool logWarnings = true;

    [Header("Quest")]
    [Tooltip("Quest a actualizar cuando el jugador entrega el item.")]
    public string questId;
    [Tooltip("Step a marcar como completado (opcional).")]
    public int questStepIndex = -1;
    [Tooltip("Iniciar la quest si está inactiva antes de marcar el paso.")]
    public bool autoStartQuestIfInactive = true;
    [Tooltip("Si se activa, en lugar de marcar un paso se completará toda la quest.")]
    public bool completeQuestInstead = false;

    [Header("Flujo si falta el item")]
    [Tooltip("Cuando es true y falta el item, se salta a la salida alternateOutputIndex (p.ej. para diálogos distintos).")]
    public bool useAlternateOutputOnMissing = true;
    [Tooltip("Índice de la salida a usar cuando falta el item (por defecto 1).")]
    public int alternateOutputIndex = 1;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (item == null || requiredAmount <= 0)
        {
            if (logWarnings)
                Debug.LogWarning("[RequireInventoryItemNode] Item o cantidad inválida.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (!PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true) || inventory == null)
        {
            if (logWarnings)
                Debug.LogWarning("[RequireInventoryItemNode] Inventory no encontrado en el Player.");
            HandleMissing(ctx, onReadyToAdvance);
            return;
        }

        if (inventory.HasItem(item, requiredAmount))
        {
            if (consumeOnSuccess)
            {
                if (!inventory.TryConsume(item, requiredAmount))
                {
                    if (logWarnings)
                        Debug.LogWarning("[RequireInventoryItemNode] No se pudo consumir el item pese a que el conteo era suficiente.");
                }
            }

            ApplyQuestProgress();
            onReadyToAdvance?.Invoke();
        }
        else
        {
            HandleMissing(ctx, onReadyToAdvance);
        }
    }

    void HandleMissing(NarrativeContext ctx, Action readyCallback)
    {
        if (useAlternateOutputOnMissing && ctx != null && ctx.Runner != null && outputs != null)
        {
            if (alternateOutputIndex >= 0 && alternateOutputIndex < outputs.Count)
            {
                ctx.Runner.ForceJumpToOutput(this, alternateOutputIndex);
                return;
            }
        }

        readyCallback?.Invoke();
    }

    void ApplyQuestProgress()
    {
        if (string.IsNullOrEmpty(questId))
            return;

        var qm = QuestManager.Instance;
        if (qm == null)
        {
            if (logWarnings)
                Debug.LogWarning("[RequireInventoryItemNode] QuestManager.Instance es null. No se actualiza progreso de misión.");
            return;
        }

        if (completeQuestInstead)
        {
            qm.CompleteQuest(questId);
            return;
        }

        if (questStepIndex >= 0)
        {
            if (autoStartQuestIfInactive && qm.GetState(questId) == QuestState.Inactive)
                qm.StartQuest(questId);

            qm.MarkStepDone(questId, questStepIndex);
            return;
        }

        if (autoStartQuestIfInactive)
        {
            qm.StartQuest(questId);
        }
    }
}
