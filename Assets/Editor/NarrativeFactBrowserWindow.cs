using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>
/// Narrative Fact Browser — Vista unificada de TODOS los hechos/estado narrativo.
///
/// Agrega datos de: Blackboard (grafos narrativos), QuestManager, señales/eventos,
/// PlayerPresetSO (flags, abilities, inventario, party, etc.) en una sola vista
/// plana y buscable.
///
/// Características:
/// - Auto-descubrimiento de hechos desde todos los sistemas
/// - Búsqueda y filtrado por fuente, categoría, tipo, tag
/// - Valores en vivo durante Play Mode
/// - Edición de valores en Play Mode
/// - Snapshots: guardar/comparar estado para detectar cambios
/// - Exportar definiciones al NarrativeFactCatalog
///
/// Menú: Tools → Narrativa → Fact Browser
/// </summary>
public class NarrativeFactBrowserWindow : EditorWindow
{
    // ─── Fact representation ───

    private enum FactSource
    {
        Blackboard,
        Quest,
        Signal,
        PlayerState,
        Inventory,
        Party,
        World
    }

    private enum FactValueType
    {
        Bool,
        Int,
        Float,
        String
    }

    private class DiscoveredFact
    {
        public string id;
        public string displayName;
        public FactSource source;
        public FactValueType valueType;
        public string currentValue;
        public string previousValue;
        public string category;
        public string detail;
        public bool changed;
        public bool isEditable;

        public string catalogDescription;
        public List<string> catalogTags;
    }

    // ─── Tab ───

    private enum Tab { Todos, Narrativo, Quests, Eventos, Jugador, Snapshots }

    // ─── State ───

    private Tab _currentTab = Tab.Todos;
    private List<DiscoveredFact> _facts = new List<DiscoveredFact>();
    private List<DiscoveredFact> _filteredFacts = new List<DiscoveredFact>();
    private Vector2 _scrollPos;
    private string _searchFilter = "";
    private FactSource? _sourceFilter = null;
    private bool _onlyChanged;
    private bool _hasScanned;
    private string _scanStatus = "";
    private NarrativeFactCatalog _catalog;
    private double _lastRuntimeRefresh;
    private const double RuntimeRefreshInterval = 0.5;

    // Snapshots
    private Dictionary<string, string> _snapshot;
    private string _snapshotName = "";
    private List<SavedSnapshot> _savedSnapshots = new List<SavedSnapshot>();

    private class SavedSnapshot
    {
        public string name;
        public string timestamp;
        public Dictionary<string, string> data;
    }

    // Sorting
    private enum SortColumn { Id, Source, Category, Value }
    private SortColumn _sortColumn = SortColumn.Id;
    private bool _sortAscending = true;

    // ─── Menu ───

    [MenuItem("El Sendero/Narrativa/Fact Browser")]
    public static void ShowWindow()
    {
        var w = GetWindow<NarrativeFactBrowserWindow>("Fact Browser");
        w.minSize = new Vector2(850, 400);
    }

