using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item Registry", fileName = "ItemRegistry")]
public class ItemRegistrySO : ScriptableObject
{
    private static ItemRegistrySO _cachedInstance;

    [SerializeField] private List<ItemData> items = new();

    readonly Dictionary<string, ItemData> _map = new();

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
        if (_map.Count == 0) Rebuild();
        _map.TryGetValue(itemId, out var item);
        return item;
    }

    public static ItemRegistrySO LoadDefault()
    {
        if (_cachedInstance == null)
            _cachedInstance = Resources.Load<ItemRegistrySO>("ItemRegistry");
        return _cachedInstance;
    }
}
