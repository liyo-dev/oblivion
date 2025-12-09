using SenderoNarrativeCore.Runtime.Services;
using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Graph
{
    /// <summary>
    /// Generic hook for custom game events such as cinematics or quest triggers.
    /// </summary>
    [CreateAssetMenu(menuName = "Sendero Narrative/Nodes/Custom Event Node", fileName = "CustomEventNode")]
    public class CustomEventNode : NarrativeNode
    {
        [SerializeField]
        [Tooltip("Identifier passed to the project's integration systems.")]
        private string eventId;

        /// <summary>
        /// Gets the identifier of the event to trigger.
        /// </summary>
        public string EventId => eventId;

        public override string Execute(NarrativeGraph graph, Runner.NarrativeRunner runner)
        {
            var questService = NarrativeServiceRegistry.GetService<IQuestService>();
            var cinematic = NarrativeServiceRegistry.GetService<ICinematicService>();

            questService?.CompleteQuest(eventId);
            cinematic?.PlayTimeline(eventId);

            Debug.Log($"[CustomEventNode] Triggered event '{eventId}'.");
            return Outputs.Count > 0 ? Outputs[0] : null;
        }
    }
}
