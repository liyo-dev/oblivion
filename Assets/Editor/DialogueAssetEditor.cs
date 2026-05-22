using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Custom Inspector para DialogueAsset que muestra la conversación como un guión de cine.
/// Resuelve las claves de localización desde los JSON para mostrar el texto real,
/// colorea cada speaker, muestra emociones con nombre, y valida claves faltantes.
/// </summary>
[CustomEditor(typeof(DialogueAsset))]
public class DialogueAssetEditor : Editor
{
    private const string LOCALIZATION_FOLDER = "Assets/Resources/Localization";
    private static readonly string[] Locales = { "es", "en" };
    private static readonly string[] LocaleLabels = { "ES (Español)", "EN (English)" };
    private static readonly string[] Catalogs = { "dialogues", "quests", "ui", "cinematics", "prologue", "other" };

    // Speaker colors (consistent palette)
    private static readonly Color[] SpeakerColors =
    {
        new Color(0.55f, 0.36f, 0.80f), // Purple
        new Color(0.30f, 0.55f, 0.85f), // Blue
        new Color(0.30f, 0.72f, 0.40f), // Green
        new Color(0.85f, 0.70f, 0.20f), // Yellow/Gold
        new Color(0.85f, 0.40f, 0.30f), // Red/Orange
        new Color(0.20f, 0.75f, 0.75f), // Teal
        new Color(0.80f, 0.45f, 0.65f), // Pink
        new Color(0.60f, 0.60f, 0.60f), // Gray
    };

    private static readonly Dictionary<NPCEmotion, string> EmotionNames = new Dictionary<NPCEmotion, string>
    {
        { NPCEmotion.None,      "—" },
        { NPCEmotion.Neutral,   "Neutral" },
        { NPCEmotion.Happy,     "Feliz" },
        { NPCEmotion.Sad,       "Triste" },
        { NPCEmotion.Angry,     "Enfadado" },
        { NPCEmotion.Surprised, "Sorprendido" },
        { NPCEmotion.Scared,    "Asustado" },
        { NPCEmotion.Thinking,  "Pensativo" },
        { NPCEmotion.Tired,     "Cansado" },
        { NPCEmotion.Smirk,     "Pícaro" },
    };

    private Dictionary<string, string> _locTable;
    private int _selectedLocale;
    private bool _showScriptView = true;
    private bool _showRawInspector;
    private Vector2 _scrollPos;
    private List<string> _warnings;

