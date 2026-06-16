using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.NPC.Modules;

/// <summary>
/// Ventana de editor que escanea TODOS los ScriptableObjects del proyecto
/// y genera un mapa de referencias cruzadas para eventos narrativos, diálogos,
/// quests y desbloqueos. Detecta eventos huérfanos, claves de localización
/// faltantes, y diálogos sin usar.
///
/// Menú: Tools → Narrativa → Cross-Reference
/// </summary>
public class NarrativeCrossReferenceWindow : EditorWindow
{
    // ─── Data structures ───

    private enum Tab { Eventos, Dialogos, Quests, Desbloqueos }

    private class EventReference
    {
        public string eventKey;
        public readonly List<EventSource> emitters = new List<EventSource>();
        public readonly List<EventSource> consumers = new List<EventSource>();
    }

    private class EventSource
    {
        public string description;
        public Object asset;
    }

    private class DialogueInfo
    {
        public DialogueAsset asset;
        public string[] speakerIds;
        public int lineCount;
        public readonly List<string> usedBy = new List<string>();
        public bool hasAllKeysES;
        public bool hasAllKeysEN;
        public List<string> missingKeys = new List<string>();
    }

    private class QuestInfo
    {
        public QuestData quest;
        public string npcOwner;
        public readonly List<string> graphReferences = new List<string>();
        public readonly List<string> conditionReferences = new List<string>();
    }

    private class UnlockInfo
    {
        public string unlockName;
        public string unlockType; // "Ability", "Spell", "Wardrobe"
        public readonly List<string> sources = new List<string>();
    }

    // ─── State ───

    private Tab _currentTab = Tab.Eventos;
    private Dictionary<string, EventReference> _events;
    private List<DialogueInfo> _dialogues;
    private List<QuestInfo> _quests;
    private List<UnlockInfo> _unlocks;
    private Vector2 _scrollPos;
    private string _searchFilter = "";
    private bool _showOnlyWarnings;
    private bool _hasScanned;
    private string _scanStatus = "";

    [MenuItem("El Sendero/Narrativa/Cross-Reference")]
    public static void ShowWindow()
    {
        var w = GetWindow<NarrativeCrossReferenceWindow>("Narrative Cross-Reference");
        w.minSize = new Vector2(800, 500);
    }

    void OnEnable()
    {
        if (!_hasScanned)
            ScanProject();
    }

    void OnGUI()
    {
        DrawToolbar();
        DrawTabs();
        DrawContent();
    }

    // ─── Toolbar ───

