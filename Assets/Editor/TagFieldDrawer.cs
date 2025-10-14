using UnityEngine;
using UnityEditor;

namespace Editor
{
    /// <summary>
    /// Property drawer para mostrar un dropdown con todos los tags disponibles en el proyecto
    /// </summary>
    [CustomPropertyDrawer(typeof(TagFieldAttribute))]
    public class TagFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                EditorGUI.BeginProperty(position, label, property);
                
                // Usar el TagField de Unity que muestra un dropdown con todos los tags
                property.stringValue = EditorGUI.TagField(position, label, property.stringValue);
                
                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }
        }
    }
}
