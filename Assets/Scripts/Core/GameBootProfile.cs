using System.Collections.Generic;
using UnityEngine;
using System;


[CreateAssetMenu(fileName = "GameBootProfile", menuName = "Game/Boot Profile")]
public class GameBootProfile : ScriptableObject
{
    [Header("Arranque")]
    public string sceneToLoad = "MainWorld";
    public PlayerPresetSO defaultPlayerPreset;

    [Header("Boot Settings")]
    [Tooltip("Ignora el save y aplica este preset al arrancar")]
    public bool usePresetInsteadOfSave; // eliminado '= false' redundante
    public PlayerPresetSO bootPreset;

    [Header("Runtime Fallback (auto-generado al cargar save)")]
    public PlayerPresetSO runtimePreset;

    public bool ShouldBootFromPreset() => usePresetInsteadOfSave && bootPreset != null;

    public string GetStartAnchorOrDefault()
        => GetActivePresetResolved()?.spawnAnchorId ?? "Bedroom";

    // ==== NUEVO: API para runtimePreset =======================================
    public void EnsureRuntimePreset()
    {
        if (!runtimePreset)
        {
            runtimePreset = ScriptableObject.CreateInstance<PlayerPresetSO>();
            runtimePreset.name = "RuntimePlayerPreset";
        }
    }

    private void CopyPreset(PlayerPresetSO src, PlayerPresetSO dst)
    {
        if (!src || !dst) return;
        dst.spawnAnchorId = src.spawnAnchorId;
        dst.level = src.level;
        dst.maxHP = src.maxHP; dst.currentHP = src.currentHP;
        dst.maxMP = src.maxMP; dst.currentMP = src.currentMP;
        dst.unlockedAbilities = new List<AbilityId>(src.unlockedAbilities ?? new List<AbilityId>());
        dst.unlockedSpells    = new List<SpellId>(src.unlockedSpells    ?? new List<SpellId>());
        dst.leftSpellId = src.leftSpellId;
        dst.rightSpellId = src.rightSpellId;
        dst.specialSpellId = src.specialSpellId;
        dst.flags = new List<string>(src.flags ?? new List<string>());
        dst.inventoryItems = new List<InventoryItemSave>(src.inventoryItems ?? new List<InventoryItemSave>());
        dst.defeatedBossIds = new List<string>(src.defeatedBossIds ?? new List<string>());

        // === NUEVO: copiar sección de abilities (permisos físicos/acciones) ===
        if (src.abilities != null)
        {
            dst.abilities = new PlayerAbilities();
            dst.abilities.swim = src.abilities.swim;
            dst.abilities.jump = src.abilities.jump;
            dst.abilities.climb = src.abilities.climb;
        }
        else
        {
            dst.abilities = new PlayerAbilities();
        }
    }

    public void EnsureRuntimePresetFromTemplate(PlayerPresetSO template)
    {
        EnsureRuntimePreset();
        if (template)
        {
            CopyPreset(template, runtimePreset);
        }
    }

    public void SetRuntimePresetFromSave(PlayerSaveData data)
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
        p.inventoryItems    = data.inventory != null ? new List<InventoryItemSave>(data.inventory) : new List<InventoryItemSave>();
        p.defeatedBossIds   = data.defeatedBossIds != null ? new List<string>(data.defeatedBossIds) : new List<string>();
        // Anchor procedente del save
        if (!string.IsNullOrEmpty(data.lastSpawnAnchorId))
            p.spawnAnchorId = data.lastSpawnAnchorId;

        // Slots: si el save trae slots, usarlos (validando); si no, fallback al comportamiento anterior
        var unlocked = p.unlockedSpells ?? new List<SpellId>();
        SpellId Validate(SpellId id) => (id != SpellId.None && unlocked.Contains(id)) ? id : SpellId.None;
        bool hasAnySavedSlot = data.leftSpellId != SpellId.None || data.rightSpellId != SpellId.None || data.specialSpellId != SpellId.None;

        if (hasAnySavedSlot)
        {
            p.leftSpellId    = Validate(data.leftSpellId);
            p.rightSpellId   = Validate(data.rightSpellId);
            p.specialSpellId = Validate(data.specialSpellId);
        }
        else
        {
            // Fallback: mantener compatibilidad con saves antiguos (sin slots); solo asignar izquierdo si hay alguno desbloqueado
            if (unlocked.Count > 0)
            {
                p.leftSpellId = unlocked[0];
            }
            else
            {
                p.leftSpellId = SpellId.None;
            }
            p.rightSpellId = SpellId.None;
            p.specialSpellId = SpellId.None;
        }

