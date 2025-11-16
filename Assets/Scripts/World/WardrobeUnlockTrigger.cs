using UnityEngine;
using UnityEngine.Events;

public class WardrobeUnlockTrigger : MonoBehaviour
{
    [SerializeField] private WardrobeItemSO wardrobeItem;
    [SerializeField] private bool unlockOnStart = false;
    [SerializeField] private bool destroyAfterUnlock = false;
    [SerializeField] private bool logWarnings = true;
    [SerializeField] private UnityEvent onUnlocked;

    void Start()
    {
        if (unlockOnStart)
            Unlock();
    }

    [ContextMenu("Unlock Wardrobe Item")]
    public void Unlock()
    {
        if (WardrobeService.UnlockWardrobeItem(wardrobeItem, logWarnings))
        {
            onUnlocked?.Invoke();
            if (destroyAfterUnlock)
                Destroy(gameObject);
        }
    }
}
