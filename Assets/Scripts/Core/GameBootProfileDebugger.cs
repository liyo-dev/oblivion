using UnityEngine;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Debugger visual para GameBootProfile.
/// Muestra el estado del profile, qué preset está activo, y el flujo de save/load.
/// </summary>
public class GameBootProfileDebugger : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Mostrar el panel de debug en pantalla")]
    public bool showDebugPanel = true;

    [Tooltip("Tecla para mostrar/ocultar el panel")]
    public KeyCode toggleKey = KeyCode.F4;

    [Tooltip("Registrar historial de operaciones (save/load/reset)")]
    public bool trackHistory = true;

    [Tooltip("Máximo de entradas en el historial")]
    public int maxHistoryEntries = 20;

    [Header("Referencias")]
    [Tooltip("El GameBootProfile a debuggear")]
    public GameBootProfile profile;

    [Tooltip("El SaveSystem activo")]
    public SaveSystem saveSystem;

    // Estado interno
    private Vector2 scrollPosition;
    private List<DebugLogEntry> history = new List<DebugLogEntry>();
    private GUIStyle panelStyle;
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle warningStyle;
    private GUIStyle successStyle;
    private bool stylesInitialized;

    private struct DebugLogEntry
    {
        public string timestamp;
        public string operation;
        public string details;
        public LogType type;
    }

    private void Awake()
    {
        // Auto-buscar referencias si no están asignadas
        if (!profile)
        {
            // Usar la propiedad estática de GameBootService
            profile = GameBootService.Profile;
        }

        if (!saveSystem)
        {
            saveSystem = FindFirstObjectByType<SaveSystem>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showDebugPanel = !showDebugPanel;
        }
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 10, 10),
            normal = { background = MakeTexture(2, 2, new Color(0, 0, 0, 0.85f)) }
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.cyan }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.white }
        };

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow }
        };

        warningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(1f, 0.5f, 0f) }
        };

        successStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = Color.green }
        };

        stylesInitialized = true;
    }

    private Texture2D MakeTexture(int width, int height, Color color)
    {
        var pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        var texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void OnGUI()
    {
        if (!showDebugPanel || !profile) return;

        InitializeStyles();

        float panelWidth = 500f;
        float panelHeight = Screen.height * 0.7f;
        Rect panelRect = new Rect(10, 10, panelWidth, panelHeight);

        GUILayout.BeginArea(panelRect, panelStyle);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Header
        GUILayout.Label($"🔍 GameBootProfile Debugger", headerStyle);
        GUILayout.Label($"Presiona [{toggleKey}] para ocultar", labelStyle);
        GUILayout.Space(10);

        // === SECCIÓN: Estado General ===
        DrawSection("Estado General", () =>
        {
            DrawKeyValue("Profile Name", profile.name);
            DrawKeyValue("Use Preset Instead of Save", profile.usePresetInsteadOfSave ? "✅ SÍ" : "❌ NO");
            DrawKeyValue("Allow Auto-Saves", profile.allowAutoSaves ? "✅ SÍ" : "❌ NO");
            
            if (saveSystem)
            {
                DrawKeyValue("Has Save File", saveSystem.HasSave() ? "✅ SÍ" : "❌ NO");
            }
            else
            {
                GUILayout.Label("⚠️ SaveSystem no encontrado", warningStyle);
            }
        });

        // === SECCIÓN: Presets ===
        DrawSection("Presets Configurados", () =>
        {
            DrawPresetInfo("Default Preset", profile.defaultPlayerPreset);
            DrawPresetInfo("Boot Preset", profile.bootPreset);
            DrawPresetInfo("Runtime Preset (activo)", profile.runtimePreset, isActive: true);
        });

        // === SECCIÓN: Estado Runtime Actual ===
        if (profile.runtimePreset)
        {
            DrawSection("Estado Runtime Actual", () =>
            {
                var p = profile.runtimePreset;
                
                DrawKeyValue("Spawn Anchor", p.spawnAnchorId ?? "(sin definir)");
                DrawKeyValue("Level", p.level.ToString());
                DrawKeyValue("HP", $"{p.currentHP:F0}/{p.maxHP:F0}");
                DrawKeyValue("MP", $"{p.currentMP:F0}/{p.maxMP:F0}");
                
                DrawKeyValue("Unlocked Abilities", (p.unlockedAbilities?.Count ?? 0).ToString());
                DrawKeyValue("Unlocked Spells", (p.unlockedSpells?.Count ?? 0).ToString());
                
                GUILayout.Space(5);
                DrawKeyValue("Left Spell", p.leftSpellId.ToString());
                DrawKeyValue("Right Spell", p.rightSpellId.ToString());
                DrawKeyValue("Special Spell", p.specialSpellId.ToString());
                
                GUILayout.Space(5);
                if (p.abilities != null)
                {
                    DrawKeyValue("Can Swim", p.abilities.swim ? "✅" : "❌");
                    DrawKeyValue("Can Jump", p.abilities.jump ? "✅" : "❌");
                    DrawKeyValue("Can Climb", p.abilities.climb ? "✅" : "❌");
                }
                else
                {
                    GUILayout.Label("⚠️ Abilities no inicializado", warningStyle);
                }

                GUILayout.Space(5);
                DrawKeyValue("Flags", (p.flags?.Count ?? 0).ToString());
                DrawKeyValue("Appearance Parts", (p.appearance?.Count ?? 0).ToString());
                DrawKeyValue("Inventory Items", (p.inventoryItems?.Count ?? 0).ToString());
                DrawKeyValue("Defeated Bosses", (p.defeatedBossIds?.Count ?? 0).ToString());
                DrawKeyValue("Narrative Blackboards", (p.narrativeBlackboards?.Count ?? 0).ToString());

                // Detalles de flags si hay
                if (p.flags != null && p.flags.Count > 0)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("📋 Flags actuales:", labelStyle);
                    int questFlags = 0;
                    int cinematicFlags = 0;
                    int otherFlags = 0;
                    
                    foreach (var flag in p.flags)
                    {
                        if (string.IsNullOrEmpty(flag)) continue;
                        if (flag.StartsWith("QUEST_")) questFlags++;
                        else if (flag.StartsWith("CINEMATIC_")) cinematicFlags++;
                        else otherFlags++;
                    }
                    
                    DrawKeyValue("  Quest Flags", questFlags.ToString());
                    DrawKeyValue("  Cinematic Flags", cinematicFlags.ToString());
                    DrawKeyValue("  Other Flags", otherFlags.ToString());
                }
            });
        }

        // === SECCIÓN: Comparación con Sistemas Vivos ===
        DrawSection("Estado de Sistemas Vivos", () =>
        {
            var healthSystem = FindFirstObjectByType<PlayerHealthSystem>();
            if (healthSystem)
            {
                DrawKeyValue("PlayerHealth (vivo)", $"{healthSystem.CurrentHealth:F0}/{healthSystem.MaxHealth:F0}");
            }

            var manaPool = FindFirstObjectByType<ManaPool>();
            if (manaPool)
            {
                DrawKeyValue("ManaPool (vivo)", $"{manaPool.Current:F0}/{manaPool.Max:F0}");
            }

            var actionManager = FindFirstObjectByType<PlayerActionManager>();
            if (actionManager)
            {
                GUILayout.Label("PlayerActionManager (vivo):", labelStyle);
                DrawKeyValue("  Swim", actionManager.AllowSwim ? "✅" : "❌");
                DrawKeyValue("  Jump", actionManager.AllowJump ? "✅" : "❌");
                DrawKeyValue("  Climb", actionManager.AllowClimb ? "✅" : "❌");
            }

            var spawnManager = SpawnManager.CurrentAnchorId;
            if (!string.IsNullOrEmpty(spawnManager))
            {
                DrawKeyValue("SpawnManager Anchor", spawnManager);
            }

            if (QuestManager.Instance)
            {
                DrawKeyValue("QuestManager", "✅ Activo");
            }

            if (NarrativeGraphHub.Instance)
            {
                var runners = NarrativeGraphHub.Instance.GetAllRunners();
                DrawKeyValue("Narrative Runners", runners?.Count.ToString() ?? "0");
            }
        });

        // === SECCIÓN: Historial ===
        if (trackHistory && history.Count > 0)
        {
            DrawSection($"Historial de Operaciones ({history.Count})", () =>
            {
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    var entry = history[i];
                    GUIStyle style = entry.type == LogType.Error ? warningStyle : 
                                   entry.type == LogType.Log ? successStyle : labelStyle;
                    
                    GUILayout.Label($"[{entry.timestamp}] {entry.operation}", style);
                    if (!string.IsNullOrEmpty(entry.details))
                    {
                        GUILayout.Label($"  → {entry.details}", labelStyle);
                    }
                    GUILayout.Space(3);
                }
            });
        }

        // === SECCIÓN: Acciones Rápidas ===
        DrawSection("Acciones de Debug", () =>
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Update Runtime from State"))
            {
                profile.UpdateRuntimePresetFromCurrentState();
                LogOperation("Update Runtime", "Sincronizado con sistemas vivos", LogType.Log);
            }
            if (GUILayout.Button("💾 Force Save"))
            {
                if (saveSystem && profile.SaveCurrentGameState(saveSystem, SaveRequestContext.Manual))
                {
                    LogOperation("Force Save", "Guardado manual ejecutado", LogType.Log);
                }
                else
                {
                    LogOperation("Force Save", "Error al guardar", LogType.Error);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📂 Load Save"))
            {
                if (saveSystem && profile.LoadProfile(saveSystem))
                {
                    LogOperation("Load Save", "Carga exitosa", LogType.Log);
                }
                else
                {
                    LogOperation("Load Save", "Error al cargar o sin save", LogType.Error);
                }
            }
            if (GUILayout.Button("🗑️ Clear History"))
            {
                history.Clear();
            }
            GUILayout.EndHorizontal();
        });

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawSection(string title, System.Action content)
    {
        GUILayout.Space(10);
        GUILayout.Label($"▶ {title}", headerStyle);
        GUILayout.Space(5);
        content?.Invoke();
    }

    private void DrawKeyValue(string key, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{key}:", labelStyle, GUILayout.Width(180));
        GUILayout.Label(value, valueStyle);
        GUILayout.EndHorizontal();
    }

    private void DrawPresetInfo(string label, PlayerPresetSO preset, bool isActive = false)
    {
        GUILayout.Space(5);
        if (preset)
        {
            string status = isActive ? "🟢" : "⚪";
            GUILayout.Label($"{status} {label}: {preset.name}", isActive ? successStyle : labelStyle);
            GUILayout.Label($"  Anchor: {preset.spawnAnchorId ?? "(sin definir)"}", labelStyle);
            GUILayout.Label($"  HP: {preset.currentHP:F0}/{preset.maxHP:F0}, MP: {preset.currentMP:F0}/{preset.maxMP:F0}", labelStyle);
        }
        else
        {
            GUILayout.Label($"❌ {label}: (no asignado)", warningStyle);
        }
    }

    /// <summary>Registra una operación en el historial (llamar desde GameBootProfile)</summary>
    public void LogOperation(string operation, string details, LogType type = LogType.Log)
    {
        if (!trackHistory) return;

        history.Add(new DebugLogEntry
        {
            timestamp = System.DateTime.Now.ToString("HH:mm:ss"),
            operation = operation,
            details = details,
            type = type
        });

        while (history.Count > maxHistoryEntries)
        {
            history.RemoveAt(0);
        }
    }

    /// <summary>Helper estático para loggear operaciones desde cualquier lugar</summary>
    public static void Log(string operation, string details, LogType type = LogType.Log)
    {
        var debugger = FindFirstObjectByType<GameBootProfileDebugger>();
        if (debugger)
        {
            debugger.LogOperation(operation, details, type);
        }
    }
}
