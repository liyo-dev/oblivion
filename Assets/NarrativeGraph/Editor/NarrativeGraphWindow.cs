using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    public class NarrativeGraphWindow : EditorWindow
    {
        private global::NarrativeGraph _graph;
        private NarrativeGraphView _view;
        private readonly Dictionary<string, NodeView> _nodeViews = new();
        private bool _suppressGraphCallbacks;

        private const string AllChaptersLabel = "Todos";
        private string _activeChapter = AllChaptersLabel;
        private ToolbarMenu _chapterMenu;
        private Label _chapterCountLabel;

        [MenuItem("El Sendero/Narrativa/Abrir Editor")]
        public static void OpenWindow()
        {
            var w = GetWindow<NarrativeGraphWindow>();
            w.titleContent = new GUIContent("Narrative Graph");
            w.Show();
        }

        private void OnEnable()
        {
            _view = new NarrativeGraphView();
            _view.name = "NarrativeGraphView";
            _view.StretchToParentSize();
            _view.OnEdgeLinked = OnEdgeLinked;
            _view.OnEdgeUnlinked = OnEdgeUnlinked;
            _view.OnNodeDeleted = OnNodeDeleted;
            rootVisualElement.Add(_view);

            var toolbar = new Toolbar();
            toolbar.AddToClassList("narrative-toolbar");

            var objField = new ObjectField("Graph") { objectType = typeof(global::NarrativeGraph) };
            objField.RegisterValueChangedCallback(evt => LoadGraph(evt.newValue as global::NarrativeGraph));
            toolbar.Add(objField);

            // Separador visual
            var sep1 = new VisualElement();
            sep1.AddToClassList("narrative-toolbar__separator");
            toolbar.Add(sep1);

            // Menú de capítulos
            _chapterMenu = new ToolbarMenu { text = "Cap: Todos" };
            _chapterMenu.AddToClassList("narrative-toolbar__chapter-menu");
            toolbar.Add(_chapterMenu);

            _chapterCountLabel = new Label();
            _chapterCountLabel.AddToClassList("narrative-toolbar__count");
            toolbar.Add(_chapterCountLabel);

            var sep2 = new VisualElement();
            sep2.AddToClassList("narrative-toolbar__separator");
            toolbar.Add(sep2);

            var createMenu = new ToolbarMenu { text = "Añadir Nodo" };
            var nodeTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => typeof(NarrativeNode).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .Where(t => t.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false).Length == 0)
                .OrderBy(t => t.Name).ToArray();

            foreach (var t in nodeTypes)
                createMenu.menu.AppendAction(t.Name, _ => CreateNode(t));
            toolbar.Add(createMenu);

            var saveBtn = new Button(Save) { text = "Guardar" };
            saveBtn.AddToClassList("narrative-toolbar__save-btn");
            toolbar.Add(saveBtn);

            rootVisualElement.Add(toolbar);

#if UNITY_EDITOR
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/NarrativeGraph/Editor/NarrativeGraphStyles.uss");
            if (sheet != null)
            {
                _view.styleSheets.Add(sheet);
                rootVisualElement.styleSheets.Add(sheet);
            }
#endif

            _view.AddManipulator(new ContextualMenuManipulator(e =>
            {
                Vector2 world = e.mousePosition;
                Vector2 local = _view.contentViewContainer.WorldToLocal(world);
                foreach (var t in nodeTypes)
                    e.menu.AppendAction($"Create/{t.Name}", _ => CreateNode(t, local));
            }));
        }

        private void CreateNode(Type t, Vector2? pos = null)
        {
            if (_graph == null) return;
            var node = (NarrativeNode)Activator.CreateInstance(t);
            node.position = pos ?? Vector2.zero;

            // Asignar capítulo activo al nuevo nodo
            if (_activeChapter != AllChaptersLabel)
                node.chapter = _activeChapter;

            Undo.RecordObject(_graph, "Add Narrative Node");
            _graph.nodes.Add(node);
            if (string.IsNullOrEmpty(_graph.startNodeGuid)) _graph.startNodeGuid = node.guid;
            EditorUtility.SetDirty(_graph);

            DrawNode(node);
        }

        private void LoadGraph(NarrativeGraph g)
        {
            _graph = g;
            _suppressGraphCallbacks = true;
            _view.DeleteElements(_view.graphElements.ToList());
            _nodeViews.Clear();
            if (_graph == null)
            {
                _suppressGraphCallbacks = false;
                RebuildChapterMenu();
                return;
            }

            // Clean up null nodes first
            _graph.nodes.RemoveAll(n => n == null);

            if (string.IsNullOrEmpty(_graph.startNodeGuid) && _graph.nodes.Count > 0)
                _graph.startNodeGuid = _graph.nodes[0].guid;

            foreach (var model in _graph.nodes)
            {
                if (model == null) continue;
                DrawNode(model);
            }

            foreach (var model in _graph.nodes)
            {
                if (model == null || model.outputs == null) continue;
                foreach (var outGuid in model.outputs.Where(s => !string.IsNullOrEmpty(s)))
                    if (_nodeViews.TryGetValue(model.guid, out var from) && _nodeViews.TryGetValue(outGuid, out var to))
                    {
                        var e = from.Output.ConnectTo(to.Input);
                        _view.AddElement(e);
                    }
            }

            _view.FrameAll();
            _suppressGraphCallbacks = false;
            RebuildChapterMenu();
            ApplyChapterFilter();
        }

        private void DrawNode(NarrativeNode model)
        {
            var view = new NodeView(model);

            var so = new SerializedObject(_graph);
            var idx = _graph.nodes.IndexOf(model);
            var prop = so.FindProperty("nodes").GetArrayElementAtIndex(idx);

            // Campo "Etiqueta" (displayTitle)
            var titleProp = prop.FindPropertyRelative("displayTitle");
            var titleField = new TextField("Etiqueta");
            titleField.BindProperty(titleProp);
            titleField.RegisterValueChangedCallback(_ =>
            {
                view.UpdateSubtitle();
                EditorUtility.SetDirty(_graph);
            });
            view.extensionContainer.Add(titleField);

            // Campo "Capítulo"
            var chapterProp = prop.FindPropertyRelative("chapter");
            if (chapterProp != null)
            {
                var chapterField = new TextField("Capítulo");
                chapterField.AddToClassList("narrative-node__chapter-field");
                chapterField.BindProperty(chapterProp);
                chapterField.RegisterValueChangedCallback(_ =>
                {
                    view.UpdateChapterBadge();
                    EditorUtility.SetDirty(_graph);
                    RebuildChapterMenu();
                });
                view.extensionContainer.Add(chapterField);
            }

            // Resto de propiedades del nodo (oculta guid/position/outputs/chapter)
            foreach (var child in EnumerateDirectChildren(prop))
            {
                var p = child.propertyPath;
                if (p.EndsWith(".displayTitle")) continue;
                if (p.EndsWith(".chapter"))      continue;
                if (p.EndsWith(".guid"))         continue;
                if (p.EndsWith(".position"))     continue;
                if (p.EndsWith(".outputs"))      continue;

                // Special case: abilityKeysToUnlock / abilitiesToUnlock (legacy) and spellsToUnlock -> render ReorderableList
                if (p.EndsWith(".abilityKeysToUnlock") || p.EndsWith(".abilitiesToUnlock") || p.EndsWith(".spellsToUnlock"))
                {
#if UNITY_EDITOR
                    try
                    {
                        var listProp = so.FindProperty(child.propertyPath);
                        if (listProp != null)
                        {
                            var listLabel = listProp.displayName;
                            var reorder = new UnityEditorInternal.ReorderableList(so, listProp, true, true, true, true);
                            reorder.drawHeaderCallback = (rect) => { EditorGUI.LabelField(rect, listLabel); };
                            reorder.drawElementCallback = (rect, index, isActive, isFocused) =>
                            {
                                // Usar discard para evitar warnings de parámetros no usados
                                _ = isActive; _ = isFocused;
                                var el = listProp.GetArrayElementAtIndex(index);
                                rect.y += 2;
                                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), el, GUIContent.none);
                            };
                            reorder.elementHeight = EditorGUIUtility.singleLineHeight + 6;

                            var container = new IMGUIContainer(() =>
                            {
                                reorder.DoLayoutList();
                                // apply changes
                                so.ApplyModifiedProperties();
                            });

                            view.extensionContainer.Add(container);
                            continue;
                        }
                    }
                    catch (Exception) { /* si algo falla, caer al PropertyField por defecto */ }
#endif
                }

                var pf = new PropertyField(child);
                pf.Bind(so);
                view.extensionContainer.Add(pf);
            }

            // Solo el StartNode puede ser marcado como inicio
            if (model is StartNode)
            {
                var btn = new Button(() =>
                {
                    _graph.startNodeGuid = model.guid;
                    EditorUtility.SetDirty(_graph);
                })
                { text = "Set as Start" };
                view.titleButtonContainer.Add(btn);
            }

            _view.AddElement(view);
            _nodeViews[model.guid] = view;

            view.RefreshPorts();
            view.RefreshExpandedState();
        }

        private IEnumerable<SerializedProperty> EnumerateDirectChildren(SerializedProperty parent)
        {
            var copy = parent.Copy();
            var end = copy.GetEndProperty();
            bool enterChildren = true;

            while (copy.NextVisible(enterChildren) && !SerializedProperty.EqualContents(copy, end))
            {
                enterChildren = false;
                if (copy.depth == parent.depth + 1)
                    yield return copy.Copy();
            }
        }

        // ─── Capítulos ───────────────────────────────────────────────────

        private List<string> CollectChapters()
        {
            var chapters = new List<string>();
            if (_graph == null) return chapters;

            foreach (var node in _graph.nodes)
            {
                if (node == null) continue;
                var ch = node.chapter;
                if (!string.IsNullOrWhiteSpace(ch) && !chapters.Contains(ch))
                    chapters.Add(ch);
            }
            chapters.Sort(StringComparer.OrdinalIgnoreCase);
            return chapters;
        }

        private void RebuildChapterMenu()
        {
            if (_chapterMenu == null) return;

            _chapterMenu.menu.ClearItems();

            _chapterMenu.menu.AppendAction(AllChaptersLabel, _ => SetChapterFilter(AllChaptersLabel),
                a => _activeChapter == AllChaptersLabel
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);

            var chapters = CollectChapters();
            if (chapters.Count > 0)
                _chapterMenu.menu.AppendSeparator();

            foreach (var ch in chapters)
            {
                var captured = ch;
                _chapterMenu.menu.AppendAction(captured, _ => SetChapterFilter(captured),
                    a => _activeChapter == captured
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }

            // Opción para nodos sin capítulo asignado
            int noChapterCount = 0;
            if (_graph != null)
                noChapterCount = _graph.nodes.Count(n => n != null && string.IsNullOrWhiteSpace(n.chapter));

            if (noChapterCount > 0)
            {
                _chapterMenu.menu.AppendSeparator();
                _chapterMenu.menu.AppendAction($"Sin capítulo ({noChapterCount})", _ => SetChapterFilter("__none__"),
                    a => _activeChapter == "__none__"
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal);
            }

            UpdateChapterLabel();
        }

        private void SetChapterFilter(string chapter)
        {
            _activeChapter = chapter;
            _chapterMenu.text = _activeChapter == AllChaptersLabel
                ? "Cap: Todos"
                : _activeChapter == "__none__"
                    ? "Cap: Sin capítulo"
                    : $"Cap: {_activeChapter}";
            ApplyChapterFilter();
            UpdateChapterLabel();
        }

        private void UpdateChapterLabel()
        {
            if (_chapterCountLabel == null || _graph == null) return;

            int total = _graph.nodes.Count(n => n != null);
            if (_activeChapter == AllChaptersLabel)
            {
                _chapterCountLabel.text = $"{total} nodos";
            }
            else
            {
                int visible = _graph.nodes.Count(n => n != null && NodeMatchesChapter(n));
                _chapterCountLabel.text = $"{visible}/{total} nodos";
            }
        }

        private bool NodeMatchesChapter(NarrativeNode node)
        {
            if (_activeChapter == AllChaptersLabel) return true;
            if (_activeChapter == "__none__")
                return string.IsNullOrWhiteSpace(node.chapter);
            return string.Equals(node.chapter, _activeChapter, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyChapterFilter()
        {
            bool showAll = _activeChapter == AllChaptersLabel;

            foreach (var kvp in _nodeViews)
            {
                var nv = kvp.Value;
                if (nv?.Model == null) continue;

                bool match = showAll || NodeMatchesChapter(nv.Model);
                nv.visible = true;
                nv.SetEnabled(true);

                if (!match)
                {
                    nv.AddToClassList("narrative-node--dimmed");
                    nv.RemoveFromClassList("narrative-node--active-chapter");
                }
                else
                {
                    nv.RemoveFromClassList("narrative-node--dimmed");
                    if (!showAll)
                        nv.AddToClassList("narrative-node--active-chapter");
                    else
                        nv.RemoveFromClassList("narrative-node--active-chapter");
                }
            }

            // Atenuar aristas que no pertenecen al filtro activo
            _view.edges.ForEach(edge =>
            {
                if (edge?.output?.node is NodeView from && edge?.input?.node is NodeView to)
                {
                    bool fromMatch = showAll || (from.Model != null && NodeMatchesChapter(from.Model));
                    bool toMatch = showAll || (to.Model != null && NodeMatchesChapter(to.Model));

                    if (fromMatch && toMatch)
                    {
                        edge.RemoveFromClassList("narrative-edge--dimmed");
                    }
                    else
                    {
                        edge.AddToClassList("narrative-edge--dimmed");
                    }
                }
            });
        }

        // ─── Callbacks de aristas y nodos ────────────────────────────────

        private void OnEdgeLinked(Edge e)
        {
            if (_suppressGraphCallbacks) return;
            if (_graph == null) return;
            if (e.output?.node is NodeView from && e.input?.node is NodeView to)
            {
                Undo.RecordObject(_graph, "Link Nodes");
                if (from.Model.outputs == null) from.Model.outputs = new List<string>();
                if (!from.Model.outputs.Contains(to.Model.guid))
                    from.Model.outputs.Add(to.Model.guid);
                EditorUtility.SetDirty(_graph);
            }
        }

        private void OnEdgeUnlinked(Edge e)
        {
            if (_suppressGraphCallbacks) return;
            if (_graph == null) return;
            if (e.output?.node is NodeView from && e.input?.node is NodeView to)
            {
                Undo.RecordObject(_graph, "Unlink Nodes");
                from.Model.outputs?.Remove(to.Model.guid);
                EditorUtility.SetDirty(_graph);
            }
        }

        private void Save()
        {
            if (_graph == null) return;
            foreach (var kv in _nodeViews)
                kv.Value.Model.position = kv.Value.GetPosition().position;

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
        }
        
        private void OnNodeDeleted(NodeView nv)
        {
            if (_suppressGraphCallbacks) return;
            if (_graph == null || nv == null || nv.Model == null) return;
            Undo.RecordObject(_graph, "Delete Narrative Node");

            // Quita referencias desde otros nodos
            foreach (var n in _graph.nodes)
            {
                if (n == null) continue;
                n.outputs?.RemoveAll(g => g == nv.Model.guid);
            }

            // Borra el propio nodo
            _graph.nodes.Remove(nv.Model);

            // Si era el start, lo limpiamos
            if (_graph.startNodeGuid == nv.Model.guid)
                _graph.startNodeGuid = null;

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            RebuildChapterMenu();
        }
    }
}
