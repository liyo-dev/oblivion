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
    private int _tabIndex;

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

        _storyBeatsList = CreateList("storyBeats", DrawStoryBeat, "Historia principal", 7.5f);
        _graphNotesList = CreateList("graphNotes", DrawGraphNote, "Notas vinculadas al grafo", 8.5f);
        _quickNotesList = CreateList("quickNotes", DrawQuickNote, "Notas rápidas", 6.5f);
        _levelIdeasList = CreateList("levelIdeas", DrawLevelIdea, "Ideas de nivel", 10f);
        _tasksList = CreateList("tasks", DrawTask, "Tareas", 8f);
    }

    private ReorderableList CreateList(string property, ReorderableList.ElementCallbackDelegate drawElement, string header, float heightMultiplier = 5f)
    {
        var prop = _serialized.FindProperty(property);
        var list = new ReorderableList(_serialized, prop, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, header, Styles.ListHeader),
            elementHeight = (EditorGUIUtility.singleLineHeight * heightMultiplier) + Styles.Card.padding.vertical + 6f
        };

        list.drawElementCallback = drawElement;
        list.drawElementBackgroundCallback = (rect, index, active, focused) =>
        {
            rect = Styles.Card.PaddingRect(rect, 2f);
            var bg = active ? Styles.ElementBackgroundActive : Styles.ElementBackground;
            EditorGUI.DrawRect(rect, bg);
        };
        return list;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope(Styles.HeaderBox))
        {
            EditorGUILayout.LabelField("Cuaderno de diseño", Styles.HeaderTitle);

            EditorGUILayout.BeginHorizontal();
            var newAsset = (DesignNotebook)EditorGUILayout.ObjectField("Documento", _asset, typeof(DesignNotebook), false);
            if (newAsset != _asset)
                LoadAsset(newAsset);

            if (GUILayout.Button("Nuevo", GUILayout.Width(70f)))
                CreateNewAsset();
            EditorGUILayout.EndHorizontal();
        }

        if (_asset == null)
        {
            EditorGUILayout.HelpBox("Selecciona o crea un DesignNotebook para empezar.", MessageType.Info);
            return;
        }

        if (_serialized == null)
            _serialized = new SerializedObject(_asset);

        if (_storyBeatsList == null)
            RebuildLists();

        _serialized.Update();

        var tabs = new[]
        {
            "Resumen",
            "Historia",
            "Notas de grafo",
            "Notas rápidas",
            "Ideas de nivel",
            "Tareas",
            "Exportar"
        };

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            _tabIndex = GUILayout.Toolbar(_tabIndex, tabs, Styles.TabButton, GUILayout.Height(24));
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space();

        using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scroll.scrollPosition;
            EditorGUILayout.BeginVertical(Styles.SectionBox);
            EditorGUILayout.Space();
            DrawCurrentTab();
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        if (_serialized.ApplyModifiedProperties())
            EditorUtility.SetDirty(_asset);
    }

    private void DrawCurrentTab()
    {
        switch (_tabIndex)
        {
            case 0:
                DrawSummary();
                break;
            case 1:
                DrawListSection(_storyBeatsList, "Historia principal");
                break;
            case 2:
                DrawListSection(_graphNotesList, "Notas vinculadas al grafo narrativo");
                break;
            case 3:
                DrawListSection(_quickNotesList, "Notas rápidas");
                break;
            case 4:
                DrawListSection(_levelIdeasList, "Ideas de nivel");
                break;
            case 5:
                DrawListSection(_tasksList, "Tareas y pendientes");
                break;
            case 6:
                DrawExportButtons();
                break;
        }
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("Resumen general", Styles.SectionTitle);
        EditorGUILayout.HelpBox("Captura la visión del proyecto y el tono deseado en un vistazo.", MessageType.None);

        EditorGUILayout.PropertyField(_serialized.FindProperty("highLevelSynopsis"), new GUIContent("Sinopsis"));
        EditorGUILayout.PropertyField(_serialized.FindProperty("toneAndGoals"), new GUIContent("Tono y objetivos"));
    }

    private void DrawListSection(ReorderableList list, string title)
    {
        EditorGUILayout.LabelField(title, Styles.SectionTitle);
        using (new EditorGUILayout.VerticalScope(Styles.Card))
        {
            if (list == null)
                EditorGUILayout.LabelField("Sin elementos", Styles.GhostLabel);
            else
                list.DoLayoutList();
        }
    }

    private void DrawStoryBeat(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _storyBeatsList.serializedProperty.GetArrayElementAtIndex(index);
        var line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("title"), new GUIContent("Título"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 3.5f), element.FindPropertyRelative("description"), new GUIContent("Descripción"));
        line += EditorGUIUtility.singleLineHeight * 3.5f + 2f;
        line += EditorGUIUtility.singleLineHeight + 4f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.1f), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
    }

    private void DrawGraphNote(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _graphNotesList.serializedProperty.GetArrayElementAtIndex(index);
        float line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("title"), new GUIContent("Título"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 3.2f), element.FindPropertyRelative("note"), new GUIContent("Nota"));
        line += EditorGUIUtility.singleLineHeight * 3.2f + 2f;

        var graphProp = element.FindPropertyRelative("graph");
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), graphProp, new GUIContent("Grafo"));
        line += EditorGUIUtility.singleLineHeight + 2f;

        DrawNodeSelector(rect, ref line, element, graphProp);

        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.1f), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
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
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 3f), element.FindPropertyRelative("note"), new GUIContent("Nota"));
        line += EditorGUIUtility.singleLineHeight * 3f + 4f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.1f), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
    }

    private void DrawLevelIdea(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _levelIdeasList.serializedProperty.GetArrayElementAtIndex(index);
        float line = rect.y;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("name"), new GUIContent("Nombre"));
        line += EditorGUIUtility.singleLineHeight + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 2.2f), element.FindPropertyRelative("fantasy"), new GUIContent("Fantasía"));
        line += EditorGUIUtility.singleLineHeight * 2.2f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 2.2f), element.FindPropertyRelative("challenges"), new GUIContent("Retos"));
        line += EditorGUIUtility.singleLineHeight * 2.2f + 2f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 2.2f), element.FindPropertyRelative("rewards"), new GUIContent("Recompensas"));
        line += EditorGUIUtility.singleLineHeight * 2.2f + 4f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.1f), element.FindPropertyRelative("tags"), new GUIContent("Tags"));
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
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 2.4f), element.FindPropertyRelative("description"), new GUIContent("Descripción"));
        line += EditorGUIUtility.singleLineHeight * 2.4f + 4f;
        EditorGUI.PropertyField(new Rect(rect.x, line, rect.width, EditorGUIUtility.singleLineHeight * 1.1f), element.FindPropertyRelative("relatedScene"), new GUIContent("Escena/Pista"));
    }

    private void DrawExportButtons()
    {
        EditorGUILayout.LabelField("Exportar", Styles.SectionTitle);
        EditorGUILayout.HelpBox("Obtén un documento imprimible o editable con toda la información del cuaderno.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Exportar a .txt", GUILayout.Height(30)))
                ExportToText();
            if (GUILayout.Button("Exportar a PDF", GUILayout.Height(30)))
                ExportToPdf();
        }
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
        const float margin = 72f; // 1 inch
        const float pageWidth = 612f; // 8.5in
        const float pageHeight = 792f; // 11in
        const float lineHeight = 14f;
        var maxLinesPerPage = Mathf.FloorToInt((pageHeight - (margin * 2f)) / lineHeight);

        var sanitized = content.Replace("\r\n", "\n");
        var lines = sanitized.Split('\n');

        var pageContents = new List<string>();
        var current = new StringBuilder();
        int lineCount = 0;

        void StartTextBlock()
        {
            current.AppendLine("BT");
            current.AppendLine("/F1 12 Tf");
            current.AppendLine($"1 0 0 1 {margin} {pageHeight - margin} Tm");
            current.AppendLine($"{lineHeight} TL");
        }

        void EndTextBlock()
        {
            current.AppendLine("ET");
            pageContents.Add(current.ToString());
            current.Clear();
            lineCount = 0;
        }

        StartTextBlock();
        foreach (var line in lines)
        {
            current.AppendLine($"({EscapePdf(line)}) Tj");
            current.AppendLine("T*");
            lineCount++;

            if (lineCount >= maxLinesPerPage)
            {
                EndTextBlock();
                StartTextBlock();
            }
        }
        EndTextBlock();

        var encoding = Encoding.GetEncoding(1252, new EncoderReplacementFallback("?"), new DecoderReplacementFallback("?"));
        var offsets = new Dictionary<int, long>();
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, encoding) { NewLine = "\n" };

        writer.WriteLine("%PDF-1.4");

        void WriteObject(int id, params string[] linesToWrite)
        {
            writer.Flush();
            offsets[id] = ms.Position;
            writer.WriteLine($"{id} 0 obj");
            foreach (var l in linesToWrite)
                writer.WriteLine(l);
            writer.WriteLine("endobj");
        }

        void WriteStreamObject(int id, byte[] bytes)
        {
            writer.Flush();
            offsets[id] = ms.Position;
            writer.WriteLine($"{id} 0 obj");
            writer.WriteLine($"<< /Length {bytes.Length} >>");
            writer.WriteLine("stream");
            writer.Flush();
            ms.Write(bytes, 0, bytes.Length);
            writer.WriteLine();
            writer.WriteLine("endstream");
            writer.WriteLine("endobj");
        }

        int catalogId = 1;
        int pagesId = 2;
        int nextId = 3;
        int fontId = nextId + (pageContents.Count * 2);
        var pageObjectIds = new List<int>();

        WriteObject(catalogId, $"<< /Type /Catalog /Pages {pagesId} 0 R >>");

        var contentIds = new List<int>();
        foreach (var page in pageContents)
        {
            var contentId = nextId++;
            contentIds.Add(contentId);
            var bytes = encoding.GetBytes(page);
            WriteStreamObject(contentId, bytes);

            var pageId = nextId++;
            pageObjectIds.Add(pageId);
            WriteObject(pageId, $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {pageWidth} {pageHeight}] /Contents {contentId} 0 R /Resources << /Font << /F1 {fontId} 0 R >> >> >>");
        }

        WriteObject(pagesId, $"<< /Type /Pages /Kids [{string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"))}] /Count {pageObjectIds.Count} >>");

        nextId = fontId + 1;
        WriteObject(fontId, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        writer.Flush();
        var startXref = ms.Position;
        var maxId = offsets.Keys.Max();
        writer.WriteLine("xref");
        writer.WriteLine($"0 {maxId + 1}");
        writer.WriteLine("0000000000 65535 f ");
        for (int i = 1; i <= maxId; i++)
            writer.WriteLine($"{offsets[i]:0000000000} 00000 n ");
        writer.WriteLine("trailer");
        writer.WriteLine($"<< /Size {maxId + 1} /Root {catalogId} 0 R >>");
        writer.WriteLine("startxref");
        writer.WriteLine(startXref);
        writer.WriteLine("%%EOF");

        writer.Flush();
        File.WriteAllBytes(path, ms.ToArray());
    }

    private string EscapePdf(string line)
    {
        if (string.IsNullOrEmpty(line)) return string.Empty;
        return line
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static class Styles
    {
        public static readonly GUIStyle HeaderBox;
        public static readonly GUIStyle HeaderTitle;
        public static readonly GUIStyle SectionBox;
        public static readonly GUIStyle SectionTitle;
        public static readonly GUIStyle ListHeader;
        public static readonly GUIStyle Card;
        public static readonly GUIStyle TabButton;
        public static readonly GUIStyle GhostLabel;
        public static readonly Color ElementBackground;
        public static readonly Color ElementBackgroundActive;

        static Styles()
        {
            HeaderBox = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(12, 12, 8, 10),
                margin = new RectOffset(6, 6, 4, 4)
            };

            HeaderTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };

            SectionBox = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(14, 14, 12, 14)
            };

            SectionTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(4, 4, 2, 6)
            };

            ListHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.85f, 0.9f, 1f) : new Color(0.12f, 0.18f, 0.3f) }
            };

            Card = new GUIStyle("Box")
            {
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(0, 0, 10, 10),
                normal = { background = MakeTex(EditorGUIUtility.isProSkin ? new Color(0.13f, 0.16f, 0.2f) : new Color(0.88f, 0.92f, 0.98f)) }
            };

            TabButton = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 24,
                fontSize = 11,
                margin = new RectOffset(2, 2, 2, 2)
            };

            GhostLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };

            ElementBackground = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.22f, 0.28f)
                : new Color(0.85f, 0.9f, 0.98f);

            ElementBackgroundActive = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.34f, 0.48f)
                : new Color(0.75f, 0.84f, 0.95f);
        }

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

    }
}

internal static class DesignNotebookGUIExtensions
{
    public static Rect PaddingRect(this GUIStyle style, Rect rect, float inset)
    {
        rect.x += inset;
        rect.y += inset * 0.5f;
        rect.width -= inset * 2f;
        rect.height -= inset * 1.5f;
        return rect;
    }
}
