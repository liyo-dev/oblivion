using System.Collections.Generic;
using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Graph
{
    /// <summary>
    /// Base class for all narrative nodes. Nodes are ScriptableObjects so they can live as sub-assets of a NarrativeGraph.
    /// Projects should derive new node types to integrate with their gameplay without modifying the core runtime.
    /// </summary>
    public abstract class NarrativeNode : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Unique identifier within the graph. Must be stable for serialization.")]
        private string nodeId = System.Guid.NewGuid().ToString();

        [SerializeField]
        [Tooltip("Node ids that this node can transition to.")]
        private List<string> outputs = new List<string>();

        /// <summary>
        /// Gets the unique identifier for this node.
        /// </summary>
        public string NodeId => nodeId;

        /// <summary>
        /// Gets the list of connected node identifiers.
        /// </summary>
        public IReadOnlyList<string> Outputs => outputs;

        /// <summary>
        /// Executes the node logic and returns the next node identifier, if any.
        /// Implementations should be deterministic and side-effect free aside from service calls.
        /// </summary>
        /// <param name="graph">Graph that owns the node.</param>
        /// <param name="runner">Runner that is executing the graph.</param>
        /// <returns>Identifier of the next node, or null to stop.</returns>
        public abstract string Execute(NarrativeGraph graph, Runner.NarrativeRunner runner);

        /// <summary>
        /// Adds a connection to another node. The graph editor uses this to wire nodes together.
        /// </summary>
        /// <param name="nextId">Identifier of the target node.</param>
        public void AddOutput(string nextId)
        {
            if (!outputs.Contains(nextId))
            {
                outputs.Add(nextId);
            }
        }

        /// <summary>
        /// Removes an output connection by id.
        /// </summary>
        /// <param name="nextId">Identifier to remove.</param>
        public void RemoveOutput(string nextId)
        {
            outputs.Remove(nextId);
        }

        /// <summary>
        /// Renames the node by updating its identifier.
        /// Ensure all references are updated in the graph when using this method.
        /// </summary>
        /// <param name="newId">New unique identifier.</param>
        public void Rename(string newId)
        {
            nodeId = newId;
        }
    }
}
