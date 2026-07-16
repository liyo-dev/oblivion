using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CinematicShotPreview))]
public class CinematicShotPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var shotsProp = serializedObject.FindProperty("shots");
        if (shotsProp == null || shotsProp.arraySize == 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("Asigna planos en el array 'Shots' de arriba.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Previsualizar plano (sin Play)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Haz clic en un plano para mover la Scene View a esa cámara.\n" +
            "Selecciona el GO en Hierarchy para ver el frustum y el panel de preview.",
            MessageType.None);
        EditorGUILayout.Space(4);

        for (int i = 0; i < shotsProp.arraySize; i++)
        {
            var t = shotsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (t == null)
            {
                EditorGUILayout.HelpBox($"Shot [{i}] no asignado.", MessageType.Warning);
                continue;
            }

            string shotLabel = t.TryGetComponent(out CinematicShot cs) && !string.IsNullOrEmpty(cs.label)
                ? cs.label
                : t.name;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button($"▶  [{i}]  {shotLabel}"))
            {
                AlignSceneViewTo(t);
                // Seleccionar el GO para ver el frustum y poder ajustar en Inspector
                Selection.activeGameObject = t.gameObject;
            }

            if (GUILayout.Button("Ping", GUILayout.Width(46)))
                EditorGUIUtility.PingObject(t.gameObject);

            EditorGUILayout.EndHorizontal();
        }
    }

    static void AlignSceneViewTo(Transform t)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;

        // Posicionar la Scene View exactamente donde está la cámara del plano
        sv.pivot    = t.position + t.forward * sv.cameraDistance;
        sv.rotation = t.rotation;
        sv.Repaint();
    }
}
