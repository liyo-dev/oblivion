using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Game.NPC;
using Game.NPC.Common;
using Game.NPC.Modules;

/// <summary>
/// NPC Narrative Card — Ficha completa de un NPC seleccionado.
/// Muestra todos sus módulos narrativos, diálogos referenciados, eventos que emite/consume,
/// quests asignadas, y estado runtime (durante Play Mode).
/// Selecciona un NPC en la escena o un NPCInteractiveNarrativeConfig para ver su ficha.
/// </summary>
public class NPCNarrativeCardWindow : EditorWindow
{
    private Vector2 _scrollPos;
    private NPCBehaviourManagerV2 _selectedNPC;
    private NPCConfiguration _config;
    private bool _foldModules = true;
    private bool _foldDialogues = true;
    private bool _foldEvents = true;
    private bool _foldQuests = true;
    private bool _foldRuntime = true;

    // Cached analysis
    private List<DialogueReference> _dialogueRefs = new();
    private List<EventReference> _eventRefs = new();
    private List<QuestReference> _questRefs = new();
    private Object _lastAnalyzed;

    private struct DialogueReference
    {
        public DialogueAsset asset;
        public string context;
    }

    private struct EventReference
    {
        public string eventKey;
        public string direction; // "emits" or "consumes"
        public string context;
    }

    private struct QuestReference
    {
        public QuestData quest;
        public string context;
    }

    [MenuItem("El Sendero/Narrativa/NPC Narrative Card")]
    public static void ShowWindow()
    {
        var w = GetWindow<NPCNarrativeCardWindow>();
        w.titleContent = new GUIContent("NPC Card");
        w.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        _selectedNPC = null;
        _config = null;

        var go = Selection.activeGameObject;
        if (go != null)
        {
            _selectedNPC = go.GetComponent<NPCBehaviourManagerV2>();
            if (_selectedNPC != null)
                _config = _selectedNPC.Configuration;
        }

        if (_selectedNPC == null)
        {
            var configAsset = Selection.activeObject as NPCInteractiveNarrativeConfig;
            if (configAsset != null)
            {
                _config = new NPCConfiguration { interactiveNarrativeConfig = configAsset };
            }
        }

        AnalyzeNPC();
        Repaint();
    }

