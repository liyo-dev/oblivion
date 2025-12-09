using SenderoNarrativeCore.Runtime.Services;
using UnityEngine;

namespace SenderoNarrativeCore.Runtime.Graph
{
    /// <summary>
    /// Example node that retrieves localized text and optionally plays audio. Projects can extend this node or create their own.
    /// </summary>
    [CreateAssetMenu(menuName = "Sendero Narrative/Nodes/Dialogue Node", fileName = "DialogueNode")]
    public class DialogueNode : NarrativeNode
    {
        [SerializeField]
        [Tooltip("Localization key relative to the StoryContextAsset localization root key.")]
        private string textKey;

        [SerializeField]
        [Tooltip("Optional music identifier to request when this node runs.")]
        private string musicId;

        [SerializeField]
        [Tooltip("Optional sound effect identifier to request when this node runs.")]
        private string sfxId;

        /// <summary>
        /// Gets the key used to fetch localized text.
        /// </summary>
        public string TextKey => textKey;

        public override string Execute(NarrativeGraph graph, Runner.NarrativeRunner runner)
        {
            var localization = NarrativeServiceRegistry.GetService<ILocalizationService>();
            var audio = NarrativeServiceRegistry.GetService<IAudioService>();

            if (audio != null)
            {
                if (!string.IsNullOrEmpty(musicId))
                {
                    audio.PlayMusic(musicId);
                }

                if (!string.IsNullOrEmpty(sfxId))
                {
                    audio.PlaySfx(sfxId);
                }
            }

            if (localization != null)
            {
                var key = string.IsNullOrEmpty(runner?.StoryContext?.LocalizationRootKey)
                    ? textKey
                    : $"{runner.StoryContext.LocalizationRootKey}.{textKey}";
                var localizedText = localization.GetText(key);
                Debug.Log($"[DialogueNode] {key}: {localizedText}");
            }
            else
            {
                Debug.LogWarning($"No localization service registered. Dialogue node '{name}' will fallback to raw key '{textKey}'.");
            }

            return Outputs.Count > 0 ? Outputs[0] : null;
        }
    }
}
