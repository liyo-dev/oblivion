using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Spell Library", fileName = "SpellLibrary")]
public class SpellLibrarySO : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public SpellId id;
        public MagicSpellSO spell;
    }

    [SerializeField] private List<Entry> entries = new();

    private Dictionary<SpellId, MagicSpellSO> _map;

    void OnEnable() => Rebuild();
#if UNITY_EDITOR
    void OnValidate() => Rebuild();
#endif

    public void Rebuild()
    {
        if (_map == null) _map = new Dictionary<SpellId, MagicSpellSO>();
        _map.Clear();

        foreach (var e in entries)
        {
            if (!e.spell) continue;           // ignora entradas vacías
            _map[e.id] = e.spell;             // la última entrada con el mismo ID prevalece
        }
    }

    public MagicSpellSO Get(SpellId id)
    {
        if (id == SpellId.None) return null;  
        if (_map == null) Rebuild();
        _map.TryGetValue(id, out var spell);
        return spell;                         // null si no existe
    }

    public bool TryGet(SpellId id, out MagicSpellSO spell)
    {
        if (_map == null) Rebuild();
        return _map.TryGetValue(id, out spell);
    }

    public IReadOnlyList<Entry> Entries => entries;
}