using System.Collections.Generic;
using UnityEngine;

public static class WardrobeService
{
    public static event System.Action<WardrobeItemSO> OnWardrobeItemUnlocked;

    // Cache liviano para el popup de feedback
    static CollectiblePopupQueue _popupQueue;

    public static bool UnlockWardrobeItem(WardrobeItemSO item, bool logWarnings = true)
    {
        if (!item)
        {
            if (logWarnings)
                Debug.LogWarning("[WardrobeService] Wardrobe item no asignado.");
            return false;
        }

        if (PlayerService.TryGetComponent(out WardrobeInventory wardrobe, includeInactive: true, allowSceneLookup: true))
        {
            bool changed = wardrobe.Unlock(item, persistToPreset: true);
            if (changed)
            {
                OnWardrobeItemUnlocked?.Invoke(item);
                TryShowPopup(item);
            }
            else if (logWarnings)
                Debug.LogWarning($"[WardrobeService] El item '{item.WardrobeId}' ya estaba desbloqueado.");
            return changed;
        }

        var preset = UnlockService.GetActivePreset();
        if (!preset)
        {
            if (logWarnings)
                Debug.LogWarning("[WardrobeService] No hay preset activo para registrar el desbloqueo.");
            return false;
        }

        if (preset.unlockedWardrobeIds == null)
            preset.unlockedWardrobeIds = new List<string>();

        if (preset.unlockedWardrobeIds.Contains(item.WardrobeId))
        {
            if (logWarnings)
                Debug.LogWarning($"[WardrobeService] El item '{item.WardrobeId}' ya estaba en el preset.");
            return false;
        }

        preset.unlockedWardrobeIds.Add(item.WardrobeId);
        OnWardrobeItemUnlocked?.Invoke(item);
        TryShowPopup(item);
        return true;
    }

    static void TryShowPopup(WardrobeItemSO item)
    {
        if (item == null) return;

        // Reutiliza CollectiblePopupQueue si está en escena
        if (_popupQueue == null || !_popupQueue)
        {
            _popupQueue = Object.FindFirstObjectByType<CollectiblePopupQueue>();
            if (_popupQueue == null)
                _popupQueue = Object.FindAnyObjectByType<CollectiblePopupQueue>();
        }

        if (_popupQueue != null)
        {
            _popupQueue.SpawnWardrobePopup(item, 1);
        }
    }
}
