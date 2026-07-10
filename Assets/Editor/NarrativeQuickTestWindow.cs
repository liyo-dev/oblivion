using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Quick Test System — Configura el juego para arrancar desde un nodo específico del grafo narrativo.
/// Genera automáticamente un preset temporal con el blackboard configurado para que el NarrativeRunner
/// continúe desde el nodo seleccionado, y lanza Play Mode.
/// 
/// Flujo:
/// 1. Seleccionar grafo + nodo objetivo + preset base
/// 2. Click "Play desde aquí"
/// 3. Se crea/actualiza un PlayerPresetSO temporal
/// 4. Se configura GameBootProfile con usePresetInsteadOfSave = true
/// 5. Se entra en Play Mode
/// 6. Al salir de Play Mode, se restaura el bootPreset original
/// </summary>
public class NarrativeQuickTestWindow : EditorWindow
{
    // Selection
    private NarrativeGraph _targetGraph;
    private string _targetNodeGuid;
    private string _graphLabel = "Historia Principal";
    private PlayerPresetSO _basePreset;

    // Graph label options
    private static readonly string[] KnownGraphLabels = { "Historia Principal", "Misiones Secundarias" };

    // State
    private int _selectedNodeIndex;
    private Vector2 _scrollPos;
    private bool _autoRestore = true;
    private string _status = "";

    // Saved original state for restore
    private static PlayerPresetSO _originalBootPreset;
    private static bool _originalUsePreset;
    private static bool _needsRestore;

    private const string QuickTestPresetPath = "Assets/Editor/QuickTestPreset_Temp.asset";

    [MenuItem("El Sendero/Narrativa/Quick Test from Node")]
    public static void ShowWindow()
    {
        var w = GetWindow<NarrativeQuickTestWindow>();
        w.titleContent = new GUIContent("Quick Test");
        w.minSize = new Vector2(400, 350);
        w.Show();
    }

    /// <summary>
    /// Opens the Quick Test window pre-configured with a specific graph and node.
    /// Called from the narrative graph editor context menu.
    /// </summary>
    public static void OpenWithNode(NarrativeGraph graph, NarrativeNode node, string graphLabel = null)
    {
        var w = GetWindow<NarrativeQuickTestWindow>();
        w.titleContent = new GUIContent("Quick Test");
        w._targetGraph = graph;
        w._targetNodeGuid = node?.guid;
        if (!string.IsNullOrEmpty(graphLabel))
            w._graphLabel = graphLabel;
        else
            w.TryAutoDetectLabel(graph);
        w.Show();
        w.Repaint();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && _needsRestore)
        {
            RestoreOriginalBootPreset();
        }
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        EditorGUILayout.LabelField("Quick Test — Play desde un nodo", titleStyle);
        EditorGUILayout.Space(8);

        // Graph selection
        _targetGraph = (NarrativeGraph)EditorGUILayout.ObjectField(
            "Grafo Narrativo", _targetGraph, typeof(NarrativeGraph), false);

