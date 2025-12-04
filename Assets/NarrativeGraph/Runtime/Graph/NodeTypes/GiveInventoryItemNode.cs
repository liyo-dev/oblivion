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
    [Tooltip("Deprecated: el nodo ya es idempotente por defecto usando Blackboard.")]
    public bool onlyOnce = true; // mantenido por compatibilidad visual en el inspector

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

        // Evitar duplicados: si este nodo ya otorgó el ítem, no repetir.
        // Clave estable basada en grafo + guid del nodo (y itemId por claridad).
        string graphName = ctx?.Graph != null ? ctx.Graph.name : "<no-graph>";
        string key = $"INV_GIVEN:{graphName}:{guid}:{item.itemId}";
        if (ctx != null && ctx.Blackboard != null)
        {
            if (ctx.Blackboard.Get<bool>(key, false))
            {
                if (logWarnings)
                    Debug.Log($"[GiveInventoryItemNode] Ítem '{item.itemId}' ya entregado por este nodo. Omitiendo duplicado.");
                onReadyToAdvance?.Invoke();
                return;
            }
        }

        inventory.Add(item, amount);

        if (ctx != null && ctx.Blackboard != null)
        {
            ctx.Blackboard.Set(key, true);
        }
        onReadyToAdvance?.Invoke();
    }
}
