using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MagicLibrary", menuName = "Game/Magic/Library")]
public class MagicLibrarySO : ScriptableObject
{
    [SerializeField] private List<MagicSpellSO> spells = new();

    public MagicSpellSO GetSpell(MagicKind kind, MagicElement element)
    {
        for (int i = 0; i < spells.Count; i++)
        {
            var s = spells[i];
            if (s != null && s.kind == kind && s.element == element)
                return s;
        }
        return null;
    }

    public IReadOnlyList<MagicSpellSO> All => spells;
}
