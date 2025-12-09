using SenderoNarrativeCore.Runtime.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SenderoNarrativeCore.Editor.Graph
{
    /// <summary>
    /// Editor window that hosts the <see cref="NarrativeGraphView"/>.
    /// </summary>
    public class NarrativeGraphWindow : EditorWindow
    {
        private NarrativeGraphView graphView;
        private NarrativeGraph graph;

        [MenuItem("Window/Sendero Narrative/Narrative Graph")]
        public static void Open()
        {
            var window = GetWindow<NarrativeGraphWindow>();
            window.titleContent = new GUIContent("Narrative Graph");
        }

        private void OnEnable()
        {
            ConstructGraphView();
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(graphView);
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void ConstructGraphView()
        {
            graphView = new NarrativeGraphView
            {
                name = "Narrative Graph"
            };

            graphView.StretchToParentSize();
            rootVisualElement.Add(graphView);
        }

        private void OnSelectionChanged()
        {
            var selection = Selection.activeObject as NarrativeGraph;
            if (selection != null && selection != graph)
            {
                graph = selection;
                graphView.PopulateView(graph);
            }
        }

        private void OnGUI()
        {
            if (graph == null)
            {
                EditorGUILayout.HelpBox("Select a NarrativeGraph asset to edit it.", MessageType.Info);
            }
        }
    }
}
