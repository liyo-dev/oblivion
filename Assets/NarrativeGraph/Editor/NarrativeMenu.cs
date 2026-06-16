using UnityEditor;
using UnityEngine;
using GraphAsset = global::NarrativeGraph;

namespace Sendero.Narrative.Editor
{
    public static class NarrativeMenu
    {
        [MenuItem("El Sendero/Narrativa/Nuevo Grafo")]
        public static void CreateGraph()
        {
            var g = ScriptableObject.CreateInstance<GraphAsset>();
            AssetDatabase.CreateAsset(g, "Assets/NarrativeGraph/NewNarrativeGraph.asset");
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(g);
        }
    }
}