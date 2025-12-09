using SenderoNarrativeCore.Editor.Graph;
using SenderoNarrativeCore.Editor.Context;
using SenderoNarrativeCore.Runtime.Context;
using SenderoNarrativeCore.Runtime.Graph;
using UnityEditor;
using UnityEngine;

namespace SenderoNarrativeCore.Editor.Utilities
{
    /// <summary>
    /// Menu shortcuts for quickly creating and editing narrative assets.
    /// </summary>
    public static class EditorMenuItems
    {
        [MenuItem("Assets/Create/Sendero Narrative/Narrative Graph")]
        public static void CreateGraph()
        {
            var graph = ScriptableObject.CreateInstance<NarrativeGraph>();
            AssetDatabase.CreateAsset(graph, "Assets/NewNarrativeGraph.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = graph;
            NarrativeGraphWindow.Open();
        }

        [MenuItem("Assets/Create/Sendero Narrative/Story Context")]
        public static void CreateStoryContext()
        {
            var context = ScriptableObject.CreateInstance<StoryContextAsset>();
            AssetDatabase.CreateAsset(context, "Assets/NewStoryContext.asset");
            AssetDatabase.SaveAssets();
            Selection.activeObject = context;
            StoryContextEditorWindow.Open();
        }
    }
}
