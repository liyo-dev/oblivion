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

        Debug.Log($"[WardrobeService] Intentando desbloquear item: {item.WardrobeId} (Category: {item.Category}, PartName: {item.PartName})");

        if (PlayerService.TryGetComponent(out WardrobeInventory wardrobe, includeInactive: true, allowSceneLookup: true))
        {
        Debug.Log($"[WardrobeService] WardrobeInventory encontrado, desbloqueando...");
        bool changed = wardrobe.Unlock(item, persistToPreset: true);
        if (changed)
        {
            Debug.Log($"[WardrobeService] ✅ Item '{item.WardrobeId}' desbloqueado correctamente");
            Debug.Log($"[WardrobeService] 📢 Invocando OnWardrobeItemUnlocked para '{item.WardrobeId}'");
            OnWardrobeItemUnlocked?.Invoke(item);
            TryShowPopup(item);
            
            // Log del estado actual del wardrobe después del desbloqueo
            var method = wardrobe.GetType().GetMethod("GetUnlockedOptions");
            if (method != null)
            {
                try
                {
                    var paramType = method.GetParameters()[0].ParameterType;
                    var categoryName = item.Category.ToString();
                    var enumVal = System.Enum.Parse(paramType, categoryName);
                    var result = method.Invoke(wardrobe, new object[] { enumVal });
                    if (result != null)
                    {
                        var count = (int)result.GetType().GetProperty("Count").GetValue(result);
                        Debug.Log($"[WardrobeService] Tras desbloquear, categoría {categoryName} tiene {count} items");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[WardrobeService] Error al verificar opciones desbloqueadas: {ex.Message}");
                }
            }
        }
        else if (logWarnings)
            Debug.LogWarning($"[WardrobeService] El item '{item.WardrobeId}' ya estaba desbloqueado.");
        return changed;
        }

        Debug.LogWarning("[WardrobeService] No se encontró WardrobeInventory en el player, guardando directamente al preset");

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
        Debug.Log($"[WardrobeService] ✅ Item '{item.WardrobeId}' añadido al preset");
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
            _popupQueue = ServiceLocator.Get<CollectiblePopupQueue>(false);
        }

        if (_popupQueue != null)
        {
            _popupQueue.SpawnWardrobePopup(item, 1);
        }
    }
}
