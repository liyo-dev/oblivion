using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class LocalizationTableEditor : EditorWindow
{
    // Categorías de localización
    public enum LocalizationCategory
    {
        All,
        Dialogues,
        Quests,
        UI,
        Cinematics,
        Prologue,
        Other
    }

    [System.Serializable]
    public class LocalizationEntry
    {
        public string key;
        public string spanish;
        public string english;
        public bool isNew;
        public LocalizationCategory category;
    }

    private List<LocalizationEntry> entries = new List<LocalizationEntry>();
    private Vector2 scrollPosition;
    private string searchFilter = "";
    private string newKey = "";
    private bool showOnlyEmpty = false;
    
    // Sistema de pestañas
    private LocalizationCategory currentTab = LocalizationCategory.All;
    
    private const string LOCALIZATION_FOLDER = "Assets/Resources/Localization";

    // Mapeo de archivos a categorías
    private static readonly Dictionary<string, LocalizationCategory> FileCategoryMap = new Dictionary<string, LocalizationCategory>
    {
        { "dialogues", LocalizationCategory.Dialogues },
        { "quests", LocalizationCategory.Quests },
        { "ui", LocalizationCategory.UI },
        { "cinematicintro", LocalizationCategory.Cinematics },
        { "cinematicdemon", LocalizationCategory.Cinematics }, 
        { "prologue", LocalizationCategory.Prologue }
    };

    [MenuItem("Tools/Localization Table Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<LocalizationTableEditor>("Localization Table");
        window.minSize = new Vector2(1100, 600);
    }

    void OnEnable()
    {
        LoadData();
    }

    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        // Header with controls
        DrawHeader();
        
        // Tabs
        DrawTabs();
        
        // Search and filter controls
        DrawControls();
        
        // Table headers
        DrawTableHeaders();
        
        // Scrollable table content
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawTableContent();
        EditorGUILayout.EndScrollView();
        
        // Add new entry section
        DrawAddNewSection();
        
        // Save buttons
        DrawSaveButtons();
        
        EditorGUILayout.EndVertical();
    }

    void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Localization Table Editor", EditorStyles.boldLabel);
        var filteredCount = GetFilteredEntries().Count;
        EditorGUILayout.LabelField($"Showing: {filteredCount} / Total: {entries.Count}", EditorStyles.miniLabel);
        EditorGUILayout.Space();
    }

    void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal();
        
        var tabStyle = new GUIStyle(GUI.skin.button);
        tabStyle.fontSize = 12;
        tabStyle.padding = new RectOffset(10, 10, 5, 5);
        
        foreach (LocalizationCategory category in System.Enum.GetValues(typeof(LocalizationCategory)))
        {
            var wasSelected = currentTab == category;
            var bgColor = GUI.backgroundColor;
            
            if (wasSelected)
            {
                GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            }
            
            var categoryCount = category == LocalizationCategory.All 
                ? entries.Count 
                : entries.Count(e => e.category == category);
            
            var label = $"{category} ({categoryCount})";
            
            if (GUILayout.Button(label, tabStyle, GUILayout.Height(30)))
            {
                currentTab = category;
            }
            
            GUI.backgroundColor = bgColor;
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    void DrawControls()
    {
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter, GUILayout.ExpandWidth(true));
        
        showOnlyEmpty = EditorGUILayout.ToggleLeft("Show only empty", showOnlyEmpty, GUILayout.Width(120));
        
        if (GUILayout.Button("Reload", GUILayout.Width(60)))
        {
            LoadData();
        }
        
        if (GUILayout.Button("Export All", GUILayout.Width(80)))
        {
            ExportAllToCSV();
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    void DrawTableHeaders()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Key", EditorStyles.boldLabel, GUILayout.Width(250));
        EditorGUILayout.LabelField("Category", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField("Spanish", EditorStyles.boldLabel, GUILayout.Width(300));
        EditorGUILayout.LabelField("English", EditorStyles.boldLabel, GUILayout.Width(300));
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        
        // Separator line
        EditorGUILayout.Space(2);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, Color.gray);
        EditorGUILayout.Space(2);
    }

    void DrawTableContent()
    {
        var filteredEntries = GetFilteredEntries();
        var entriesToRemove = new List<LocalizationEntry>();
        
        for (int i = 0; i < filteredEntries.Count; i++)
        {
            var entry = filteredEntries[i];
            
            // Highlight new entries
            if (entry.isNew)
            {
                var bgColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.green;
                EditorGUILayout.BeginHorizontal("box");
                GUI.backgroundColor = bgColor;
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
            }
            
            // Key (read-only for existing entries)
            if (entry.isNew)
            {
                entry.key = EditorGUILayout.TextField(entry.key, GUILayout.Width(250));
            }
            else
            {
                EditorGUILayout.SelectableLabel(entry.key, GUILayout.Width(250), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            
            // Category dropdown
            entry.category = (LocalizationCategory)EditorGUILayout.EnumPopup(entry.category, GUILayout.Width(100));
            
            // Spanish text
            entry.spanish = EditorGUILayout.TextArea(entry.spanish, GUILayout.Width(300), GUILayout.Height(EditorGUIUtility.singleLineHeight * 2));
            
            // English text
            entry.english = EditorGUILayout.TextArea(entry.english, GUILayout.Width(300), GUILayout.Height(EditorGUIUtility.singleLineHeight * 2));
            
            // Actions
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            
            if (entry.isNew)
            {
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    if (!string.IsNullOrEmpty(entry.key))
                    {
                        entry.isNew = false;
                    }
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(60)))
                {
                    entriesToRemove.Add(entry);
                }
            }
            else
            {
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("Delete Entry", 
                        $"Are you sure you want to delete '{entry.key}'?", "Yes", "No"))
                    {
                        entriesToRemove.Add(entry);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            
            // Add spacing between rows
            EditorGUILayout.Space(2);
        }
        
        // Remove entries after the loop to avoid breaking GUI layout
        foreach (var entryToRemove in entriesToRemove)
        {
            entries.Remove(entryToRemove);
        }
    }

    void DrawAddNewSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add New Entry", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Key:", GUILayout.Width(50));
        newKey = EditorGUILayout.TextField(newKey, GUILayout.Width(200));
        
        if (GUILayout.Button("Add New Entry", GUILayout.Width(100)))
        {
            if (!string.IsNullOrEmpty(newKey) && !entries.Any(e => e.key == newKey))
            {
                entries.Add(new LocalizationEntry 
                { 
                    key = newKey, 
                    spanish = "", 
                    english = "", 
                    isNew = true,
                    category = currentTab == LocalizationCategory.All ? LocalizationCategory.Other : currentTab
                });
                newKey = "";
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawSaveButtons()
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Save All Changes", GUILayout.Height(30)))
        {
            SaveData();
            EditorUtility.DisplayDialog("Saved", "Localization files saved successfully!", "OK");
        }
        
        if (GUILayout.Button("Revert Changes", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Revert", "Are you sure? All unsaved changes will be lost.", "Yes", "No"))
            {
                LoadData();
            }
        }
        
        EditorGUILayout.EndHorizontal();
    }

    List<LocalizationEntry> GetFilteredEntries()
    {
        var filtered = entries.AsEnumerable();
        
        // Filter by current tab
        if (currentTab != LocalizationCategory.All)
        {
            filtered = filtered.Where(e => e.category == currentTab);
        }
        
        // Filter by search
        if (!string.IsNullOrEmpty(searchFilter))
        {
            filtered = filtered.Where(e => 
                e.key.ToLower().Contains(searchFilter.ToLower()) ||
                e.spanish.ToLower().Contains(searchFilter.ToLower()) ||
                e.english.ToLower().Contains(searchFilter.ToLower()));
        }
        
        // Filter by empty
        if (showOnlyEmpty)
        {
            filtered = filtered.Where(e => 
                string.IsNullOrEmpty(e.spanish) || 
                string.IsNullOrEmpty(e.english));
        }
        
        return filtered.OrderBy(e => e.key).ToList();
    }

    void LoadData()
    {
        entries.Clear();
        
        if (!Directory.Exists(LOCALIZATION_FOLDER))
        {
            Debug.LogError($"Localization folder not found: {LOCALIZATION_FOLDER}");
            return;
        }

        // Cargar todos los archivos JSON en la carpeta
        var jsonFiles = Directory.GetFiles(LOCALIZATION_FOLDER, "*.json");
        
        // Agrupar por idioma
        var spanishFiles = jsonFiles.Where(f => f.Contains("_es.json")).ToList();
        var englishFiles = jsonFiles.Where(f => f.Contains("_en.json")).ToList();
        
        // Crear un diccionario para español e inglés
        var spanishData = new Dictionary<string, string>();
        var englishData = new Dictionary<string, string>();
        var categoryData = new Dictionary<string, LocalizationCategory>();
        
        // Cargar archivos españoles
        foreach (var file in spanishFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file).Replace("_es", "");
            var category = GetCategoryFromFileName(fileName);
            var data = LoadJsonFile(file);
            
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    spanishData[kvp.Key] = kvp.Value;
                    categoryData[kvp.Key] = category;
                }
            }
        }
        
        // Cargar archivos ingleses
        foreach (var file in englishFiles)
        {
            var data = LoadJsonFile(file);
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    englishData[kvp.Key] = kvp.Value;
                }
            }
        }
        
        // Combinar todas las claves
        var allKeys = new HashSet<string>();
        allKeys.UnionWith(spanishData.Keys);
        allKeys.UnionWith(englishData.Keys);
        
        foreach (var key in allKeys)
        {
            entries.Add(new LocalizationEntry
            {
                key = key,
                spanish = spanishData.GetValueOrDefault(key, ""),
                english = englishData.GetValueOrDefault(key, ""),
                isNew = false,
                category = categoryData.GetValueOrDefault(key, DetectCategory(key))
            });
        }
        
        Debug.Log($"[LocalizationTableEditor] Loaded {entries.Count} entries from {spanishFiles.Count} file pairs");
        Repaint();
    }

    LocalizationCategory GetCategoryFromFileName(string fileName)
    {
        foreach (var kvp in FileCategoryMap)
        {
            if (fileName.ToLower().Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }
        return LocalizationCategory.Other;
    }

    LocalizationCategory DetectCategory(string key)
    {
        // Auto-detect category based on key prefix
        if (key.StartsWith("DLG_") || key.StartsWith("CHAR_"))
            return LocalizationCategory.Dialogues;
        
        if (key.StartsWith("QUEST_"))
            return LocalizationCategory.Quests;
        
        if (key.StartsWith("UI_") || key.StartsWith("BTN_") || key.StartsWith("MENU_") || 
            key.StartsWith("Settings_") || key.StartsWith("MainMenu_") || key.StartsWith("Interact_") || 
            key.StartsWith("System_") || key.StartsWith("SAVEPOINT_") || key.StartsWith("INTERACT_"))
            return LocalizationCategory.UI;
        
        if (key.StartsWith("CIN_") || key.StartsWith("CUTSCENE_") || key.StartsWith("INTRO_"))
            return LocalizationCategory.Cinematics;
        
        if (key.StartsWith("PROLOGUE_"))
            return LocalizationCategory.Prologue;
        
        return LocalizationCategory.Other;
    }

    Dictionary<string, string> LoadJsonFile(string path)
    {
        if (!File.Exists(path)) return null;
        
        try
        {
            var json = File.ReadAllText(path);
            var dict = new Dictionary<string, string>();
            
            // Intentar cargar con estructura estándar (texts)
            try
            {
                var data = JsonUtility.FromJson<LocalizationData>(json);
                if (data?.texts != null)
                {
                    foreach (var text in data.texts)
                    {
                        dict[text.key] = text.value;
                    }
                    return dict;
                }
            }
            catch { /* Intentar otra estructura */ }
            
            // Intentar cargar con estructura de subtítulos (subtitles)
            try
            {
                var subtitleData = JsonUtility.FromJson<SubtitleData>(json);
                if (subtitleData?.subtitles != null)
                {
                    foreach (var subtitle in subtitleData.subtitles)
                    {
                        dict[subtitle.id] = subtitle.text;
                    }
                    return dict;
                }
            }
            catch { /* No se pudo cargar con ninguna estructura */ }
            
            Debug.LogWarning($"[LocalizationTableEditor] No se pudo determinar la estructura de {Path.GetFileName(path)}");
            return dict;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading {path}: {e.Message}");
            return null;
        }
    }

    void SaveData()
    {
        // Agrupar entradas por categoría
        var entriesByCategory = entries.Where(e => !e.isNew)
            .GroupBy(e => e.category)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        // Guardar cada categoría en su archivo correspondiente
        foreach (var kvp in FileCategoryMap)
        {
            var category = kvp.Value;
            var fileName = kvp.Key;
            
            if (entriesByCategory.TryGetValue(category, out var categoryEntries))
            {
                // Determinar si es un archivo de subtítulos (prologue, cinematicintro, cinematicdemon)
                bool isSubtitleFormat = fileName == "prologue" || fileName == "cinematicintro" || fileName == "cinematicdemon";
                
                // Guardar español
                var esPath = Path.Combine(LOCALIZATION_FOLDER, $"{fileName}_es.json");
                if (isSubtitleFormat)
                    SaveSubtitleJsonFile(esPath, categoryEntries.Select(e => new SubtitleEntry { id = e.key, text = e.spanish }));
                else
                    SaveJsonFile(esPath, categoryEntries.Select(e => new TextEntry { key = e.key, value = e.spanish }));
                
                // Guardar inglés
                var enPath = Path.Combine(LOCALIZATION_FOLDER, $"{fileName}_en.json");
                if (isSubtitleFormat)
                    SaveSubtitleJsonFile(enPath, categoryEntries.Select(e => new SubtitleEntry { id = e.key, text = e.english }));
                else
                    SaveJsonFile(enPath, categoryEntries.Select(e => new TextEntry { key = e.key, value = e.english }));
            }
        }
        
        // Guardar entradas "Other" en un archivo separado si existen
        if (entriesByCategory.TryGetValue(LocalizationCategory.Other, out var otherEntries))
        {
            var esPath = Path.Combine(LOCALIZATION_FOLDER, "other_es.json");
            var enPath = Path.Combine(LOCALIZATION_FOLDER, "other_en.json");
            SaveJsonFile(esPath, otherEntries.Select(e => new TextEntry { key = e.key, value = e.spanish }));
            SaveJsonFile(enPath, otherEntries.Select(e => new TextEntry { key = e.key, value = e.english }));
        }
        
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationTableEditor] All files saved successfully");
    }

    void SaveJsonFile(string path, IEnumerable<TextEntry> textEntries)
    {
        var data = new LocalizationData
        {
            texts = textEntries.OrderBy(t => t.key).ToArray()
        };
        
        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
    }

    void SaveSubtitleJsonFile(string path, IEnumerable<SubtitleEntry> subtitleEntries)
    {
        var data = new SubtitleData
        {
            subtitles = subtitleEntries.OrderBy(s => s.id).ToArray()
        };
        
        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
    }

    void ExportAllToCSV()
    {
        var csvPath = EditorUtility.SaveFilePanel("Export to CSV", "", "localization.csv", "csv");
        if (string.IsNullOrEmpty(csvPath)) return;
        
        using (var writer = new StreamWriter(csvPath))
        {
            writer.WriteLine("Key,Category,Spanish,English");
            foreach (var entry in entries.OrderBy(e => e.category).ThenBy(e => e.key))
            {
                writer.WriteLine($"\"{entry.key}\",\"{entry.category}\",\"{entry.spanish.Replace("\"", "\"\"")}\",\"{entry.english.Replace("\"", "\"\"")}\"");
            }
        }
        
        EditorUtility.DisplayDialog("Export Complete", $"Exported {entries.Count} entries to CSV", "OK");
    }

    [System.Serializable]
    public class LocalizationData
    {
        public TextEntry[] texts;
    }

    [System.Serializable]
    public class TextEntry
    {
        public string key;
        public string value;
    }

    [System.Serializable]
    public class SubtitleData
    {
        public SubtitleEntry[] subtitles;
    }

    [System.Serializable]
    public class SubtitleEntry
    {
        public string id;
        public string text;
    }
}
