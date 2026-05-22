using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Runtime debug window that provides a unified view of all game state during Play Mode.
/// Replaces the need for separate debug keys (F4 profile, K inventory, Signal Monitor).
/// Allows viewing AND modifying: quests, events, inventory, party, flags, abilities.
/// </summary>
public class GameStateInspectorWindow : EditorWindow
{
    private enum Tab { Quests, Eventos, Inventario, Party, Flags, Abilities, Preset }

    private Tab _currentTab;
    private Vector2 _scrollPos;
    private string _filter = "";

    // Event emission
    private string _emitEventKey = "";

    // Inventory add
    private string _addItemId = "";
    private int _addItemAmount = 1;

    // Flag add
    private string _addFlagKey = "";

    // Refresh timer
    private double _lastRefresh;
    private const double RefreshInterval = 0.5;

    [MenuItem("Tools/Narrativa/Game State Inspector")]
    public static void ShowWindow()
    {
        var w = GetWindow<GameStateInspectorWindow>();
        w.titleContent = new GUIContent("Game State Inspector");
        w.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - _lastRefresh > RefreshInterval)
        {
            _lastRefresh = EditorApplication.timeSinceStartup;
            Repaint();
        }
    }

    private void OnGUI()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("El Game State Inspector solo funciona durante Play Mode.", MessageType.Info);
            return;
        }

        DrawToolbar();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case Tab.Quests:     DrawQuestsTab(); break;
            case Tab.Eventos:    DrawEventsTab(); break;
            case Tab.Inventario: DrawInventoryTab(); break;
            case Tab.Party:      DrawPartyTab(); break;
            case Tab.Flags:      DrawFlagsTab(); break;
            case Tab.Abilities:  DrawAbilitiesTab(); break;
            case Tab.Preset:     DrawPresetTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    // ─── Toolbar ───

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var tabNames = System.Enum.GetNames(typeof(Tab));
        _currentTab = (Tab)GUILayout.Toolbar((int)_currentTab, tabNames, EditorStyles.toolbarButton);

        GUILayout.FlexibleSpace();

        _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
            _filter = "";

        EditorGUILayout.EndHorizontal();
    }

    private bool MatchesFilter(string text)
    {
        if (string.IsNullOrEmpty(_filter)) return true;
        return text != null && text.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ─── Quests Tab ───

    private void DrawQuestsTab()
    {
        if (QuestManager.Instance == null)
        {
            EditorGUILayout.HelpBox("QuestManager no encontrado en la escena.", MessageType.Warning);
            return;
        }

        var quests = QuestManager.Instance.GetAll()
            .Where(q => MatchesFilter(q.Id))
            .OrderBy(q => q.State)
            .ThenBy(q => q.Id)
            .ToList();

        EditorGUILayout.LabelField($"{quests.Count} quests en runtime", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var rq in quests)
        {
            DrawQuestEntry(rq);
        }
    }

    private void DrawQuestEntry(QuestManager.RuntimeQuest rq)
    {
        var stateColor = rq.State switch
        {
            QuestState.Active => new Color(0.3f, 0.85f, 0.3f),
            QuestState.Completed => new Color(0.5f, 0.5f, 0.5f),
            QuestState.Failed => new Color(0.85f, 0.3f, 0.3f),
            _ => new Color(0.7f, 0.7f, 0.7f)
        };

        EditorGUILayout.BeginVertical("helpBox");

        EditorGUILayout.BeginHorizontal();
        var nameStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = stateColor } };
        EditorGUILayout.LabelField($"[{rq.State}] {rq.Id}", nameStyle);

        if (rq.State == QuestState.Inactive)
        {
            if (GUILayout.Button("Start", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                var signals = DefaultNarrativeSignals.EnsureInstance();
                signals.StartQuest(rq.Id, null);
            }
        }
        else if (rq.State == QuestState.Active)
        {
            if (GUILayout.Button("Complete", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                QuestManager.Instance.CompleteQuest(rq.Id);
            }
        }

        if (rq.Data != null && GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
        {
            Selection.activeObject = rq.Data;
            EditorGUIUtility.PingObject(rq.Data);
        }

        EditorGUILayout.EndHorizontal();

        // Show steps
        if (rq.Steps != null && rq.Steps.Length > 0 && rq.State == QuestState.Active)
        {
            for (int i = 0; i < rq.Steps.Length; i++)
            {
                var step = rq.Steps[i];
                var stepPrefix = step.completed ? "[OK]" : "[  ]";
                EditorGUILayout.LabelField($"  {stepPrefix} Step {i}: {step.description}", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    // ─── Events Tab ───

    private void DrawEventsTab()
    {
        var signals = Object.FindFirstObjectByType<DefaultNarrativeSignals>();
        if (signals == null)
        {
            EditorGUILayout.HelpBox("DefaultNarrativeSignals no encontrado.", MessageType.Warning);
            return;
        }

        // Emit event control
        EditorGUILayout.BeginHorizontal("helpBox");
        EditorGUILayout.LabelField("Emitir evento:", GUILayout.Width(90));
        _emitEventKey = EditorGUILayout.TextField(_emitEventKey);
        if (GUILayout.Button("Emitir", GUILayout.Width(60)) && !string.IsNullOrWhiteSpace(_emitEventKey))
        {
            signals.RaiseCustom(_emitEventKey, "[GameStateInspector]");
            _emitEventKey = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

#if UNITY_EDITOR
        // Show current state
        var pending = signals.CurrentPending;
        var raised = signals.CurrentRaised;
        var subs = signals.CurrentSubscribers;

        EditorGUILayout.LabelField("Estado actual de eventos", EditorStyles.boldLabel);

        if (raised != null && raised.Count > 0)
        {
            EditorGUILayout.LabelField($"Emitidos ({raised.Count}):", EditorStyles.boldLabel);
            foreach (var key in raised.Where(k => MatchesFilter(k)))
            {
                EditorGUILayout.LabelField($"  {key}", EditorStyles.miniLabel);
            }
        }

        if (pending != null && pending.Count > 0)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Pendientes ({pending.Count}):", EditorStyles.boldLabel);
            foreach (var key in pending.Where(k => MatchesFilter(k)))
            {
                EditorGUILayout.LabelField($"  {key}", EditorStyles.miniLabel);
            }
        }

        if (subs != null && subs.Count > 0)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Suscriptores ({subs.Count}):", EditorStyles.boldLabel);
            foreach (var kvp in subs.Where(k => MatchesFilter(k.Key)))
            {
                EditorGUILayout.LabelField($"  {kvp.Key}", EditorStyles.miniLabel);
            }
        }

        // History
        EditorGUILayout.Space(4);
        var history = DefaultNarrativeSignals.History;
        if (history != null && history.Count > 0)
        {
            EditorGUILayout.LabelField($"Historial ({history.Count} entradas):", EditorStyles.boldLabel);
            int start = Mathf.Max(0, history.Count - 30);
            for (int i = history.Count - 1; i >= start; i--)
            {
                var record = history[i];
                if (!MatchesFilter(record.key)) continue;
                EditorGUILayout.LabelField(
                    $"  [{record.status}] {record.key} — {record.detail}",
                    EditorStyles.miniLabel);
            }
        }
#endif
    }

    // ─── Inventory Tab ───

    private void DrawInventoryTab()
    {
        Inventory inventory = null;
        if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true))
            inventory = inv;

        if (inventory == null)
        {
            EditorGUILayout.HelpBox("Inventory no encontrado via PlayerService.", MessageType.Warning);
            return;
        }

        // Add item control
        EditorGUILayout.BeginHorizontal("helpBox");
        EditorGUILayout.LabelField("Item ID:", GUILayout.Width(55));
        _addItemId = EditorGUILayout.TextField(_addItemId);
        _addItemAmount = EditorGUILayout.IntField(_addItemAmount, GUILayout.Width(40));
        if (GUILayout.Button("Add", GUILayout.Width(40)) && !string.IsNullOrWhiteSpace(_addItemId))
        {
            var itemData = FindItemData(_addItemId);
            if (itemData != null)
            {
                inventory.Add(itemData, _addItemAmount);
            }
            else
            {
                Debug.LogWarning($"[GameStateInspector] No se encontró ItemData con id '{_addItemId}'");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        var items = inventory.GetAllItems()
            .Where(e => MatchesFilter(e.item != null ? e.item.itemId : "") ||
                        MatchesFilter(e.item != null ? e.item.displayName : ""))
            .OrderBy(e => e.item != null ? e.item.displayName : "")
            .ToList();

        EditorGUILayout.LabelField($"{items.Count} items en inventario", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var entry in items)
        {
            EditorGUILayout.BeginHorizontal("helpBox");
            if (entry.item != null)
            {
                EditorGUILayout.LabelField($"{entry.item.displayName} (x{entry.count})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(entry.item.itemId, EditorStyles.miniLabel, GUILayout.Width(120));
                if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    Selection.activeObject = entry.item;
                    EditorGUIUtility.PingObject(entry.item);
                }
            }
            else
            {
                EditorGUILayout.LabelField($"(unknown) x{entry.count}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private static ItemData FindItemData(string itemId)
    {
        var guids = AssetDatabase.FindAssets("t:ItemData");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null && item.itemId == itemId)
                return item;
        }
        return null;
    }

    // ─── Party Tab ───

    private void DrawPartyTab()
    {
        if (!Game.NPC.PlayerParty.HasInstance)
        {
            EditorGUILayout.HelpBox("PlayerParty no encontrado.", MessageType.Warning);
            return;
        }

        var party = Game.NPC.PlayerParty.Instance;
        var members = party.Members;

        EditorGUILayout.LabelField($"{members.Count} miembros en el equipo", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var member in members)
        {
            if (member == null) continue;
            EditorGUILayout.BeginHorizontal("helpBox");
            EditorGUILayout.LabelField(member.name, EditorStyles.boldLabel);
            if (GUILayout.Button("Sel", EditorStyles.miniButton, GUILayout.Width(30)))
            {
                Selection.activeGameObject = member.gameObject;
                EditorGUIUtility.PingObject(member.gameObject);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ─── Flags Tab ───

    private void DrawFlagsTab()
    {
        var preset = UnlockService.GetActivePreset();
        if (preset == null)
        {
            EditorGUILayout.HelpBox("No hay preset activo (UnlockService).", MessageType.Warning);
            return;
        }

        // Add flag control
        EditorGUILayout.BeginHorizontal("helpBox");
        EditorGUILayout.LabelField("Flag:", GUILayout.Width(35));
        _addFlagKey = EditorGUILayout.TextField(_addFlagKey);
        if (GUILayout.Button("Add", GUILayout.Width(40)) && !string.IsNullOrWhiteSpace(_addFlagKey))
        {
            if (!preset.flags.Contains(_addFlagKey))
            {
                preset.flags.Add(_addFlagKey);
                EditorUtility.SetDirty(preset);
            }
            _addFlagKey = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        var flags = preset.flags
            .Where(f => MatchesFilter(f))
            .OrderBy(f => f)
            .ToList();

        EditorGUILayout.LabelField($"{flags.Count} flags activos", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        foreach (var flag in flags)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(flag, EditorStyles.miniLabel);
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                preset.flags.Remove(flag);
                EditorUtility.SetDirty(preset);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ─── Abilities Tab ───

    private void DrawAbilitiesTab()
    {
        var preset = UnlockService.GetActivePreset();
        if (preset == null)
        {
            EditorGUILayout.HelpBox("No hay preset activo.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Abilities", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        var abilities = preset.abilities;
        if (abilities != null)
        {
            DrawAbilityToggle("Swim", abilities.swim, v => { abilities.swim = v; EditorUtility.SetDirty(preset); });
            DrawAbilityToggle("Jump", abilities.jump, v => { abilities.jump = v; EditorUtility.SetDirty(preset); });
            DrawAbilityToggle("Climb", abilities.climb, v => { abilities.climb = v; EditorUtility.SetDirty(preset); });
            DrawAbilityToggle("Magic", abilities.magic, v => { abilities.magic = v; EditorUtility.SetDirty(preset); });
            DrawAbilityToggle("Fly", abilities.fly, v => { abilities.fly = v; EditorUtility.SetDirty(preset); });
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Spells desbloqueados", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        if (preset.unlockedSpells != null)
        {
            foreach (var spell in preset.unlockedSpells)
            {
                EditorGUILayout.LabelField($"  {spell}", EditorStyles.miniLabel);
            }
            if (preset.unlockedSpells.Count == 0)
                EditorGUILayout.LabelField("  (ninguno)", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Spell Slots", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  Left:    {preset.leftSpellId}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"  Right:   {preset.rightSpellId}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"  Special: {preset.specialSpellId}", EditorStyles.miniLabel);
    }

    private void DrawAbilityToggle(string label, bool current, System.Action<bool> setter)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"  {label}", GUILayout.Width(120));
        var newVal = EditorGUILayout.Toggle(current, GUILayout.Width(20));
        if (newVal != current) setter(newVal);
        EditorGUILayout.EndHorizontal();
    }

    // ─── Preset Tab ───

    private void DrawPresetTab()
    {
        var preset = UnlockService.GetActivePreset();
        if (preset == null)
        {
            EditorGUILayout.HelpBox("No hay preset activo.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Preset: {preset.name}", EditorStyles.boldLabel);
        if (GUILayout.Button("Select Asset", EditorStyles.miniButton, GUILayout.Width(80)))
        {
            Selection.activeObject = preset;
            EditorGUIUtility.PingObject(preset);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  Level: {preset.level}");
        EditorGUILayout.LabelField($"  HP: {preset.currentHP:F0} / {preset.maxHP:F0}");
        EditorGUILayout.LabelField($"  MP: {preset.currentMP:F0} / {preset.maxMP:F0}");
        EditorGUILayout.LabelField($"  Spawn: {preset.spawnAnchorId}");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Resumen", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  Flags: {preset.flags?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Items inventario: {preset.inventoryItems?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Party members: {preset.partyMemberIds?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Wardrobe unlocked: {preset.unlockedWardrobeIds?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Defeated bosses: {preset.defeatedBossIds?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Teleport points: {preset.unlockedTeleportPoints?.Count ?? 0}");
        EditorGUILayout.LabelField($"  NPC positions: {preset.npcPositions?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Consumed interactables: {preset.consumedInteractableIds?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Completed narratives: {preset.completedInteractiveNarratives?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Seen lore popups: {preset.seenLorePopupIds?.Count ?? 0}");
        EditorGUILayout.LabelField($"  Narrative blackboards: {preset.narrativeBlackboards?.Count ?? 0}");

        EditorGUILayout.Space(8);

        // Quick actions
        EditorGUILayout.LabelField("Acciones rápidas", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Full HP/MP"))
        {
            preset.currentHP = preset.maxHP;
            preset.currentMP = preset.maxMP;
            EditorUtility.SetDirty(preset);
        }
        if (GUILayout.Button("Level +1"))
        {
            preset.level++;
            EditorUtility.SetDirty(preset);
        }
        EditorGUILayout.EndHorizontal();
    }
}
