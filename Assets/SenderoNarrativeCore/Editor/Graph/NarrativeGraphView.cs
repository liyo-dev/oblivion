using System;
using System.Linq;
using SenderoNarrativeCore.Runtime.Graph;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SenderoNarrativeCore.Editor.Graph
{
    /// <summary>
    /// UI Toolkit GraphView that visualizes and edits <see cref="NarrativeGraph"/> assets.
    /// </summary>
    public class NarrativeGraphView : UnityEditor.Experimental.GraphView.GraphView
    {
        private NarrativeGraph graph;

        public System.Action<NodeView> OnNodeSelected;

        public NarrativeGraphView()
        {
            Insert(0, new GridBackground());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            graphViewChanged = OnGraphViewChanged;
            SetupContextualMenu();
        }

        private void SetupContextualMenu()
        {
            this.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Create Start Node", _ => CreateNode(typeof(StartNode)));
                evt.menu.AppendAction("Create Dialogue Node", _ => CreateNode(typeof(DialogueNode)));
                evt.menu.AppendAction("Create Custom Event Node", _ => CreateNode(typeof(CustomEventNode)));
            }));
        }

        public void PopulateView(NarrativeGraph narrativeGraph)
        {
            graph = narrativeGraph;

            DeleteElements(graphElements.ToList());
            if (graph == null)
            {
                return;
            }

            foreach (var node in graph.Nodes.Where(n => n != null))
            {
                CreateNodeView(node);
            }

            foreach (var node in graph.Nodes.Where(n => n != null))
            {
                foreach (var output in node.Outputs)
                {
                    var targetNode = graph.GetNode(output);
                    if (targetNode == null)
                    {
                        continue;
                    }

                    var fromView = FindNodeView(node);
                    var toView = FindNodeView(targetNode);
                    if (fromView != null && toView != null)
                    {
                        var edge = fromView.Output.ConnectTo(toView.Input);
                        AddElement(edge);
                    }
                }
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList().Where(p => p != startPort && p.direction != startPort.direction).ToList();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is NodeView nodeView)
                    {
                        Undo.RecordObject(graph, "Remove narrative node");
                        graph.RemoveNode(nodeView.Node);
                        EditorUtility.SetDirty(graph);
                    }

                    if (element is Edge edge)
                    {
                        var outputView = edge.output.node as NodeView;
                        var inputView = edge.input.node as NodeView;
                        if (outputView != null && inputView != null)
                        {
                            Undo.RecordObject(outputView.Node, "Remove link");
                            outputView.Node.RemoveOutput(inputView.Node.NodeId);
                            EditorUtility.SetDirty(outputView.Node);
                        }
                    }
                }
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var outputView = edge.output.node as NodeView;
                    var inputView = edge.input.node as NodeView;
                    if (outputView != null && inputView != null)
                    {
                        Undo.RecordObject(outputView.Node, "Add link");
                        outputView.Node.AddOutput(inputView.Node.NodeId);
                        EditorUtility.SetDirty(outputView.Node);
                    }
                }
            }

            return change;
        }

        private void CreateNodeView(NarrativeNode node)
        {
            var view = new NodeView(node)
            {
                OnNodeSelected = OnNodeSelected
            };

            AddElement(view);
        }

        private NarrativeNode CreateNode(Type type)
        {
            var node = ScriptableObject.CreateInstance(type) as NarrativeNode;
            node.name = type.Name;

            Undo.RecordObject(graph, "Add narrative node");
            graph.AddNode(node);
            AssetDatabase.AddObjectToAsset(node, graph);
            AssetDatabase.SaveAssets();

            CreateNodeView(node);
            return node;
        }

        private NodeView FindNodeView(NarrativeNode node)
        {
            return GetNodeByGuid(node.NodeId) as NodeView;
        }
    }
}
