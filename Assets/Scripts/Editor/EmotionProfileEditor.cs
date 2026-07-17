using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Editor para EmotionProfile que facilita la configuración visual de las emociones.
/// </summary>
[CustomEditor(typeof(EmotionProfile))]
public class EmotionProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.Space();
        
        // Header
        EditorGUILayout.LabelField("Sistema de Emociones para NPCs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configura qué meshes de ojos/boca se activan para cada emoción y qué animación corporal reproduce el jugador.\n" +
            "Body Anim State Name → nombre exacto del estado en el Animator Controller del jugador (ej: 'Cheer01').",
            MessageType.Info
        );
        
        EditorGUILayout.Space();
        
        // Configuración General
        EditorGUILayout.LabelField("Configuración General", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("transitionDuration"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Configuración de Emociones", EditorStyles.boldLabel);
        
        // Botón para llenar con valores predeterminados
        if (GUILayout.Button("📋 Cargar Nombres Predeterminados", GUILayout.Height(25)))
        {
            EmotionProfile profile = (EmotionProfile)target;
            Undo.RecordObject(profile, "Load Default Mesh Names");
            
            profile.emotions = new EmotionMeshData[]
            {
                new EmotionMeshData { emotion = NPCEmotion.Neutral,   eyeMeshName = "Eye01", mouthMeshName = "Mouth01", bodyAnimStateName = "" },
                new EmotionMeshData { emotion = NPCEmotion.Happy,     eyeMeshName = "Eye03", mouthMeshName = "Mouth03", bodyAnimStateName = "Cheer01" },
                new EmotionMeshData { emotion = NPCEmotion.Sad,       eyeMeshName = "Eye02", mouthMeshName = "Mouth02", bodyAnimStateName = "Cry01" },
                new EmotionMeshData { emotion = NPCEmotion.Angry,     eyeMeshName = "Eye04", mouthMeshName = "Mouth04", bodyAnimStateName = "Angry01" },
                new EmotionMeshData { emotion = NPCEmotion.Surprised, eyeMeshName = "Eye05", mouthMeshName = "Mouth05", bodyAnimStateName = "Question02" },
                new EmotionMeshData { emotion = NPCEmotion.Scared,    eyeMeshName = "Eye06", mouthMeshName = "Mouth06", bodyAnimStateName = "Fear01" },
                new EmotionMeshData { emotion = NPCEmotion.Thinking,  eyeMeshName = "Eye07", mouthMeshName = "Mouth07", bodyAnimStateName = "Question01" },
                new EmotionMeshData { emotion = NPCEmotion.Tired,     eyeMeshName = "Eye08", mouthMeshName = "Mouth08", bodyAnimStateName = "Talk02" },
                new EmotionMeshData { emotion = NPCEmotion.Smirk,     eyeMeshName = "Eye09", mouthMeshName = "Mouth09", bodyAnimStateName = "Laugh01" },
            };
            profile.neutralBodyAnims = new[] { "Talk01", "Talk02", "Talk03" };
            
            EditorUtility.SetDirty(profile);
        }
        
        EditorGUILayout.Space();
        
        // Mapeo de Emociones
        EditorGUILayout.LabelField("Mapeo de Emociones", EditorStyles.boldLabel);
        
        var emotionsProperty = serializedObject.FindProperty("emotions");

        for (int i = 0; i < emotionsProperty.arraySize; i++)
        {
            var element = emotionsProperty.GetArrayElementAtIndex(i);
            var emotionProp  = element.FindPropertyRelative("emotion");
            var eyeProp      = element.FindPropertyRelative("eyeMeshName");
            var mouthProp    = element.FindPropertyRelative("mouthMeshName");
            var bodyAnimProp = element.FindPropertyRelative("bodyAnimStateName");

            NPCEmotion emotion = (NPCEmotion)emotionProp.intValue;
            string label = GetEmotionLabel(emotion);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, label, true);

            if (element.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(emotionProp,  new GUIContent("Emoción"));
                EditorGUILayout.PropertyField(eyeProp,      new GUIContent("👁 Mesh Ojos"));
                EditorGUILayout.PropertyField(mouthProp,    new GUIContent("👄 Mesh Boca"));
                EditorGUILayout.PropertyField(bodyAnimProp, new GUIContent("🏃 Anim Corporal (jugador)"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animaciones Neutras (jugador)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("neutralBodyAnims"), new GUIContent("🗣 Anims Neutral"), true);
        
        EditorGUILayout.Space();
        
        // Botones para añadir/quitar
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Añadir Emoción"))
        {
            emotionsProperty.InsertArrayElementAtIndex(emotionsProperty.arraySize);
        }
        if (emotionsProperty.arraySize > 0 && GUILayout.Button("- Quitar Última"))
        {
            emotionsProperty.DeleteArrayElementAtIndex(emotionsProperty.arraySize - 1);
        }
        EditorGUILayout.EndHorizontal();
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private string GetEmotionLabel(NPCEmotion emotion)
    {
        switch (emotion)
        {
            case NPCEmotion.None: return "⬜ None (Sin cambio)";
            case NPCEmotion.Neutral: return "😐 Neutral";
            case NPCEmotion.Happy: return "😊 Happy (Feliz)";
            case NPCEmotion.Sad: return "😢 Sad (Triste)";
            case NPCEmotion.Angry: return "😠 Angry (Enfadado)";
            case NPCEmotion.Surprised: return "😲 Surprised (Sorprendido)";
            case NPCEmotion.Scared: return "😨 Scared (Asustado)";
            case NPCEmotion.Thinking: return "🤔 Thinking (Pensativo)";
            case NPCEmotion.Tired: return "😴 Tired (Cansado)";
            case NPCEmotion.Smirk: return "😏 Smirk (Sonrisa pícara)";
            default: return emotion.ToString();
        }
    }
}

/// <summary>
/// Custom Editor para NPCEmotionController que muestra información útil.
/// </summary>
[CustomEditor(typeof(NPCEmotionController))]
public class NPCEmotionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space();
        
        NPCEmotionController controller = (NPCEmotionController)target;
        
        // Mostrar información de runtime
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Estado en Runtime", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Emoción Actual", controller.CurrentEmotion.ToString());
                EditorGUILayout.LabelField("En Diálogo", controller.IsInDialogue ? "Sí" : "No");
                EditorGUILayout.LabelField("Meshes Ojos", controller.EyeMeshCount.ToString());
                EditorGUILayout.LabelField("Meshes Boca", controller.MouthMeshCount.ToString());
            }
            
            EditorGUILayout.Space();
            
            // Botones de test
            EditorGUILayout.LabelField("Test de Emociones", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Neutral")) controller.SetEmotion(NPCEmotion.Neutral);
            if (GUILayout.Button("Happy")) controller.SetEmotion(NPCEmotion.Happy);
            if (GUILayout.Button("Sad")) controller.SetEmotion(NPCEmotion.Sad);
            if (GUILayout.Button("Angry")) controller.SetEmotion(NPCEmotion.Angry);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Surprised")) controller.SetEmotion(NPCEmotion.Surprised);
            if (GUILayout.Button("Scared")) controller.SetEmotion(NPCEmotion.Scared);
            if (GUILayout.Button("Thinking")) controller.SetEmotion(NPCEmotion.Thinking);
            if (GUILayout.Button("Tired")) controller.SetEmotion(NPCEmotion.Tired);
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("↩ Restaurar Original"))
            {
                controller.ForceReset();
            }
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para probar las emociones y ver los meshes detectados.",
                MessageType.Info
            );
        }
    }
}

