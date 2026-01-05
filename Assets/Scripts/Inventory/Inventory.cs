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
    /// Returns a snapshot list of items currently owned (>0 units).
    /// </summary>
    public List<Entry> GetAllItems()
    {
        var list = new List<Entry>(_bag.Count);
        foreach (var kv in _bag)
        {
            if (kv.Value <= 0) continue;

            if (!_definitions.TryGetValue(kv.Key, out var item) || item == null)
                item = ResolveOrCreateDefinition(kv.Key);

            list.Add(new Entry { item = item, count = kv.Value });
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
        if (snapshot == null)
        {
            if (clearExisting)
            {
                // Resetear inventario aunque no haya snapshot (evita arrastrar ítems antiguos).
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
            return;
        }

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

        foreach (var itemSave in snapshot)
        {
            if (string.IsNullOrEmpty(itemSave.itemId)) continue;

            // Registrar definición si no existe
            if (!_definitions.ContainsKey(itemSave.itemId))
            {
                var resolvedItem = ResolveOrCreateDefinition(itemSave.itemId);
                if (resolvedItem != null)
                {
                    RegisterDefinition(resolvedItem);
                }
                else
                {
                    Debug.LogWarning($"[Inventory] No se pudo resolver definición para itemId '{itemSave.itemId}'");
                }
            }

            // Agregar al inventario
            _bag[itemSave.itemId] = itemSave.count;
            if (notifyChanges)
            {
                if (_definitions.TryGetValue(itemSave.itemId, out var def) && def != null)
                {
                    OnInventoryChanged?.Invoke(def, itemSave.count);
                }
            }
        }
    }

    public bool HasItem(ItemData item, int requiredAmount = 1)
    {
        if (item == null || requiredAmount <= 0) return false;
        return _bag.TryGetValue(item.itemId, out var cur) && cur >= requiredAmount;
    }

    public bool HasItem(string itemId, int requiredAmount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || requiredAmount <= 0) return false;
        return _bag.TryGetValue(itemId, out var cur) && cur >= requiredAmount;
    }

    public bool TryConsume(ItemData item, int amount = 1, bool notifyChanges = true)
    {
        if (item == null) return false;
        return TryConsume(item.itemId, amount, notifyChanges, item);
    }

    public bool TryConsume(string itemId, int amount = 1, bool notifyChanges = true, ItemData fallbackDefinition = null)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (amount <= 0) amount = 1;

        if (!_bag.TryGetValue(itemId, out int cur) || cur < amount) return false;

        int next = cur - amount;
        if (next > 0)
        {
            _bag[itemId] = next;
        }
        else
        {
            _bag.Remove(itemId);
        }

        if (_definitions.TryGetValue(itemId, out var def) == false && fallbackDefinition != null)
        {
            RegisterDefinition(fallbackDefinition);
            def = fallbackDefinition;
        }

        if (notifyChanges)
        {
            OnInventoryChanged?.Invoke(def, Mathf.Max(next, 0));
        }

        return true;
    }

    public ItemData GetDefinition(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        _definitions.TryGetValue(itemId, out var def);
        return def;
    }

    private void PreloadKnownItems()
    {
        if (knownItems != null)
        {
            for (int i = 0; i < knownItems.Count; i++)
            {
                var item = knownItems[i];
                if (!item) continue;
                RegisterDefinition(item);
            }
        }

        // Además, precargar todas las definiciones del ItemRegistry (si existe)
        var registry = ItemRegistrySO.LoadDefault();
        if (registry != null && registry.Items != null)
        {
            for (int i = 0; i < registry.Items.Count; i++)
            {
                var item = registry.Items[i];
                if (!item) continue;
                RegisterDefinition(item);
            }
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
        {
            // Si la definición que tenemos es un placeholder temporal (sin icono ni nombre),
            // intentar reemplazarla con la versión real del registro o la lista conocida.
            if (IsPlaceholder(existing))
            {
                Debug.Log($"[Inventory] Definición existente para '{itemId}' es un placeholder, intentando actualizar...");
                var upgraded = TryResolveFromSources(itemId);
                if (upgraded != null)
                {
                    RegisterDefinition(upgraded);
                    return upgraded;
                }
            }

            return existing;
        }

        // No había definición previa, intentar resolver usando los orígenes conocidos.
        var resolved = TryResolveFromSources(itemId);
        if (resolved != null)
            return resolved;

        // Fallback: crear un ItemData temporal en runtime para no quedar sin referencia
        Debug.LogWarning($"[Inventory] ⚠️ Creando placeholder temporal para item '{itemId}'. El item no se encontró en knownItems, ItemRegistry ni Resources.");
        Debug.LogWarning($"[Inventory] 💡 Para solucionar: Añade el ItemData al ItemRegistry (Resources/ItemRegistry) o a la lista knownItems del componente Inventory.");
        var runtimeItem = ScriptableObject.CreateInstance<ItemData>();
        runtimeItem.itemId = itemId;
        runtimeItem.displayName = itemId;
        RegisterDefinition(runtimeItem);
        return runtimeItem;
    }

    private ItemData TryResolveFromSources(string itemId)
    {
        // Intentar encontrarlo en la lista conocida
        if (knownItems != null)
        {
            for (int i = 0; i < knownItems.Count; i++)
            {
                var item = knownItems[i];
                if (!item) continue;
                if (item.itemId == itemId)
                {
                    Debug.Log($"[Inventory] Item '{itemId}' resuelto desde knownItems");
                    RegisterDefinition(item);
                    return item;
                }
            }
        }

        // Intentar resolverlo a través del registro global
        var registry = ItemRegistrySO.LoadDefault();
        if (registry != null)
        {
            var resolved = registry.Get(itemId);
            if (resolved != null)
            {
                Debug.Log($"[Inventory] Item '{itemId}' resuelto desde ItemRegistry");
                RegisterDefinition(resolved);
                return resolved;
            }
        }

        // Fallback: intentar cargar directamente desde Resources por nombre del asset
        // Esto es útil para items que no están en el registro pero sí existen como assets
        var directLoad = Resources.Load<ItemData>($"Items/{itemId}");
        if (directLoad != null)
        {
            Debug.Log($"[Inventory] Item '{itemId}' resuelto directamente desde Resources/Items/");
            RegisterDefinition(directLoad);
            return directLoad;
        }
        
        // Intentar buscar en todas las carpetas de Resources
        var allItems = Resources.LoadAll<ItemData>("");
        foreach (var item in allItems)
        {
            if (item != null && item.itemId == itemId)
            {
                Debug.Log($"[Inventory] Item '{itemId}' encontrado mediante búsqueda exhaustiva en Resources");
                RegisterDefinition(item);
                return item;
            }
        }

        Debug.LogWarning($"[Inventory] ⚠️ No se pudo resolver el item '{itemId}' desde ninguna fuente. Verifica que el itemId coincida con el asset o que esté en el ItemRegistry.");
        return null;
    }

    private static bool IsPlaceholder(ItemData item)
    {
        if (!item) return true;

        bool lacksIcon = item.icon == null;
        bool lacksName = string.IsNullOrEmpty(item.displayName) || item.displayName == item.itemId;

        return lacksIcon || lacksName;
    }
}
