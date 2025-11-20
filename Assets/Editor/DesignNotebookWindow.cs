using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class DesignNotebookWindow : EditorWindow
{
    private DesignNotebook _asset;
    private SerializedObject _serialized;
    private Vector2 _scroll;

    private ReorderableList _storyBeatsList;
    private ReorderableList _graphNotesList;
    private ReorderableList _quickNotesList;
    private ReorderableList _levelIdeasList;
    private ReorderableList _tasksList;

    [MenuItem("Tools/Design/Notebook")]
    public static void Open()
    {
        var window = GetWindow<DesignNotebookWindow>();
        window.titleContent = new GUIContent("Design Notebook");
        window.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChange;
        TryUseSelectedAsset();
        RebuildLists();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChange;
    }

    private void OnSelectionChange()
    {
        if (TryUseSelectedAsset())
            Repaint();
    }

    private bool TryUseSelectedAsset()
    {
        if (Selection.activeObject is DesignNotebook notebook)
        {
            LoadAsset(notebook);
            return true;
        }

        return false;
    }

    private void LoadAsset(DesignNotebook notebook)
    {
        if (notebook == _asset) return;
        _asset = notebook;
        _serialized = _asset != null ? new SerializedObject(_asset) : null;
        RebuildLists();
    }

    private void RebuildLists()
    {
        if (_asset == null || _serialized == null)
            return;

        _storyBeatsList = CreateList("storyBeats", DrawStoryBeat, "Historia principal", 5.5f);
        _graphNotesList = CreateList("graphNotes", DrawGraphNote, "Notas vinculadas al grafo", 6.5f);
        _quickNotesList = CreateList("quickNotes", DrawQuickNote, "Notas rápidas", 5.5f);
        _levelIdeasList = CreateList("levelIdeas", DrawLevelIdea, "Ideas de nivel", 8f);
        _tasksList = CreateList("tasks", DrawTask, "Tareas", 6.5f);
    }

    private ReorderableList CreateList(string property, ReorderableList.ElementCallbackDelegate drawElement, string header, float heightMultiplier = 5f)
    {
        var prop = _serialized.FindProperty(property);
        var list = new ReorderableList(_serialized, prop, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, header, EditorStyles.boldLabel),
            elementHeight = EditorGUIUtility.singleLineHeight * heightMultiplier
        };

        list.drawElementCallback = drawElement;
        return list;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cuaderno de diseño", EditorStyles.largeLabel);

        EditorGUILayout.BeginHorizontal();
        var newAsset = (DesignNotebook)EditorGUILayout.ObjectField("Documento", _asset, typeof(DesignNotebook), false);
        if (newAsset != _asset)
            LoadAsset(newAsset);

        if (GUILayout.Button("Nuevo", GUILayout.Width(70f)))
            CreateNewAsset();
        EditorGUILayout.EndHorizontal();

        if (_asset == null)
        {
            EditorGUILayout.HelpBox("Selecciona o crea un DesignNotebook para empezar.", MessageType.Info);
            return;
        }

        if (_serialized == null)
            _serialized = new SerializedObject(_asset);

        _serialized.Update();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Resumen", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_serialized.FindProperty("highLevelSynopsis"));
        EditorGUILayout.PropertyField(_serialized.FindProperty("toneAndGoals"));

        EditorGUILayout.Space();
        _storyBeatsList?.DoLayoutList();
        EditorGUILayout.Space();
        _graphNotesList?.DoLayoutList();
        EditorGUILayout.Space();
        _quickNotesList?.DoLayoutList();
        EditorGUILayout.Space();
        _levelIdeasList?.DoLayoutList();
        EditorGUILayout.Space();
        _tasksList?.DoLayoutList();

        EditorGUILayout.Space();
        DrawExportButtons();

        EditorGUILayout.EndScrollView();

        if (_serialized.ApplyModifiedProperties())
            EditorUtility.SetDirty(_asset);
    }

    private void DrawStoryBeat(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _storyBeatsList.serializedProperty.GetArrayElementAtIndex(index);
        var line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("title"), new GUIContent("Título"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 2.5f), element.FindPropertyRelative("description"), new GUIContent("Descripción"));
        line += EditorGUIUtility.singleLineHeight * 2.5f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
    }

    private void DrawGraphNote(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _graphNotesList.serializedProperty.GetArrayElementAtIndex(index);
        float line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("title"), new GUIContent("Título"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 2.5f), element.FindPropertyRelative("note"), new GUIContent("Nota"));
        line += EditorGUIUtility.singleLineHeight * 2.5f + 2f;

        var graphProp = element.FindPropertyRelative("graph");
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), graphProp, new GUIContent("Grafo"));
        line += EditorGUIUtility.singleLineHeight + 2f;

        DrawNodeSelector(rect, ref line, element, graphProp);

        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
    }

    private void DrawNodeSelector(Rect rect, ref float line, SerializedProperty element, SerializedProperty graphProp)
    {
        var guidProp = element.FindPropertyRelative("nodeGuid");
        var titleProp = element.FindPropertyRelative("cachedNodeTitle");
        var graph = graphProp.objectReferenceValue as NarrativeGraph;

        EditorGUI.BeginDisabledGroup(graph == null);
        var nodes = graph == null ? Array.Empty<NarrativeNode>() : graph.nodes?.Where(n => n != null).ToArray();
        var labels = nodes?.Select(n => string.IsNullOrEmpty(n.displayTitle) ? n.GetType().Name : n.displayTitle).ToArray() ?? Array.Empty<string>();
        var guids = nodes?.Select(n => n.guid).ToArray() ?? Array.Empty<string>();

        var currentIndex = Array.IndexOf(guids, guidProp.stringValue);
        if (currentIndex < 0 && guids.Length > 0)
            currentIndex = 0;

        var newIndex = EditorGUI.Popup(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), "Nodo", currentIndex, labels);
        if (newIndex >= 0 && newIndex < guids.Length)
        {
            guidProp.stringValue = guids[newIndex];
            titleProp.stringValue = labels[newIndex];
        }

        EditorGUI.EndDisabledGroup();
        line += EditorGUIUtility.singleLineHeight + 2f;
    }

    private void DrawQuickNote(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _quickNotesList.serializedProperty.GetArrayElementAtIndex(index);
        float line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("title"), new GUIContent("Título"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 2.5f), element.FindPropertyRelative("note"), new GUIContent("Nota"));
        line += EditorGUIUtility.singleLineHeight * 2.5f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
    }

    private void DrawLevelIdea(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _levelIdeasList.serializedProperty.GetArrayElementAtIndex(index);
        float line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("name"), new GUIContent("Nombre"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.5f), element.FindPropertyRelative("fantasy"), new GUIContent("Fantasía"));
        line += EditorGUIUtility.singleLineHeight * 1.5f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.5f), element.FindPropertyRelative("challenges"), new GUIContent("Retos"));
        line += EditorGUIUtility.singleLineHeight * 1.5f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.5f), element.FindPropertyRelative("rewards"), new GUIContent("Recompensas"));
        line += EditorGUIUtility.singleLineHeight * 1.5f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
    }

    private void DrawTask(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _tasksList.serializedProperty.GetArrayElementAtIndex(index);
        float line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("title"), new GUIContent("Tarea"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width * 0.6f, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("owner"), new GUIContent("Responsable"));
        EditorGUI.PropertyField(new Rect(rect.x + rect.width * 0.62f, line, rect.width * 0.36f, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("state"), GUIContent.none);
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.5f), element.FindPropertyRelative("description"), new GUIContent("Descripción"));
        line += EditorGUIUtility.singleLineHeight * 1.5f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("relatedScene"), new GUIContent("Escena/Pista"));
    }

    private void DrawExportButtons()
    {
        EditorGUILayout.LabelField("Exportar", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Exportar a .txt"))
            ExportToText();
        if (GUILayout.Button("Exportar a PDF"))
            ExportToPdf();
        EditorGUILayout.EndHorizontal();
    }

    private void CreateNewAsset()
    {
        var path = EditorUtility.SaveFilePanelInProject("Nuevo cuaderno de diseño", "DesignNotebook", "asset", "Elige la ubicación para el asset.");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<DesignNotebook>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        LoadAsset(asset);
        EditorGUIUtility.PingObject(asset);
    }

    private void ExportToText()
    {
        var path = EditorUtility.SaveFilePanel("Exportar a texto", Application.dataPath, _asset.name + "_Design", "txt");
        if (string.IsNullOrEmpty(path)) return;
        File.WriteAllText(path, BuildExportBody());
        EditorUtility.RevealInFinder(path);
    }

    private void ExportToPdf()
    {
        var path = EditorUtility.SaveFilePanel("Exportar a PDF", Application.dataPath, _asset.name + "_Design", "pdf");
        if (string.IsNullOrEmpty(path)) return;
        CreateSimplePdf(path, BuildExportBody());
        EditorUtility.RevealInFinder(path);
    }

    private string BuildExportBody()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Documento: {_asset.name}");
        sb.AppendLine(new string('=', 64));
        sb.AppendLine("RESUMEN");
        sb.AppendLine(_asset.highLevelSynopsis);
        sb.AppendLine();
        sb.AppendLine("TONO Y OBJETIVOS");
        sb.AppendLine(_asset.toneAndGoals);
        sb.AppendLine();

        AppendList(sb, "HISTORIA PRINCIPAL", _asset.storyBeats.Select(b => ($"• {b.title}", b.description, b.tags)));
        AppendList(sb, "NOTAS VINCULADAS AL GRAFO", _asset.graphNotes.Select(n => ($"• {n.title} ({n.cachedNodeTitle})", n.note, n.tags)));
        AppendList(sb, "NOTAS RÁPIDAS", _asset.quickNotes.Select(n => ($"• {n.title}", n.note, n.tags)));
        AppendList(sb, "IDEAS DE NIVEL", _asset.levelIdeas.Select(l => ($"• {l.name}", BuildLevelIdeaBody(l), l.tags)));
        AppendList(sb, "TAREAS", _asset.tasks.Select(t => ($"• [{t.state}] {t.title}", BuildTaskBody(t), t.relatedScene)));

        return sb.ToString();
    }

    private void AppendList(StringBuilder sb, string header, IEnumerable<(string title, string body, string tags)> entries)
    {
        sb.AppendLine(header);
        sb.AppendLine(new string('-', header.Length));
        foreach (var (title, body, tags) in entries)
        {
            sb.AppendLine(title);
            if (!string.IsNullOrEmpty(tags))
                sb.AppendLine($"Tags: {tags}");
            sb.AppendLine(body);
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private string BuildLevelIdeaBody(LevelIdea idea)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(idea.fantasy)) sb.AppendLine($"Fantasía: {idea.fantasy}");
        if (!string.IsNullOrEmpty(idea.challenges)) sb.AppendLine($"Retos: {idea.challenges}");
        if (!string.IsNullOrEmpty(idea.rewards)) sb.AppendLine($"Recompensas: {idea.rewards}");
        return sb.ToString();
    }

    private string BuildTaskBody(DesignTask task)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(task.description)) sb.AppendLine(task.description);
        if (!string.IsNullOrEmpty(task.owner)) sb.AppendLine($"Responsable: {task.owner}");
        if (!string.IsNullOrEmpty(task.relatedScene)) sb.AppendLine($"Escena/Pista: {task.relatedScene}");
        return sb.ToString();
    }

    private void CreateSimplePdf(string path, string content)
    {
        var sanitized = content.Replace("\r\n", "\n");
        var lines = sanitized.Split('\n');
        var textBuilder = new StringBuilder();
        textBuilder.AppendLine("BT");
        textBuilder.AppendLine("/F1 12 Tf");
        textBuilder.AppendLine("1 0 0 1 72 720 Tm");
        textBuilder.AppendLine("12 TL");
        foreach (var line in lines)
        {
            textBuilder.AppendLine($"({EscapePdf(line)}) Tj");
            textBuilder.AppendLine("T*");
        }
        textBuilder.AppendLine("ET");

        var textBytes = Encoding.ASCII.GetBytes(textBuilder.ToString());
        var offsets = new List<long>();
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII);

        writer.WriteLine("%PDF-1.4");

        void WriteObject(params string[] linesToWrite)
        {
            writer.Flush();
            offsets.Add(ms.Position);
            foreach (var l in linesToWrite)
                writer.WriteLine(l);
            writer.WriteLine("endobj");
        }

        WriteObject("1 0 obj", "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject("2 0 obj", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        WriteObject("3 0 obj", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");

        writer.Flush();
        offsets.Add(ms.Position);
        writer.WriteLine("4 0 obj");
        writer.WriteLine($"<< /Length {textBytes.Length} >>");
        writer.WriteLine("stream");
        writer.Flush();
        ms.Write(textBytes, 0, textBytes.Length);
        writer.WriteLine();
        writer.WriteLine("endstream");
        writer.WriteLine("endobj");

        WriteObject("5 0 obj", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        writer.Flush();
        var startXref = ms.Position;
        writer.WriteLine("xref");
        writer.WriteLine($"0 {offsets.Count + 1}");
        writer.WriteLine("0000000000 65535 f ");
        foreach (var o in offsets)
            writer.WriteLine($"{o:0000000000} 00000 n ");
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(startXref);
        writer.WriteLine("%%EOF");

        writer.Flush();
        File.WriteAllBytes(path, ms.ToArray());
    }

    private string EscapePdf(string line)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;
        return line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
