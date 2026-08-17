using System;
using System.Collections.Generic;
using System.Linq;
using Game.NPC;
using UnityEngine;

public enum SaveRequestContext
{
    Manual,
    Auto
}

[CreateAssetMenu(fileName = "GameBootProfile", menuName = "El Sendero/Core/Boot Profile")]
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

    [Header("Save Options")]
    [Tooltip("Permite auto-guardados fuera de los puntos de guardado manuales.")]
    public bool allowAutoSaves = false;

    public bool ShouldBootFromPreset() => usePresetInsteadOfSave && bootPreset != null;

    public string GetStartAnchorOrDefault()
        => GetActivePresetResolved()?.spawnAnchorId ?? "Bedroom";

    // Snapshot narrativo eliminado (no se usa)

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
        dst.appearance = new List<AppearanceEntry>(src.appearance ?? new List<AppearanceEntry>());
        dst.unlockedWardrobeIds = new List<string>(src.unlockedWardrobeIds ?? new List<string>());
        dst.inventoryItems = new List<InventoryItemSave>(src.inventoryItems ?? new List<InventoryItemSave>());
        dst.defeatedBossIds = new List<string>(src.defeatedBossIds ?? new List<string>());
        dst.consumedInteractableIds = new List<string>(src.consumedInteractableIds ?? new List<string>());
        dst.completedInteractiveNarratives = new List<string>(src.completedInteractiveNarratives ?? new List<string>());
        dst.seenLorePopupIds = new List<string>(src.seenLorePopupIds ?? new List<string>());
        dst.partyMemberIds = new List<string>(src.partyMemberIds ?? new List<string>());
        dst.activeCharacterSlot = src.activeCharacterSlot;
        dst.unlockedTeleportPoints = new List<string>(src.unlockedTeleportPoints ?? new List<string>());
        
        // === CRÍTICO: Copiar narrativeBlackboards para que el grafo continúe desde la posición guardada ===
        if (src.narrativeBlackboards != null && src.narrativeBlackboards.Count > 0)
        {
            dst.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
            foreach (var bb in src.narrativeBlackboards)
            {
                var copy = new PlayerSaveData.NarrativeBlackboardSnapshot
                {
                    graphLabel = bb.graphLabel,
                    blackboardData = new List<SimpleBlackboard.Entry>()
                };
                if (bb.blackboardData != null)
                {
                    foreach (var entry in bb.blackboardData)
                    {
                        copy.blackboardData.Add(new SimpleBlackboard.Entry
                        {
                            key = entry.key,
                            value = entry.value,
                            type = entry.type
                        });
                    }
                }
                dst.narrativeBlackboards.Add(copy);
            }
        }
        else
        {
            dst.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        }
        
        // === Copiar posiciones de NPCs ===
        if (src.npcPositions != null && src.npcPositions.Count > 0)
        {
            dst.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
            foreach (var npc in src.npcPositions)
            {
                dst.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
                {
                    npcId = npc.npcId,
                    position = npc.position,
                    rotation = npc.rotation,
                    hasRotation = npc.hasRotation,
                    hasActiveState = npc.hasActiveState,
                    isActive = npc.isActive
                });
            }
        }
        else
        {
            dst.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        }

        // === Copiar vínculos sociales dinámicos entre NPCs (snapshot, ver NPCRelationshipRegistry) ===
        dst.npcRelationships = new List<NPCRelationshipRegistry.SaveEntry>(src.npcRelationships ?? new List<NPCRelationshipRegistry.SaveEntry>());

        if (src.abilities != null)
        {
            dst.abilities = new PlayerAbilities();
            dst.abilities.swim = src.abilities.swim;
            dst.abilities.jump = src.abilities.jump;
            dst.abilities.climb = src.abilities.climb;
            dst.abilities.magic = src.abilities.magic;
            dst.abilities.fly = src.abilities.fly;
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

    private List<InventoryItemSave> SanitizeInventorySnapshot(IEnumerable<InventoryItemSave> source)
    {
        if (source == null) return new List<InventoryItemSave>();

        var merged = new Dictionary<string, int>();
        foreach (var entry in source)
        {
            if (string.IsNullOrWhiteSpace(entry.itemId)) continue;
            if (entry.count <= 0) continue;

            merged.TryGetValue(entry.itemId, out var current);
            merged[entry.itemId] = current + entry.count;
        }

        var snapshot = new List<InventoryItemSave>(merged.Count);
        foreach (var kvp in merged)
        {
            snapshot.Add(new InventoryItemSave
            {
                itemId = kvp.Key,
                count = kvp.Value
            });
        }

        return snapshot;
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
        p.appearance        = data.appearance != null ? new List<AppearanceEntry>(data.appearance) : new List<AppearanceEntry>();
        p.unlockedWardrobeIds = data.unlockedWardrobeIds != null ? new List<string>(data.unlockedWardrobeIds) : new List<string>();
        p.inventoryItems    = SanitizeInventorySnapshot(data.inventory);
        p.defeatedBossIds   = data.defeatedBossIds != null ? new List<string>(data.defeatedBossIds) : new List<string>();
        p.narrativeBlackboards = data.narrativeBlackboards != null ? new List<PlayerSaveData.NarrativeBlackboardSnapshot>(data.narrativeBlackboards) : new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        p.consumedInteractableIds = data.consumedInteractables != null ? new List<string>(data.consumedInteractables) : new List<string>();
        p.seenLorePopupIds = data.seenLorePopupIds != null ? new List<string>(data.seenLorePopupIds) : new List<string>();

        // === CRÍTICO: Restaurar narrativas interactivas completadas desde el save ===
        // Esto permite que al cargar una partida anterior, las narrativas de un solo uso
        // que NO estaban completadas en ese save vuelvan a estar disponibles
        p.completedInteractiveNarratives = data.completedInteractiveNarratives != null 
            ? new List<string>(data.completedInteractiveNarratives) 
            : new List<string>();
        Debug.Log($"[GameBootProfile] 📜 Restauradas {p.completedInteractiveNarratives.Count} narrativas completadas desde save");

        // === NUEVO: Restaurar miembros del equipo (party) ===
        p.partyMemberIds = data.partyMemberIds != null 
            ? new List<string>(data.partyMemberIds) 
            : new List<string>();
        p.activeCharacterSlot = data.activeCharacterSlot;
        Debug.Log($"[GameBootProfile] 🤝 Party: {p.partyMemberIds.Count} miembros a restaurar");

        // Restaurar NPCs persistidos directamente en el runtimePreset para que otros sistemas puedan aplicarlos
        if (p.npcPositions == null) p.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        else p.npcPositions.Clear();

        if (data.npcPositions != null && data.npcPositions.Count > 0)
        {
            for (int i = 0; i < data.npcPositions.Count; i++)
            {
                var e = data.npcPositions[i];
                p.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
                {
                    npcId = e.npcId,
                    position = e.position,
                    rotation = e.rotation,
                    hasRotation = e.hasRotation,
                    hasActiveState = e.hasActiveState,
                    isActive = e.isActive
                });
            }
        }

        // === NUEVO: restaurar vínculos sociales dinámicos entre NPCs ===
        // Mismo patrón que TeleportRegistry.LoadFromSaveData: el preset guarda el snapshot,
        // pero la fuente de verdad en runtime es el registro estático — hay que recargarlo
        // explícitamente para que WanderState/IdleState lo usen a partir de ahora.
        p.npcRelationships = data.npcRelationships != null
            ? new List<NPCRelationshipRegistry.SaveEntry>(data.npcRelationships)
            : new List<NPCRelationshipRegistry.SaveEntry>();
        NPCRelationshipRegistry.LoadFromSaveEntries(p.npcRelationships);

        // Anchor procedente del save
        if (!string.IsNullOrEmpty(data.lastSpawnAnchorId))
        {
            p.spawnAnchorId = data.lastSpawnAnchorId;
            Debug.Log($"[GameBootProfile] 📍 Anchor establecido desde save: '{data.lastSpawnAnchorId}'");
        }
        else
        {
            Debug.LogWarning($"[GameBootProfile] ⚠️ Save no tiene lastSpawnAnchorId - anchor quedará como estaba: '{p.spawnAnchorId}'");
        }

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
        p.abilities.fly = data.canFly;
        p.abilities.magic = data.canMagic;

        // === NUEVO: restaurar puntos de teletransporte desbloqueados ===
        if (data.unlockedTeleportPoints != null && data.unlockedTeleportPoints.Count > 0)
        {
            // Actualizar preset
            p.unlockedTeleportPoints = new List<string>(data.unlockedTeleportPoints);
            // Sincronizar con TeleportRegistry
            TeleportRegistry.LoadFromSaveData(data.unlockedTeleportPoints);
            Debug.Log($"[GameBootProfile] 🚀 Teleport points restaurados: {data.unlockedTeleportPoints.Count}");
        }
        else
        {
            // Limpiar si el save no tiene puntos
            p.unlockedTeleportPoints = new List<string>();
        }
        // NO limpiar TeleportRegistry si ya hay puntos cargados - evita borrar puntos al re-llamar esta función
        // TeleportRegistry.Clear() solo se llama en NewGameReset()
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
        data.appearance = activePreset.appearance != null ? new List<AppearanceEntry>(activePreset.appearance) : new List<AppearanceEntry>();
        data.unlockedWardrobeIds = activePreset.unlockedWardrobeIds != null ? new List<string>(activePreset.unlockedWardrobeIds) : new List<string>();
        data.consumedInteractables = activePreset.consumedInteractableIds != null
            ? new List<string>(activePreset.consumedInteractableIds)
            : new List<string>();
        data.seenLorePopupIds = activePreset.seenLorePopupIds != null
            ? new List<string>(activePreset.seenLorePopupIds)
            : new List<string>();
        data.inventory = SanitizeInventorySnapshot(activePreset.inventoryItems);
        data.defeatedBossIds = activePreset.defeatedBossIds != null ? new List<string>(activePreset.defeatedBossIds) : new List<string>();
        
        // ✅ CRÍTICO: incluir narrativas interactivas completadas
        data.completedInteractiveNarratives = activePreset.completedInteractiveNarratives != null 
            ? new List<string>(activePreset.completedInteractiveNarratives) 
            : new List<string>();
        
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
            data.canFly = activePreset.abilities.fly;
            data.canMagic = activePreset.abilities.magic;
        }

        // === NUEVO: incluir posiciones de NPCs ===
        if (activePreset.npcPositions != null && activePreset.npcPositions.Count > 0)
        {
            data.npcPositions = new List<PlayerSaveData.NpcPosEntry>(activePreset.npcPositions.Count);
            for (int i = 0; i < activePreset.npcPositions.Count; i++)
            {
                var e = activePreset.npcPositions[i];
                data.npcPositions.Add(new PlayerSaveData.NpcPosEntry
                {
                    npcId = e.npcId,
                    position = e.position,
                    rotation = e.rotation,
                    hasRotation = e.hasRotation,
                    hasActiveState = e.hasActiveState,
                    isActive = e.isActive
                });
            }
        }

        // === NUEVO: incluir blackboards narrativos ===
        data.narrativeBlackboards = activePreset.narrativeBlackboards != null 
            ? new List<PlayerSaveData.NarrativeBlackboardSnapshot>(activePreset.narrativeBlackboards) 
            : new List<PlayerSaveData.NarrativeBlackboardSnapshot>();

        // === NUEVO: incluir miembros del equipo (party) ===
        data.partyMemberIds = activePreset.partyMemberIds != null
            ? new List<string>(activePreset.partyMemberIds)
            : new List<string>();
        data.activeCharacterSlot = activePreset.activeCharacterSlot;

        // === NUEVO: incluir puntos de teletransporte desbloqueados ===
        data.unlockedTeleportPoints = activePreset.unlockedTeleportPoints != null
            ? new List<string>(activePreset.unlockedTeleportPoints)
            : new List<string>();

        // === NUEVO: incluir vínculos sociales dinámicos entre NPCs ===
        // A diferencia de otros campos, la fuente de verdad en runtime es el registro estático
        // (NPCRelationshipRegistry), no el preset — se lee directamente de ahí, no de activePreset.
        data.npcRelationships = NPCRelationshipRegistry.ToSaveEntries();

        return data;
    }

    /// <summary>Aplica datos de PlayerSaveData al profile (actualiza runtimePreset)</summary>
    private void ApplySaveDataToProfile(PlayerSaveData data)
    {
        if (data == null) return;
        SetRuntimePresetFromSave(data);

        var preset = GetActivePresetResolved();

        // Aplicar posiciones de NPCs desde el save al preset
        if (preset != null)
        {
            if (preset.npcPositions == null) preset.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
            else preset.npcPositions.Clear();

            if (data.npcPositions != null && data.npcPositions.Count > 0)
            {
                for (int i = 0; i < data.npcPositions.Count; i++)
                {
                    var e = data.npcPositions[i];
                    preset.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
                    {
                        npcId = e.npcId,
                        position = e.position,
                        rotation = e.rotation,
                        hasRotation = e.hasRotation,
                        hasActiveState = e.hasActiveState,
                        isActive = e.isActive
                    });
                }
            }
        }

        if (!string.IsNullOrEmpty(preset?.spawnAnchorId))
        {
            SpawnManager.SetCurrentAnchor(preset.spawnAnchorId);
        }

        if (BossProgressTracker.TryGetInstance(out var tracker))
        {
            tracker.LoadFromSnapshot(preset?.defeatedBossIds);
        }

        var questManager = QuestManager.Instance;
        questManager?.RestoreFromProfileFlags(preset?.flags);

        // Aplicar posiciones de NPCs en la escena tras cargar el perfil
        ApplyNpcPositionsToScene(preset);
    }

    public PlayerSaveData BuildDefaultSave()
    {
        var d = new PlayerSaveData();
        var preset = defaultPlayerPreset ? defaultPlayerPreset : runtimePreset;
        d.lastSpawnAnchorId = preset && !string.IsNullOrEmpty(preset.spawnAnchorId) ? preset.spawnAnchorId : "Bedroom";
        d.inventory = new List<InventoryItemSave>();
        d.defeatedBossIds = new List<string>();
        d.appearance = new List<AppearanceEntry>();
        return d;
    }

    // === NUEVO: Mtodos para guardar/cargar el profile completo ===

    /// <summary>Guarda el estado actual del profile en el SaveSystem</summary>
    public bool SaveProfile(SaveSystem saveSystem, SaveRequestContext context = SaveRequestContext.Manual)
    {
        if (!saveSystem)
        {
            GameBootProfileDebugger.Log("SaveProfile", "? SaveSystem no disponible", LogType.Error);
            return false;
        }

        var data = BuildSaveDataFromProfile();
        bool success = saveSystem.Save(data, context);
        
        if (success)
        {
            GameBootProfileDebugger.Log("SaveProfile", $"? Guardado exitoso (context: {context})", LogType.Log);
        }
        else
        {
            GameBootProfileDebugger.Log("SaveProfile", "? Error al guardar", LogType.Error);
        }
        
        return success;
    }

    /// <summary>Carga datos del SaveSystem y los aplica al profile</summary>
    public bool LoadProfile(SaveSystem saveSystem)
    {
        if (!saveSystem || !saveSystem.HasSave())
        {
            GameBootProfileDebugger.Log("LoadProfile", "? Sin SaveSystem o sin save disponible", LogType.Warning);
            return false;
        }

        if (saveSystem.Load(out var data))
        {
            ApplySaveDataToProfile(data);

            // Snapshot narrativo eliminado; no se restaura

            NarrativeAutoSetup.ResetForLoadedProfile();
            
            // ✅ Limpiar el registro de NPCs narrativos para que se re-registren con el estado correcto.
            // fullClear=true porque LoadProfile() siempre precede a una carga de escena:
            // los executors volverán a llamar OnEnable y se re-registrarán solos.
            Game.NPC.Modules.NPCInteractiveNarrativeRegistry.Clear(fullClear: true);
            Debug.Log("[GameBootProfile] 🔄 NPCInteractiveNarrativeRegistry limpiado para carga de partida");


            GameBootProfileDebugger.Log("LoadProfile", $"? Cargado exitoso - Anchor: {data.lastSpawnAnchorId}, HP: {data.currentHp:F0}", LogType.Log);
            return true;
        }
        
        GameBootProfileDebugger.Log("LoadProfile", "? Error al cargar datos", LogType.Error);
        return false;
    }

    /// <summary>Actualiza el runtimePreset con los valores actuales del juego (PlayerHealthSystem, etc.)</summary>
    public void UpdateRuntimePresetFromCurrentState()
    {
        EnsureRuntimePreset();
        var p = runtimePreset;

        var syncedSystems = new System.Collections.Generic.List<string>();

        // Actualizar anchor actual en el runtime preset
        var currentAnchor = SpawnManager.CurrentAnchorId;
        if (!string.IsNullOrEmpty(currentAnchor))
        {
            p.spawnAnchorId = currentAnchor;
            syncedSystems.Add($"SpawnAnchor({currentAnchor})");
        }

        // Obtener datos del PlayerHealthSystem si existe
        var playerHealthSystem = FindAnyObjectByType<PlayerHealthSystem>();
        if (playerHealthSystem != null)
        {
            p.maxHP = playerHealthSystem.MaxHealth;
            p.currentHP = playerHealthSystem.CurrentHealth;
            syncedSystems.Add($"Health({p.currentHP:F0}/{p.maxHP:F0})");
        }

        // Obtener datos del sistema de man si existe
        var manaPool = FindAnyObjectByType<ManaPool>();
        if (manaPool != null)
        {
            p.maxMP = manaPool.Max;
            p.currentMP = manaPool.Current;
            syncedSystems.Add($"Mana({p.currentMP:F0}/{p.maxMP:F0})");
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

            // Aadir flags exportados por el QuestManager (active/completed/steps)
            qm.ExportFlags(newFlags);

            // Log detallado de quests activas/completadas para debug
            var questFlags = newFlags.FindAll(f => f.StartsWith("QUEST_"));
            Debug.Log($"[GameBootProfile] Quest flags al guardar: {string.Join(", ", questFlags)}");

            p.flags = newFlags;
            syncedSystems.Add($"QuestFlags({newFlags.Count})");
        }

        // === NUEVO: sincronizar abilities desde el PlayerActionManager (estado runtime actual) ===
         var actionManager = FindAnyObjectByType<PlayerActionManager>();
         if (actionManager != null)
         {
            if (p.abilities == null) p.abilities = new PlayerAbilities();
            p.abilities.swim = actionManager.AllowSwim;
            p.abilities.jump = actionManager.AllowJump;
            p.abilities.climb = actionManager.AllowClimb;
            p.abilities.fly = actionManager.AllowFly;
            p.abilities.magic = actionManager.AllowMagic;
            syncedSystems.Add($"Abilities(S:{actionManager.AllowSwim},J:{actionManager.AllowJump},C:{actionManager.AllowClimb},F:{actionManager.AllowFly},M:{actionManager.AllowMagic})");
         }

        // Nota: Los dems datos (level, abilities, spells, flags) se mantienen del preset actual
        if (PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true))
        {
            p.inventoryItems = SanitizeInventorySnapshot(inventory.GetSaveSnapshot());
            syncedSystems.Add($"Inventory({p.inventoryItems?.Count ?? 0})");
        }
        else
        {
            p.inventoryItems = new List<InventoryItemSave>();
        }

        // === NUEVO: sincronizar Wardrobe desde WardrobeInventory ===
        if (PlayerService.TryGetComponent<WardrobeInventory>(out var wardrobe, includeInactive: true, allowSceneLookup: true))
        {
            p.unlockedWardrobeIds = wardrobe.GetUnlockedIds();
            syncedSystems.Add($"Wardrobe({p.unlockedWardrobeIds?.Count ?? 0})");
            Debug.Log($"[GameBootProfile] Wardrobe sincronizado al preset: {p.unlockedWardrobeIds?.Count ?? 0} items desbloqueados");
        }
        else
        {
            p.unlockedWardrobeIds = new List<string>();
            Debug.LogWarning("[GameBootProfile] WardrobeInventory no disponible - Wardrobe guardado vacío");
        }

        if (PlayerService.TryGetComponent<ModularAutoBuilder>(out var builder, includeInactive: true, allowSceneLookup: true))
        {
            // Cuando el personaje activo no es Will, el builder muestra la apariencia de ese personaje.
            // preset.appearance debe guardar SIEMPRE la apariencia de Will para que al cargar
            // el controller de Will tenga su aspecto correcto, independientemente de quién sea el activo.
            var registry = CharacterAppearanceRegistry.Instance;
            var activeSlot = PartyControlManager.Instance?.ActiveSlot ?? PartyControlManager.CharacterSlot.Will;

            // preset.appearance siempre debe almacenar la apariencia de Will, no la del personaje activo.
            bool skipAppearanceUpdate = false;
            Dictionary<PartCategory, string> selection;

            if (activeSlot != PartyControlManager.CharacterSlot.Will && registry != null)
            {
                selection = registry.GetAppearance(PartyControlManager.CharacterSlot.Will);
                if (selection == null || selection.Count == 0)
                {
                    // Registry sin datos de Will: no actualizar para evitar guardar la apariencia
                    // del personaje activo (Estela/Liam) como si fuera la de Will.
                    Debug.LogWarning("[GameBootProfile] ⚠️ Registry sin apariencia de Will — preset.appearance se mantiene sin cambios para evitar corrupción.");
                    skipAppearanceUpdate = true;
                    selection = null;
                }
            }
            else
            {
                selection = builder.GetSelection();
            }

            if (!skipAppearanceUpdate)
            {
                if (selection != null)
                {
                    if (p.appearance == null) p.appearance = new List<AppearanceEntry>();
                    else p.appearance.Clear();

                    foreach (var kv in selection)
                    {
                        p.appearance.Add(new AppearanceEntry
                        {
                            category = kv.Key,
                            partName = string.IsNullOrEmpty(kv.Value) ? null : kv.Value
                        });
                    }
                    syncedSystems.Add($"Appearance({p.appearance.Count})");
                    Debug.Log($"[GameBootProfile] Apariencia sincronizada: {p.appearance.Count} partes [{string.Join(", ", p.appearance.ConvertAll(a => $"{a.category}:{a.partName}"))}]");
                }
                else
                {
                    Debug.LogWarning("[GameBootProfile] ModularAutoBuilder.GetSelection() devolvió null");
                    p.appearance = new List<AppearanceEntry>();
                }
            }
        }
        else
        {
            Debug.LogWarning("[GameBootProfile] ModularAutoBuilder no encontrado - Apariencia guardada vacía");
            p.appearance = new List<AppearanceEntry>();
        }

        if (BossProgressTracker.TryGetInstance(out var bossTracker))
        {
            p.defeatedBossIds = bossTracker.GetSnapshot();
            syncedSystems.Add($"Bosses({p.defeatedBossIds?.Count ?? 0})");
        }
        else
        {
            p.defeatedBossIds = new List<string>();
        }

        int npcCount = CaptureNpcPositionsFromScene(p);
        if (npcCount >= 0)
            syncedSystems.Add($"NPCs({npcCount})");

        // Capturar estado de los grafos narrativos
        if (NarrativeGraphHub.Instance != null)
        {
            Debug.Log("[GameBootProfile] === CAPTURANDO BLACKBOARDS ===");
            p.narrativeBlackboards = NarrativeGraphHub.Instance.CaptureBlackboards();
            syncedSystems.Add($"Narratives({p.narrativeBlackboards?.Count ?? 0})");
            Debug.Log($"[GameBootProfile] Blackboards capturados: {p.narrativeBlackboards?.Count ?? 0}");
        }
        else
        {
            Debug.LogWarning("[GameBootProfile] NarrativeGraphHub.Instance es NULL - no se pueden guardar blackboards");
            p.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        }

        // === NUEVO: sincronizar Party desde PlayerParty ===
        if (Game.NPC.PlayerParty.HasInstance)
        {
            var party = Game.NPC.PlayerParty.Instance;
            var partyIds = party.GetMemberIdsForSave();
            p.partyMemberIds = partyIds;
            syncedSystems.Add($"Party({partyIds.Count})");
            Debug.Log($"[GameBootProfile] Party sincronizado al preset: {partyIds.Count} miembros [{string.Join(", ", partyIds)}]");
        }
        else
        {
            p.partyMemberIds = new List<string>();
            Debug.LogWarning("[GameBootProfile] PlayerParty no disponible - Party guardado vacío");
        }

        // Guardar el personaje activo al momento de guardar.
        // CRÍTICO: debe hacerse AQUÍ (antes de guardar) y NO en SyncPartyToPreset(),
        // porque SyncPartyToPreset corre durante OnProfileReady cuando _activeIndex aún es
        // el valor por defecto (Will=1), sobreescribiendo el slot real guardado.
        if (PartyControlManager.Instance != null)
        {
            p.activeCharacterSlot = PartyControlManager.Instance.ActiveIndex;
            syncedSystems.Add($"ActiveSlot({(PartyControlManager.CharacterSlot)p.activeCharacterSlot})");
        }

        // Snapshot narrativo eliminado; no se captura

        // === Sincronizar puntos de teletransporte desbloqueados desde TeleportRegistry ===
        p.unlockedTeleportPoints = TeleportRegistry.ToSaveData();
        syncedSystems.Add($"Teleports({p.unlockedTeleportPoints?.Count ?? 0})");

        Debug.Log($"[GameBootProfile] RuntimePreset actualizado - Anchor: {p.spawnAnchorId}, HP: {p.currentHP}/{p.maxHP}, MP: {p.currentMP}/{p.maxMP}");
        GameBootProfileDebugger.Log("UpdateRuntimePreset", $"? Sincronizados: {string.Join(", ", syncedSystems)}", LogType.Log);
    }

    int CaptureNpcPositionsFromScene(PlayerPresetSO preset)
    {
        if (preset == null)
            return -1;

        if (preset.npcPositions == null)
            preset.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();

        // IMPORTANTE: No hacer Clear() aquí para no borrar entradas añadidas por NpcAutoMoveNode
        // En su lugar, trabajar con las entradas existentes y actualizar/añadir según sea necesario
        var existingEntries = new Dictionary<string, int>(); // npcId -> index
        for (int i = 0; i < preset.npcPositions.Count; i++)
        {
            var entry = preset.npcPositions[i];
            if (!string.IsNullOrEmpty(entry.npcId))
                existingEntries[entry.npcId] = i;
        }

        var npcs = ServiceLocator.GetAll<NPCBehaviourManagerV2>();
        foreach (var npc in npcs)
        {
            if (npc == null || !npc.persistLastPosition)
                continue;

            var npcId = npc.gameObject.name;

            // Si ya existe una entrada en el preset (añadida por NpcAutoMoveNode), mantenerla
            if (existingEntries.ContainsKey(npcId))
            {
                // La entrada ya existe, verificar si lastPosition es más reciente
                var position = npc.lastPosition;
                if (position == default)
                    position = npc.transform.position;

                if (position != default)
                {
                    var existingIndex = existingEntries[npcId];
                    var existing = preset.npcPositions[existingIndex];

                    // Solo actualizar posición si lastPosition es diferente (por si hubo cambios posteriores)
                    if (Vector3.Distance(existing.position, position) > 0.01f)
                    {
                        existing.position = position;
                        Debug.Log($"[GameBootProfile] Actualizada posición de NPC '{npcId}' desde lastPosition/transform: {position}");
                    }
                    // Siempre actualizar la rotación para capturar turnAroundOnArrival y otros giros
                    existing.rotation = npc.transform.rotation;
                    existing.hasRotation = true; // FIX INC-028
                    preset.npcPositions[existingIndex] = existing;
                }
                // Si la posición sigue siendo default, mantener la entrada del preset sin cambios
                continue;
            }

            // No existe entrada previa, crear una nueva solo si lastPosition no es default
            var npcPosition = npc.lastPosition;
            // Si el NPC nunca persistió manualmente su posición, usar la posición actual de la escena
            if (npcPosition == default)
                npcPosition = npc.transform.position;

            if (npcPosition == default)
                continue;

            var newEntry = new PlayerPresetSO.NpcPosEntry
            {
                npcId = npcId,
                position = npcPosition,
                rotation = npc.transform.rotation,
                hasRotation = true, // FIX INC-028
                hasActiveState = false,
                isActive = true
            };

            preset.npcPositions.Add(newEntry);
            existingEntries[npcId] = preset.npcPositions.Count - 1;
        }

        // Eliminar entradas de NPCs que ya no existen en la escena (fueron destruidos)
        for (int i = preset.npcPositions.Count - 1; i >= 0; i--)
        {
            var entry = preset.npcPositions[i];
            if (string.IsNullOrEmpty(entry.npcId))
            {
                preset.npcPositions.RemoveAt(i);
                continue;
            }

            // Mantener entradas aunque el NPC no esté en escena (puede estar en otra escena)
            // Solo remover si el NPC existe en escena pero ya no tiene persistLastPosition
            var npcInScene = npcs.FirstOrDefault(n => n != null && n.gameObject.name == entry.npcId);
            if (npcInScene != null && !npcInScene.persistLastPosition)
            {
                preset.npcPositions.RemoveAt(i);
            }
        }

        return preset.npcPositions.Count;
    }

    /// <summary>
    /// Aplica las posiciones de NPC guardadas en el preset a los NPCs presentes en la escena actual.
    /// </summary>
    public void ApplyNpcPositionsToScene(PlayerPresetSO preset)
    {
        if (preset == null || preset.npcPositions == null || preset.npcPositions.Count == 0)
            return;

        var npcs = ServiceLocator.GetAll<NPCBehaviourManagerV2>();
        if (npcs == null || npcs.Count == 0)
        {
            Debug.LogWarning($"[GameBootProfile] ApplyNpcPositionsToScene: no se encontraron NPCBehaviourManagerV2 en escena. Las posiciones no se aplicarán.");
            return;
        }

        var map = new Dictionary<string, PlayerPresetSO.NpcPosEntry>(preset.npcPositions.Count);
        foreach (var e in preset.npcPositions)
        {
            if (!string.IsNullOrEmpty(e.npcId) && !map.ContainsKey(e.npcId))
                map[e.npcId] = e;
        }

        var matched = new System.Collections.Generic.HashSet<string>();
        foreach (var npc in npcs)
        {
            if (npc == null) continue;
            var id = npc.gameObject.name;
            if (string.IsNullOrEmpty(id)) continue;
            if (!map.TryGetValue(id, out var entry)) continue;
            matched.Add(id);

            var pos = entry.position;
            if (pos == default) continue;

            try
            {
                // Validar la posición persistida antes de aplicarla. Un preset capturado
                // en mal momento (p.ej. un NPC "colgado" en el aire tras ser levitado, o
                // congelado junto a una pared por el bug de LevitationTarget) puede haber
                // guardado una Y inválida; aplicarla sin comprobar deja al NPC flotando
                // al cargar la partida en vez de en el suelo.
                Vector3 targetPos = pos;
                if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    targetPos = navHit.position;
                }
                else if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var groundHit, 20f))
                {
                    targetPos = groundHit.point;
                    Debug.LogWarning($"[GameBootProfile] Posición persistida de NPC '{id}' no está sobre NavMesh; ajustada al suelo más cercano ({pos} → {targetPos}).");
                }
                else
                {
                    targetPos = npc.transform.position;
                    Debug.LogWarning($"[GameBootProfile] Posición persistida de NPC '{id}' inválida (sin NavMesh ni suelo cerca de {pos}); se mantiene la posición actual en escena para no dejarlo flotando.");
                }

                // Si tiene NavMeshAgent, usar Warp para evitar física/paths.
                // FIX (17 ago 2026) — CAUSA RAÍZ del bug crítico "Estela/Liam desaparecen del
                // party tras cerrar y reabrir el juego": este método se llama desde el flujo
                // normal de WorldBootstrap justo cuando MainWorld termina de cargar, para
                // reposicionar TODOS los NPCs con posición guardada (incluidos _ESTELA y _LIAM).
                // Antes, igual que en EstelaAppearsSequencer/LiamCrystalBallSequencer/etc., se
                // gateaba el Warp() detrás de "agent.isOnNavMesh" — pero justo tras un arranque
                // en frío del juego (proceso de Unity recién lanzado) el NavMeshAgent puede
                // reportar isOnNavMesh=false todavía en este frame, aunque el NavMesh en sí ya
                // esté disponible. En una recarga "caliente" (game over, o salir al menú y
                // volver a entrar dentro del mismo proceso) el runtime de NavMesh ya estaba
                // inicializado de antes y esto nunca fallaba — de ahí que el bug SOLO apareciera
                // tras cerrar y volver a abrir el juego. Al caer en el "else", se hacía
                // transform.position = targetPos directamente, sin pasar por el NavMeshAgent, lo
                // que deja agent.isOnNavMesh en false PARA SIEMPRE (nada más en el juego lo
                // resincroniza) — y JoinParty() exige isOnNavMesh=true para unir al NPC al party,
                // así que Estela/Liam se encontraban siempre en NPCRegistry pero jamás podían
                // unirse. Warp() sí coloca al agente sobre el punto de NavMesh válido más cercano
                // incluso partiendo de isOnNavMesh=false, así que ahora se llama siempre.
                var agent = npc.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.Warp(targetPos);
                    if (!agent.isOnNavMesh)
                        npc.transform.position = targetPos;
                }
                else
                {
                    npc.transform.position = targetPos;
                }

                // FIX INC-028: usar el flag explícito hasRotation en vez de comparar contra
                // Quaternion.identity. Identity es una rotación real y válida para algunos NPCs
                // (p.ej. Eldran en la taberna); con la comparación antigua esos casos se
                // descartaban como "sin rotación persistida" y el NPC quedaba con la rotación del
                // prefab (de espaldas) tras cargar la partida.
                // Compatibilidad con saves antiguos (sin el campo hasRotation, siempre false al
                // deserializar): el sentinel correcto de "nunca se capturó rotación" es
                // default(Quaternion) = (0,0,0,0) — el valor con el que Unity rellena un campo
                // Quaternion ausente del YAML — NO Quaternion.identity = (0,0,0,1). Comparar
                // contra identity hacía que entradas realmente sin rotación capturada (como el
                // _ELDRAN de PlayerPreset_Taberna, que solo tiene position/isActive) se trataran
                // como "sí tienen rotación real" y se les aplicara ese quaternion degenerado
                // (0,0,0,0) al transform, produciendo justo el giro "de espaldas" que este fix
                // pretendía evitar. Ahora solo se aplica la rotación si de verdad fue capturada.
                bool rotationWasCaptured = entry.hasRotation || entry.rotation != default(Quaternion);
                if (rotationWasCaptured)
                {
                    npc.transform.rotation = entry.rotation;
                    if (agent != null && agent.isOnNavMesh)
                        agent.nextPosition = npc.transform.position;
                }

                // Aplicar estado activo si se guardó
                if (entry.hasActiveState)
                {
                    npc.gameObject.SetActive(entry.isActive);
                }

                // Actualizar el lastPosition del NPC para futuras capturas
                npc.lastPosition = pos;

                Debug.Log($"[GameBootProfile] NPC '{id}' reposicionado a {pos} desde preset runtime.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameBootProfile] No se pudo reposicionar NPC '{id}': {ex.Message}");
            }
        }

        foreach (var key in map.Keys)
        {
            if (!matched.Contains(key))
                Debug.LogWarning($"[GameBootProfile] npcId '{key}' en preset no encontrado en escena — ¿NPC renombrado?");
        }
    }

    /// <summary>Actualaiza runtimePreset desde los sistemas y guarda en el SaveSystem. Respeta allowAutoSaves para saves automticos.</summary>
    public bool SaveCurrentGameState(SaveSystem saveSystem, SaveRequestContext context = SaveRequestContext.Manual)
    {
        if (!saveSystem)
        {
            GameBootProfileDebugger.Log("SaveCurrentGameState", "? SaveSystem no disponible", LogType.Error);
            return false;
        }

        if (context == SaveRequestContext.Auto && !allowAutoSaves)
        {
            Debug.Log("[GameBootProfile] Auto-guardado omitido (allowAutoSaves = false)." );
            GameBootProfileDebugger.Log("SaveCurrentGameState", "?? Auto-guardado omitido (allowAutoSaves = false)", LogType.Warning);
            return false;
        }

        // Sincronizar runtimePreset con estado actual del juego
        UpdateRuntimePresetFromCurrentState();
        GameBootProfileDebugger.Log("SaveCurrentGameState", $"?? Runtime actualizado antes de guardar (context: {context})", LogType.Log);

        // Guardar profile actualizado
        return SaveProfile(saveSystem, context);
    }

    // === NUEVO: Flujo de "Nueva partida" ===============================

    /// <summary>
    /// Elimina el save (si se pasa) y restablece el runtimePreset al preset por defecto.
    /// Evita arrastrar datos de partidas anteriores cuando el GameBootService persiste.
    /// </summary>
    public void NewGameReset(SaveSystem saveSystem = null)
    {
        Debug.Log("[GameBootProfile] ===== NUEVA PARTIDA - RESETEANDO TODO =====");
        
        if (saveSystem) saveSystem.Delete();

        if (defaultPlayerPreset)
        {
            Debug.Log($"[GameBootProfile] Copiando desde defaultPlayerPreset: {defaultPlayerPreset.name}");
            EnsureRuntimePresetFromTemplate(defaultPlayerPreset);
            GameBootProfileDebugger.Log("NewGameReset", $"✅ Nueva partida desde defaultPlayerPreset: {defaultPlayerPreset.name}", LogType.Log);
        }
        else
        {
            Debug.LogWarning("[GameBootProfile] No hay defaultPlayerPreset - Creando preset vacío");
            EnsureRuntimePreset();
            ResetPresetToEmpty(runtimePreset);
            GameBootProfileDebugger.Log("NewGameReset", "⚠️ Nueva partida con preset vacío (sin defaultPlayerPreset)", LogType.Warning);
        }

        // Garantizar que la magia arranca bloqueada en partidas nuevas, incluso si el preset
        // por defecto tuviera valores residuales (por testing o saves previos).
        Debug.Log("[GameBootProfile] Bloqueando magia para nueva partida");
        LockMagicForNewGame(runtimePreset);

        // La apariencia ya se copi del defaultPlayerPreset en EnsureRuntimePresetFromTemplate
        // No hace falta aplicar nada adicional, la apariencia del default ya est en runtimePreset

        // Asegurar que las posiciones de NPC NO se arrastran en Nueva Partida
        if (runtimePreset != null)
        {
            if (runtimePreset.npcPositions != null)
                runtimePreset.npcPositions.Clear();
        }

        // Limpiar flags transitorias (ej: cinemticas vistas) para garantizar que Nueva Partida siempre las repita.
        if (runtimePreset != null && runtimePreset.flags != null)
        {
            runtimePreset.flags.RemoveAll(flag => !string.IsNullOrEmpty(flag) && flag.StartsWith("CINEMATIC_SEEN:", StringComparison.OrdinalIgnoreCase));
        }

        // Reiniciar progreso de bosses para partidas nuevas
        if (BossProgressTracker.TryGetInstance(out var tracker))
        {
            var snapshot = runtimePreset != null ? runtimePreset.defeatedBossIds : null;
            tracker.LoadFromSnapshot(snapshot);
        }

        // Resetear misiones al iniciar nueva partida
        if (QuestManager.Instance != null)
            QuestManager.Instance.ResetAllQuests();

        // Limpiar blackboards de los grafos narrativos para Nueva Partida.
        // StopAllRunners primero para evitar que runners de la sesión anterior sigan ejecutando
        // con los blackboards ya borrados, lo que causaba activación de quests en estado inconsistente.
        if (NarrativeGraphHub.Instance != null)
        {
            NarrativeGraphHub.Instance.StopAllRunners();
            NarrativeGraphHub.Instance.ClearAllBlackboards();
            Debug.Log("[GameBootProfile] Runners detenidos y blackboards narrativos limpiados para Nueva Partida");
        }

        // Limpiar snapshots narrativos del preset para forzar inicio desde StartNode
        if (runtimePreset != null && runtimePreset.narrativeBlackboards != null)
        {
            runtimePreset.narrativeBlackboards.Clear();
            Debug.Log("[GameBootProfile] Snapshots narrativos del preset limpiados para Nueva Partida");
        }

        // ✅ NUEVO: Limpiar narrativas interactivas completadas para que se puedan volver a ejecutar
        if (runtimePreset != null && runtimePreset.completedInteractiveNarratives != null)
        {
            runtimePreset.completedInteractiveNarratives.Clear();
            Debug.Log("[GameBootProfile] ✅ Narrativas interactivas limpiadas para Nueva Partida");
        }

        // Limpiar lore popups vistos para que se puedan volver a mostrar en una nueva partida
        if (runtimePreset != null)
        {
            runtimePreset.seenLorePopupIds ??= new List<string>();
            runtimePreset.seenLorePopupIds.Clear();
        }
        
        // ✅ Limpiar el registro de NPCs narrativos para que se re-registren frescos
        Game.NPC.Modules.NPCInteractiveNarrativeRegistry.Clear();
        Debug.Log("[GameBootProfile] 🔄 NPCInteractiveNarrativeRegistry limpiado para Nueva Partida");

        // ✅ Limpiar puntos de teletransporte desbloqueados para nueva partida
        TeleportRegistry.Clear();
        if (runtimePreset != null && runtimePreset.unlockedTeleportPoints != null)
        {
            runtimePreset.unlockedTeleportPoints.Clear();
        }
        Debug.Log("[GameBootProfile] 🚀 TeleportRegistry y preset limpiados para Nueva Partida");

        // ✅ Limpiar miembros del equipo para nueva partida
        if (runtimePreset != null && runtimePreset.partyMemberIds != null)
        {
            runtimePreset.partyMemberIds.Clear();
        }
        Debug.Log("[GameBootProfile] 🤝 Party limpiado en preset para Nueva Partida");

        NarrativeAutoSetup.ResetForNewGame();

        Debug.Log("[GameBootProfile] Reset realizado para Nueva Partida (runtimePreset -> default)");
        GameBootProfileDebugger.Log("NewGameReset", "? Reset completado - sistemas reiniciados", LogType.Log);
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
        p.appearance = new List<AppearanceEntry>();
        p.inventoryItems = new List<InventoryItemSave>();
        p.defeatedBossIds = new List<string>();
        p.consumedInteractableIds = new List<string>();
        p.seenLorePopupIds = new List<string>();
        // === NUEVO: resetear abilities ===
        p.abilities = new PlayerAbilities();
    }

    /// <summary>
    /// Elimina cualquier rastro de magia desbloqueada para una partida nueva.
    /// </summary>
    void LockMagicForNewGame(PlayerPresetSO preset)
    {
        if (preset == null) return;

        // Bloquear habilidad de magia.
        if (preset.abilities == null) preset.abilities = new PlayerAbilities();
        preset.abilities.magic = false;

        // Asegurar que el listado de habilidades no marca magia como desbloqueada.
        preset.unlockedAbilities ??= new List<AbilityId>();
        preset.unlockedAbilities.Remove(AbilityId.MagicAttack);

        // Limpiar hechizos y slots asociados para que no aparezcan en UI.
        preset.unlockedSpells ??= new List<SpellId>();
        preset.unlockedSpells.Clear();
        preset.leftSpellId = SpellId.None;
        preset.rightSpellId = SpellId.None;
        preset.specialSpellId = SpellId.None;

        // Sin magia, el man debe iniciar en 0 para evitar mostrar barra llena.
        preset.maxMP = 0f;
        preset.currentMP = 0f;
    }

}