    private void DrawToolbar()
    {
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Re-escanear proyecto", EditorStyles.toolbarButton, GUILayout.Width(150)))
        {
            ScanProject();
        }

        GUILayout.Space(8);
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

        GUILayout.Space(8);
        _showOnlyWarnings = GUILayout.Toggle(_showOnlyWarnings, "Solo warnings", EditorStyles.toolbarButton, GUILayout.Width(100));

        GUILayout.FlexibleSpace();

        if (!string.IsNullOrEmpty(_scanStatus))
        {
            GUILayout.Label(_scanStatus, EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal();
        var tabNames = new[] { "Eventos", "Diálogos", "Quests", "Desbloqueos" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            var tab = (Tab)i;
            bool selected = _currentTab == tab;
            var style = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12
            };
            if (GUILayout.Toggle(selected, tabNames[i], style, GUILayout.Height(24)))
            {
                _currentTab = tab;
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawContent()
    {
        if (!_hasScanned)
        {
            EditorGUILayout.HelpBox("Pulsa 'Re-escanear proyecto' para analizar.", MessageType.Info);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case Tab.Eventos: DrawEventsTab(); break;
            case Tab.Dialogos: DrawDialoguesTab(); break;
            case Tab.Quests: DrawQuestsTab(); break;
            case Tab.Desbloqueos: DrawUnlocksTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    // ─── Events Tab ───

    private void DrawEventsTab()
    {
        if (_events == null) return;

        var sorted = _events.Values
            .Where(e => MatchesFilter(e.eventKey))
            .Where(e => !_showOnlyWarnings || e.emitters.Count == 0 || e.consumers.Count == 0)
            .OrderBy(e => e.eventKey)
            .ToList();

        EditorGUILayout.LabelField($"{sorted.Count} eventos encontrados", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var evt in sorted)
        {
            DrawEventEntry(evt);
        }
    }

    private void DrawEventEntry(EventReference evt)
    {
        bool hasWarning = evt.emitters.Count == 0 || evt.consumers.Count == 0;

        var bgColor = hasWarning
            ? (EditorGUIUtility.isProSkin ? new Color(0.35f, 0.25f, 0.15f) : new Color(1f, 0.95f, 0.85f))
            : (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.92f, 0.92f, 0.92f));

        EditorGUILayout.BeginVertical("helpBox");

        // Event key header
        EditorGUILayout.BeginHorizontal();
        var keyStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        EditorGUILayout.LabelField(evt.eventKey, keyStyle);

        if (hasWarning)
        {
            var warnStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.9f, 0.6f, 0.1f) }
            };
            if (evt.emitters.Count == 0)
                EditorGUILayout.LabelField("Sin emisor", warnStyle, GUILayout.Width(80));
            if (evt.consumers.Count == 0)
                EditorGUILayout.LabelField("Sin consumidor", warnStyle, GUILayout.Width(100));
        }
        else
        {
            var okStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.3f, 0.7f, 0.3f) }
            };
            EditorGUILayout.LabelField($"OK ({evt.emitters.Count}E, {evt.consumers.Count}C)", okStyle, GUILayout.Width(100));
        }

        EditorGUILayout.EndHorizontal();

