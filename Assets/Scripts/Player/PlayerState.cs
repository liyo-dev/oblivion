using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerState : MonoBehaviour
{
    // ===== RUNTIME =====
    public int    Level      { get; private set; }
    public float  MaxHp      { get; private set; }
    public float  CurrentHp  { get; private set; }
    public float  MaxMp      { get; private set; }
    public float  CurrentMp  { get; private set; }
    public string LastSpawnAnchorId { get; private set; }

    // Desbloqueos / flags (runtime)
    readonly HashSet<AbilityId> _abilities = new();
    readonly HashSet<SpellId>   _spells    = new();
    readonly HashSet<string>    _flags     = new();

    // Refs opcionales (no serializadas)
    Damageable _damageable;
    ManaPool   _mana;

    // Eventos opcionales
    public System.Action OnAbilitiesChanged;
    public System.Action OnSpellsChanged;
    public System.Action OnFlagsChanged;
    public System.Action OnStatsChanged;

    void Awake()
    {
        _damageable = GetComponent<Damageable>();
        _mana       = GetComponent<ManaPool>();
    }

    // ===== CARGA DESDE SAVE / PRESET =====

    public void LoadFromSave(PlayerSaveData d)
    {
        if (d == null) return;

        Level     = Mathf.Max(1, d.level);
        MaxHp     = Mathf.Max(1f, d.maxHp);
        CurrentHp = Mathf.Clamp(d.currentHp, 0f, MaxHp);
        MaxMp     = Mathf.Max(0f, d.maxMp);
        CurrentMp = Mathf.Clamp(d.currentMp, 0f, MaxMp);
        LastSpawnAnchorId = string.IsNullOrEmpty(d.lastSpawnAnchorId) ? LastSpawnAnchorId : d.lastSpawnAnchorId;

        _abilities.Clear(); if (d.abilities != null) foreach (var a in d.abilities) _abilities.Add(a);
        _spells.Clear();    if (d.spells    != null) foreach (var s in d.spells)    _spells.Add(s);
        _flags.Clear();     if (d.flags     != null) foreach (var f in d.flags)     _flags.Add(f);

        ApplyToComponents();
    }

    public void ApplyPreset(PlayerPresetSO p, string spawnAnchorId = null)
    {
        if (!p) return;

        Level     = Mathf.Max(1, p.level);
        MaxHp     = Mathf.Max(1f, p.maxHP);
        CurrentHp = Mathf.Clamp(p.currentHP, 0f, MaxHp);
        MaxMp     = Mathf.Max(0f, p.maxMP);
        CurrentMp = Mathf.Clamp(p.currentMP, 0f, MaxMp);
        if (!string.IsNullOrEmpty(spawnAnchorId)) LastSpawnAnchorId = spawnAnchorId;

        _abilities.Clear(); if (p.unlockedAbilities != null) foreach (var a in p.unlockedAbilities) _abilities.Add(a);
        _spells.Clear();    if (p.unlockedSpells    != null) foreach (var s in p.unlockedSpells)    _spells.Add(s);
        _flags.Clear();     if (p.flags             != null) foreach (var f in p.flags)             _flags.Add(f);

        ApplyToComponents();
    }

    // ===== GUARDADO =====

    public PlayerSaveData CreateSave()
    {
        return new PlayerSaveData
        {
            lastSpawnAnchorId = string.IsNullOrEmpty(LastSpawnAnchorId) ? SpawnManager.CurrentAnchorId : LastSpawnAnchorId,
            level     = Level,
            maxHp     = MaxHp,     currentHp = CurrentHp,
            maxMp     = MaxMp,     currentMp = CurrentMp,
            abilities = new List<AbilityId>(_abilities),
            spells    = new List<SpellId>(_spells),
            flags     = new List<string>(_flags)
        };
    }

    // ===== SINCRONIZACIÓN CON COMPONENTES =====

    public void ApplyToComponents()
    {
        if (_mana) _mana.Init(MaxMp, CurrentMp);

        if (_damageable)
        {
            // Preferimos un setter si existe
            var mSet = _damageable.GetType().GetMethod(
                "SetMaxAndCurrent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(float), typeof(float) }, null);

            if (mSet != null)
                mSet.Invoke(_damageable, new object[] { MaxHp, Mathf.Clamp(CurrentHp, 0f, MaxHp) });
            else
            {
                // Fallback: subir vida si es necesario (para bajar, lo hará gameplay)
                var pCur = _damageable.GetType().GetProperty("Current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var mHeal = _damageable.GetType().GetMethod("Heal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);
                if (pCur != null && pCur.CanRead && mHeal != null)
                {
                    float cur = (float)pCur.GetValue(_damageable, null);
                    float target = Mathf.Clamp(CurrentHp, 0f, MaxHp);
                    float diff = target - cur;
                    if (diff > 0.01f) mHeal.Invoke(_damageable, new object[] { diff });
                }
            }
        }

        OnStatsChanged?.Invoke();
    }

    // ===== API SIMPLE =====

    public void SetSpawnAnchor(string anchorId) { if (!string.IsNullOrEmpty(anchorId)) LastSpawnAnchorId = anchorId; }

    public bool HasAbility(AbilityId a) => _abilities.Contains(a);
    public bool HasSpell(SpellId s)     => _spells.Contains(s);
    public bool HasFlag(string f)       => _flags.Contains(f);

    public void UnlockAbility(AbilityId a) { if (_abilities.Add(a)) OnAbilitiesChanged?.Invoke(); }
    public void UnlockSpell(SpellId s)     { if (_spells.Add(s))    OnSpellsChanged?.Invoke(); }
    public void SetFlag(string f, bool v)
    {
        bool ch = v ? _flags.Add(f) : _flags.Remove(f);
        if (ch) OnFlagsChanged?.Invoke();
    }

    // Setters de stats (por si HUD / cheat / debug)
    public void SetLevel(int v)         { Level = Mathf.Max(1, v); OnStatsChanged?.Invoke(); }
    public void SetMaxHealth(float v)   { MaxHp = Mathf.Max(1f, v); CurrentHp = Mathf.Clamp(CurrentHp, 0f, MaxHp); ApplyToComponents(); }
    public void SetHealth(float v)      { CurrentHp = Mathf.Clamp(v, 0f, MaxHp); ApplyToComponents(); }
    public void SetMaxMana(float v)     { MaxMp = Mathf.Max(0f, v); CurrentMp = Mathf.Clamp(CurrentMp, 0f, MaxMp); ApplyToComponents(); }
    public void SetMana(float v)        { CurrentMp = Mathf.Clamp(v, 0f, MaxMp); if (_mana) _mana.Init(MaxMp, CurrentMp); OnStatsChanged?.Invoke(); }

    // Snapshots para SaveSystem
    public List<AbilityId> GetAbilitiesSnapshot() => new(_abilities);
    public List<SpellId>   GetSpellsSnapshot()    => new(_spells);
    public List<string>    GetFlagsSnapshot()     => new(_flags);
}
