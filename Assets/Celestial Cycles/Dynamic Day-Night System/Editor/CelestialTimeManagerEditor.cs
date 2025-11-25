using UnityEditor;
using UnityEngine;

namespace CelestialCyclesSystem
{
    [CustomEditor(typeof(CelestialTimeManager))]
    public class CelestialTimeManagerEditor : Editor
    {
        private SerializedProperty timeOfDayControllerProperty;
        private SerializedProperty overrideCloudControl;

        private readonly string[] hiddenCloudProps = new string[]
        {
    "CloudAmount", "BlendCloud", "CloudSpeed", "CloudIntensity",
    "CloudColor", "AmbientCloudProperty", "AmbientCloudSpeed",
    "CloudLayerIntensity01", "CloudLayerIntensity02",
    "CloudScale_01", "CloudScale_02", "DarkSky"
        };

        private void OnEnable()
        {
            timeOfDayControllerProperty = serializedObject.FindProperty("timeOfDayController");
            overrideCloudControl = serializedObject.FindProperty("overrideCloudControl");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            CelestialTimeManager manager = (CelestialTimeManager)target;

            if (Lightmapping.giWorkflowMode == Lightmapping.GIWorkflowMode.Iterative)
            {
                EditorGUILayout.HelpBox("⚠️ Auto Generate Lighting is ON. Please disable it to avoid performance issues.", MessageType.Warning);
                if (GUILayout.Button("Turn Off Auto Generate"))
                {
                    Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
                }
            }

            // 🌤️ Summary Header
            GUIStyle summaryStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                normal = { textColor = Color.cyan },
                alignment = TextAnchor.MiddleCenter
            };

            float shift = Shader.GetGlobalFloat("_Shift");
            string phase = "Unknown";
            float time = manager.currentTimeOfDay;
            float sunriseStart = manager.sunriseTime - (manager.sunriseDuration / 2f);
            float sunriseEnd = manager.sunriseTime + (manager.sunriseDuration / 2f);
            float sunsetStart = manager.sunsetTime - (manager.sunsetDuration / 2f);
            float sunsetEnd = manager.sunsetTime + (manager.sunsetDuration / 2f);

            if (time >= sunriseStart && time < sunriseEnd)
                phase = "Dawn";
            else if (time >= sunriseEnd && time < sunsetStart)
                phase = "Noon";
            else if (time >= sunsetStart && time < sunsetEnd)
                phase = "Dusk";
            else
                phase = "Night";

            string shiftLabel;
            if (Mathf.Approximately(shift, 0f))
                shiftLabel = "Day";
            else if (Mathf.Approximately(shift, 1f))
                shiftLabel = "Night";
            else
                shiftLabel = $"_Shift: {shift:0.0}";

            GUILayout.Space(10);
            GUILayout.Label(
                $"☀ Time: {manager.currentTimeOfDay:0}   " +
                $"{shiftLabel}   " +
                $"Phase: {phase}",
                summaryStyle
            );
            GUILayout.Space(10);

            if (GUILayout.Button("Update Realtime GI"))
            {
                DynamicGI.UpdateEnvironment();
            }

            // ✏️ Draw all properties except cloud ones
            DrawPropertiesExcluding(serializedObject, hiddenCloudProps);

            // 🌥️ Cloud override toggle
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(overrideCloudControl, new GUIContent("Override Cloud Control"));

            if (overrideCloudControl.boolValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("☁️ Shared Cloud Shader Properties", EditorStyles.boldLabel);
                DrawCloudProperty("BlendCloud", "Blend Ambient and Volume Cloud");
                DrawCloudProperty("CloudColor", "Cloud Color");
                DrawCloudProperty("DarkSky", "Dark Sky");
                EditorGUILayout.LabelField("☁️ Ambient Cloud Properties", EditorStyles.boldLabel);
                DrawCloudProperty("AmbientCloudProperty", "Ambient Cloud Property");
                DrawCloudProperty("AmbientCloudSpeed", "Ambient Cloud Speed");
                EditorGUILayout.LabelField("☁️ Volume Cloud Properties", EditorStyles.boldLabel);
                DrawCloudProperty("CloudAmount", "Cloud Amount");
                DrawCloudProperty("CloudIntensity", "Cloud Intensity");
                DrawCloudProperty("CloudLayerIntensity01", "Cloud Layer 1 Intensity");
                DrawCloudProperty("CloudLayerIntensity02", "Cloud Layer 2 Intensity");
                DrawCloudProperty("CloudScale_01", "Cloud Scale 1");
                DrawCloudProperty("CloudScale_02", "Cloud Scale 2");
                DrawCloudProperty("CloudSpeed", "Cloud Speed");
            }
            else
            {
                EditorGUILayout.HelpBox("Clouds are managed by WeatherManager.\nEnable 'Override Cloud Control' to use cloud settings from TimeManager.", MessageType.Info);
            }

            // 🌅 Draw TimeOfDay controller if assigned
            if (timeOfDayControllerProperty.objectReferenceValue != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Time of Day Controller Settings", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                Editor controllerEditor = CreateEditor(timeOfDayControllerProperty.objectReferenceValue);
                controllerEditor.OnInspectorGUI();
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCloudProperty(string name, string label = null)
        {
            SerializedProperty prop = serializedObject.FindProperty(name);
            if (prop != null)
            {
                if (string.IsNullOrEmpty(label))
                    EditorGUILayout.PropertyField(prop);
                else
                    EditorGUILayout.PropertyField(prop, new GUIContent(label));
            }
        }
    }
}