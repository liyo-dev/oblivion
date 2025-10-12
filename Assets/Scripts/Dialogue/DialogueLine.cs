using UnityEngine;

[System.Serializable]
public struct DialogueLine
{
    [Tooltip("ID de localización para el nombre del hablante (ej: 'CHAR_ALEX'). Si está vacío, usa speakerName directamente.")]
    public string speakerNameId;
    
    [Tooltip("ID de localización para el texto (ej: 'DLG_INTRO_01'). Si está vacío, usa text directamente.")]
    public string textId;

    [Tooltip("Opcional")]
    public Sprite portrait;
}