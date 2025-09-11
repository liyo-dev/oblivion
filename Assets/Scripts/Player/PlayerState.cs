using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerState : MonoBehaviour
{
    [Header("Inicio")]
    public PlayerPresetSO newGamePreset;

    [Header("Refs opcionales")]
    public Damageable damageable; // si lo tienes
    public ManaPool  mana;        // si lo tienes

    // Runtime
    public int   Level     { get; private set; }
    public float MaxHp     { get; private set; }
    public float CurrentHp { get; private set; }
    public float MaxMp     { get; private set; }
    public float CurrentMp { get; private set; }

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

    // === Carga desde SAVE ===
    public void LoadFromSave(PlayerSaveData d)
    {
        SetLevel(d.level);
        SetMaxHealth(d.maxHp);
        SetHealth(d.currentHp);
        SetMaxMana(d.maxMp);
        SetMana(d.currentMp);

        _abilities = new HashSet<AbilityId>(d.abilities ?? new List<AbilityId>());
        _spells    = new HashSet<SpellId>(d.spells    ?? new List<SpellId>());
        _flags     = new HashSet<string> (d.flags     ?? new List<string>());

        ApplyToComponents();
    }

    public PlayerSaveData CreateSave(string spawnAnchorId, QuestLog questLog)
    {
        return new PlayerSaveData {
            level = Level, maxHp = MaxHp, currentHp = CurrentHp, maxMp = MaxMp, currentMp = CurrentMp,
            abilities = new List<AbilityId>(_abilities),
            spells    = new List<SpellId>(_spells),
            flags     = new List<string>(_flags),
            lastSpawnAnchorId = spawnAnchorId
        };
    }

    // === Sincroniza con componentes (Damageable/ManaPool/HUD) ===
    public void ApplyToComponents()
    {
        // ManaPool: establecemos valores exactos
        if (mana) mana.Init(MaxMp, CurrentMp);

        // Damageable: intentamos sincronizar de forma segura
        if (damageable)
            TrySyncDamageable(damageable, MaxHp, CurrentHp);

        // Notificar cambios a quien escuche (HUD, etc.)
        OnAbilitiesChanged?.Invoke();
        OnSpellsChanged?.Invoke();
        OnFlagsChanged?.Invoke();
    }

    // Intenta fijar max y current en Damageable sin romper tu implementación
    void TrySyncDamageable(Damageable dmg, float max, float current)
    {
        // 1) Si existe SetMaxAndCurrent(max, current), úsalo
        var mSet = dmg.GetType().GetMethod("SetMaxAndCurrent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float), typeof(float) }, null);
        if (mSet != null)
        {
            mSet.Invoke(dmg, new object[] { max, Mathf.Clamp(current, 0f, max) });
            return;
        }

        // 2) Si existe campo maxHealth, intenta fijarlo
        var fMax = dmg.GetType().GetField("maxHealth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fMax != null)
        {
            fMax.SetValue(dmg, max);
        }

        // 3) Si existe propiedad Current (solo lectura), aproximamos con Heal() si existe
        var pCur = dmg.GetType().GetProperty("Current", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var mHeal = dmg.GetType().GetMethod("Heal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(float) }, null);

        if (pCur != null && pCur.CanRead && mHeal != null)
        {
            float curValue = (float)pCur.GetValue(dmg, null);
            float target   = Mathf.Clamp(current, 0f, max);
            float diff     = target - curValue;
            if (diff > 0.01f) mHeal.Invoke(dmg, new object[] { diff });
            // Si diff < 0 no aplicamos daño desde aquí (lo gestiona el combate).
        }
#if UNITY_EDITOR
        else
        {
            Debug.Log($"[PlayerState] Damageable no expone setters compatibles. Se ha sincronizado mana y eventos, pero la vida en Damageable puede no reflejarse hasta recibir daño/curación in-game.");
        }
#endif
    }

    // === API de consulta ===
    public bool HasAbility(AbilityId a) => _abilities.Contains(a);
    public bool HasSpell(SpellId s)     => _spells.Contains(s);
    public bool HasFlag(string f)       => _flags.Contains(f);

    // === API de modificación ===
    public void UnlockAbility(AbilityId a){ if (_abilities.Add(a)) OnAbilitiesChanged?.Invoke(); }
    public void UnlockSpell(SpellId s)   { if (_spells.Add(s))    OnSpellsChanged?.Invoke(); }
    public void SetFlag(string f, bool v)
    {
        bool ch = v ? _flags.Add(f) : _flags.Remove(f);
        if (ch) OnFlagsChanged?.Invoke();
    }

    // === Setters (ahora propagan a componentes) ===
    public void SetLevel(int level)          { Level   = level; }
    public void SetMaxHealth(float maxHp)    { MaxHp   = maxHp;  ApplyToComponents(); }
    public void SetHealth(float hp)          { CurrentHp = Mathf.Clamp(hp, 0, MaxHp); ApplyToComponents(); }
    public void SetMaxMana(float maxMp)      { MaxMp   = maxMp;  ApplyToComponents(); }
    public void SetMana(float mp)            { CurrentMp = Mathf.Clamp(mp, 0, MaxMp); ApplyToComponents(); }

    // Propiedades para compatibilidad con SavePoint
    public float MaxHealth => MaxHp;
    public float MaxMana   => MaxMp;

    // Snapshots para guardado
    public List<AbilityId> GetAbilitiesSnapshot() => new List<AbilityId>(_abilities);
    public List<SpellId>   GetSpellsSnapshot()    => new List<SpellId>(_spells);
    public List<string>    GetFlagsSnapshot()     => new List<string>(_flags);

    // Carga desde listas (save)
    public void LoadAbilities(List<AbilityId> abilities)
    { 
        _abilities = new HashSet<AbilityId>(abilities ?? new List<AbilityId>());
        OnAbilitiesChanged?.Invoke();
    }

    public void LoadSpells(List<SpellId> spells)
    { 
        _spells = new HashSet<SpellId>(spells ?? new List<SpellId>());
        OnSpellsChanged?.Invoke();
    }

    public void LoadFlags(List<string> flags)
    { 
        _flags = new HashSet<string>(flags ?? new List<string>());
        OnFlagsChanged?.Invoke();
    }

    // Aplicar preset al jugador
    public void ApplyPreset(PlayerPresetSO preset)
    {
        if (!preset) return;

        Debug.Log($"Aplicando preset: {preset.name}");

        SetLevel(preset.level);
        SetMaxHealth(preset.maxHP);
        SetHealth(preset.currentHP);
        SetMaxMana(preset.maxMP);
        SetMana(preset.currentMP);

        _abilities.Clear();
        if (preset.unlockedAbilities != null)
            foreach (var ability in preset.unlockedAbilities) _abilities.Add(ability);

        _spells.Clear();
        if (preset.unlockedSpells != null)
            foreach (var spell in preset.unlockedSpells) _spells.Add(spell);

        _flags.Clear();
        if (preset.flags != null)
            foreach (var flag in preset.flags) _flags.Add(flag);

        ApplyToComponents();
    }

    // Helper combinado (sigue existiendo)
    public void SetHpMp(float hp, float mp)
    {
        CurrentHp = Mathf.Clamp(hp, 0, MaxHp);
        CurrentMp = Mathf.Clamp(mp, 0, MaxMp);
        ApplyToComponents();
    }
}
