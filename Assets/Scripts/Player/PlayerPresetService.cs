using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerPresetService : MonoBehaviour
{
    [Header("Librería de hechizos (ID → SO)")]
    [SerializeField] private SpellLibrarySO spellLibrary;
    public SpellLibrarySO SpellLibrary => spellLibrary;

    [Header("Opciones")]
    [SerializeField] private bool autoFillEmptySlotsFromUnlocked; // default false por defecto
    [SerializeField] private GameObject instigatorOverride;

    MagicProjectileSpawner _spawner;
    MagicCaster _magicCaster; // ← NUEVO: referencia al MagicCaster
    ManaPool _manaPool;       // ← NUEVO: referencia al ManaPool
    PlayerActionManager _actionManager; // ← NUEVO: referencia al PlayerActionManager
    ModularAutoBuilder _appearanceBuilder;
    Inventory _inventory;     // ← NUEVO: referencia al Inventory
    WardrobeInventory _wardrobeInventory;
    
    // Evitar inicialización doble si el evento llega más de una vez o ya está listo al habilitar
    bool _initialized;
    bool _warnedMissingAppearanceBuilder;

    public static bool HasAppliedPreset { get; private set; }

    void Awake()
    {
        // Buscar spawner y magicCaster también en los padres del GameObject.
        // Esto evita crear un MagicProjectileSpawner LOCAL cuando ya existe uno en el root del jugador.
        _spawner = GetComponent<MagicProjectileSpawner>() ?? GetComponentInParent<MagicProjectileSpawner>();
        if (_spawner == null)
        {
            // Como último recurso, añadimos uno local (solo si realmente no existe en la jerarquía)
            _spawner = gameObject.AddComponent<MagicProjectileSpawner>();
        }

        _magicCaster = GetComponent<MagicCaster>() ?? GetComponentInParent<MagicCaster>();
        _manaPool = GetComponent<ManaPool>() ?? GetComponentInParent<ManaPool>(); // ← NUEVO: obtener ManaPool
        if (_manaPool != null)
        {
            ServiceLocator.Register(_manaPool);
        }
        _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>(); // ← NUEVO: obtener PlayerActionManager
        _inventory = GetComponent<Inventory>() ?? GetComponentInParent<Inventory>(); // ← NUEVO: obtener Inventory
    }

    // Suscribirnos al evento y cubrir el caso de que ya esté disponible
    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(PlayerPresetService));
        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;
        InitializePresetService();
        _initialized = true;
        // Ya no necesitamos el evento tras inicializar
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void InitializePresetService()
    {
        var profile = GameBootService.Profile;
        
        if (!profile)
        {
            enabled = false;
            Debug.LogError("[PlayerPresetService] Falta GameBootProfile.");
            return;
        }
        if (!spellLibrary)
        {
            Debug.LogWarning("[PlayerPresetService] SpellLibrary no asignada en el inspector — algunas funcionalidades (resolución de SpellId) no estarán disponibles.");
        }

        // Log diagnóstico: comprobar qué componentes hemos encontrado para evitar configuraciones en el objeto equivocado
        EnsureAppearanceBuilderReference();
        // Debug.Log($"[PlayerPresetService] Componentes encontrados -> spawner={( _spawner != null ? _spawner.GetType().Name : "null" )}, magicCaster={( _magicCaster != null ? _magicCaster.GetType().Name : "null" )}, manaPool={( _manaPool != null ? _manaPool.GetType().Name : "null" )}, actionManager={( _actionManager != null ? _actionManager.GetType().Name : "null" )}, appearanceBuilder={( _appearanceBuilder != null ? _appearanceBuilder.GetType().Name : "null" )}, inventory={( _inventory != null ? _inventory.GetType().Name : "null" )}");

        var preset = profile.GetActivePresetResolved();
        if (!preset) 
        { 
            Debug.LogWarning("[PlayerPresetService] Sin preset activo."); 
            return; 
        }

        OnPresetApplying?.Invoke();
        HasAppliedPreset = false;

        // Log inicial de diagnóstico
        var spellsCount = spellLibrary.Spells != null ? spellLibrary.Spells.Count : -1;
        int unlockedCount = preset.unlockedSpells != null ? preset.unlockedSpells.Count : 0;
        // Debug.Log($"[PlayerPresetService] SpellLibrary spells: {spellsCount} | Unlocked: {unlockedCount} | Slots IDs → L:{preset.leftSpellId} R:{preset.rightSpellId} S:{preset.specialSpellId}");

        // USAR EL ANCHOR DEL PRESET SO
        if (!string.IsNullOrEmpty(preset.spawnAnchorId))
        {
            SpawnManager.SetCurrentAnchor(preset.spawnAnchorId);
        }

        // Pre-snapshot de Will ANTES de cualquier cambio en el builder.
        // Si preset.appearance está corrompido con partes de otro personaje, el snapshot
        // del prefab sirve como referencia correcta para detectar la corrupción y corregirla.
        EnsureAppearanceBuilderReference();
        CharacterAppearanceRegistry.Instance?.SnapshotWillFromBuilderIfNeeded(_appearanceBuilder);

        // Sincronizar maná del preset → ManaPool (evita arrancar 50/50 si el preset está en 0/0)
        ApplyManaFromPreset(preset);

        // Sincronizar inventario del preset → Inventory
        ApplyInventoryFromPreset(preset);

        // Configurar hechizos del preset
        ConfigureSpells(preset);
        ApplyAppearanceFromPreset(preset);
        ApplyWardrobeFromPreset(preset);

        // === NUEVO: Aplicar abilities del preset al PlayerActionManager si existe ===
        if (_actionManager == null)
            _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();

        if (_actionManager != null && preset != null)
        {
            if (preset.abilities == null)
            {
                Debug.LogWarning("[PlayerPresetService] preset.abilities es NULL — creando con valores por defecto");
                preset.abilities = new PlayerAbilities { swim = false, jump = false, climb = false, magic = false, fly = false };
            }
            // Debug.Log($"[PlayerPresetService] Aplicando abilities del preset: Swim={preset.abilities.swim} Jump={preset.abilities.jump} Climb={preset.abilities.climb} Fly={preset.abilities.fly} Magic={preset.abilities.magic}");
            _actionManager.ApplyAbilities(preset.abilities);
            // Debug.Log("[PlayerPresetService] Abilities del preset aplicadas al PlayerActionManager");
            // Debug.Log($"[PlayerPresetService] PlayerActionManager.AllowMagic = {_actionManager.AllowMagic}");
        }
        else if (_actionManager == null)
        {
            Debug.LogWarning("[PlayerPresetService] No se encontró PlayerActionManager para aplicar abilities del preset");
        }

        // Notificar a subscriptores (HUD, UI u otros) que el preset ha sido aplicado
        HasAppliedPreset = true;
        OnPresetApplied?.Invoke();
    }

    private void EnsureAppearanceBuilderReference()
    {
        if (_appearanceBuilder != null) return;

        _appearanceBuilder = GetComponent<ModularAutoBuilder>()
            ?? GetComponentInChildren<ModularAutoBuilder>(true)
            ?? GetComponentInParent<ModularAutoBuilder>();

        if (_appearanceBuilder == null && PlayerService.TryGetComponent<ModularAutoBuilder>(out var builder, includeInactive: true, allowSceneLookup: true))
        {
            _appearanceBuilder = builder;
        }

        if (_appearanceBuilder == null && !_warnedMissingAppearanceBuilder)
        {
            Debug.LogWarning("[PlayerPresetService] No se encontro ModularAutoBuilder para sincronizar la apariencia del jugador.");
            _warnedMissingAppearanceBuilder = true;
        }
    }

    private void ApplyAppearanceFromPreset(PlayerPresetSO preset)
    {
        if (preset == null) return;

        EnsureAppearanceBuilderReference();
        if (_appearanceBuilder == null)
        {
            Debug.LogError("[PlayerPresetService] ❌ APARIENCIA NO APLICADA: _appearanceBuilder es NULL. El jugador mantendrá la apariencia del prefab.");
            return;
        }

        var entries = preset.appearance;

        // Si el preset no tiene apariencia definida (lista vacía o null) y Will es el activo,
        // no tocar el builder — dejar al jugador con la apariencia actual del prefab.
        // NUNCA aplicar una selección vacía si hay un personaje visible: haría al jugador invisible.
        if (entries == null || entries.Count == 0)
        {
            var currentSlot = PartyControlManager.Instance?.ActiveSlot ?? PartyControlManager.CharacterSlot.Will;
            Debug.LogWarning($"[PlayerPresetService] ⚠️ Preset '{preset.name}' sin apariencia definida (activeSlot={currentSlot}). " +
                "Se omite la aplicación al builder para evitar invisibilidad. " +
                "Define la apariencia en el Inspector del preset para control preciso.");
            return;
        }

        var selection = new Dictionary<PartCategory, string>();
        foreach (PartCategory cat in (PartCategory[])Enum.GetValues(typeof(PartCategory)))
        {
            selection[cat] = null;
        }
        foreach (var entry in entries)
        {
            selection[entry.category] = string.IsNullOrEmpty(entry.partName) ? null : entry.partName;
        }

        var activeSlot = PartyControlManager.Instance?.ActiveSlot ?? PartyControlManager.CharacterSlot.Will;

        Debug.Log($"[PlayerPresetService] 🎨 ApplyAppearanceFromPreset — preset='{preset.name}' activeSlot={activeSlot} entries=[{string.Join(", ", entries.ConvertAll(e => $"{e.category}:{e.partName}"))}] builder={_appearanceBuilder.gameObject.name}");

        if (activeSlot != PartyControlManager.CharacterSlot.Will)
        {
            // Proteger el registry de Will contra datos corrompidos del preset antes de guardarlos.
            var registry = CharacterAppearanceRegistry.Instance;
            var snap = registry?.GetAppearance(PartyControlManager.CharacterSlot.Will);
            if (registry != null && registry.HasWillSnapshot && snap?.Count > 0 && IsSelectionCorrupted(selection, registry))
            {
                Debug.LogWarning("[PlayerPresetService] ↩️ preset.appearance corrompido — registry de Will NO se sobreescribe.");
                return;
            }

            Debug.Log($"[PlayerPresetService] ↩️ Slot activo es {activeSlot} (no Will): guardando apariencia de Will en registry sin tocar el builder.");
            CharacterAppearanceRegistry.Instance?.SetAppearanceFromList(
                PartyControlManager.CharacterSlot.Will, entries);
            return;
        }

        // Aplicar a través del registry para mantener _currentBuilderSlot sincronizado.
        // SetAppearanceFromList actualiza _appearances[Will] con los datos del preset,
        // y ApplyAppearance lo aplica al builder y actualiza _currentBuilderSlot = Will.
        var reg = CharacterAppearanceRegistry.Instance;
        if (reg != null)
        {
            // Detectar corrupción: si el preset contiene partes de otro personaje, usar el
            // snapshot del registry en lugar de sobreescribirlo con datos corruptos.
            // Requisito: el snapshot debe ser no-vacío para ser válido como referencia.
            var willSnap = reg.GetAppearance(PartyControlManager.CharacterSlot.Will);
            bool corrupted = reg.HasWillSnapshot && willSnap.Count > 0 && IsSelectionCorrupted(selection, reg);
            if (corrupted)
            {
                Debug.LogWarning("[PlayerPresetService] ⚠️ Corrupción detectada en preset.appearance. Aplicando snapshot existente del registry sin sobreescribir.");
                reg.ApplyAppearance(PartyControlManager.CharacterSlot.Will);
            }
            else
            {
                reg.SetAppearanceFromList(PartyControlManager.CharacterSlot.Will, entries);
                reg.ApplyAppearance(PartyControlManager.CharacterSlot.Will);
            }
        }
        else
        {
            Debug.LogWarning("[PlayerPresetService] CharacterAppearanceRegistry no disponible — aplicando directamente al builder.");
            _appearanceBuilder.DeactivateAllCategories();
            _appearanceBuilder.ApplySelection(selection);
        }

        Debug.Log($"[PlayerPresetService] ✅ Apariencia de Will aplicada al builder. Partes activas: [{string.Join(", ", _appearanceBuilder.GetSelection().Select(kv => $"{kv.Key}:{kv.Value}"))}]");
    }

    /// <summary>
    /// Devuelve true si la selección contiene ≥2 partes de otro personaje (Estela o Liam)
    /// que además difieren del pre-snapshot de Will, indicando corrupción.
    /// </summary>
    private static bool IsSelectionCorrupted(Dictionary<PartCategory, string> selection,
                                              CharacterAppearanceRegistry registry)
    {
        var willSnap  = registry.GetAppearance(PartyControlManager.CharacterSlot.Will);
        var estelaApp = registry.GetAppearance(PartyControlManager.CharacterSlot.Estela);
        var liamApp   = registry.GetAppearance(PartyControlManager.CharacterSlot.Liam);

        int corruptCount = 0;
        foreach (var kv in selection)
        {
            if (string.IsNullOrEmpty(kv.Value)) continue;

            bool inOtherChar =
                (estelaApp.TryGetValue(kv.Key, out var ev) && ev == kv.Value) ||
                (liamApp.TryGetValue(kv.Key,   out var lv) && lv == kv.Value);

            bool differsFromSnap =
                !willSnap.TryGetValue(kv.Key, out var sv) || sv != kv.Value;

            if (inOtherChar && differsFromSnap)
                corruptCount++;
        }

        if (corruptCount >= 2)
        {
            Debug.LogWarning($"[PlayerPresetService] 🔍 {corruptCount} partes de otro personaje detectadas en preset.appearance.");
            return true;
        }
        return false;
    }

    public void SnapshotAppearanceToPreset()
    {
        EnsureAppearanceBuilderReference();
        if (_appearanceBuilder == null) return;

        // preset.appearance siempre almacena la apariencia de Will.
        // Si el personaje activo no es Will, solo actualizamos el registry del personaje activo
        // y salimos sin tocar preset.appearance para evitar corromperla con partes de otro personaje.
        var activeSlot = PartyControlManager.Instance?.ActiveSlot ?? PartyControlManager.CharacterSlot.Will;
        if (activeSlot != PartyControlManager.CharacterSlot.Will)
        {
            CharacterAppearanceRegistry.Instance?.CaptureActiveCharacterAppearance();
            return;
        }

        if (!GameBootService.IsAvailable)
        {
            Debug.LogWarning("[PlayerPresetService] GameBootService no esta listo; no se guarda la apariencia.");
            return;
        }

        var profile = GameBootService.Profile;
        if (profile == null)
        {
            Debug.LogWarning("[PlayerPresetService] GameBootService.Profile es null; no se guarda la apariencia.");
            return;
        }

        var preset = profile.GetActivePresetResolved();
        if (preset == null)
        {
            Debug.LogWarning("[PlayerPresetService] No hay preset activo; no se guarda la apariencia.");
            return;
        }

        var selection = _appearanceBuilder.GetSelection();

        if (preset.appearance == null)
            preset.appearance = new List<AppearanceEntry>();
        else
            preset.appearance.Clear();

        var processed = new HashSet<PartCategory>();
        foreach (PartCategory cat in (PartCategory[])System.Enum.GetValues(typeof(PartCategory)))
        {
            if (!processed.Add(cat)) continue;

            var options = _appearanceBuilder.GetOptions(cat);
            if (options == null || options.Count == 0) continue;

            selection.TryGetValue(cat, out var partName);
            preset.appearance.Add(new AppearanceEntry
            {
                category = cat,
                partName = string.IsNullOrEmpty(partName) ? null : partName
            });
        }

        Debug.Log("[PlayerPresetService] Apariencia guardada en el preset activo.");
        
        // Sincronizar con CharacterAppearanceRegistry para que el switch de personaje
        // mantenga los cambios de vestuario realizados (ej: la capa)
        CharacterAppearanceRegistry.Instance?.CaptureActiveCharacterAppearance();
    }

    void ApplyWardrobeFromPreset(PlayerPresetSO preset)
    {
        if (preset == null) return;

        if (_wardrobeInventory == null)
            PlayerService.TryGetComponent(out _wardrobeInventory, includeInactive: true, allowSceneLookup: true);

        _wardrobeInventory?.ApplyFromPreset(preset);
    }

    // === NUEVO: Aplica el inventario desde el preset al componente Inventory del jugador ===
    private void ApplyInventoryFromPreset(PlayerPresetSO preset)
    {
        if (!preset) return;
        
        if (_inventory == null)
        {
            // Intentar obtener el Inventory del jugador
            if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: false, allowSceneLookup: true))
            {
                _inventory = inv;
            }
        }

        if (_inventory != null)
        {
            var items = preset.inventoryItems;
            if (items != null && items.Count > 0)
            {
                _inventory.LoadSnapshot(items, clearExisting: true, notifyChanges: false);
                //Debug.Log($"[PlayerPresetService] Inventario cargado desde preset ({items.Count} tipos de ítems)");
            }
            else
            {
                _inventory.LoadSnapshot(null, clearExisting: true, notifyChanges: false);
                //Debug.Log("[PlayerPresetService] Inventario inicializado vacío (preset sin items)");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerPresetService] No se encontró componente Inventory para cargar el inventario del preset");
        }
    }

    // === NUEVO: Aplica valores de maná desde el preset al ManaPool del jugador ===
    private void ApplyManaFromPreset(PlayerPresetSO preset)
    {
        if (!preset) return;
        if (_manaPool == null)
        {
            // Obtener ManaPool directamente desde el ServiceLocator
            _manaPool = ServiceLocator.Get<ManaPool>(false);
        }
        if (_manaPool != null)
        {
            //Debug.Log($"[PlayerPresetService] Using ManaPool on GameObject: {_manaPool.gameObject.name}");
        }
        if (_manaPool != null)
        {
            // Log del estado actual del preset ANTES de cualquier modificación
            //Debug.Log($"[PlayerPresetService] ApplyManaFromPreset - Preset inicial: maxMP={preset.maxMP}, currentMP={preset.currentMP}, abilities.magic={preset.abilities?.magic}");
            
            // Si el preset NO tiene la ability de magia, no queremos mostrar una barra de maná llena.
            if (preset.abilities != null && !preset.abilities.magic)
            {
                _manaPool.Init(0f, 0f);
                //Debug.Log("[PlayerPresetService] Preset indica que no tiene magia -> maxMP y currentMP seteados a 0");
                return;
            }

            // Si el preset explícitamente no define maná, intentar inferir un valor mínimo a partir
            // de los hechizos asignados o desbloqueados para evitar que el jugador tenga 0 MP y no pueda lanzar.
            float maxMP = Mathf.Max(0f, preset.maxMP);
            float currentMP = Mathf.Clamp(preset.currentMP, 0f, maxMP > 0f ? maxMP : float.MaxValue);
            
            //Debug.Log($"[PlayerPresetService] ApplyManaFromPreset - Valores calculados: maxMP={maxMP}, currentMP={currentMP}");

            if (maxMP <= 0f)
            {
                float highestCost = 0f;
                // Revisar slots asignados
                SpellId[] slotIds = new[] { preset.leftSpellId, preset.rightSpellId, preset.specialSpellId };
                foreach (var id in slotIds)
                {
                    if (id == SpellId.None) continue;
                    var s = spellLibrary != null ? spellLibrary.Get(id) : null;
                    if (s != null) highestCost = Mathf.Max(highestCost, s.manaCost);
                }
                // Revisar desbloqueados
                if (preset.unlockedSpells != null && spellLibrary != null)
                {
                    foreach (var id in preset.unlockedSpells)
                    {
                        if (id == SpellId.None) continue;
                        var s = spellLibrary.Get(id);
                        if (s != null) highestCost = Mathf.Max(highestCost, s.manaCost);
                    }
                }

                if (highestCost > 0f)
                {
                    // Dar suficiente maná para lanzar al menos 1-2 veces el hechizo más caro
                    maxMP = Mathf.Max(20f, highestCost * 2f);
                    currentMP = Mathf.Max(currentMP, maxMP);
                    //Debug.Log($"[PlayerPresetService] Preset maxMP was 0 — inferred maxMP={maxMP} from highest spell cost {highestCost}");
                }
                else
                {
                    // Si no hay hechizos conocidos, respetar explícitamente un preset 0/0.
                    // Anteriormente forzábamos maxMP=50 para evitar que el jugador no pudiera lanzar,
                    // pero esto sobreescribía presets donde el diseñador quería 0/0. Ahora:
                    // - Si el preset explícitamente tiene maxMP<=0 y currentMP<=0 -> respetar 0/0
                    // - En caso contrario (p.ej. currentMP > 0) usar un fallback razonable
                    if (preset.currentMP <= 0f && preset.maxMP <= 0f)
                    {
                        maxMP = 0f;
                        currentMP = 0f;
                        //Debug.Log("[PlayerPresetService] Preset explicitly sets 0/0 and no spells found -> respecting 0/0");
                    }
                    else
                    {
                        // Fallback: si el preset pide algo de currentMP pero no definió max, usar el valor histórico por compatibilidad
                        maxMP = 50f;
                        if (currentMP <= 0f) currentMP = Mathf.Clamp(preset.currentMP, 0f, maxMP);
                        //Debug.Log("[PlayerPresetService] Preset maxMP was 0 and no spells found; using default maxMP=50 to satisfy preset currentMP");
                    }
                 }
             }

             _manaPool.Init(maxMP, currentMP);
             //Debug.Log($"[PlayerPresetService] ApplyManaFromPreset - ManaPool.Init({maxMP}, {currentMP}) llamado. ManaPool ahora: {_manaPool.Max}/{_manaPool.Current}");
         }
         else
         {
             Debug.LogWarning("[PlayerPresetService] No se encontró ManaPool en escena para sincronizar MP");
         }
    }

    private void ConfigureSpells(PlayerPresetSO preset)
    {
        // Resolver IDs respetando None
        var leftId = preset.leftSpellId;
        var rightId = preset.rightSpellId;
        var specialId = preset.specialSpellId;

        // Fallback: si no hay ningún slot asignado pero sí hay desbloqueados, autocompletamos aunque la opción esté desactivada
        bool allNone = leftId == SpellId.None && rightId == SpellId.None && specialId == SpellId.None;

        var left = leftId == SpellId.None ? null : spellLibrary.Get(leftId);
        var right = rightId == SpellId.None ? null : spellLibrary.Get(rightId);
        var special = specialId == SpellId.None ? null : spellLibrary.Get(specialId);

        if (leftId != SpellId.None && left == null) Debug.LogWarning($"[PlayerPresetService] Left ID {leftId} no está en SpellLibrary");
        if (rightId != SpellId.None && right == null) Debug.LogWarning($"[PlayerPresetService] Right ID {rightId} no está en SpellLibrary");
        if (specialId != SpellId.None && special == null) Debug.LogWarning($"[PlayerPresetService] Special ID {specialId} no está en SpellLibrary");

        // Validar tipos de slot
        if (left && left.slotType == SpellSlotType.SpecialOnly) { Debug.LogWarning("[PlayerPresetService] Left es SpecialOnly, se descarta"); left = null; }
        if (right && right.slotType == SpellSlotType.SpecialOnly) { Debug.LogWarning("[PlayerPresetService] Right es SpecialOnly, se descarta"); right = null; }
        if (special && special.slotType != SpellSlotType.SpecialOnly) { Debug.LogWarning("[PlayerPresetService] Special no es SpecialOnly, se descarta"); special = null; }

        // Evitar duplicados en slots izq/der (limpieza automática, normal en transiciones)
        if (left && right && leftId == rightId) 
        { 
            Debug.Log($"[PlayerPresetService] Duplicado detectado en Left/Right ({leftId}), se limpia automáticamente"); 
            right = null; 
        }

        // Auto-completar slots vacíos (opcional o por fallback si todos estaban None)
        if ((autoFillEmptySlotsFromUnlocked || allNone) && preset.unlockedSpells != null)
        {
            MagicSpellSO FindFirstAvailable(bool requireSpecial, SpellId avoid)
            {
                foreach (var id in preset.unlockedSpells)
                {
                    if (id == SpellId.None || id == avoid) continue;
                    var s = spellLibrary.Get(id);
                    if (!s) continue;
                    if (requireSpecial && s.slotType != SpellSlotType.SpecialOnly) continue;
                    if (!requireSpecial && s.slotType == SpellSlotType.SpecialOnly) continue;
                    return s;
                }
                return null;
            }

            if (!left) left = FindFirstAvailable(false, rightId);
            if (!right) right = FindFirstAvailable(false, leftId);
            if (!special) special = FindFirstAvailable(true, SpellId.None);
        }

        // Aplicar configuración al spawner
        if (_spawner != null)
        {
            _spawner.SetSpells(left, right, special);
            _spawner.SetInstigator(instigatorOverride ? instigatorOverride : gameObject);
        }

        // ← NUEVO: Aplicar configuración al MagicCaster también
        if (_magicCaster)
        {
            _magicCaster.SetSpells(left, right, special);
            //Debug.Log($"[PlayerPresetService] MagicCaster configurado con hechizos del preset");
        }
        else
        {
            Debug.LogWarning("[PlayerPresetService] No se encontró MagicCaster en el GameObject");
        }

        //Debug.Log($"[PlayerPresetService] Hechizos configurados - L:{left?.name} R:{right?.name} S:{special?.name}");
    }

    // === NUEVO: API pública para re-aplicar el preset activo en runtime (incluye mana) ===
    public void ApplyCurrentPreset(bool includeInventory = false, bool includeAbilities = true)
    {
        if (!GameBootService.IsAvailable)
        {
            Debug.LogWarning("[PlayerPresetService] GameBootService aún no está disponible");
            return;
        }
        var preset = GameBootService.Profile.GetActivePresetResolved();
        if (!preset)
        {
            Debug.LogWarning("[PlayerPresetService] No hay preset activo para aplicar");
            return;
        }
        OnPresetApplying?.Invoke();
        HasAppliedPreset = false;

        // Aplicar stats (maná) y luego los hechizos
        ApplyManaFromPreset(preset);
        
        // IMPORTANTE: Solo restaurar inventario cuando cargamos partida o iniciamos nueva
        // NO cuando solo cambiamos equipamiento en runtime (evita perder items recogidos)
        if (includeInventory)
        {
            ApplyInventoryFromPreset(preset);
        }
        
        ConfigureSpells(preset);
        ApplyAppearanceFromPreset(preset);
        ApplyWardrobeFromPreset(preset);

        if (includeAbilities)
        {
            // NUEVO: aplicar abilities al action manager solo cuando se solicita
            if (_actionManager == null)
                _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();

            if (_actionManager != null)
            {
                if (preset.abilities == null)
                {
                    Debug.LogWarning("[PlayerPresetService] preset.abilities es NULL en ApplyCurrentPreset — creando con valores por defecto");
                    preset.abilities = new PlayerAbilities { swim = true, jump = true, climb = true, magic = false, fly = true };
                }
                Debug.Log($"[PlayerPresetService] Re-aplicando abilities: Swim={preset.abilities.swim} Jump={preset.abilities.jump} Climb={preset.abilities.climb} Fly={preset.abilities.fly} Magic={preset.abilities.magic}");
                _actionManager.ApplyAbilities(preset.abilities);
                Debug.Log("[PlayerPresetService] Re-aplicado preset: abilities actualizadas");
            }
        }

        // Notificar a subscriptores que el preset ha sido aplicado (p.ej. UI)
        HasAppliedPreset = true;
        OnPresetApplied?.Invoke();
    }

    // Evento público para notificar re-aplicación de preset (subscriptores UI o sistemas)
    public static System.Action OnPresetApplying;
    public static System.Action OnPresetApplied;
}