    private void OnGUI()
    {
        if (_selectedNPC == null && _config == null)
        {
            EditorGUILayout.HelpBox(
                "Selecciona un NPC en la escena (con NPCBehaviourManagerV2) " +
                "o un NPCInteractiveNarrativeConfig para ver su ficha narrativa.",
                MessageType.Info);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawModulesSection();
        DrawDialoguesSection();
        DrawEventsSection();
        DrawQuestsSection();

        if (EditorApplication.isPlaying && _selectedNPC != null)
            DrawRuntimeSection();

        EditorGUILayout.EndScrollView();
    }

    // ─── Header ───

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("helpBox");

        var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        string npcName = _selectedNPC != null ? _selectedNPC.name : _config?.interactiveNarrativeConfig?.name ?? "(Config)";
        EditorGUILayout.LabelField(npcName, titleStyle);

        if (_config != null)
        {
            EditorGUILayout.LabelField($"Tipo: {_config.behaviourType}", EditorStyles.miniLabel);

            var modules = new List<string>();
            if (_config.ambientConfig != null) modules.Add("Ambient");
            if (_config.interactiveNarrativeConfig != null) modules.Add("Interactive");
            if (_config.questConfig != null) modules.Add("Quest");
            if (_config.combatConfig != null) modules.Add("Combat");
            if (_config.partyConfig != null) modules.Add("Party");

            EditorGUILayout.LabelField($"Módulos: {string.Join(", ", modules)}", EditorStyles.miniLabel);
        }

        if (_selectedNPC != null && GUILayout.Button("Select GameObject", EditorStyles.miniButton))
        {
            Selection.activeGameObject = _selectedNPC.gameObject;
            EditorGUIUtility.PingObject(_selectedNPC.gameObject);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    // ─── Modules ───

    private void DrawModulesSection()
    {
        _foldModules = EditorGUILayout.Foldout(_foldModules, "Módulos de Configuración", true, EditorStyles.foldoutHeader);
        if (!_foldModules || _config == null) return;

        EditorGUI.indentLevel++;

        DrawModuleRef("Ambient", _config.ambientConfig);
        DrawModuleRef("Interactive Narrative", _config.interactiveNarrativeConfig);
        DrawModuleRef("Quest", _config.questConfig);
        DrawModuleRef("Combat", _config.combatConfig);
        DrawModuleRef("Party", _config.partyConfig);

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }

    private void DrawModuleRef(string label, Object config)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(160));
        if (config != null)
        {
            EditorGUILayout.LabelField(config.name, EditorStyles.miniLabel);
            if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
        }
        else
        {
            var grayStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = Color.gray } };
            EditorGUILayout.LabelField("(no asignado)", grayStyle);
        }
        EditorGUILayout.EndHorizontal();
    }

    // ─── Dialogues ───

    private void DrawDialoguesSection()
    {
        _foldDialogues = EditorGUILayout.Foldout(_foldDialogues,
            $"Diálogos Referenciados ({_dialogueRefs.Count})", true, EditorStyles.foldoutHeader);
        if (!_foldDialogues) return;

        if (_dialogueRefs.Count == 0)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("No se encontraron diálogos referenciados.", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
        else
        {
            foreach (var dlg in _dialogueRefs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;

                string assetName = dlg.asset != null ? dlg.asset.name : "(null)";
                EditorGUILayout.LabelField($"{assetName}", EditorStyles.boldLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField(dlg.context, EditorStyles.miniLabel);

                if (dlg.asset != null && GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    Selection.activeObject = dlg.asset;
                    EditorGUIUtility.PingObject(dlg.asset);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(4);
    }

    // ─── Events ───

    private void DrawEventsSection()
    {
        _foldEvents = EditorGUILayout.Foldout(_foldEvents,
            $"Eventos ({_eventRefs.Count})", true, EditorStyles.foldoutHeader);
        if (!_foldEvents) return;

        if (_eventRefs.Count == 0)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("No se encontraron eventos.", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
        else
        {
            var emitColor = new Color(0.3f, 0.85f, 0.3f);
            var consumeColor = new Color(0.85f, 0.6f, 0.2f);

            foreach (var evt in _eventRefs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;

                var dirStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = evt.direction == "emits" ? emitColor : consumeColor }
                };

                string arrow = evt.direction == "emits" ? "EMITE" : "CONSUME";
                EditorGUILayout.LabelField($"[{arrow}]", dirStyle, GUILayout.Width(80));
                EditorGUILayout.LabelField(evt.eventKey, EditorStyles.boldLabel, GUILayout.Width(180));
                EditorGUILayout.LabelField(evt.context, EditorStyles.miniLabel);

                EditorGUI.indentLevel--;
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(4);
    }

    // ─── Quests ───

    private void DrawQuestsSection()
    {
        _foldQuests = EditorGUILayout.Foldout(_foldQuests,
            $"Quests ({_questRefs.Count})", true, EditorStyles.foldoutHeader);
        if (!_foldQuests) return;

        if (_questRefs.Count == 0)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("No se encontraron quests.", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
        else
        {
            foreach (var q in _questRefs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.indentLevel++;

                string questName = q.quest != null ? q.quest.questId : "(null)";
                EditorGUILayout.LabelField(questName, EditorStyles.boldLabel, GUILayout.Width(180));
                EditorGUILayout.LabelField(q.context, EditorStyles.miniLabel);

                if (q.quest != null && GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    Selection.activeObject = q.quest;
                    EditorGUIUtility.PingObject(q.quest);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(4);
    }

    // ─── Runtime ───

    private void DrawRuntimeSection()
    {
        _foldRuntime = EditorGUILayout.Foldout(_foldRuntime, "Estado Runtime", true, EditorStyles.foldoutHeader);
        if (!_foldRuntime || _selectedNPC == null) return;

        EditorGUI.indentLevel++;

        var ctx = _selectedNPC.Context;
        if (ctx != null)
        {
            var brain = _selectedNPC.Brain;
            string stateName = brain?.CurrentState != null ? brain.CurrentState.GetType().Name : "(none)";
            EditorGUILayout.LabelField($"Estado actual: {stateName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"En combate: {ctx.IsInCombat}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"En cinemática: {_selectedNPC.IsInCinematic}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Es aliado: {_selectedNPC.IsAlly}", EditorStyles.miniLabel);
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }

    // ─── Analysis ───

    private void AnalyzeNPC()
    {
        _dialogueRefs.Clear();
        _eventRefs.Clear();
        _questRefs.Clear();

        if (_config == null) return;

        AnalyzeInteractiveNarrative(_config.interactiveNarrativeConfig);
        AnalyzeQuestConfig(_config.questConfig);
    }

    private void AnalyzeInteractiveNarrative(NPCInteractiveNarrativeConfig config)
    {
        if (config == null || config.conditionalNarratives == null) return;

        foreach (var cn in config.conditionalNarratives)
        {
            if (cn == null) continue;

            string desc = !string.IsNullOrEmpty(cn.description)
                ? cn.description
                : $"priority {cn.priority}";

            // Events emitted
            if (cn.sendNarrativeEvent && !string.IsNullOrWhiteSpace(cn.narrativeEventKey))
            {
                _eventRefs.Add(new EventReference
                {
                    eventKey = cn.narrativeEventKey,
                    direction = "emits",
                    context = $"ConditionalNarrative \"{desc}\" (al completar)"
                });
            }

            // Events consumed (condition)
            if (cn.condition != null && cn.condition.conditionType == NarrativeConditionType.Custom
                && !string.IsNullOrWhiteSpace(cn.condition.customEventKey))
            {
                _eventRefs.Add(new EventReference
                {
                    eventKey = cn.condition.customEventKey,
                    direction = "consumes",
                    context = $"Condición en \"{desc}\""
                });
            }

            // Chain entries
            if (cn.narrativeChain != null)
            {
                foreach (var chain in cn.narrativeChain)
                {
                    if (chain == null) continue;

                    // Dialogue references
                    if (chain.dialogue != null)
                    {
                        _dialogueRefs.Add(new DialogueReference
                        {
                            asset = chain.dialogue,
                            context = $"NarrativeChain \"{desc}\""
                        });
                    }

                    // Events from chain
                    if (chain.sendNarrativeEvent && !string.IsNullOrWhiteSpace(chain.narrativeEventKey))
                    {
                        _eventRefs.Add(new EventReference
                        {
                            eventKey = chain.narrativeEventKey,
                            direction = "emits",
                            context = $"ChainEntry en \"{desc}\""
                        });
                    }

                    // Defeat events
                    if (chain.sendEventOnDefeat && !string.IsNullOrWhiteSpace(chain.defeatEventKey))
                    {
                        _eventRefs.Add(new EventReference
                        {
                            eventKey = chain.defeatEventKey,
                            direction = "emits",
                            context = $"DefeatEvent en \"{desc}\""
                        });
                    }
                }
            }
        }
    }

    private void AnalyzeQuestConfig(NPCQuestConfig config)
    {
        if (config == null || config.questChain == null) return;

        foreach (var chain in config.questChain)
        {
            if (chain == null) continue;

            if (chain.questData != null)
            {
                _questRefs.Add(new QuestReference
                {
                    quest = chain.questData,
                    context = "QuestChain"
                });
            }

            if (chain.dlgBefore != null)
                _dialogueRefs.Add(new DialogueReference { asset = chain.dlgBefore, context = "Quest: dlgBefore" });
            if (chain.dlgInProgress != null)
                _dialogueRefs.Add(new DialogueReference { asset = chain.dlgInProgress, context = "Quest: dlgInProgress" });
            if (chain.dlgTurnIn != null)
                _dialogueRefs.Add(new DialogueReference { asset = chain.dlgTurnIn, context = "Quest: dlgTurnIn" });
            if (chain.dlgCompleted != null)
                _dialogueRefs.Add(new DialogueReference { asset = chain.dlgCompleted, context = "Quest: dlgCompleted" });
        }
    }
}
