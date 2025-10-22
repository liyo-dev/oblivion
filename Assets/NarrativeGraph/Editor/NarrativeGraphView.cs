using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    public class NarrativeGraphView : GraphView
    {
        public System.Action<Edge> OnEdgeLinked;
        public System.Action<Edge> OnEdgeUnlinked;
        public System.Action<NodeView> OnNodeDeleted;

        public NarrativeGraphView()
        {
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var mini = new MiniMap { anchored = true };
            mini.SetPosition(new Rect(10, 30, 180, 120));
            Add(mini);

            style.flexGrow = 1f;

            graphViewChanged = GraphChanged;
        }

        GraphViewChange GraphChanged(GraphViewChange changes)
        {
            if (changes.edgesToCreate != null)
            {
                foreach (var e in changes.edgesToCreate)
                {
                    AddElement(e);
                    OnEdgeLinked?.Invoke(e);
                }
                changes.edgesToCreate = null;
            }

            if (changes.elementsToRemove != null)
            {
                foreach (var el in changes.elementsToRemove)
                {
                    if (el is Edge ed)
                        OnEdgeUnlinked?.Invoke(ed);
                    else if (el is NodeView nv)
                        OnNodeDeleted?.Invoke(nv);  
                }
            }

            return changes;
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
}
