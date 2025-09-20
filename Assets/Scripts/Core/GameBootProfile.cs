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

    // === Helpers =======================================

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

    // === NUEVO: Métodos para guardar/cargar el profile completo ===
    
    /// <summary>Guarda el estado actual del profile en el SaveSystem</summary>
    public bool SaveProfile(SaveSystem saveSystem)
    {
        if (!saveSystem) return false;
        
        var data = BuildSaveDataFromProfile();
        return saveSystem.Save(data);
    }
    
    /// <summary>Carga datos del SaveSystem y los aplica al profile</summary>
    public bool LoadProfile(SaveSystem saveSystem)
    {
        if (!saveSystem || !saveSystem.HasSave()) return false;
        
        if (saveSystem.Load(out var data))
        {
            ApplySaveDataToProfile(data);
            return true;
        }
        return false;
    }
    
    /// <summary>Actualiza el profile con el estado actual del PlayerState y guarda</summary>
    public bool SaveCurrentGameState(SaveSystem saveSystem, PlayerState playerState)
    {
        if (!saveSystem || !playerState) return false;
        
        // Actualizar el runtimePreset con el estado actual del jugador
        UpdateRuntimePresetFromPlayerState(playerState);
        
        // Guardar el profile actualizado
        return SaveProfile(saveSystem);
    }
    
    /// <summary>Construye PlayerSaveData a partir del estado actual del profile</summary>
    private PlayerSaveData BuildSaveDataFromProfile()
    {
        var activePreset = GetActivePresetResolved();
        if (!activePreset) return BuildDefaultSave();
        
        var data = new PlayerSaveData();
        data.lastSpawnAnchorId = SpawnManager.CurrentAnchorId ?? defaultAnchorId;
        data.level = activePreset.level;
        data.maxHp = activePreset.maxHP;
        data.currentHp = activePreset.currentHP;
        data.maxMp = activePreset.maxMP;
        data.currentMp = activePreset.currentMP;
        data.abilities = new List<AbilityId>(activePreset.unlockedAbilities ?? new List<AbilityId>());
        data.spells = new List<SpellId>(activePreset.unlockedSpells ?? new List<SpellId>());
        data.flags = new List<string>(activePreset.flags ?? new List<string>());
        
        return data;
    }
    
    /// <summary>Aplica datos de PlayerSaveData al profile (actualiza runtimePreset)</summary>
    private void ApplySaveDataToProfile(PlayerSaveData data)
    {
        if (data == null) return;
        
        // Actualizar el anchorId por defecto si es necesario
        if (!string.IsNullOrEmpty(data.lastSpawnAnchorId))
        {
            defaultAnchorId = data.lastSpawnAnchorId;
        }
        
        // Crear/actualizar runtimePreset con los datos cargados
        SetRuntimePresetFromSave(data, defaultPlayerPreset);
    }
    
    /// <summary>Actualiza runtimePreset con los datos actuales del PlayerState</summary>
    private void UpdateRuntimePresetFromPlayerState(PlayerState playerState)
    {
        EnsureRuntimePreset();
        var p = runtimePreset;
        
        // Actualizar posición actual
        defaultAnchorId = SpawnManager.CurrentAnchorId ?? defaultAnchorId;
        
        // Actualizar datos del jugador desde PlayerState
        p.level = playerState.Level;
        p.maxHP = playerState.MaxHp;
        p.currentHP = playerState.CurrentHp;
        p.maxMP = playerState.MaxMp;
        p.currentMP = playerState.CurrentMp;
        p.unlockedAbilities = new List<AbilityId>(playerState.GetAbilitiesSnapshot() ?? new List<AbilityId>());
        p.unlockedSpells = new List<SpellId>(playerState.GetSpellsSnapshot() ?? new List<SpellId>());
        p.flags = new List<string>(playerState.GetFlagsSnapshot() ?? new List<string>());
        
        // Mantener configuración de slots actuales si existen
        // (o podrías obtenerlos también del PlayerState si tienes esa info)
    }
}
