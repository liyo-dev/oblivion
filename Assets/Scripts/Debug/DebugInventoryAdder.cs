using UnityEngine;

/// <summary>
/// Debug helper: press K to add the configured ItemData to the player's Inventory
/// and (optionally) force spawn a popup via any CollectiblePopupQueue in scene.
/// </summary>
public class DebugInventoryAdder : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;
    // By default don't force spawn because Inventory.Add already triggers OnItemAdded.
    public bool alsoForcePopup = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (item == null)
            {
                Debug.LogWarning("[DebugInventoryAdder] No ItemData assigned.");
                return;
            }

            if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true))
            {
                inv.Add(item, amount);
                Debug.Log($"[DebugInventoryAdder] Added {amount} {item.displayName} to Inventory '{inv.gameObject.name}'");
            }
            else
            {
                Debug.LogWarning("[DebugInventoryAdder] Inventory not found via PlayerService.");
            }

            if (alsoForcePopup)
            {
                var queue = FindObjectOfType<CollectiblePopupQueue>();
                if (queue != null)
                {
                    Debug.Log("[DebugInventoryAdder] Forcing TestSpawn on CollectiblePopupQueue");
                    queue.TestSpawn(item, amount);
                }
                else
                {
                    Debug.LogWarning("[DebugInventoryAdder] No CollectiblePopupQueue found in scene.");
                }
            }
        }
    }
}
