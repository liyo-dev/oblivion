using Game.NPC.Modules;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Editor para NPCQuestConfig. Igual que NPCInteractiveNarrativeConfigEditor: evita que el
/// Inspector por defecto muestre todos los campos de todos los tipos de postAction a la vez, y evita
/// que "+" duplique la última quest de la cadena en vez de crear una entrada vacía.
/// </summary>
[CustomEditor(typeof(NPCQuestConfig))]
public class NPCQuestConfigEditor : Editor
{
    private SerializedProperty _questChain;

    private void OnEnable()
    {
        _questChain = serializedObject.FindProperty("questChain");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Cadena de misiones que ofrece este NPC. Cada elemento representa una quest en orden.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quest Chain", EditorStyles.boldLabel);

        for (int i = 0; i < _questChain.arraySize; i++)
        {
            var entryProp = _questChain.GetArrayElementAtIndex(i);
            var questDataProp = entryProp.FindPropertyRelative("questData");
            var questObj = questDataProp.objectReferenceValue;
            string label = questObj != null ? $"#{i} — {questObj.name}" : $"Quest #{i} (sin asignar)";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, label, true, EditorStyles.foldoutHeader);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                _questChain.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            if (entryProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                NPCQuestConfigGUI.DrawChainEntry(entryProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        if (GUILayout.Button("+ Añadir Quest a la Cadena", GUILayout.Height(24)))
        {
            var newEntry = SerializedArrayUtils.AddElementReset(_questChain, typeof(QuestChainEntry));
            newEntry.isExpanded = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Item Detection (Global)", EditorStyles.boldLabel);
        DrawProp("enableItemDetection");
        DrawProp("detectionRadius");
        DrawProp("detectionAngle");
        DrawProp("detectionLayer");
        DrawProp("detectionInterval");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Auto-Detection", EditorStyles.boldLabel);
        DrawProp("autoStartOnDetection");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
        DrawProp("rotationSpeed");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Persistent Icon", EditorStyles.boldLabel);
        DrawProp("questIconPrefab");
        DrawProp("questIconOffset");
        DrawProp("showIconWhenQuestAvailable");
        DrawProp("showIconWhenQuestInProgress");
        DrawProp("showIconWhenQuestReadyToTurnIn");
        DrawProp("turnInIconPrefab");
        DrawProp("hideIconWhenAllCompleted");

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawProp(string name)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
    }
}
