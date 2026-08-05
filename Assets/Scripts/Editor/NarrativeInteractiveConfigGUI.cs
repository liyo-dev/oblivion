using Game.NPC.Modules;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dibuja NarrativeCondition, NarrativeChainEntry y ConditionalNarrative mostrando SOLO los campos
/// relevantes para el tipo seleccionado (conditionType / actionType), en vez de todos los campos de
/// todos los tipos como hace el Inspector por defecto de Unity con arrays de clases [Serializable].
///
/// Usado por NPCInteractiveNarrativeConfigEditor. Si se necesita el mismo patrón para otro SO
/// (ej. QuestChainEntry en NPCQuestConfig), replicar esta clase con los nombres de campo de esa clase.
/// </summary>
public static class NarrativeInteractiveConfigGUI
{
    // ────────────────────────────────────────────────────────────────
    // NarrativeCondition
    // ────────────────────────────────────────────────────────────────

    public static void DrawCondition(SerializedProperty conditionProp)
    {
        var conditionTypeProp = conditionProp.FindPropertyRelative("conditionType");
        EditorGUILayout.PropertyField(conditionTypeProp);

        // OJO: usar intValue, no enumValueIndex. enumValueIndex es la posición en el popup;
        // intValue es el valor entero real del enum. Para enums con huecos o valores negativos
        // (como QuestActionType.Custom=99 o QuestRequiredCharacter.Any=-1) ambos difieren.
        var type = (NarrativeConditionType)conditionTypeProp.intValue;

        bool needsQuest =
            type == NarrativeConditionType.QuestNotStarted ||
            type == NarrativeConditionType.QuestStarted ||
            type == NarrativeConditionType.QuestCompleted ||
            type == NarrativeConditionType.QuestActive ||
            type == NarrativeConditionType.QuestStepCompleted ||
            type == NarrativeConditionType.QuestNotCompleted;

        if (needsQuest)
        {
            EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("targetQuest"));
        }

