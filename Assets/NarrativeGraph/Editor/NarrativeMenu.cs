using UnityEditor;
using UnityEngine;
using GraphAsset = global::NarrativeGraph;

namespace Sendero.Narrative.Editor
{
    public static class NarrativeMenu
    {
        [MenuItem("Tools/Narrative Graph/New Graph")]
        public static void CreateGraph()
        {
            var g = ScriptableObject.CreateInstance<GraphAsset>();
            AssetDatabase.CreateAsset(g, "Assets/NarrativeGraph/NewNarrativeGraph.asset");
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(g);
        }
    }
}