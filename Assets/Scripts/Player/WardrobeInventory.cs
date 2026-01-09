using System;
using System.Collections.Generic;
using UnityEngine;

public class WardrobeInventory : MonoBehaviour
{
    [SerializeField] private ModularAutoBuilder builder;
    [SerializeField] private bool unlockAllByDefault = true;
    [SerializeField] private WardrobeItemSO[] initialUnlocked;

    readonly Dictionary<PartCategory, List<WardrobeEntry>> _entries = new();
    readonly HashSet<string> _persistedIds = new(StringComparer.OrdinalIgnoreCase);

    PlayerPresetSO _currentPreset;

    public event Action OnWardrobeChanged;

    void Awake()
    {
        if (builder == null)
            builder = GetComponentInChildren<ModularAutoBuilder>(true) ?? GetComponent<ModularAutoBuilder>();
        InitializeCategories();
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        if (GameBootService.IsAvailable)
            HandleProfileReady();
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    void HandleProfileReady()
    {
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset != null)
            ApplyFromPreset(preset);
    }

    void InitializeCategories()
    {
        foreach (PartCategory cat in Enum.GetValues(typeof(PartCategory)))
        {
            if (!_entries.ContainsKey(cat))
                _entries[cat] = new List<WardrobeEntry>();
        }
    }

    void ClearAll()
    {
        foreach (var list in _entries.Values)
            list.Clear();
        _persistedIds.Clear();
    }

    public void ApplyFromPreset(PlayerPresetSO preset)
    {
        if (preset == null) return;

        EnsureBuilderReference();
        InitializeCategories();

        _currentPreset = preset;
        ClearAll();

        if (unlockAllByDefault)
            AutoUnlockAll();

        if (initialUnlocked != null)
        {
            for (int i = 0; i < initialUnlocked.Length; i++)
            {
                var item = initialUnlocked[i];
                if (item)
                    Unlock(item, persistToPreset: false);
            }
        }

        if (preset.unlockedWardrobeIds != null)
        {
            foreach (var id in preset.unlockedWardrobeIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var item = WardrobeItemSO.Find(id);
                if (item == null)
                {
                    Debug.LogWarning($"[WardrobeInventory] Wardrobe item '{id}' no encontrado al aplicar preset.");
                    continue;
                }

                _persistedIds.Add(id);
                AddEntry(CreateEntry(item), persistable: true);
            }
        }

        EnsureCurrentSelectionEntries();
        NotifyChanged();
    }

    void EnsureBuilderReference()
    {
        if (builder != null) return;
        builder = GetComponentInChildren<ModularAutoBuilder>(true) ?? GetComponent<ModularAutoBuilder>();
        if (builder == null)
            PlayerService.TryGetComponent(out builder, includeInactive: true, allowSceneLookup: true);
    }

    void AutoUnlockAll()
    {
        EnsureBuilderReference();
        if (builder == null) return;

        foreach (PartCategory cat in Enum.GetValues(typeof(PartCategory)))
        {
            var options = builder.GetOptions(cat);
            if (options == null) continue;
            foreach (var option in options)
            {
                if (string.IsNullOrEmpty(option)) continue;
                AddEntry(new WardrobeEntry
                {
                    category = cat,
                    partName = option,
                    displayName = option,
                    wardrobeId = null
                }, persistable: false);
            }
        }
    }

    void EnsureCurrentSelectionEntries()
    {
        EnsureBuilderReference();
        if (builder == null) return;

        var selection = builder.GetSelection();
        if (selection == null) return;

        foreach (var kvp in selection)
        {
            if (string.IsNullOrEmpty(kvp.Value)) continue;
            AddEntry(new WardrobeEntry
            {
                category = kvp.Key,
                partName = kvp.Value,
                displayName = kvp.Value,
                wardrobeId = null
            }, persistable: false);
        }
    }

    WardrobeEntry CreateEntry(WardrobeItemSO item)
    {
        return new WardrobeEntry
        {
            category = item.Category,
            partName = item.PartName,
            displayName = item.DisplayName,
            description = item.Description,
            icon = item.Icon,
            wardrobeId = item.WardrobeId
        };
    }

