using Game.NPC.Modules;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dibuja QuestChainEntry y QuestPostAction mostrando solo los campos relevantes según
/// autoDetectItemDelivery / requiredCharacter / postAction.actionType, en vez de todos los campos
/// de golpe como hace el Inspector por defecto. Mismo patrón que NarrativeInteractiveConfigGUI.
/// Usado por NPCQuestConfigEditor.
/// </summary>
public static class NPCQuestConfigGUI
{
    // ────────────────────────────────────────────────────────────────
    // QuestPostAction
    // ────────────────────────────────────────────────────────────────

    public static void DrawPostAction(SerializedProperty actionProp)
    {
        var actionTypeProp = actionProp.FindPropertyRelative("actionType");
        EditorGUILayout.PropertyField(actionTypeProp);
        // intValue, no enumValueIndex: QuestActionType.Custom = 99, muy lejos de su índice de
        // declaración (5). Ver misma nota en NarrativeInteractiveConfigGUI.
        var type = (QuestActionType)actionTypeProp.intValue;

        if (type == QuestActionType.None)
            return;

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Diálogo Pre-Acción", EditorStyles.miniBoldLabel);
        Field(actionProp, "dialogueBeforeAction");
        Field(actionProp, "maxDialogueDistance");

        if (type == QuestActionType.Move || type == QuestActionType.Teleport)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Move/Teleport Settings", EditorStyles.miniBoldLabel);
            Field(actionProp, "targetAnchorName");
            Field(actionProp, "targetTransform");
            if (type == QuestActionType.Move)
                Field(actionProp, "maxMovementDuration");
            Field(actionProp, "walkDisplayDuration");
            Field(actionProp, "turnAroundOnArrival");

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Transition Settings", EditorStyles.miniBoldLabel);
            Field(actionProp, "transitionSettings");
            Field(actionProp, "useTransition");
            Field(actionProp, "transitionDelay");
        }

        if (type == QuestActionType.StartCombat)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Combat Settings", EditorStyles.miniBoldLabel);
            Field(actionProp, "combatTarget");
        }

        if (type == QuestActionType.Dialogue)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Dialogue Settings", EditorStyles.miniBoldLabel);
            Field(actionProp, "dialogueToPlay");
        }

        if (type == QuestActionType.Custom)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Custom Action", EditorStyles.miniBoldLabel);
            Field(actionProp, "customAction");
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Timing", EditorStyles.miniBoldLabel);
        Field(actionProp, "delayBeforeAction");

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Encadenamiento de Quests", EditorStyles.miniBoldLabel);
        Field(actionProp, "chainNextQuestAfterAction");
        if (actionProp.FindPropertyRelative("chainNextQuestAfterAction").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(actionProp, "chainDelay");
            EditorGUI.indentLevel--;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // QuestChainEntry
    // ────────────────────────────────────────────────────────────────

    public static void DrawChainEntry(SerializedProperty entryProp)
    {
        Field(entryProp, "questData");
        Field(entryProp, "completionMode");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Detección de Objetos", EditorStyles.boldLabel);
        Field(entryProp, "autoDetectItemDelivery");
        if (entryProp.FindPropertyRelative("autoDetectItemDelivery").boolValue)
        {
            EditorGUI.indentLevel++;
            Field(entryProp, "itemDeliveryStepIndex");
            Field(entryProp, "itemTag");
            Field(entryProp, "ignoreFovForItem");
            Field(entryProp, "overrideDetectionRadius");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Verificación de Inventario", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Items Requeridos", EditorStyles.miniBoldLabel);
        EditorListGUI.DrawList(
            entryProp.FindPropertyRelative("requiredItems"), typeof(ItemRequirement),
            "+ Añadir Item Requerido",
            (el, i) => ItemLabel(el, i, "item"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Items de Wardrobe Requeridos", EditorStyles.miniBoldLabel);
        EditorListGUI.DrawList(
            entryProp.FindPropertyRelative("requiredWardrobeItems"), typeof(WardrobeItemRequirement),
            "+ Añadir Wardrobe Item Requerido",
            (el, i) => ItemLabel(el, i, "item"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Verificación de Miembros del Equipo", EditorStyles.boldLabel);
        EditorListGUI.DrawList(
            entryProp.FindPropertyRelative("requiredPartyMembers"), typeof(PartyMemberRequirement),
            "+ Añadir Miembro Requerido",
            (el, i) => StringLabel(el, i, "memberId"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Personaje Requerido", EditorStyles.boldLabel);
        var requiredCharProp = entryProp.FindPropertyRelative("requiredCharacter");
        EditorGUILayout.PropertyField(requiredCharProp);
        // intValue, no enumValueIndex: QuestRequiredCharacter.Any = -1 (no es el primer índice
        // declarado en todos los casos de uso genérico). Ver misma nota arriba.
        if ((QuestRequiredCharacter)requiredCharProp.intValue != QuestRequiredCharacter.Any)
        {
            EditorGUI.indentLevel++;
            Field(entryProp, "dlgWrongCharacter");
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Diálogos", EditorStyles.boldLabel);
        Field(entryProp, "dlgBefore");
        Field(entryProp, "dlgInProgress");
        Field(entryProp, "dlgTurnIn");
        Field(entryProp, "dlgCompleted");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Eventos", EditorStyles.boldLabel);
        Field(entryProp, "onQuestCompleted");
        Field(entryProp, "onOfferDialogueStarted");
        Field(entryProp, "onOfferDialogueFinished");
        Field(entryProp, "onPostActionCompleted");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Acción Post-Quest", EditorStyles.boldLabel);
        DrawPostAction(entryProp.FindPropertyRelative("postAction"));
    }

    private static string ItemLabel(SerializedProperty element, int index, string objectFieldName)
    {
        var objProp = element.FindPropertyRelative(objectFieldName);
        var obj = objProp.objectReferenceValue;
        return obj != null ? $"#{index} — {obj.name}" : $"#{index} — (sin asignar)";
    }

    private static string StringLabel(SerializedProperty element, int index, string stringFieldName)
    {
        string value = element.FindPropertyRelative(stringFieldName).stringValue;
        return string.IsNullOrEmpty(value) ? $"#{index} — (sin asignar)" : $"#{index} — {value}";
    }

    private static void Field(SerializedProperty parent, string relativeName)
    {
        EditorGUILayout.PropertyField(parent.FindPropertyRelative(relativeName));
    }
}
