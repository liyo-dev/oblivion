using System;
using UnityEngine;

/// <summary>
/// Añade un item al inventario del jugador desde el grafo narrativo.
/// </summary>
[Serializable]
public sealed class GiveInventoryItemNode : NarrativeNode
{
    [Header("Item")]
    public ItemData item;
    [Min(1)] public int amount = 1;
    [Tooltip("Cuando está activo, loguea advertencias si no se encuentra inventario o item.")]
    public bool logWarnings = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (item == null || amount <= 0)
        {
            if (logWarnings)
                Debug.LogWarning("[GiveInventoryItemNode] Item o cantidad inválida.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (!PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true) || inventory == null)
        {
            if (logWarnings)
                Debug.LogWarning("[GiveInventoryItemNode] Inventory no encontrado en el Player.");
            onReadyToAdvance?.Invoke();
            return;
        }

        inventory.Add(item, amount);
        onReadyToAdvance?.Invoke();
    }
}
