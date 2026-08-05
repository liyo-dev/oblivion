using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilidad genérica para el bug clásico de Unity: al pulsar "+" en un array/lista serializada,
/// <see cref="SerializedProperty.InsertArrayElementAtIndex"/> NO crea un elemento nuevo con valores
/// por defecto — copia los valores del último elemento existente. Esto hace que, por ejemplo, al
/// añadir una segunda "NarrativeChainEntry" tras rellenar una de tipo Move, la nueva entrada aparezca
/// ya rellena como si fuera una copia de la anterior.
///
/// Usar <see cref="AddElementReset"/> en vez de llamar directamente a InsertArrayElementAtIndex desde
/// cualquier CustomEditor/PropertyDrawer que gestione un array de una clase [Serializable] con
/// constructor sin parámetros. Aplicar este mismo patrón a cualquier otro SO del proyecto que tenga
/// el mismo problema (ver CLAUDE.md § mejora de SOs).
/// </summary>
public static class SerializedArrayUtils
{
    /// <summary>
    /// Añade un elemento nuevo al final de <paramref name="arrayProperty"/> con valores por defecto
    /// de <paramref name="elementType"/>, en vez de duplicar el último elemento existente.
    /// </summary>
    /// <param name="arrayProperty">Propiedad de tipo array o List serializada.</param>
    /// <param name="elementType">
    /// Tipo C# del elemento (ej: typeof(NarrativeChainEntry)). Debe tener constructor sin parámetros.
    /// Si es null, o el tipo no es instanciable, se hace un reset best-effort campo a campo.
    /// </param>
    /// <returns>La SerializedProperty del elemento recién insertado, ya reseteado.</returns>
    public static SerializedProperty AddElementReset(SerializedProperty arrayProperty, Type elementType)
    {
        int index = arrayProperty.arraySize;
        arrayProperty.InsertArrayElementAtIndex(index);
        SerializedProperty newElement = arrayProperty.GetArrayElementAtIndex(index);
        ResetElement(newElement, elementType);
        return newElement;
    }

    /// <summary>
    /// Resetea un elemento ya insertado a valores por defecto. Usa boxedValue (Unity 2022+) con una
    /// instancia nueva del tipo cuando es posible; si no, cae a un reset genérico recorriendo hijos.
    /// </summary>
    public static void ResetElement(SerializedProperty element, Type elementType)
    {
        if (elementType != null && !elementType.IsAbstract && !typeof(UnityEngine.Object).IsAssignableFrom(elementType))
        {
            try
            {
                object freshInstance = Activator.CreateInstance(elementType);
                element.boxedValue = freshInstance;
                return;
            }
            catch (Exception)
            {
                // El tipo no tiene constructor sin parámetros (o algo raro): fallback abajo.
            }
        }

        ResetChildrenGeneric(element);
    }

    /// <summary>
    /// Fallback genérico: recorre todas las propiedades visibles hijas y las pone a su valor por
    /// defecto según su tipo (0 / false / "" / null / arrays vacíos). No requiere conocer el tipo C#.
    /// </summary>
    private static void ResetChildrenGeneric(SerializedProperty element)
    {
        SerializedProperty prop = element.Copy();
        SerializedProperty end = element.GetEndProperty();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren) && !SerializedProperty.EqualContents(prop, end))
        {
            enterChildren = true;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = Color.white;
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = Vector2.zero;
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = Vector3.zero;
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = Vector4.zero;
                    break;
                case SerializedPropertyType.ArraySize:
                    // No tocar arraySize aquí: se gestiona con DeleteArrayElementAtIndex si hiciera falta.
                    enterChildren = false;
                    break;
                case SerializedPropertyType.Generic:
                    // Se entra en sus hijos en la siguiente iteración (enterChildren = true).
                    break;
                default:
                    // Tipos no cubiertos (LayerMask, Gradient, etc.): se dejan como están.
                    break;
            }
        }
    }
}