        if (_targetGraph == null)
        {
            EditorGUILayout.HelpBox("Selecciona un NarrativeGraph para comenzar.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        // Graph label
        int labelIdx = System.Array.IndexOf(KnownGraphLabels, _graphLabel);
        if (labelIdx < 0) labelIdx = 0;
        labelIdx = EditorGUILayout.Popup("Graph Label", labelIdx, KnownGraphLabels);
        _graphLabel = KnownGraphLabels[labelIdx];

        EditorGUILayout.Space(4);

        // Node selection dropdown
        var nodes = _targetGraph.nodes?.Where(n => n != null).ToList() ?? new List<NarrativeNode>();
        if (nodes.Count == 0)
        {
            EditorGUILayout.HelpBox("El grafo no tiene nodos.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        var nodeNames = nodes.Select(n =>
        {
            string label = n.GetType().Name;
            if (!string.IsNullOrWhiteSpace(n.displayTitle))
                label += $" — \"{n.displayTitle}\"";
            return label;
        }).ToArray();

        // Find current selection
        if (!string.IsNullOrEmpty(_targetNodeGuid))
        {
            int idx = nodes.FindIndex(n => n.guid == _targetNodeGuid);
            if (idx >= 0) _selectedNodeIndex = idx;
        }

        _selectedNodeIndex = Mathf.Clamp(_selectedNodeIndex, 0, nodes.Count - 1);
        _selectedNodeIndex = EditorGUILayout.Popup("Nodo objetivo", _selectedNodeIndex, nodeNames);
        _targetNodeGuid = nodes[_selectedNodeIndex].guid;

        // Show node info
        var selectedNode = nodes[_selectedNodeIndex];
        EditorGUILayout.BeginVertical("helpBox");
        EditorGUILayout.LabelField($"Tipo: {selectedNode.GetType().Name}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"GUID: {selectedNode.guid}", EditorStyles.miniLabel);
        if (!string.IsNullOrEmpty(selectedNode.displayTitle))
            EditorGUILayout.LabelField($"Etiqueta: {selectedNode.displayTitle}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Base preset
        EditorGUILayout.LabelField("Preset Base", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "El preset base define el estado del jugador (inventario, stats, flags, etc.) " +
            "desde el que arrancará la prueba.", EditorStyles.wordWrappedMiniLabel);
        _basePreset = (PlayerPresetSO)EditorGUILayout.ObjectField(
            "Base Preset", _basePreset, typeof(PlayerPresetSO), false);

        if (_basePreset == null)
        {
            // Try auto-find default
            var defaultPreset = FindDefaultPreset();
            if (defaultPreset != null)
            {
                EditorGUILayout.HelpBox(
                    $"No hay preset base seleccionado. Se usará '{defaultPreset.name}' (defaultPlayerPreset del profile).",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No hay preset base. Se creará uno vacío.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.Space(4);
        _autoRestore = EditorGUILayout.Toggle("Restaurar bootPreset al salir", _autoRestore);

        EditorGUILayout.Space(12);

        // Status
        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        // Launch button
        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.3f);
        if (GUILayout.Button("Play desde aquí", GUILayout.Height(36)))
        {
            LaunchQuickTest();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);

        // Manual restore button
        if (_needsRestore)
        {
            GUI.backgroundColor = new Color(0.85f, 0.6f, 0.2f);
            if (GUILayout.Button("Restaurar bootPreset original"))
            {
                RestoreOriginalBootPreset();
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
    }

    private void LaunchQuickTest()
    {
        if (EditorApplication.isPlaying)
        {
            _status = "Ya estás en Play Mode. Sal primero.";
            return;
        }

        // 1. Find GameBootProfile
        var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
        if (profile == null)
        {
            var guids = AssetDatabase.FindAssets("t:GameBootProfile");
            if (guids.Length > 0)
                profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (profile == null)
        {
            _status = "GameBootProfile no encontrado.";
            return;
        }

        // 2. Save original state for restore
        _originalBootPreset = profile.bootPreset;
        _originalUsePreset = profile.usePresetInsteadOfSave;
        _needsRestore = _autoRestore;

        // 3. Determine base preset
        var basePreset = _basePreset ?? profile.defaultPlayerPreset;

        // 4. Create/update temp preset
        var tempPreset = AssetDatabase.LoadAssetAtPath<PlayerPresetSO>(QuickTestPresetPath);
        if (tempPreset == null)
        {
            tempPreset = ScriptableObject.CreateInstance<PlayerPresetSO>();
            AssetDatabase.CreateAsset(tempPreset, QuickTestPresetPath);
        }

        // Copy base preset data
        if (basePreset != null)
            CopyPresetData(basePreset, tempPreset);
        tempPreset.name = "QuickTest_Temp";

        // 5. Set up narrative blackboard to start from the target node
        var bbSnapshot = new PlayerSaveData.NarrativeBlackboardSnapshot
        {
            graphLabel = _graphLabel,
            blackboardData = new List<SimpleBlackboard.Entry>
            {
                new SimpleBlackboard.Entry
                {
                    key = "__currentNodeGuid",
                    type = "string",
                    value = _targetNodeGuid
                }
            }
        };

        // Keep existing blackboard entries for other graphs, replace for target graph
        if (tempPreset.narrativeBlackboards == null)
            tempPreset.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();

        tempPreset.narrativeBlackboards.RemoveAll(s => s.graphLabel == _graphLabel);
        tempPreset.narrativeBlackboards.Add(bbSnapshot);

        EditorUtility.SetDirty(tempPreset);

        // 6. Configure GameBootProfile
        profile.usePresetInsteadOfSave = true;
        profile.bootPreset = tempPreset;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var selectedNode = _targetGraph.FindNode(_targetNodeGuid);
        string nodeDesc = selectedNode != null
            ? $"{selectedNode.GetType().Name} \"{selectedNode.displayTitle}\""
            : _targetNodeGuid;

        _status = $"Lanzando Play desde {nodeDesc} en {_graphLabel}...";
        Debug.Log($"[QuickTest] Configurado: grafo='{_graphLabel}', nodo='{nodeDesc}', preset base='{basePreset?.name ?? "vacío"}'");

        // 7. Enter Play Mode
        EditorApplication.isPlaying = true;
    }

    private static void RestoreOriginalBootPreset()
    {
        var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
        if (profile == null)
        {
            var guids = AssetDatabase.FindAssets("t:GameBootProfile");
            if (guids.Length > 0)
                profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (profile != null)
        {
            profile.bootPreset = _originalBootPreset;
            profile.usePresetInsteadOfSave = _originalUsePreset;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[QuickTest] bootPreset restaurado al original.");
        }

        _needsRestore = false;
    }

    private static PlayerPresetSO FindDefaultPreset()
    {
        var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
        if (profile == null)
        {
            var guids = AssetDatabase.FindAssets("t:GameBootProfile");
            if (guids.Length > 0)
                profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        return profile?.defaultPlayerPreset;
    }

    private void TryAutoDetectLabel(NarrativeGraph graph)
    {
        if (graph == null) return;
        var graphPath = AssetDatabase.GetAssetPath(graph);
        var graphGuid = AssetDatabase.AssetPathToGUID(graphPath);

        // Search Start.unity scene for NarrativeGraphHub configuration
        var sceneGuids = AssetDatabase.FindAssets("t:Scene Start");
        foreach (var sceneGuid in sceneGuids)
        {
            var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (!scenePath.Contains("Start")) continue;

            var sceneText = System.IO.File.ReadAllText(scenePath);
            int idx = sceneText.IndexOf(graphGuid, System.StringComparison.Ordinal);
            if (idx < 0) continue;

            // Look backwards for "label:" line
            int searchStart = Mathf.Max(0, idx - 200);
            string chunk = sceneText.Substring(searchStart, idx - searchStart);
            int labelIdx = chunk.LastIndexOf("label:", System.StringComparison.Ordinal);
            if (labelIdx >= 0)
            {
                int lineEnd = chunk.IndexOf('\n', labelIdx);
                if (lineEnd < 0) lineEnd = chunk.Length;
                string labelLine = chunk.Substring(labelIdx + 6, lineEnd - labelIdx - 6).Trim();
                if (!string.IsNullOrEmpty(labelLine))
                {
                    _graphLabel = labelLine;
                    return;
                }
            }
        }
    }

    private static void CopyPresetData(PlayerPresetSO src, PlayerPresetSO dst)
    {
        if (src == null || dst == null) return;

        dst.spawnAnchorId = src.spawnAnchorId;
        dst.level = src.level;
        dst.maxHP = src.maxHP;
        dst.currentHP = src.currentHP;
        dst.maxMP = src.maxMP;
        dst.currentMP = src.currentMP;
        dst.unlockedAbilities = new List<AbilityId>(src.unlockedAbilities ?? new List<AbilityId>());
        dst.unlockedSpells = new List<SpellId>(src.unlockedSpells ?? new List<SpellId>());
        dst.leftSpellId = src.leftSpellId;
        dst.rightSpellId = src.rightSpellId;
        dst.specialSpellId = src.specialSpellId;
        dst.flags = new List<string>(src.flags ?? new List<string>());
        dst.abilities = new PlayerAbilities
        {
            swim = src.abilities?.swim ?? false,
            jump = src.abilities?.jump ?? false,
            climb = src.abilities?.climb ?? false,
            magic = src.abilities?.magic ?? false,
            fly = src.abilities?.fly ?? false
        };
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

        // Copy narrative blackboards
        dst.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        if (src.narrativeBlackboards != null)
        {
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

        // Copy NPC positions
        dst.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        if (src.npcPositions != null)
        {
            foreach (var npc in src.npcPositions)
            {
                dst.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
                {
                    npcId = npc.npcId,
                    position = npc.position,
                    rotation = npc.rotation,
                    hasActiveState = npc.hasActiveState,
                    isActive = npc.isActive
                });
            }
        }
    }
}
