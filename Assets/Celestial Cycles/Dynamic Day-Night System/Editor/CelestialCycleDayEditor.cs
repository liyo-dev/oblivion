using UnityEditor;
using UnityEngine;

namespace CelestialCyclesSystem
{
    [CustomEditor(typeof(CelestialCycleDay))]
    public class CelestialCycleDayEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector options
            DrawDefaultInspector();

            GUIStyle headerStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter // Center the text
            };
            headerStyle.normal.textColor = Color.white;
            headerStyle.margin = new RectOffset(0, 0, 10, 10); // Adjust spacing as needed


            EditorGUILayout.Space();
            EditorGUILayout.Space();
            CelestialCycleDay controller = (CelestialCycleDay)target;
            EditorGUILayout.LabelField("Morning Settings", headerStyle);
            controller.morningSettings = (CelestialCyclePeriod)DrawSettingsEditor(controller.morningSettings);
            EditorGUILayout.LabelField("Noon Settings", headerStyle);
            controller.noonSettings = (CelestialCyclePeriod)DrawSettingsEditor(controller.noonSettings);
            EditorGUILayout.LabelField("Evening Settings", headerStyle);
            controller.eveningSettings = (CelestialCyclePeriod)DrawSettingsEditor(controller.eveningSettings);
            EditorGUILayout.LabelField("Night Settings", headerStyle);
            controller.nightSettings = (CelestialCyclePeriod)DrawSettingsEditor(controller.nightSettings);
        }

        private ScriptableObject DrawSettingsEditor(ScriptableObject settings)
        {
            // Style for the box
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(10, 10, 10, 10); // Adjust padding as needed
            boxStyle.margin = new RectOffset(5, 5, 5, 5); // Adjust margin as needed


            if (settings != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(boxStyle); // Start of the box section
                Editor editor = CreateEditor(settings);
                editor.OnInspectorGUI();
                EditorGUILayout.EndVertical(); // End of the box section
            }

            return settings;
        }


    }
}