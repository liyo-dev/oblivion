using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana para controlar el filtrado de logs por tag en tiempo de ejecución.
/// Menú: El Sendero > GameLog > Ventana de Logs
/// </summary>
public class GameLogEditorWindow : EditorWindow
{
    private Vector2 _scroll;
    private string  _filter = "";

    [MenuItem("El Sendero/GameLog/Ventana de Logs %#l")]
    static void Open() => GetWindow<GameLogEditorWindow>("GameLog").Show();

    [MenuItem("El Sendero/GameLog/Silenciar todo _F9")]
    static void DisableAll()
    {
        GameLog.DisableAll();
        Debug.Log("[GameLog] Todos los tags DESACTIVADOS");
    }

    [MenuItem("El Sendero/GameLog/Activar todo _F10")]
    static void EnableAll()
    {
        GameLog.EnableAll();
        Debug.Log("[GameLog] Todos los tags ACTIVADOS");
    }

    void OnGUI()
    {
        DrawToolbar();
        DrawTagList();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        bool globalNew = GUILayout.Toggle(GameLog.GlobalEnabled, "Global ON", EditorStyles.toolbarButton, GUILayout.Width(72));
        if (globalNew != GameLog.GlobalEnabled) GameLog.GlobalEnabled = globalNew;

        GUILayout.Space(4);

        if (GUILayout.Button("Activar todo",   EditorStyles.toolbarButton, GUILayout.Width(80))) GameLog.EnableAll();
        if (GUILayout.Button("Silenciar todo", EditorStyles.toolbarButton, GUILayout.Width(90))) GameLog.DisableAll();
        if (GUILayout.Button("Reset",          EditorStyles.toolbarButton, GUILayout.Width(50))) GameLog.ResetAll();

        GUILayout.FlexibleSpace();

        // Indicador: si los nuevos tags se admiten o se suprimen por defecto
        bool def = GameLog.DefaultNewTagEnabled;
        GUILayout.Label("Nuevos:", EditorStyles.toolbarButton, GUILayout.Width(54));
        var defColor = def ? Color.white : Color.gray;
        var prevColor = GUI.contentColor;
        GUI.contentColor = defColor;
        GUILayout.Label(def ? "visibles" : "ocultos", EditorStyles.toolbarButton, GUILayout.Width(58));
        GUI.contentColor = prevColor;

        if (GUILayout.Button("Escanear proyecto", EditorStyles.toolbarButton, GUILayout.Width(120)))
            ScanProject();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Filtrar:", GUILayout.Width(40));
        _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22))) _filter = "";
        EditorGUILayout.EndHorizontal();
    }

    void DrawTagList()
    {
        var tags = GameLog.Tags;

        if (tags.Count == 0)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "No hay tags descubiertos aún.\n" +
                "• Pulsa Play para que aparezcan automáticamente al dispararse los logs.\n" +
                "• O pulsa 'Escanear proyecto' para pre-cargar todos los tags del código.",
                MessageType.Info);
            return;
        }

        var sorted = tags
            .Where(kv => string.IsNullOrEmpty(_filter) || kv.Key.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(kv => kv.Key)
            .ToList();

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField($"{sorted.Count} / {tags.Count} tags", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(2);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (var kv in sorted)
        {
            EditorGUILayout.BeginHorizontal();

            bool newEnabled = EditorGUILayout.Toggle(kv.Value, GUILayout.Width(18));
            if (newEnabled != kv.Value) GameLog.SetTag(kv.Key, newEnabled);

            var style = kv.Value ? EditorStyles.label : new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } };
            GUILayout.Label($"[{kv.Key}]", style);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // Busca todos los tags [Tag] usados en el código y los pre-registra
    static void ScanProject()
    {
        var pattern = new Regex(@"\[(\w+)\]", RegexOptions.Compiled);
        var found   = new HashSet<string>();

        string[] files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            string text = File.ReadAllText(file);
            foreach (Match m in pattern.Matches(text))
            {
                string tag = m.Groups[1].Value;
                // Filtrar palabras que no son tags de log (SerializeField, etc.)
                if (tag.Length > 2 && tag.Length < 40 && !IsUnityKeyword(tag))
                    found.Add(tag);
            }
        }

        foreach (var tag in found)
            GameLog.RegisterTag(tag);

        Debug.Log($"[GameLog] Escaneo completo — {found.Count} tags encontrados en {files.Length} archivos.");
    }

    static bool IsUnityKeyword(string tag) => tag is
        "SerializeField" or "HideInInspector" or "RequireComponent" or
        "Header" or "Tooltip" or "Space" or "Range" or "MenuItem" or
        "DisallowMultipleComponent" or "ExecuteAlways" or "ExecuteInEditMode" or
        "CreateAssetMenu" or "CustomEditor" or "CustomPropertyDrawer" or
        "ContextMenu" or "RuntimeInitializeOnLoadMethod" or "InitializeOnLoadMethod" or
        "NonSerialized" or "System" or "UnityEngine" or "UnityEditor";

    void OnInspectorUpdate() => Repaint();
}