        // === NUEVO: restaurar permisos de abilities desde el save (si existen) ===
        if (p.abilities == null) p.abilities = new PlayerAbilities();
        p.abilities.swim = data.canSwim;
        p.abilities.jump = data.canJump;
        p.abilities.climb = data.canClimb;
    }

    /// <summary>Preset activo: siempre runtimePreset (creado desde bootPreset, save o default)</summary>
    public PlayerPresetSO GetActivePresetResolved()
    {
        if (runtimePreset) return runtimePreset;
        if (ShouldBootFromPreset() && bootPreset)
        {
            EnsureRuntimePresetFromTemplate(bootPreset);
            return runtimePreset;
        }
        if (defaultPlayerPreset)
        {
            EnsureRuntimePresetFromTemplate(defaultPlayerPreset);
            return runtimePreset;
        }
        EnsureRuntimePreset();
        return runtimePreset;
    }

    // === Helpers =======================================

    /// <summary>Construye PlayerSaveData a partir del estado actual del profile</summary>
    private PlayerSaveData BuildSaveDataFromProfile()
    {
        var activePreset = GetActivePresetResolved();
        if (!activePreset) return BuildDefaultSave();

        var data = new PlayerSaveData();
        data.lastSpawnAnchorId = SpawnManager.CurrentAnchorId ?? activePreset.spawnAnchorId ?? "Bedroom";
        data.level = activePreset.level;
        data.maxHp = activePreset.maxHP;
        data.currentHp = activePreset.currentHP;
        data.maxMp = activePreset.maxMP;
        data.currentMp = activePreset.currentMP;
        data.abilities = new List<AbilityId>(activePreset.unlockedAbilities ?? new List<AbilityId>());
        data.spells = new List<SpellId>(activePreset.unlockedSpells ?? new List<SpellId>());
        data.flags = new List<string>(activePreset.flags ?? new List<string>());
        data.inventory = activePreset.inventoryItems != null ? new List<InventoryItemSave>(activePreset.inventoryItems) : new List<InventoryItemSave>();
        data.defeatedBossIds = activePreset.defeatedBossIds != null ? new List<string>(activePreset.defeatedBossIds) : new List<string>();
        // Guardar slots actuales
        data.leftSpellId = activePreset.leftSpellId;
        data.rightSpellId = activePreset.rightSpellId;
        data.specialSpellId = activePreset.specialSpellId;

        // === NUEVO: incluir permisos de abilities en el save ===
        if (activePreset.abilities != null)
        {
            data.canSwim = activePreset.abilities.swim;
            data.canJump = activePreset.abilities.jump;
            data.canClimb = activePreset.abilities.climb;
        }

        return data;
    }

    /// <summary>Aplica datos de PlayerSaveData al profile (actualiza runtimePreset)</summary>
    private void ApplySaveDataToProfile(PlayerSaveData data)
    {
        if (data == null) return;
        SetRuntimePresetFromSave(data);
    }

    public PlayerSaveData BuildDefaultSave()
    {
        var d = new PlayerSaveData();
        var preset = defaultPlayerPreset ? defaultPlayerPreset : runtimePreset;
        d.lastSpawnAnchorId = preset && !string.IsNullOrEmpty(preset.spawnAnchorId) ? preset.spawnAnchorId : "Bedroom";
        d.inventory = new List<InventoryItemSave>();
        d.defeatedBossIds = new List<string>();
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

    /// <summary>Actualiza el runtimePreset con los valores actuales de los sistemas del juego y guarda</summary>
    public bool SaveCurrentGameState(SaveSystem saveSystem)
    {
        if (!saveSystem) return false;

        // Actualizar el runtimePreset con el estado actual del juego
        UpdateRuntimePresetFromCurrentState();

        // Guardar el profile actualizado
        return SaveProfile(saveSystem);
    }

    /// <summary>Actualiza runtimePreset con los datos actuales del juego (PlayerHealthSystem, etc.)</summary>
    private void UpdateRuntimePresetFromCurrentState()
    {
        EnsureRuntimePreset();
        var p = runtimePreset;

        // Actualizar anchor actual en el runtime preset
        var currentAnchor = SpawnManager.CurrentAnchorId;
        if (!string.IsNullOrEmpty(currentAnchor))
        {
            p.spawnAnchorId = currentAnchor;
        }

        // Obtener datos del PlayerHealthSystem si existe
        var playerHealthSystem = FindFirstObjectByType<PlayerHealthSystem>();
        if (playerHealthSystem != null)
        {
            p.maxHP = playerHealthSystem.MaxHealth;
            p.currentHP = playerHealthSystem.CurrentHealth;
        }

        // Obtener datos del sistema de maná si existe
        var manaPool = FindFirstObjectByType<ManaPool>();
        if (manaPool != null)
        {
            p.maxMP = manaPool.Max;
            p.currentMP = manaPool.Current;
        }
        
        // === NUEVO: sincronizar flags de quests desde QuestManager =================
        var qm = QuestManager.Instance;
        if (qm != null)
        {
            // Construir lista nueva con flags no-quest actuales + estado de quests vivo
            var newFlags = new List<string>(p.flags?.Count ?? 0);

            // Conserva flags antiguos que NO sean de quests (no empiezan por "QUEST_")
            if (p.flags != null)
            {
                for (int i = 0; i < p.flags.Count; i++)
                {
                    var f = p.flags[i];
                    if (string.IsNullOrEmpty(f) || f.StartsWith("QUEST_", StringComparison.Ordinal)) continue;
                    newFlags.Add(f);
                }
            }

            // Añadir flags exportados por el QuestManager (active/completed/steps)
            qm.ExportFlags(newFlags);

            p.flags = newFlags;
        }

        // === NUEVO: sincronizar abilities desde el PlayerActionManager (estado runtime actual) ===
         var actionManager = FindFirstObjectByType<PlayerActionManager>();
         if (actionManager != null)
         {
            if (p.abilities == null) p.abilities = new PlayerAbilities();
            p.abilities.swim = actionManager.AllowSwim;
            p.abilities.jump = actionManager.AllowJump;
            p.abilities.climb = actionManager.AllowClimb;
         }

        // Nota: Los demás datos (level, abilities, spells, flags) se mantienen del preset actual
        if (PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true))
        {
            p.inventoryItems = inventory.GetSaveSnapshot();
        }
        else
        {
            p.inventoryItems = new List<InventoryItemSave>();
        }

        if (BossProgressTracker.TryGetInstance(out var bossTracker))
        {
            p.defeatedBossIds = bossTracker.GetSnapshot();
        }
        else
        {
            p.defeatedBossIds = new List<string>();
        }

        Debug.Log($"[GameBootProfile] RuntimePreset actualizado - Anchor: {p.spawnAnchorId}, HP: {p.currentHP}/{p.maxHP}, MP: {p.currentMP}/{p.maxMP}");
    }

    // === NUEVO: Flujo de "Nueva partida" ===============================

    /// <summary>
    /// Elimina el save (si se pasa) y restablece el runtimePreset al preset por defecto.
    /// Evita arrastrar datos de partidas anteriores cuando el GameBootService persiste.
    /// </summary>
    public void NewGameReset(SaveSystem saveSystem = null)
    {
        if (saveSystem) saveSystem.Delete();

        if (defaultPlayerPreset)
        {
            EnsureRuntimePresetFromTemplate(defaultPlayerPreset);
        }
        else
        {
            EnsureRuntimePreset();
            ResetPresetToEmpty(runtimePreset);
        }

        // Resetear misiones al iniciar nueva partida
        if (QuestManager.Instance != null)
            QuestManager.Instance.ResetAllQuests();

        Debug.Log("[GameBootProfile] Reset realizado para Nueva Partida (runtimePreset -> default)");
    }

    private void ResetPresetToEmpty(PlayerPresetSO p)
    {
        if (!p) return;
        p.spawnAnchorId = "Bedroom";
        p.level = 1;
        p.maxHP = 100f; p.currentHP = 100f;
        p.maxMP = 50f;  p.currentMP = 50f;
        p.unlockedAbilities = new List<AbilityId>();
        p.unlockedSpells = new List<SpellId>();
        p.leftSpellId = SpellId.None;
        p.rightSpellId = SpellId.None;
        p.specialSpellId = SpellId.None;
        p.flags = new List<string>();
        p.inventoryItems = new List<InventoryItemSave>();
        p.defeatedBossIds = new List<string>();
        // === NUEVO: resetear abilities ===
        p.abilities = new PlayerAbilities();
    }
}
