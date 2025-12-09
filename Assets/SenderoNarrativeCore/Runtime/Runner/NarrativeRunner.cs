using System.Collections;
using System.Collections.Generic;
using SenderoNarrativeCore.Runtime.Context;
using SenderoNarrativeCore.Runtime.Graph;
using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Runner
{
    /// <summary>
    /// Component responsible for executing a <see cref="NarrativeGraph"/>. Attach this to a scene object to run stories.
    /// </summary>
    public class NarrativeRunner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Story context asset that defines the graph and associated resources.")]
        private StoryContextAsset storyContext;

        [SerializeField]
        [Tooltip("Limit of steps executed in a single run to avoid accidental infinite loops.")]
        private int executionLimit = 256;

        /// <summary>
        /// Gets the current story context.
        /// </summary>
        public StoryContextAsset StoryContext => storyContext;

        /// <summary>
        /// Runs the graph from its configured start node.
        /// </summary>
        public void RunFromStart()
        {
            var startId = storyContext?.Graph?.StartNodeId;
            if (string.IsNullOrEmpty(startId))
            {
                Debug.LogError("Cannot run narrative: no start node configured.");
                return;
            }

            RunFromNode(startId);
        }

        /// <summary>
        /// Runs the graph beginning at a specific node identifier.
        /// </summary>
        /// <param name="nodeId">Identifier to start from.</param>
        public void RunFromNode(string nodeId)
        {
            if (storyContext?.Graph == null)
            {
                Debug.LogError("NarrativeRunner has no StoryContext assigned.");
                return;
            }

            StartCoroutine(RunCoroutine(nodeId));
        }

        private IEnumerator RunCoroutine(string nodeId)
        {
            var visited = 0;
            var graph = storyContext.Graph;
            var currentNode = graph.GetNode(nodeId);

            while (currentNode != null && visited < executionLimit)
            {
                NarrativeSignals.RaiseNodeEntered(currentNode);
                var nextId = currentNode.Execute(graph, this);
                NarrativeSignals.RaiseNodeExited(currentNode);

                visited++;
                if (string.IsNullOrEmpty(nextId))
                {
                    yield break;
                }

                var nextNode = graph.GetNode(nextId);
                if (nextNode == null)
                {
                    Debug.LogError($"Narrative graph missing node '{nextId}'. Execution stopped.");
                    yield break;
                }

                currentNode = nextNode;
                yield return null;
            }

            if (visited >= executionLimit)
            {
                Debug.LogError("Narrative execution stopped because the execution limit was reached. Check for loops.");
            }
        }

        /// <summary>
        /// Assigns a new story context at runtime.
        /// </summary>
        /// <param name="context">Context to assign.</param>
        public void SetContext(StoryContextAsset context)
        {
            storyContext = context;
        }
    }
}
