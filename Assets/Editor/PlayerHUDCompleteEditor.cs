using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(PlayerHUDComplete))]
    public class PlayerHUDCompleteEditor : global::UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(8);
            if (GUILayout.Button("Create HUD in Scene"))
            {
                // Llama al menú que crea el HUD
                CreatePlayerHUDEditor.CreatePlayerHUD();
            }

            if (GUILayout.Button("Assign Scene HUD (PlayerHUD_Main)"))
            {
                var t = (PlayerHUDComplete)target;
                var main = GameObject.Find("PlayerHUD_Main");
                if (main)
                {
                    var so = new SerializedObject(t);
                    var prop = so.FindProperty("editorRootPanel");
                    if (prop != null) prop.objectReferenceValue = main;
                    var prop2 = so.FindProperty("editorCanvas");
                    var canvas = main.GetComponentInParent<Canvas>();
                    if (prop2 != null) prop2.objectReferenceValue = canvas;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(t);
                    Debug.Log("PlayerHUDComplete: referencias asignadas desde inspector.");
                }
                else
                {
                    Debug.LogWarning("No se encontró 'PlayerHUD_Main' en la escena. Genera el HUD primero (Create HUD in Scene).");
                }
            }
        }
    }
}
