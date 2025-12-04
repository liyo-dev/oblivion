using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class DesignNotebookWindow : EditorWindow
{
    private DesignNotebook _asset;
    private SerializedObject _serialized;
    private Vector2 _scroll;
    private int _tabIndex;
    private DesignStoryGraphView _storyGraphView;
    private int _fontSize = 18;
    private Vector2 _synopsisScroll;
    private bool _isPreviewMode;
    private Vector2 _quickNotesScroll;
    private int _draggingNoteIndex = -1;
    private Vector2 _dragOffset;

    private const string SynopsisControlName = "DesignNotebook_Synopsis";

    [MenuItem("Tools/Design/Notebook")]
    public static void Open()
    {
        var window = GetWindow<DesignNotebookWindow>();
        window.titleContent = new GUIContent("Design Notebook");
        window.Show();
    }

    [MenuItem("Window/General/Design Notebook")]
    public static void OpenDocked()
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
        EnsureNotebookCollections();
        RefreshStoryGraph();
    }

    private void EnsureNotebookCollections()
    {
        if (_asset == null) return;

        bool changed = false;

        if (_asset.storyCards == null)
        {
            _asset.storyCards = new List<DesignStoryCard>();
            changed = true;
        }

        if (_asset.storyLinks == null)
        {
            _asset.storyLinks = new List<DesignStoryLink>();
            changed = true;
        }

        if (_asset.quickNotes == null)
        {
            _asset.quickNotes = new List<DesignScratch>();
            changed = true;
        }

        if (changed)
        {
            MarkAssetDirty();
            _serialized?.UpdateIfRequiredOrScript();
        }
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

        _serialized.Update();
        EnsureNotebookCollections();
        _serialized.UpdateIfRequiredOrScript();

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
                DrawQuickNotesBoard();
                break;
            case 3:
                DrawExportButtons();
                break;
        }
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("Resumen general", Styles.SectionTitle);
        EditorGUILayout.HelpBox("Captura la visión del proyecto. Puedes alternar entre edición y vista previa.", MessageType.None);

        var synopsisProp = _serialized.FindProperty("highLevelSynopsis");
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            var newPreview = GUILayout.Toolbar(_isPreviewMode ? 1 : 0, new[] { "Editar", "Ver" }, Styles.TabButton, GUILayout.Width(160f));
            _isPreviewMode = newPreview == 1;
        }

        var minHeight = Mathf.Max(position.height - 220f, 300f);
        using (var synopsisScroll = new EditorGUILayout.ScrollViewScope(_synopsisScroll, GUILayout.Height(minHeight)))
        {
            _synopsisScroll = synopsisScroll.scrollPosition;
            if (_isPreviewMode)
            {
                EditorGUILayout.LabelField(synopsisProp.stringValue, Styles.RichPreview, GUILayout.ExpandHeight(true));
            }
            else
            {
                GUI.SetNextControlName(SynopsisControlName);
                var synopsis = EditorGUILayout.TextArea(
                    synopsisProp.stringValue,
                    Styles.GetRichTextArea(_fontSize),
                    GUILayout.ExpandHeight(true));
                synopsisProp.stringValue = synopsis;
            }
        }
    }


    private void DrawQuickNotesBoard()
    {
        EditorGUILayout.LabelField("Notas rápidas", Styles.SectionTitle);
        EditorGUILayout.HelpBox("Organiza ideas en un tablero tipo corcho con tarjetas visibles.", MessageType.None);

        var quickNotesProp = _serialized.FindProperty("quickNotes");
        const float cardWidth = 230f;
        const float cardHeight = 190f;
        const float cardSpacing = 18f;
        float viewWidth = position.width - 56f;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Nueva nota", GUILayout.Width(110f)))
        {
            quickNotesProp.arraySize++;
            var element = quickNotesProp.GetArrayElementAtIndex(quickNotesProp.arraySize - 1);
            element.FindPropertyRelative("title").stringValue = "Nueva nota";
            element.FindPropertyRelative("note").stringValue = string.Empty;
            element.FindPropertyRelative("tags").stringValue = string.Empty;
            element.FindPropertyRelative("color").colorValue = Styles.DefaultNoteColor;
            var positionProp = element.FindPropertyRelative("position");
            positionProp.vector2Value = GetQuickNoteGridPosition(quickNotesProp.arraySize - 1, viewWidth, cardWidth, cardHeight, cardSpacing);
            _quickNotesScroll = Vector2.zero;
            quickNotesProp.serializedObject.ApplyModifiedProperties();
            _serialized.Update();
            MarkAssetDirty();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();

        float boardHeight = Mathf.Max(position.height - 260f, cardHeight + 40f);
        var boardRect = GUILayoutUtility.GetRect(viewWidth, boardHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (quickNotesProp.arraySize == 0)
        {
            EditorGUI.LabelField(boardRect, "Añade tu primera nota para empezar a organizar.", Styles.GhostLabelCentered);
            return;
        }

        EnsureQuickNotePositions(quickNotesProp, viewWidth, cardWidth, cardHeight, cardSpacing);

        var contentSize = CalculateQuickNotesContentSize(quickNotesProp, cardWidth, cardHeight, cardSpacing, boardRect);
        _quickNotesScroll = GUI.BeginScrollView(boardRect, _quickNotesScroll, new Rect(0, 0, contentSize.x, contentSize.y));

        int deleteIndex = -1;
        var evt = Event.current;
        for (int i = 0; i < quickNotesProp.arraySize; i++)
        {
            var element = quickNotesProp.GetArrayElementAtIndex(i);
            var positionProp = element.FindPropertyRelative("position");
            var cardRect = new Rect(positionProp.vector2Value.x, positionProp.vector2Value.y, cardWidth, cardHeight);

            var dragRect = new Rect(cardRect.x + (cardWidth * 0.5f) - 40f, cardRect.y, 80f, 22f);
            EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.Pan);

            HandleQuickNoteDragging(evt, cardRect, dragRect, i, positionProp);

            if (DrawQuickNoteCard(cardRect, element))
                deleteIndex = i;
        }

        GUI.EndScrollView();

        if (evt.type == EventType.MouseUp && _draggingNoteIndex >= 0)
            _draggingNoteIndex = -1;

        if (deleteIndex >= 0)
            quickNotesProp.DeleteArrayElementAtIndex(deleteIndex);
    }

    private Vector2 CalculateQuickNotesContentSize(SerializedProperty quickNotesProp, float cardWidth, float cardHeight, float spacing, Rect viewRect)
    {
        float maxX = 0f;
        float maxY = 0f;
        for (int i = 0; i < quickNotesProp.arraySize; i++)
        {
            var pos = quickNotesProp.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector2Value;
            maxX = Mathf.Max(maxX, pos.x + cardWidth + spacing);
            maxY = Mathf.Max(maxY, pos.y + cardHeight + spacing);
        }

        return new Vector2(
            Mathf.Max(viewRect.width, maxX),
            Mathf.Max(viewRect.height, maxY));
    }

    private void HandleQuickNoteDragging(Event evt, Rect cardRect, Rect dragRect, int index, SerializedProperty positionProp)
    {
        switch (evt.type)
        {
            case EventType.MouseDown:
                if (evt.button == 0 && dragRect.Contains(evt.mousePosition))
                {
                    _draggingNoteIndex = index;
                    _dragOffset = evt.mousePosition - cardRect.position;
                    GUI.FocusControl(null);
                    evt.Use();
                }
                break;
            case EventType.MouseDrag:
                if (_draggingNoteIndex == index)
                {
                    var newPos = evt.mousePosition - _dragOffset;
                    newPos.x = Mathf.Max(0f, newPos.x);
                    newPos.y = Mathf.Max(0f, newPos.y);
                    positionProp.vector2Value = newPos;
                    MarkAssetDirty();
                    Repaint();
                    evt.Use();
                }
                break;
            case EventType.MouseUp:
                if (_draggingNoteIndex == index)
                {
                    _draggingNoteIndex = -1;
                    evt.Use();
                }
                break;
        }
    }

    private Vector2 GetQuickNoteGridPosition(int index, float viewWidth, float cardWidth, float cardHeight, float spacing)
    {
        int columns = Mathf.Max(1, Mathf.FloorToInt(viewWidth / (cardWidth + spacing)));
        int col = index % columns;
        int row = Mathf.Max(0, index / columns);
        return new Vector2(col * (cardWidth + spacing), row * (cardHeight + spacing));
    }

    private void ReflowQuickNotePositions(SerializedProperty quickNotesProp, float viewWidth, float cardWidth, float cardHeight, float spacing)
    {
        for (int i = 0; i < quickNotesProp.arraySize; i++)
        {
            var element = quickNotesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("position").vector2Value = GetQuickNoteGridPosition(i, viewWidth, cardWidth, cardHeight, spacing);
        }
    }

    private void EnsureQuickNotePositions(SerializedProperty quickNotesProp, float viewWidth, float cardWidth, float cardHeight, float spacing)
    {
        bool needsLayout = false;
        for (int i = 0; i < quickNotesProp.arraySize; i++)
        {
            var positionProp = quickNotesProp.GetArrayElementAtIndex(i).FindPropertyRelative("position");
            if (positionProp.vector2Value == Vector2.zero)
            {
                positionProp.vector2Value = GetQuickNoteGridPosition(i, viewWidth, cardWidth, cardHeight, spacing);
                needsLayout = true;
            }
        }

        if (needsLayout)
            MarkAssetDirty();
    }

    private bool DrawQuickNoteCard(Rect rect, SerializedProperty element)
    {
        var colorProp = element.FindPropertyRelative("color");
        var titleProp = element.FindPropertyRelative("title");
        var noteProp = element.FindPropertyRelative("note");
        var tagsProp = element.FindPropertyRelative("tags");

        EditorGUI.DrawRect(rect, Styles.NoteShadow);
        var inner = Styles.NoteCard.PaddingRect(rect, 6f);
        EditorGUI.DrawRect(inner, colorProp.colorValue);

        var pinRect = new Rect(inner.x + (inner.width * 0.5f) - 6f, inner.y - 6f, 12f, 12f);
        EditorGUI.DrawRect(pinRect, Styles.PinShadow);
        EditorGUI.DrawRect(new Rect(pinRect.x + 1f, pinRect.y + 1f, pinRect.width - 2f, pinRect.height - 2f), Styles.PinColor);

        var deleteRect = new Rect(inner.xMax - 18f, inner.y + 4f, 14f, 14f);
        if (GUI.Button(deleteRect, new GUIContent("✕", "Eliminar nota"), Styles.MiniIconButton))
            return true;

        var contentRect = Styles.Card.PaddingRect(inner, 6f);
        var line = contentRect.y;

        EditorGUI.LabelField(new Rect(contentRect.x, line, contentRect.width - 20f, EditorGUIUtility.singleLineHeight), "Título", Styles.NoteLabel);
        line += EditorGUIUtility.singleLineHeight + 2f;
        titleProp.stringValue = EditorGUI.TextField(new Rect(contentRect.x, line, contentRect.width - 20f, EditorGUIUtility.singleLineHeight), titleProp.stringValue, Styles.NoteTitleField);

        line += EditorGUIUtility.singleLineHeight + 4f;
        EditorGUI.LabelField(new Rect(contentRect.x, line, contentRect.width, EditorGUIUtility.singleLineHeight), "Nota", Styles.NoteLabel);
        line += EditorGUIUtility.singleLineHeight + 2f;
        var noteRect = new Rect(contentRect.x, line, contentRect.width, contentRect.height - (EditorGUIUtility.singleLineHeight * 3.2f));
        noteProp.stringValue = EditorGUI.TextArea(noteRect, noteProp.stringValue, Styles.NoteBody);

        line = noteRect.yMax + 4f;
        EditorGUI.LabelField(new Rect(contentRect.x, line, contentRect.width, EditorGUIUtility.singleLineHeight), "Tags", Styles.NoteLabel);
        line += EditorGUIUtility.singleLineHeight + 2f;
        tagsProp.stringValue = EditorGUI.TextField(new Rect(contentRect.x, line, contentRect.width - 80f, EditorGUIUtility.singleLineHeight), tagsProp.stringValue, Styles.NoteTitleField);

        var colorPickerRect = new Rect(contentRect.xMax - 70f, line - 2f, 68f, EditorGUIUtility.singleLineHeight + 4f);
        colorProp.colorValue = EditorGUI.ColorField(colorPickerRect, GUIContent.none, colorProp.colorValue, true, true, false);

        return false;
    }

    private Vector2 GetNextStoryCardPosition()
    {
        const float cardWidth = 260f;
        const float cardHeight = 200f;
        const float spacing = 40f;
        const float margin = 32f;

        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - (margin * 2f)) / (cardWidth + spacing)));
        var start = new Vector2(margin, margin);

        var cards = _asset?.storyCards ?? new List<DesignStoryCard>();
        for (int i = 0; i < cards.Count + 8; i++)
        {
            int col = i % columns;
            int row = i / columns;
            var candidate = start + new Vector2(col * (cardWidth + spacing), row * (cardHeight + spacing));
            bool overlapsExisting = cards.Any(c => Vector2.Distance(c.position, candidate) < 0.5f);
            if (!overlapsExisting)
                return candidate;
        }

        return start + new Vector2((cards.Count + 1) * (cardWidth + spacing) * 0.5f, cardHeight + spacing);
    }

    private void DrawStoryTab()
    {
        EnsureGraphView();
        _storyGraphView.style.display = DisplayStyle.Flex;
        _storyGraphView.SetNotebook(_asset, MarkAssetDirty);
        _storyGraphView.BringToFront();

        EditorGUILayout.BeginVertical(Styles.SectionBox);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Historia principal", Styles.SectionTitle);
        EditorGUILayout.HelpBox("Añade tarjetas, cámbiales el color y conéctalas para aclarar el game design.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Nueva tarjeta", GUILayout.Width(120f)))
        {
            EnsureGraphView();
            _storyGraphView.SetNotebook(_asset, MarkAssetDirty);
            _storyGraphView.CreateCard(GetNextStoryCardPosition());
        }
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

    private void AppendList(StringBuilder sb, string header, IEnumerable<(string title, string body, string tags)> entries, string extraLabel = "Tags")
    {
        sb.AppendLine(header);
        sb.AppendLine(new string('-', header.Length));
        foreach (var (title, body, tags) in entries)
        {
            sb.AppendLine(title);
            if (!string.IsNullOrEmpty(tags))
                sb.AppendLine($"{extraLabel}: {tags}");
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
        public static readonly GUIStyle NoteCard;
        public static readonly GUIStyle NoteLabel;
        public static readonly GUIStyle NoteTitleField;
        public static readonly GUIStyle NoteBody;
        public static readonly GUIStyle MiniIconButton;
        public static readonly GUIStyle GhostLabelCentered;
        public static readonly GUIStyle RichTextArea;
        public static readonly GUIStyle RichPreview;
        public static readonly GUIStyle TabButton;
        public static readonly Color ElementBackground;
        public static readonly Color ElementBackgroundActive;
        public static readonly Color NoteShadow;
        public static readonly Color PinColor;
        public static readonly Color PinShadow;
        public static readonly Color DefaultNoteColor;

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

            NoteCard = new GUIStyle(Card)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(12, 12, 12, 12)
            };

            NoteLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = new Color(0.18f, 0.18f, 0.18f) }
            };

            NoteTitleField = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            NoteBody = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = true
            };

            MiniIconButton = new GUIStyle("MiniToolbarButton")
            {
                padding = new RectOffset(2, 2, 2, 2),
                fontSize = 10
            };

            GhostLabelCentered = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontStyle = FontStyle.Italic
            };

            RichTextArea = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = true
            };

            RichPreview = new GUIStyle(RichTextArea)
            {
                normal =
                {
                    background = MakeTex(EditorGUIUtility.isProSkin ? new Color(0.11f, 0.13f, 0.16f) : new Color(0.95f, 0.97f, 1f)),
                    textColor = EditorStyles.label.normal.textColor
                },
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(4, 4, 6, 6)
            };

            TabButton = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 24,
                fontSize = 11,
                margin = new RectOffset(2, 2, 2, 2)
            };

            ElementBackground = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.22f, 0.28f)
                : new Color(0.85f, 0.9f, 0.98f);

            ElementBackgroundActive = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.34f, 0.48f)
                : new Color(0.75f, 0.84f, 0.95f);

            NoteShadow = new Color(0f, 0f, 0f, 0.08f);
            PinColor = new Color(0.8f, 0.2f, 0.2f);
            PinShadow = new Color(0f, 0f, 0f, 0.2f);
            DefaultNoteColor = new Color(1f, 0.95f, 0.65f);
        }

        public static GUIStyle GetRichTextArea(int fontSize)
        {
            var style = new GUIStyle(RichTextArea)
            {
                fontSize = fontSize
            };
            return style;
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
    private bool _isRebuilding;
    private readonly GridBackground _grid;

    public DesignStoryGraphView()
    {
        _grid = new GridBackground();
        Insert(0, _grid);
        _grid.StretchToParentSize();

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
        if (_notebook == notebook && _onDirty == onDirty)
            return;

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

        if (_nodes.TryGetValue(card.guid, out var view))
        {
            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }
    }

    private void Rebuild()
    {
        _isRebuilding = true;
        try
        {
            DeleteElements(graphElements.ToList());
            _nodes.Clear();
        }
        finally
        {
            _isRebuilding = false;
        }

        if (_notebook == null) return;

        if (_grid.parent == null)
        {
            Insert(0, _grid);
            _grid.StretchToParentSize();
        }

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
        if (_isRebuilding)
            return changes;

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
    private Vector2 _lastSize;

    public StoryCardNodeView(DesignStoryCard card, Action onDirty)
    {
        Card = card;
        _onDirty = onDirty;
        title = string.IsNullOrEmpty(card.title) ? "Tarjeta" : card.title;
        capabilities |= Capabilities.Resizable;
        var initialSize = card.size;
        if (initialSize == Vector2.zero)
            initialSize = new Vector2(320f, 320f);
        style.width = initialSize.x;
        style.height = initialSize.y;
        _lastSize = initialSize;

        Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
        Input.portName = "Entradas";
        inputContainer.Add(Input);

        Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
        Output.portName = "Salidas";
        outputContainer.Add(Output);

        mainContainer.style.flexDirection = FlexDirection.Column;
        mainContainer.style.paddingTop = 8f;
        mainContainer.style.paddingBottom = 10f;
        mainContainer.style.paddingLeft = 8f;
        mainContainer.style.paddingRight = 8f;
        mainContainer.style.backgroundColor = new StyleColor(new Color(0.18f, 0.22f, 0.28f));

        var titleField = new TextField("Título") { value = card.title };
        titleField.style.marginBottom = 6f;
        titleField.labelElement.style.minWidth = 50f;
        titleField.labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
        var titleInput = titleField.Q(TextField.textInputUssName);
        if (titleInput != null)
            titleInput.style.flexGrow = 1f;
        titleField.RegisterValueChangedCallback(evt =>
        {
            card.title = evt.newValue;
            title = string.IsNullOrEmpty(evt.newValue) ? "Tarjeta" : evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(titleField);

        var noteField = new TextField("Detalle") { value = card.note, multiline = true };
        noteField.style.minHeight = 300f;
        noteField.style.height = 0f;
        noteField.style.flexGrow = 1f;
        noteField.style.flexShrink = 1f;
        noteField.style.flexBasis = 300f;
        noteField.style.marginTop = 2f;
        noteField.style.marginBottom = 8f;
        noteField.labelElement.style.minWidth = 50f;
        noteField.labelElement.style.unityTextAlign = TextAnchor.UpperLeft;
        var noteInput = noteField.Q(TextField.textInputUssName);
        if (noteInput != null)
        {
            noteInput.style.flexGrow = 1f;
            noteInput.style.minHeight = 280f;
            noteInput.style.whiteSpace = WhiteSpace.Normal;
        }
        noteField.RegisterValueChangedCallback(evt =>
        {
            card.note = evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(noteField);

        var colorField = new ColorField("Color") { value = card.color };
        colorField.style.marginTop = 8f;
        colorField.style.marginBottom = 4f;
        colorField.labelElement.style.minWidth = 50f;
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
        SetPosition(new Rect(card.position, initialSize));

        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
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

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        var newSize = evt.newRect.size;
        if (Mathf.Approximately(newSize.x, _lastSize.x) && Mathf.Approximately(newSize.y, _lastSize.y))
            return;

        Card.size = newSize;
        _lastSize = newSize;
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
