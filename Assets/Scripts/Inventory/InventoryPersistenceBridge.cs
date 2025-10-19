using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Inventory))]
[DefaultExecutionOrder(-40)]
public class InventoryPersistenceBridge : MonoBehaviour
{
    [SerializeField] private bool applyOnProfileReady = true;

    private Inventory _inventory;
    private bool _appliedFromPreset;

    private void Awake()
    {
        _inventory = GetComponent<Inventory>();
    }

    private void OnEnable()
    {
        if (_inventory == null)
            _inventory = GetComponent<Inventory>();

        SubscribeInventoryEvents();

        GameBootService.OnProfileReady += HandleProfileReady;
        PlayerService.OnPlayerRegistered += HandlePlayerRegistered;

        if (applyOnProfileReady && GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
    }

    private void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        PlayerService.OnPlayerRegistered -= HandlePlayerRegistered;

        UnsubscribeInventoryEvents();
    }

    private void HandlePlayerRegistered(GameObject player)
    {
        // Si el inventory no est� en este objeto, intentar cachearlo v�a PlayerService
        if (_inventory == null)
        {
            if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true))
            {
                _inventory = inv;
                SubscribeInventoryEvents();
            }
        }

        if (applyOnProfileReady && GameBootService.IsAvailable)
        {
            ApplyPresetToInventory();
        }
    }

    private void HandleProfileReady()
    {
        if (!applyOnProfileReady) return;
        ApplyPresetToInventory();
    }

    private void SubscribeInventoryEvents()
    {
        if (_inventory == null) return;
        _inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void UnsubscribeInventoryEvents()
    {
        if (_inventory == null) return;
        _inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged(ItemData item, int newAmount)
    {
        if (_appliedFromPreset) return;
        WriteSnapshotToPreset();
    }

    private void ApplyPresetToInventory()
    {
        if (_inventory == null) return;
        if (!GameBootService.IsAvailable) return;

        var profile = GameBootService.Profile;
        if (profile == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;

        _appliedFromPreset = true;
        _inventory.LoadSnapshot(preset.inventoryItems, clearExisting: true, notifyChanges: false);
        _appliedFromPreset = false;

        // Asegurar que el runtimePreset refleje los datos (por si ven�an null)
        WriteSnapshotToPreset();
    }

    private void WriteSnapshotToPreset()
    {
        if (!GameBootService.IsAvailable) return;
        var profile = GameBootService.Profile;
        if (profile == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;

        if (_inventory == null) return;

        var snapshot = _inventory.GetSaveSnapshot();
        if (preset.inventoryItems == null)
            preset.inventoryItems = new List<InventoryItemSave>();
        else
            preset.inventoryItems.Clear();

        preset.inventoryItems.AddRange(snapshot);
    }
}
