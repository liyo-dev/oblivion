using System;
using System.Collections.Generic;

[Serializable]
public class QuestProgressEntry
{
    public string questId;
    public int stage;       // 0..N
    public bool completed;
}

[Serializable]
public class PlayerSaveData
{
    // Stats
    public int level;
    public float maxHP, currentHP, maxMP, currentMP;

    // Progresos
    public List<AbilityId> unlockedAbilities = new();
    public List<SpellId>   unlockedSpells    = new();
    public List<string>    flags             = new();

    // Misiones
    public List<QuestProgressEntry> quests = new();

    // Dónde reaparecer
    public string lastSpawnAnchorId;
    
    public static PlayerSaveData FromPreset(PlayerPresetSO p, string spawnId)
    {
        return new PlayerSaveData {
            level=p.level, maxHP=p.maxHP, currentHP=p.currentHP, maxMP=p.maxMP, currentMP=p.currentMP,
            unlockedAbilities = new List<AbilityId>(p.unlockedAbilities),
            unlockedSpells    = new List<SpellId>(p.unlockedSpells),
            flags             = new List<string>(p.flags),
            lastSpawnAnchorId = spawnId
        };
    }
}