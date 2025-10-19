using UnityEngine;
using System.Collections.Generic;

public class Inventory : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("Listado opcional de items conocidos para resolver IDs al cargar partidas.")]
    [SerializeField] private List<ItemData> knownItems = new();

    // Cantidades por itemId
    private readonly Dictionary<string, int> _bag = new();
    // Mapa itemId -> ItemData para UI y eventos
    private readonly Dictionary<string, ItemData> _definitions = new();

    public struct Entry
    {
        public ItemData item;
        public int count;
    }

    // Event fired when inventory changes: (itemData, newAmount)
    public event System.Action<ItemData, int> OnInventoryChanged;

    // Event fired when items are explicitly added: (itemData, addedAmount, newTotal)
    public event System.Action<ItemData, int, int> OnItemAdded;

    void Awake()
    {
        PreloadKnownItems();
    }

    public void Add(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return;
        RegisterDefinition(item);

        _bag.TryGetValue(item.itemId, out int cur);
        int next = cur + amount;
        _bag[item.itemId] = next;
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

    /// <summary>
    /// Convierte el inventario actual en una lista serializable.
    /// </summary>
    public List<InventoryItemSave> GetSaveSnapshot()
    {
        var snapshot = new List<InventoryItemSave>(_bag.Count);
        foreach (var kvp in _bag)
        {
            if (kvp.Value <= 0) continue;
            snapshot.Add(new InventoryItemSave
            {
                itemId = kvp.Key,
                count = kvp.Value
            });
        }
        return snapshot;
    }

    /// <summary>
    /// Restaura el inventario desde una lista serializable. Puede notificar cambios a los listeners.
    /// </summary>
    public void LoadSnapshot(IEnumerable<InventoryItemSave> snapshot, bool clearExisting = true, bool notifyChanges = true)
    {
        if (snapshot == null) return;

        if (clearExisting)
        {
            // Notificar reset del inventario actual
            if (notifyChanges && _bag.Count > 0)
            {
                foreach (var kvp in _bag)
                {
                    if (_definitions.TryGetValue(kvp.Key, out var def) && def != null)
                    {
                        OnInventoryChanged?.Invoke(def, 0);
                    }
                }
            }
            _bag.Clear();
        }

        foreach (var entry in snapshot)
        {
            if (string.IsNullOrEmpty(entry.itemId)) continue;
            if (entry.count <= 0) continue;

            var item = ResolveOrCreateDefinition(entry.itemId);
            _bag[entry.itemId] = entry.count;

            if (notifyChanges)
            {
                OnInventoryChanged?.Invoke(item, entry.count);
            }
        }
    }

    private void PreloadKnownItems()
    {
        if (knownItems == null) return;
        for (int i = 0; i < knownItems.Count; i++)
        {
            var item = knownItems[i];
            if (!item) continue;
            RegisterDefinition(item);
        }
    }

    private void RegisterDefinition(ItemData item)
    {
        if (!item) return;
        if (string.IsNullOrEmpty(item.itemId))
        {
            Debug.LogWarning($"[Inventory:{gameObject.name}] ItemData '{item.name}' no tiene itemId asignado.");
            return;
        }

        _definitions[item.itemId] = item;
    }

    private ItemData ResolveOrCreateDefinition(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (_definitions.TryGetValue(itemId, out var existing) && existing != null)
            return existing;

        // Intentar encontrarlo en la lista conocida
        if (knownItems != null)
        {
            for (int i = 0; i < knownItems.Count; i++)
            {
                var item = knownItems[i];
                if (!item) continue;
                if (item.itemId == itemId)
                {
                    RegisterDefinition(item);
                    return item;
                }
            }
        }

        // Fallback: crear un ItemData temporal en runtime para no quedar sin referencia
        var runtimeItem = ScriptableObject.CreateInstance<ItemData>();
        runtimeItem.itemId = itemId;
        runtimeItem.displayName = itemId;
        RegisterDefinition(runtimeItem);
        return runtimeItem;
    }
}
