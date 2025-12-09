using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Graph
{
    /// <summary>
    /// ScriptableObject container for a narrative graph. Holds node assets and references to the start node.
    /// </summary>
    [CreateAssetMenu(menuName = "Sendero Narrative/Narrative Graph", fileName = "NarrativeGraph")]
    public class NarrativeGraph : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Identifier of the node that should be used as the graph entry point.")]
        private string startNodeId;

        [SerializeField]
        [Tooltip("All nodes contained in this graph. Nodes are typically stored as sub-assets of the graph for portability.")]
        private List<NarrativeNode> nodes = new List<NarrativeNode>();

        /// <summary>
        /// Gets the identifier for the entry node.
        /// </summary>
        public string StartNodeId => startNodeId;

        /// <summary>
        /// Gets the read-only collection of nodes in this graph.
        /// </summary>
        public IReadOnlyList<NarrativeNode> Nodes => nodes;

        /// <summary>
        /// Finds a node by identifier.
        /// </summary>
        /// <param name="nodeId">Identifier to search for.</param>
        /// <returns>Matching node or null.</returns>
        public NarrativeNode GetNode(string nodeId)
        {
            return nodes.FirstOrDefault(n => n != null && n.NodeId == nodeId);
        }

        /// <summary>
        /// Assigns the start node using a node reference.
        /// </summary>
        /// <param name="node">Node to use as start. It must belong to the graph.</param>
        public void SetStartNode(NarrativeNode node)
        {
            if (node != null && nodes.Contains(node))
            {
                startNodeId = node.NodeId;
                MarkDirty();
            }
        }

        /// <summary>
        /// Adds a node to the graph if it is not already present.
        /// </summary>
        /// <param name="node">Node to add.</param>
        public void AddNode(NarrativeNode node)
        {
            if (node == null || nodes.Contains(node))
            {
                return;
            }

            nodes.Add(node);
            MarkDirty();
        }

        /// <summary>
        /// Removes a node and all edges pointing to it.
        /// </summary>
        /// <param name="node">Node to remove.</param>
        public void RemoveNode(NarrativeNode node)
        {
            if (node == null)
            {
                return;
            }

            nodes.Remove(node);
            foreach (var n in nodes)
            {
                if (n is { })
                {
                    n.RemoveOutput(node.NodeId);
                }
            }

            if (startNodeId == node.NodeId)
            {
                startNodeId = null;
            }

            MarkDirty();
        }

        /// <summary>
        /// Validates that node references are consistent.
        /// </summary>
        /// <param name="validationErrors">List that will be populated with any validation errors.</param>
        /// <returns>True when no validation issues are found.</returns>
        public bool ValidateGraph(out List<string> validationErrors)
        {
            validationErrors = new List<string>();

            if (string.IsNullOrEmpty(startNodeId))
            {
                validationErrors.Add("Graph is missing a start node.");
            }

            var knownIds = new HashSet<string>(nodes.Where(n => n != null).Select(n => n.NodeId));
            foreach (var node in nodes.Where(n => n != null))
            {
                foreach (var output in node.Outputs)
                {
                    if (!knownIds.Contains(output))
                    {
                        validationErrors.Add($"Node '{node.name}' references missing output '{output}'.");
                    }
                }
            }

            return validationErrors.Count == 0;
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
