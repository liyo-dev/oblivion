using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerSaveData
{
    public string lastSpawnAnchorId;

    public int level;
    public float maxHp, currentHp;
    public float maxMp, currentMp;

    // Hechizos/skills
    public List<AbilityId> abilities = new();
    public List<SpellId>   spells    = new();
    public List<string>    flags     = new(); // misiones/estados simples

    // Slots guardados (opcional: si faltan en saves antiguos, quedarán en None por defecto)
    public SpellId leftSpellId  = SpellId.None;
    public SpellId rightSpellId = SpellId.None;
    public SpellId specialSpellId = SpellId.None;

    // ---- Helpers actualizados para usar GameBootProfile ----
    
    /// <summary>
    /// Crea PlayerSaveData a partir del GameBootProfile actual (sincrónico).
    /// Si el profile aún no está disponible, devuelve un objeto vacío y loguea error.
    /// Para esperar al evento, usa FromGameBootProfileWhenReady.
    /// </summary>
    public static PlayerSaveData FromGameBootProfile()
    {
        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
            Debug.LogError("[PlayerSaveData] GameBootService.Profile es null (¿se llamó antes de OnProfileReady?)");
            return new PlayerSaveData();
        }

        var preset = bootProfile.GetActivePresetResolved();
        if (preset == null)
        {
            Debug.LogError("[PlayerSaveData] No hay preset activo en GameBootProfile");
            return new PlayerSaveData();
        }

        var d = new PlayerSaveData();
        var activeAnchor = preset.spawnAnchorId;
        d.lastSpawnAnchorId = SpawnManager.CurrentAnchorId ?? activeAnchor ?? "Bedroom";

        // Obtener datos desde el preset activo
        d.level = preset.level;
        d.maxHp = preset.maxHP;
        d.currentHp = preset.currentHP;
        d.maxMp = preset.maxMP;
        d.currentMp = preset.currentMP;

        d.abilities = new List<AbilityId>(preset.unlockedAbilities ?? new List<AbilityId>());
        d.spells = new List<SpellId>(preset.unlockedSpells ?? new List<SpellId>());
        d.flags = new List<string>(preset.flags ?? new List<string>());

        // Slots
        d.leftSpellId = preset.leftSpellId;
        d.rightSpellId = preset.rightSpellId;
        d.specialSpellId = preset.specialSpellId;

        return d;
    }

    /// <summary>
    /// Variante event-driven: espera a GameBootService.OnProfileReady (si hace falta) y devuelve el resultado por callback.
    /// Si el profile ya está disponible, llama al callback inmediatamente.
    /// </summary>
    public static void FromGameBootProfileWhenReady(Action<PlayerSaveData> onReady)
    {
        if (GameBootService.IsAvailable)
        {
            onReady?.Invoke(FromGameBootProfile());
            return;
        }

        void Handler()
        {
            GameBootService.OnProfileReady -= Handler;
            onReady?.Invoke(FromGameBootProfile());
        }

        GameBootService.OnProfileReady += Handler;
    }

    /// <summary>
    /// Aplica estos datos al GameBootProfile (actualiza runtimePreset) de forma sincrónica.
    /// Para esperar a OnProfileReady, usa ApplyToGameBootProfileWhenReady.
    /// </summary>
    public void ApplyToGameBootProfile()
    {
        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
            Debug.LogError("[PlayerSaveData] GameBootService.Profile es null (¿se llamó antes de OnProfileReady?)");
            return;
        }

        // Usar el método existente del GameBootProfile para aplicar los datos
        bootProfile.SetRuntimePresetFromSave(this);
        
        Debug.Log($"[PlayerSaveData] Datos aplicados al GameBootProfile - Level: {level}, HP: {currentHp}/{maxHp}");
    }

    /// <summary>
    /// Variante event-driven: espera a GameBootService.OnProfileReady (si hace falta) y luego aplica los datos.
    /// onApplied(true) si se aplicó, false si no.
    /// </summary>
    public void ApplyToGameBootProfileWhenReady(Action<bool> onApplied = null)
    {
        if (GameBootService.IsAvailable)
        {
            ApplyToGameBootProfile();
            onApplied?.Invoke(true);
            return;
        }

        void Handler()
        {
            GameBootService.OnProfileReady -= Handler;
            if (GameBootService.Profile == null)
            {
                onApplied?.Invoke(false);
                return;
            }
            ApplyToGameBootProfile();
            onApplied?.Invoke(true);
        }

        GameBootService.OnProfileReady += Handler;
    }
}