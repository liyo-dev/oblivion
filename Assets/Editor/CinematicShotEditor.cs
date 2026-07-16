using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CinematicShot))]
public class CinematicShotEditor : Editor
{
    private bool _live;

    void OnEnable()  => SceneView.duringSceneGui += SyncView;
    void OnDisable() { SceneView.duringSceneGui -= SyncView; _live = false; }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);

        GUI.backgroundColor = _live ? new Color(1f, 0.35f, 0.35f) : new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button(_live ? "● Live Preview ACTIVO  —  clic para desactivar"
                                   : "○ Activar Live Preview", GUILayout.Height(30)))
            _live = !_live;
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(4);

        if (_live)
            EditorGUILayout.HelpBox(
                "La Scene View sigue a esta cámara.\n" +
                "Mueve o rota el GO con los handles y verás el resultado en directo.",
                MessageType.None);
        else
            EditorGUILayout.HelpBox(
                "Selecciona este GO → la esquina inferior derecha de la Scene View " +
                "muestra en tiempo real lo que verá esta cámara.",
                MessageType.None);
    }

    void SyncView(SceneView sv)
    {
        if (!_live || target == null) return;
        var t = ((CinematicShot)target).transform;
        // Colocar el pivot justo delante del GO para que la Scene View mire desde él
        sv.pivot    = t.position + t.forward * Mathf.Max(sv.cameraDistance, 0.3f);
        sv.rotation = t.rotation;
        sv.Repaint();
    }
}
