using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Graph
{
    /// <summary>
    /// Entry point node. Simply forwards execution to its first output.
    /// </summary>
    [CreateAssetMenu(menuName = "Sendero Narrative/Nodes/Start Node", fileName = "StartNode")]
    public class StartNode : NarrativeNode
    {
        public override string Execute(NarrativeGraph graph, Runner.NarrativeRunner runner)
        {
            return Outputs.Count > 0 ? Outputs[0] : null;
        }
    }
}
