using System;
using System.Collections.Generic;
using System.Linq;
using EasyTransition;
using UnityEditor;
using UnityEngine;
using Game.NPC.Modules;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Herramienta de editor para migrar las narrativas de NPCs al grafo narrativo.
    /// Analiza todos los NPCInteractiveNarrativeConfig y genera los nodos equivalentes
    /// en el grafo seleccionado (WaitNPCInteraction → PlayDialogue → NPCCommand, etc.).
    /// </summary>
    public class NPCNarrativeMigrationTool : EditorWindow
    {
        private NarrativeGraph _targetGraph;
        private Vector2 _scrollPos;
        private string _targetChapter = "Cap. NPC";
        private List<ConfigAnalysis> _analyses = new();
        private bool _analyzed;

        [MenuItem("Tools/Narrative Graph/NPC Migration Tool")]
        public static void ShowWindow()
        {
            var w = GetWindow<NPCNarrativeMigrationTool>("NPC → Grafo");
            w.minSize = new Vector2(500, 400);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Migración de Narrativas NPC → Grafo", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _targetGraph = (NarrativeGraph)EditorGUILayout.ObjectField(
                "Grafo destino", _targetGraph, typeof(NarrativeGraph), false);
            _targetChapter = EditorGUILayout.TextField("Capítulo para nodos generados", _targetChapter);

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Analizar NPCs", GUILayout.Height(30)))
                AnalyzeAllConfigs();

            GUI.enabled = _analyzed && _targetGraph != null;
            if (GUILayout.Button("Generar Nodos en Grafo", GUILayout.Height(30)))
                GenerateAllNodes();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(12);

            if (!_analyzed)
            {
                EditorGUILayout.HelpBox(
                    "Pulsa 'Analizar NPCs' para escanear todos los NPCInteractiveNarrativeConfig del proyecto.",
                    MessageType.Info);
                return;
            }

            // Mostrar resultados del análisis
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            foreach (var analysis in _analyses)
            {
                DrawAnalysis(analysis);
            }
            EditorGUILayout.EndScrollView();
        }

        void AnalyzeAllConfigs()
        {
            _analyses.Clear();
            var guids = AssetDatabase.FindAssets("t:NPCInteractiveNarrativeConfig");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<NPCInteractiveNarrativeConfig>(path);
                if (config == null) continue;

                _analyses.Add(AnalyzeConfig(config, path));
            }

            _analyzed = true;
            Debug.Log($"[NPCMigration] Analizados {_analyses.Count} configs de NPCs.");
        }

        ConfigAnalysis AnalyzeConfig(NPCInteractiveNarrativeConfig config, string assetPath)
        {
            var analysis = new ConfigAnalysis
            {
                config = config,
                assetPath = assetPath,
                npcName = config.name.Replace("NPC_InteractiveNarrative_Config_", ""),
                narratives = new List<NarrativeAnalysis>()
            };

            if (config.conditionalNarratives == null) return analysis;

            foreach (var narrative in config.conditionalNarratives)
            {
                var na = new NarrativeAnalysis
                {
                    conditionType = narrative.condition.conditionType,
                    questRef = narrative.condition.targetQuest,
                    customEventKey = narrative.condition.customEventKey,
                    singleUse = narrative.singleUse,
                    actions = new List<ActionAnalysis>()
                };

                if (narrative.narrativeChain != null)
                {
                    foreach (var entry in narrative.narrativeChain)
                    {
                        na.actions.Add(new ActionAnalysis
                        {
                            actionType = entry.actionType,
                            dialogue = entry.dialogue,
                            targetAnchor = entry.targetAnchorName,
                            questToStart = entry.questToStart,
                            combatConfig = entry.combatConfig,
                            defeatEventKey = entry.defeatEventKey,
                            sendEventOnDefeat = entry.sendEventOnDefeat,
                            narrativeEventKey = entry.sendNarrativeEvent ? entry.narrativeEventKey : null,
                            teleportTransition = entry.teleportTransition
                        });
                    }
                }

                if (narrative.sendNarrativeEvent && !string.IsNullOrEmpty(narrative.narrativeEventKey))
                    na.emitsEvent = narrative.narrativeEventKey;

                analysis.narratives.Add(na);
            }

            return analysis;
        }

        void DrawAnalysis(ConfigAnalysis a)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"📋 {a.npcName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"   Ruta: {a.assetPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"   Cadenas narrativas: {a.narratives.Count}");

            foreach (var n in a.narratives)
            {
                EditorGUI.indentLevel++;
                string condStr = FormatCondition(n);
                EditorGUILayout.LabelField($"Condición: {condStr}");

                foreach (var action in n.actions)
                {
                    string actionStr = FormatAction(action);
                    EditorGUILayout.LabelField($"  → {actionStr}", EditorStyles.miniLabel);
                }

                if (!string.IsNullOrEmpty(n.emitsEvent))
                    EditorGUILayout.LabelField($"  ⚡ Emite evento: {n.emitsEvent}", EditorStyles.miniLabel);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        string FormatCondition(NarrativeAnalysis n)
        {
            switch (n.conditionType)
            {
                case NarrativeConditionType.None: return "Siempre";
                case NarrativeConditionType.QuestNotStarted:
                    return $"Quest NO iniciada: {(n.questRef ? n.questRef.name : "???")}";
                case NarrativeConditionType.QuestStarted:
                    return $"Quest iniciada: {(n.questRef ? n.questRef.name : "???")}";
                case NarrativeConditionType.QuestCompleted:
                    return $"Quest completada: {(n.questRef ? n.questRef.name : "???")}";
                case NarrativeConditionType.QuestActive:
                    return $"Quest activa: {(n.questRef ? n.questRef.name : "???")}";
                case NarrativeConditionType.Custom:
                    return $"Evento custom: {n.customEventKey}";
                default: return n.conditionType.ToString();
            }
        }

        string FormatAction(ActionAnalysis a)
        {
            switch (a.actionType)
            {
                case NarrativeActionType.Dialogue:
                    return $"Diálogo: {(a.dialogue ? a.dialogue.name : "???")}";
                case NarrativeActionType.Move:
                    return $"Mover a: {a.targetAnchor}";
                case NarrativeActionType.PlayAnimation:
                    return "Animación";
                case NarrativeActionType.StartQuest:
                    return $"Iniciar quest: {(a.questToStart ? a.questToStart.name : "???")}";
                case NarrativeActionType.StartCombat:
                    return $"Combate{(a.sendEventOnDefeat ? $" → evento: {a.defeatEventKey}" : "")}";
                case NarrativeActionType.Wait:
                    return "Esperar";
                case NarrativeActionType.JoinParty:
                    return "Unirse al equipo";
                case NarrativeActionType.LeaveParty:
                    return "Abandonar equipo";
                case NarrativeActionType.MoveNearPlayer:
                    return "Acercarse al jugador";
                case NarrativeActionType.LeadPlayerToAnchor:
                    return $"Escoltar a: {a.targetAnchor}";
                case NarrativeActionType.TeleportNearPlayer:
                    return "Teletransportarse cerca del jugador";
                case NarrativeActionType.TeleportPlayer:
                    return $"Teletransportar jugador a: {a.targetAnchor}";
                default: return a.actionType.ToString();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GENERACIÓN DE NODOS EN EL GRAFO
        // ─────────────────────────────────────────────────────────────────────

        void GenerateAllNodes()
        {
            if (_targetGraph == null)
            {
                EditorUtility.DisplayDialog("Error", "Selecciona un grafo destino primero.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirmar migración",
                $"Se generarán nodos en '{_targetGraph.name}' para {_analyses.Count} NPCs.\n\n" +
                "Los nodos NO se conectarán al flujo existente (se crearán como islas que luego conectas manualmente).\n\n" +
                "¿Continuar?", "Generar", "Cancelar"))
                return;

            Undo.RecordObject(_targetGraph, "NPC Migration - Generar nodos");

            int totalNodes = 0;
            float baseY = GetMaxNodeY() + 200f;

            for (int i = 0; i < _analyses.Count; i++)
            {
                var analysis = _analyses[i];
                float xOffset = 0f;
                float yOffset = baseY + i * 300f;

                totalNodes += GenerateNodesForNPC(analysis, xOffset, yOffset);
            }

            EditorUtility.SetDirty(_targetGraph);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Migración completada",
                $"Se generaron {totalNodes} nodos para {_analyses.Count} NPCs.\n\n" +
                "Los nodos aparecen como islas separadas en el grafo.\n" +
                "Conéctalos manualmente al flujo narrativo principal.",
                "OK");

            Debug.Log($"[NPCMigration] Generados {totalNodes} nodos para {_analyses.Count} NPCs en '{_targetGraph.name}'.");
        }

        int GenerateNodesForNPC(ConfigAnalysis analysis, float startX, float startY)
        {
            int nodesCreated = 0;
            float x = startX;
            float y = startY;
            string npcId = analysis.npcName.ToUpper();

            foreach (var narrative in analysis.narratives)
            {
                // Nodo WaitNPCInteraction como punto de entrada
                var waitNode = CreateNode<WaitNPCInteractionNode>(x, y,
                    $"[{analysis.npcName}] Esperar interacción");
                waitNode.npcId = npcId;
                nodesCreated++;
                x += 400f;

                string prevGuid = waitNode.guid;

                foreach (var action in narrative.actions)
                {
                    NarrativeNode newNode = null;

                    switch (action.actionType)
                    {
                        case NarrativeActionType.Dialogue:
                            var dlgNode = CreateNode<PlayDialogueNode>(x, y,
                                $"[{analysis.npcName}] Diálogo: {(action.dialogue ? action.dialogue.name : "?")}");
                            dlgNode.dialogue = action.dialogue;
                            newNode = dlgNode;
                            break;

                        case NarrativeActionType.StartQuest:
                            var questNode = CreateNode<StartQuestNode>(x, y,
                                $"[{analysis.npcName}] Quest: {(action.questToStart ? action.questToStart.questId : "?")}");
                            questNode.questId = action.questToStart != null ? action.questToStart.questId : "";
                            newNode = questNode;
                            break;

                        case NarrativeActionType.StartCombat:
                            var combatNode = CreateNode<StartCombatNode>(x, y,
                                $"[{analysis.npcName}] Combate");
                            combatNode.npcId = npcId;
                            combatNode.combatConfig = action.combatConfig;
                            combatNode.sendEventOnDefeat = action.sendEventOnDefeat;
                            combatNode.defeatEventKey = action.defeatEventKey;
                            newNode = combatNode;
                            break;

                        case NarrativeActionType.JoinParty:
                            var joinNode = CreateNode<NPCCommandNode>(x, y,
                                $"[{analysis.npcName}] Unirse al equipo");
                            joinNode.npcId = npcId;
                            joinNode.command = NPCCommandNode.CommandType.JoinParty;
                            newNode = joinNode;
                            break;

                        case NarrativeActionType.LeaveParty:
                            var leaveNode = CreateNode<NPCCommandNode>(x, y,
                                $"[{analysis.npcName}] Abandonar equipo");
                            leaveNode.npcId = npcId;
                            leaveNode.command = NPCCommandNode.CommandType.LeaveParty;
                            newNode = leaveNode;
                            break;

                        case NarrativeActionType.Move:
                            var moveNode = CreateNode<NPCCommandNode>(x, y,
                                $"[{analysis.npcName}] Mover a {action.targetAnchor}");
                            moveNode.npcId = npcId;
                            moveNode.command = NPCCommandNode.CommandType.Move;
                            moveNode.targetAnchorName = action.targetAnchor;
                            newNode = moveNode;
                            break;

                        case NarrativeActionType.MoveNearPlayer:
                            var nearNode = CreateNode<NPCCommandNode>(x, y,
                                $"[{analysis.npcName}] Acercarse al jugador");
                            nearNode.npcId = npcId;
                            nearNode.command = NPCCommandNode.CommandType.MoveNearPlayer;
                            newNode = nearNode;
                            break;

                        case NarrativeActionType.TeleportNearPlayer:
                            var tpNearNode = CreateNode<NPCCommandNode>(x, y,
                                $"[{analysis.npcName}] Teleport cerca del jugador");
                            tpNearNode.npcId = npcId;
                            tpNearNode.command = NPCCommandNode.CommandType.TeleportNearPlayer;
                            newNode = tpNearNode;
                            break;

                        case NarrativeActionType.LeadPlayerToAnchor:
                            var escortNode = CreateNode<NPCCommandNode>(x, y,
                                $"[{analysis.npcName}] Escoltar a {action.targetAnchor}");
                            escortNode.npcId = npcId;
                            escortNode.command = NPCCommandNode.CommandType.LeadPlayerToAnchor;
                            escortNode.targetAnchorName = action.targetAnchor;
                            newNode = escortNode;
                            break;

                        case NarrativeActionType.TeleportPlayer:
                            var tpPlayerNode = CreateNode<TeleportPlayerNode>(x, y,
                                $"[{analysis.npcName}] Teleport jugador a {action.targetAnchor}");
                            tpPlayerNode.targetAnchorName = action.targetAnchor;
                            tpPlayerNode.teleportTransition = action.teleportTransition;
                            newNode = tpPlayerNode;
                            break;

                        case NarrativeActionType.Wait:
                            var waitCmd = CreateNode<NPCCommandNode>(x, y,
                                $"[{analysis.npcName}] Esperar");
                            waitCmd.npcId = npcId;
                            waitCmd.command = NPCCommandNode.CommandType.Wait;
                            newNode = waitCmd;
                            break;
                    }

                    if (newNode != null)
                    {
                        // Conectar al nodo anterior
                        ConnectNodes(prevGuid, newNode.guid);
                        prevGuid = newNode.guid;
                        nodesCreated++;
                        x += 400f;
                    }
                }

                // Si la narrativa emite un evento al completarse, añadir nodo de nota
                if (!string.IsNullOrEmpty(narrative.emitsEvent))
                {
                    var noteNode = CreateNode<GraphNoteNode>(x, y,
                        $"[{analysis.npcName}] ⚡ Emite: {narrative.emitsEvent}");
                    ConnectNodes(prevGuid, noteNode.guid);
                    nodesCreated++;
                    x += 400f;
                }

                // Siguiente cadena narrativa debajo
                y += 150f;
                x = startX;
            }

            return nodesCreated;
        }

        T CreateNode<T>(float x, float y, string title) where T : NarrativeNode, new()
        {
            var node = new T
            {
                guid = Guid.NewGuid().ToString(),
                position = new Vector2(x, y),
                displayTitle = title,
                chapter = _targetChapter,
                outputs = new List<string>()
            };
            _targetGraph.nodes.Add(node);
            return node;
        }

        void ConnectNodes(string fromGuid, string toGuid)
        {
            var fromNode = _targetGraph.FindNode(fromGuid);
            if (fromNode != null)
            {
                if (fromNode.outputs == null)
                    fromNode.outputs = new List<string>();
                fromNode.outputs.Add(toGuid);
            }
        }

        float GetMaxNodeY()
        {
            float maxY = 0f;
            if (_targetGraph?.nodes == null) return maxY;

            foreach (var node in _targetGraph.nodes)
            {
                if (node != null && node.position.y > maxY)
                    maxY = node.position.y;
            }
            return maxY;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Clases de análisis
        // ─────────────────────────────────────────────────────────────────────

        class ConfigAnalysis
        {
            public NPCInteractiveNarrativeConfig config;
            public string assetPath;
            public string npcName;
            public List<NarrativeAnalysis> narratives;
        }

        class NarrativeAnalysis
        {
            public NarrativeConditionType conditionType;
            public QuestData questRef;
            public string customEventKey;
            public bool singleUse;
            public string emitsEvent;
            public List<ActionAnalysis> actions;
        }

        class ActionAnalysis
        {
            public NarrativeActionType actionType;
            public DialogueAsset dialogue;
            public string targetAnchor;
            public QuestData questToStart;
            public Game.NPC.Modules.NPCCombatConfig combatConfig;
            public string defeatEventKey;
            public bool sendEventOnDefeat;
            public string narrativeEventKey;
            public TransitionSettings teleportTransition;
        }
    }
}
