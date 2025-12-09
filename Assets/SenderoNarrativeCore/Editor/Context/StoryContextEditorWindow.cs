using System.Collections.Generic;
using SenderoNarrativeCore.Editor.Graph;
using SenderoNarrativeCore.Runtime.Context;
using UnityEditor;
using UnityEngine;

namespace SenderoNarrativeCore.Editor.Context
{
    /// <summary>
    /// Editor window for configuring <see cref="StoryContextAsset"/> data.
    /// </summary>
    public class StoryContextEditorWindow : EditorWindow
    {
        private SerializedObject serializedContext;
        private StoryContextAsset selectedContext;

        [MenuItem("Window/Sendero Narrative/Story Context")]
        public static void Open()
        {
            var window = GetWindow<StoryContextEditorWindow>();
            window.titleContent = new GUIContent("Story Context");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            selectedContext = Selection.activeObject as StoryContextAsset;
            if (selectedContext != null)
            {
                serializedContext = new SerializedObject(selectedContext);
            }
            else
            {
                serializedContext = null;
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (selectedContext == null || serializedContext == null)
            {
                EditorGUILayout.HelpBox("Select a StoryContextAsset to edit it.", MessageType.Info);
                return;
            }

            serializedContext.Update();
            DrawField("graph");
            DrawField("localizationRootKey");
            DrawField("audioProfile");
            DrawField("sceneNamesToLoad", true);
            DrawField("designNotes");

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Graph"))
                {
                    NarrativeGraphWindow.Open();
                    Selection.activeObject = selectedContext.Graph;
                }

                if (GUILayout.Button("Validate"))
                {
                    ValidateContext();
                }
            }

            serializedContext.ApplyModifiedProperties();
        }

        private void DrawField(string propertyName, bool allowChildren = false)
        {
            var property = serializedContext.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, allowChildren);
            }
        }

        private void ValidateContext()
        {
            var issues = new List<string>();
            if (selectedContext.Graph == null)
            {
                issues.Add("StoryContext has no NarrativeGraph assigned.");
            }
            else if (!selectedContext.Graph.ValidateGraph(out var graphIssues))
            {
                issues.AddRange(graphIssues);
            }

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Story Context", "Validation succeeded.", "Ok");
            }
            else
            {
                EditorUtility.DisplayDialog("Story Context", string.Join("\n", issues), "Ok");
            }
        }
    }
}
