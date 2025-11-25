using UnityEngine;
using UnityEditor;
namespace CelestialCyclesSystem
{
    [CustomEditor(typeof(CelestialCycleTrigger))]

    public class CelestialCycleTriggerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector options
            DrawDefaultInspector();

            CelestialCycleTrigger script = (CelestialCycleTrigger)target;

            // Create a tag dropdown for triggeringTag
            script.playerTag = EditorGUILayout.TagField("Player Tag", script.playerTag);

            // Apply changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(script);
            }
        }
    }
}