using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Efecto cinematográfico por línea de diálogo.
/// Funciona tanto en modo individual como grupal.
/// </summary>
public enum DialogueLineEffect
{
    None,
    CloseUp
}

[System.Serializable]
public struct DialogueLine
{
    [Tooltip("ID de localización para el nombre del hablante (ej: 'CHAR_ALEX'). Si está vacío, usa speakerName directamente.")]
    public string speakerNameId;

    [Tooltip("ID de localización para el texto (ej: 'DLG_INTRO_01'). Si está vacío, usa 'text' directamente.")]
    public string textId;

    [TextArea]                            
    public string text;

    [Tooltip("Opcional")]
    public Sprite portrait;
    
    [Header("Cinematografía")]
    [Tooltip("¿Quién habla en esta línea? (true = jugador, false = NPC)")]
    public bool isPlayerSpeaking;
    
    [Tooltip("Efecto cinematográfico para esta línea (None = automático)")]
    [FormerlySerializedAs("forcedShotType")]
    public DialogueLineEffect cinematicEffect;
    
    [Header("Emociones")]
    [Tooltip("Emoción del NPC durante esta línea (None = sin cambio, mantiene la emoción anterior)")]
    public NPCEmotion emotion;
}