using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerState : MonoBehaviour
{
    [Header("Inicio")]
    public GameConfigSO  config;
    public PlayerPresetSO newGamePreset;

    [Header("Refs opcionales")]
    public Damageable damageable; // si lo tienes
    public ManaPool  mana;        // si lo tienes

    // Runtime
    public int Level { get; private set; }
    public float MaxHP { get; private set; }
    public float CurrentHP { get; private set; }
    public float MaxMP { get; private set; }
    public float CurrentMP { get; private set; }

    HashSet<AbilityId> _abilities = new();
    HashSet<SpellId>   _spells    = new();
    HashSet<string>    _flags     = new();

    public System.Action OnAbilitiesChanged;
    public System.Action OnSpellsChanged;
    public System.Action OnFlagsChanged;

    void Awake()
    {
        if (!damageable) damageable = GetComponent<Damageable>();
        if (!mana)       mana       = GetComponent<ManaPool>();
    }

    public void LoadFromSave(PlayerSaveData d)
    {
        Level = d.level;
        MaxHP = d.maxHP; CurrentHP = Mathf.Clamp(d.currentHP, 0, MaxHP);
        MaxMP = d.maxMP; CurrentMP = Mathf.Clamp(d.currentMP, 0, MaxMP);
        _abilities = new HashSet<AbilityId>(d.unlockedAbilities ?? new());
        _spells    = new HashSet<SpellId>(d.unlockedSpells    ?? new());
        _flags     = new HashSet<string>  (d.flags            ?? new());
        ApplyToComponents();
    }

    public PlayerSaveData CreateSave(string spawnAnchorId, QuestLog questLog)
    {
        return new PlayerSaveData {
            level = Level, maxHP=MaxHP, currentHP=CurrentHP, maxMP=MaxMP, currentMP=CurrentMP,
            unlockedAbilities = new List<AbilityId>(_abilities),
            unlockedSpells    = new List<SpellId>(_spells),
            flags             = new List<string>(_flags),
            quests            = questLog ? questLog.Export() : new(),
            lastSpawnAnchorId = spawnAnchorId
        };
    }

    public void ApplyToComponents()
    {
        if (damageable){ damageable.SetMaxAndCurrent(MaxHP, CurrentHP); }
        if (mana)      { mana.Init(MaxMP, CurrentMP); }
        OnAbilitiesChanged?.Invoke();
        OnSpellsChanged?.Invoke();
        OnFlagsChanged?.Invoke();
    }

    // === API ===
    public bool HasAbility(AbilityId a)=>_abilities.Contains(a);
    public bool HasSpell(SpellId s)=>_spells.Contains(s);
    public bool HasFlag(string f)=>_flags.Contains(f);

    public void UnlockAbility(AbilityId a){ if(_abilities.Add(a)) OnAbilitiesChanged?.Invoke(); }
    public void UnlockSpell(SpellId s){ if(_spells.Add(s)) OnSpellsChanged?.Invoke(); }
    public void SetFlag(string f,bool v){ bool ch = v? _flags.Add(f) : _flags.Remove(f); if(ch) OnFlagsChanged?.Invoke(); }

    // Helpers set health from save/preset (añade en tu Damageable)
    public void SetHPMP(float hp, float mp){ CurrentHP=hp; CurrentMP=mp; ApplyToComponents(); }
}
