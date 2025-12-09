using SenderoNarrativeCore.Runtime.Context;
using UnityEditor;
using UnityEngine;

namespace SenderoNarrativeCore.Editor.Utilities
{
    /// <summary>
    /// Utility for staging a play session from a specific narrative node.
    /// </summary>
    public static class PlayFromHereEditorUtility
    {
        private const string ContextKey = "SenderoNarrativeCore_PlayFromHere_ContextGuid";
        private const string NodeKey = "SenderoNarrativeCore_PlayFromHere_NodeId";

        /// <summary>
        /// Stores the target context and node and enters play mode.
        /// </summary>
        /// <param name="context">Story context asset containing the graph.</param>
        /// <param name="nodeId">Identifier to start from.</param>
        public static void PlayFromNode(StoryContextAsset context, string nodeId)
        {
            if (context == null)
            {
                EditorUtility.DisplayDialog("Play From Here", "Assign a StoryContextAsset before playing.", "Ok");
                return;
            }

            var path = AssetDatabase.GetAssetPath(context);
            var guid = AssetDatabase.AssetPathToGUID(path);

            EditorPrefs.SetString(ContextKey, guid);
            EditorPrefs.SetString(NodeKey, nodeId);

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }
        }

        internal static bool TryConsume(out StoryContextAsset context, out string nodeId)
        {
            context = null;
            nodeId = null;

            var guid = EditorPrefs.GetString(ContextKey, string.Empty);
            nodeId = EditorPrefs.GetString(NodeKey, string.Empty);

            if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            context = AssetDatabase.LoadAssetAtPath<StoryContextAsset>(path);

            EditorPrefs.DeleteKey(ContextKey);
            EditorPrefs.DeleteKey(NodeKey);
            return context != null;
        }
    }
}
