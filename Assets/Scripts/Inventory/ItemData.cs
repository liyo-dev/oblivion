using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Juego/Item", fileName = "IT_NewItem")]
public class ItemData : ScriptableObject
{
    public enum ItemUsageKind
    {
        Consumable,
        Quest,
        Equipment,
        Currency
    }

    public string itemId;
    [Tooltip("ID de localización para el nombre (ej: 'ITEM_COIN_NAME'). Si está vacío, usa displayName.")]
    public string displayNameId;
    public string displayName;
    public Sprite icon;

    [Header("Uso desde inventario")]
    [Tooltip("Permite consumir este item directamente desde el inventario.")]
    public bool usableFromInventory;

    [Tooltip("Efectos que se aplican al usar el item desde el inventario (se procesan en orden).")]
    public List<PickupEffect> useEffects = new();

    [Tooltip("ID de localización para la descripción (ej: 'ITEM_COIN_DESC'). Si está vacío, usa useDescription.")]
    public string useDescriptionId;

    [Tooltip("Texto descriptivo que se muestra en el menú del jugador.")]
    [TextArea] public string useDescription;

    /// <summary>Obtiene el nombre localizado del item (usa displayNameId si está definido).</summary>
    public string GetLocalizedName()
    {
        if (!string.IsNullOrEmpty(displayNameId) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(displayNameId, displayName);
        return displayName;
    }

    /// <summary>Obtiene la descripción localizada del item (usa useDescriptionId si está definido).</summary>
    public string GetLocalizedDescription()
    {
        if (!string.IsNullOrEmpty(useDescriptionId) && LocalizationManager.Instance != null)
            return LocalizationManager.Instance.Get(useDescriptionId, useDescription);
        return useDescription;
    }

    [Header("Economía / Tienda")]
    public ItemUsageKind usageKind = ItemUsageKind.Consumable;
    [Min(0)] public int buyPrice = 10;
    [Min(0)] public int sellValue = 5;
    [Tooltip("Para items de equipamiento: WardrobeItem que se desbloquea al comprarlo.")]
    public WardrobeItemSO wardrobeUnlock;
}
