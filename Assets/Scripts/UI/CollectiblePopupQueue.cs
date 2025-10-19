using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Listens to the player's Inventory changes and spawns collectible popup panels.
/// Attach this to a UI Canvas object and assign a popup prefab (with CollectiblePopupPanel).
/// </summary>
public class CollectiblePopupQueue : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Prefab that contains a CollectiblePopupPanel component.")]
    public GameObject popupPrefab;

    [Tooltip("Parent transform where popups will be instantiated (usually a panel under a Canvas)")]
    public Transform popupParent;

    [Header("Settings")]
    public float displayDuration = 1.25f;

    [Tooltip("When true new popups will be inserted as first sibling (top). When false they are added as last sibling (bottom) — adjust your VerticalLayoutGroup accordingly to stack from bottom.")]
    public bool spawnAtTop = false;

    [Header("Grouping")]
    [Tooltip("Time window (seconds) to group multiple quick additions of the same item into a single popup.")]
    public float groupingWindow = 0.4f;

    [Header("Deduplication")]
    [Tooltip("Short window (seconds) that suppresses immediate duplicate spawns for the same item id (defensive).")]
    public float dedupeWindow = 0.25f;

    private Inventory _inventory;

    // pending aggregated amounts per itemId
    private readonly Dictionary<string, int> _pending = new();

    // last spawn times per itemId to prevent near-simultaneous duplicates
    private readonly Dictionary<string, float> _lastSpawnTime = new();

    // keys for items that are active or currently being created to avoid race duplicates
    private readonly HashSet<string> _activeOrCreating = new();

    // small buffer (seconds) added to removal scheduling to ensure panel has time to fade out
    private const float RemovalBuffer = 0.12f;

    void Awake()
    {
        Debug.Log("[CollectiblePopupQueue] Awake: trying to bind to player inventory...");
        // Try bind to registered player inventory
        TryBindToPlayer();
        PlayerService.OnPlayerRegistered += OnPlayerRegistered;
    }

    void OnDestroy()
    {
        UnbindFromInventory();
        PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
    }

    private void OnPlayerRegistered(UnityEngine.GameObject player)
    {
        Debug.Log("[CollectiblePopupQueue] OnPlayerRegistered called");
        TryBindToPlayer();
    }

    private void TryBindToPlayer()
    {
        UnbindFromInventory();
        if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true))
        {
            _inventory = inv;
            _inventory.OnItemAdded += OnItemAdded;
            // Do not subscribe to OnInventoryChanged to avoid duplicate spawns (OnItemAdded is the authoritative event)
            Debug.Log($"[CollectiblePopupQueue] Bound to Inventory on '{inv.gameObject.name}'");
        }
        else
        {
            Debug.Log("[CollectiblePopupQueue] Inventory not found via PlayerService");
        }
    }

    private void UnbindFromInventory()
    {
        if (_inventory != null)
        {
            _inventory.OnItemAdded -= OnItemAdded;
            // Previously also unsubscribed OnInventoryChanged; removed since we don't subscribe to it.
            Debug.Log("[CollectiblePopupQueue] Unbound from Inventory");
            _inventory = null;
        }
    }

    private void OnInventoryChanged(ItemData item, int newAmount)
    {
        Debug.Log($"[CollectiblePopupQueue] OnInventoryChanged called for {item?.displayName} newAmount={newAmount}");
        // fallback: show the new total (used only if OnItemAdded isn't provided by publisher)
        SpawnPopup(item, newAmount);
    }

    private void OnItemAdded(ItemData item, int addedAmount, int newTotal)
    {
        if (item == null || addedAmount <= 0) return;

        Debug.Log($"[CollectiblePopupQueue] OnItemAdded called for {item?.displayName} added={addedAmount} total={newTotal}");

        // Aggregate multiple quick additions for the same item into one popup
        var id = item.itemId ?? item.displayName ?? "__null__";
        lock (_pending)
        {
            if (_pending.TryGetValue(id, out var cur))
                _pending[id] = cur + addedAmount;
            else
            {
                _pending[id] = addedAmount;
                // start flush coroutine for this item
                StartCoroutine(FlushGroupedPopup(id, item, groupingWindow));
            }
        }
    }

    private System.Collections.IEnumerator FlushGroupedPopup(string itemId, ItemData item, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        int amount = 0;
        lock (_pending)
        {
            if (_pending.TryGetValue(itemId, out var v))
            {
                amount = v;
                _pending.Remove(itemId);
            }
        }

        if (amount > 0)
            SpawnPopup(item, amount);
    }

    public void TestSpawn(ItemData item, int amount)
    {
        Debug.Log("[CollectiblePopupQueue] TestSpawn called");
        SpawnPopup(item, amount);
    }

    private void SpawnPopup(ItemData item, int amountToShow)
    {
        if (item != null)
        {
            var key = item.itemId ?? item.displayName ?? "__null__";

            // If there's a pending grouped flush for this item, skip spawning now; the flush will spawn the aggregated popup.
            lock (_pending)
            {
                if (_pending.ContainsKey(key))
                {
                    Debug.Log($"[CollectiblePopupQueue] Spawn suppressed because a grouped flush is pending for {key}");
                    return;
                }
            }

            // If already active or being created, aggregate into existing (avoid race where two coroutines try to instantiate)
            lock (_activeOrCreating)
            {
                if (_activeOrCreating.Contains(key))
                {
                    // try to find the existing panel and add amount
                    if (popupParent != null)
                    {
                        var existing = popupParent.GetComponentsInChildren<CollectiblePopupPanel>(true);
                        foreach (var p in existing)
                        {
                            if (p != null && p.ItemId == key)
                            {
                                Debug.Log($"[CollectiblePopupQueue] Aggregating into already-creating popup for {key} (adding {amountToShow})");
                                p.AddAmount(amountToShow);
                                return;
                            }
                        }
                    }
                    // If we couldn't find it yet, still skip; the creation will aggregate once ready.
                    Debug.Log($"[CollectiblePopupQueue] Deferring spawn because {key} is being created");
                    return;
                }
                // reserve key to mark creation in progress
                _activeOrCreating.Add(key);
            }

            if (_lastSpawnTime.TryGetValue(key, out var last))
            {
                if (Time.realtimeSinceStartup - last < dedupeWindow)
                {
                    Debug.Log($"[CollectiblePopupQueue] Suppressed duplicate popup for {key} (within dedupe window {dedupeWindow}s)");
                    // remove creation reservation since we won't create
                    lock (_activeOrCreating) { _activeOrCreating.Remove(key); }
                    return;
                }
            }
            _lastSpawnTime[key] = Time.realtimeSinceStartup;

            // If there's already an active popup for this item, aggregate into it instead of spawning a new one.
            if (popupParent != null)
            {
                var existing = popupParent.GetComponentsInChildren<CollectiblePopupPanel>(true);
                foreach (var p in existing)
                {
                    if (p != null && p.ItemId == key)
                    {
                        Debug.Log($"[CollectiblePopupQueue] Aggregating into existing popup for {key} (adding {amountToShow})");
                        p.AddAmount(amountToShow);
                        lock (_activeOrCreating) { _activeOrCreating.Remove(key); }
                        return;
                    }
                }
            }
        }

        if (popupPrefab == null)
        {
            Debug.LogWarning("[CollectiblePopupQueue] popupPrefab is null — cannot spawn popup.");
            // ensure we release reservation if present
            if (item != null)
            {
                var key = item.itemId ?? item.displayName ?? "__null__";
                lock (_activeOrCreating) { _activeOrCreating.Remove(key); }
            }
            return;
        }

        Transform parent = popupParent != null ? popupParent : transform;
        var go = Instantiate(popupPrefab, parent);
        if (go == null)
        {
            Debug.LogWarning("[CollectiblePopupQueue] Instantiate returned null");
            if (item != null)
            {
                var key = item.itemId ?? item.displayName ?? "__null__";
                lock (_activeOrCreating) { _activeOrCreating.Remove(key); }
            }
            return;
        }

        // Ensure proper sibling order according to setting
        if (spawnAtTop)
            go.transform.SetAsFirstSibling();
        else
            go.transform.SetAsLastSibling();

        // Force layout rebuild so sizes/positions are updated immediately
        var parentRect = parent as RectTransform;
        if (parentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);

        // Ensure RectTransform is reset so it appears inside the parent
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning("[CollectiblePopupQueue] Spawned popup root has no RectTransform (not a UI element). It may not be visible inside a Canvas.");
        }

        if (!go.activeInHierarchy)
            go.SetActive(true);

        var panel = go.GetComponent<CollectiblePopupPanel>();
        if (panel != null)
        {
            Debug.Log($"[CollectiblePopupQueue] Spawned popup for {item?.displayName} amount={amountToShow}");
            panel.Init(item, amountToShow, displayDuration);

            // schedule removal of creation reservation after expected life (displayDuration + small buffer)
            if (item != null)
            {
                var key = item.itemId ?? item.displayName ?? "__null__";
                float delay = displayDuration + RemovalBuffer + 0.5f; // safe extra margin
                StartCoroutine(RemoveActiveAfter(key, delay));
            }
        }
        else
        {
            Debug.LogWarning("[CollectiblePopupQueue] popupPrefab has no CollectiblePopupPanel component. Inspecting children...");
            var panelChild = go.GetComponentInChildren<CollectiblePopupPanel>();
            if (panelChild != null)
            {
                Debug.Log("[CollectiblePopupQueue] Found CollectiblePopupPanel in children — initializing");
                panelChild.Init(item, amountToShow, displayDuration);

                if (item != null)
                {
                    var key = item.itemId ?? item.displayName ?? "__null__";
                    float delay = displayDuration + RemovalBuffer + 0.5f;
                    StartCoroutine(RemoveActiveAfter(key, delay));
                }
            }
            else
            {
                Debug.LogWarning("[CollectiblePopupQueue] No CollectiblePopupPanel found on popup prefab. Destroying instance.");
                if (item != null)
                {
                    var key = item.itemId ?? item.displayName ?? "__null__";
                    lock (_activeOrCreating) { _activeOrCreating.Remove(key); }
                }
                Destroy(go);
            }
        }
    }

    private System.Collections.IEnumerator RemoveActiveAfter(string key, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        lock (_activeOrCreating)
        {
            _activeOrCreating.Remove(key);
        }
    }
}
