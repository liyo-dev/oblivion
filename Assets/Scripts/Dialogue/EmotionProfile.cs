using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject que define cómo se mapean las emociones a meshes de ojos y boca.
/// Diseñado para personajes que usan GameObjects intercambiables (Eye01, Eye02, Mouth01, etc.)
/// </summary>
[CreateAssetMenu(fileName = "NewEmotionProfile", menuName = "El Sendero/Diálogos/Emotion Profile")]
public class EmotionProfile : ScriptableObject
{
    [Header("Configuración General")]
    [Tooltip("Tiempo de transición entre emociones (para futuras animaciones)")]
    [Range(0f, 1f)]
    public float transitionDuration = 0.1f;
    
    [Header("Mapeo de Emociones")]
    [Tooltip("Configuración de meshes faciales y animación corporal para cada emoción")]
    public EmotionMeshData[] emotions = new EmotionMeshData[]
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

    [Header("Animaciones Neutras (jugador)")]
    [Tooltip("Estados del Animator que se rotan cuando la emoción es None o Neutral")]
    public string[] neutralBodyAnims = { "Talk01", "Talk02", "Talk03" };
    
    /// <summary>
    /// Obtiene la configuración de meshes para una emoción específica.
    /// </summary>
    public EmotionMeshData GetEmotionData(NPCEmotion emotion)
    {
        foreach (var data in emotions)
        {
            if (data.emotion == emotion)
                return data;
        }
        
        // Fallback a neutral si no se encuentra
        return emotions.Length > 0 ? emotions[0] : default;
    }
}

/// <summary>
/// Datos de configuración para una emoción específica.
/// Define qué meshes de ojos y boca activar, y qué estado del Animator reproducir en el cuerpo.
/// </summary>
[Serializable]
public struct EmotionMeshData
{
    [Tooltip("La emoción que configura este dato")]
    public NPCEmotion emotion;

    [Header("Cara")]
    [Tooltip("Nombre del GameObject de ojos a activar (ej: 'Eye01', 'Eye03')")]
    public string eyeMeshName;

    [Tooltip("Nombre del GameObject de boca a activar (ej: 'Mouth01', 'Mouth03')")]
    public string mouthMeshName;

    [Header("Animación Corporal (jugador)")]
    [Tooltip("Nombre del estado en el Animator Controller del jugador (ej: 'Cheer01', 'Cry01'). Vacío = sin animación específica.")]
    public string bodyAnimStateName;
}

