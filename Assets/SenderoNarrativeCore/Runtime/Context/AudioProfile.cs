using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Context
{
    /// <summary>
    /// Placeholder asset for associating music and ambience settings with a story context.
    /// Projects can replace or extend this asset with richer data as needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Sendero Narrative/Audio Profile", fileName = "AudioProfile")]
    public class AudioProfile : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Identifier for background music that should play during this context.")]
        private string musicId;

        /// <summary>
        /// Gets the configured music identifier.
        /// </summary>
        public string MusicId => musicId;
    }
}