    void OnEnable()
    {
        _locTable = new Dictionary<string, string>(512);
        _warnings = new List<string>();
        LoadLocalization(_selectedLocale);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var asset = (DialogueAsset)target;
        if (asset.lines == null || asset.lines.Length == 0)
        {
            EditorGUILayout.HelpBox("Este DialogueAsset no tiene líneas.", MessageType.Info);
            DrawDefaultInspector();
            return;
        }

        DrawToolbar(asset);

        if (_showScriptView)
        {
            DrawScriptView(asset);
        }

        EditorGUILayout.Space(8);
        _showRawInspector = EditorGUILayout.Foldout(_showRawInspector, "Vista Raw (campos originales)", true);
        if (_showRawInspector)
        {
            DrawDefaultInspector();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawToolbar(DialogueAsset asset)
    {
        EditorGUILayout.Space(4);

        // Title
        var speakers = CollectSpeakers(asset);
        var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        EditorGUILayout.LabelField($"Vista de Guión ({asset.lines.Length} líneas, {speakers.Count} speakers)", headerStyle);

        EditorGUILayout.BeginHorizontal();

        // Locale toggle
        EditorGUI.BeginChangeCheck();
        _selectedLocale = GUILayout.Toolbar(_selectedLocale, LocaleLabels, GUILayout.Width(240));
        if (EditorGUI.EndChangeCheck())
        {
            LoadLocalization(_selectedLocale);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Validar claves", GUILayout.Width(110)))
        {
            ValidateKeys(asset);
        }

        if (GUILayout.Button("Exportar guión", GUILayout.Width(110)))
        {
            ExportScript(asset);
        }

        EditorGUILayout.EndHorizontal();

        // Speaker legend
        EditorGUILayout.BeginHorizontal();
        int colorIdx = 0;
        foreach (var speaker in speakers)
        {
            var color = SpeakerColors[colorIdx % SpeakerColors.Length];
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.normal.textColor = color;
            string resolvedName = ResolveSpeaker(speaker);
            EditorGUILayout.LabelField($"● {resolvedName}", style, GUILayout.Width(Mathf.Max(80, resolvedName.Length * 8)));
            colorIdx++;
        }
        GUILayout.FlexibleSpace();

        if (asset.isGroupConversation)
        {
            EditorGUILayout.LabelField("[Conversación grupal]", EditorStyles.miniLabel, GUILayout.Width(130));
        }

        EditorGUILayout.EndHorizontal();

        // Warnings
        if (_warnings != null && _warnings.Count > 0)
        {
            EditorGUILayout.Space(4);
            foreach (var w in _warnings)
            {
                EditorGUILayout.HelpBox(w, MessageType.Warning);
            }
        }

        EditorGUILayout.Space(4);
        DrawSeparator();
    }

    private void DrawScriptView(DialogueAsset asset)
    {
        var speakers = CollectSpeakers(asset);
        var speakerColorMap = new Dictionary<string, Color>();
        int colorIdx = 0;
        foreach (var s in speakers)
        {
            speakerColorMap[s] = SpeakerColors[colorIdx % SpeakerColors.Length];
            colorIdx++;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.MaxHeight(600));

        for (int i = 0; i < asset.lines.Length; i++)
        {
            var line = asset.lines[i];
            DrawDialogueLine(i, line, speakerColorMap);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawDialogueLine(int index, DialogueLine line, Dictionary<string, Color> speakerColorMap)
    {
        string speakerId = line.speakerNameId ?? "";
        string resolvedSpeaker = ResolveSpeaker(speakerId);
        string resolvedText = ResolveText(line);
        string emotionName = GetEmotionName(line.emotion);

        Color speakerColor = speakerColorMap.ContainsKey(speakerId)
            ? speakerColorMap[speakerId]
            : Color.gray;

        // Card background
        var bgColor = EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f)
            : new Color(0.92f, 0.92f, 0.92f);

        var cardRect = EditorGUILayout.BeginVertical(CreateCardStyle(bgColor));

        // Header: "Speaker (Emotion)"
        EditorGUILayout.BeginHorizontal();
        var speakerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            normal = { textColor = speakerColor }
        };
        string headerText = emotionName != "—"
            ? $"{resolvedSpeaker} ({emotionName})"
            : resolvedSpeaker;
        EditorGUILayout.LabelField(headerText, speakerStyle);

        // Line number
        var lineNumStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };
        EditorGUILayout.LabelField($"#{index}", lineNumStyle, GUILayout.Width(30));

        EditorGUILayout.EndHorizontal();

        // Text body
        var textStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 12,
            padding = new RectOffset(8, 8, 2, 2),
            richText = true
        };
        if (!string.IsNullOrEmpty(resolvedText))
        {
            EditorGUILayout.LabelField($"\"{resolvedText}\"", textStyle);
        }
        else
        {
            var missingStyle = new GUIStyle(textStyle)
            {
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) },
                fontStyle = FontStyle.Italic
            };
            string missingKey = !string.IsNullOrEmpty(line.textId) ? line.textId : "(sin textId)";
            EditorGUILayout.LabelField($"[FALTA: {missingKey}]", missingStyle);
        }

