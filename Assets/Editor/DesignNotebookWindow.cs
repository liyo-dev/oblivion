using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

public class DesignNotebookWindow : EditorWindow
{
    private DesignNotebook _asset;
    private SerializedObject _serialized;
    private Vector2 _scroll;
    private int _tabIndex;

    private ReorderableList _quickNotesList;
    private DesignStoryGraphView _storyGraphView;
    private Color _highlightColor = new Color(1f, 0.92f, 0.23f);
    private int _fontSize = 18;

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
        rootVisualElement.style.position = Position.Relative;
        EnsureGraphView();
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
        RefreshStoryGraph();
    }

    private void RebuildLists()
    {
        if (_asset == null || _serialized == null)
            return;

        _quickNotesList = CreateList("quickNotes", DrawQuickNote, "Notas rápidas", 6.5f);
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
            HideGraphView();
            return;
        }

        if (_serialized == null)
            _serialized = new SerializedObject(_asset);

        if (_quickNotesList == null)
            RebuildLists();

        _serialized.Update();

        var tabs = new[]
        {
            "Resumen",
            "Historia",
            "Notas rápidas",
            "Exportar"
        };

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            _tabIndex = GUILayout.Toolbar(_tabIndex, tabs, Styles.TabButton, GUILayout.Height(24));
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.Space();

        if (_tabIndex == 1)
        {
            DrawStoryTab();
        }
        else
        {
            HideGraphView();
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                EditorGUILayout.BeginVertical(Styles.SectionBox);
                EditorGUILayout.Space();
                DrawCurrentTab();
                EditorGUILayout.Space();
                EditorGUILayout.EndVertical();
            }
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
            case 2:
                DrawListSection(_quickNotesList, "Notas rápidas");
                break;
            case 3:
                DrawExportButtons();
                break;
        }
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("Resumen general", Styles.SectionTitle);
        EditorGUILayout.HelpBox("Captura la visión del proyecto con herramientas de formato enriquecido.", MessageType.None);

        var synopsisProp = _serialized.FindProperty("highLevelSynopsis");
        DrawTextFormattingTools(synopsisProp);

        var minHeight = Mathf.Max(position.height - 220f, 300f);
        var rect = GUILayoutUtility.GetRect(position.width - 32f, minHeight, GUILayout.ExpandHeight(true));
        EditorGUI.PropertyField(rect, synopsisProp, new GUIContent("Sinopsis"), true);
    }

    private void DrawTextFormattingTools(SerializedProperty prop)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Negrita", GUILayout.Width(70f)))
            InsertSnippet(prop, "<b>texto</b>");
        if (GUILayout.Button("Subrayar", GUILayout.Width(80f)))
            InsertSnippet(prop, "<u>texto</u>");
        if (GUILayout.Button("Resaltar", GUILayout.Width(80f)))
            InsertSnippet(prop, $"<color=#{ColorUtility.ToHtmlStringRGB(_highlightColor)}>texto</color>");

        _highlightColor = EditorGUILayout.ColorField(_highlightColor, GUILayout.Width(80f));
        _fontSize = EditorGUILayout.IntSlider("Tamaño de fuente", _fontSize, 10, 48);
        if (GUILayout.Button("Aplicar tamaño", GUILayout.Width(110f)))
            InsertSnippet(prop, $"<size={_fontSize}>texto</size>");
        EditorGUILayout.EndHorizontal();
    }

    private void InsertSnippet(SerializedProperty prop, string snippet)
    {
        prop.stringValue = string.IsNullOrEmpty(prop.stringValue)
            ? snippet
            : prop.stringValue + "\n" + snippet;
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

    private void DrawStoryTab()
    {
        EnsureGraphView();
        _storyGraphView.style.display = DisplayStyle.Flex;
        _storyGraphView.SetNotebook(_asset, MarkAssetDirty);

        EditorGUILayout.BeginVertical(Styles.SectionBox);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Historia principal", Styles.SectionTitle);
        EditorGUILayout.HelpBox("Añade tarjetas, cámbiales el color y conéctalas para aclarar el game design.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Nueva tarjeta", GUILayout.Width(120f)))
            _storyGraphView.CreateCard(new Vector2(position.width * 0.1f, position.height * 0.1f));
        if (GUILayout.Button("Enmarcar todo", GUILayout.Width(120f)))
            _storyGraphView.FrameAll();
        if (GUILayout.Button("Centrar", GUILayout.Width(100f)))
            _storyGraphView.FrameSelection();
        EditorGUILayout.EndHorizontal();

        var rect = GUILayoutUtility.GetRect(position.width - 32f, position.height - 220f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        LayoutGraphView(rect);
        EditorGUILayout.EndVertical();
    }

    private void LayoutGraphView(Rect rect)
    {
        _storyGraphView.style.position = Position.Absolute;
        _storyGraphView.style.left = rect.xMin;
        _storyGraphView.style.top = rect.yMin;
        _storyGraphView.style.width = rect.width;
        _storyGraphView.style.height = rect.height;
    }

    private void HideGraphView()
    {
        if (_storyGraphView != null)
            _storyGraphView.style.display = DisplayStyle.None;
    }

    private void EnsureGraphView()
    {
        if (_storyGraphView != null) return;
        _storyGraphView = new DesignStoryGraphView();
        _storyGraphView.style.display = DisplayStyle.None;
        rootVisualElement.Add(_storyGraphView);
    }

    private void RefreshStoryGraph()
    {
        if (_storyGraphView == null) return;
        if (_asset == null)
            _storyGraphView.style.display = DisplayStyle.None;
        else
            _storyGraphView.SetNotebook(_asset, MarkAssetDirty);
    }

    private void MarkAssetDirty()
    {
        if (_asset == null) return;
        EditorUtility.SetDirty(_asset);
        _serialized?.UpdateIfRequiredOrScript();
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

        sb.AppendLine("HISTORIA PRINCIPAL");
        sb.AppendLine(new string('-', 18));
        foreach (var card in _asset.storyCards)
        {
            sb.AppendLine($"• {card.title}");
            if (!string.IsNullOrEmpty(card.note)) sb.AppendLine(card.note);
            var linked = _asset.storyLinks.Where(l => l.fromGuid == card.guid).Select(l => FindCardTitle(l.toGuid)).Where(t => !string.IsNullOrEmpty(t)).ToArray();
            if (linked.Length > 0)
                sb.AppendLine("Conecta con: " + string.Join(", ", linked));
            sb.AppendLine();
        }
        sb.AppendLine();

        AppendList(sb, "NOTAS RÁPIDAS", _asset.quickNotes.Select(n => ($"• {n.title}", n.note, n.tags)));

        return sb.ToString();
    }

    private string FindCardTitle(string guid)
    {
        var card = _asset.storyCards.FirstOrDefault(c => c.guid == guid);
        return card?.title;
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

    private void CreateNewAsset()
    {
        var asset = CreateInstance<DesignNotebook>();
        var path = EditorUtility.SaveFilePanelInProject("Crear DesignNotebook", "DesignNotebook", "asset", "Selecciona ubicación para el cuaderno");
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        LoadAsset(asset);
        EditorGUIUtility.PingObject(asset);
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

internal class DesignStoryGraphView : GraphView
{
    private DesignNotebook _notebook;
    private readonly Dictionary<string, StoryCardNodeView> _nodes = new();
    private Action _onDirty;

    public DesignStoryGraphView()
    {
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        style.flexGrow = 1f;

        this.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            Vector2 world = evt.mousePosition;
            Vector2 local = contentViewContainer.WorldToLocal(world);
            CreateCard(local);
        }));

        graphViewChanged = GraphChanged;
    }

    public void SetNotebook(DesignNotebook notebook, Action onDirty)
    {
        _notebook = notebook;
        _onDirty = onDirty;
        Rebuild();
    }

    public void CreateCard(Vector2 position)
    {
        if (_notebook == null) return;
        Undo.RecordObject(_notebook, "Agregar tarjeta de historia");
        var card = new DesignStoryCard
        {
            title = "Nueva tarjeta",
            position = position
        };
        _notebook.storyCards.Add(card);
        _onDirty?.Invoke();
        Rebuild();
    }

    private void Rebuild()
    {
        DeleteElements(graphElements.ToList());
        _nodes.Clear();
        if (_notebook == null) return;

        foreach (var card in _notebook.storyCards)
        {
            var view = new StoryCardNodeView(card, MarkDirty);
            _nodes[card.guid] = view;
            AddElement(view);
        }

        foreach (var link in _notebook.storyLinks.ToList())
        {
            if (!_nodes.TryGetValue(link.fromGuid, out var from) || !_nodes.TryGetValue(link.toGuid, out var to))
                continue;
            var edge = from.Output.ConnectTo(to.Input);
            AddElement(edge);
        }
    }

    private GraphViewChange GraphChanged(GraphViewChange changes)
    {
        if (changes.edgesToCreate != null)
        {
            foreach (var e in changes.edgesToCreate)
            {
                var from = e.output.node as StoryCardNodeView;
                var to = e.input.node as StoryCardNodeView;
                if (from == null || to == null) continue;

                if (_notebook.storyLinks.Any(l => l.fromGuid == from.Card.guid && l.toGuid == to.Card.guid))
                    continue;

                Undo.RecordObject(_notebook, "Conectar tarjetas de historia");
                _notebook.storyLinks.Add(new DesignStoryLink { fromGuid = from.Card.guid, toGuid = to.Card.guid });
                AddElement(e);
                MarkDirty();
            }
            changes.edgesToCreate = null;
        }

        if (changes.elementsToRemove != null)
        {
            foreach (var el in changes.elementsToRemove)
            {
                if (el is Edge edge)
                {
                    var from = edge.output.node as StoryCardNodeView;
                    var to = edge.input.node as StoryCardNodeView;
                    if (from == null || to == null) continue;

                    var link = _notebook.storyLinks.FirstOrDefault(l => l.fromGuid == from.Card.guid && l.toGuid == to.Card.guid);
                    if (link != null)
                    {
                        Undo.RecordObject(_notebook, "Desconectar tarjetas de historia");
                        _notebook.storyLinks.Remove(link);
                        MarkDirty();
                    }
                }
                else if (el is StoryCardNodeView node)
                {
                    RemoveNode(node);
                }
            }
        }

        return changes;
    }

    private void RemoveNode(StoryCardNodeView node)
    {
        Undo.RecordObject(_notebook, "Eliminar tarjeta de historia");
        _notebook.storyCards.Remove(node.Card);
        _notebook.storyLinks.RemoveAll(l => l.fromGuid == node.Card.guid || l.toGuid == node.Card.guid);
        MarkDirty();
    }

    private void MarkDirty()
    {
        _onDirty?.Invoke();
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var result = new List<Port>();
        ports.ForEach(port =>
        {
            if (port == startPort) return;
            if (port.node == startPort.node) return;
            if (port.direction == startPort.direction) return;
            result.Add(port);
        });
        return result;
    }
}

