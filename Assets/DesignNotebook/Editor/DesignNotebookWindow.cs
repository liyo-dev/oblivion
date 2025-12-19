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
    private Vector2 _boardScroll;
    private int _tabIndex;
    private DesignStoryGraphView _storyGraphView;
    private int _fontSize = 18;
    private Vector2 _synopsisScroll;
    private bool _isPreviewMode;
    private Vector2 _quickNotesScroll;
    private int _draggingNoteIndex = -1;
    private Vector2 _dragOffset;
    private bool _blackboardMode;
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

        if (_asset.documents == null)
        {
            _asset.documents = new List<DesignDocumentNote>();
            changed = true;
        }

        if (_asset.blackboardStrokes == null)
        {
            _asset.blackboardStrokes = new List<DesignBlackboardStroke>();
            changed = true;
        }

        if (_asset.blackboardBrushSize <= 0f)
        {
            _asset.blackboardBrushSize = 5f;
            changed = true;
        }

        if (_asset.blackboardBrushColor.a <= 0f)
        {
            _asset.blackboardBrushColor = Color.white;
            changed = true;
        }

        if (_asset.blackboardBackground.a <= 0f)
        {
            _asset.blackboardBackground = new Color(0.07f, 0.08f, 0.1f);
            changed = true;
        }

        if (_asset.boardBackground.a <= 0f)
        {
            _asset.boardBackground = new Color(0.07f, 0.08f, 0.1f);
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
            EditorGUILayout.LabelField("Design Notebook Studio", Styles.HeaderTitle);
            EditorGUILayout.LabelField("Diseña, presenta y exporta con un panel curado para pitching.", Styles.HeaderSubtitle);

            EditorGUILayout.BeginHorizontal();
            var newAsset = (DesignNotebook)EditorGUILayout.ObjectField("Documento activo", _asset, typeof(DesignNotebook), false);
            if (newAsset != _asset)
                LoadAsset(newAsset);

            if (GUILayout.Button("Nuevo", GUILayout.Width(90f)))
                CreateNewAsset();
            EditorGUILayout.EndHorizontal();

            DrawHeaderStats();
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
            "Board",
            "Exportar"
        };

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            _tabIndex = GUILayout.Toolbar(_tabIndex, tabs, Styles.TabButton, GUILayout.Height(24));
            GUILayout.FlexibleSpace();
        }

        _tabIndex = Mathf.Clamp(_tabIndex, 0, tabs.Length - 1);

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

    private void DrawHeaderStats()
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.BeginHorizontal();
        float availableWidth = Mathf.Max(position.width - 60f, 360f);
        float targetWidth = Mathf.Clamp((availableWidth / 3f) - 8f, 140f, 260f);

        DrawStatPill("Tarjetas", _asset?.storyCards?.Count ?? 0, "Historias conectadas", targetWidth);
        DrawStatPill("Notas rápidas", _asset?.quickNotes?.Count ?? 0, "Ideas sueltas en el board", targetWidth);
        DrawStatPill("Docs", _asset?.documents?.Count ?? 0, "Sinopsis y referencias", targetWidth);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStatPill(string label, int value, string helper, float targetWidth)
    {
        using (new EditorGUILayout.VerticalScope(
                   Styles.StatPill,
                   GUILayout.MinWidth(targetWidth),
                   GUILayout.MaxWidth(targetWidth),
                   GUILayout.ExpandWidth(true)))
        {
            EditorGUILayout.LabelField(label, Styles.StatLabel);
            EditorGUILayout.LabelField(value.ToString(), Styles.StatValue);
            EditorGUILayout.LabelField(helper, Styles.StatHelper);
        }
    }

    private void DrawCurrentTab()
    {
        switch (_tabIndex)
        {
            case 0:
                DrawSummary();
                break;
            case 2:
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

        _fontSize = EditorGUILayout.IntSlider("Tamaño de fuente", _fontSize, 10, 48);

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
        float viewWidth = Mathf.Max(position.width - 56f, cardWidth + 60f);
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
        DrawQuickNotesBackdrop(boardRect);
        var innerBoard = InsetRect(boardRect, 12f);
        viewWidth = innerBoard.width;

        if (quickNotesProp.arraySize == 0)
        {
            EditorGUI.LabelField(innerBoard, "Añade tu primera nota para empezar a organizar.", Styles.GhostLabelCentered);
            return;
        }

        EnsureQuickNotePositions(quickNotesProp, viewWidth, cardWidth, cardHeight, cardSpacing);

        var contentSize = CalculateQuickNotesContentSize(quickNotesProp, cardWidth, cardHeight, cardSpacing, innerBoard);
        _quickNotesScroll = GUI.BeginScrollView(innerBoard, _quickNotesScroll, new Rect(0, 0, contentSize.x, contentSize.y));

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

    private void DrawQuickNotesBackdrop(Rect boardRect)
    {
        EditorGUI.DrawRect(boardRect, Styles.CorkShadow);
        var frame = InsetRect(boardRect, 3f);
        EditorGUI.DrawRect(frame, Styles.CorkFrame);
        var cork = InsetRect(frame, 6f);
        EditorGUI.DrawRect(cork, Styles.CorkBackground);

        const float step = 30f;
        Handles.BeginGUI();
        var previousColor = Handles.color;
        Handles.color = new Color(1f, 1f, 1f, 0.055f);
        for (float x = cork.xMin + step; x < cork.xMax; x += step)
            Handles.DrawLine(new Vector3(x, cork.yMin, 0f), new Vector3(x, cork.yMax, 0f));
        for (float y = cork.yMin + step; y < cork.yMax; y += step)
            Handles.DrawLine(new Vector3(cork.xMin, y, 0f), new Vector3(cork.xMax, y, 0f));
        Handles.color = previousColor;
        Handles.EndGUI();
    }

    private Rect InsetRect(Rect rect, float amount)
    {
        rect.xMin += amount;
        rect.yMin += amount;
        rect.xMax -= amount;
        rect.yMax -= amount;
        return rect;
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
        float usedHeight = line - contentRect.y;
        float reservedForTags = EditorGUIUtility.singleLineHeight * 3.2f;
        float noteHeight = Mathf.Max(EditorGUIUtility.singleLineHeight * 2f, contentRect.height - usedHeight - reservedForTags);
        var noteRect = new Rect(contentRect.x, line, contentRect.width, noteHeight);
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

        using (var scroll = new EditorGUILayout.ScrollViewScope(_boardScroll))
        {
            _boardScroll = scroll.scrollPosition;

            EditorGUILayout.BeginVertical(Styles.SectionBox);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Board unificado", Styles.SectionTitle);
            EditorGUILayout.HelpBox("Un solo tablero donde combinar tarjetas, notas rápidas y documentos enlazables.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Nueva tarjeta", GUILayout.Width(120f)))
                {
                    EnsureGraphView();
                    _storyGraphView.SetNotebook(_asset, MarkAssetDirty);
                    _storyGraphView.CreateCard(GetNextStoryCardPosition());
                }

                if (GUILayout.Button("Nota rápida", GUILayout.Width(110f)))
                {
                    EnsureGraphView();
                    _storyGraphView.SetNotebook(_asset, MarkAssetDirty);
                    _storyGraphView.CreateQuickNote(GetNextStoryCardPosition());
                }

                if (GUILayout.Button("Documento", GUILayout.Width(110f)))
                {
                    EnsureGraphView();
                    _storyGraphView.SetNotebook(_asset, MarkAssetDirty);
                    _storyGraphView.CreateDocument(GetNextStoryCardPosition());
                }

                if (GUILayout.Button("Enmarcar todo", GUILayout.Width(120f)))
                    _storyGraphView.FrameAll();
                if (GUILayout.Button("Centrar selección", GUILayout.Width(130f)))
                    _storyGraphView.FrameSelection();
            }

            DrawBoardSettingsRow();

            float availableHeight = Mathf.Max(position.height - 220f, 420f);
            float graphHeight = Mathf.Max(availableHeight * 0.8f, 420f);
            var rect = GUILayoutUtility.GetRect(position.width - 32f, graphHeight, GUILayout.ExpandWidth(true), GUILayout.Height(graphHeight));
            LayoutGraphView(rect);

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawBoardSettingsRow()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Color del board", GUILayout.Width(110f));
            var newColor = EditorGUILayout.ColorField(_asset.boardBackground);
            if (newColor != _asset.boardBackground)
            {
                Undo.RecordObject(_asset, "Cambiar color del board");
                _asset.boardBackground = newColor;
                _storyGraphView.SetNotebook(_asset, MarkAssetDirty);
                MarkAssetDirty();
            }

            var brushColorProp = _serialized.FindProperty("blackboardBrushColor");
            var brushSizeProp = _serialized.FindProperty("blackboardBrushSize");
            var backgroundProp = _serialized.FindProperty("blackboardBackground");

            GUILayout.Space(12f);
            GUILayout.Label("Pizarra", GUILayout.Width(60f));
            var newMode = GUILayout.Toggle(_blackboardMode, "Modo pizarra", Styles.TabButton, GUILayout.Width(120f));
            if (newMode != _blackboardMode)
            {
                _blackboardMode = newMode;
                _storyGraphView.SetBlackboardMode(_blackboardMode);
            }

            GUILayout.Label("Tiza", GUILayout.Width(40f));
            var newBrushColor = EditorGUILayout.ColorField(brushColorProp.colorValue, GUILayout.Width(80f));
            if (newBrushColor != brushColorProp.colorValue)
            {
                brushColorProp.colorValue = newBrushColor;
                _storyGraphView.UpdateBrushSettings(brushColorProp.colorValue, brushSizeProp.floatValue);
            }

            GUILayout.Label("Grosor", GUILayout.Width(55f));
            brushSizeProp.floatValue = Mathf.Clamp(EditorGUILayout.Slider(brushSizeProp.floatValue, 1f, 24f, GUILayout.Width(180f)), 1f, 24f);
            _storyGraphView.UpdateBrushSettings(brushColorProp.colorValue, brushSizeProp.floatValue);

            var newBackground = EditorGUILayout.ColorField(backgroundProp.colorValue, GUILayout.Width(80f));
            if (newBackground != backgroundProp.colorValue)
            {
                backgroundProp.colorValue = newBackground;
                _storyGraphView.UpdateBlackboardBackground(newBackground);
            }

            if (GUILayout.Button("Limpiar pizarra", GUILayout.Width(140f)))
                _storyGraphView.ClearBlackboard();
        }
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
        if (_serialized != null && !_serialized.hasModifiedProperties)
            _serialized.UpdateIfRequiredOrScript();
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
        sb.AppendLine();
        AppendList(sb, "DOCUMENTOS", _asset.documents.Select(d => ($"• {d.title}", d.body, "")), "");

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
        var usableWidth = pageWidth - (margin * 2f);
        var maxCharsPerLine = Mathf.Max(32, Mathf.FloorToInt(usableWidth / 6f));

        var sanitized = content.Replace("\r\n", "\n");
        var rawLines = sanitized.Split('\n');

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
        foreach (var line in WrapLinesForPdf(rawLines, maxCharsPerLine))
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

    private IEnumerable<string> WrapLinesForPdf(IEnumerable<string> lines, int maxCharactersPerLine)
    {
        foreach (var line in lines)
        {
            foreach (var wrapped in WrapSingleLine(line, maxCharactersPerLine))
                yield return wrapped;
        }
    }

    private IEnumerable<string> WrapSingleLine(string line, int maxCharactersPerLine)
    {
        if (string.IsNullOrEmpty(line))
        {
            yield return string.Empty;
            yield break;
        }

        int start = 0;
        while (start < line.Length)
        {
            int length = Mathf.Min(maxCharactersPerLine, line.Length - start);
            int endExclusive = start + length;

            if (endExclusive < line.Length)
            {
                int lastBreak = FindLineBreakIndex(line, start, endExclusive);
                if (lastBreak > start)
                    endExclusive = lastBreak + 1;
            }

            int segmentLength = Mathf.Max(1, endExclusive - start);
            var segment = line.Substring(start, segmentLength).TrimEnd();
            yield return segment;
            start = endExclusive;

            while (start < line.Length && (line[start] == ' ' || line[start] == '\t'))
                start++;
        }
    }

    private int FindLineBreakIndex(string line, int start, int endExclusive)
    {
        for (int i = endExclusive - 1; i >= start; i--)
        {
            char c = line[i];
            if (char.IsWhiteSpace(c) || c == '-' || c == ',')
                return i;
        }
        return -1;
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
        public static readonly GUIStyle HeaderSubtitle;
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
        public static readonly GUIStyle StatPill;
        public static readonly GUIStyle StatLabel;
        public static readonly GUIStyle StatValue;
        public static readonly GUIStyle StatHelper;
        public static readonly Color ElementBackground;
        public static readonly Color ElementBackgroundActive;
        public static readonly Color NoteShadow;
        public static readonly Color PinColor;
        public static readonly Color PinShadow;
        public static readonly Color DefaultNoteColor;
        public static readonly Color CorkShadow;
        public static readonly Color CorkFrame;
        public static readonly Color CorkBackground;
        public static readonly Color AccentColor;
        public static readonly Color AccentSoft;

        static Styles()
        {
            AccentColor = EditorGUIUtility.isProSkin ? new Color(0.34f, 0.28f, 0.22f) : new Color(0.42f, 0.36f, 0.3f);
            AccentSoft = EditorGUIUtility.isProSkin ? new Color(0.44f, 0.36f, 0.28f) : new Color(0.52f, 0.46f, 0.38f);

            HeaderBox = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(12, 12, 12, 14),
                margin = new RectOffset(6, 6, 4, 4),
                normal =
                {
                    background = MakeTex(EditorGUIUtility.isProSkin
                        ? new Color(0.12f, 0.12f, 0.13f)
                        : new Color(0.2f, 0.2f, 0.22f))
                }
            };

            HeaderTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.94f, 0.9f, 0.86f) : new Color(0.18f, 0.16f, 0.14f) }
            };

            HeaderSubtitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.82f, 0.78f, 0.72f) : new Color(0.28f, 0.24f, 0.2f) },
                padding = new RectOffset(2, 2, 0, 4),
                margin = new RectOffset(4, 4, 0, 6)
            };

            SectionBox = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(14, 14, 12, 14),
                normal = { background = MakeTex(EditorGUIUtility.isProSkin ? new Color(0.12f, 0.13f, 0.14f) : new Color(0.9f, 0.9f, 0.92f)) }
            };

            SectionTitle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                margin = new RectOffset(4, 4, 2, 6),
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.88f, 0.82f) : new Color(0.26f, 0.22f, 0.18f) }
            };

            ListHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.82f, 0.78f, 0.72f) : new Color(0.22f, 0.18f, 0.14f) }
            };

            Card = new GUIStyle("Box")
            {
                padding = new RectOffset(10, 10, 8, 10),
                margin = new RectOffset(0, 0, 10, 10),
                normal = { background = MakeTex(EditorGUIUtility.isProSkin ? new Color(0.14f, 0.15f, 0.16f) : new Color(0.86f, 0.86f, 0.88f)) }
            };

            NoteCard = new GUIStyle(Card)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(12, 12, 12, 12)
            };

            NoteLabel = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.96f, 0.94f, 0.9f) : new Color(0.2f, 0.18f, 0.16f) }
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
                    background = MakeTex(EditorGUIUtility.isProSkin ? new Color(0.11f, 0.11f, 0.12f) : new Color(0.94f, 0.94f, 0.95f)),
                    textColor = EditorStyles.label.normal.textColor
                },
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(4, 4, 6, 6)
            };

            TabButton = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 26,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(2, 2, 2, 2),
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.94f, 0.9f, 0.86f) : new Color(0.18f, 0.16f, 0.14f) }
            };

            StatPill = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(12, 12, 8, 10),
                margin = new RectOffset(6, 6, 2, 2),
                normal =
                {
                    background = MakeTex(EditorGUIUtility.isProSkin
                        ? new Color(0.14f, 0.14f, 0.16f)
                        : new Color(0.25f, 0.25f, 0.27f))
                }
            };

            StatLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.86f, 0.84f, 0.8f) : new Color(0.28f, 0.24f, 0.2f) }
            };

            StatValue = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.94f, 0.92f, 0.9f) : new Color(0.12f, 0.1f, 0.08f) }
            };

            StatHelper = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.78f, 0.74f, 0.68f, 0.9f) : new Color(0.32f, 0.28f, 0.22f) }
            };

            ElementBackground = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.18f, 0.16f)
                : new Color(0.84f, 0.82f, 0.8f);

            ElementBackgroundActive = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 0.24f, 0.2f)
                : new Color(0.76f, 0.72f, 0.68f);

            NoteShadow = new Color(0f, 0f, 0f, 0.08f);
            PinColor = new Color(0.8f, 0.2f, 0.2f);
            PinShadow = new Color(0f, 0f, 0f, 0.2f);
            DefaultNoteColor = new Color(1f, 0.95f, 0.65f);
            CorkShadow = new Color(0f, 0f, 0f, 0.16f);
            CorkFrame = new Color(0.34f, 0.26f, 0.18f);
            CorkBackground = new Color(0.88f, 0.78f, 0.64f);
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

        private static Texture2D MakeVerticalGradientTex(int height, Color top, Color bottom)
        {
            var tex = new Texture2D(1, Mathf.Max(2, height));
            for (int y = 0; y < tex.height; y++)
            {
                float t = Mathf.InverseLerp(0, tex.height - 1, y);
                tex.SetPixel(0, y, Color.Lerp(top, bottom, t));
            }
            tex.Apply();
            return tex;
        }

    }
}

