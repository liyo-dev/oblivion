using SenderoNarrativeCore.Runtime.Graph;
using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Context
{
    /// <summary>
    /// Bundles a narrative graph with metadata such as localization and scenes required to run a scenario.
    /// </summary>
    [CreateAssetMenu(menuName = "Sendero Narrative/Story Context", fileName = "StoryContext")]
    public class StoryContextAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Narrative graph to run when this context is active.")]
        private NarrativeGraph graph;

        [SerializeField]
        [Tooltip("Root key for localization entries belonging to this context.")]
        private string localizationRootKey;

        [SerializeField]
        [Tooltip("Optional audio profile used to configure background music.")]
        private AudioProfile audioProfile;

        [SerializeField]
        [Tooltip("List of scene names required when playing this context.")]
        private string[] sceneNamesToLoad = new string[0];

        [SerializeField]
        [Tooltip("Optional design documentation or notes.")]
        private TextAsset designNotes;

        /// <summary>
        /// Gets the narrative graph associated with this context.
        /// </summary>
        public NarrativeGraph Graph => graph;

        /// <summary>
        /// Gets the localization root key used by nodes within this context.
        /// </summary>
        public string LocalizationRootKey => localizationRootKey;

        /// <summary>
        /// Gets the audio profile asset.
        /// </summary>
        public AudioProfile AudioProfile => audioProfile;

        /// <summary>
        /// Gets the scenes that should be loaded when playing from this context.
        /// </summary>
        public string[] SceneNamesToLoad => sceneNamesToLoad;

        /// <summary>
        /// Gets the optional text asset containing design notes.
        /// </summary>
        public TextAsset DesignNotes => designNotes;
    }
}