        // Emitters
        if (evt.emitters.Count > 0)
        {
            EditorGUI.indentLevel++;
            var emitStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.4f, 0.7f, 0.9f) }
            };
            foreach (var em in evt.emitters)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Emitido por:", emitStyle, GUILayout.Width(80));
                EditorGUILayout.LabelField(em.description, EditorStyles.miniLabel);
                if (em.asset != null && GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    Selection.activeObject = em.asset;
                    EditorGUIUtility.PingObject(em.asset);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        // Consumers
        if (evt.consumers.Count > 0)
        {
            EditorGUI.indentLevel++;
            var consumeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.9f, 0.7f, 0.4f) }
            };
            foreach (var co in evt.consumers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Consumido por:", consumeStyle, GUILayout.Width(95));
                EditorGUILayout.LabelField(co.description, EditorStyles.miniLabel);
                if (co.asset != null && GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    Selection.activeObject = co.asset;
                    EditorGUIUtility.PingObject(co.asset);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    // ─── Dialogues Tab ───

    private void DrawDialoguesTab()
    {
        if (_dialogues == null) return;

        var filtered = _dialogues
            .Where(d => MatchesFilter(d.asset.name))
            .Where(d => !_showOnlyWarnings || d.missingKeys.Count > 0 || d.usedBy.Count == 0)
            .OrderBy(d => d.asset.name)
            .ToList();

        EditorGUILayout.LabelField($"{filtered.Count} diálogos encontrados", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var dlg in filtered)
        {
            DrawDialogueEntry(dlg);
        }
    }

    private void DrawDialogueEntry(DialogueInfo dlg)
    {
        bool hasWarning = dlg.missingKeys.Count > 0 || dlg.usedBy.Count == 0;

        EditorGUILayout.BeginVertical("helpBox");

        // Header
        EditorGUILayout.BeginHorizontal();

        var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        EditorGUILayout.LabelField($"{dlg.asset.name} ({dlg.lineCount} líneas)", nameStyle);

        if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
        {
            Selection.activeObject = dlg.asset;
            EditorGUIUtility.PingObject(dlg.asset);
        }

        EditorGUILayout.EndHorizontal();

        // Speakers
        if (dlg.speakerIds != null && dlg.speakerIds.Length > 0)
        {
            var speakerNames = dlg.speakerIds
                .GroupBy(s => s)
                .Select(g => $"{ResolveSpeakerStatic(g.Key)}({g.Count()})")
                .ToArray();
            EditorGUILayout.LabelField($"  Speakers: {string.Join(", ", speakerNames)}", EditorStyles.miniLabel);
        }

        // Used by
        if (dlg.usedBy.Count > 0)
        {
            foreach (var usage in dlg.usedBy)
            {
                EditorGUILayout.LabelField($"  Usado en: {usage}", EditorStyles.miniLabel);
            }
        }
        else
        {
            var warnStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.9f, 0.6f, 0.1f) }
            };
            EditorGUILayout.LabelField("  Sin usar en ningún NPC/grafo", warnStyle);
        }

        // Localization status
        EditorGUILayout.BeginHorizontal();
        var esLabel = dlg.hasAllKeysES ? "ES" : "ES (faltan claves)";
        var enLabel = dlg.hasAllKeysEN ? "EN" : "EN (faltan claves)";
        var esColor = dlg.hasAllKeysES ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);
        var enColor = dlg.hasAllKeysEN ? new Color(0.3f, 0.7f, 0.3f) : new Color(0.9f, 0.3f, 0.3f);

        var esStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = esColor } };
        var enStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = enColor } };
        EditorGUILayout.LabelField($"  Localización:", EditorStyles.miniLabel, GUILayout.Width(85));
        EditorGUILayout.LabelField(esLabel, esStyle, GUILayout.Width(120));
        EditorGUILayout.LabelField(enLabel, enStyle, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        // Missing keys
        if (dlg.missingKeys.Count > 0)
        {
            var missStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) }
            };
            foreach (var mk in dlg.missingKeys)
            {
                EditorGUILayout.LabelField($"    Falta: {mk}", missStyle);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    // ─── Quests Tab ───

    private void DrawQuestsTab()
    {
        if (_quests == null) return;

        var filtered = _quests
            .Where(q => MatchesFilter(q.quest != null ? q.quest.questId : ""))
            .OrderBy(q => q.quest != null ? q.quest.questId : "")
            .ToList();

        EditorGUILayout.LabelField($"{filtered.Count} quests encontradas", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var q in filtered)
        {
            DrawQuestEntry(q);
        }
    }

    private void DrawQuestEntry(QuestInfo q)
    {
        if (q.quest == null) return;

        EditorGUILayout.BeginVertical("helpBox");

        EditorGUILayout.BeginHorizontal();
        var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
        EditorGUILayout.LabelField(q.quest.questId, nameStyle);

        if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
        {
            Selection.activeObject = q.quest;
            EditorGUIUtility.PingObject(q.quest);
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(q.npcOwner))
            EditorGUILayout.LabelField($"  NPC: {q.npcOwner}", EditorStyles.miniLabel);

        foreach (var gr in q.graphReferences)
            EditorGUILayout.LabelField($"  Grafo: {gr}", EditorStyles.miniLabel);

        foreach (var cr in q.conditionReferences)
            EditorGUILayout.LabelField($"  Condición en: {cr}", EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    // ─── Unlocks Tab ───

    private void DrawUnlocksTab()
    {
        if (_unlocks == null) return;

        var filtered = _unlocks
            .Where(u => MatchesFilter(u.unlockName))
            .OrderBy(u => u.unlockType)
            .ThenBy(u => u.unlockName)
            .ToList();

        EditorGUILayout.LabelField($"{filtered.Count} desbloqueos encontrados", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var u in filtered)
        {
            EditorGUILayout.BeginVertical("helpBox");

            EditorGUILayout.LabelField($"[{u.unlockType}] {u.unlockName}", EditorStyles.boldLabel);

            foreach (var src in u.sources)
            {
                EditorGUILayout.LabelField($"  Fuente: {src}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }

    // ─── Scanning ───

    private void ScanProject()
    {
        _events = new Dictionary<string, EventReference>();
        _dialogues = new List<DialogueInfo>();
        _quests = new List<QuestInfo>();
        _unlocks = new List<UnlockInfo>();

        ScanNarrativeGraphs();
        ScanNPCConfigs();
        ScanDialogueAssets();
        ScanQuestAssets();
        ScanUnlockNodes();

        int totalEvents = _events.Count;
        int orphanEmitters = _events.Values.Count(e => e.consumers.Count == 0);
        int orphanConsumers = _events.Values.Count(e => e.emitters.Count == 0);

        _scanStatus = $"Eventos: {totalEvents} | Sin consumidor: {orphanEmitters} | Sin emisor: {orphanConsumers} | Diálogos: {_dialogues.Count} | Quests: {_quests.Count}";
        _hasScanned = true;

        Debug.Log($"[NarrativeCrossReference] Escaneo completo: {_scanStatus}");
    }

    private EventReference GetOrCreateEvent(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        key = key.Trim();
        if (!_events.TryGetValue(key, out var evt))
        {
            evt = new EventReference { eventKey = key };
            _events[key] = evt;
        }
        return evt;
    }

    private void ScanNarrativeGraphs()
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

                // RaiseCustomEventNode → emitter
                if (node is RaiseCustomEventNode raiseNode && !string.IsNullOrWhiteSpace(raiseNode.eventKey))
                {
                    var evt = GetOrCreateEvent(raiseNode.eventKey);
                    evt?.emitters.Add(new EventSource
                    {
                        description = $"{graph.name} → RaiseCustomEvent \"{raiseNode.displayTitle}\"",
                        asset = graph
                    });
                }

                // WaitCustomEventNode → consumer
                if (node is WaitCustomEventNode waitNode && !string.IsNullOrWhiteSpace(waitNode.eventKey))
                {
                    var evt = GetOrCreateEvent(waitNode.eventKey);
                    evt?.consumers.Add(new EventSource
                    {
                        description = $"{graph.name} → WaitCustomEvent \"{waitNode.displayTitle}\"",
                        asset = graph
                    });
                }

                // StartQuestNode → quest reference (uses questId string)
                if (node is StartQuestNode startQuestNode)
                {
                    if (!string.IsNullOrWhiteSpace(startQuestNode.questId))
                        AddQuestGraphRefById(startQuestNode.questId, $"{graph.name} → StartQuestNode");
                }

                // WaitQuestCompleteNode → quest reference (uses questId string)
                if (node is WaitQuestCompleteNode waitQuestNode)
                {
                    if (!string.IsNullOrWhiteSpace(waitQuestNode.questId))
                        AddQuestGraphRefById(waitQuestNode.questId, $"{graph.name} → WaitQuestComplete");
                }

                // UnlockAbilitiesNode → unlock reference
                if (node is UnlockAbilitiesNode unlockAbNode)
                {
                    ScanUnlockAbilitiesNode(unlockAbNode, graph.name);
                }

                // UnlockWardrobeItemNode → unlock reference
                if (node is UnlockWardrobeItemNode unlockWardNode)
                {
                    ScanUnlockWardrobeNode(unlockWardNode, graph.name);
                }

                // PlayDialogueNode → dialogue reference from graph
                if (node is PlayDialogueNode playDlgNode)
                {
                    if (playDlgNode.dialogue != null)
                        AddDialogueUsage(playDlgNode.dialogue, $"{graph.name} → PlayDialogueNode \"{node.displayTitle}\"");
                }
            }
        }
    }

    private void ScanNPCConfigs()
    {
        // Scan NPCInteractiveNarrativeConfig
        var guids = AssetDatabase.FindAssets("t:NPCInteractiveNarrativeConfig");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<NPCInteractiveNarrativeConfig>(path);
            if (config == null || config.conditionalNarratives == null) continue;

            string configName = config.name;

            foreach (var cn in config.conditionalNarratives)
            {
                if (cn == null) continue;
                string narrativeDesc = !string.IsNullOrEmpty(cn.description)
                    ? cn.description
                    : $"priority {cn.priority}";

                // Events emitted from ConditionalNarrative
                if (cn.sendNarrativeEvent && !string.IsNullOrWhiteSpace(cn.narrativeEventKey))
                {
                    var evt = GetOrCreateEvent(cn.narrativeEventKey);
                    evt?.emitters.Add(new EventSource
                    {
                        description = $"{configName} → ConditionalNarrative \"{narrativeDesc}\" (al completar)",
                        asset = config
                    });
                }

                // Events consumed from NarrativeCondition (Custom type)
                if (cn.condition != null && cn.condition.conditionType == NarrativeConditionType.Custom
                    && !string.IsNullOrWhiteSpace(cn.condition.customEventKey))
                {
                    var evt = GetOrCreateEvent(cn.condition.customEventKey);
                    evt?.consumers.Add(new EventSource
                    {
                        description = $"{configName} → Condición Custom en \"{narrativeDesc}\"",
                        asset = config
                    });
                }

                // Quest condition references
                if (cn.condition != null && cn.condition.targetQuest != null
                    && cn.condition.conditionType != NarrativeConditionType.None
                    && cn.condition.conditionType != NarrativeConditionType.Custom)
                {
                    AddQuestConditionRef(cn.condition.targetQuest,
                        $"{configName} → {cn.condition.conditionType}(\"{narrativeDesc}\")");
                }

                // Scan NarrativeChainEntry for events and dialogue references
                if (cn.narrativeChain != null)
                {
                    foreach (var chain in cn.narrativeChain)
                    {
                        if (chain == null) continue;

                        // Events from chain entries
                        if (chain.sendNarrativeEvent && !string.IsNullOrWhiteSpace(chain.narrativeEventKey))
                        {
                            var evt = GetOrCreateEvent(chain.narrativeEventKey);
                            evt?.emitters.Add(new EventSource
                            {
                                description = $"{configName} → Chain \"{narrativeDesc}\" → {chain.actionType}",
                                asset = config
                            });
                        }

                        // Defeat events from combat
                        if (chain.sendEventOnDefeat && !string.IsNullOrWhiteSpace(chain.defeatEventKey))
                        {
                            var evt = GetOrCreateEvent(chain.defeatEventKey);
                            evt?.emitters.Add(new EventSource
                            {
                                description = $"{configName} → Chain \"{narrativeDesc}\" → Combat defeat",
                                asset = config
                            });
                        }

                        // Dialogue usage tracking
                        if (chain.dialogue != null)
                        {
                            AddDialogueUsage(chain.dialogue, $"{configName} → \"{narrativeDesc}\"");
                        }
                    }
                }
            }
        }

        // Scan NPCQuestConfig for quest ownership
        var questConfigGuids = AssetDatabase.FindAssets("t:NPCQuestConfig");
        foreach (var guid in questConfigGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var config = AssetDatabase.LoadAssetAtPath<NPCQuestConfig>(path);
            if (config == null || config.questChain == null) continue;

            foreach (var chain in config.questChain)
            {
                if (chain == null) continue;

                // Quest ownership
                if (chain.questData != null)
                    SetQuestOwner(chain.questData, config.name);

                // Dialogue usage tracking from quest chain entries
                if (chain.dlgBefore != null)
                    AddDialogueUsage(chain.dlgBefore, $"{config.name} → dlgBefore");
                if (chain.dlgInProgress != null)
                    AddDialogueUsage(chain.dlgInProgress, $"{config.name} → dlgInProgress");
                if (chain.dlgTurnIn != null)
                    AddDialogueUsage(chain.dlgTurnIn, $"{config.name} → dlgTurnIn");
                if (chain.dlgCompleted != null)
                    AddDialogueUsage(chain.dlgCompleted, $"{config.name} → dlgCompleted");
            }
        }
    }

    private void ScanDialogueAssets()
    {
        var locTablesES = LoadLocalizationTable("es");
        var locTablesEN = LoadLocalizationTable("en");

        var guids = AssetDatabase.FindAssets("t:DialogueAsset");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(path);
            if (asset == null || asset.lines == null) continue;

            var info = new DialogueInfo
            {
                asset = asset,
                lineCount = asset.lines.Length,
                speakerIds = asset.lines
                    .Select(l => l.speakerNameId)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray()
            };

            // Check localization keys
            info.hasAllKeysES = true;
            info.hasAllKeysEN = true;

            for (int i = 0; i < asset.lines.Length; i++)
            {
                var line = asset.lines[i];

                if (!string.IsNullOrEmpty(line.textId))
                {
                    if (!locTablesES.ContainsKey(line.textId))
                    {
                        info.hasAllKeysES = false;
                        info.missingKeys.Add($"{line.textId} (ES)");
                    }
                    if (!locTablesEN.ContainsKey(line.textId))
                    {
                        info.hasAllKeysEN = false;
                        info.missingKeys.Add($"{line.textId} (EN)");
                    }
                }

                if (!string.IsNullOrEmpty(line.speakerNameId))
                {
                    if (!locTablesES.ContainsKey(line.speakerNameId))
                    {
                        info.hasAllKeysES = false;
                        info.missingKeys.Add($"{line.speakerNameId} (ES)");
                    }
                    if (!locTablesEN.ContainsKey(line.speakerNameId))
                    {
                        info.hasAllKeysEN = false;
                        info.missingKeys.Add($"{line.speakerNameId} (EN)");
                    }
                }
            }

            // Deduplicate missing keys
            info.missingKeys = info.missingKeys.Distinct().ToList();

            _dialogues.Add(info);
        }
    }

    private void ScanQuestAssets()
    {
        var guids = AssetDatabase.FindAssets("t:QuestData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var quest = AssetDatabase.LoadAssetAtPath<QuestData>(path);
            if (quest == null) continue;

            // Only add if not already in the list
            if (!_quests.Any(q => q.quest == quest))
            {
                _quests.Add(new QuestInfo { quest = quest });
            }
        }
    }

    private void ScanUnlockNodes()
    {
        // Already scanned in ScanNarrativeGraphs for graph-based unlocks
        // Now scan UnlockTrigger scene objects (best effort - prefabs)
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var triggers = prefab.GetComponentsInChildren<UnlockTrigger>(true);
            foreach (var trigger in triggers)
            {
                if (trigger.abilitiesToUnlock != null)
                {
                    foreach (var ab in trigger.abilitiesToUnlock)
                    {
                        AddUnlock("Ability", ab.ToString(), $"UnlockTrigger en prefab \"{prefab.name}\"");
                    }
                }
                if (trigger.spellsToUnlock != null)
                {
                    foreach (var sp in trigger.spellsToUnlock)
                    {
                        AddUnlock("Spell", sp.ToString(), $"UnlockTrigger en prefab \"{prefab.name}\"");
                    }
                }
            }
        }
    }

    private void ScanUnlockAbilitiesNode(UnlockAbilitiesNode node, string graphName)
    {
        if (node.abilityKeysToUnlock != null)
        {
            foreach (var ab in node.abilityKeysToUnlock)
                AddUnlock("Ability", ab.ToString(), $"{graphName} → UnlockAbilitiesNode");
        }

        if (node.spellsToUnlock != null)
        {
            foreach (var sp in node.spellsToUnlock)
                AddUnlock("Spell", sp.ToString(), $"{graphName} → UnlockAbilitiesNode");
        }
    }

    private void ScanUnlockWardrobeNode(UnlockWardrobeItemNode node, string graphName)
    {
        if (node.wardrobeItem != null)
            AddUnlock("Wardrobe", node.wardrobeItem.name, $"{graphName} → UnlockWardrobeItemNode");
    }

    // ─── Helpers ───

    private void AddDialogueUsage(DialogueAsset asset, string usage)
    {
        var existing = _dialogues.FirstOrDefault(d => d.asset == asset);
        if (existing != null)
        {
            if (!existing.usedBy.Contains(usage))
                existing.usedBy.Add(usage);
        }
        else
        {
            var info = new DialogueInfo
            {
                asset = asset,
                lineCount = asset.lines?.Length ?? 0,
                speakerIds = asset.lines?.Select(l => l.speakerNameId).Where(s => !string.IsNullOrEmpty(s)).ToArray() ?? new string[0]
            };
            info.usedBy.Add(usage);
            _dialogues.Add(info);
        }
    }

    private void AddQuestGraphRef(QuestData quest, string reference)
    {
        var existing = _quests.FirstOrDefault(q => q.quest == quest);
        if (existing == null)
        {
            existing = new QuestInfo { quest = quest };
            _quests.Add(existing);
        }
        if (!existing.graphReferences.Contains(reference))
            existing.graphReferences.Add(reference);
    }

    private void AddQuestGraphRefById(string questId, string reference)
    {
        var existing = _quests.FirstOrDefault(q => q.quest != null && q.quest.questId == questId);
        if (existing != null)
        {
            if (!existing.graphReferences.Contains(reference))
                existing.graphReferences.Add(reference);
        }
        else
        {
            // Try to find the QuestData asset by questId
            var questGuids = AssetDatabase.FindAssets("t:QuestData");
            QuestData found = null;
            foreach (var guid in questGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var qd = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                if (qd != null && qd.questId == questId)
                {
                    found = qd;
                    break;
                }
            }

            var info = new QuestInfo { quest = found };
            info.graphReferences.Add(reference);
            if (found == null)
                info.npcOwner = $"(questId: {questId}, asset no encontrado)";
            _quests.Add(info);
        }
    }

    private void AddQuestConditionRef(QuestData quest, string reference)
    {
        var existing = _quests.FirstOrDefault(q => q.quest == quest);
        if (existing == null)
        {
            existing = new QuestInfo { quest = quest };
            _quests.Add(existing);
        }
        if (!existing.conditionReferences.Contains(reference))
            existing.conditionReferences.Add(reference);
    }

    private void SetQuestOwner(QuestData quest, string owner)
    {
        var existing = _quests.FirstOrDefault(q => q.quest == quest);
        if (existing == null)
        {
            existing = new QuestInfo { quest = quest };
            _quests.Add(existing);
        }
        existing.npcOwner = owner;
    }

    private void AddUnlock(string type, string name, string source)
    {
        var existing = _unlocks.FirstOrDefault(u => u.unlockType == type && u.unlockName == name);
        if (existing == null)
        {
            existing = new UnlockInfo { unlockType = type, unlockName = name };
            _unlocks.Add(existing);
        }
        if (!existing.sources.Contains(source))
            existing.sources.Add(source);
    }

    private bool MatchesFilter(string text)
    {
        if (string.IsNullOrEmpty(_searchFilter)) return true;
        return text != null && text.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ─── Localization loading ───

    private const string LOC_FOLDER = "Assets/Resources/Localization";
    private static readonly string[] LOC_CATALOGS = { "dialogues", "quests", "ui", "cinematics", "prologue", "other" };

    private Dictionary<string, string> LoadLocalizationTable(string locale)
    {
        var table = new Dictionary<string, string>(512);

        foreach (var catalog in LOC_CATALOGS)
        {
            string path = Path.Combine(LOC_FOLDER, $"{catalog}_{locale}.json");
            if (!File.Exists(path)) continue;

            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<LocData>(json);
            if (data.texts != null)
                foreach (var e in data.texts)
                    if (!string.IsNullOrEmpty(e.key)) table[e.key] = e.value;
            if (data.subtitles != null)
                foreach (var e in data.subtitles)
                    if (!string.IsNullOrEmpty(e.id)) table[e.id] = e.text;
        }

        return table;
    }

    private static string ResolveSpeakerStatic(string speakerNameId)
    {
        if (string.IsNullOrEmpty(speakerNameId)) return "?";
        if (speakerNameId.StartsWith("CHAR_")) return speakerNameId.Substring(5);
        return speakerNameId;
    }

    // ─── JSON DTOs ───

    [System.Serializable]
    private class LocData
    {
        public LocTextEntry[] texts;
        public LocSubEntry[] subtitles;
    }

    [System.Serializable]
    private class LocTextEntry
    {
        public string key;
        public string value;
    }

    [System.Serializable]
    private class LocSubEntry
    {
        public string id;
        public string text;
    }
}