        if (type == NarrativeConditionType.QuestStepCompleted)
        {
            EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("targetStepConditionId"));
        }

        if (type == NarrativeConditionType.Custom)
        {
            EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("customEventKey"));
        }

        if (type != NarrativeConditionType.None)
        {
            EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("debugMode"));
        }
    }

    // ────────────────────────────────────────────────────────────────
    // NarrativeChainEntry
    // ────────────────────────────────────────────────────────────────

    public static void DrawChainEntry(SerializedProperty entryProp)
    {
        var actionTypeProp = entryProp.FindPropertyRelative("actionType");
        EditorGUILayout.PropertyField(actionTypeProp);

        // intValue, no enumValueIndex: NarrativeActionType tiene un hueco deliberado en el 12
        // (ShowSpeechBubble eliminado), así que TeleportPlayer(13)/ScreenFade(14) no coinciden
        // con su índice de declaración.
        var type = (NarrativeActionType)actionTypeProp.intValue;

        EditorGUILayout.Space(2);

        switch (type)
        {
            case NarrativeActionType.Dialogue:
                Field(entryProp, "dialogue");
                break;

            case NarrativeActionType.Move:
                Field(entryProp, "targetAnchorName");
                Field(entryProp, "targetTransform");
                Field(entryProp, "moveToRandomPoint");
                if (entryProp.FindPropertyRelative("moveToRandomPoint").boolValue)
                {
                    EditorGUI.indentLevel++;
                    Field(entryProp, "randomMoveMinRadius");
                    Field(entryProp, "randomMoveMaxRadius");
                    EditorGUI.indentLevel--;
                }
                Field(entryProp, "maxMovementDuration");
                Field(entryProp, "turnAroundOnArrival");
                Field(entryProp, "moveTeamMembers");
                Field(entryProp, "disappearOnArrival");
                if (entryProp.FindPropertyRelative("disappearOnArrival").boolValue)
                {
                    EditorGUI.indentLevel++;
                    Field(entryProp, "disappearTransition");
                    EditorGUI.indentLevel--;
                }
                Field(entryProp, "waitForPlayer");
                if (entryProp.FindPropertyRelative("waitForPlayer").boolValue)
                {
                    EditorGUI.indentLevel++;
                    Field(entryProp, "maxPlayerDistance");
                    Field(entryProp, "resumePlayerDistance");
                    EditorGUI.indentLevel--;
                }
                break;

            case NarrativeActionType.MoveNearPlayer:
                Field(entryProp, "nearPlayerRadius");
                Field(entryProp, "maxMovementDuration");
                Field(entryProp, "lookAtPlayerOnArrival");
                break;

            case NarrativeActionType.TeleportNearPlayer:
                Field(entryProp, "teleportNearPlayerRadius");
                Field(entryProp, "lookAtPlayerOnArrival");
                Field(entryProp, "disappearTransition");
                break;

            case NarrativeActionType.LeadPlayerToAnchor:
                Field(entryProp, "targetAnchorName");
                Field(entryProp, "targetTransform");
                Field(entryProp, "escortMaxDuration");
                Field(entryProp, "escortMaxPlayerDistance");
                Field(entryProp, "escortResumeDistance");
                Field(entryProp, "escortNpcSpeed");
                Field(entryProp, "escortPlayerSpeedMultiplier");
                Field(entryProp, "outOfRangeDialogue");
                Field(entryProp, "turnAroundOnArrival");
                break;

            case NarrativeActionType.TeleportPlayer:
                Field(entryProp, "targetAnchorName");
                Field(entryProp, "targetTransform");
                Field(entryProp, "teleportTransition");
                break;

            case NarrativeActionType.PlayAnimation:
                Field(entryProp, "animationTrigger");
                Field(entryProp, "animationClip");
                Field(entryProp, "animationDuration");
                break;

            case NarrativeActionType.StartQuest:
                Field(entryProp, "questToStart");
                break;

            case NarrativeActionType.StartCombat:
                Field(entryProp, "combatConfig");
                Field(entryProp, "combatTarget");
                Field(entryProp, "sendEventOnDefeat");
                if (entryProp.FindPropertyRelative("sendEventOnDefeat").boolValue)
                {
                    EditorGUI.indentLevel++;
                    Field(entryProp, "defeatEventKey");
                    Field(entryProp, "sendDefeatEventBeforeDeath");
                    EditorGUI.indentLevel--;
                }
                break;

            case NarrativeActionType.Wait:
                Field(entryProp, "waitDuration");
                break;

            case NarrativeActionType.ScreenFade:
                Field(entryProp, "screenFadeTransition");
                Field(entryProp, "fadeIn");
                Field(entryProp, "fadeColor");
                Field(entryProp, "fadeDuration");
                break;

            case NarrativeActionType.JoinParty:
            case NarrativeActionType.LeaveParty:
            case NarrativeActionType.CheckPartyMembers:
                EditorGUILayout.HelpBox("Esta acción no necesita configuración adicional.", MessageType.None);
                break;
        }

        // ── Secciones transversales: aplican a cualquier actionType ──
        EditorGUILayout.Space(4);
        Field(entryProp, "showAlertIcon");
        if (entryProp.FindPropertyRelative("showAlertIcon").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(entryProp, "alertIconPrefab");
            Field(entryProp, "alertIconDuration");
            Field(entryProp, "alertIconOffset");
            EditorGUI.indentLevel--;
        }

        Field(entryProp, "sendNarrativeEvent");
        if (entryProp.FindPropertyRelative("sendNarrativeEvent").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(entryProp, "narrativeEventKey");
            Field(entryProp, "sendEventOnStart");
            EditorGUI.indentLevel--;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // ConditionalNarrative (incluye su array narrativeChain)
    // ────────────────────────────────────────────────────────────────

    public static void DrawConditionalNarrative(SerializedProperty narrativeProp)
    {
        Field(narrativeProp, "description");
        Field(narrativeProp, "priority");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Condición", EditorStyles.boldLabel);
        DrawCondition(narrativeProp.FindPropertyRelative("condition"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Comportamiento de Ejecución", EditorStyles.boldLabel);
        Field(narrativeProp, "singleUse");
        Field(narrativeProp, "autoStartOnDetection");
        Field(narrativeProp, "autoExecuteOnQuestConditionMet");
        if (narrativeProp.FindPropertyRelative("autoStartOnDetection").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(narrativeProp, "freezePlayerOnDetection");
            EditorGUI.indentLevel--;
        }
        Field(narrativeProp, "lockPlayerAfterChain");
        if (narrativeProp.FindPropertyRelative("lockPlayerAfterChain").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(narrativeProp, "lockPlayerMaxDuration");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Estado Post-Narrativa", EditorStyles.boldLabel);
        var postStateProp = narrativeProp.FindPropertyRelative("postNarrativeState");
        EditorGUILayout.PropertyField(postStateProp);
        if ((PostNarrativeState)postStateProp.intValue == PostNarrativeState.SwitchToAmbient)
        {
            EditorGUI.indentLevel++;
            Field(narrativeProp, "postNarrativeAmbientConfig");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Icono Persistente", EditorStyles.boldLabel);
        Field(narrativeProp, "showPersistentIcon");
        if (narrativeProp.FindPropertyRelative("showPersistentIcon").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(narrativeProp, "persistentIconPrefab");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Evento al Grafo Narrativo", EditorStyles.boldLabel);
        Field(narrativeProp, "sendNarrativeEvent");
        if (narrativeProp.FindPropertyRelative("sendNarrativeEvent").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(narrativeProp, "narrativeEventKey");
            EditorGUI.indentLevel--;
        }

        Field(narrativeProp, "debugMode");

        // ── Cadena narrativa ──
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Cadena Narrativa", EditorStyles.boldLabel);

        var chainProp = narrativeProp.FindPropertyRelative("narrativeChain");
        for (int i = 0; i < chainProp.arraySize; i++)
        {
            var entryProp = chainProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            var actionTypeProp = entryProp.FindPropertyRelative("actionType");
            var actionType = (NarrativeActionType)actionTypeProp.intValue;
            entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, $"#{i} — {actionType}", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                chainProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break; // el layout de este frame ya no es válido tras borrar; se redibuja en el siguiente
            }
            EditorGUILayout.EndHorizontal();

            if (entryProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawChainEntry(entryProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ Añadir Acción a la Cadena"))
        {
            var newEntry = SerializedArrayUtils.AddElementReset(chainProp, typeof(NarrativeChainEntry));
            newEntry.isExpanded = true;
        }
    }

    private static void Field(SerializedProperty parent, string relativeName)
    {
        EditorGUILayout.PropertyField(parent.FindPropertyRelative(relativeName));
    }
}
