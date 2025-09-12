using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameBootProfile", menuName = "Game/Boot Profile")]
public class GameBootProfile : ScriptableObject
{
    [Header("Arranque")]
    public string sceneToLoad = "MainWorld";
    public string defaultAnchorId = "Bedroom";
    public PlayerPresetSO defaultPlayerPreset;

    [Header("Boot Settings")]
    [Tooltip("Ignora el save y aplica este preset al arrancar")]
    public bool usePresetInsteadOfSave = false;
    public PlayerPresetSO bootPreset;
    public string startAnchorId = "Bedroom";

    [Header("Runtime Fallback (auto-generado al cargar save)")]
    public PlayerPresetSO runtimePreset;

    public bool ShouldBootFromPreset() => usePresetInsteadOfSave && bootPreset != null;

    public string GetStartAnchorOrDefault()
        => string.IsNullOrEmpty(startAnchorId)
            ? (string.IsNullOrEmpty(defaultAnchorId) ? "Bedroom" : defaultAnchorId)
            : startAnchorId;

    // ==== NUEVO: API para runtimePreset =======================================
    public void EnsureRuntimePreset()
    {
        if (!runtimePreset)
        {
            runtimePreset = ScriptableObject.CreateInstance<PlayerPresetSO>();
            runtimePreset.name = "RuntimePlayerPreset";
        }
    }

    public void SetRuntimePresetFromSave(PlayerSaveData data, PlayerPresetSO slotTemplate = null)
    {
        if (data == null) return;

        EnsureRuntimePreset();
        var p = runtimePreset;

        p.level      = data.level;
        p.maxHP      = data.maxHp;     p.currentHP = Mathf.Clamp(data.currentHp, 0f, data.maxHp);
        p.maxMP      = data.maxMp;     p.currentMP = Mathf.Clamp(data.currentMp, 0f, data.maxMp);
        p.unlockedAbilities = new List<AbilityId>(data.abilities ?? new List<AbilityId>());
        p.unlockedSpells    = new List<SpellId>(data.spells    ?? new List<SpellId>());
        p.flags             = new List<string>(data.flags      ?? new List<string>());

        // Slots: si hay plantilla, respétala; si no, usa los primeros del save
        if (slotTemplate)
        {
            p.leftSpellId    = slotTemplate.leftSpellId;
            p.rightSpellId   = slotTemplate.rightSpellId;
            p.specialSpellId = slotTemplate.specialSpellId;
        }
        else
        {
            p.leftSpellId    = p.unlockedSpells.Count > 0 ? p.unlockedSpells[0] : SpellId.None;
            p.rightSpellId   = p.unlockedSpells.Count > 1 ? p.unlockedSpells[1] : SpellId.None;
            p.specialSpellId = p.unlockedSpells.Count > 2 ? p.unlockedSpells[2] : SpellId.None;
        }
    }

    /// <summary>Preset activo: bootPreset (si se fuerza), si no runtimePreset, si no default.</summary>
    public PlayerPresetSO GetActivePresetResolved()
    {
        if (ShouldBootFromPreset() && bootPreset) return bootPreset;
        if (runtimePreset)                        return runtimePreset;
        return defaultPlayerPreset;
    }

    // === Helpers previos (actualizados) =======================================

    public void ApplyBootPreset(PlayerState ps)
    {
        if (!ps || !bootPreset) return;
        // Con PlayerState nuevo, aplica el preset de una vez:
        ps.ApplyPreset(bootPreset, GetStartAnchorOrDefault());
    }

    public PlayerSaveData BuildDefaultSave()
    {
        var d = new PlayerSaveData();
        d.lastSpawnAnchorId = string.IsNullOrEmpty(defaultAnchorId) ? "Bedroom" : defaultAnchorId;
        return d;
    }
}
