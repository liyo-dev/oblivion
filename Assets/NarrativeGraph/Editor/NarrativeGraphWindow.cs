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

        [MenuItem("Tools/Narrative Graph/Open Editor")]
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

            var objField = new ObjectField("Graph") { objectType = typeof(global::NarrativeGraph) };
            objField.RegisterValueChangedCallback(evt => LoadGraph(evt.newValue as global::NarrativeGraph));
            toolbar.Add(objField);

            var createMenu = new ToolbarMenu { text = "Add Node" };
            var nodeTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => typeof(NarrativeNode).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .OrderBy(t => t.Name).ToArray();

            foreach (var t in nodeTypes)
                createMenu.menu.AppendAction(t.Name, _ => CreateNode(t));
            toolbar.Add(createMenu);

            var saveBtn = new Button(Save) { text = "Guardar" };
            toolbar.Add(saveBtn);

            rootVisualElement.Add(toolbar);

            _view.AddManipulator(new ContextualMenuManipulator((ContextualMenuPopulateEvent e) =>
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

            Undo.RecordObject(_graph, "Add Narrative Node");
            _graph.nodes.Add(node);
            if (string.IsNullOrEmpty(_graph.startNodeGuid)) _graph.startNodeGuid = node.guid;
            EditorUtility.SetDirty(_graph);

            DrawNode(node);
        }

        private void LoadGraph(global::NarrativeGraph g)
        {
            _graph = g;
            _view.DeleteElements(_view.graphElements.ToList());
            _nodeViews.Clear();
            if (_graph == null) return;

            if (string.IsNullOrEmpty(_graph.startNodeGuid) && _graph.nodes.Count > 0)
                _graph.startNodeGuid = _graph.nodes[0].guid;

            foreach (var model in _graph.nodes) DrawNode(model);

            foreach (var model in _graph.nodes)
            {
                if (model.outputs == null) continue;
                foreach (var outGuid in model.outputs.Where(s => !string.IsNullOrEmpty(s)))
                    if (_nodeViews.TryGetValue(model.guid, out var from) && _nodeViews.TryGetValue(outGuid, out var to))
                    {
                        var e = from.Output.ConnectTo(to.Input);
                        _view.AddElement(e);
                    }
            }

            _view.FrameAll();
        }

        private void DrawNode(NarrativeNode model)
        {
            var view = new NodeView(model);

            var so = new SerializedObject(_graph);
            var idx = _graph.nodes.IndexOf(model);
            var prop = so.FindProperty("nodes").GetArrayElementAtIndex(idx);

            // Campo “Etiqueta” (displayTitle)
            var titleProp = prop.FindPropertyRelative("displayTitle");
            var titleField = new TextField("Etiqueta");
            titleField.BindProperty(titleProp);
            titleField.RegisterValueChangedCallback(_ =>
            {
                view.UpdateSubtitle();
                EditorUtility.SetDirty(_graph);
            });
            view.extensionContainer.Add(titleField);

            // Resto de propiedades del nodo (oculta guid/position/outputs)
            foreach (var child in EnumerateDirectChildren(prop))
            {
                var p = child.propertyPath;
                if (p.EndsWith(".displayTitle")) continue;
                if (p.EndsWith(".guid"))         continue;
                if (p.EndsWith(".position"))     continue;
                if (p.EndsWith(".outputs"))      continue;

                var pf = new PropertyField(child);
                pf.Bind(so);
                view.extensionContainer.Add(pf);
            }

            var btn = new Button(() =>
            {
                _graph.startNodeGuid = model.guid;
                EditorUtility.SetDirty(_graph);
            })
            { text = "Set as Start" };
            view.titleButtonContainer.Add(btn);

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

        private void OnEdgeLinked(Edge e)
        {
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
            if (_graph == null || nv == null || nv.Model == null) return;
            Undo.RecordObject(_graph, "Delete Narrative Node");

            // Quita referencias desde otros nodos
            foreach (var n in _graph.nodes)
                n.outputs?.RemoveAll(g => g == nv.Model.guid);

            // Borra el propio nodo
            _graph.nodes.Remove(nv.Model);

            // Si era el start, lo limpiamos
            if (_graph.startNodeGuid == nv.Model.guid)
                _graph.startNodeGuid = null;

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
        }
    }
}