    // ─── Lifecycle ───

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        TryFindCatalog();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!Application.isPlaying || !_hasScanned) return;
        if (EditorApplication.timeSinceStartup - _lastRuntimeRefresh < RuntimeRefreshInterval) return;
        _lastRuntimeRefresh = EditorApplication.timeSinceStartup;
        RefreshRuntimeValues();
        Repaint();
    }

    private void TryFindCatalog()
    {
        if (_catalog != null) return;
        var guids = AssetDatabase.FindAssets("t:NarrativeFactCatalog");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _catalog = AssetDatabase.LoadAssetAtPath<NarrativeFactCatalog>(path);
        }
    }

    // ─── Scan ───

    private void ScanAllFacts()
    {
        _facts.Clear();
        _scanStatus = "Escaneando...";

        if (Application.isPlaying)
            ScanRuntime();
        else
            ScanAssets();

        MergeCatalogMetadata();
        ApplyFilters();
        _hasScanned = true;
        _scanStatus = $"{_facts.Count} hechos descubiertos";
    }

    private void ScanAssets()
    {
        ScanGraphBlackboardsFromAssets();
        ScanQuestsFromAssets();
        ScanEventsFromAssets();
        ScanPlayerPresetsFromAssets();
    }

    private void ScanRuntime()
    {
        ScanGraphBlackboardsRuntime();
        ScanQuestsRuntime();
        ScanSignalsRuntime();
        ScanPlayerStateRuntime();
        ScanInventoryRuntime();
        ScanPartyRuntime();
        ScanWorldStateRuntime();
    }

    // ─── Asset scanning (Edit Mode) ───

    private void ScanGraphBlackboardsFromAssets()
    {
        var guids = AssetDatabase.FindAssets("t:NarrativeGraph");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var graph = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(path);
            if (graph == null || graph.nodes == null) continue;

            foreach (var node in graph.nodes)
            {
                if (node == null) continue;
                var typeName = node.GetType().Name;

                // Extract blackboard keys from node fields
                if (typeName == "WaitCustomEventNode" || typeName == "RaiseCustomEventNode")
                {
                    var eventKey = GetFieldString(node, "eventKey");
                    if (!string.IsNullOrEmpty(eventKey))
                    {
                        var prefix = typeName == "WaitCustomEventNode" ? "wait" : "raise";
                        AddFact($"event.{eventKey}",
                            eventKey,
                            FactSource.Signal,
                            FactValueType.Bool,
                            prefix == "raise" ? "Emitido por grafo" : "Esperado en grafo",
                            "Evento",
                            $"Grafo: {graph.name}, Nodo: {node.displayTitle}");
                    }
                }
                else if (typeName == "StartQuestNode")
                {
                    var questId = GetFieldString(node, "questId");
                    if (!string.IsNullOrEmpty(questId))
                        AddFact($"quest.{questId}.state", questId, FactSource.Quest, FactValueType.String, "Inactive", "Quest", $"Iniciada desde grafo: {graph.name}");
                }
                else if (typeName == "CompleteQuestStepsNode")
                {
                    var questId = GetFieldString(node, "questId");
                    if (!string.IsNullOrEmpty(questId))
                        AddFact($"quest.{questId}.steps", questId + " (pasos)", FactSource.Quest, FactValueType.String, "—", "Quest", $"Pasos completados desde grafo: {graph.name}");
                }
                else if (typeName == "WaitQuestCompleteNode")
                {
                    var questId = GetFieldString(node, "questId");
                    if (!string.IsNullOrEmpty(questId))
                        AddFact($"quest.{questId}.completed", questId + " completada", FactSource.Quest, FactValueType.Bool, "false", "Quest", $"Esperada en grafo: {graph.name}");
                }

                // Blackboard keys used by any node
                if (!string.IsNullOrEmpty(node.guid))
                {
                    AddFact($"bb.__currentNodeGuid",
                        "Nodo actual (" + graph.name + ")",
                        FactSource.Blackboard,
                        FactValueType.String,
                        "—",
                        "Narrativo",
                        "GUID del nodo en ejecución actual");
                }
            }
        }
    }

    private void ScanQuestsFromAssets()
    {
        var guids = AssetDatabase.FindAssets("t:QuestData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
            if (quest == null) continue;

            AddFact($"quest.{quest.questId}.state",
                quest.displayName ?? quest.questId,
                FactSource.Quest,
                FactValueType.String,
                "Inactive",
                "Quest",
                quest.description ?? "");

            if (quest.steps != null)
            {
                for (int i = 0; i < quest.steps.Length; i++)
                {
                    var step = quest.steps[i];
                    AddFact($"quest.{quest.questId}.step_{i}",
                        $"{quest.questId} paso {i}: {step.description}",
                        FactSource.Quest,
                        FactValueType.Bool,
                        "false",
                        "Quest",
                        step.conditionId ?? "");
                }
            }
        }
    }

    private void ScanEventsFromAssets()
    {
        // FIX INC-033: NPCNarrativeConfig (módulo narrativo obsoleto) fue eliminado por ser
        // redundante con NPCInteractiveNarrativeConfig, que es el módulo real que usan los NPCs
        // hoy en día (ver NPCBehaviourManagerV2). Actualizamos el escaneo para que apunte al
        // módulo vigente en vez de a un tipo que ya no existe.
        var npcConfigs = AssetDatabase.FindAssets("t:NPCInteractiveNarrativeConfig");
        foreach (var guid in npcConfigs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (config == null) continue;

            // Use reflection to find event keys in NPC configs
            ScanObjectForEventKeys(config, "NPC: " + config.name);
        }
    }

    private void ScanPlayerPresetsFromAssets()
    {
        var guids = AssetDatabase.FindAssets("t:PlayerPresetSO");
        if (guids.Length == 0) return;

        // Use first preset as reference for discoverable facts
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var preset = AssetDatabase.LoadAssetAtPath<PlayerPresetSO>(path);
        if (preset == null) return;

        // Stats
        AddFact("player.level", "Nivel", FactSource.PlayerState, FactValueType.Int, preset.level.ToString(), "Jugador", "");
        AddFact("player.hp", "HP actual", FactSource.PlayerState, FactValueType.Float, preset.currentHP.ToString("F0"), "Jugador", "");
        AddFact("player.maxHp", "HP máximo", FactSource.PlayerState, FactValueType.Float, preset.maxHP.ToString("F0"), "Jugador", "");
        AddFact("player.mp", "MP actual", FactSource.PlayerState, FactValueType.Float, preset.currentMP.ToString("F0"), "Jugador", "");
        AddFact("player.maxMp", "MP máximo", FactSource.PlayerState, FactValueType.Float, preset.maxMP.ToString("F0"), "Jugador", "");

        // Abilities
        AddFact("player.canSwim", "Puede nadar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.swim.ToString(), "Habilidad", "");
        AddFact("player.canJump", "Puede saltar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.jump.ToString(), "Habilidad", "");
        AddFact("player.canClimb", "Puede escalar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.climb.ToString(), "Habilidad", "");
        AddFact("player.canFly", "Puede volar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.fly.ToString(), "Habilidad", "");
        AddFact("player.canMagic", "Puede usar magia", FactSource.PlayerState, FactValueType.Bool, preset.abilities.magic.ToString(), "Habilidad", "");

        // Spells
        foreach (SpellId spell in Enum.GetValues(typeof(SpellId)))
        {
            if (spell == SpellId.None) continue;
            bool has = preset.unlockedSpells != null && preset.unlockedSpells.Contains(spell);
            AddFact($"player.spell.{spell}", $"Hechizo: {spell}", FactSource.PlayerState, FactValueType.Bool, has.ToString(), "Hechizo", "");
        }

        // Flags
        if (preset.flags != null)
        {
            foreach (var flag in preset.flags)
                AddFact($"flag.{flag}", flag, FactSource.PlayerState, FactValueType.Bool, "true", "Flag", "Flag del preset");
        }

        // Spawn
        AddFact("player.spawnAnchor", "Punto de aparición", FactSource.PlayerState, FactValueType.String, preset.spawnAnchorId ?? "", "Mundo", "");
    }

    // ─── Runtime scanning (Play Mode) ───

    private void ScanGraphBlackboardsRuntime()
    {
        if (NarrativeGraphHub.Instance == null) return;
        var runners = NarrativeGraphHub.Instance.GetAllRunners();
        if (runners == null) return;

        foreach (var runner in runners)
        {
            if (runner == null || runner.Blackboard == null) continue;
            var graphName = runner.graph != null ? runner.graph.name : runner.gameObject.name;

            var entries = runner.Blackboard.ExportToSerializable();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.key)) continue;
                var vt = ParseValueType(entry.type);
                AddFact($"bb.{graphName}.{entry.key}",
                    $"[{graphName}] {entry.key}",
                    FactSource.Blackboard,
                    vt,
                    entry.value ?? "",
                    "Narrativo",
                    $"Blackboard del grafo {graphName}",
                    isEditable: true);
            }

            // Current node
            if (runner.CurrentNode != null)
            {
                AddFact($"bb.{graphName}.__runningNode",
                    $"[{graphName}] Nodo actual",
                    FactSource.Blackboard,
                    FactValueType.String,
                    $"{runner.CurrentNode.GetType().Name}: {runner.CurrentNode.displayTitle}",
                    "Narrativo",
                    "Nodo en ejecución");
            }
        }
    }

    private void ScanQuestsRuntime()
    {
        if (QuestManager.Instance == null) return;

        foreach (var rq in QuestManager.Instance.GetAll())
        {
            AddFact($"quest.{rq.Id}.state",
                rq.Data.displayName ?? rq.Id,
                FactSource.Quest,
                FactValueType.String,
                rq.State.ToString(),
                "Quest",
                rq.Data.description ?? "",
                isEditable: true);

            if (rq.Steps != null)
            {
                for (int i = 0; i < rq.Steps.Length; i++)
                {
                    AddFact($"quest.{rq.Id}.step_{i}",
                        $"{rq.Id} paso {i}: {rq.Steps[i].description}",
                        FactSource.Quest,
                        FactValueType.Bool,
                        rq.Steps[i].completed.ToString(),
                        "Quest",
                        rq.Steps[i].conditionId ?? "");
                }
            }
        }
    }

    private void ScanSignalsRuntime()
    {
        if (DefaultNarrativeSignals.Instance == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var signals = DefaultNarrativeSignals.Instance;

        // Raised events
        foreach (var key in signals.CurrentRaised)
        {
            AddFact($"signal.raised.{key}",
                $"Evento: {key}",
                FactSource.Signal,
                FactValueType.Bool,
                "true",
                "Evento",
                "Evento emitido (raised)");
        }

        // Pending events
        foreach (var key in signals.CurrentPending)
        {
            AddFact($"signal.pending.{key}",
                $"Evento pendiente: {key}",
                FactSource.Signal,
                FactValueType.Bool,
                "true",
                "Evento",
                "Evento pendiente (sin oyentes)");
        }

        // Active subscribers
        foreach (var kvp in signals.CurrentSubscribers)
        {
            if (!_facts.Exists(f => f.id.Contains(kvp.Key)))
            {
                AddFact($"signal.listening.{kvp.Key}",
                    $"Escuchando: {kvp.Key}",
                    FactSource.Signal,
                    FactValueType.Bool,
                    "true",
                    "Evento",
                    "Evento con listener activo");
            }
        }

        // Signal history
        if (DefaultNarrativeSignals.History.Count > 0)
        {
            var recentKeys = new HashSet<string>();
            for (int i = DefaultNarrativeSignals.History.Count - 1; i >= 0 && recentKeys.Count < 20; i--)
            {
                var record = DefaultNarrativeSignals.History[i];
                if (recentKeys.Add(record.key) && !_facts.Exists(f => f.id == $"signal.raised.{record.key}" || f.id == $"signal.pending.{record.key}"))
                {
                    AddFact($"signal.history.{record.key}",
                        $"Evento (historial): {record.key}",
                        FactSource.Signal,
                        FactValueType.String,
                        record.status.ToString(),
                        "Evento",
                        $"Último estado: {record.status} (t={record.time:F1}s)");
                }
            }
        }
#endif
    }

    private void ScanPlayerStateRuntime()
    {
        var bootProfile = FindBootProfile();
        if (bootProfile == null) return;

        var getPreset = bootProfile.GetType().GetMethod("GetActivePresetResolved",
            BindingFlags.Public | BindingFlags.Instance);
        if (getPreset == null) return;

        var preset = getPreset.Invoke(bootProfile, null) as PlayerPresetSO;
        if (preset == null) return;

        AddFact("player.level", "Nivel", FactSource.PlayerState, FactValueType.Int, preset.level.ToString(), "Jugador", "", isEditable: true);
        AddFact("player.hp", "HP actual", FactSource.PlayerState, FactValueType.Float, preset.currentHP.ToString("F1"), "Jugador", "", isEditable: true);
        AddFact("player.maxHp", "HP máximo", FactSource.PlayerState, FactValueType.Float, preset.maxHP.ToString("F1"), "Jugador", "", isEditable: true);
        AddFact("player.mp", "MP actual", FactSource.PlayerState, FactValueType.Float, preset.currentMP.ToString("F1"), "Jugador", "", isEditable: true);
        AddFact("player.maxMp", "MP máximo", FactSource.PlayerState, FactValueType.Float, preset.maxMP.ToString("F1"), "Jugador", "", isEditable: true);
        AddFact("player.spawnAnchor", "Spawn anchor", FactSource.PlayerState, FactValueType.String, preset.spawnAnchorId ?? "", "Mundo", "");

        // Abilities
        if (preset.abilities != null)
        {
            AddFact("player.canSwim", "Puede nadar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.swim.ToString(), "Habilidad", "", isEditable: true);
            AddFact("player.canJump", "Puede saltar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.jump.ToString(), "Habilidad", "", isEditable: true);
            AddFact("player.canClimb", "Puede escalar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.climb.ToString(), "Habilidad", "", isEditable: true);
            AddFact("player.canFly", "Puede volar", FactSource.PlayerState, FactValueType.Bool, preset.abilities.fly.ToString(), "Habilidad", "", isEditable: true);
            AddFact("player.canMagic", "Puede usar magia", FactSource.PlayerState, FactValueType.Bool, preset.abilities.magic.ToString(), "Habilidad", "", isEditable: true);
        }

        // Spells
        if (preset.unlockedSpells != null)
        {
            foreach (SpellId spell in Enum.GetValues(typeof(SpellId)))
            {
                if (spell == SpellId.None) continue;
                AddFact($"player.spell.{spell}", $"Hechizo: {spell}", FactSource.PlayerState, FactValueType.Bool,
                    preset.unlockedSpells.Contains(spell).ToString(), "Hechizo", "");
            }
        }

        // Flags
        if (preset.flags != null)
        {
            foreach (var flag in preset.flags)
                AddFact($"flag.{flag}", flag, FactSource.PlayerState, FactValueType.Bool, "true", "Flag", "");
        }

        // Defeated bosses
        if (preset.defeatedBossIds != null)
        {
            foreach (var bossId in preset.defeatedBossIds)
                AddFact($"world.boss.{bossId}", $"Boss derrotado: {bossId}", FactSource.World, FactValueType.Bool, "true", "Mundo", "");
        }

        // Unlocked teleport points
        if (preset.unlockedTeleportPoints != null)
        {
            foreach (var tp in preset.unlockedTeleportPoints)
                AddFact($"world.teleport.{tp}", $"Teleport: {tp}", FactSource.World, FactValueType.Bool, "true", "Mundo", "");
        }

        // Consumed interactables
        if (preset.consumedInteractableIds != null)
        {
            foreach (var id in preset.consumedInteractableIds)
                AddFact($"world.consumed.{id}", $"Interactuable consumido: {id}", FactSource.World, FactValueType.Bool, "true", "Mundo", "");
        }

        // Seen lore popups
        if (preset.seenLorePopupIds != null)
        {
            foreach (var id in preset.seenLorePopupIds)
                AddFact($"world.lore.{id}", $"Lore visto: {id}", FactSource.World, FactValueType.Bool, "true", "Mundo", "");
        }

        // Completed interactive narratives
        if (preset.completedInteractiveNarratives != null)
        {
            foreach (var id in preset.completedInteractiveNarratives)
                AddFact($"narrative.completed.{id}", $"Narrativa completada: {id}", FactSource.PlayerState, FactValueType.Bool, "true", "Narrativo", "");
        }
    }

    private void ScanInventoryRuntime()
    {
        if (!PlayerService.TryGetComponent(out Inventory inventory, includeInactive: true, allowSceneLookup: true))
            return;

        var items = inventory.GetAllItems();
        foreach (var entry in items)
        {
            var itemId = entry.item != null ? entry.item.itemId : "unknown";
            var itemName = entry.item != null ? entry.item.displayName : itemId;
            AddFact($"inventory.{itemId}",
                $"Item: {itemName}",
                FactSource.Inventory,
                FactValueType.Int,
                entry.count.ToString(),
                "Inventario",
                "");
        }
    }

    private void ScanPartyRuntime()
    {
        var partyObj = UnityEngine.Object.FindAnyObjectByType<Game.NPC.PlayerParty>();
        if (partyObj == null) return;

        var members = partyObj.Members;
        if (members == null) return;

        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member == null) continue;
            var memberId = member.gameObject.name;
            AddFact($"party.member.{memberId}",
                $"Party: {memberId}",
                FactSource.Party,
                FactValueType.Bool,
                "true",
                "Party",
                $"Miembro {i} del equipo");
        }

        AddFact("party.count", "Miembros en party", FactSource.Party, FactValueType.Int, members.Count.ToString(), "Party", "");
    }

    private void ScanWorldStateRuntime()
    {
        // Spawn anchor
        var currentAnchor = typeof(SpawnManager).GetProperty("CurrentAnchorId",
            BindingFlags.Public | BindingFlags.Static);
        if (currentAnchor != null)
        {
            var val = currentAnchor.GetValue(null) as string;
            AddFact("world.currentAnchor", "Anchor actual", FactSource.World, FactValueType.String, val ?? "—", "Mundo", "");
        }
    }

    // ─── Runtime refresh ───

    private void RefreshRuntimeValues()
    {
        if (!Application.isPlaying) return;

        var previousValues = new Dictionary<string, string>();
        foreach (var f in _facts)
            previousValues[f.id] = f.currentValue;

        _facts.Clear();
        ScanRuntime();
        MergeCatalogMetadata();

        // Detect changes
        foreach (var f in _facts)
        {
            if (previousValues.TryGetValue(f.id, out var prev))
            {
                f.previousValue = prev;
                f.changed = f.currentValue != prev;
            }
        }

        ApplyFilters();
    }

    // ─── Catalog integration ───

    private void MergeCatalogMetadata()
    {
        if (_catalog == null) return;

        foreach (var fact in _facts)
        {
            var def = _catalog.FindDefinition(fact.id);
            if (def == null) continue;
            if (!string.IsNullOrEmpty(def.displayName)) fact.displayName = def.displayName;
            if (!string.IsNullOrEmpty(def.description)) fact.catalogDescription = def.description;
            if (def.tags != null && def.tags.Count > 0) fact.catalogTags = def.tags;
        }
    }

    // ─── Helpers ───

    private void AddFact(string id, string displayName, FactSource source, FactValueType valueType,
        string currentValue, string category, string detail, bool isEditable = false)
    {
        // Avoid duplicates
        var existing = _facts.Find(f => f.id == id);
        if (existing != null)
        {
            // Update value if runtime
            if (Application.isPlaying)
                existing.currentValue = currentValue;
            return;
        }

        _facts.Add(new DiscoveredFact
        {
            id = id,
            displayName = displayName,
            source = source,
            valueType = valueType,
            currentValue = currentValue,
            category = category,
            detail = detail,
            isEditable = isEditable && Application.isPlaying
        });
    }

    private void ScanObjectForEventKeys(UnityEngine.Object obj, string context)
    {
        if (obj == null) return;
        var serialized = new SerializedObject(obj);
        var prop = serialized.GetIterator();
        while (prop.NextVisible(true))
        {
            if (prop.propertyType == SerializedPropertyType.String && prop.name.ToLower().Contains("event"))
            {
                var val = prop.stringValue;
                if (!string.IsNullOrEmpty(val) && val.StartsWith("EVT"))
                {
                    AddFact($"event.{val}", val, FactSource.Signal, FactValueType.Bool, "—", "Evento", context);
                }
            }
        }
    }

    private static string GetFieldString(object obj, string fieldName)
    {
        if (obj == null) return null;
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(obj) as string;
    }

    private static FactValueType ParseValueType(string type)
    {
        switch (type)
        {
            case "bool": return FactValueType.Bool;
            case "int": return FactValueType.Int;
            case "float": return FactValueType.Float;
            default: return FactValueType.String;
        }
    }

    private static UnityEngine.Object FindBootProfile()
    {
        var type = Type.GetType("GameBootService, Assembly-CSharp");
        if (type == null) return null;
        var profileProp = type.GetProperty("Profile", BindingFlags.Public | BindingFlags.Static);
        if (profileProp == null) return null;
        return profileProp.GetValue(null) as UnityEngine.Object;
    }

    // ─── Filtering ───

    private void ApplyFilters()
    {
        _filteredFacts = _facts;

        // Tab filter
        switch (_currentTab)
        {
            case Tab.Narrativo:
                _filteredFacts = _filteredFacts.Where(f => f.source == FactSource.Blackboard || f.category == "Narrativo").ToList();
                break;
            case Tab.Quests:
                _filteredFacts = _filteredFacts.Where(f => f.source == FactSource.Quest).ToList();
                break;
            case Tab.Eventos:
                _filteredFacts = _filteredFacts.Where(f => f.source == FactSource.Signal).ToList();
                break;
            case Tab.Jugador:
                _filteredFacts = _filteredFacts.Where(f =>
                    f.source == FactSource.PlayerState ||
                    f.source == FactSource.Inventory ||
                    f.source == FactSource.Party).ToList();
                break;
            case Tab.Snapshots:
                break;
        }

        // Source filter
        if (_sourceFilter.HasValue)
            _filteredFacts = _filteredFacts.Where(f => f.source == _sourceFilter.Value).ToList();

        // Search
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            var filter = _searchFilter.ToLowerInvariant();
            _filteredFacts = _filteredFacts.Where(f =>
                (f.id != null && f.id.ToLowerInvariant().Contains(filter)) ||
                (f.displayName != null && f.displayName.ToLowerInvariant().Contains(filter)) ||
                (f.currentValue != null && f.currentValue.ToLowerInvariant().Contains(filter)) ||
                (f.category != null && f.category.ToLowerInvariant().Contains(filter)) ||
                (f.detail != null && f.detail.ToLowerInvariant().Contains(filter))
            ).ToList();
        }

        // Only changed
        if (_onlyChanged)
            _filteredFacts = _filteredFacts.Where(f => f.changed).ToList();

        // Sort
        switch (_sortColumn)
        {
            case SortColumn.Id:
                _filteredFacts = _sortAscending
                    ? _filteredFacts.OrderBy(f => f.id).ToList()
                    : _filteredFacts.OrderByDescending(f => f.id).ToList();
                break;
            case SortColumn.Source:
                _filteredFacts = _sortAscending
                    ? _filteredFacts.OrderBy(f => f.source).ThenBy(f => f.id).ToList()
                    : _filteredFacts.OrderByDescending(f => f.source).ThenBy(f => f.id).ToList();
                break;
            case SortColumn.Category:
                _filteredFacts = _sortAscending
                    ? _filteredFacts.OrderBy(f => f.category).ThenBy(f => f.id).ToList()
                    : _filteredFacts.OrderByDescending(f => f.category).ThenBy(f => f.id).ToList();
                break;
            case SortColumn.Value:
                _filteredFacts = _sortAscending
                    ? _filteredFacts.OrderBy(f => f.currentValue).ToList()
                    : _filteredFacts.OrderByDescending(f => f.currentValue).ToList();
                break;
        }
    }

    // ─── Snapshots ───

    private void TakeSnapshot(string name)
    {
        var snapshot = new SavedSnapshot
        {
            name = string.IsNullOrEmpty(name) ? $"Snapshot {_savedSnapshots.Count + 1}" : name,
            timestamp = DateTime.Now.ToString("HH:mm:ss"),
            data = new Dictionary<string, string>()
        };

        foreach (var f in _facts)
            snapshot.data[f.id] = f.currentValue;

        _savedSnapshots.Add(snapshot);
        _snapshot = snapshot.data;
    }

    private void CompareWithSnapshot(SavedSnapshot snapshot)
    {
        _snapshot = snapshot.data;
        foreach (var f in _facts)
        {
            if (_snapshot.TryGetValue(f.id, out var snapshotVal))
            {
                f.previousValue = snapshotVal;
                f.changed = f.currentValue != snapshotVal;
            }
            else
            {
                f.previousValue = "—";
                f.changed = true;
            }
        }
        _onlyChanged = true;
        ApplyFilters();
    }

    // ─── GUI ───

    private void OnGUI()
    {
        DrawToolbar();
        DrawTabs();

        if (!_hasScanned)
        {
            EditorGUILayout.HelpBox(
                "Pulsa 'Escanear hechos' para descubrir todos los hechos narrativos del proyecto.\n" +
                "En Play Mode se escanean los valores runtime en vivo.",
                MessageType.Info);
            return;
        }

        if (_currentTab == Tab.Snapshots)
        {
            DrawSnapshotsTab();
            return;
        }

        DrawFactTable();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Escanear hechos", EditorStyles.toolbarButton, GUILayout.Width(110)))
            ScanAllFacts();

        GUILayout.Space(4);

        var oldSearch = _searchFilter;
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        if (_searchFilter != oldSearch) ApplyFilters();

        GUILayout.Space(4);

        // Source filter dropdown
        var sourceNames = new string[] { "Todas", "Blackboard", "Quest", "Signal", "PlayerState", "Inventory", "Party", "World" };
        int currentSource = _sourceFilter.HasValue ? (int)_sourceFilter.Value + 1 : 0;
        var newSource = EditorGUILayout.Popup(currentSource, sourceNames, EditorStyles.toolbarPopup, GUILayout.Width(100));
        if (newSource != currentSource)
        {
            _sourceFilter = newSource == 0 ? (FactSource?)null : (FactSource)(newSource - 1);
            ApplyFilters();
        }

        GUILayout.Space(4);

        var oldChanged = _onlyChanged;
        _onlyChanged = GUILayout.Toggle(_onlyChanged, "Solo cambios", EditorStyles.toolbarButton, GUILayout.Width(90));
        if (_onlyChanged != oldChanged) ApplyFilters();

        GUILayout.FlexibleSpace();

        GUILayout.Label($"{_filteredFacts.Count}/{_facts.Count}", EditorStyles.toolbarButton);

        if (Application.isPlaying)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(0.3f, 0.9f, 0.3f);
            GUILayout.Label("● LIVE", EditorStyles.toolbarButton);
            GUI.color = oldColor;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal();
        foreach (Tab t in Enum.GetValues(typeof(Tab)))
        {
            var style = t == _currentTab
                ? new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold }
                : EditorStyles.toolbarButton;
            if (GUILayout.Button(t.ToString(), style))
            {
                _currentTab = t;
                ApplyFilters();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawFactTable()
    {
        // Column headers
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        DrawSortableHeader("Hecho", SortColumn.Id, 250);
        DrawSortableHeader("Fuente", SortColumn.Source, 80);
        DrawSortableHeader("Categoría", SortColumn.Category, 80);
        DrawSortableHeader("Valor", SortColumn.Value, 150);
        GUILayout.Label("Detalle", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = 0; i < _filteredFacts.Count; i++)
        {
            var fact = _filteredFacts[i];
            DrawFactRow(fact, i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSortableHeader(string label, SortColumn column, float width)
    {
        var suffix = _sortColumn == column ? (_sortAscending ? " ▲" : " ▼") : "";
        if (GUILayout.Button(label + suffix, EditorStyles.toolbarButton, GUILayout.Width(width)))
        {
            if (_sortColumn == column) _sortAscending = !_sortAscending;
            else { _sortColumn = column; _sortAscending = true; }
            ApplyFilters();
        }
    }

    private void DrawFactRow(DiscoveredFact fact, int index)
    {
        var bgColor = index % 2 == 0
            ? new Color(0.22f, 0.22f, 0.22f)
            : new Color(0.25f, 0.25f, 0.25f);

        if (fact.changed)
            bgColor = new Color(0.35f, 0.30f, 0.15f);

        var rect = EditorGUILayout.BeginHorizontal();
        EditorGUI.DrawRect(rect, bgColor);

        // Name
        var nameStyle = new GUIStyle(EditorStyles.miniLabel);
        if (fact.changed) nameStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);
        GUILayout.Label(fact.displayName, nameStyle, GUILayout.Width(250));

        // Source
        var sourceColor = GetSourceColor(fact.source);
        var sourceStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = sourceColor } };
        GUILayout.Label(fact.source.ToString(), sourceStyle, GUILayout.Width(80));

        // Category
        GUILayout.Label(fact.category, EditorStyles.miniLabel, GUILayout.Width(80));

        // Value
        DrawValueCell(fact);

        // Detail / description
        var detailText = !string.IsNullOrEmpty(fact.catalogDescription) ? fact.catalogDescription : fact.detail;
        GUILayout.Label(detailText, EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawValueCell(DiscoveredFact fact)
    {
        var valueStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };

        // Color-code booleans
        if (fact.valueType == FactValueType.Bool)
        {
            bool val = fact.currentValue == "True" || fact.currentValue == "true" || fact.currentValue == "1";
            valueStyle.normal.textColor = val ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.7f, 0.4f, 0.4f);
        }

        if (fact.changed && !string.IsNullOrEmpty(fact.previousValue))
        {
            // Show old → new
            var changeText = $"{fact.previousValue} → {fact.currentValue}";
            valueStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
            GUILayout.Label(changeText, valueStyle, GUILayout.Width(150));
        }
        else
        {
            GUILayout.Label(fact.currentValue ?? "—", valueStyle, GUILayout.Width(150));
        }
    }

    private void DrawSnapshotsTab()
    {
        EditorGUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        _snapshotName = EditorGUILayout.TextField("Nombre:", _snapshotName, GUILayout.Width(300));
        if (GUILayout.Button("Tomar Snapshot", GUILayout.Width(120)))
        {
            if (_hasScanned) TakeSnapshot(_snapshotName);
            _snapshotName = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        if (_savedSnapshots.Count == 0)
        {
            EditorGUILayout.HelpBox("No hay snapshots guardados. Escanea hechos y luego toma un snapshot para guardar el estado actual.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Snapshots guardados:", EditorStyles.boldLabel);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = _savedSnapshots.Count - 1; i >= 0; i--)
        {
            var snap = _savedSnapshots[i];
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField($"{snap.name} ({snap.timestamp})", GUILayout.Width(250));
            EditorGUILayout.LabelField($"{snap.data.Count} hechos", GUILayout.Width(80));

            if (GUILayout.Button("Comparar con actual", GUILayout.Width(140)))
            {
                if (_hasScanned)
                {
                    _currentTab = Tab.Todos;
                    CompareWithSnapshot(snap);
                }
            }

            if (GUILayout.Button("Eliminar", GUILayout.Width(70)))
            {
                _savedSnapshots.RemoveAt(i);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8);

        // Export catalog button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Exportar hechos descubiertos al catálogo", GUILayout.Width(280)))
        {
            ExportToCatalog();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // ─── Export to Catalog ───

    private void ExportToCatalog()
    {
        if (_facts.Count == 0) return;

        if (_catalog == null)
        {
            // Create new catalog
            var path = EditorUtility.SaveFilePanelInProject(
                "Guardar catálogo de hechos",
                "NarrativeFactCatalog",
                "asset",
                "Elige dónde guardar el catálogo de hechos");

            if (string.IsNullOrEmpty(path)) return;

            _catalog = ScriptableObject.CreateInstance<NarrativeFactCatalog>();
            AssetDatabase.CreateAsset(_catalog, path);
        }

        Undo.RecordObject(_catalog, "Export facts to catalog");

        int added = 0;
        foreach (var fact in _facts)
        {
            if (_catalog.FindDefinition(fact.id) != null) continue;

            var def = new NarrativeFactCatalog.FactDefinition
            {
                factId = fact.id,
                displayName = fact.displayName,
                category = MapCategory(fact.category),
                factType = MapValueType(fact.valueType),
                source = MapSource(fact.source),
                description = fact.detail ?? ""
            };
            _catalog.definitions.Add(def);
            added++;
        }

        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FactBrowser] Exportados {added} hechos nuevos al catálogo ({_catalog.name}). Total: {_catalog.definitions.Count}");
    }

    private static NarrativeFactCatalog.FactCategory MapCategory(string category)
    {
        switch (category)
        {
            case "Narrativo": return NarrativeFactCatalog.FactCategory.Narrative;
            case "Quest": return NarrativeFactCatalog.FactCategory.Quest;
            case "Evento": return NarrativeFactCatalog.FactCategory.Event;
            case "Habilidad": return NarrativeFactCatalog.FactCategory.Ability;
            case "Hechizo": return NarrativeFactCatalog.FactCategory.Ability;
            case "Inventario": return NarrativeFactCatalog.FactCategory.Inventory;
            case "Flag": return NarrativeFactCatalog.FactCategory.Flag;
            case "Mundo": return NarrativeFactCatalog.FactCategory.World;
            case "Party": return NarrativeFactCatalog.FactCategory.NPC;
            case "Jugador": return NarrativeFactCatalog.FactCategory.Custom;
            default: return NarrativeFactCatalog.FactCategory.Custom;
        }
    }

    private static NarrativeFactCatalog.FactType MapValueType(FactValueType vt)
    {
        switch (vt)
        {
            case FactValueType.Bool: return NarrativeFactCatalog.FactType.Bool;
            case FactValueType.Int: return NarrativeFactCatalog.FactType.Int;
            case FactValueType.Float: return NarrativeFactCatalog.FactType.Float;
            default: return NarrativeFactCatalog.FactType.String;
        }
    }

    private static NarrativeFactCatalog.FactSource MapSource(FactSource source)
    {
        switch (source)
        {
            case FactSource.Blackboard: return NarrativeFactCatalog.FactSource.Blackboard;
            case FactSource.Quest: return NarrativeFactCatalog.FactSource.QuestManager;
            case FactSource.Signal: return NarrativeFactCatalog.FactSource.Signals;
            case FactSource.PlayerState: return NarrativeFactCatalog.FactSource.PlayerState;
            case FactSource.Inventory: return NarrativeFactCatalog.FactSource.PlayerState;
            case FactSource.Party: return NarrativeFactCatalog.FactSource.PlayerState;
            case FactSource.World: return NarrativeFactCatalog.FactSource.PlayerState;
            default: return NarrativeFactCatalog.FactSource.Custom;
        }
    }

    // ─── Colors ───

    private static Color GetSourceColor(FactSource source)
    {
        switch (source)
        {
            case FactSource.Blackboard: return new Color(0.30f, 0.65f, 0.90f);
            case FactSource.Quest: return new Color(0.95f, 0.75f, 0.20f);
            case FactSource.Signal: return new Color(0.95f, 0.55f, 0.35f);
            case FactSource.PlayerState: return new Color(0.40f, 0.80f, 0.70f);
            case FactSource.Inventory: return new Color(0.65f, 0.75f, 0.40f);
            case FactSource.Party: return new Color(0.70f, 0.45f, 0.85f);
            case FactSource.World: return new Color(0.60f, 0.60f, 0.45f);
            default: return Color.white;
        }
    }
}
