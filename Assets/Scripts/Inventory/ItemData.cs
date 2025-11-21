using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Item", fileName = "IT_NewItem")]
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
    public string displayName;
    public Sprite icon;

    [Header("Uso desde inventario")]
    [Tooltip("Permite consumir este item directamente desde el inventario.")]
    public bool usableFromInventory;

    [Tooltip("Efectos que se aplican al usar el item desde el inventario (se procesan en orden).")]
    public List<PickupEffect> useEffects = new();

    [Tooltip("Texto descriptivo que se muestra en el menú del jugador.")]
    [TextArea] public string useDescription;

    [Header("Economía / Tienda")]
    public ItemUsageKind usageKind = ItemUsageKind.Consumable;
    [Min(0)] public int buyPrice = 10;
    [Min(0)] public int sellValue = 5;
    [Tooltip("Para items de equipamiento: WardrobeItem que se desbloquea al comprarlo.")]
    public WardrobeItemSO wardrobeUnlock;
}
