using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dibuja un array/lista serializada de una clase [Serializable] "plana" (sin campos que dependan de
/// un enum interno) como una serie de cajas plegables, con "+"/"✕" que nunca duplican el último
/// elemento (ver SerializedArrayUtils). Pensado para reutilizar en cualquier SO del proyecto que tenga
/// listas de requisitos/registros simples (ItemRequirement, WardrobeItemRequirement,
/// PartyMemberRequirement, etc.).
/// </summary>
public static class EditorListGUI
{
    /// <summary>
    /// Dibuja la lista completa: elementos existentes (cada uno con todos sus campos hijos vía
    /// PropertyField) más el botón de añadir.
    /// </summary>
    /// <param name="listProp">Propiedad de array/lista.</param>
    /// <param name="elementType">Tipo C# del elemento, para poder resetearlo al añadir uno nuevo.</param>
    /// <param name="addButtonLabel">Texto del botón de añadir.</param>
    /// <param name="labelGetter">Función opcional para obtener el título de cada elemento en su foldout.</param>
    public static void DrawList(SerializedProperty listProp, Type elementType, string addButtonLabel, Func<SerializedProperty, int, string> labelGetter = null)
    {
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elementProp = listProp.GetArrayElementAtIndex(i);
            string label = labelGetter != null ? labelGetter(elementProp, i) : $"#{i}";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            elementProp.isExpanded = EditorGUILayout.Foldout(elementProp.isExpanded, label, true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                listProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return; // el resto del array se redibuja correctamente en el próximo frame
            }
            EditorGUILayout.EndHorizontal();

            if (elementProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawAllChildren(elementProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button(addButtonLabel))
        {
            var newElement = SerializedArrayUtils.AddElementReset(listProp, elementType);
            newElement.isExpanded = true;
        }
    }

    /// <summary>Dibuja todos los campos hijos directos de una propiedad (sin agrupar por tipo).</summary>
    private static void DrawAllChildren(SerializedProperty parentProp)
    {
        SerializedProperty prop = parentProp.Copy();
        SerializedProperty end = parentProp.GetEndProperty();
        bool first = true;

        while (prop.NextVisible(first) && !SerializedProperty.EqualContents(prop, end))
        {
            EditorGUILayout.PropertyField(prop, true);
            first = false;
        }
    }
}
