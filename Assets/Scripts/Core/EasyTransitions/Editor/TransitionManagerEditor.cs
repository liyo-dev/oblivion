using UnityEngine;
using UnityEditor;

namespace EasyTransition
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(TransitionManager))]
    public class TransitionManagerEditor : UnityEditor.Editor
    {

        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space();
        }
    }

}

