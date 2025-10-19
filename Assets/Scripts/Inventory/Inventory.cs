using UnityEngine;
using System.Collections.Generic;

public class Inventory : MonoBehaviour
{
    private readonly Dictionary<string, int> _bag = new();
    // Keep a mapping of itemId -> ItemData so UI can display icons/names.
    private readonly Dictionary<string, ItemData> _definitions = new();

    // Event fired when inventory changes: (itemData, newAmount)
    public event System.Action<ItemData, int> OnInventoryChanged;

    // Event fired when items are explicitly added: (itemData, addedAmount, newTotal)
    public event System.Action<ItemData, int, int> OnItemAdded;

    public struct Entry
    {
        public ItemData item;
        public int count;
    }

    public void Add(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;
        _bag.TryGetValue(item.itemId, out int cur);
        int next = cur + amount;
        _bag[item.itemId] = next;
        _definitions[item.itemId] = item;
        Debug.Log($"[Inventory:{gameObject.name}] +{amount} {item.displayName} (total {_bag[item.itemId]})");

        // Notify listeners (UI, popups) about the change.
        // Call OnItemAdded first so any grouping/aggregation logic can run before any listeners processing the new total.
        OnItemAdded?.Invoke(item, amount, next);
        OnInventoryChanged?.Invoke(item, next);
    }

    public int Count(string itemId) => _bag.TryGetValue(itemId, out var v) ? v : 0;

    /// <summary>
    /// Returns a snapshot list of all items in the inventory.
    /// </summary>
    public List<Entry> GetAllItems()
    {
        var list = new List<Entry>(_definitions.Count);
        foreach (var kv in _definitions)
        {
            int cnt = 0;
            _bag.TryGetValue(kv.Key, out cnt);
            list.Add(new Entry { item = kv.Value, count = cnt });
        }
        return list;
    }
}