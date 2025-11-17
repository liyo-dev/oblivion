using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Spell Library", fileName = "SpellLibrary")]
public class SpellLibrarySO : ScriptableObject
{
    [Tooltip("Lista de hechizos disponibles. El SpellId se lee automáticamente del SO.")]
    [SerializeField] private List<MagicSpellSO> spells = new();

    private Dictionary<SpellId, MagicSpellSO> _map;

    void OnEnable() => Rebuild();
#if UNITY_EDITOR
    void OnValidate() => Rebuild();
#endif

    public void Rebuild()
    {
        if (_map == null) _map = new Dictionary<SpellId, MagicSpellSO>();
        _map.Clear();

        foreach (var spell in spells)
        {
            if (!spell) continue;                    // ignora entradas vacías
            if (spell.spellId == SpellId.None)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[SpellLibrary] El hechizo '{spell.name}' tiene SpellId.None asignado. Asigna un ID válido.", spell);
#endif
                continue;
            }
            
            if (_map.ContainsKey(spell.spellId))
            {
#if UNITY_EDITOR
                // Solo avisar si son SOs diferentes con el mismo ID
                if (_map[spell.spellId] != spell)
                {
                    Debug.LogWarning($"[SpellLibrary] SpellId duplicado: {spell.spellId}. '{spell.name}' sobrescribe '{_map[spell.spellId].name}'.", spell);
                }
#endif
            }
            
            _map[spell.spellId] = spell;             // la última entrada con el mismo ID prevalece
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

    public IReadOnlyList<MagicSpellSO> Spells => spells;
}