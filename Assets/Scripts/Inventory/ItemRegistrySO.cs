using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Juego/Item Registry", fileName = "ItemRegistry")]
public class ItemRegistrySO : ScriptableObject
{
    private static ItemRegistrySO _cachedInstance;

    [SerializeField] private List<ItemData> items = new();

    readonly Dictionary<string, ItemData> _map = new();

    // Exponer lectura para que otros sistemas (Inventory) puedan precargar definiciones completas
    public IReadOnlyList<ItemData> Items => items;

    void OnEnable() => Rebuild();
#if UNITY_EDITOR
    void OnValidate() => Rebuild();
#endif

    public void Rebuild()
    {
        _map.Clear();
        if (items == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item) continue;
            if (string.IsNullOrEmpty(item.itemId)) continue;
            _map[item.itemId] = item;
        }
    }

    public ItemData Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        
        // Siempre reconstruir si el mapa está vacío pero tenemos items
        if (_map.Count == 0 && items != null && items.Count > 0)
        {
            Debug.Log($"[ItemRegistrySO] Mapa vacío con {items.Count} items, reconstruyendo...");
            Rebuild();
        }
        
        if (_map.TryGetValue(itemId, out var item))
            return item;
            
        // Si no se encontró, intentar búsqueda case-insensitive como fallback
        foreach (var kvp in _map)
        {
            if (string.Equals(kvp.Key, itemId, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[ItemRegistrySO] Item '{itemId}' encontrado con case diferente: '{kvp.Key}'. Considera corregir el itemId.");
                return kvp.Value;
            }
        }
        
        return null;
    }

    public static ItemRegistrySO LoadDefault()
    {
        if (_cachedInstance == null)
        {
            _cachedInstance = Resources.Load<ItemRegistrySO>("ItemRegistry");
            if (_cachedInstance != null)
            {
                // Forzar rebuild al cargar para asegurar que el mapa está actualizado
                _cachedInstance.Rebuild();
                // Debug.Log($"[ItemRegistrySO] Cargado desde Resources con {_cachedInstance.items?.Count ?? 0} items");
            }
            else
            {
                Debug.LogWarning("[ItemRegistrySO] No se encontró ItemRegistry en Resources/ItemRegistry. Los items no se resolverán correctamente al cargar partidas.");
            }
        }
        return _cachedInstance;
    }
}
