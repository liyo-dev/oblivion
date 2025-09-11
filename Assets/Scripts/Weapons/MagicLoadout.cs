using UnityEngine;

public class MagicLoadout : MonoBehaviour
{
    public MagicLibrarySO library;

    [Header("Slots activos")]
    public MagicElement leftElement    = MagicElement.Fire;
    public MagicElement rightElement   = MagicElement.Ice;
    public MagicElement specialElement = MagicElement.Fire;

    public MagicSpellSO GetForSlot(MagicSlot slot)
    {
        if (!library) return null;

        var elem = slot == MagicSlot.Left ? leftElement
            : slot == MagicSlot.Right ? rightElement
            : specialElement;

        var kind = (slot == MagicSlot.Special) ? MagicKind.Special : MagicKind.Projectile;
        return library.GetSpell(kind, elem);
    }

    public void ApplyElements(MagicElement left, MagicElement right, MagicElement special)
    {
        leftElement = left; rightElement = right; specialElement = special;
    }
}