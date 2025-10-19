using UnityEngine;

public static class InventoryUseUtility
{
    public static bool TryUseItem(Inventory inventory, ItemData item, PlayerPickupCollector collector, out string failureReason, out bool consumed)
    {
        failureReason = string.Empty;
        consumed = false;

        if (inventory == null)
        {
            failureReason = "Inventario no disponible.";
            return false;
        }

        if (item == null)
        {
            failureReason = "Item no válido.";
            return false;
        }

        if (!item.usableFromInventory)
        {
            failureReason = "Este item no se puede usar desde el inventario.";
            return false;
        }

        if (collector == null)
        {
            failureReason = "No se encontró PlayerPickupCollector.";
            return false;
        }

        if (!inventory.HasItem(item))
        {
            failureReason = "No tienes este item.";
            return false;
        }

        if (item.useEffects == null || item.useEffects.Count == 0)
        {
            failureReason = "El item no tiene efectos configurados.";
            return false;
        }

        bool anyChange = false;
        bool shouldConsume = false;

        for (int i = 0; i < item.useEffects.Count; i++)
        {
            var effect = item.useEffects[i];
            bool consumeEffect;
            bool changed = collector.TryCollect(effect, out consumeEffect);
            if (consumeEffect) shouldConsume = true;
            if (changed) anyChange = true;
        }

        if (shouldConsume)
        {
            inventory.TryConsume(item);
            consumed = true;
        }

        if (!anyChange)
        {
            failureReason = "No tuvo efecto.";
        }

        return anyChange;
    }
}