internal class DesignStoryGraphView : GraphView
{
    private DesignNotebook _notebook;
    private readonly Dictionary<string, StoryCardNodeView> _cardNodes = new();
    private readonly Dictionary<string, DocumentNodeView> _documentNodes = new();
    private readonly List<QuickNoteNodeView> _quickNoteNodes = new();
    private Action _onDirty;
    private bool _isRebuilding;
    private readonly GridBackground _grid;
    private readonly IMGUIContainer _blackboardOverlay;
    private bool _blackboardMode;
    private DesignBlackboardStroke _activeStroke;
    private readonly List<Vector3> _strokePoints = new();
    private Color _brushColor = Color.white;
    private float _brushSize = 5f;
    private Color _blackboardBackground = new Color(0.07f, 0.08f, 0.1f);

    public DesignStoryGraphView()
    {
        _grid = new GridBackground();
        Insert(0, _grid);
        _grid.StretchToParentSize();
        style.backgroundColor = new StyleColor(new Color(0.07f, 0.09f, 0.12f));
        _grid.style.backgroundColor = new StyleColor(new Color(0.05f, 0.07f, 0.1f));

        _blackboardOverlay = new IMGUIContainer(DrawBlackboardOverlay)
        {
            style =
            {
                position = Position.Absolute,
                left = 0,
                right = 0,
                top = 0,
                bottom = 0
            },
            pickingMode = PickingMode.Ignore
        };
        Add(_blackboardOverlay);

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        style.flexGrow = 1f;

        this.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            Vector2 local = contentViewContainer.WorldToLocal(evt.mousePosition);
            evt.menu.AppendAction("Nueva tarjeta", _ => CreateCard(local));
            evt.menu.AppendAction("Nota rápida", _ => CreateQuickNote(local));
            evt.menu.AppendAction("Documento", _ => CreateDocument(local));
        }));

        graphViewChanged = GraphChanged;
    }

    public void SetNotebook(DesignNotebook notebook, Action onDirty)
    {
        if (_notebook == notebook && _onDirty == onDirty)
            return;

        _notebook = notebook;
        _onDirty = onDirty;
        style.backgroundColor = new StyleColor(_notebook.boardBackground);
        _grid.style.backgroundColor = new StyleColor(Color.Lerp(_notebook.boardBackground, Color.black, 0.35f));
        _brushColor = _notebook.blackboardBrushColor;
        _brushSize = _notebook.blackboardBrushSize;
        _blackboardBackground = _notebook.blackboardBackground;
        _blackboardOverlay.MarkDirtyRepaint();
        Rebuild();
    }

    public void SetBlackboardMode(bool enabled)
    {
        _blackboardMode = enabled;
        _blackboardOverlay.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
        _blackboardOverlay.MarkDirtyRepaint();
    }

    public void UpdateBrushSettings(Color color, float thickness)
    {
        _brushColor = color;
        _brushSize = thickness;
        _blackboardOverlay.MarkDirtyRepaint();
    }

    public void UpdateBlackboardBackground(Color background)
    {
        _blackboardBackground = background;
        _blackboardOverlay.MarkDirtyRepaint();
    }

    public void ClearBlackboard()
    {
        if (_notebook == null) return;
        Undo.RecordObject(_notebook, "Limpiar pizarra");
        _notebook.blackboardStrokes.Clear();
        _activeStroke = null;
        _onDirty?.Invoke();
        _blackboardOverlay.MarkDirtyRepaint();
    }

    private void DrawBlackboardOverlay()
    {
        if (_notebook == null)
            return;

        var rect = new Rect(Vector2.zero, layout.size);
        if (Event.current.type == EventType.Repaint)
        {
            Handles.BeginGUI();
            DrawBlackboardGrid(rect, _blackboardBackground);
            DrawBlackboardStrokes(rect);
            Handles.EndGUI();
        }

        HandleBlackboardInput(rect);
    }

    private void HandleBlackboardInput(Rect rect)
    {
        if (!_blackboardMode)
            return;

        int controlId = GUIUtility.GetControlID("DesignNotebookBlackboardOverlay".GetHashCode(), FocusType.Passive);
        var evt = Event.current;
        var type = evt.GetTypeForControl(controlId);

        switch (type)
        {
            case EventType.MouseDown:
                if (evt.button == 0 && rect.Contains(evt.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    BeginBlackboardStroke(evt.mousePosition, rect);
                    evt.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId && _activeStroke != null)
                {
                    AddPointToStroke(evt.mousePosition, rect);
                    evt.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId && evt.button == 0)
                {
                    AddPointToStroke(evt.mousePosition, rect);
                    EndBlackboardStroke();
                    GUIUtility.hotControl = 0;
                    evt.Use();
                }
                break;
            case EventType.Ignore:
                if (GUIUtility.hotControl == controlId && evt.rawType == EventType.MouseUp)
                {
                    EndBlackboardStroke();
                    GUIUtility.hotControl = 0;
                }
                break;
        }
    }

    private void BeginBlackboardStroke(Vector2 guiPosition, Rect rect)
    {
        if (_notebook == null) return;

        Undo.RecordObject(_notebook, "Dibujar en pizarra");
        _activeStroke = new DesignBlackboardStroke
        {
            color = _brushColor,
            thickness = _brushSize
        };
        _notebook.blackboardStrokes.Add(_activeStroke);
        AddPointToStroke(guiPosition, rect);
        _onDirty?.Invoke();
    }

    private void AddPointToStroke(Vector2 guiPosition, Rect rect)
    {
        if (_activeStroke == null)
            return;

        var local = ClampToRect(guiPosition, rect);
        var points = _activeStroke.points;
        if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], local) > 0.5f)
        {
            points.Add(local);
            _blackboardOverlay.MarkDirtyRepaint();
            _onDirty?.Invoke();
        }
    }

    private void EndBlackboardStroke()
    {
        _activeStroke = null;
        _blackboardOverlay.MarkDirtyRepaint();
        _onDirty?.Invoke();
    }

    private Vector2 ClampToRect(Vector2 guiPosition, Rect rect)
    {
        var local = guiPosition - rect.position;
        local.x = Mathf.Clamp(local.x, 0f, Mathf.Max(1f, rect.width));
        local.y = Mathf.Clamp(local.y, 0f, Mathf.Max(1f, rect.height));
        return local;
    }

    private void DrawBlackboardGrid(Rect rect, Color backgroundColor)
    {
        var previousColor = Handles.color;
        var gridColor = Color.Lerp(Color.white, backgroundColor, 0.65f);
        gridColor.a = 0.04f;
        Handles.color = gridColor;
        const float step = 36f;
        for (float x = rect.xMin + step; x < rect.xMax; x += step)
            Handles.DrawLine(new Vector3(x, rect.yMin, 0f), new Vector3(x, rect.yMax, 0f));
        for (float y = rect.yMin + step; y < rect.yMax; y += step)
            Handles.DrawLine(new Vector3(rect.xMin, y, 0f), new Vector3(rect.xMax, y, 0f));

        Handles.color = new Color(0f, 0f, 0f, 0.25f);
        Handles.DrawAAPolyLine(2f, new[]
        {
            new Vector3(rect.xMin, rect.yMin, 0f),
            new Vector3(rect.xMax, rect.yMin, 0f),
            new Vector3(rect.xMax, rect.yMax, 0f),
            new Vector3(rect.xMin, rect.yMax, 0f),
            new Vector3(rect.xMin, rect.yMin, 0f)
        });
        Handles.color = previousColor;
    }

    private void DrawBlackboardStrokes(Rect rect)
    {
        if (_notebook?.blackboardStrokes == null)
            return;

        foreach (var stroke in _notebook.blackboardStrokes)
        {
            if (stroke == null || stroke.points == null || stroke.points.Count < 2)
                continue;

            _strokePoints.Clear();
            for (int i = 0; i < stroke.points.Count; i++)
            {
                var p = stroke.points[i];
                _strokePoints.Add(new Vector3(rect.x + p.x, rect.y + p.y, 0f));
            }
            Handles.color = stroke.color;
            Handles.DrawAAPolyLine(stroke.thickness, _strokePoints.ToArray());
        }
        Handles.color = Color.white;
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

        if (_cardNodes.TryGetValue(card.guid, out var view))
        {
            ClearSelection();
            AddToSelection(view);
            FrameSelection();
        }
    }

    public void CreateQuickNote(Vector2 position)
    {
        if (_notebook == null) return;
        Undo.RecordObject(_notebook, "Agregar nota rápida");
        var note = new DesignScratch
        {
            title = "Nota rápida",
            position = position
        };
        _notebook.quickNotes.Add(note);
        _onDirty?.Invoke();
        Rebuild();
    }

    public void CreateDocument(Vector2 position)
    {
        if (_notebook == null) return;
        Undo.RecordObject(_notebook, "Agregar documento");
        var doc = new DesignDocumentNote
        {
            position = position
        };
        _notebook.documents.Add(doc);
        _onDirty?.Invoke();
        Rebuild();
    }

    private void Rebuild()
    {
        _isRebuilding = true;
        try
        {
            DeleteElements(graphElements.ToList());
            _cardNodes.Clear();
            _quickNoteNodes.Clear();
            _documentNodes.Clear();

            if (_notebook == null) return;

            style.backgroundColor = new StyleColor(_notebook.boardBackground);
            _grid.style.backgroundColor = new StyleColor(Color.Lerp(_notebook.boardBackground, Color.black, 0.35f));

            if (_grid.parent == null)
            {
                Insert(0, _grid);
                _grid.StretchToParentSize();
            }

            foreach (var card in _notebook.storyCards)
            {
                var view = new StoryCardNodeView(card, MarkDirty);
                _cardNodes[card.guid] = view;
                AddElement(view);
            }

            foreach (var note in _notebook.quickNotes)
            {
                var view = new QuickNoteNodeView(note, MarkDirty);
                _quickNoteNodes.Add(view);
                AddElement(view);
            }

            foreach (var doc in _notebook.documents)
            {
                var view = new DocumentNodeView(doc, MarkDirty);
                _documentNodes[doc.guid] = view;
                AddElement(view);
            }

            foreach (var link in _notebook.storyLinks.ToList())
            {
                if (!_cardNodes.TryGetValue(link.fromGuid, out var from) || !_cardNodes.TryGetValue(link.toGuid, out var to))
                {
                    _notebook.storyLinks.Remove(link);
                    continue;
                }
                var edge = from.Output.ConnectTo(to.Input);
                StyleEdge(edge, from.Card.color, to.Card.color);
                AddElement(edge);
            }
        }
        finally
        {
            _isRebuilding = false;
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
                StyleEdge(e, from.Card.color, to.Card.color);
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
                else if (el is QuickNoteNodeView note)
                {
                    RemoveQuickNote(note);
                }
                else if (el is DocumentNodeView doc)
                {
                    RemoveDocument(doc);
                }
            }
        }

        return changes;
    }

    internal static void StyleEdge(Edge edge, Color fromColor, Color toColor)
    {
        if (edge?.edgeControl == null) return;

        var start = Color.Lerp(fromColor, new Color(0.3f, 0.9f, 0.9f), 0.15f);
        var end = Color.Lerp(toColor, Color.white, 0.1f);
        edge.edgeControl.outputColor = start;
        edge.edgeControl.inputColor = end;
                edge.edgeControl.edgeWidth = 4;
        edge.edgeControl.capRadius = 6f;
    }

    private void RemoveNode(StoryCardNodeView node)
    {
        Undo.RecordObject(_notebook, "Eliminar tarjeta de historia");
        _notebook.storyCards.Remove(node.Card);
        _notebook.storyLinks.RemoveAll(l => l.fromGuid == node.Card.guid || l.toGuid == node.Card.guid);
        MarkDirty();
    }

    private void RemoveQuickNote(QuickNoteNodeView node)
    {
        Undo.RecordObject(_notebook, "Eliminar nota rápida");
        _notebook.quickNotes.Remove(node.Note);
        MarkDirty();
    }

    private void RemoveDocument(DocumentNodeView node)
    {
        Undo.RecordObject(_notebook, "Eliminar documento");
        _notebook.documents.Remove(node.Document);
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
    private readonly VisualElement _accentStrip;
    private readonly Color _baseBody = new Color(0.08f, 0.1f, 0.15f);
    private readonly Color _baseHeader = new Color(0.1f, 0.12f, 0.18f);
    private readonly Color[] _swatchColors =
    {
        new Color(0.54f, 0.38f, 0.3f),
        new Color(0.48f, 0.58f, 0.46f),
        new Color(0.64f, 0.52f, 0.4f),
        new Color(0.56f, 0.44f, 0.52f),
        new Color(0.66f, 0.46f, 0.34f),
        new Color(0.32f, 0.32f, 0.32f)
    };

    public StoryCardNodeView(DesignStoryCard card, Action onDirty)
    {
        Card = card;
        _onDirty = onDirty;
        title = string.IsNullOrEmpty(card.title) ? "Tarjeta" : card.title;
        capabilities |= Capabilities.Resizable;
        var initialSize = card.size;
        if (initialSize == Vector2.zero)
            initialSize = new Vector2(320f, 420f);
        if (card.titleFontSize <= 0)
            card.titleFontSize = 14;
        style.width = initialSize.x;
        style.height = initialSize.y;
        style.minHeight = 380f;
        _lastSize = initialSize;

        _accentStrip = new VisualElement();
        _accentStrip.style.width = 6f;
        _accentStrip.style.backgroundColor = new StyleColor(Card.color);
        _accentStrip.style.position = Position.Absolute;
        _accentStrip.style.left = -2f;
        _accentStrip.style.top = 0f;
        _accentStrip.style.bottom = 0f;
        _accentStrip.style.marginRight = 4f;
        titleContainer.Add(_accentStrip);
        titleContainer.style.paddingTop = 8f;
        titleContainer.style.paddingBottom = 6f;
        titleContainer.style.paddingLeft = 10f;
        titleContainer.style.paddingRight = 10f;
        titleContainer.style.marginTop = 6f;
        titleContainer.style.minHeight = 32f;
        var titleLabel = titleContainer.Q<Label>("title-label");
        if (titleLabel != null)
        {
            titleLabel.style.marginLeft = 4f;
            titleLabel.style.unityFontStyleAndWeight = card.titleFontStyle;
            titleLabel.style.fontSize = card.titleFontSize > 0 ? card.titleFontSize : 14f;
            titleLabel.style.color = new StyleColor(Color.white);
        }

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
        mainContainer.style.marginTop = 4f;
        mainContainer.style.backgroundColor = new StyleColor(new Color(0.14f, 0.17f, 0.22f));
        style.backgroundColor = new StyleColor(new Color(0.08f, 0.1f, 0.14f));
        style.borderBottomLeftRadius = 10f;
        style.borderBottomRightRadius = 10f;
        style.borderTopLeftRadius = 10f;
        style.borderTopRightRadius = 10f;

        const float LabelWidth = 70f;

        var titleField = new TextField("Título") { value = card.title };
        titleField.style.marginBottom = 6f;
        titleField.labelElement.style.minWidth = LabelWidth;
        titleField.labelElement.style.maxWidth = LabelWidth;
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
        noteField.style.minHeight = 160f;
        noteField.style.height = 0f;
        noteField.style.flexGrow = 1f;
        noteField.style.flexShrink = 1f;
        noteField.style.flexBasis = 180f;
        noteField.style.marginTop = 8f;
        noteField.style.marginBottom = 8f;
        noteField.labelElement.style.minWidth = LabelWidth;
        noteField.labelElement.style.maxWidth = LabelWidth;
        noteField.labelElement.style.unityTextAlign = TextAnchor.UpperLeft;
        var noteInput = noteField.Q(TextField.textInputUssName);
        if (noteInput != null)
        {
            noteInput.style.flexGrow = 1f;
            noteInput.style.minHeight = 140f;
            noteInput.style.whiteSpace = WhiteSpace.Normal;
        }
        noteField.RegisterValueChangedCallback(evt =>
        {
            card.note = evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(noteField);

        var settingsFoldout = new Foldout
        {
            text = "Ajustes rápidos",
            value = card.settingsExpanded,
            style =
            {
                marginTop = 6f,
                unityFontStyleAndWeight = FontStyle.Bold
            }
        };
        settingsFoldout.RegisterValueChangedCallback(evt =>
        {
            card.settingsExpanded = evt.newValue;
            _onDirty?.Invoke();
        });

        var settingsContainer = settingsFoldout.contentContainer;
        settingsContainer.style.marginTop = 4f;
        settingsContainer.style.marginLeft = 4f;
        settingsContainer.style.marginRight = 4f;

        var colorHeader = new Label("Color de la tarjeta")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 2f
            }
        };
        settingsContainer.Add(colorHeader);

        var colorRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                marginTop = 2f,
                marginBottom = 2f
            }
        };
        var colorLabel = new Label("Color")
        {
            style =
            {
                minWidth = LabelWidth,
                maxWidth = LabelWidth,
                unityTextAlign = TextAnchor.MiddleLeft
            }
        };
        colorRow.Add(colorLabel);

        var colorField = new ColorField { value = card.color };
        colorField.style.height = 16f;
        colorField.style.marginLeft = 4f;
        colorField.style.marginRight = 4f;
        colorField.style.width = 130f;
        colorField.style.flexGrow = 0f;
        colorField.labelElement.style.display = DisplayStyle.None;
        colorField.RegisterValueChangedCallback(evt =>
        {
            card.color = evt.newValue;
            UpdateColor();
            _onDirty?.Invoke();
        });
        colorRow.Add(colorField);
        settingsContainer.Add(colorRow);

        var swatches = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                marginBottom = 8f,
                marginTop = 4f,
                marginLeft = LabelWidth + 4f
            }
        };
        foreach (var c in _swatchColors)
        {
            var b = new Button { style = { width = 18f, height = 18f, marginRight = 4f, marginTop = 2f, marginBottom = 2f, paddingLeft = 0, paddingRight = 0, paddingTop = 0, paddingBottom = 0 } };
            b.style.backgroundColor = new StyleColor(c);
            b.clickable.clicked += () =>
            {
                card.color = c;
                colorField.value = c;
                UpdateColor();
                _onDirty?.Invoke();
            };
            swatches.Add(b);
        }
        settingsContainer.Add(swatches);

        var typographyRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                marginTop = 2f,
                marginBottom = 2f
            }
        };
        var fontLabel = new Label("Fuente")
        {
            style =
            {
                minWidth = LabelWidth,
                maxWidth = LabelWidth,
                unityTextAlign = TextAnchor.MiddleLeft
            }
        };
        typographyRow.Add(fontLabel);

        var fontStyleField = new EnumField(card.titleFontStyle);
        fontStyleField.style.width = 110f;
        fontStyleField.RegisterValueChangedCallback(evt =>
        {
            card.titleFontStyle = (FontStyle)evt.newValue;
            UpdateTypography(titleLabel, titleField, noteField);
            _onDirty?.Invoke();
        });
        typographyRow.Add(fontStyleField);

        var fontSizeField = new IntegerField { value = Mathf.Clamp(card.titleFontSize, 12, 28) };
        fontSizeField.style.width = 70f;
        fontSizeField.style.marginLeft = 8f;
        fontSizeField.label = "Tamaño";
        fontSizeField.labelElement.style.minWidth = 56f;
        fontSizeField.RegisterValueChangedCallback(evt =>
        {
            card.titleFontSize = Mathf.Clamp(evt.newValue, 12, 28);
            fontSizeField.SetValueWithoutNotify(card.titleFontSize);
            UpdateTypography(titleLabel, titleField, noteField);
            _onDirty?.Invoke();
        });
        typographyRow.Add(fontSizeField);
        settingsContainer.Add(typographyRow);

        mainContainer.Add(settingsFoldout);

        UpdateColor();
        UpdateTypography(titleLabel, titleField, noteField);
        RefreshExpandedState();
        RefreshPorts();
        SetPosition(new Rect(card.position, initialSize));

        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void UpdateColor()
    {
        var accent = Card.color;
        var headerColor = Color.Lerp(accent, _baseHeader, 0.35f);
        var bodyColor = Color.Lerp(accent, _baseBody, 0.55f);

        mainContainer.style.backgroundColor = new StyleColor(bodyColor);
        titleContainer.style.backgroundColor = new StyleColor(Color.Lerp(headerColor, _baseHeader, 0.35f));

        style.borderLeftWidth = 2f;
        style.borderRightWidth = 2f;
        style.borderTopWidth = 2f;
        style.borderBottomWidth = 2f;
        style.borderLeftColor = accent;
        style.borderRightColor = accent;
        style.borderTopColor = accent;
        style.borderBottomColor = accent;

        Input.portColor = accent;
        Output.portColor = accent;
        _accentStrip.style.backgroundColor = new StyleColor(accent);
        UpdateConnectedEdges();
    }

    private void UpdateTypography(Label titleLabel, TextField titleField, TextField noteField)
    {
        if (Card.titleFontSize <= 0)
            Card.titleFontSize = 14;

        if (titleLabel != null)
        {
            titleLabel.style.unityFontStyleAndWeight = Card.titleFontStyle;
            titleLabel.style.fontSize = Card.titleFontSize;
            titleLabel.style.color = new StyleColor(Color.white);
        }

        if (titleField != null)
        {
            var input = titleField.Q(TextField.textInputUssName);
            if (input != null)
            {
                input.style.fontSize = Card.titleFontSize;
                input.style.unityFontStyleAndWeight = Card.titleFontStyle;
            }
        }

        if (noteField != null)
        {
            var noteInput = noteField.Q(TextField.textInputUssName);
            if (noteInput != null)
            {
                noteInput.style.fontSize = Mathf.Clamp(Card.titleFontSize - 1, 11, 28);
            }
        }
    }

    private void UpdateConnectedEdges()
    {
        foreach (var edge in Input.connections.Concat(Output.connections))
        {
            var fromNode = edge.output?.node as StoryCardNodeView;
            var toNode = edge.input?.node as StoryCardNodeView;
            var fromColor = fromNode?.Card.color ?? Card.color;
            var toColor = toNode?.Card.color ?? Card.color;
            DesignStoryGraphView.StyleEdge(edge, fromColor, toColor);
        }
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

internal class QuickNoteNodeView : Node
{
    public DesignScratch Note { get; }
    private readonly Action _onDirty;

    public QuickNoteNodeView(DesignScratch note, Action onDirty)
    {
        Note = note;
        _onDirty = onDirty;
        title = string.IsNullOrEmpty(note.title) ? "Nota rápida" : note.title;
        capabilities |= Capabilities.Movable;
        style.width = 240f;
        style.height = 260f;
        style.minHeight = 220f;
        style.borderBottomLeftRadius = 8f;
        style.borderBottomRightRadius = 8f;
        style.borderTopLeftRadius = 8f;
        style.borderTopRightRadius = 8f;
        style.marginTop = 6f;

        UpdateColors();

        const float LabelWidth = 60f;

        var titleField = new TextField("Título") { value = note.title };
        titleField.labelElement.style.minWidth = LabelWidth;
        titleField.labelElement.style.maxWidth = LabelWidth;
        titleField.RegisterValueChangedCallback(evt =>
        {
            Note.title = evt.newValue;
            title = string.IsNullOrEmpty(evt.newValue) ? "Nota rápida" : evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(titleField);

        var bodyField = new TextField("Nota") { value = note.note, multiline = true };
        bodyField.style.minHeight = 150f;
        bodyField.style.flexBasis = 180f;
        bodyField.style.flexGrow = 1f;
        bodyField.labelElement.style.minWidth = LabelWidth;
        bodyField.labelElement.style.maxWidth = LabelWidth;
        bodyField.RegisterValueChangedCallback(evt =>
        {
            Note.note = evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(bodyField);

        var tagsField = new TextField("Tags") { value = note.tags };
        tagsField.labelElement.style.minWidth = LabelWidth;
        tagsField.labelElement.style.maxWidth = LabelWidth;
        tagsField.RegisterValueChangedCallback(evt =>
        {
            Note.tags = evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(tagsField);

        var colorField = new ColorField("Color") { value = note.color };
        colorField.labelElement.style.minWidth = LabelWidth;
        colorField.labelElement.style.maxWidth = LabelWidth;
        colorField.RegisterValueChangedCallback(evt =>
        {
            Note.color = evt.newValue;
            UpdateColors();
            _onDirty?.Invoke();
        });
        mainContainer.Add(colorField);

        SetPosition(new Rect(note.position, new Vector2(style.width.value.value, style.height.value.value)));
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        Note.position = newPos.position;
        _onDirty?.Invoke();
    }

    private void UpdateColors()
    {
        var body = Color.Lerp(Note.color, new Color(0.05f, 0.05f, 0.05f), 0.2f);
        mainContainer.style.backgroundColor = new StyleColor(body);
        titleContainer.style.backgroundColor = new StyleColor(Color.Lerp(body, Color.black, 0.35f));
        style.borderLeftColor = Note.color;
        style.borderRightColor = Note.color;
        style.borderTopColor = Note.color;
        style.borderBottomColor = Note.color;
        style.borderLeftWidth = 2f;
        style.borderRightWidth = 2f;
        style.borderTopWidth = 2f;
        style.borderBottomWidth = 2f;
    }
}

internal class DocumentNodeView : Node
{
    public DesignDocumentNote Document { get; }
    private readonly Action _onDirty;
    private Vector2 _lastSize;

    public DocumentNodeView(DesignDocumentNote document, Action onDirty)
    {
        Document = document;
        _onDirty = onDirty;
        title = string.IsNullOrEmpty(document.title) ? "Documento" : document.title;
        capabilities |= Capabilities.Resizable;
        var initialSize = document.size == Vector2.zero ? new Vector2(360f, 520f) : document.size;
        if (document.size == Vector2.zero)
            Document.size = initialSize;
        style.width = initialSize.x;
        style.height = initialSize.y;
        style.minHeight = 480f;
        _lastSize = initialSize;

        mainContainer.style.flexDirection = FlexDirection.Column;
        mainContainer.style.paddingTop = 6f;
        mainContainer.style.paddingLeft = 6f;
        mainContainer.style.paddingRight = 6f;
        mainContainer.style.paddingBottom = 6f;
        mainContainer.style.backgroundColor = new StyleColor(Color.Lerp(document.color, Color.black, 0.25f));
        titleContainer.style.backgroundColor = new StyleColor(Color.Lerp(document.color, Color.black, 0.35f));
        titleContainer.style.marginBottom = 4f;

        const float LabelWidth = 70f;

        var titleField = new TextField("Título") { value = document.title };
        titleField.labelElement.style.minWidth = LabelWidth;
        titleField.labelElement.style.maxWidth = LabelWidth;
        titleField.RegisterValueChangedCallback(evt =>
        {
            Document.title = evt.newValue;
            title = string.IsNullOrEmpty(evt.newValue) ? "Documento" : evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(titleField);

        var bodyField = new TextField("Cuerpo") { value = document.body, multiline = true };
        bodyField.style.minHeight = 340f;
        bodyField.style.flexBasis = 360f;
        bodyField.style.flexGrow = 1f;
        bodyField.labelElement.style.minWidth = LabelWidth;
        bodyField.labelElement.style.maxWidth = LabelWidth;
        bodyField.RegisterValueChangedCallback(evt =>
        {
            Document.body = evt.newValue;
            _onDirty?.Invoke();
        });
        mainContainer.Add(bodyField);

        var colorField = new ColorField("Color") { value = document.color };
        colorField.labelElement.style.minWidth = LabelWidth;
        colorField.labelElement.style.maxWidth = LabelWidth;
        colorField.RegisterValueChangedCallback(evt =>
        {
            Document.color = evt.newValue;
            mainContainer.style.backgroundColor = new StyleColor(Color.Lerp(Document.color, Color.black, 0.25f));
            titleContainer.style.backgroundColor = new StyleColor(Color.Lerp(Document.color, Color.black, 0.35f));
            _onDirty?.Invoke();
        });
        mainContainer.Add(colorField);

        SetPosition(new Rect(document.position, document.size));
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        Document.position = newPos.position;
        _onDirty?.Invoke();
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        var newSize = evt.newRect.size;
        if (Mathf.Approximately(newSize.x, _lastSize.x) && Mathf.Approximately(newSize.y, _lastSize.y))
            return;

        Document.size = newSize;
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