    bool AddEntry(WardrobeEntry entry, bool persistable)
    {
        if (string.IsNullOrEmpty(entry.partName))
        {
            Debug.LogWarning("[WardrobeInventory.AddEntry] partName está vacío");
            return false;
        }
        
        if (!_entries.TryGetValue(entry.category, out var list))
        {
            list = new List<WardrobeEntry>();
            _entries[entry.category] = list;
            // Debug.Log($"[WardrobeInventory.AddEntry] Creada nueva lista para categoría {entry.category}");
        }

        int existingIndex = list.FindIndex(e => string.Equals(e.partName, entry.partName, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            // Debug.Log($"[WardrobeInventory.AddEntry] Ya existe entrada para '{entry.partName}' en {entry.category}");
            if (persistable)
            {
                var existing = list[existingIndex];
                if (string.IsNullOrEmpty(existing.wardrobeId) && !string.IsNullOrEmpty(entry.wardrobeId))
                {
                    existing.wardrobeId = entry.wardrobeId;
                    existing.displayName = entry.displayName;
                    existing.description = entry.description;
                    existing.icon = entry.icon;
                    list[existingIndex] = existing;
                    // Debug.Log($"[WardrobeInventory.AddEntry] ✅ Actualizada entrada existente con wardrobeId '{entry.wardrobeId}'");
                    return true;
                }
            }
            return false;
        }

        int insertIndex = list.FindIndex(e => string.Compare(e.displayName, entry.displayName, StringComparison.OrdinalIgnoreCase) > 0);
        if (insertIndex >= 0)
            list.Insert(insertIndex, entry);
        else
            list.Add(entry);

        // Debug.Log($"[WardrobeInventory.AddEntry] ✅ Añadida nueva entrada '{entry.partName}' ({entry.displayName}) a {entry.category}. Total items en categoría: {list.Count}");

        if (persistable && !string.IsNullOrEmpty(entry.wardrobeId))
            _persistedIds.Add(entry.wardrobeId);

        return true;
    }

    public bool Unlock(WardrobeItemSO item, bool persistToPreset = true)
    {
        if (!item)
        {
            Debug.LogWarning("[WardrobeInventory] WardrobeItemSO no asignado.");
            return false;
        }

        // Debug.Log($"[WardrobeInventory] Unlock: {item.WardrobeId} (Category: {item.Category}, PartName: {item.PartName}, persistToPreset: {persistToPreset})");

        var entry = CreateEntry(item);
        bool added = AddEntry(entry, persistable: true);
        
        if (!added)
        {
            // Debug.LogWarning($"[WardrobeInventory] El item '{item.WardrobeId}' no pudo ser añadido (probablemente ya existe)");
            return false;
        }

        // Debug.Log($"[WardrobeInventory] ✅ Item '{item.WardrobeId}' añadido correctamente a la categoría {item.Category}");

        if (persistToPreset && !string.IsNullOrEmpty(entry.wardrobeId))
        {
            _persistedIds.Add(entry.wardrobeId);
            SyncPresetUnlockedIds();
            // Debug.Log($"[WardrobeInventory] Item '{item.WardrobeId}' persistido al preset");
        }

        // Debug.Log($"[WardrobeInventory] Notificando cambios (OnWardrobeChanged)");
        NotifyChanged();
        return true;
    }

    public IReadOnlyList<WardrobeEntry> GetUnlockedOptions(PartCategory category)
    {
        return _entries.TryGetValue(category, out var list) ? list : Array.Empty<WardrobeEntry>();
    }

    public bool HasOptions(PartCategory category)
    {
        return _entries.TryGetValue(category, out var list) && list.Count > 0;
    }

    public bool TryGetEntry(PartCategory category, string partName, out WardrobeEntry entry)
    {
        entry = default;
        if (!_entries.TryGetValue(category, out var list)) return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].partName, partName, StringComparison.OrdinalIgnoreCase))
            {
                entry = list[i];
                return true;
            }
        }
        return false;
    }

    void SyncPresetUnlockedIds()
    {
        if (_currentPreset == null && GameBootService.IsAvailable)
            _currentPreset = GameBootService.Profile?.GetActivePresetResolved();
        if (_currentPreset == null) return;

        if (_currentPreset.unlockedWardrobeIds == null)
            _currentPreset.unlockedWardrobeIds = new List<string>();

        _currentPreset.unlockedWardrobeIds.Clear();
        _currentPreset.unlockedWardrobeIds.AddRange(_persistedIds);
    }

    void NotifyChanged()
    {
        OnWardrobeChanged?.Invoke();
    }

    public struct WardrobeEntry
    {
        public PartCategory category;
        public string partName;
        public string displayName;
        public string description;
        public Sprite icon;
        public string wardrobeId;
    }
}