internal class StoryCardNodeView : Node
{
    public DesignStoryCard Card { get; }
    public Port Input { get; }
    public Port Output { get; }
    private readonly Action _onDirty;

    public StoryCardNodeView(DesignStoryCard card, Action onDirty)
    {
        Card = card;
        _onDirty = onDirty;
        title = string.IsNullOrEmpty(card.title) ? "Tarjeta" : card.title;

        Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
        Input.portName = "Entradas";
        inputContainer.Add(Input);

        Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
        Output.portName = "Salidas";
        outputContainer.Add(Output);

        var titleField = new TextField("Título") { value = card.title };
        titleField.RegisterValueChangedCallback(evt =>
        {
            card.title = evt.newValue;
            title = string.IsNullOrEmpty(evt.newValue) ? "Tarjeta" : evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(titleField);

        var noteField = new TextField("Detalle") { value = card.note, multiline = true };
        noteField.style.minHeight = 80f;
        noteField.RegisterValueChangedCallback(evt =>
        {
            card.note = evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(noteField);

        var colorField = new ColorField("Color") { value = card.color };
        colorField.RegisterValueChangedCallback(evt =>
        {
            card.color = evt.newValue;
            UpdateColor();
            _onDirty?.Invoke();
        });
        mainContainer.Add(colorField);

        UpdateColor();
        RefreshExpandedState();
        RefreshPorts();
        SetPosition(new Rect(card.position, new Vector2(260, 200)));
    }

    private void UpdateColor()
    {
        mainContainer.style.backgroundColor = new StyleColor(Card.color);
        var border = Card.color;
        border.a = 1f;
        style.borderLeftColor = border;
        style.borderRightColor = border;
        style.borderTopColor = border;
        style.borderBottomColor = border;
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        Card.position = newPos.position;
        _onDirty?.Invoke();
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