        // Footer: keys
        var footerStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };
        string footer = "";
        if (!string.IsNullOrEmpty(line.textId))
            footer += line.textId;
        if (!string.IsNullOrEmpty(line.speakerNameId))
            footer += $"  →  {line.speakerNameId}";
        if (line.isPlayerSpeaking)
            footer += "  [Jugador]";
        if (line.forcedShotType != default)
            footer += $"  [Plano: {line.forcedShotType}]";

        if (!string.IsNullOrEmpty(footer))
            EditorGUILayout.LabelField(footer, footerStyle);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private GUIStyle CreateCardStyle(Color bgColor)
    {
        var style = new GUIStyle("helpBox")
        {
            padding = new RectOffset(10, 10, 6, 6),
            margin = new RectOffset(4, 4, 2, 2)
        };
        return style;
    }

    private void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
    }

    // ─── Localization helpers ───

    private void LoadLocalization(int localeIndex)
    {
        _locTable.Clear();
        string locale = Locales[localeIndex];

        foreach (var catalog in Catalogs)
        {
            string path = Path.Combine(LOCALIZATION_FOLDER, $"{catalog}_{locale}.json");
            if (!File.Exists(path)) continue;

            string json = File.ReadAllText(path);
            ParseLocalizationJson(json);
        }
    }

    private void ParseLocalizationJson(string json)
    {
        // Parse "texts" format: { "texts": [{"key":"...", "value":"..."}] }
        var data = JsonUtility.FromJson<LocData>(json);
        if (data.texts != null)
        {
            foreach (var entry in data.texts)
            {
                if (!string.IsNullOrEmpty(entry.key))
                    _locTable[entry.key] = entry.value;
            }
        }
        // Parse "subtitles" format: { "subtitles": [{"id":"...", "text":"..."}] }
        if (data.subtitles != null)
        {
            foreach (var entry in data.subtitles)
            {
                if (!string.IsNullOrEmpty(entry.id))
                    _locTable[entry.id] = entry.text;
            }
        }
    }

    private string ResolveSpeaker(string speakerNameId)
    {
        if (string.IsNullOrEmpty(speakerNameId)) return "(Sin speaker)";
        if (_locTable.TryGetValue(speakerNameId, out var resolved))
            return resolved;
        // Fallback: strip CHAR_ prefix for readability
        if (speakerNameId.StartsWith("CHAR_"))
            return speakerNameId.Substring(5);
        return speakerNameId;
    }

    private string ResolveText(DialogueLine line)
    {
        // Try textId first
        if (!string.IsNullOrEmpty(line.textId) && _locTable.TryGetValue(line.textId, out var resolved))
            return resolved;
        // Fallback to inline text
        if (!string.IsNullOrEmpty(line.text))
            return line.text;
        return null;
    }

    private string GetEmotionName(NPCEmotion emotion)
    {
        return EmotionNames.TryGetValue(emotion, out var name) ? name : emotion.ToString();
    }

    private List<string> CollectSpeakers(DialogueAsset asset)
    {
        var seen = new HashSet<string>();
        var ordered = new List<string>();
        foreach (var line in asset.lines)
        {
            string id = line.speakerNameId ?? "";
            if (seen.Add(id))
                ordered.Add(id);
        }
        return ordered;
    }

    // ─── Validation ───

    private void ValidateKeys(DialogueAsset asset)
    {
        _warnings = new List<string>();

        foreach (var locale in Locales)
        {
            var table = new Dictionary<string, string>(512);
            foreach (var catalog in Catalogs)
            {
                string path = Path.Combine(LOCALIZATION_FOLDER, $"{catalog}_{locale}.json");
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

            for (int i = 0; i < asset.lines.Length; i++)
            {
                var line = asset.lines[i];

                if (!string.IsNullOrEmpty(line.textId) && !table.ContainsKey(line.textId))
                    _warnings.Add($"Línea {i}: textId \"{line.textId}\" no encontrado en {locale.ToUpper()}");

                if (!string.IsNullOrEmpty(line.speakerNameId) && !table.ContainsKey(line.speakerNameId))
                    _warnings.Add($"Línea {i}: speakerNameId \"{line.speakerNameId}\" no encontrado en {locale.ToUpper()}");
            }
        }

        if (_warnings.Count == 0)
        {
            Debug.Log($"[DialogueAssetEditor] Todas las claves de \"{target.name}\" son válidas en ES y EN.");
        }
    }

    // ─── Export ───

    private void ExportScript(DialogueAsset asset)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Guión: {asset.name}");
        sb.AppendLine($"# Líneas: {asset.lines.Length}");
        sb.AppendLine($"# Idioma: {LocaleLabels[_selectedLocale]}");
        sb.AppendLine($"# Conversación grupal: {(asset.isGroupConversation ? "Sí" : "No")}");
        sb.AppendLine();

        for (int i = 0; i < asset.lines.Length; i++)
        {
            var line = asset.lines[i];
            string speaker = ResolveSpeaker(line.speakerNameId);
            string text = ResolveText(line) ?? "[FALTA TEXTO]";
            string emotion = GetEmotionName(line.emotion);

            sb.AppendLine($"[{i}] {speaker} ({emotion}):");
            sb.AppendLine($"    \"{text}\"");
            sb.AppendLine();
        }

        string dir = "Assets/Editor/ExportedScripts";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, $"{asset.name}_script.txt");
        File.WriteAllText(filePath, sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[DialogueAssetEditor] Guión exportado a: {filePath}");
        EditorUtility.RevealInFinder(filePath);
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
