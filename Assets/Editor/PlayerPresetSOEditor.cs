using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(PlayerPresetSO))]
public class PlayerPresetSOEditor : UnityEditor.Editor
{
    private bool _showQuestHelper = false;
    private bool _showFlagsDebug = false;
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayerPresetSO preset = (PlayerPresetSO)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Herramientas de Apariencia", EditorStyles.boldLabel);

        if (GUILayout.Button("Capturar apariencia del jugador en escena"))
        {
            CaptureAppearanceFromScene(preset);
        }

        EditorGUILayout.HelpBox(
            "Usa el botón 'Capturar apariencia del jugador en escena' para copiar la configuración " +
            "visual actual del jugador en la escena a este preset. El jugador debe tener un componente " +
            "ModularAutoBuilder activo en la escena.",
            MessageType.Info
        );
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎮 Herramientas de Testing (Play Mode)", EditorStyles.boldLabel);
        
        GUI.enabled = Application.isPlaying;
        
        if (GUILayout.Button("📸 Crear Preset de Test desde Estado Actual", GUILayout.Height(30)))
        {
            CreateTestPresetFromCurrentState();
        }
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("🔄 Actualizar ESTE Preset desde Estado Actual", GUILayout.Height(25)))
        {
            UpdateThisPresetFromCurrentState(preset);
        }
        
        GUI.enabled = true;
        
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "⚠️ Estos botones solo funcionan en PLAY MODE.\n\n" +
                "📸 CREAR: Crea un NUEVO preset capturando el estado actual.\n\n" +
                "🔄 ACTUALIZAR: Sobrescribe ESTE preset con el estado actual.\n" +
                "   Útil para actualizar presets de testeo existentes.\n\n" +
                "Captura: Stats, hechizos, inventario, apariencia, quests,\n" +
                "narrativas (con posición del grafo), NPCs, bosses, etc.",
                MessageType.Info
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "✅ Modo Play activo.\n\n" +
                "📸 CREAR: Genera un nuevo preset.\n" +
                "🔄 ACTUALIZAR: Sobrescribe '" + preset.name + "' con el estado actual.",
                MessageType.Info
            );
        }
        
        // === HERRAMIENTA: Quest Helper ===
        EditorGUILayout.Space(10);
        _showQuestHelper = EditorGUILayout.Foldout(_showQuestHelper, "📋 Ayudante de Quests (Formato de Flags)", true);
        if (_showQuestHelper)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Formato de Flags de Quests:", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
            
            EditorGUILayout.LabelField("Quest Activa:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel("QUEST_ACTIVE:quest_id", EditorStyles.textField, GUILayout.Height(18));
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Quest Completada:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel("QUEST_COMPLETED:quest_id", EditorStyles.textField, GUILayout.Height(18));
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Step Completado:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel("QUEST_STEP_DONE:quest_id:0", EditorStyles.textField, GUILayout.Height(18));
            EditorGUILayout.HelpBox("El número al final es el índice del step (0-based)", MessageType.None);
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Quest Archivada:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel("QUEST_ARCHIVED:quest_id", EditorStyles.textField, GUILayout.Height(18));
            
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Quest Tracked:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel("QUEST_FOLLOWED:quest_id", EditorStyles.textField, GUILayout.Height(18));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("💡 Ejemplo completo:\n\n" +
                "Quest activa con 2 steps completados:\n" +
                "• QUEST_ACTIVE:victoria_get_cloak\n" +
                "• QUEST_STEP_DONE:victoria_get_cloak:0\n" +
                "• QUEST_STEP_DONE:victoria_get_cloak:1\n\n" +
                "Quest completada:\n" +
                "• QUEST_COMPLETED:tutorial_complete", MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        // === HERRAMIENTA: Debug de Flags ===
        EditorGUILayout.Space(10);
        _showFlagsDebug = EditorGUILayout.Foldout(_showFlagsDebug, "🔍 Análisis de Flags (Debug)", true);
        if (_showFlagsDebug)
        {
            EditorGUILayout.BeginVertical("box");
            
            // === Diagnóstico de Blackboards Narrativos ===
            EditorGUILayout.LabelField("📖 Blackboards Narrativos:", EditorStyles.boldLabel);
            
            if (preset.narrativeBlackboards == null || preset.narrativeBlackboards.Count == 0)
            {
                EditorGUILayout.HelpBox("⚠️ No hay blackboards narrativos.\n\nEl grafo narrativo empezará desde el inicio.", MessageType.Warning);
            }
            else
            {
                foreach (var bb in preset.narrativeBlackboards)
                {
                    var currentNodeEntry = bb.blackboardData?.FirstOrDefault(e => e.key == "__currentNodeGuid");
                    bool hasCurrentNode = currentNodeEntry != null && !string.IsNullOrEmpty(currentNodeEntry.value);
                    
                    string status = hasCurrentNode ? "✅" : "❌";
                    string nodeInfo = hasCurrentNode 
                        ? $"Nodo: {currentNodeEntry.value.Substring(0, System.Math.Min(8, currentNodeEntry.value.Length))}..." 
                        : "Sin posición (empezará desde inicio)";
                    
                    EditorGUILayout.LabelField($"{status} {bb.graphLabel}: {bb.blackboardData?.Count ?? 0} entradas");
                    EditorGUILayout.LabelField($"     {nodeInfo}", EditorStyles.miniLabel);
                }
                
                bool allHaveNode = preset.narrativeBlackboards.All(bb => 
                    bb.blackboardData?.Any(e => e.key == "__currentNodeGuid" && !string.IsNullOrEmpty(e.value)) == true);
                
                if (!allHaveNode)
                {
                    EditorGUILayout.HelpBox(
                        "⚠️ Algunos grafos NO tienen posición guardada (__currentNodeGuid).\n\n" +
                        "Esto causará que el grafo recorra todos los nodos desde el inicio al cargar.\n\n" +
                        "SOLUCIÓN: En Play Mode, usa el botón '🔄 Actualizar ESTE Preset' para capturar el estado correcto.", 
                        MessageType.Warning);
                }
            }
            
            EditorGUILayout.Space(10);
            
            // === Diagnóstico de Flags de Quests (código existente) ===
            if (preset.flags == null || preset.flags.Count == 0)
            {
                EditorGUILayout.HelpBox("Este preset no tiene flags configurados.", MessageType.Warning);
            }
            else
            {
                var questFlags = preset.flags.Where(f => f.StartsWith("QUEST_")).ToList();
                var otherFlags = preset.flags.Where(f => !f.StartsWith("QUEST_")).ToList();
                
                EditorGUILayout.LabelField($"Total de flags: {preset.flags.Count}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"├─ Flags de quests: {questFlags.Count}");
                EditorGUILayout.LabelField($"└─ Otros flags: {otherFlags.Count}");
                
                if (questFlags.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("📋 Flags de Quests:", EditorStyles.boldLabel);
                    
                    foreach (var flag in questFlags)
                    {
                        string icon = "❓";
                        if (flag.StartsWith("QUEST_COMPLETED:")) icon = "✅";
                        else if (flag.StartsWith("QUEST_ACTIVE:")) icon = "🔵";
                        else if (flag.StartsWith("QUEST_STEP_DONE:")) icon = "✓";
                        else if (flag.StartsWith("QUEST_ARCHIVED:")) icon = "📦";
                        else if (flag.StartsWith("QUEST_FOLLOWED:")) icon = "⭐";
                        
                        EditorGUILayout.SelectableLabel($"{icon} {flag}", EditorStyles.textField, GUILayout.Height(18));
                    }
                }
                
                if (otherFlags.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("🏴 Otros Flags:", EditorStyles.boldLabel);
                    
                    foreach (var flag in otherFlags)
                    {
                        EditorGUILayout.SelectableLabel(flag, EditorStyles.textField, GUILayout.Height(18));
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // === HERRAMIENTA: Ejemplos Rápidos ===
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("⚡ Ejemplos Rápidos de Quests", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📝 Añadir Quest Activa (Ejemplo)"))
        {
            AddExampleActiveQuest(preset);
        }
        if (GUILayout.Button("✅ Añadir Quest Completada (Ejemplo)"))
        {
            AddExampleCompletedQuest(preset);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void CaptureAppearanceFromScene(PlayerPresetSO preset)
    {
        // Buscar el jugador en la escena
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró un GameObject con tag 'Player' en la escena.", "OK");
            return;
        }

        // Buscar ModularAutoBuilder
        ModularAutoBuilder builder = playerGO.GetComponentInChildren<ModularAutoBuilder>(true);
        if (builder == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró ModularAutoBuilder en el jugador.", "OK");
            return;
        }

        // Obtener la selección actual
        var selection = builder.GetSelection();
        if (selection == null || selection.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "El ModularAutoBuilder no tiene ninguna selección activa.", "OK");
            return;
        }

        // Limpiar y llenar la lista de apariencia
        Undo.RecordObject(preset, "Capturar apariencia del jugador");

        if (preset.appearance == null)
            preset.appearance = new List<AppearanceEntry>();
        else
            preset.appearance.Clear();

        foreach (var kv in selection)
        {
            // Solo agregar si hay una parte seleccionada (no null)
            if (!string.IsNullOrEmpty(kv.Value))
            {
                preset.appearance.Add(new AppearanceEntry
                {
                    category = kv.Key,
                    partName = kv.Value
                });
            }
        }

        EditorUtility.SetDirty(preset);
        Debug.Log($"[PlayerPresetSOEditor] Apariencia capturada: {preset.appearance.Count} partes guardadas en '{preset.name}'");

        // Mostrar resumen
        string summary = "Partes capturadas:\n";
        foreach (var entry in preset.appearance)
        {
            summary += $"  • {entry.category}: {entry.partName}\n";
        }
        EditorUtility.DisplayDialog("Apariencia Capturada", summary, "OK");
    }
    
    private void CreateTestPresetFromCurrentState()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Esta función solo funciona en Play Mode.", "OK");
            return;
        }
        
        // Obtener el GameBootProfile activo
        var profile = GameBootService.Profile;
        if (profile == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró GameBootProfile activo.", "OK");
            return;
        }
        
        // ✅ CRÍTICO: Actualizar el runtimePreset con el estado ACTUAL de todos los sistemas
        // Esto captura las quests, inventario, wardrobe, etc. desde los managers vivos
        Debug.Log("[PlayerPresetSOEditor] 🔄 Actualizando runtimePreset desde el estado actual...");
        profile.UpdateRuntimePresetFromCurrentState();
        
        var activePreset = profile.GetActivePresetResolved();
        if (activePreset == null)
        {
            EditorUtility.DisplayDialog("Error", "No hay preset activo en el GameBootProfile.", "OK");
            return;
        }
        
        // Log de debug para verificar que las quests se capturaron
        var questFlags = activePreset.flags?.Where(f => f.StartsWith("QUEST_")).ToList() ?? new List<string>();
        Debug.Log($"[PlayerPresetSOEditor] 📋 Flags de quests capturados: {questFlags.Count}");
        if (questFlags.Count > 0)
        {
            Debug.Log($"[PlayerPresetSOEditor] Quest flags: {string.Join(", ", questFlags)}");
        }
        else
        {
            Debug.LogWarning("[PlayerPresetSOEditor] ⚠️ No se capturaron flags de quests. ¿Hay quests activas o completadas?");
        }
        
        // Pedir nombre para el nuevo preset
        string defaultName = $"PlayerPreset_Test_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        string newName = EditorUtility.SaveFilePanelInProject(
            "Guardar Preset de Test",
            defaultName,
            "asset",
            "Introduce un nombre para el nuevo preset de test",
            "Assets/ScriptableObjects/Player"
        );
        
        if (string.IsNullOrEmpty(newName))
        {
            Debug.Log("[PlayerPresetSOEditor] Creación de preset cancelada por el usuario.");
            return;
        }
        
        // Crear una copia del preset activo
        PlayerPresetSO newPreset = ScriptableObject.CreateInstance<PlayerPresetSO>();
        
        // Copiar todos los datos del preset activo (que ya contiene el estado guardado)
        CopyPresetData(activePreset, newPreset);
        
        // Guardar el nuevo asset
        AssetDatabase.CreateAsset(newPreset, newName);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        // Seleccionar el nuevo preset en el Project
        EditorGUIUtility.PingObject(newPreset);
        Selection.activeObject = newPreset;
        
        // Analizar flags de quests para el resumen
        var questFlagsInPreset = newPreset.flags?.Where(f => f.StartsWith("QUEST_")).ToList() ?? new List<string>();
        int activeQuests = questFlagsInPreset.Count(f => f.StartsWith("QUEST_ACTIVE:"));
        int completedQuests = questFlagsInPreset.Count(f => f.StartsWith("QUEST_COMPLETED:"));
        int stepsDone = questFlagsInPreset.Count(f => f.StartsWith("QUEST_STEP_DONE:"));
        
        // Log detallado de datos capturados
        Debug.Log($"[PlayerPresetSOEditor] 📸 Datos capturados del preset:");
        Debug.Log($"  • Apariencia: {newPreset.appearance?.Count ?? 0} partes");
        if (newPreset.appearance != null && newPreset.appearance.Count > 0)
        {
            foreach (var app in newPreset.appearance)
            {
                Debug.Log($"    - {app.category}: {app.partName ?? "(null)"}");
            }
        }
        Debug.Log($"  • Party: {newPreset.partyMemberIds?.Count ?? 0} miembros");
        if (newPreset.partyMemberIds != null && newPreset.partyMemberIds.Count > 0)
        {
            foreach (var memberId in newPreset.partyMemberIds)
            {
                Debug.Log($"    - {memberId}");
            }
        }
        Debug.Log($"  • Wardrobe: {newPreset.unlockedWardrobeIds?.Count ?? 0} items");
        
        // Mostrar resumen
        string summary = $"✅ Preset de test creado exitosamente:\n\n" +
                        $"📁 Ubicación: {newName}\n\n" +
                        $"📊 Contenido capturado:\n" +
                        $"  • Nivel: {newPreset.level}\n" +
                        $"  • HP: {newPreset.currentHP}/{newPreset.maxHP}\n" +
                        $"  • MP: {newPreset.currentMP}/{newPreset.maxMP}\n" +
                        $"  • Hechizos desbloqueados: {newPreset.unlockedSpells?.Count ?? 0}\n" +
                        $"  • Items en inventario: {newPreset.inventoryItems?.Count ?? 0}\n" +
                        $"  • Apariencia: {newPreset.appearance?.Count ?? 0} partes\n" +
                        $"  • Vestuario: {newPreset.unlockedWardrobeIds?.Count ?? 0} items\n" +
                        $"  • Party members: {newPreset.partyMemberIds?.Count ?? 0} miembros\n" +
                        $"  • 📋 QUESTS:\n" +
                        $"    - Activas: {activeQuests}\n" +
                        $"    - Completadas: {completedQuests}\n" +
                        $"    - Steps completados: {stepsDone}\n" +
                        $"    - Total flags: {newPreset.flags?.Count ?? 0}\n" +
                        $"  • Narrativas completadas: {newPreset.completedInteractiveNarratives?.Count ?? 0}\n" +
                        $"  • NPCs persistidos: {newPreset.npcPositions?.Count ?? 0}\n" +
                        $"  • Bosses derrotados: {newPreset.defeatedBossIds?.Count ?? 0}\n" +
                        $"  • Puntos teleport: {newPreset.unlockedTeleportPoints?.Count ?? 0}\n\n" +
                        $"Ahora puedes usar este preset en GameBootProfile para iniciar desde este punto.";
        
        EditorUtility.DisplayDialog("Preset de Test Creado", summary, "OK");
        
        Debug.Log($"[PlayerPresetSOEditor] ✅ Preset de test creado: {newName}");
        Debug.Log($"[PlayerPresetSOEditor] 📋 Quests: {activeQuests} activas, {completedQuests} completadas, {stepsDone} steps");
        
        // Log de blackboards narrativos
        if (newPreset.narrativeBlackboards != null && newPreset.narrativeBlackboards.Count > 0)
        {
            Debug.Log($"[PlayerPresetSOEditor] 📖 Narrativas capturadas: {newPreset.narrativeBlackboards.Count} grafos");
            foreach (var bb in newPreset.narrativeBlackboards)
            {
                var currentNodeEntry = bb.blackboardData?.FirstOrDefault(e => e.key == "__currentNodeGuid");
                var hasCurrentNode = currentNodeEntry != null && !string.IsNullOrEmpty(currentNodeEntry.value);
                Debug.Log($"[PlayerPresetSOEditor]   • Grafo '{bb.graphLabel}': {bb.blackboardData?.Count ?? 0} entradas, __currentNodeGuid: {(hasCurrentNode ? "✅ SÍ" : "❌ NO")}");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerPresetSOEditor] ⚠️ No se capturaron blackboards narrativos");
        }
    }
    
    private void UpdateThisPresetFromCurrentState(PlayerPresetSO preset)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Error", "Esta función solo funciona en Play Mode.", "OK");
            return;
        }
        
        // Confirmar la acción
        if (!EditorUtility.DisplayDialog("Confirmar Actualización", 
            $"¿Estás seguro de que quieres sobrescribir '{preset.name}' con el estado actual del juego?\n\n" +
            "Esta acción NO se puede deshacer.",
            "Sí, Actualizar", "Cancelar"))
        {
            return;
        }
        
        // Obtener el GameBootProfile activo
        var profile = GameBootService.Profile;
        if (profile == null)
        {
            EditorUtility.DisplayDialog("Error", "No se encontró GameBootProfile activo.", "OK");
            return;
        }
        
        // Actualizar el runtimePreset con el estado actual
        Debug.Log("[PlayerPresetSOEditor] 🔄 Actualizando runtimePreset desde el estado actual...");
        profile.UpdateRuntimePresetFromCurrentState();
        
        var activePreset = profile.GetActivePresetResolved();
        if (activePreset == null)
        {
            EditorUtility.DisplayDialog("Error", "No hay preset activo en el GameBootProfile.", "OK");
            return;
        }
        
        // Copiar los datos al preset seleccionado
        Undo.RecordObject(preset, "Actualizar Preset desde Estado Actual");
        CopyPresetData(activePreset, preset);
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        
        // Log de verificación de blackboards
        int blackboardCount = preset.narrativeBlackboards?.Count ?? 0;
        int blackboardsWithNode = 0;
        if (preset.narrativeBlackboards != null)
        {
            foreach (var bb in preset.narrativeBlackboards)
            {
                var currentNodeEntry = bb.blackboardData?.FirstOrDefault(e => e.key == "__currentNodeGuid");
                if (currentNodeEntry != null && !string.IsNullOrEmpty(currentNodeEntry.value))
                {
                    blackboardsWithNode++;
                    Debug.Log($"[PlayerPresetSOEditor] ✅ Grafo '{bb.graphLabel}': __currentNodeGuid = {currentNodeEntry.value.Substring(0, System.Math.Min(8, currentNodeEntry.value.Length))}...");
                }
                else
                {
                    Debug.LogWarning($"[PlayerPresetSOEditor] ⚠️ Grafo '{bb.graphLabel}': NO tiene __currentNodeGuid");
                }
            }
        }
        
        // Mostrar resumen
        var questFlags = preset.flags?.Where(f => f.StartsWith("QUEST_")).ToList() ?? new List<string>();
        int activeQuests = questFlags.Count(f => f.StartsWith("QUEST_ACTIVE:"));
        int completedQuests = questFlags.Count(f => f.StartsWith("QUEST_COMPLETED:"));
        
        string summary = $"✅ Preset '{preset.name}' actualizado:\n\n" +
                        $"📊 Contenido:\n" +
                        $"  • HP: {preset.currentHP}/{preset.maxHP}\n" +
                        $"  • Quests activas: {activeQuests}, completadas: {completedQuests}\n" +
                        $"  • Bosses derrotados: {preset.defeatedBossIds?.Count ?? 0}\n" +
                        $"  • Party members: {preset.partyMemberIds?.Count ?? 0}\n\n" +
                        $"📖 Narrativas: {blackboardCount} grafos\n" +
                        $"   Con posición guardada: {blackboardsWithNode}/{blackboardCount}\n\n" +
                        (blackboardsWithNode == blackboardCount && blackboardCount > 0 
                            ? "✅ Los grafos narrativos continuarán desde donde estaban." 
                            : "⚠️ Algunos grafos empezarán desde el inicio.");
        
        EditorUtility.DisplayDialog("Preset Actualizado", summary, "OK");
        
        Debug.Log($"[PlayerPresetSOEditor] ✅ Preset '{preset.name}' actualizado con {blackboardsWithNode}/{blackboardCount} grafos con posición guardada");
    }
    
    private void CopyPresetData(PlayerPresetSO source, PlayerPresetSO destination)
    {
        // Spawn
        destination.spawnAnchorId = source.spawnAnchorId;
        
        // Stats
        destination.level = source.level;
        destination.maxHP = source.maxHP;
        destination.currentHP = source.currentHP;
        destination.maxMP = source.maxMP;
        destination.currentMP = source.currentMP;
        
        // Hechizos y abilities
        destination.unlockedAbilities = new List<AbilityId>(source.unlockedAbilities);
        destination.unlockedSpells = new List<SpellId>(source.unlockedSpells);
        destination.leftSpellId = source.leftSpellId;
        destination.rightSpellId = source.rightSpellId;
        destination.specialSpellId = source.specialSpellId;
        
        // Flags
        destination.flags = new List<string>(source.flags);
        
        // Abilities
        destination.abilities = new PlayerAbilities
        {
            swim = source.abilities.swim,
            jump = source.abilities.jump,
            climb = source.abilities.climb,
            fly = source.abilities.fly,
            magic = source.abilities.magic
        };
        
        // Apariencia
        destination.appearance = new List<AppearanceEntry>();
        foreach (var entry in source.appearance)
        {
            destination.appearance.Add(new AppearanceEntry
            {
                category = entry.category,
                partName = entry.partName
            });
        }
        
        // Vestuario
        destination.unlockedWardrobeIds = new List<string>(source.unlockedWardrobeIds);
        
        // Inventario
        destination.inventoryItems = new List<InventoryItemSave>();
        foreach (var item in source.inventoryItems)
        {
            destination.inventoryItems.Add(new InventoryItemSave
            {
                itemId = item.itemId,
                count = item.count
            });
        }
        
        // Progreso
        destination.defeatedBossIds = new List<string>(source.defeatedBossIds);
        
        // Narrativas
        destination.narrativeBlackboards = new List<PlayerSaveData.NarrativeBlackboardSnapshot>();
        foreach (var bb in source.narrativeBlackboards)
        {
            var newBb = new PlayerSaveData.NarrativeBlackboardSnapshot
            {
                graphLabel = bb.graphLabel,
                blackboardData = new List<SimpleBlackboard.Entry>()
            };
            
            if (bb.blackboardData != null)
            {
                foreach (var entry in bb.blackboardData)
                {
                    newBb.blackboardData.Add(new SimpleBlackboard.Entry
                    {
                        key = entry.key,
                        value = entry.value
                    });
                }
            }
            
            destination.narrativeBlackboards.Add(newBb);
        }
        
        // NPCs
        destination.npcPositions = new List<PlayerPresetSO.NpcPosEntry>();
        foreach (var npc in source.npcPositions)
        {
            destination.npcPositions.Add(new PlayerPresetSO.NpcPosEntry
            {
                npcId = npc.npcId,
                position = npc.position,
                hasActiveState = npc.hasActiveState,
                isActive = npc.isActive
            });
        }
        
        // Interactuables
        destination.consumedInteractableIds = new List<string>(source.consumedInteractableIds ?? new List<string>());
        
        // Narrativas interactivas
        destination.completedInteractiveNarratives = new List<string>(source.completedInteractiveNarratives ?? new List<string>());
        
        // Party members
        destination.partyMemberIds = new List<string>(source.partyMemberIds ?? new List<string>());
        
        // Teleport points
        destination.unlockedTeleportPoints = new List<string>(source.unlockedTeleportPoints ?? new List<string>());
    }
    
    private void AddExampleActiveQuest(PlayerPresetSO preset)
    {
        Undo.RecordObject(preset, "Añadir Quest Activa de Ejemplo");
        
        if (preset.flags == null) preset.flags = new List<string>();
        
        preset.flags.Add("QUEST_ACTIVE:example_quest");
        preset.flags.Add("QUEST_STEP_DONE:example_quest:0");
        
        EditorUtility.SetDirty(preset);
        
        EditorUtility.DisplayDialog("Ejemplo Añadido", 
            "Se han añadido los siguientes flags:\n\n" +
            "• QUEST_ACTIVE:example_quest\n" +
            "• QUEST_STEP_DONE:example_quest:0\n\n" +
            "Esto representa una quest activa con el primer step completado.\n\n" +
            "Reemplaza 'example_quest' con el ID real de tu quest.", 
            "OK");
    }
    
    private void AddExampleCompletedQuest(PlayerPresetSO preset)
    {
        Undo.RecordObject(preset, "Añadir Quest Completada de Ejemplo");
        
        if (preset.flags == null) preset.flags = new List<string>();
        
        preset.flags.Add("QUEST_COMPLETED:example_quest");
        
        EditorUtility.SetDirty(preset);
        
        EditorUtility.DisplayDialog("Ejemplo Añadido", 
            "Se ha añadido el siguiente flag:\n\n" +
            "• QUEST_COMPLETED:example_quest\n\n" +
            "Esto representa una quest completada.\n\n" +
            "Reemplaza 'example_quest' con el ID real de tu quest.", 
            "OK");
    }
}

