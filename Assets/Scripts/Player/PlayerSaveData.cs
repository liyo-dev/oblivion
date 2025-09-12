using System;
using System.Collections.Generic;

[Serializable]
public class PlayerSaveData
{
    public string lastSpawnAnchorId;

    public int level;
    public float maxHp, currentHp;
    public float maxMp, currentMp;

    public List<AbilityId> abilities = new();
    public List<SpellId>   spells    = new();
    public List<string>    flags     = new(); // misiones/estados simples

    // ---- Helpers ----
    public static PlayerSaveData From(PlayerState ps)
    {
        var d = new PlayerSaveData();
        d.lastSpawnAnchorId = SpawnManager.CurrentAnchorId ?? "Bedroom";

        d.level = ps.Level;
        d.maxHp = ps.MaxHp;  d.currentHp = ps.CurrentHp;
        d.maxMp = ps.MaxMp;  d.currentMp = ps.CurrentMp;

        d.abilities = ps.GetAbilitiesSnapshot();
        d.spells    = ps.GetSpellsSnapshot();
        d.flags     = ps.GetFlagsSnapshot();
        return d;
    }

    public void ApplyTo(PlayerState ps)
    {
        // Con PlayerState simplificado usamos su método de carga directo:
        ps.LoadFromSave(this);
    }
}