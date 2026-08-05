using Game.NPC.Modules;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Editor para NPCInteractiveNarrativeConfig. Sustituye el Inspector por defecto de Unity
/// (que renderiza TODOS los campos de TODOS los tipos de condición/acción a la vez, y duplica el
/// último elemento al pulsar "+") por una vista que:
///  - Solo muestra los campos relevantes según conditionType / actionType seleccionados.
///  - Añade elementos nuevos ya reseteados a sus valores por defecto (ver SerializedArrayUtils).
/// </summary>
[CustomEditor(typeof(NPCInteractiveNarrativeConfig))]
public class NPCInteractiveNarrativeConfigEditor : Editor
{
    private SerializedProperty _conditionalNarratives;
    private SerializedProperty _persistState;
    private SerializedProperty _enableDetailedLogs;

    private void OnEnable()
    {
        _conditionalNarratives = serializedObject.FindProperty("conditionalNarratives");
        _persistState = serializedObject.FindProperty("persistState");
        _enableDetailedLogs = serializedObject.FindProperty("enableDetailedLogs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Lista de narrativas con condiciones. Se evalúan en orden de prioridad (mayor primero). " +
            "Para una narrativa simple sin condiciones, añade una con condición 'None'.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Narrativas Condicionales", EditorStyles.boldLabel);

        for (int i = 0; i < _conditionalNarratives.arraySize; i++)
        {
            var narrativeProp = _conditionalNarratives.GetArrayElementAtIndex(i);
            var descriptionProp = narrativeProp.FindPropertyRelative("description");
            string label = string.IsNullOrEmpty(descriptionProp.stringValue)
                ? $"Narrativa #{i} (sin nombre)"
                : $"#{i} — {descriptionProp.stringValue}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            narrativeProp.isExpanded = EditorGUILayout.Foldout(narrativeProp.isExpanded, label, true, EditorStyles.foldoutHeader);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                _conditionalNarratives.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (narrativeProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                NarrativeInteractiveConfigGUI.DrawConditionalNarrative(narrativeProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        if (GUILayout.Button("+ Añadir Narrativa Condicional", GUILayout.Height(24)))
        {
            var newNarrative = SerializedArrayUtils.AddElementReset(_conditionalNarratives, typeof(ConditionalNarrative));
            newNarrative.isExpanded = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Persistencia Narrativa", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_persistState);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_enableDetailedLogs);

        serializedObject.ApplyModifiedProperties();
    }
}
