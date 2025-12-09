using SenderoNarrativeCore.Runtime.Graph;
using SenderoNarrativeCore.Runtime.Context;
using SenderoNarrativeCore.Editor.Utilities;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace SenderoNarrativeCore.Editor.Graph
{
    /// <summary>
    /// Visual representation of a <see cref="NarrativeNode"/> inside the graph editor.
    /// </summary>
    public class NodeView : UnityEditor.Experimental.GraphView.Node
    {
        public NarrativeNode Node { get; private set; }

        public Port Input { get; private set; }
        public Port Output { get; private set; }

        public System.Action<NodeView> OnNodeSelected;

        public NodeView(NarrativeNode node)
        {
            Node = node;
            title = node.name;
            viewDataKey = node.NodeId;

            style.left = Random.Range(100, 300);
            style.top = Random.Range(100, 300);

            CreateInputPorts();
            CreateOutputPorts();
            SetupLabels();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);
            evt.menu.AppendAction("Play From Here", _ => TriggerPlayFromHere());
        }

        private void TriggerPlayFromHere()
        {
            var graph = Node != null ? AssetDatabase.LoadAssetAtPath<Runtime.Graph.NarrativeGraph>(AssetDatabase.GetAssetPath(Node)) : null;
            var context = Selection.activeObject as StoryContextAsset;
            if (context == null)
            {
                context = AssetDatabase.LoadAssetAtPath<StoryContextAsset>(AssetDatabase.GetAssetPath(graph));
            }

            PlayFromHereEditorUtility.PlayFromNode(context, Node.NodeId);
        }

        private void SetupLabels()
        {
            var idLabel = new Label(Node.NodeId)
            {
                style = { unityFontStyleAndWeight = FontStyle.Italic }
            };
            mainContainer.Add(idLabel);
        }

        private void CreateInputPorts()
        {
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            if (Input != null)
            {
                Input.portName = "In";
                inputContainer.Add(Input);
            }
        }

        private void CreateOutputPorts()
        {
            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            if (Output != null)
            {
                Output.portName = "Out";
                outputContainer.Add(Output);
            }
        }

        public override void OnSelected()
        {
            base.OnSelected();
            OnNodeSelected?.Invoke(this);
        }
    }
}
