using System;
using SenderoNarrativeCore.Runtime.Graph;

namespace SenderoNarrativeCore.Runtime.Runner
{
    /// <summary>
    /// Broadcasts lifecycle events for narrative execution. Projects can subscribe for debugging or analytics.
    /// </summary>
    public static class NarrativeSignals
    {
        /// <summary>
        /// Fired when a node is entered.
        /// </summary>
        public static event Action<NarrativeNode> NodeEntered;

        /// <summary>
        /// Fired after a node completes execution.
        /// </summary>
        public static event Action<NarrativeNode> NodeExited;

        internal static void RaiseNodeEntered(NarrativeNode node)
        {
            NodeEntered?.Invoke(node);
        }

        internal static void RaiseNodeExited(NarrativeNode node)
        {
            NodeExited?.Invoke(node);
        }
    }
}
